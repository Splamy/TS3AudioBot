#ifndef USER_HPP
#define USER_HPP

#include <public_definitions.h>

class ServerConnection;

class User
{
private:
	ServerConnection *connection;
	anyID id;

public:
	User(ServerConnection *connection, anyID id);

	bool inGroup(uint64 group);
};

#endif
