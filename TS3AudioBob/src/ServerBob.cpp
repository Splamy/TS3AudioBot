#include "ServerBob.hpp"

#include "ServerConnection.hpp"
#include "TsApi.hpp"
#include "User.hpp"
#include "Utils.hpp"

#include <public_errors.h>

#include <algorithm>
#include <sstream>
#include <stdexcept>

const std::vector<std::string> ServerBob::quitMessages =
	{ "I'm outta here", "You're boring", "Have a nice day", "Bye", "Good night",
	  "Nothing to do here", "Taking a break", "Lorem ipsum dolor sit amet...",
	  "Nothing can hold me back", "It's getting quiet", "Drop the bazzzzzz" };

ServerBob::ServerBob(std::shared_ptr<TsApi> tsApi, uint64_t botAdminGroup) :
	rootCommand("root command"),
	audioOn(false),
	botAdminGroup(botAdminGroup),
	tsApi(tsApi)
{
	audio::Player::init();

	// Register commands
	addCommand("error", &ServerBob::errorCommand, "", false);
	addCommand("unknown", &ServerBob::errorCommand, "", false);
	addCommand("help", &ServerBob::helpCommand, "Gives you this handy command list");
	addCommand("help music", &ServerBob::helpMusicCommand);
	addCommand("ping", &ServerBob::pingCommand, "Returns with a pong if the Bob is alive");
	addCommand("exit", &ServerBob::exitCommand, "Let the Bob go home");
	addCommand("audio [on|off]", &ServerBob::audioCommand, "Let the bob send or be silent");
	addCommand("music start <address>", &ServerBob::musicStartCommand, "Starts playing music from the given address");
	addCommand("music volume <0-1>", &ServerBob::musicVolumeCommand, "Change the music volume");
	addCommand("music seek <second>", &ServerBob::musicSeekCommand, "Sets the current position to a specific second");
	addCommand("music loop [on|off]", &ServerBob::musicLoopCommand, "Control more music properties");
	addCommand("music stop", &ServerBob::musicStopCommand);
	addCommand("music pause", &ServerBob::musicPauseCommand);
	addCommand("music unpause", &ServerBob::musicUnpauseCommand);
	addCommand("music address", &ServerBob::musicAddressCommand);
	addCommand("whisper clear", &ServerBob::whisperClearCommand, "Clear the whisperlist");
	addCommand("whisper client add <id>", &ServerBob::whisperClientAddCommand, "Add or remove clients from the whisperlist");
	addCommand("whisper client remove <id>", &ServerBob::whisperClientRemoveCommand);
	addCommand("whisper channel add <id>", &ServerBob::whisperChannelAddCommand, "Add or remove channels from the whisperlist");
	addCommand("whisper channel remove <id>", &ServerBob::whisperChannelRemoveCommand);
	addCommand("status whisper", &ServerBob::statusWhisperCommand, "Get status information");
	addCommand("status audio", &ServerBob::statusAudioCommand);
	addCommand("status music", &ServerBob::statusMusicCommand);
	addCommand("list clients", &ServerBob::listClientsCommand, "Lists all connected clients or channels");
	addCommand("list channels", &ServerBob::listChannelsCommand);

	// Get currently active connections
	uint64_t *handlerIds;
	if (!this->tsApi->handleTsError(this->tsApi->getFunctions().getServerConnectionHandlerList(&handlerIds)))
		throw std::runtime_error("Can't fetch server connections");
	for (uint64_t *handlerId = handlerIds; *handlerId != 0; handlerId++)
		connections.emplace_back(tsApi, *handlerId);
	this->tsApi->getFunctions().freeMemory(handlerIds);

	// Set audio to default state
	setAudio(audioOn);
}

ServerBob::ServerBob(ServerBob &&bob) :
	rootCommand(std::move(bob.rootCommand)),
	connections(std::move(bob.connections)),
	audioOn(bob.audioOn),
	qualityOn(bob.qualityOn),
	botAdminGroup(bob.botAdminGroup),
	tsApi(std::move(bob.tsApi))
{
}

