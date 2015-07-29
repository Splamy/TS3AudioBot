#include "User.hpp"

#include <ServerBob.hpp>
#include <ServerConnection.hpp>

#include <public_rare_definitions.h>
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
	dbIdInitialized(false),
	dbIdRequested(false),
	groupsInitialized(false),
	groupUpdateRequested(false),
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
			requestDbId();
			dbIdRequested = true;
		}
	}
}

const std::string& User::getUniqueId() const
{
	return uniqueId;
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
