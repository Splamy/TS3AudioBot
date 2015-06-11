all: plugin.so

plugin.so: plugin.cpp plugin.hpp Makefile
	g++ -std=c++0x -fPIC -Wall -Wextra -Wfatal-errors -Wno-format-security -g -shared -o plugin.so plugin.cpp -Iinclude
