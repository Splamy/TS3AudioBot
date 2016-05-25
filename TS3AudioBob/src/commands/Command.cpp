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

#include "Command.hpp"

/** A specialisation for bool to allow more, better values. */
bool CommandSystem::parseArgument(std::string &message, bool *result)
{
	const static std::vector<std::string> trueValues = { "on", "true", "yes", "1" };
	const static std::vector<std::string> allValues = { "on", "true", "yes", "1",
		"off", "false", "no", "0" };

	std::vector<std::string> possible = chooseWord(allValues, message);
	if (possible.empty())
		return false;

	// Look what possibilities were found
	bool foundTrue = false;
	bool foundFalse = false;
	for (const auto &p : possible)
	{
		if (std::find(trueValues.cbegin(), trueValues.cend(), p) != trueValues.cend())
			foundTrue = true;
		else
			foundFalse = true;
	}

	if (foundTrue && foundFalse)
		return false;
	*result = foundTrue;
	return true;
}

std::vector<std::string> CommandSystem::choose(const std::vector<std::string> &possible,
	const std::string &input)
{
#ifdef COMMAND_DEBUG
	std::cout << Utils::format("Testing {0} in", input);
	for (const auto &s : possible)
		std::cout << " " << s;
	std::cout << "\n";
#endif

	std::vector<std::pair<std::string, std::string::size_type> >
		possibilities(possible.size());
	std::transform(possible.begin(), possible.end(), possibilities.begin(),
		[](const std::string &s) -> std::pair<std::string, std::string::size_type>
		{ return std::make_pair(s, 0); });

	for (std::string::size_type msgIndex = 0; msgIndex < input.length(); msgIndex++)
	{
		// Stop if the command is over
		if (Utils::isSpace(input[msgIndex]))
		{
			// Skip following whitespaces
			do
			{
				msgIndex++;
			} while (msgIndex < input.length() && Utils::isSpace(input[msgIndex]));
			break;
		}
		// Stop if only one command is left
		if (possibilities.size() == 1)
		{
			// Skip to end of command
			while (msgIndex < input.length() && !Utils::isSpace(input[msgIndex]))
				msgIndex++;
			continue;
		}

		decltype(possibilities) newPossibilities;
		// Filter possibilities
		for (std::size_t i = 0; i < possibilities.size(); i++)
		{
			std::string::size_type newPos =
				possibilities[i].first.find(input[msgIndex], possibilities[i].second);
			if (newPos != std::string::npos)
				newPossibilities.emplace_back(possibilities[i].first, newPos + 1);
		}
		// Ignore this character if there are no results
		// ATTENTION: This can probably lead to funny behaviour ;)
		if (!newPossibilities.empty())
			possibilities = std::move(newPossibilities);
	}

	// Take the command with the lowest index
	std::string::size_type minIndex = std::string::npos;
	for (const auto &c : possibilities)
	{
		if (c.second < minIndex)
			minIndex = c.second;
	}
	for (std::size_t i = 0; i < possibilities.size(); i++)
	{
		if (possibilities[i].second != minIndex)
		{
			possibilities.erase(possibilities.begin() + i);
			i--;
		}
	}

	std::vector<std::string> result(possibilities.size());
	std::transform(possibilities.begin(), possibilities.end(), result.begin(),
		[](const std::pair<std::string, std::string::size_type> &s) -> std::string
		{ return s.first; });

#ifdef COMMAND_DEBUG
	std::cout << "Result:";
	for (const auto &s : result)
		std::cout << " " << s;
	std::cout << "\n";
#endif

	return result;
}

std::vector<std::string> CommandSystem::chooseWord(const std::vector<std::string> &possible,
	std::string &input)
{
	std::string stripped = Utils::strip(input, true, false);
	// Search for command and rest
	std::string::size_type commandEnd = 0;
	while (commandEnd < stripped.length() && !Utils::isSpace(stripped[commandEnd]))
		commandEnd++;
	std::string::size_type restStart = commandEnd;
	while (restStart < stripped.length() && Utils::isSpace(stripped[restStart]))
		restStart++;
	if (commandEnd == 0)
		return std::vector<std::string>();

	// Search if we can find a right method
	std::string command = stripped.substr(0, commandEnd);
	std::vector<std::string> commands = CommandSystem::choose(possible, command);

	input = stripped.substr(commandEnd);
	return commands;
}
