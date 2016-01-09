#ifndef SERVER_BOB_HPP
#define SERVER_BOB_HPP

#include <istream>
#include <string>
#include <vector>

#include <Command.hpp>

class AbstractCommandExecutor;
class ServerConnection;
class TsApi;

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
	bool fillAudioData(uint64 handlerId, uint8_t *buffer, size_t length,
		int channelCount, bool sending);
	ServerConnection* getServer(uint64 handlerId);
	void handleCommand(uint64 handlerId, anyID sender, const char *uniqueId,
		const std::string &message);
	void executeCommand(ServerConnection *connection, User *sender,
		const std::string &message);

private:
	template <class... Args>
	void addCommand(const std::string &name, CommandResult (ServerBob::*fun)
		(ServerConnection*, User*, const std::string&, Args...),
		const std::string &help, const std::string *commandString = nullptr,
		bool ignoreArguments = false, bool showHelp = true);

	void setAudio(bool on);
	void setQuality(bool on);
	void close();

	// Commands
	CommandResult unknownCommand       (ServerConnection *connection, User *sender, const std::string &message);
	CommandResult loopCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
	CommandResult audioCommand         (ServerConnection *connection, User *sender, const std::string &message, std::string command, bool on);
	CommandResult musicStartCommand    (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string start, std::string address);
	CommandResult musicVolumeCommand   (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string volumeStr, double volume);
	CommandResult musicSeekCommand     (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string seek, double position);
	CommandResult musicLoopCommand     (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string loop, bool on);
	CommandResult musicCommand         (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string action);
	CommandResult qualityCommand       (ServerConnection *connection, User *sender, const std::string &message, std::string command, bool on);
	CommandResult whisperClientCommand (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string client, std::string action, int id);
	CommandResult whisperChannelCommand(ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string channel, std::string action, int id);
	CommandResult whisperClearCommand  (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string clear);
	CommandResult statusAudioCommand   (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string audio);
	CommandResult statusWhisperCommand (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string whisper);
	CommandResult statusMusicCommand   (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string music);
	CommandResult helpCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
	CommandResult helpMusicCommand     (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string music);
	CommandResult pingCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
	CommandResult listClientsCommand   (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string clients);
	CommandResult listChannelsCommand  (ServerConnection *connection, User *sender, const std::string &message, std::string command, std::string channels);
	CommandResult exitCommand          (ServerConnection *connection, User *sender, const std::string &message, std::string command);
};

#endif
