#ifndef SERVER_BOB_HPP
#define SERVER_BOB_HPP

#include <Command.hpp>

#include <ts3_functions.h>
#include <string>
#include <vector>

class AbstractCommandExecutor;
class ServerConnection;

class ServerBob
{
public:
	typedef std::vector<std::unique_ptr<AbstractCommandExecutor> > Commands;

	TS3Functions functions;

private:
	static const std::vector<std::string> quitMessages;

	Commands commands;
	std::vector<ServerConnection> connections;
	bool audioOn;
	bool qualityOn;

public:
	ServerBob();

private:
	template<class... Args>
	void addCommand(const std::string &name,
		std::function<CommandResult(ServerConnection *connection, uint64 sender,
		const std::string &message, Args...)> fun,
		const std::string &help, bool ignoreArguments = false);

	void setAudio(bool on);
	void setQuality(bool on);
	void close();

	// Commands
	CommandResult unknownCommand(ServerConnection *connection, uint64 sender, const std::string& message);
	CommandResult helpCommand(ServerConnection *connection, uint64 sender, const std::string& message);
	CommandResult pingCommand(ServerConnection *connection, uint64 sender, const std::string& message);
	CommandResult exitCommand(ServerConnection *connection, uint64 sender, const std::string& message);
};

#endif
