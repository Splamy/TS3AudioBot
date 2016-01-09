#include "ServerBob.hpp"

#include <algorithm>
#include <sstream>
#include <stdexcept>

#include <public_errors.h>

#include <ServerConnection.hpp>
#include <TsApi.hpp>
#include <User.hpp>
#include <Utils.hpp>

namespace
{
static bool connectionIdEqual(uint64 handlerId, ServerConnection &connection)
{
	return handlerId == connection.getHandlerId();
}
}

const std::vector<std::string> ServerBob::quitMessages =
	{ "I'm outta here", "You're boring", "Have a nice day", "Bye", "Good night",
	  "Nothing to do here", "Taking a break", "Lorem ipsum dolor sit amet...",
	  "Nothing can hold me back", "It's getting quiet", "Drop the bazzzzzz" };

ServerBob::ServerBob(std::shared_ptr<TsApi> tsApi, uint64 botAdminGroup) :
	audioOn(false),
	botAdminGroup(botAdminGroup),
	tsApi(tsApi)
{
	audio::Player::init();

	// Register commands
	addCommand("help", &ServerBob::helpCommand,              "Gives you this handy command list");
	addCommand("ping", &ServerBob::pingCommand,              "Returns with a pong if the Bob is alive");
	addCommand("exit", &ServerBob::exitCommand,              "Let the Bob go home");
	std::string commandString = "audio [on|off]";
	addCommand("audio", &ServerBob::audioCommand,            "Let the bob send or be silent", &commandString);
	commandString = "music [start <address>|stop|pause|unpause]";
	addCommand("music", &ServerBob::musicStartCommand,       "Control the integrated music player", &commandString);
	addCommand("music", &ServerBob::musicCommand,            "", nullptr, false, false);
	commandString = "whisper clear";
	addCommand("whisper", &ServerBob::whisperClearCommand,   "Clears the whisperlist", &commandString);
	commandString = "whisper [client|channel] [add|remove] <id>";
	addCommand("whisper", &ServerBob::whisperClientCommand,  "Control the whisperlist of the Bob", &commandString);
	addCommand("whisper", &ServerBob::whisperChannelCommand, "", nullptr, false, false);
	commandString = "status [whisper|audio|music]";
	addCommand("status", &ServerBob::statusAudioCommand,     "Get status information", &commandString);
	addCommand("status", &ServerBob::statusWhisperCommand,   "", nullptr, false, false);
	addCommand("status", &ServerBob::statusMusicCommand,     "", nullptr, false, false);
	addCommand("error", &ServerBob::loopCommand,             "", nullptr, true, false);
	commandString = "list [clients|channels]";
	addCommand("list", &ServerBob::listClientsCommand,       "Lists all connected clients", &commandString);
	addCommand("list", &ServerBob::listChannelsCommand,      "Lists all existing channels", nullptr, false, false);
	addCommand("unknown", &ServerBob::loopCommand,           "", nullptr, true, false);

	// Get currently active connections
	uint64 *handlerIds;
	if (!this->tsApi->handleTsError(this->tsApi->getFunctions().getServerConnectionHandlerList(&handlerIds)))
		throw std::runtime_error("Can't fetch server connections");
	for (uint64 *handlerId = handlerIds; *handlerId != 0; handlerId++)
		connections.emplace_back(tsApi, *handlerId);
	this->tsApi->getFunctions().freeMemory(handlerIds);

	// Set audio to default state
	setAudio(audioOn);
}

ServerBob::ServerBob(ServerBob &&bob) :
	commands(std::move(bob.commands)),
	connections(std::move(bob.connections)),
	audioOn(bob.audioOn),
	qualityOn(bob.qualityOn),
	botAdminGroup(bob.botAdminGroup),
	tsApi(std::move(bob.tsApi))
{
}

void ServerBob::gotDbId(uint64 handlerId, const char *uniqueId, uint64 dbId)
{
	ServerConnection *connection = getServer(handlerId);
	User *user = connection->getUser(uniqueId);
	user->setDbId(dbId);
	user->requestGroupUpdate();
}

