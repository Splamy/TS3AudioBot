#ifndef UTILS_HPP
#define UTILS_HPP

#include <functional>
#include <string>
#include <vector>

namespace Utils
{
	// Define things that are used to bind parameters to a variadic template function
	/** A sequence of integers */
	template<int...>
	struct IntSequence{};
	/** Used to create a sequence of integers recursively from 0 - (I - 1)
		(including I and appending Is if they exist) */
	template<int I, int... Is>
	struct IntSequenceCreator : IntSequenceCreator<I - 1, I - 1, Is...> {};
	/** Final template instanciation of the recursion
		(break the recursion when 0 is reached) */
	template<int... Is>
	struct IntSequenceCreator<0, Is...> : IntSequence<Is...>{};
	/** A placeholder that holds an int.
		It should be used with the IntSequenceCreator to generate a sequence of placeholders */
	template<int>
	struct Placeholder{};

	template<class R, class... Args, class P, class P2, int... Is>
	std::function<R(Args...)> myBindIntern(const std::function<R(P, Args...)> &fun, P2 p, IntSequence<Is...>)
	{
		std::function<R(Args...)> f = std::bind(fun, p, Placeholder<Is>()...);
		return f;
	}

	/** Binds a parameter to a function.
	 *  A real life example can be seen in CommandExecutor (Command.hpp).
	 */
	template<class R, class... Args, class P, class P2>
	std::function<R(Args...)> myBind(const std::function<R(P, Args...)> &fun, P2 p)
	{
		return myBindIntern(fun, p, IntSequenceCreator<sizeof...(Args)>());
	}

	// FIXME Only for more up-to-date compilers than we have on the server...
	/*template<typename I, class P, class... Ps>
	auto myBind(const std::function<I> &fun, P p, Ps... ps) -> decltype(myBind(myBind(fun, p), ps...));

	template<typename I, class P, class... Ps>
	auto myBind(const std::function<I> &fun, P p, Ps... ps) -> decltype(myBind(myBind(fun, p), ps...))
	{
		return myBind(myBind(fun, p), ps...);
	}*/

	bool isSpace(char c);
	/** Returns a string with all whitespaces stripped at the beginning and the end. */
	std::string strip(const std::string &input, bool left = true, bool right = true);
	/** Replaces occurences of a string in-place. */
	std::string& replace(std::string &input, const std::string &target, const std::string &replacement);
	/** Checks if the beginning of a string is another string. */
	bool startsWith(const std::string &string, const std::string &prefix);
	/** Creates a string without non-ascii and control characters. */
	std::string onlyAscii(const std::string &input);
	/** Logs a method to console. */
	template<class... Args>
	void log(const std::string &format, Args... args)
	{
		printf(format.c_str(), args...);
		printf("\n");
	}

	template<class... Args>
	std::string format(const std::string &format, Args... args)
	{
		std::vector<char> buf(1 + std::snprintf(NULL, 0, format.c_str(), args...));
		std::snprintf(buf.data(), buf.size(), format.c_str(), args...);
		return std::string(buf.cbegin(), buf.cend() - 1);
	}

	// max is not contained in the result range
	int getRandomNumber(int min, int max);
}

// Define it as placeholder for the standard library so we can still bind the
// left over parameters
namespace std
{
	// The index of the placeholder will be its stored integer
	// Increment the indices because the placeholder expects 1 for the first placeholder
	template<int I>
	struct is_placeholder<Utils::Placeholder<I> > : integral_constant<int, I + 1>{};
}

#endif
