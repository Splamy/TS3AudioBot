#ifndef SERVER_BOB_HPP
#define SERVER_BOB_HPP

#include <Command.hpp>

#include <ts3_functions.h>
#include <istream>
#include <string>
#include <vector>

class AbstractCommandExecutor;
class ServerConnection;

class ServerBob
{
public:
	typedef std::vector<std::unique_ptr<AbstractCommandExecutor> > Commands;

	const TS3Functions functions;

private:
	static const std::vector<std::string> quitMessages;

	Commands commands;
	std::vector<ServerConnection> connections;
	bool audioOn;
	bool qualityOn;
	uint64 botAdminGroup;

public:
	ServerBob(const TS3Functions &functions, uint64 botAdminGroup);
	/** Don't copy this object. */
	ServerBob(ServerBob&) = delete;
	ServerBob(ServerBob &&bob);

	void gotDbId(uint64 handlerId, const char *uniqueId, uint64 dbId);
	void gotServerGroup(uint64 handlerId, uint64 dbId, uint64 serverGroup);
	void addServer(uint64 handlerId);
	void removeServer(uint64 handlerId);
	ServerConnection* getServer(uint64 handlerId);
	bool handleTsError(unsigned int error);
	void handleCommand(uint64 handlerId, anyID sender, const char *uniqueId,
		const std::string &message);
	void executeCommand(ServerConnection *connection, User *sender,
		const std::string &message);

	/** Prints a message into the TeamSpeak log. */
	template <class... Args>
	void log(const std::string &format, Args... args)
	{
		std::string message = Utils::format(format, args...);
		if (!handleTsError(functions.logMessage(message.c_str(), LogLevel_WARNING, "", 0)))
			printf("%s\n", message.c_str());
	}

private:
	template <class... Args>
	void addCommand(const std::string &name, CommandResult (ServerBob::*fun)
		(ServerConnection*, User*, const std::string&, Args...),
		const std::string &help, const std::string *commandString = NULL,
		bool ignoreArguments = false, bool showHelp = true);

	void setAudio(bool on);
	void setQuality(bool on);
	void close();

	// Commands
	CommandResult unknownCommand       (ServerConnection *connection, User *sender, const std::string &message);
	CommandResult loopCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
	CommandResult audioCommand         (ServerConnection *connection, User *sender, const std::string &message, std::string command, bool on);
	CommandResult qualityCommand       (ServerConnection *connection, User *sender, const std::string &message, std::string command, bool on);
	CommandResult whisperClientCommand (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string client, std::string action, int id);
	CommandResult whisperChannelCommand(ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string channel, std::string action, int id);
	CommandResult whisperClearCommand  (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string clear);
	CommandResult statusAudioCommand   (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string audio);
	CommandResult statusWhisperCommand (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string whisper);
	CommandResult helpCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
	CommandResult pingCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
	CommandResult exitCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
};

#endif
