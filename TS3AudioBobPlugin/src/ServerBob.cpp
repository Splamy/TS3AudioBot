#include "ServerBob.hpp"

#include <ServerConnection.hpp>
#include <User.hpp>
#include <Utils.hpp>

#include <public_errors.h>
#include <algorithm>
#include <sstream>
#include <stdexcept>

const std::vector<std::string> ServerBob::quitMessages =
	{ "I'm outta here", "You're boring", "Have a nice day", "Bye" };

ServerBob::ServerBob(const TS3Functions &functions, uint64 botAdminGroup) :
	functions(functions),
	audioOn(false),
	botAdminGroup(botAdminGroup)
{
	// Register commands
	addCommand("help", &ServerBob::helpCommand,              "Gives you this handy command list");
	addCommand("ping", &ServerBob::pingCommand,              "Returns with a pong if the Bob is alive");
	addCommand("exit", &ServerBob::exitCommand,              "Let the Bob go home");
	std::string commandString = "audio [on|off]";
	addCommand("audio", &ServerBob::audioCommand,            "Let the bob send or be silent", &commandString);
	commandString = "whisper clear";
	addCommand("whisper", &ServerBob::whisperClearCommand,   "", &commandString);
	commandString = "whisper [client|channel] [add|remove] <id>";
	addCommand("whisper", &ServerBob::whisperClientCommand,  "Control the whisperlist of the Bob", &commandString);
	addCommand("whisper", &ServerBob::whisperChannelCommand, "", NULL, false, false);
	commandString = "status [whisper|audio]";
	addCommand("status", &ServerBob::statusAudioCommand,     "Get status information", &commandString);
	addCommand("status", &ServerBob::statusWhisperCommand,   "", NULL, false, false);
	addCommand("error", &ServerBob::loopCommand,             "", NULL, true, false);
	addCommand("unknown", &ServerBob::loopCommand,           "", NULL, true, false);

	// Get currently active connections
	uint64 *handlerIds;
	if (!handleTsError(functions.getServerConnectionHandlerList(&handlerIds)))
		throw std::runtime_error("Can't fetch server connections");
	for (uint64 *handlerId = handlerIds; *handlerId != 0; handlerId++)
		connections.emplace_back(this, *handlerId);
	functions.freeMemory(handlerIds);

	// Set audio to default state
	setAudio(audioOn);
}

ServerBob::ServerBob(ServerBob &&bob) :
	functions(bob.functions),
	commands(std::move(bob.commands)),
	connections(std::move(bob.connections)),
	audioOn(bob.audioOn),
	qualityOn(bob.qualityOn),
	botAdminGroup(bob.botAdminGroup)
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
		log("User not found");
}

void ServerBob::addServer(uint64 handlerId)
{
	connections.emplace_back(this, handlerId);
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
	log("Can't find server id to remove");
}

ServerConnection* ServerBob::getServer(uint64 handlerId)
{
	for (std::vector<ServerConnection>::iterator it = connections.begin();
	     it != connections.end(); it++)
	{
		if (it->getHandlerId() == handlerId)
			return &(*it);
	}
	return NULL;
}

bool ServerBob::handleTsError(unsigned int error)
{
	if (error != ERROR_ok)
	{
		char* errorMsg;
		if (functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			log("TeamSpeak-error: %s", errorMsg);
			// Send the message to the bot
			std::string msg = errorMsg;
			functions.freeMemory(errorMsg);
			Utils::replace(msg, "\n", "\\n");
		} else
			log("TeamSpeak-double-error");
		return false;
	}
	return true;
}

