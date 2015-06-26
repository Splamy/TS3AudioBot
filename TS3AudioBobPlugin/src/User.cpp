#include "User.hpp"

#include <ServerBob.hpp>
#include <ServerConnection.hpp>

#include <public_rare_definitions.h>
#include <sstream>

User::User(ServerConnection *connection, anyID id) :
	connection(connection),
	id(id)
{
}

bool User::inGroup(uint64 group)
{
	char *result;
	if(connection->handleTsError(connection->getServerBob()->functions.getClientVariableAsString(connection->getHandlerID(), id, CLIENT_SERVERGROUPS, &result)))
	{
		std::istringstream in(result);
		connection->getServerBob()->functions.freeMemory(result);
		char c;
		uint64 g;
		while(!in.eof())
		{
			in >> g;
			if(!in && !in.eof())
				return false;
			if(g == group)
				return true;
			// Read the comma
			in >> c;
			if(!in)
				return false;
		}
	}
	return false;
}
