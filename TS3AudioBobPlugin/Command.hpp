#ifndef COMMAND_HPP
#define COMMAND_HPP

#include <Definitions.hpp>

#include <Utils.hpp>
#include <public_definitions.h>

#include <algorithm>
#include <array>
#include <iostream>
#include <memory>
#include <sstream>
#include <string>

class ServerConnection;

struct CommandResult
{
	bool success;
	std::shared_ptr<std::string> errorMessage;

	CommandResult(bool success = true, std::shared_ptr<std::string> errorMessage = std::shared_ptr<std::string>()) :
		success(success),
		errorMessage(errorMessage)
	{
	}
};

class AbstractCommandExecutor
{
public:
	// Returns if the command was handled
	virtual CommandResult operator()(ServerConnection *connection, anyID sender, const std::string &message) = 0;
	virtual std::string getHelp() const = 0;
};

template<class... Args>
class CommandExecutor : public AbstractCommandExecutor
{
public:
	typedef std::function<CommandResult(ServerConnection *connection, anyID sender, const std::string &message, Args...)> FuncType;

private:
	/** The function that should be invoked by this command. */
	FuncType fun;
	/** True, if this command should ignore arguments that can't be passed to the method. */
	bool ignoreMore;

public:
	CommandExecutor(FuncType fun, bool ignoreMore = false) :
		fun(fun),
		ignoreMore(ignoreMore)
	{
	}

protected:
	/** Extracts an argument from a string and returns the parsed argument
	 *  and the leftover string.
	 *  If the parsing failed, the resulting message is undefined.
	 *
	 *  @return If the parsing was successful.
	 */
	template<class T>
	bool parseArgument(std::string &message, T *result)
	{
		// Default conversion with a string stream
		std::istringstream input(message);
		input >> *result;
		if(input.eof())
			message.clear();
		else if(!input)
			return false;
		else
			message.erase(message.begin(), message.begin() + input.tellg());
		Utils::log(":()");
		return true;
	}

	/** A specialisation for bool to allow more, better values. */
	bool parseArgument(std::string &message, bool *result)
	{
		// Default conversion with a string stream
		std::istringstream input(message);
		std::string str;
		input >> str;
		if(input.eof())
			message.clear();
		else if(!input)
			return false;
		else
			message.erase(message.begin(), message.begin() + input.tellg());
		std::transform(str.begin(), str.end(), str.begin(), ::tolower);
		// Possible true and false values
		const static std::array<std::string, 3> yes = { "on", "true", "yes" };
		const static std::array<std::string, 3> no = { "off", "false", "no" };
		Utils::log("Searching for '%s'", str.c_str());
		bool found = false;
		// std::find(yes.cbegin(), yes.cend(), str) != yes.cend()
		for(std::array<std::string, 3>::const_iterator it = yes.cbegin(); it != yes.cend(); it++)
		{
			if(*it == str)
			{
				*result = true;
				found = true;
			} else
				Utils::log("'%s' != '%s'", it->c_str(), str.c_str());
		}
		if(!found)
		{
			for(std::array<std::string, 3>::const_iterator it = no.cbegin(); it != no.cend(); it++)
			{
				if(*it == str)
				{
					*result = false;
					found = true;
				} else
					Utils::log("'%s' != '%s'", it->c_str(), str.c_str());
			}
			if(!found)
				// Value not found
				return false;
		}
		return true;
	}

private:
	/** Calls fun by adding parameters recursively.
	 *  The function that is the last layer of the recursion.
	 */
	template<int... Is>
	CommandResult execute(std::string message, std::function<CommandResult()> f,
		Utils::IntSequence<Is...>)
	{
		if(!message.empty() && !ignoreMore)
			return CommandResult(false, std::shared_ptr<std::string>(new std::string("error too many parameters")));
		return f();
	}

	template<class P, class... Params, int... Is>
	CommandResult execute(std::string message,
		std::function<CommandResult(P p, Params... params)> f, Utils::IntSequence<Is...>)
	{
		std::string msg = Utils::strip(message, true, false);
		P p;
		if(!parseArgument(msg, &p))
			return CommandResult(false, std::shared_ptr<std::string>(new std::string("error wrong parameter type")));
		
		// Bind this parameter
		std::function<CommandResult(Params...)> f2 = myBind(f, p, Utils::IntSequenceCreator<sizeof...(Params)>());
		return execute(msg, f2, Utils::IntSequenceCreator<sizeof...(Params)>());
	}

public:
	CommandResult operator()(ServerConnection *connection, anyID sender, const std::string &message) override
	{
		// Bind connection
		std::function<CommandResult(anyID, const std::string&, Args...)> f1 = myBind(fun, connection,
			Utils::IntSequenceCreator<sizeof...(Args) + 2>());
		// Bind sender
		std::function<CommandResult(const std::string&, Args...)> f2 = myBind(f1, sender,
			Utils::IntSequenceCreator<sizeof...(Args) + 1>());
		// Bind message
		std::function<CommandResult(Args...)> f = myBind(f2, message, Utils::IntSequenceCreator<sizeof...(Args)>());
		return execute(message, f, Utils::IntSequenceCreator<sizeof...(Args)>());
	}
};

template<class... Args>
class StringCommandExecutor : public CommandExecutor<Args...>
{
public:
	typedef typename CommandExecutor<Args...>::FuncType FuncType;

private:
	/** The string that identities this command in lowercase. */
	const std::string command;
	const std::string help;

public:
	StringCommandExecutor(const std::string &command, const std::string &help,
		FuncType fun, bool ignore = false) : CommandExecutor<Args...>(fun, ignore),
		command(command),
		help(help)
	{
	}

	CommandResult operator()(ServerConnection *connection, anyID sender, const std::string &message) override
	{
		const std::string msg = Utils::strip(message);
		std::string::const_iterator pos = std::find_if(msg.begin(), msg.end(), Utils::isSpace);
		std::string cmd = Utils::strip(pos == msg.end() ? msg : std::string(msg.begin(), pos));
		const std::string args = pos == msg.end() ? "" : Utils::strip(std::string(pos, msg.end()));
		std::transform(cmd.begin(), cmd.end(), cmd.begin(), ::tolower);
		if(cmd != command)
			return CommandResult(false);
		return CommandExecutor<Args...>::operator()(connection, sender, args);
	}

	std::string getHelp() const override
	{
		return help;
	}
};

#endif
