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
	ServerBob(TS3Functions &functions);

	void addServer(uint64 handlerID);
	void removeServer(uint64 handlerID);
	bool handleTsError(unsigned int error);
	void handleCommand(uint64 handlerID, anyID sender, const std::string &message);

private:
	template<class... Args>
	void addCommand(const std::string &name, CommandResult (ServerBob::*fun)
		(ServerConnection*, anyID, const std::string&, Args...),
		const std::string &help, const std::string *commandString = NULL,
		bool ignoreArguments = false, bool showHelp = true);

	void setAudio(bool on);
	void setQuality(bool on);
	void close();

	// Commands
	CommandResult unknownCommand       (ServerConnection *connection, anyID sender, const std::string &message);
	CommandResult loopCommand          (ServerConnection *connection, anyID sender, const std::string &message, std::string command);
	CommandResult audioCommand         (ServerConnection *connection, anyID sender, const std::string &message, std::string command, bool on);
	CommandResult qualityCommand       (ServerConnection *connection, anyID sender, const std::string &message, std::string command, bool on);
	CommandResult whisperClientCommand (ServerConnection *connection, anyID sender, const std::string &message, std::string command, std::string client, std::string action, int id);
	CommandResult whisperChannelCommand(ServerConnection *connection, anyID sender, const std::string &message, std::string command, std::string channel, std::string action, int id);
	CommandResult whisperClearCommand  (ServerConnection *connection, anyID sender, const std::string &message, std::string command, std::string clear);
	CommandResult statusAudioCommand   (ServerConnection *connection, anyID sender, const std::string &message, std::string command, std::string audio);
	CommandResult statusWhisperCommand (ServerConnection *connection, anyID sender, const std::string &message, std::string command, std::string whisper);
	CommandResult helpCommand          (ServerConnection *connection, anyID sender, const std::string &message, std::string command);
	CommandResult pingCommand          (ServerConnection *connection, anyID sender, const std::string &message, std::string command);
	CommandResult exitCommand          (ServerConnection *connection, anyID sender, const std::string &message, std::string command);
};

#endif