void ServerBob::gotDbId(uint64_t handlerId, const char *uniqueId, uint64_t dbId)
{
	ServerConnection *connection = getServer(handlerId);
	std::vector<User*> users = connection->getUsers(uniqueId);
	for (User *user : users)
	{
		if (!user->hasDbId())
		{
			user->setDbId(dbId);
			user->requestGroupUpdate();
		}
	}
}

void ServerBob::gotServerGroup(uint64_t handlerId, uint64_t dbId, uint64_t serverGroup)
{
	ServerConnection *connection = getServer(handlerId);
	std::vector<User*> users = connection->getUsers(dbId);
	if (users.empty())
		tsApi->log("User not found");
	for (User *user : users)
	{
		user->addGroup(serverGroup);
		if (serverGroup == botAdminGroup)
		{
			user->setGroupsInitialized(true);
			// Execute left over commands
			while (user->hasCommands())
				executeCommand(connection, user, user->dequeueCommand());
		}
	}
}

void ServerBob::addServer(uint64_t handlerId)
{
	connections.emplace_back(tsApi, handlerId);
	connections.back().setAudio(audioOn);
}

void ServerBob::removeServer(uint64_t handlerId)
{
	for (std::vector<ServerConnection>::iterator it = connections.begin();
	     it != connections.end(); it++)
	{
		if (it->getHandlerId() == handlerId)
		{
			connections.erase(it);
			return;
		}
	}
	tsApi->log("Can't find server id to remove");
}

bool ServerBob::fillAudioData(uint64_t handlerId, uint8_t *buffer,
	size_t length, int channelCount, bool sending)
{
	for (ServerConnection &connection : connections)
	{
		if (connection.getHandlerId() == handlerId)
			return connection.fillAudioData(buffer, length, channelCount, sending);
	}
	return false;
}

ServerConnection* ServerBob::getServer(uint64_t handlerId)
{
	for (ServerConnection &connection : connections)
	{
		if (connection.getHandlerId() == handlerId)
			return &connection;
	}
	return nullptr;
}

void ServerBob::handleCommand(uint64_t handlerId, anyID sender,
	const char *uniqueId, const std::string &message)
{
	// Search the connection and the user
	std::vector<ServerConnection>::iterator connection =
		std::find_if(connections.begin(), connections.end(),
		[handlerId](const ServerConnection &connection)
			{ return connection.getHandlerId() == handlerId; });

	if (connection == connections.end())
	{
		tsApi->log("Server connection for command not found");
		return;
	}

	User *user = connection->getUser(sender);
	if (!user)
	{
		// Try to get that user
		connection->addUser(sender, uniqueId);
		user = connection->getUser(sender);
	}
	// Enqueue the message
	user->enqueueCommand(message);
	std::string noNewline = message;
	Utils::replace(noNewline, "\n", "\\n");
	tsApi->log("Enqueued command '{0}'", noNewline);
	// Execute commands
	while (user->hasCommands())
		executeCommand(&(*connection), user, user->dequeueCommand());
}

void ServerBob::executeCommand(ServerConnection *connection, User *sender,
	const std::string &message)
{
	if (!sender->inGroup(botAdminGroup))
	{
		tsApi->log("Unauthorized access from {0}", sender->getId());
		connection->sendCommand(sender, "error access denied");
		return;
	}
	std::string noNewline = message;
	Utils::replace(noNewline, "\n", "\\n");
	tsApi->log("Executing command '{0}'", noNewline);

	// Search the right command
	CommandResult res = rootCommand(connection, sender, message);
	if (!res)
	{
		if (!res.errorMessage.empty())
			connection->sendCommand(sender, res.errorMessage);
		else
			unknownCommand(connection, sender, message);
	}
}

