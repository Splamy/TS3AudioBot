#ifndef COMMANDS_ABSTRACT_COMMAND_HPP
#define COMMANDS_ABSTRACT_COMMAND_HPP

#include <string>
#include <utility>
#include <vector>

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
public:
	/** Returns the result of the execution. */
	virtual const std::string& getName() const = 0;
	virtual std::vector<std::pair<std::string, std::string> >
		createDescriptions() const = 0;
	/** Execute this command with the given information.
	 *  The message is the part of the input that should be parsed and used by
	 *  this command. completeMessage contains the original input.
	 */
	virtual CommandResult operator()(ServerConnection *connection, User *sender,
		const std::string &completeMessage, const std::string &message) const = 0;
	virtual ~AbstractCommand() {}
};

#endif
