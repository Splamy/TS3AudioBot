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

#include "Utils.hpp"

#include <cctype>
#include <random>

std::string Utils::formatArgument(const std::string &format, const std::string &arg)
{
	if (format.empty())
		return arg;
	std::string::size_type start = format[0] == '-' ? 1 : 0;
	std::istringstream in(format.substr(start));
	std::string::size_type size;
	in >> size;
	if (!in && !in.eof())
		throw std::invalid_argument("Unknown string formatting options");
	if (size <= arg.size())
		return arg;
	std::string fill(size - arg.size(), ' ');
	if (format[0] == '-')
		return fill + arg;
	else
		return arg + fill;
}

std::string Utils::getFormattedString(const std::string &/*format*/, std::size_t /*index*/)
{
	throw std::invalid_argument("Can't find the argument at the specified "
		"index.");
}

bool Utils::isSpace(char c)
{
	return std::isspace(c) != 0;
}

std::string Utils::strip(const std::string &input, bool left, bool right)
{
	if (input.empty())
		return std::string();

	std::string::const_iterator start = input.begin();
	std::string::const_iterator end = input.end() - 1;
	if (left)
	{
		while (start <= end && std::isspace(*start))
			start++;
	}
	if (right)
	{
		while (end >= start && std::isspace(*end))
			end--;
	}
	if (start > end)
		return "";
	return std::string(start, end + 1);
}

std::string& Utils::replace(std::string &input, const std::string &target,
	const std::string &replacement)
{
	std::string::size_type pos = 0;
	while ((pos = input.find(target, pos)) != std::string::npos)
	{
		input.replace(pos, target.size(), replacement);
		pos += replacement.size();
	}
	return input;
}

bool Utils::startsWith(const std::string &string, const std::string &prefix)
{
	return prefix.size() <= string.size() && std::equal(prefix.begin(),
		prefix.end(), string.begin());
}

bool Utils::endsWith(const std::string &string, const std::string &suffix)
{
	return suffix.size() <= string.size() && std::equal(suffix.begin(),
		suffix.end(), string.end() - suffix.size());
}

std::string Utils::sanitizeAscii(const std::string &input)
{
	std::vector<char> result(input.size());
	int j = 0;
	char c;
	for (int i = 0; (c = input[i]); i++)
	{
		// ' ' until '~'
		if (c >= 32 && c <= 126)
			result[j++] = c;
	}
	result[j] = '\0';
	return std::string(result.data());
}

std::string& Utils::sanitizeLines(std::string &input)
{
	Utils::replace(input, "\\", "\\\\");
	Utils::replace(input, "\n", "\\n");
	Utils::replace(input, "\r", "\\r");
	return input;
}

std::string Utils::format(std::string format)
{
	return format;
}

int Utils::getRandomNumber(int min, int max)
{
	std::random_device random;
	std::mt19937 generator(random());
	std::uniform_int_distribution<> uniform(min, max);
	return uniform(generator);
}