void ServerBob::handleCommand(uint64 handlerId, anyID sender,
	const char *uniqueId, const std::string &message)
{
	// Search the connection and the user
	std::vector<ServerConnection>::iterator connection = connections.begin();
	for (; connection != connections.end(); connection++)
	{
		if (connection->getHandlerId() == handlerId)
			break;
	}
	if (connection == connections.end())
	{
		log("Server connection for command not found");
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
	log("Enqueued command '%s'", noNewline.c_str());
	// Execute commands
	while (user->hasCommands())
		executeCommand(&(*connection), user, user->dequeueCommand());
}

void ServerBob::executeCommand(ServerConnection *connection, User *sender,
	const std::string &message)
{
	if (!sender->inGroup(botAdminGroup))
	{
		log("Unauthorized access from %d", sender->getId());
		connection->sendCommand(sender, "error access denied");
		return;
	}
	std::string noNewline = message;
	Utils::replace(noNewline, "\n", "\\n");
	log("Executing command '%s'", noNewline.c_str());

	// Search the right command
	CommandResult res;
	std::shared_ptr<std::string> errorMessage;
	for (Commands::const_iterator it = commands.cbegin(); it != commands.cend(); it++)
	{
		res = (**it)(connection, sender, message);
		if (res.success)
			break;
		else if (res.errorMessage)
			errorMessage = res.errorMessage;
	}
	if (!res.success)
	{
		if (errorMessage)
			connection->sendCommand(sender, errorMessage->c_str());
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
		new StringCommandExecutor<Args...>(name, help, Utils::myBind(
		static_cast<std::function<CommandResult(ServerBob*, ServerConnection*,
		User*, const std::string&, Args...)> >(fun), this),
		commandString, ignoreArguments, showHelp)));
}

void ServerBob::setAudio(bool on)
{
	audioOn = on;
	for (std::vector<ServerConnection>::iterator it = connections.begin();
	     it != connections.end(); it++)
		it->setAudio(on);
}

void ServerBob::setQuality(bool on)
{
	qualityOn = on;
	for (std::vector<ServerConnection>::iterator it = connections.begin();
	     it != connections.end(); it++)
		it->setQuality(on);
}

void ServerBob::close()
{
	std::string msg = quitMessages[Utils::getRandomNumber(0, quitMessages.size())];
	for (std::vector<ServerConnection>::iterator it = connections.begin();
	     it != connections.end(); it++)
		it->close(msg);
	connections.clear();
	// "Graceful" exit
	exit(0);
}

// Commands
CommandResult ServerBob::unknownCommand(ServerConnection *connection,
	User *sender, const std::string &message)
{
	std::string msg = message;
	Utils::replace(msg, "\n", "\\n");
	std::string formatted = Utils::format("Unknown command: %s", msg.c_str());
	// Send error message
	connection->sendCommand(sender, "error unknown command %s", msg.c_str());
	return CommandResult(false, std::make_shared<std::string>(formatted));
}

CommandResult ServerBob::loopCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &message, std::string /*command*/)
{
	log("Loop detected, have fun (%s)", message.c_str());
	return CommandResult();
}

CommandResult ServerBob::audioCommand(ServerConnection * /*connection*/,
	User * /*sender*/, const std::string &/*message*/, std::string /*command*/,
	bool on)
{
	setAudio(on);
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
		if(user)
			connection->addWhisperUser(user);
		else
			return CommandResult(false,
				std::make_shared<std::string>("error client id not found"));
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
	connection->sendCommand(sender, "status audio %s", audioOn ? "on" : "off");
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
	const std::vector<const User*> *users = connection->getWhisperUsers();
	for (std::vector<const User*>::const_iterator it = users->cbegin();
	     it != users->cend(); it++)
		out << " " << *it;
	out << "\nstatus whisper channels";
	const std::vector<uint64> *channels = connection->getWhisperChannels();
	for (std::vector<uint64>::const_iterator it = channels->cbegin();
	     it != channels->cend(); it++)
		out << " " << *it;
	connection->sendCommand(sender, out.str());
	return CommandResult();
}

CommandResult ServerBob::helpCommand(ServerConnection *connection, User *sender,
	const std::string& /*message*/, std::string /*command*/)
{
	std::ostringstream output;
	output << "help";
	std::size_t maxLength = 0;
	for (Commands::const_iterator it = commands.cbegin(); it != commands.cend(); it++)
	{
		if ((*it)->getHelp() && (*it)->getCommandName())
		{
			std::size_t s = (*it)->getCommandName()->size();
			if (s > maxLength)
				maxLength = s;
		}
	}
	std::ostringstream fStream;
	fStream << "\n%-" << maxLength << "s  %s";
	const std::string format = fStream.str();
	for (Commands::const_iterator it = commands.cbegin(); it != commands.cend();
	     it++)
	{
		if ((*it)->getHelp() && (*it)->getCommandName())
			output << Utils::format(format, (*it)->getCommandName()->c_str(),
				(*it)->getHelp()->c_str());
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

CommandResult ServerBob::exitCommand(ServerConnection* /*connection*/,
	User * /*sender*/, const std::string& /*message*/, std::string /*command*/)
{
	close();
	return CommandResult();
}
