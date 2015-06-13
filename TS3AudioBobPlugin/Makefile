CXXFLAGS = -std=c++0x -fPIC -Wall -Wextra -Wfatal-errors -Wno-format-security -g -Iinclude

objects = plugin.o

all: plugin.so

plugin.so: plugin.o Makefile
	$(CXX) -shared  $(objects) -o plugin.so

%.o : %.cpp %.hpp
	$(CXX) -c $(CXXFLAGS) $< -o $@

clean:
	rm -f $(objects)
	rm -f plugin.so
