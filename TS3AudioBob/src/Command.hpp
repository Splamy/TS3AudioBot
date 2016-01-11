#ifndef COMMAND_HPP
#define COMMAND_HPP

#include <algorithm>
#include <array>
#include <memory>
#include <sstream>
#include <string>

#include <public_definitions.h>

#include <Definitions.hpp>
#include <Utils.hpp>

class ServerConnection;
class User;

struct CommandResult
{
	enum Result
	{
		SUCCESS,
		ERROR,
		/** Try the next command because this command didn't fit. */
		TRY_NEXT
	};

	Result result;
	std::string errorMessage;

	CommandResult(Result result = SUCCESS, std::string errorMessage = "") :
		result(result),
		errorMessage(errorMessage)
	{
	}
	operator bool() const
	{
		return result == SUCCESS;
	}
};

class AbstractCommand
{
	/** Returns the result of the execution. */
	virtual const std::string& getName() const = 0;
	virtual std::vector<std::pair<std::string, std::string> >
		createDescriptions() const = 0;
	virtual CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &message) const = 0;
	virtual ~AbstractCommand() {}
};

template <class... Args>
class Command : public AbstractCommand
{
public:
	typedef std::function<CommandResult(ServerConnection *connection,
		User *sender, const std::string &message, Args...)> FuncType;

private:
	/** The name of this command, e.g. 'status'. */
	std::string name;
	/** The description of this command.
	 *  If this description is empty, it will be merged with the description of
	 *  the previous command if it has the same name.
	 */
	std::string description;
	bool displayDescription;
	FuncType fun;

public:
	Command(const std::string &name, FuncType fun, const std::string &description = "",
		bool displayDescription = true) :
		name(name),
		description(description),
		displayDescription(displayDescription),
		fun(fun)
	{
	}
	virtual ~Command() {}

	virtual const std::string& getName() const;
	virtual std::vector<std::pair<std::string, std::string> > createDescriptions() const;
	virtual CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &message) const;
};

class AbstractCommandExecutor
{
public:
	// Returns if the command was handled
	virtual CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &message) = 0;
	virtual std::shared_ptr<const std::string> getCommandName() const = 0;
	virtual std::shared_ptr<const std::string> getHelp() const = 0;
};

template <class... Args>
class CommandExecutor : public AbstractCommandExecutor
{
public:
	typedef std::function<CommandResult(ServerConnection *connection,
		User *sender, const std::string &message, Args...)> FuncType;

private:
	/** The function that should be invoked by this command. */
	FuncType fun;
	/** True, if this command should ignore arguments that can't be passed to
	 *  the method.
	 */
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
	template <class T>
	bool parseArgument(std::string &message, T *result)
	{
		// Default conversion with a string stream
		std::istringstream input(message);
		input >> *result;
		if (input.eof())
			message.clear();
		else if (!input)
			return false;
		else
			message.erase(message.begin(), message.begin() + input.tellg());
		return true;
	}

	/** A specialisation for bool to allow more, better values. */
	bool parseArgument(std::string &message, bool *result)
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

private:
	/** Calls fun by adding parameters recursively.
	 *  The function that is the last layer of the recursion.
	 */
	CommandResult execute(std::string message, std::function<CommandResult()> f)
	{
		if (!message.empty() && !ignoreMore)
			return CommandResult(CommandResult::TRY_NEXT,
				"error too many parameters");
		return f();
	}

	template <class P, class... Params>
	CommandResult execute(std::string message,
		std::function<CommandResult(P p, Params... params)> f)
	{
		if (message.empty())
			return CommandResult(CommandResult::TRY_NEXT,
				"error too few parameters");
		std::string msg = Utils::strip(message, true, false);
		P p;
		if (!parseArgument(msg, &p))
			return CommandResult(CommandResult::TRY_NEXT,
				"error wrong parameter type");

		// Bind this parameter
		std::function<CommandResult(Params...)> f2 = Utils::myBind(f, p);
		return execute(msg, f2);
	}

public:
	CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &message) override
	{
		// Bind already known arguments
		std::function<CommandResult(Args...)> f = Utils::myBind(fun, connection,
			sender, message);
		return execute(message, f);
	}
};

template <class... Args>
class StringCommandExecutor : public CommandExecutor<Args...>
{
public:
	typedef typename CommandExecutor<Args...>::FuncType FuncType;

private:
	/** The string that identities this command in lowercase. */
	const std::string command;
	const std::shared_ptr<const std::string> commandString;
	const std::shared_ptr<const std::string> help;

public:
	StringCommandExecutor(const std::string &command, const std::string &help,
		FuncType fun, const std::string *commandString = nullptr,
		bool ignore = false, bool showHelp = true) :
		CommandExecutor<Args...>(fun, ignore),
		command(command),
		commandString(showHelp ? (commandString ?
			new std::string(*commandString) : new std::string(command)) : nullptr),
		help(showHelp ? new std::string(help) : nullptr)
	{
	}

	CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &message) override
	{
		const std::string msg = Utils::strip(message);
		std::string::const_iterator pos = std::find_if(msg.begin(), msg.end(),
			Utils::isSpace);
		std::string cmd = Utils::strip(pos == msg.end() ? msg :
			std::string(msg.begin(), pos));
		std::transform(cmd.begin(), cmd.end(), cmd.begin(), ::tolower);
		if (cmd != command)
			return CommandResult(CommandResult::TRY_NEXT);
		return CommandExecutor<Args...>::operator()(connection, sender, message);
	}

	std::shared_ptr<const std::string> getCommandName() const override
	{
		return commandString;
	}

	std::shared_ptr<const std::string> getHelp() const override
	{
		return help;
	}
};

#endif
