#ifndef SERVER_BOB_HPP
#define SERVER_BOB_HPP

#include "commands/CommandGroup.hpp"

#include <public_definitions.h>

#include <cstdint>
#include <istream>
#include <string>
#include <vector>

class ServerConnection;
class TsApi;

class ServerBob
{
private:
	static const std::vector<std::string> quitMessages;

	CommandGroup rootCommand;
	std::vector<ServerConnection> connections;
	bool audioOn;
	bool qualityOn;
	uint64_t botAdminGroup;

	std::shared_ptr<TsApi> tsApi;

public:
	ServerBob(std::shared_ptr<TsApi> tsApi, uint64_t botAdminGroup);
	/** Don't copy this object. */
	ServerBob(ServerBob&) = delete;
	ServerBob(ServerBob &&bob);

	void gotDbId(uint64_t handlerId, const char *uniqueId, uint64_t dbId);
	void gotServerGroup(uint64_t handlerId, uint64_t dbId, uint64_t serverGroup);
	void addServer(uint64_t handlerId);
	void removeServer(uint64_t handlerId);
	bool fillAudioData(uint64_t handlerId, uint8_t *buffer, size_t length,
		int channelCount, bool sending);
	ServerConnection* getServer(uint64_t handlerId);
	void handleCommand(uint64_t handlerId, anyID sender, const char *uniqueId,
		const std::string &message);
	void executeCommand(ServerConnection *connection, std::shared_ptr<User> sender,
		const std::string &message);

private:
	template <class... Args>
	void addCommand(const std::string &command, CommandResult (ServerBob::*fun)
		(ServerConnection*, std::shared_ptr<User>, const std::string&, const std::string&, Args...),
		const std::string &description = "", bool displayDescription = true);
	template <class... Args>
	void addCommand(const std::string &command, std::function<CommandResult
		(ServerConnection*, std::shared_ptr<User>, const std::string&, const std::string&, Args...)> fun,
		const std::string &description = "", bool displayDescription = true);

	void setAudio(bool on);
	void setQuality(bool on);
	std::string combineHelp(std::vector<std::pair<std::string, std::string> >
		descriptions);
	void close();
	void unknownCommand(ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message);

	// Commands
	CommandResult errorCommand         (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, std::string s);
	CommandResult audioCommand         (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, bool on);
	CommandResult musicStartCommand    (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, std::string address);
	CommandResult musicVolumeCommand   (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, double volume);
	CommandResult musicSeekCommand     (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, double position);
	CommandResult musicLoopCommand     (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, bool on);
	CommandResult musicStopCommand     (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult musicPauseCommand    (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult musicUnpauseCommand  (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult musicAddressCommand  (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult qualityCommand       (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, bool on);
	CommandResult whisperClientAddCommand    (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, int id);
	CommandResult whisperClientRemoveCommand (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, int id);
	CommandResult whisperChannelAddCommand   (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, int id);
	CommandResult whisperChannelRemoveCommand(ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, int id);
	CommandResult whisperClearCommand  (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult statusAudioCommand   (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult statusWhisperCommand (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult statusMusicCommand   (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult helpCommand          (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult helpMusicCommand     (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult pingCommand          (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult listClientsCommand   (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult listChannelsCommand  (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
	CommandResult callbackCommand      (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest, bool on);
	CommandResult exitCommand          (ServerConnection *connection, std::shared_ptr<User> sender, const std::string &message, const std::string &rest);
};

#endif
