#include <cppunit/BriefTestProgressListener.h>
#include <cppunit/TextOutputter.h>
#include <cppunit/extensions/TestFactoryRegistry.h>
#include <cppunit/TestResult.h>
#include <cppunit/TestResultCollector.h>
#include <cppunit/TestRunner.h>

int main()
{
	CPPUNIT_NS::TestResult controller;
	CPPUNIT_NS::TestResultCollector result;
	CPPUNIT_NS::BriefTestProgressListener progress;

	controller.addListener(&result);
	controller.addListener(&progress);

	CPPUNIT_NS::TestRunner runner;
	runner.addTest(CPPUNIT_NS::TestFactoryRegistry::getRegistry().makeTest());
	runner.run(controller);

	// For compiler-friendly output, just use the CompilerOutputter
	CPPUNIT_NS::TextOutputter outputter(&result, CPPUNIT_NS::stdCOut());
	outputter.write();

	return result.wasSuccessful() ? 0 : 1;
}
