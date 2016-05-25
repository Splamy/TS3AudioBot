// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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
	bool dbIdInitialized = false;
	bool dbIdRequested = false;
	std::vector<uint64> groups;
	bool groupsInitialized = false;
	bool groupUpdateRequested = false;
	std::chrono::steady_clock::time_point lastGroupAdd;
	std::queue<std::string> commandQueue;

	/** If this user should receive messages on callbacks (without explicitly
	 *  requesting information).
	 */
	bool enableCallbacks = false;

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
	bool inGroup(uint64 group) const;

	void setEnableCallbacks(bool enableCallbacks);
	bool getEnableCallbacks() const;
};

#endif