template <class... Args>
void ServerBob::addCommand(const std::string &command, CommandResult (ServerBob::*fun)
	(ServerConnection*, User*, const std::string&, Args...),
	const std::string &description, bool displayDescription)
{
	addCommand(command, Utils::myBindMember(fun, this), description, displayDescription);
}

template <class... Args>
void ServerBob::addCommand(const std::string &command, std::function<
	CommandResult(ServerConnection*, User*, const std::string&, Args...)> fun,
	const std::string &description, bool displayDescription)
{
	rootCommand.addCommand(command, fun, description, displayDescription);
}

void ServerBob::setAudio(bool on)
{
	audioOn = on;
	for (ServerConnection &connection : connections)
		connection.setAudio(on);
}

void ServerBob::setQuality(bool on)
{
	qualityOn = on;
	for (ServerConnection &connection : connections)
		connection.setQuality(on);
}

std::string ServerBob::combineHelp(
	std::vector<std::pair<std::string, std::string> > descriptions)
{
	std::ostringstream output;

	// Find the maximum command length to align the commands
	std::size_t maxLength = 0;
	for (const auto &desc : descriptions)
	{
		std::size_t s = desc.first.length();
		if (s > maxLength)
			maxLength = s;
	}
	std::ostringstream fStream;
	// Align the output to s characters
	// Each line has two spaces at the beginning because TeamSpeak will delete
	// one single whitespace at the start of a line which leads to bad alignment.
	fStream << "\n  {0:-" << maxLength << "}    {1}";
	const std::string format = fStream.str();
	for (const auto &desc : descriptions)
		output << Utils::format(format, desc.first, desc.second);
	return output.str();
}

void ServerBob::close()
{
	std::string msg = quitMessages[Utils::getRandomNumber(0, quitMessages.size() - 1)];
	for (ServerConnection &connection : connections)
		connection.close(msg);
	connections.clear();
	// "Graceful" exit
	exit(EXIT_SUCCESS);
}

void ServerBob::unknownCommand(ServerConnection *connection,
	User *sender, const std::string &message)
{
	std::string msg = message;
	Utils::replace(msg, "\n", "\\n");
	tsApi->log(Utils::format("Unknown command: {0}", msg));
	// Send error message
	connection->sendCommand(sender, "error unknown command {0}", msg);
	
}

// Commands
CommandResult ServerBob::errorCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*rest*/)
{
	tsApi->log("Loop detected, have fun");
	return CommandResult();
}

CommandResult ServerBob::audioCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, bool on)
{
	setAudio(on);
	return CommandResult();
}

CommandResult ServerBob::musicStartCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, std::string address)
{
	// Strip [URL] and [/URL]
	if (Utils::startsWith(address, "[URL]"))
		address = address.substr(5);
	if (Utils::endsWith(address, "[/URL]"))
		address = address.substr(0, address.length() - 6);

	// Start an audio stream
	connection->startAudio(address);
	return CommandResult();
}

CommandResult ServerBob::musicVolumeCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, double volume)
{
	connection->setVolume(volume);
	return CommandResult();
}

CommandResult ServerBob::musicSeekCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, double position)
{
	if (!connection->hasAudioPlayer())
		return CommandResult(CommandResult::ERROR,
			"error the audio player doesn't exist at the moment");
	connection->setAudioPosition(position);
	return CommandResult();
}

CommandResult ServerBob::musicLoopCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, bool on)
{
	connection->setLooped(on);
	return CommandResult();
}

CommandResult ServerBob::musicStopCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/)
{
	connection->stopAudio();
	return CommandResult();
}

CommandResult ServerBob::musicPauseCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/)
{
	if (!connection->hasAudioPlayer())
		return CommandResult(CommandResult::ERROR,
			"error the audio player doesn't exist at the moment");
	connection->setAudioPaused(true);
	return CommandResult();
}

