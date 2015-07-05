#include "TsApi.hpp"

#include <public_errors.h>

TsApi::TsApi(const TS3Functions &functions) :
	functions(functions)
{
}

const TS3Functions& TsApi::getFunctions() const
{
	return functions;
}

bool TsApi::handleTsError(unsigned int error) const
{
	if (error != ERROR_ok)
	{
		char* errorMsg;
		if (functions.getErrorMessage(error, &errorMsg) == ERROR_ok)
		{
			// Send the message to the bot
			std::string msg = errorMsg;
			functions.freeMemory(errorMsg);
			log("TeamSpeak-error: {0}", msg);
		} else
			log("TeamSpeak-double-error ({0})", error);
		return false;
	}
	return true;
}
