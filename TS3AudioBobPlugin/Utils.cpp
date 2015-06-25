#include "Utils.hpp"

using namespace Utils;

bool Utils::isSpace(char c)
{
	return std::isspace(c);
}

std::string Utils::strip(const std::string &input, bool left, bool right)
{
	if(input.empty())
		return std::string();

	std::string::const_iterator start = input.begin();
	std::string::const_iterator end = input.end() - 1;
	if(left)
	{
		while(start <= end && std::isspace(*start))
			start++;
	}
	if(right)
	{
		while(end >= start && std::isspace(*end))
			end--;
	}
	if(start > end)
		return "";
	return std::string(start, end + 1);
}

std::string& Utils::replace(std::string &input, const std::string &target, const std::string &replacement)
{
	std::size_t pos;
	while((pos = input.find(target)) != std::string::npos)
		input.replace(pos, target.size(), replacement);
	return input;
}

bool Utils::startsWith(const std::string &string, const std::string &prefix)
{
	return prefix.size() <= string.size() && std::equal(prefix.begin(), prefix.end(), string.begin());
}

// Only print ascii chars and no control characters (maybe there can be problems
// with Remote Code Execution, that has to be verified)
std::string Utils::onlyAscii(const std::string &input)
{
	std::vector<char> result(input.size());
	int j = 0;
	char c;
	for(int i = 0; (c = input[i]); i++)
	{
		// ' ' - '~'
		if(c >= 32 && c <= 126)
			result[j++] = c;
	}
	result[j] = '\0';
	std::string str(result.data());
	return std::move(str);
}

int Utils::getRandomNumber(int min, int max)
{
	// Generate random number
	//std::random_device random;
	//std::mt19937 generator(random());
	//std::uniform_int_distribution<int> uniform(min, max);
	//return uniform(generator);
	return rand() % (max - min) + min;
}
