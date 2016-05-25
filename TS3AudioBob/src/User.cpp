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

#include "User.hpp"

#include "ServerBob.hpp"
#include "ServerConnection.hpp"

#include <public_rare_definitions.h>

#include <algorithm>
#include <sstream>
#include <stdexcept>

const std::chrono::seconds User::GROUP_WAIT_TIME(3);
const std::chrono::seconds User::GROUP_REFRESH_TIME(10);

User::User(ServerConnection *connection, std::shared_ptr<TsApi> tsApi, anyID id,
	const std::string &uniqueId) :
	tsApi(std::move(tsApi)),
	connection(connection),
	id(id),
	uniqueId(uniqueId),
	lastGroupAdd(std::chrono::steady_clock::now() - GROUP_REFRESH_TIME)
{
}

User::User(User &&user) :
	tsApi(std::move(user.tsApi)),
	connection(user.connection),
	id(user.id),
	uniqueId(std::move(user.uniqueId)),
	dbId(user.dbId),
	dbIdInitialized(user.dbIdInitialized),
	dbIdRequested(user.dbIdRequested),
	groups(std::move(user.groups)),
	groupsInitialized(user.groupsInitialized),
	groupUpdateRequested(user.groupUpdateRequested),
	lastGroupAdd(std::move(user.lastGroupAdd)),
	commandQueue(std::move(user.commandQueue))
{
}

User& User::operator = (User &&user)
{
	tsApi = std::move(user.tsApi);
	connection = user.connection;
	id = user.id;
	uniqueId = std::move(user.uniqueId);
	dbId = user.dbId;
	dbIdInitialized = user.dbIdInitialized;
	dbIdRequested = user.dbIdRequested;
	groups = std::move(user.groups);
	groupsInitialized = user.groupsInitialized;
	groupUpdateRequested = user.groupUpdateRequested;
	lastGroupAdd = std::move(user.lastGroupAdd);
	commandQueue = std::move(user.commandQueue);
	return *this;
}

void User::requestDbId()
{
	tsApi->handleTsError(tsApi->getFunctions().requestClientDBIDfromUID(
		connection->getHandlerId(), uniqueId.c_str(), nullptr));
}

void User::requestGroupUpdate()
{
	if (dbIdInitialized)
	{
		// Request client information
		groups.clear();
		tsApi->handleTsError(tsApi->getFunctions().
			requestServerGroupsByClientID(connection->getHandlerId(), dbId,
			nullptr));
		groupUpdateRequested = false;
		groupsInitialized = false;
	} else
	{
		groupUpdateRequested = true;
		if (!dbIdRequested)
		{
			dbIdRequested = true;
			requestDbId();
		}
	}
}

const std::string& User::getUniqueId() const
{
	return uniqueId;
}

bool User::hasDbId() const
{
	return dbIdInitialized;
}

uint64 User::getDbId() const
{
	if (!dbIdInitialized)
		throw std::runtime_error("The database id is not yet known");
	return dbId;
}

void User::setDbId(uint64 dbId)
{
	if (dbIdInitialized)
		throw std::runtime_error("The database id was already set");
	this->dbId = dbId;
	dbIdInitialized = true;
	dbIdRequested = false;
}

anyID User::getId() const
{
	return id;
}

void User::enqueueCommand(const std::string &message)
{
	// Check if the group list should be updated
	auto diff = std::chrono::steady_clock::now() - lastGroupAdd;
	if (!groupsInitialized || diff > GROUP_WAIT_TIME)
	{
		if ((!groupsInitialized || diff > GROUP_REFRESH_TIME) && !groupUpdateRequested)
			requestGroupUpdate();
		else
			groupsInitialized = true;
	}
	commandQueue.push(message);
}

std::string User::dequeueCommand()
{
	std::string result = commandQueue.front();
	commandQueue.pop();
	return result;
}

bool User::hasCommands() const
{
	return dbIdInitialized && groupsInitialized && !commandQueue.empty();
}

void User::setGroupsInitialized(bool groupsInitialized)
{
	this->groupsInitialized = groupsInitialized;
}

void User::addGroup(uint64 group)
{
	groups.push_back(group);
	lastGroupAdd = std::chrono::steady_clock::now();
}

bool User::inGroup(uint64 group) const
{
	if (!groupsInitialized)
		throw std::runtime_error("The group list is not initialized");
	return std::find(groups.cbegin(), groups.cend(), group) != groups.cend();
}

void User::setEnableCallbacks(bool enableCallbacks)
{
	this->enableCallbacks = enableCallbacks;
}

bool User::getEnableCallbacks() const
{
	return enableCallbacks;
}