void ServerBob::gotServerGroup(uint64 handlerId, uint64 dbId, uint64 serverGroup)
{
	ServerConnection *connection = getServer(handlerId);
	User *user = connection->getUser(dbId);
	if (user)
	{
		user->addGroup(serverGroup);
		if (serverGroup == botAdminGroup)
		{
			user->setGroupsInitialized(true);
			// Execute left over commands
			while (user->hasCommands())
				executeCommand(connection, user, user->dequeueCommand());
		}
	} else
		tsApi->log("User not found");
}

void ServerBob::addServer(uint64 handlerId)
{
	connections.emplace_back(tsApi, handlerId);
	connections.back().setAudio(audioOn);
}

void ServerBob::removeServer(uint64 handlerId)
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

void ServerBob::fillAudioData(uint64 /*handlerId*/, uint8_t *buffer, size_t length,
	int channelCount)
{
	if (audioPlayer)
	{
		audio::AudioProperties props = audioPlayer->getTargetProperties();
		if (props.channelCount != channelCount)
		{
			props.channelCount = channelCount;
			props.channelLayout = channelCount == 2 ?
				AV_CH_LAYOUT_STEREO : AV_CH_LAYOUT_MONO;
			// Reset dynamically computed properties
			props.bytesPerSecond = 0;
			props.frameSize = 0;
			audioPlayer->setTargetProperties(props);
		}
		audioPlayer->fillBuffer(buffer, length);
	}
}

ServerConnection* ServerBob::getServer(uint64 handlerId)
{
	for (ServerConnection &connection : connections)
	{
		if (connection.getHandlerId() == handlerId)
			return &connection;
	}
	return nullptr;
}

void ServerBob::handleCommand(uint64 handlerId, anyID sender,
	const char *uniqueId, const std::string &message)
{
	// Search the connection and the user
	std::vector<ServerConnection>::iterator connection =
		std::find_if(connections.begin(), connections.end(),
		std::bind(connectionIdEqual, handlerId, std::placeholders::_1));

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
	CommandResult res;
	std::shared_ptr<std::string> errorMessage;
	for (Commands::reference command : commands)
	{
		res = (*command)(connection, sender, message);
		if (res.success)
			break;
		else if (res.errorMessage)
			errorMessage = res.errorMessage;
	}
	if (!res.success)
	{
		if (errorMessage)
			connection->sendCommand(sender, *errorMessage);
		else
			unknownCommand(connection, sender, message);
	}
}

template <class... Args>
void ServerBob::addCommand(const std::string &name,
	CommandResult (ServerBob::*fun)(ServerConnection*, User*,
	const std::string&, Args...),
	const std::string &help, const std::string *commandString, bool ignoreArguments,
	bool showHelp)
{
	commands.push_back(std::unique_ptr<AbstractCommandExecutor>(
		new StringCommandExecutor<Args...>(name, help, Utils::myBindMember(fun, this),
		commandString, ignoreArguments, showHelp)));
}

void ServerBob::setAudio(bool on)
{
	audioOn = on;
	for (ServerConnection &connection : connections)
		connection.setAudio(on);
	if (audioPlayer)
	{
		if (audioOn)
		{
			if (autoPaused)
			{
				autoPaused = false;
				audioPlayer->setPaused(false);
			}
		} else if (!audioPlayer->isPaused())
		{
			autoPaused = true;
			audioPlayer->setPaused(true);
		}
	}
}

void ServerBob::setQuality(bool on)
{
	qualityOn = on;
	for (ServerConnection &connection : connections)
		connection.setQuality(on);
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

// Commands
CommandResult ServerBob::unknownCommand(ServerConnection *connection,
	User *sender, const std::string &message)
{
	std::string msg = message;
	Utils::replace(msg, "\n", "\\n");
	std::string formatted = Utils::format("Unknown command: {0}", msg);
	// Send error message
	connection->sendCommand(sender, "error unknown command {0}", msg);
	return CommandResult(false, std::make_shared<std::string>(formatted));
}

CommandResult ServerBob::loopCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/)
{
	tsApi->log("Loop detected, have fun");
	return CommandResult();
}

