#ifndef TEST_UTILS_HPP
#define TEST_UTILS_HPP

#include <cppunit/extensions/HelperMacros.h>

#define CPPUNITEX_TEST_TIMELIMIT(testMethod, timeLimit) \
	CPPUNIT_TEST_SUITE_ADD_TEST(new TimeOutTestCaller<TestFixtureType>(\
		namer.getTestNameFor(#testMethod),\
		&TestFixtureType::testMethod,\
		factory.makeFixture(),\
		timeLimit))

class TestUtils : public CPPUNIT_NS::TestFixture
{
	CPPUNIT_TEST_SUITE(TestUtils);
	CPPUNIT_TEST(isSpace);
	CPPUNIT_TEST(replace);
	CPPUNIT_TEST(strip);
	CPPUNIT_TEST(borders);
	CPPUNIT_TEST(format);
	CPPUNIT_TEST(random);
	CPPUNIT_TEST_SUITE_END();

protected:
	void isSpace();
	void replace();
	void strip();
	/** Test starts- and endsWith. */
	void borders();
	void format();
	void random();

public:
	void setUp();
};

#endif
