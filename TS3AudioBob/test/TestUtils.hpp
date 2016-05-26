// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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
