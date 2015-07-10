#ifndef SERVER_BOB_HPP
#define SERVER_BOB_HPP

#include <Command.hpp>
#include <TsApi.hpp>

#include <istream>
#include <string>
#include <vector>

class AbstractCommandExecutor;
class ServerConnection;

class ServerBob
{
public:
	typedef std::vector<std::unique_ptr<AbstractCommandExecutor> > Commands;

private:
	static const std::vector<std::string> quitMessages;

	Commands commands;
	std::vector<ServerConnection> connections;
	bool audioOn;
	bool qualityOn;
	uint64 botAdminGroup;

	std::shared_ptr<TsApi> tsApi;

public:
	ServerBob(std::shared_ptr<TsApi> tsApi, uint64 botAdminGroup);
	/** Don't copy this object. */
	ServerBob(ServerBob&) = delete;
	ServerBob(ServerBob &&bob);

	void gotDbId(uint64 handlerId, const char *uniqueId, uint64 dbId);
	void gotServerGroup(uint64 handlerId, uint64 dbId, uint64 serverGroup);
	void addServer(uint64 handlerId);
	void removeServer(uint64 handlerId);
	ServerConnection* getServer(uint64 handlerId);
	void handleCommand(uint64 handlerId, anyID sender, const char *uniqueId,
		const std::string &message);
	void executeCommand(ServerConnection *connection, User *sender,
		const std::string &message);

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
	CommandResult listClientsCommand   (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string clients);
	CommandResult listChannelsCommand  (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string channels);
	CommandResult exitCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
};

#endif
