CXXFLAGS = -std=c++0x -fPIC -fvisibility=hidden -Wall -Wextra -Wfatal-errors -Wno-format-security -Wno-missing-braces -g -Iinclude -I.

objects = Plugin.o ServerBob.o Utils.o Command.o ServerConnection.o

all: libts3audiobob.so

libts3audiobob.so: $(objects) Makefile
	$(CXX) -shared  $(objects) -o libts3audiobob.so

%.o : %.cpp %.hpp
	$(CXX) -c $(CXXFLAGS) $< -o $@

clean:
	rm -f $(objects)
	rm -f plugin.so
