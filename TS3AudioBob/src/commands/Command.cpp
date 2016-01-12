#include "Command.hpp"

/** A specialisation for bool to allow more, better values. */
bool CommandParser::parseArgument(std::string &message, bool *result)
{
	// Default conversion with a string stream
	std::string str;
	if (!parseArgument(message, &str))
		return false;
	std::transform(str.begin(), str.end(), str.begin(), ::tolower);
	// Possible true and false values
	const static std::array<std::string, 3> yes = { "on", "true", "yes" };
	const static std::array<std::string, 3> no = { "off", "false", "no" };
	if (std::find(yes.cbegin(), yes.cend(), str) != yes.cend())
		*result = true;
	else if (std::find(no.cbegin(), no.cend(), str) != no.cend())
		*result = false;
	else
		return false;
	return true;
}
