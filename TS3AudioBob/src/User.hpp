#ifndef USER_HPP
#define USER_HPP

#include "TsApi.hpp"

#include <chrono>
#include <memory>
#include <queue>
#include <string>

class ServerConnection;

class User
{
private:
	static const std::chrono::seconds GROUP_WAIT_TIME;
	static const std::chrono::seconds GROUP_REFRESH_TIME;

	std::shared_ptr<TsApi> tsApi;
	ServerConnection *connection;
	anyID id;
	std::string uniqueId;
	uint64 dbId;
	bool dbIdInitialized;
	bool dbIdRequested;
	std::vector<uint64> groups;
	bool groupsInitialized;
	bool groupUpdateRequested;
	std::chrono::steady_clock::time_point lastGroupAdd;
	std::queue<std::string> commandQueue;

public:
	User(ServerConnection *connection, std::shared_ptr<TsApi> tsApi, anyID id, const std::string &uniqueId);
	/** Don't copy this object. */
	User(User&) = delete;
	User(User &&user);
	User& operator = (User &&user);

	void requestDbId();
	/** Requests TeamSpeak to update the server group list. */
	void requestGroupUpdate();
	const std::string& getUniqueId() const;
	bool hasDbId() const;
	uint64 getDbId() const;
	void setDbId(uint64 dbId);
	anyID getId() const;
	/** Returns true if this user has queued commands that should be executed. */
	bool hasCommands() const;
	/** Adds a command to the command queue for this user and automatically
	 *  requests an update of the group list when a certain time passed.
	 *
	 *  Commands should always be passed to enqueue and dequeue to be sure that
	 *  the user groups are up to date.
	 */
	void enqueueCommand(const std::string &message);
	std::string dequeueCommand();
	void setGroupsInitialized(bool groupsInitialized);
	void addGroup(uint64 group);
	bool inGroup(uint64 group)const ;
};

#endif
