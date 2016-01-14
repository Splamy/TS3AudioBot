#ifndef COMMANDS_COMMAND_HPP
#define COMMANDS_COMMAND_HPP

#include "AbstractCommand.hpp"

#include "Utils.hpp"

#include <algorithm>
#include <functional>
#include <sstream>

#define COMMAND_DEBUG

#ifdef COMMAND_DEBUG
	#include <iostream>
#endif

namespace CommandSystem
{
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
	bool parseArgument(std::string &message, bool *result);

	/** Search for a best matching string for input in possible.
	 *  The result is a list of matching strings where the algorithm wasn't
	 *  able to find any preference.
	 */
	std::vector<std::string> choose(const std::vector<std::string> &possible,
		const std::string &input);

	std::vector<std::string> chooseWord(const std::vector<std::string> &possible,
		std::string &input);
}

template <class... Args>
class Command : public AbstractCommand
{
public:
	typedef std::function<CommandResult(ServerConnection *connection,
		User *sender, const std::string &message, Args...)> FuncType;

private:
	/** The name of this command, e.g. 'status'. */
	std::string name;
	/** The parameters of this command, e.g. '<id>' */
	std::string parameters;
	/** The description of this command.
	 *  If this description is empty, it will be merged with the description of
	 *  the previous command if it has the same name.
	 */
	std::string description;
	bool displayDescription;
	FuncType fun;

public:
	Command(const std::string &name, const std::string &parameters, FuncType fun,
		const std::string &description = "",
		bool displayDescription = true) :
		name(name),
		parameters(parameters),
		description(description),
		displayDescription(displayDescription),
		fun(fun)
	{
	}
	virtual ~Command() {}

private:
	/** Calls fun by adding parameters recursively.
	 *  The function that is the last layer of the recursion.
	 */
	CommandResult execute(std::string message, std::function<CommandResult()> f)
		const
	{
		if (!message.empty())
			return CommandResult(CommandResult::TRY_NEXT,
				"error too many parameters");
		return f();
	}

	template <class P, class... Params>
	CommandResult execute(std::string message,
		std::function<CommandResult(P p, Params... params)> f) const
	{
		if (message.empty())
			return CommandResult(CommandResult::TRY_NEXT,
				"error too few parameters");
		std::string msg = Utils::strip(message, true, false);
		P p;
		if (!CommandSystem::parseArgument(msg, &p))
			return CommandResult(CommandResult::ERROR,
				"error wrong parameter type");

		// Bind this parameter
		std::function<CommandResult(Params...)> f2 = Utils::myBind(f, p);
		return execute(msg, f2);
	}

	/** A specialisation if a string is the last parameter.
	 *  The rest of the text will be the parameter.
	 */
	CommandResult execute(std::string message,
		std::function<CommandResult(std::string)> f) const
	{
		return f(Utils::strip(message, true, false));
	}

public:
	const std::string& getName() const override
	{
		return name;
	}

	std::vector<std::pair<std::string, std::string> > createDescriptions() const
		override
	{
		std::vector<std::pair<std::string, std::string> > result;
		if (displayDescription)
			result.emplace_back(parameters, description);
		return result;
	}

	CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &message) const override
	{
		// Bind already known arguments
		std::function<CommandResult(Args...)> f = Utils::myBind(fun,
			connection, sender, message);
#ifdef COMMAND_DEBUG
		auto r = execute(message, f);
		std::cout << Utils::format("Trying to execute '{0}' '{1}' → {2} {3}\n",
			name, parameters, r.result, r.errorMessage);
		return r;
#endif
		return execute(message, f);
	}
};

#endif
