#ifndef UTILS_HPP
#define UTILS_HPP

#include <functional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace Utils
{
	// Define things that are used to bind parameters to a variadic template
	// function
	/** A sequence of integers */
	template <int...>
	struct IntSequence{};
	/** Used to create a sequence of integers recursively from 0 - (I - 1)
		(including I and appending Is if they exist) */
	template <int I, int... Is>
	struct IntSequenceCreator : IntSequenceCreator<I - 1, I - 1, Is...> {};
	/** Final template instanciation of the recursion
		(break the recursion when 0 is reached) */
	template <int... Is>
	struct IntSequenceCreator<0, Is...> : IntSequence<Is...>{};
	/** A placeholder that holds an int.
	 *  It should be used with the IntSequenceCreator to generate a sequence of
	 *  placeholders.
	 */
	template <int>
	struct Placeholder{};

	template <class C, class R, class... Args, int... Is>
	std::function<R(Args...)> myBindMemberIntern(R(C::*fun)(Args...),
		C *c, IntSequence<Is...>)
	{
		std::function<R(Args...)> f = std::bind(fun, c, Placeholder<Is>()...);
		return f;
	}

	/** Binds an object to a member function. */
	template <class C, class R, class... Args>
	std::function<R(Args...)> myBindMember(R(C::*fun)(Args...), C *c)
	{
		return myBindMemberIntern(fun, c, IntSequenceCreator<sizeof...(Args)>());
	}

	template <class R, class... Args, class P, class P2, int... Is>
	std::function<R(Args...)> myBindIntern(
		const std::function<R(P, Args...)> &fun, P2 p, IntSequence<Is...>)
	{
		return std::bind(fun, p, Placeholder<Is>()...);
	}

	/** Binds a parameter to a function.
	 *  A real life example can be seen in CommandExecutor (Command.hpp).
	 */
	template <class R, class... Args, class P, class P2>
	std::function<R(Args...)> myBind(const std::function<R(P, Args...)> &fun,
		P2 p)
	{
		return myBindIntern(fun, p, IntSequenceCreator<sizeof...(Args)>());
	}

	// Bind more than one Argument at once.
	// In my imagination that should call itself recursively, but it doesn't
	// work :( (gcc only accepts two parameters, clang supports up to 3 if you
	// copy this declaration...)
	template <typename F, class P, class... Ps>
	auto myBind(const std::function<F> &fun, P p, Ps... ps)
		-> decltype(myBind(myBind(fun, p), ps...));

	template <typename F, class P, class... Ps>
	auto myBind(const std::function<F> &fun, P p, Ps... ps)
		-> decltype(myBind(myBind(fun, p), ps...))
	{
		return myBind(myBind(fun, p), ps...);
	}

	// A lambda function that should be able to bind arguments like myBind.
	// Unfortunately this only works up to 2 arguments for gcc and clang.
	// You have to specify the left over arguments (Args) explicitely.
	template <class... Args, class R, class... P>
	std::function<R(Args...)> myBind2(const std::function<R(P..., Args...)> &fun, P... p)
	{
		std::function<R(Args...)> r = [&fun, &p...](Args... r) { return fun(p..., r...); };
		return r;
	}

	/** Convert an object to a string with the possibility to format the object.
	 */
	std::string formatArgument(const std::string &format, const std::string &arg);

	template <class T>
	std::string formatArgument(const std::string &/*format*/, T t)
	{
		std::ostringstream out;
		out << t;
		return out.str();
	}

	/** A function that throws an exception, used for the next function. */
	std::string getFormattedString(const std::string &format, std::size_t index);

	/** Extract an argument from a variadic template list and converts it to a
	 *  string.
	 */
	template <class T, class... Args>
	std::string getFormattedString(const std::string &format, std::size_t index, T t, Args... args)
	{
		if (index == 0)
			return formatArgument(format, t);
		else
			return getFormattedString(format, index - 1, args...);
	}

	bool isSpace(char c);
	/** Returns a string with all whitespaces stripped at the beginning and the
	 *  end.
	 */
	std::string strip(const std::string &input, bool left = true,
		bool right = true);
	/** Replaces occurences of a string in-place. */
	std::string& replace(std::string &input, const std::string &target,
		const std::string &replacement);
	/** Checks if the beginning of a string is another string. */
	bool startsWith(const std::string &string, const std::string &prefix);
	/** Checks if the ending of a string is another string. */
	bool endsWith(const std::string &string, const std::string &suffix);

	/** Only print readable ascii characters and no control characters (there
	 *  can be problems with terminals that interpret characters and we don't
	 *  want to have possible vulnerabilities).
	 */
	std::string sanitizeAscii(const std::string &input);

	std::string& sanitizeLines(std::string &input);

	/** A function that returns the argument. It is used if format is called
	 *  without any arguments that should be formatted. This is useful if you
	 *  e.g. want to print user input and pass that to a log function that
	 *  calls format(), so you don't have to use format("{0}", argument).
	 *
	 *  Be aware that this function also ignore brackets, so you shouldn't
	 *  escape them if you don't pass other arguments to format().
	 */
	std::string format(std::string format);

	/** This function inserts arguments into a string with the usage of streams.
	 *  The format string can contain normal characters, arguments are inserted
	 *  by {n} where n is the number of the argument that should be inserted,
	 *  starting with 0. For inserting the character literals { or }, use {{ or
	 *  }}.
	 *  To format an argument you can pass properties with the index like this:
	 *  {0:-15} to format a string to the length 15 and prepend spaces if
	 *  necessary.
	 *
	 *  @param format The string that contains the information how to format
	 *                the given arguments.
	 *  @param args   The arguments that will be used to format the string.
	 *  @return The formatted string.
	 */
	template <class... Args>
	std::string format(const std::string &format, Args... args)
	{
		std::ostringstream out;
		for (std::string::size_type i = 0; i < format.size(); i++)
		{
			char c = format[i];
			if (c == '{')
			{
				i++;
				if (format[i] == '{')
					// Escaped opening bracket
					out << '{';
				else
				{
					std::string::size_type split = format.find(':', i);
					std::string::size_type end = format.find('}', i);
					if (end == std::string::npos)
						throw std::invalid_argument("Error when formatting a "
							"string, are you missing the closing bracket?");
					std::string fmt = split > end ? "" : format.substr(
						split + 1, end);
					std::istringstream in(format.substr(i, split > end ?
						end - i : split - i));
					std::size_t index;
					in >> index;
					if ((!in &&  !in.eof()) || index > sizeof...(args))
						throw std::invalid_argument("Error when formatting a "
							"string, index coun't be parsed or is out of range.");

					// Insert the argument at the specified index
					out << getFormattedString(fmt, index, args...);

					i = end;
				}
			} else if (c == '}')
			{
				// Escaped closing bracket
				i++;
				if (format[i] != '}')
					throw std::invalid_argument("Error when formatting a "
						"string, are you missing the opening bracket?");
				out << '}';
			} else
				// Normal character
				out << c;
		}
		return out.str();
	}

	template <class T>
	std::vector<T> parseList(const std::string &str, bool *success)
	{
		std::istringstream in(str);
		char c;
		T t;
		std::vector<T> result;
		*success = false;
		while (!in.eof())
		{
			in >> t;
			if (!in && !in.eof())
				return;
			result.push_back(t);
			// Read the comma or other separating character
			in >> c;
			if (!in && !in.eof())
				return;
		}
		*success = true;
		return result;
	}

	/** Creates a new random number from the range [min, max] (can contain min
	 *  and max).
	 */
	int getRandomNumber(int min, int max);
}

// Define it as placeholder for the standard library so we can still bind the
// left over parameters
namespace std
{
	/** The index of the placeholder will be its stored integer
	 *  Increment the indices because the placeholder expects 1 for the first
	 *  placeholder.
	 */
	template <int I>
	struct is_placeholder<Utils::Placeholder<I> > :
		integral_constant<int, I + 1>{};
}

#endif
