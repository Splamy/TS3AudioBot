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

	void addServer(uint64 handlerID);
	void removeServer(uint64 handlerID);
	bool handleTsError(unsigned int error);
	void handleCommand(uint64 handlerID, anyID sender, const std::string &message);

private:
	template<class... Args>
	void addCommand(const std::string &name, CommandResult (ServerBob::*fun)
		(ServerConnection*, anyID, const std::string&, Args...),
		const std::string &help, bool ignoreArguments = false);

	void setAudio(bool on);
	void setQuality(bool on);
	void close();

	// Commands
	CommandResult unknownCommand       (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult loopCommand          (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult audioCommand         (ServerConnection *connection, anyID sender, const std::string &message, bool on);
	CommandResult qualityCommand       (ServerConnection *connection, anyID sender, const std::string &message, bool on);
	CommandResult whisperClientCommand (ServerConnection *connection, anyID sender, const std::string &message, std::string client);
	CommandResult whisperChannelCommand(ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult whisperClearCommand  (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult statusAudioCommand   (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult statusWhisperCommand (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult helpCommand          (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult pingCommand          (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult exitCommand          (ServerConnection *connection, anyID sender, const std::string &message);
};

#endif
