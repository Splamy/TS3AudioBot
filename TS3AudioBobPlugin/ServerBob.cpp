#include "ServerBob.hpp"

#include <ServerConnection.hpp>
#include <Utils.hpp>

static const std::string FILENAME = "queryId";

const std::vector<std::string> ServerBob::quitMessages =
	{ "I'm outta here", "You're boring", "Have a nice day", "Bye" };

ServerBob::ServerBob() :
	audioOn(false)
{
	// Register commands
#define ADD_COMMAND(name, fun, help) commands.push_back(\
	std::unique_ptr<AbstractCommandExecutor>(new StringCommandExecutor<>(name,\
	help, std::bind(&ServerBob::fun, this, std::placeholders::_1, std::placeholders::_2,\
	std::placeholders::_3), false)))
	ADD_COMMAND("help", helpCommand, "help  Gives you this handy command list");
	ADD_COMMAND("ping", pingCommand, "ping  Returns with a pong if the Bob is alive");
	ADD_COMMAND("exit", exitCommand, "exit  Let the Bob go home");
#undef ADD_COMMAND
	//addCommand("help", static_cast<std::function<CommandResult(uint64 sender,
	//	const std::string &message)>>(std::bind(&ServerBob::helpCommand, this,
	//	std::placeholders::_1, std::placeholders::_2)), "Gives you this handy command list");
	/*addCommand("audio",   &ServerBob::audioCommand,  "audio [on|off]  Let the bob send or be silent");
	addCommand("status",  &ServerBob::statusCommand, "", true)));
	addCommand("error",   &ServerBob::loopCommand,   "");
	addCommand("unknown", &ServerBob::loopCommand,   "");*/
}

template<class... Args>
void ServerBob::addCommand(const std::string &name,
	std::function<CommandResult(ServerConnection *connection, uint64 sender,
	const std::string &message, Args...)> fun,
	const std::string &help, bool ignoreArguments)
{
	commands.push_back(std::unique_ptr<AbstractCommandExecutor>(
		new StringCommandExecutor<Args...>(name, help, fun, ignoreArguments)));
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
CommandResult ServerBob::unknownCommand(ServerConnection *connection, uint64 sender, const std::string& message)
{
	std::string msg = message;
	Utils::replace(msg, "\n", "\\n");
	std::string formatted = Utils::format("Unknown command: %s", msg.c_str());
	Utils::log(formatted);
	// Send error message
	connection->sendCommand(sender, "error unknown command %s", msg.c_str());
	return CommandResult(false, std::shared_ptr<std::string>(new std::string(formatted)));
}

CommandResult ServerBob::helpCommand(ServerConnection *connection, uint64 sender, const std::string& /*message*/)
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
		"\twhisper [on|off]\n"
		"\twhisper [add|remove] client <clientID>\n"
		"\twhisper [add|remove] channel <channelID>\n"
		"\twhisper clear\n"
		"\tstatus  audio\n"
		"\tstatus  whisper"
	);*/
	return CommandResult();
}

CommandResult ServerBob::pingCommand(ServerConnection *connection, uint64 sender, const std::string& /*message*/)
{
	connection->sendCommand(sender, "pong");
	return CommandResult();
}

CommandResult ServerBob::exitCommand(ServerConnection* /*connection*/, uint64 /*sender*/, const std::string& /*message*/)
{
	close();
	return CommandResult();
}