CommandResult ServerBob::musicUnpauseCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/)
{
	if (!connection->hasAudioPlayer())
		return CommandResult(CommandResult::ERROR,
			"error the audio player doesn't exist at the moment");
	connection->setAudioPaused(false);
	return CommandResult();
}

CommandResult ServerBob::musicAddressCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/)
{
	if (!connection->hasAudioPlayer())
		return CommandResult(CommandResult::ERROR,
			"error the audio player doesn't exist at the moment");

	std::string address = connection->getStreamAddress();
	Utils::replace(address, "\\", "\\\\");
	Utils::replace(address, "\n", "\\n");
	connection->sendCommand(sender, "answer music address\n{0}", address);
	return CommandResult();
}

CommandResult ServerBob::qualityCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, bool on)
{
	setQuality(on);
	return CommandResult();
}

CommandResult ServerBob::whisperClientAddCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, int id)
{
	if (id < 0)
		return CommandResult(CommandResult::ERROR,
			"error client id can't be negative");
	User *user = connection->getUser(static_cast<anyID>(id));
	if (!user)
	{
		// Try to add that user
		char *clientUid;
		if (!tsApi->handleTsError(tsApi->getFunctions().
			getClientVariableAsString(connection->getHandlerId(),
			static_cast<anyID>(id), CLIENT_UNIQUE_IDENTIFIER, &clientUid)))
			return CommandResult(CommandResult::ERROR,
				"error client id not found");
		connection->addUser(id, clientUid);
		tsApi->getFunctions().freeMemory(clientUid);
		user = connection->getUser(static_cast<anyID>(id));
	}
	connection->addWhisperUser(user);
	return CommandResult();
}

CommandResult ServerBob::whisperClientRemoveCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, int id)
{
	if (id < 0)
		return CommandResult(CommandResult::ERROR,
			"error client id can't be negative");
	User *user = connection->getUser(static_cast<anyID>(id));
	if (user)
		connection->removeWhisperUser(user);
	else
		return CommandResult(CommandResult::ERROR,
			"error client id not found");
	return CommandResult();
}

CommandResult ServerBob::whisperChannelAddCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, int id)
{
	if (id < 0)
		return CommandResult(CommandResult::ERROR,
			"error client id can't be negative");
	connection->addWhisperChannel(id);
	return CommandResult();
}

CommandResult ServerBob::whisperChannelRemoveCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, int id)
{
	if (id < 0)
		return CommandResult(CommandResult::ERROR,
			"error client id can't be negative");
	if (!connection->removeWhisperChannel(id))
		return CommandResult(CommandResult::ERROR,
			"error channel id not found");
	return CommandResult();
}

CommandResult ServerBob::whisperClearCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/)
{
	connection->clearWhisper();
	return CommandResult();
}

CommandResult ServerBob::statusAudioCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/)
{
	connection->sendCommand(sender, "answer audio\nstatus {0}", audioOn ? "on" : "off");
	return CommandResult();
}

CommandResult ServerBob::statusWhisperCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/)
{
	std::ostringstream out;
	out << "answer whisper\nclients";
	for (const User *user : *connection->getWhisperUsers())
		out << ' ' << user->getId();
	out << "\nchannels";
	for (const uint64_t channel : *connection->getWhisperChannels())
		out << ' ' << channel;
	connection->sendCommand(sender, out.str());
	return CommandResult();
}

CommandResult ServerBob::statusMusicCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/)
{
	std::ostringstream out;
	out << "answer music" << connection->getAudioStatus();

	connection->sendCommand(sender, out.str());
	return CommandResult();
}

CommandResult ServerBob::helpCommand(ServerConnection *connection, User *sender,
	const std::string& /*message*/)
{
	std::ostringstream output;
	output << "answer help";
	std::vector<std::pair<std::string, std::string> > descriptions =
		rootCommand.createDescriptions();
	// Omit commands that start with music
	const auto newEnd = std::remove_if(descriptions.begin(), descriptions.end(),
		[](const std::pair<std::string, std::string> &d)
			{ return Utils::startsWith(d.first, "music"); });
	descriptions.erase(newEnd, descriptions.end());

	output << combineHelp(descriptions);

	connection->sendCommand(sender, output.str());
	return CommandResult();
}