CommandResult ServerBob::audioCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	bool on)
{
	setAudio(on);
	return CommandResult();
}

CommandResult ServerBob::musicStartCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	std::string start, std::string address)
{
	std::transform(start.begin(), start.end(), start.begin(), ::tolower);
	if (start != "start")
		return CommandResult(false);

	// Strip [URL] and [/URL]
	if (address.length() >= 5 && address.compare(0, 5, "[URL]") == 0)
		address = address.substr(5);
	if (address.length() >= 6 && address.compare(address.length() - 6, 6, "[/URL]") == 0)
		address = address.substr(0, address.length() - 6);

	// Load and start an audio stream
	// TODO sometimes that doesn't work
	autoPaused = false;
	audioPlayer.reset(new audio::Player(address));
	// Use default properties, the channel settings will by dynamically updated
	audioPlayer->setTargetProperties(AV_SAMPLE_FMT_S16, 48000, 2,
		AV_CH_LAYOUT_STEREO);
	audioPlayer->start();
	return CommandResult();
}

CommandResult ServerBob::musicCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	std::string action)
{
	std::transform(action.begin(), action.end(), action.begin(), ::tolower);
	if (action == "stop")
		audioPlayer.reset();
	else if (action == "pause")
	{
		if (audioPlayer)
		{
			audioPlayer->setPaused(true);
			autoPaused = false;
		} else
			return CommandResult(false, std::make_shared<std::string>(
				"error no audio is played at the moment"));
	} else if (action == "unpause")
	{
		if (audioPlayer)
		{
			audioPlayer->setPaused(false);
			autoPaused = false;
		} else
			return CommandResult(false, std::make_shared<std::string>(
				"error no audio is played at the moment"));
	} else
		return CommandResult(false);
	return CommandResult();
}

CommandResult ServerBob::qualityCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	bool on)
{
	setQuality(on);
	return CommandResult();
}

CommandResult ServerBob::whisperClientCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	std::string client, std::string action, int id)
{
	std::transform(client.begin(), client.end(), client.begin(), ::tolower);
	std::transform(action.begin(), action.end(), action.begin(), ::tolower);
	if (client != "client")
		return CommandResult(false);
	if (id < 0)
		return CommandResult(false,
			std::make_shared<std::string>("error client id can't be negative"));
	if (action == "add")
	{
		User *user = connection->getUser(static_cast<anyID>(id));
		if (!user)
		{
			// Try to add that user
			char *clientUid;
			if (!tsApi->handleTsError(tsApi->getFunctions().
			    getClientVariableAsString(connection->getHandlerId(),
			    static_cast<anyID>(id), CLIENT_UNIQUE_IDENTIFIER, &clientUid)))
				return CommandResult(false,
					std::make_shared<std::string>("error client id not found"));
			connection->addUser(id, clientUid);
			tsApi->getFunctions().freeMemory(clientUid);
			user = connection->getUser(static_cast<anyID>(id));
		}
		connection->addWhisperUser(user);
	} else if (action == "remove")
	{
		User *user = connection->getUser(static_cast<anyID>(id));
		if (user)
			connection->removeWhisperUser(user);
		else
			return CommandResult(false,
				std::make_shared<std::string>("error client id not found"));
	} else
		return CommandResult(false,
			std::make_shared<std::string>("error unknown action"));
	return CommandResult();
}

CommandResult ServerBob::whisperChannelCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	std::string channel, std::string action, int id)
{
	std::transform(channel.begin(), channel.end(), channel.begin(), ::tolower);
	std::transform(action.begin(), action.end(), action.begin(), ::tolower);
	if (channel != "channel")
		return CommandResult(false);
	if (id < 0)
		return CommandResult(false,
			std::make_shared<std::string>("error client id can't be negative"));
	if (action == "add")
		connection->addWhisperChannel(id);
	else if (action == "remove")
	{
		if (!connection->removeWhisperChannel(id))
			return CommandResult(false,
				std::make_shared<std::string>("error channel id not found"));
	} else
		return CommandResult(false,
			std::make_shared<std::string>("error unknown action"));
	return CommandResult();
}

