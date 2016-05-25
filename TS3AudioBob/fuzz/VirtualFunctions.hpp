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

#ifndef VIRTUAL_FUNCTIONS_HPP
#define VIRTUAL_FUNCTIONS_HPP

#include <cstdlib>
#include <functional>
#include <queue>
#include <ts3_functions.h>

static const uint64 ADMIN_GROUP_ID = 5;

/** Commands that should be executed delayed in the main loop. */
extern std::queue<std::function<void()> > commandQueue;

void initTS3Plugin();
void shutdownTS3Plugin();
void sendMessage(const char *message);
void workCommandQueue();

#endif