CommandResult ServerBob::helpMusicCommand(ServerConnection *connection,
	User *sender, const std::string& /*message*/)
{
	std::ostringstream output;
	output << "answer help music";
	std::vector<std::pair<std::string, std::string> > descriptions =
		rootCommand.createDescriptions();
	// Only include commands that contain music
	const auto newEnd = std::remove_if(descriptions.begin(), descriptions.end(),
		[](const std::pair<std::string, std::string> &d)
			{ return d.first.find("music") == std::string::npos; });
	descriptions.erase(newEnd, descriptions.end());

	output << combineHelp(descriptions);

	connection->sendCommand(sender, output.str());
	return CommandResult();
}

CommandResult ServerBob::pingCommand(ServerConnection *connection, User *sender,
	const std::string& /*message*/)
{
	connection->sendCommand(sender, "pong");
	return CommandResult();
}

CommandResult ServerBob::listClientsCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/)
{
	std::vector<anyID> clientIds;
	std::vector<std::string> clientNames;
	std::size_t maxLength = 0;
	anyID *clientList;
	tsApi->getFunctions().getClientList(connection->getHandlerId(), &clientList);
	anyID *currentClient = clientList;
	while (*currentClient != 0)
	{
		char *name;
		tsApi->getFunctions().getClientVariableAsString(
			connection->getHandlerId(), *currentClient, CLIENT_NICKNAME, &name);
		std::string n = name;
		tsApi->getFunctions().freeMemory(name);
		clientNames.push_back(n);
		clientIds.push_back(*currentClient);
		if (n.size() > maxLength)
			maxLength = n.size();
		currentClient++;
	}
	tsApi->getFunctions().freeMemory(clientList);

	std::ostringstream fStream;
	// Align the output to s characters
	fStream << "\n{0:" << maxLength << "} {1}";
	const std::string format = fStream.str();
	std::ostringstream output;
	output << "clients";
	for (std::size_t i = 0; i < clientIds.size(); i++)
		output << Utils::format(format, clientNames[i], clientIds[i]);
	connection->sendCommand(sender, output.str());
	return CommandResult();
}

CommandResult ServerBob::listChannelsCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/)
{
	std::vector<uint64> channelIds;
	std::vector<std::string> channelNames;
	std::size_t maxLength = 0;
	uint64_t *channelList;
	tsApi->getFunctions().getChannelList(connection->getHandlerId(), &channelList);
	uint64_t *currentChannel = channelList;
	while (*currentChannel != 0)
	{
		char *name;
		tsApi->getFunctions().getChannelVariableAsString(
			connection->getHandlerId(), *currentChannel, CHANNEL_NAME, &name);
		std::string n = name;
		tsApi->getFunctions().freeMemory(name);
		channelNames.push_back(n);
		channelIds.push_back(*currentChannel);
		if (n.size() > maxLength)
			maxLength = n.size();
		currentChannel++;
	}
	tsApi->getFunctions().freeMemory(channelList);

	std::ostringstream fStream;
	// Align the output to s characters
	fStream << "\n{0:" << maxLength << "} {1}";
	const std::string format = fStream.str();
	std::ostringstream output;
	output << "channels";
	for (std::size_t i = 0; i < channelIds.size(); i++)
		output << Utils::format(format, channelNames[i], channelIds[i]);
	connection->sendCommand(sender, output.str());
	return CommandResult();
}

CommandResult ServerBob::exitCommand(ServerConnection* /*connection*/,
	User * /*sender*/, const std::string& /*message*/)
{
	close();
	return CommandResult();
}
