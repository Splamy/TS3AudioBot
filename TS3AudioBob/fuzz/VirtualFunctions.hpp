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
