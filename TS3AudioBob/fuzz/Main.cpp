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
