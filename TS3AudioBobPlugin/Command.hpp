#ifndef COMMAND_HPP
#define COMMAND_HPP

#include <Definitions.hpp>

#include <Utils.hpp>
#include <public_definitions.h>

#include <algorithm>
#include <memory>
#include <sstream>
#include <string>

class ServerConnection;

struct CommandResult
{
	bool success;
	std::shared_ptr<std::string> errorMessage;

	CommandResult(bool success = true, std::shared_ptr<std::string> errorMessage = NULL) :
		success(success),
		errorMessage(errorMessage)
	{
	}
};

class AbstractCommandExecutor
{
public:
	// Returns if the command was handled
	virtual CommandResult operator()(ServerConnection *connection, uint64 sender, const std::string &message) = 0;
	virtual std::string getHelp() const = 0;
};

template<class... Args>
class CommandExecutor : public AbstractCommandExecutor
{
public:
	typedef std::function<CommandResult(ServerConnection *connection, uint64 sender, const std::string &message, Args...)> FuncType;

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
		const std::string msg = Utils::strip(message, true, false);
		P p;
		std::istringstream input(msg);
		// Read booleans as true/false
		input >> std::boolalpha >> p;
		// Bind this parameter
		std::function<CommandResult(Params...)> f2 = myBind(f, p, Utils::IntSequenceCreator<sizeof...(Params)>());
		int pos;
		// Test if it was successful
		if(input.eof())
			pos = -1;
		else if(!input)
			return CommandResult(false, std::shared_ptr<std::string>(new std::string("error wrong parameter type")));
		else
			pos = input.tellg();
		// Drop the already read part of the message
		return input && execute(pos == -1 ? "" : msg.substr(pos), f2,
			Utils::IntSequenceCreator<sizeof...(Params)>());
	}

public:
	CommandResult operator()(ServerConnection *connection, uint64 sender, const std::string &message) override
	{
		// Bind connection
		std::function<CommandResult(uint64, const std::string&, Args...)> f1 = myBind(fun, connection,
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

	CommandResult operator()(ServerConnection *connection, uint64 sender, const std::string &message) override
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
