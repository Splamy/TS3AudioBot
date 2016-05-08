#include "ServerBob.hpp"
#include "ServerConnection.hpp"
#include "TsApi.hpp"
#include "Utils.hpp"

#include "VirtualFunctions.hpp"

#include <cstring>
#include <fstream>
#include <string>

int main()
{
	// Write config file
	{
		std::ofstream configFile("../configTS3AudioBot.cfg");
		configFile << Utils::format("MainBot::adminGroupId={0}\n", ADMIN_GROUP_ID);
	}

	initTS3Plugin();

	// Read commands
	std::string cmd;
	while (std::getline(std::cin, cmd))
	{
		workCommandQueue();
		sendMessage(cmd.c_str());
	}

	shutdownTS3Plugin();

	return 0;
}