CommandResult ServerBob::whisperClearCommand(ServerConnection *connection,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	std::string clear)
{
	std::transform(clear.begin(), clear.end(), clear.begin(), ::tolower);
	if (clear != "clear")
		return CommandResult(false);
	connection->clearWhisper();
	return CommandResult();
}

CommandResult ServerBob::statusAudioCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/, std::string /*command*/,
		std::string audio)
{
	std::transform(audio.begin(), audio.end(), audio.begin(), ::tolower);
	if (audio != "audio")
		return CommandResult(false);
	connection->sendCommand(sender, "status audio {0}", audioOn ? "on" : "off");
	return CommandResult();
}

CommandResult ServerBob::statusWhisperCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/, std::string /*command*/,
	std::string whisper)
{
	std::transform(whisper.begin(), whisper.end(), whisper.begin(), ::tolower);
	if (whisper != "whisper")
		return CommandResult(false);
	std::ostringstream out;
	out << "status whisper clients";
	for (const User *user : *connection->getWhisperUsers())
		out << " " << user->getId();
	out << "\nstatus whisper channels";
	for (const uint64 channel : *connection->getWhisperChannels())
		out << " " << channel;
	connection->sendCommand(sender, out.str());
	return CommandResult();
}

CommandResult ServerBob::statusMusicCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/, std::string /*command*/,
	std::string music)
{
	std::transform(music.begin(), music.end(), music.begin(), ::tolower);
	if (music != "music")
		return CommandResult(false);
	std::ostringstream out;
	out << "status music ";
	if (!audioPlayer)
		out << "off";
	else if (audioPlayer->hasErrors())
		out << "error";
	else if (audioPlayer->isFinished())
		out << "finished";
	else if (audioPlayer->isPaused())
		out << "paused";
	else
		out << "playing";

	if (audioPlayer && !audioPlayer->hasErrors())
		out << " with length " << audioPlayer->getDuration() << " s";

	connection->sendCommand(sender, out.str());
	return CommandResult();
}

CommandResult ServerBob::helpCommand(ServerConnection *connection, User *sender,
	const std::string& /*message*/, std::string /*command*/)
{
	std::ostringstream output;
	output << "help";
	std::size_t maxLength = 0;
	for (Commands::const_reference command : commands)
	{
		if (command->getHelp() && command->getCommandName())
		{
			std::size_t s = command->getCommandName()->size();
			if (s > maxLength)
				maxLength = s;
		}
	}
	std::ostringstream fStream;
	// Align the output to s characters
	fStream << "\n{0:-" << maxLength << "}    {1}";
	const std::string format = fStream.str();
	for (Commands::const_reference command : commands)
	{
		if (command->getHelp() && command->getCommandName())
			output << Utils::format(format, *command->getCommandName(),
				*command->getHelp());
	}

	connection->sendCommand(sender, output.str());
	return CommandResult();
}

CommandResult ServerBob::pingCommand(ServerConnection *connection, User *sender,
	const std::string& /*message*/, std::string /*command*/)
{
	connection->sendCommand(sender, "pong");
	return CommandResult();
}

CommandResult ServerBob::listClientsCommand(ServerConnection *connection,
	User *sender, const std::string &/*message*/, std::string /*command*/,
	std::string clients)
{
	std::transform(clients.begin(), clients.end(), clients.begin(), ::tolower);
	if (clients != "clients")
		return CommandResult(false);
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
	User *sender, const std::string &/*message*/, std::string /*command*/,
	std::string channels)
{
	std::transform(channels.begin(), channels.end(), channels.begin(), ::tolower);
	if (channels != "channels")
		return CommandResult(false);
	std::vector<uint64> channelIds;
	std::vector<std::string> channelNames;
	std::size_t maxLength = 0;
	uint64 *channelList;
	tsApi->getFunctions().getChannelList(connection->getHandlerId(), &channelList);
	uint64 *currentChannel = channelList;
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
	User * /*sender*/, const std::string& /*message*/, std::string /*command*/)
{
	close();
	return CommandResult();
}
