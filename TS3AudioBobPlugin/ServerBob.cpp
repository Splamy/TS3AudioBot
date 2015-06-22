#include "ServerBob.hpp"

#include <ServerConnection.hpp>
#include <Utils.hpp>

#include <public_errors.h>
#include <stdexcept>

static const std::string FILENAME = "queryId";

const std::vector<std::string> ServerBob::quitMessages =
	{ "I'm outta here", "You're boring", "Have a nice day", "Bye" };

ServerBob::ServerBob() :
	audioOn(false)
{
	// Register commands
	addCommand("help", &ServerBob::helpCommand, "help  Gives you this handy command list");
	addCommand("ping", &ServerBob::pingCommand, "ping  Returns with a pong if the Bob is alive");
	addCommand("exit", &ServerBob::exitCommand, "exit  Let the Bob go home");
	addCommand("audio",   &ServerBob::audioCommand,  "audio [on|off]  Let the bob send or be silent");
	addCommand("error",   &ServerBob::loopCommand,   "");
	addCommand("unknown", &ServerBob::loopCommand,   "");

	// Get currently active connections
	uint64 *handlerIDs;
	if(!handleTsError(functions.getServerConnectionHandlerList(&handlerIDs)))
		throw std::runtime_error("Can't fetch server connections");
	for(uint64 *handlerID = handlerIDs; *handlerID != 0; handlerID++)
		connections.emplace_back(this, *handlerID);
	functions.freeMemory(handlerIDs);

	// Set audio to default state
	setAudio(audioOn);
}

void ServerBob::addServer(uint64 handlerID)
{
	connections.emplace_back(this, handlerID);
	connections.back().setAudio(audioOn);
}

void ServerBob::removeServer(uint64 handlerID)
{
	for(std::vector<ServerConnection>::iterator it = connections.begin();
		it != connections.end(); it++)
	{
		if(it->getHandlerID() == handlerID)
		{
			connections.erase(it);
			return;
		}
	}
	Utils::log("Didn't find the server that should be removed");
}

bool ServerBob::handleTsError(unsigned int error)
{
	if(error != ERROR_ok)
	{
		char* errorMsg;
		if(functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			Utils::log("TeamSpeak-error: %s", errorMsg);
			// Send the message to the bot
			std::string msg = errorMsg;
			functions.freeMemory(errorMsg);
			Utils::replace(msg, "\n", "\\n");
		} else
			Utils::log("TeamSpeak-double-error");
		return false;
	}
	return true;
}

void ServerBob::handleCommand(uint64 handlerID, anyID sender, const std::string &message)
{
	std::vector<ServerConnection>::iterator connection = connections.begin();
	for(; connection != connections.end(); connection++)
	{
		if(connection->getHandlerID() == handlerID)
			break;
	}
	if(connection == connections.end())
	{
		Utils::log("Server connection for command not found");
		return;
	}

	// TODO Check if this message is from an authorized client
	//Utils::log("Handling message %s", message.c_str());
	CommandResult res;
	for(Commands::const_iterator it = commands.cbegin(); it != commands.cend(); it++)
	{
		res = (**it)(&(*connection), sender, message);
		if(res.success)
			break;
		else if (res.errorMessage)
			connection->sendCommand(sender, *res.errorMessage);
	}
	if(!res.success)
		unknownCommand(&(*connection), sender, message);
}

template<class... Args>
void ServerBob::addCommand(const std::string &name,
	CommandResult (ServerBob::*fun)(ServerConnection*, anyID,
	const std::string&, Args...),
	const std::string &help, bool ignoreArguments)
{
	commands.push_back(std::unique_ptr<AbstractCommandExecutor>(
		new StringCommandExecutor<Args...>(name, help, myBind(
		static_cast<std::function<CommandResult(ServerBob*, ServerConnection*,
		anyID, const std::string&, Args...)> >(fun),
		this, Utils::IntSequenceCreator<sizeof...(Args) + 3>()),
		ignoreArguments)));
}


void ServerBob::setAudio(bool on)
{
	audioOn = on;
	for(std::vector<ServerConnection>::iterator it = connections.begin();
		it != connections.end(); it++)
		it->setAudio(on);
}

void ServerBob::setQuality(bool on)
{
	qualityOn = on;
	for(std::vector<ServerConnection>::iterator it = connections.begin();
		it != connections.end(); it++)
		it->setQuality(on);
}

void ServerBob::close()
{
	std::string msg = quitMessages[Utils::getRandomNumber(0, quitMessages.size())];
	for(std::vector<ServerConnection>::iterator it = connections.begin();
		it != connections.end(); it++)
		it->close(msg);
	connections.clear();
	// "Graceful" exit
	exit(0);
}

// Commands
CommandResult ServerBob::unknownCommand(ServerConnection *connection, anyID sender, const std::string &message)
{
	std::string msg = message;
	Utils::replace(msg, "\n", "\\n");
	std::string formatted = Utils::format("Unknown command: %s", msg.c_str());
	Utils::log(formatted);
	// Send error message
	connection->sendCommand(sender, "error unknown command %s", msg.c_str());
	return CommandResult(false, std::shared_ptr<std::string>(new std::string(formatted)));
}

CommandResult ServerBob::loopCommand(ServerConnection * /*connection*/, anyID /*sender*/, const std::string &message)
{
	if(Utils::startsWith(message, "error unknown") || Utils::startsWith(message, "unknown command"))
	{
		Utils::log("Loop detected, have fun");
		return CommandResult();
	}
	return CommandResult(false);
}

CommandResult ServerBob::audioCommand(ServerConnection * /*connection*/, anyID /*sender*/, const std::string &/*message*/, bool on)
{
	setAudio(on);
	return CommandResult();
}

CommandResult ServerBob::qualityCommand(ServerConnection * /*connection*/, anyID /*sender*/, const std::string &/*message*/, bool on)
{
	setQuality(on);
	return CommandResult();
}

CommandResult ServerBob::helpCommand(ServerConnection *connection, anyID sender, const std::string& /*message*/)
{
	std::string result = "help";
	for(Commands::const_iterator it = commands.cbegin(); it != commands.cend(); it++)
	{
		result += "\n\t";
		result += (*it)->getHelp();
	}
	connection->sendCommand(sender, result);
	/*connection->sendCommand(sender, "help \n"
		"\taudio   [on|off]\n"
		"\tquality [on|off]\n"
		"\twhisper [add|remove] client <clientID>\n"
		"\twhisper [add|remove] channel <channelID>\n"
		"\twhisper clear\n"
		"\tstatus  audio\n"
		"\tstatus  whisper"
	);*/
	return CommandResult();
}

CommandResult ServerBob::pingCommand(ServerConnection *connection, anyID sender, const std::string& /*message*/)
{
	connection->sendCommand(sender, "pong");
	return CommandResult();
}

CommandResult ServerBob::exitCommand(ServerConnection* /*connection*/, anyID /*sender*/, const std::string& /*message*/)
{
	close();
	return CommandResult();
}
