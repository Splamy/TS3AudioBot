#include <cppunit/config/SourcePrefix.h>
#include "TestUtils.hpp"

#include <Utils.hpp>

CPPUNIT_TEST_SUITE_REGISTRATION(TestUtils);

void TestUtils::isSpace()
{
	CPPUNIT_ASSERT_MESSAGE("Space",           Utils::isSpace(' '));
	CPPUNIT_ASSERT_MESSAGE("Vertical space",  Utils::isSpace('\v'));
	CPPUNIT_ASSERT_MESSAGE("Tab",             Utils::isSpace('\t'));
	CPPUNIT_ASSERT_MESSAGE("Newline",         Utils::isSpace('\n'));
	CPPUNIT_ASSERT_MESSAGE("Carriage return", Utils::isSpace('\r'));
}

void TestUtils::replace()
{
	std::string input = "asDF";
	CPPUNIT_ASSERT_EQUAL(std::string("asDF"), Utils::replace(input, "z", "a"));
	input = "asdF";
	CPPUNIT_ASSERT_EQUAL(std::string("asDF"), Utils::replace(input, "d", "D"));
	input = "asdF";
	CPPUNIT_ASSERT_EQUAL(std::string("asdddF"), Utils::replace(input, "d", "ddd"));
	input = "\ra\ns\r\nF\r\n";
	CPPUNIT_ASSERT_EQUAL(std::string("\ra\nsF"), Utils::replace(input, "\r\n", ""));
}

void TestUtils::strip()
{
	std::string input = "    text    ";
	CPPUNIT_ASSERT_EQUAL(std::string("text"), Utils::strip(input));
	input = "    text    ";
	CPPUNIT_ASSERT_EQUAL(std::string("text    "), Utils::strip(input, true, false));
	input = "    text    ";
	CPPUNIT_ASSERT_EQUAL(std::string("    text"), Utils::strip(input, false));
	input = "    text    ";
	CPPUNIT_ASSERT_EQUAL(std::string("    text    "), Utils::strip(input, false, false));
	input = "a    text    b";
	CPPUNIT_ASSERT_EQUAL(std::string("a    text    b"), Utils::strip(input));
}

void TestUtils::borders()
{
	std::string input = "Start Text End";
	CPPUNIT_ASSERT(Utils::startsWith(input, "Start T"));
	CPPUNIT_ASSERT(Utils::startsWith(input, "S"));
	CPPUNIT_ASSERT(!Utils::startsWith(input, "tart"));
	CPPUNIT_ASSERT(Utils::startsWith(input, ""));
	input = "";
	CPPUNIT_ASSERT(Utils::startsWith(input, ""));
	CPPUNIT_ASSERT(!Utils::startsWith(input, " "));
	CPPUNIT_ASSERT(!Utils::startsWith(input, "Start Text End"));

	input = "Start Text End";
	CPPUNIT_ASSERT(Utils::endsWith(input, "t End"));
	CPPUNIT_ASSERT(Utils::endsWith(input, "d"));
	CPPUNIT_ASSERT(!Utils::endsWith(input, "En"));
	CPPUNIT_ASSERT(Utils::endsWith(input, ""));
	input = "";
	CPPUNIT_ASSERT(Utils::endsWith(input, ""));
	CPPUNIT_ASSERT(!Utils::endsWith(input, " "));
	CPPUNIT_ASSERT(!Utils::endsWith(input, "Start Text End"));
}

void TestUtils::format()
{
	CPPUNIT_ASSERT_EQUAL(std::string("texttext"), Utils::format("texttext", 1, 2, "", 0.1));
	CPPUNIT_ASSERT_EQUAL(std::string("a1,2"), Utils::format("a{0},{1}", 1, 2, "", 0.1));
	CPPUNIT_ASSERT_EQUAL(std::string(""), Utils::format("{2}", 1, 2, "", 0.1));
	CPPUNIT_ASSERT_EQUAL_MESSAGE("No arguments", std::string("{0}"), Utils::format("{0}"));
	CPPUNIT_ASSERT_EQUAL_MESSAGE("No arguments", std::string("{-1}"), Utils::format("{-1}"));
	CPPUNIT_ASSERT_THROW_MESSAGE("Not enough arguments", Utils::format("{1}", 0), std::invalid_argument);
	CPPUNIT_ASSERT_THROW_MESSAGE("Negative argument", Utils::format("{-1}", 0), std::invalid_argument);
	CPPUNIT_ASSERT_THROW_MESSAGE("Unclosed brace 1", Utils::format("{0", 0, 1), std::invalid_argument);
	CPPUNIT_ASSERT_THROW_MESSAGE("Unclosed brace 2", Utils::format("{0}}", 0, 1), std::invalid_argument);
	CPPUNIT_ASSERT_THROW_MESSAGE("Unopened brace 1", Utils::format("0}", 0, 1), std::invalid_argument);
	CPPUNIT_ASSERT_THROW_MESSAGE("Unopened brace 2", Utils::format("{{0}", 0, 1), std::invalid_argument);

	CPPUNIT_ASSERT_EQUAL_MESSAGE("Double braces 1", std::string("{0}"), Utils::format("{{0}}", 0));
	CPPUNIT_ASSERT_EQUAL_MESSAGE("Double braces 2", std::string("{0"), Utils::format("{{0", 0));
	CPPUNIT_ASSERT_EQUAL_MESSAGE("Double braces 3", std::string("0}"), Utils::format("0}}", 0));
}

void TestUtils::random()
{
	for (std::size_t count = 100; count; count--)
	{
		int n = Utils::getRandomNumber(42, 1337);
		CPPUNIT_ASSERT_MESSAGE("Lower border 1", 42 <= n);
		CPPUNIT_ASSERT_MESSAGE("Upper border 1", 1337 >= n);
	}
	for (std::size_t count = 100; count; count--)
	{
		int n = Utils::getRandomNumber(-100, 100);
		CPPUNIT_ASSERT_MESSAGE("Lower border 2", -100 <= n);
		CPPUNIT_ASSERT_MESSAGE("Upper border 2", 100 >= n);
	}
}

void TestUtils::setUp()
{
}
