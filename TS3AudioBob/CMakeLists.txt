cmake_minimum_required(VERSION 2.6.1)
project(TS3AudioBob)

set(CMAKE_MODULE_PATH ${CMAKE_MODULE_PATH} "${PROJECT_SOURCE_DIR}/cmake")

option(BUILD_TESTS "Build unit tests" OFF)
option(BUILD_FUZZ "Build fuzzying binary" OFF)
option(USE_ASAN "Build with Leak/Address sanitizer" OFF)

if(BUILD_TESTS)
	enable_testing()
endif()

if(NOT CMAKE_BUILD_TYPE)
	message(STATUS "No build type selected, default to 'Debug'")
	set(CMAKE_BUILD_TYPE Debug)
endif()

if(UNIX AND NOT APPLE AND USE_ASAN)
	set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -fsanitize=address -fno-omit-frame-pointer")
endif()

set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall -Wextra -Wfatal-errors -std=c++11 -fPIC -fvisibility=hidden -pthread")
set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -lpthread")
set(CMAKE_EXECUTABLE_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} -lpthread")
set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} -g")
set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} -O3 -mtune=native -march=native")

include_directories("${PROJECT_SOURCE_DIR}/Include" "${PROJECT_SOURCE_DIR}/src")

## Plugin
file(GLOB_RECURSE LIB_SOURCES "${PROJECT_SOURCE_DIR}/src/*.cpp")
add_library(ts3audiobob SHARED ${LIB_SOURCES})

find_package(FFmpeg REQUIRED)

include_directories(${FFMPEG_INCLUDE_DIRS})

target_link_libraries(ts3audiobob ${FFMPEG_LIBRARIES})

if(UNIX AND NOT APPLE AND USE_ASAN)
	target_link_libraries(ts3audiobob "-fsanitize=address")
endif()

## Test
if(BUILD_TESTS)
	file(GLOB_RECURSE TEST_SOURCES "${PROJECT_SOURCE_DIR}/src/*.cpp" "${PROJECT_SOURCE_DIR}/test/*.cpp")

	find_package(CppUnit)

	if(CPPUNIT_FOUND)
		add_executable(ts3audiobobtest ${TEST_SOURCES})
		include_directories(${CPPUNIT_INCLUDE_DIR})
		target_link_libraries(ts3audiobobtest
			${FFMPEG_LIBRARIES}
			${CPPUNIT_LIBRARY})

		add_test(UnitTests test)

		if(UNIX AND NOT APPLE AND USE_ASAN)
			target_link_libraries(ts3audiobobtest "-fsanitize=address")
		endif()
	endif()
endif()

## Fuzz
if(BUILD_FUZZ)
	file(GLOB_RECURSE FUZZ_SOURCES "${PROJECT_SOURCE_DIR}/src/*.cpp" "${PROJECT_SOURCE_DIR}/fuzz/*.cpp")

	#set(CMAKE_CXX_COMPILER /usr/bin/afl-clang++)
	#set(CMAKE_C_COMPILER /usr/bin/afl-clang)
	add_executable(ts3audiobobfuzz ${FUZZ_SOURCES})
	target_link_libraries(ts3audiobobfuzz
		${FFMPEG_LIBRARIES})

	if(UNIX AND NOT APPLE AND USE_ASAN)
		target_link_libraries(ts3audiobobfuzz "-fsanitize=address")
	endif()
endif()
