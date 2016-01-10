# This file contains several functions used for the build process
import re
import subprocess

folder = "."
execfile("Tools/SConsLib.py")

# Functions
# Sets custom strings for commands
def setCommandStrings():
	env["ARCOMSTR"]			   = "Archiving $TARGET"
	env["ASCOMSTR"]			   = "Assembling $TARGET"
	env["ASPPCOMSTR"]			 = "Assembling $TARGET"
	env["BIBTEXCOMSTR"]		   = "Generating bibliography $TARGET"
	env["BITKEEPERCOMSTR"]		= "Fetching $TARGET"
	env["CCCOMSTR"]			   = "Compiling $TARGET"
	env["CVSCOMSTR"]			  = "Fetching $TARGET"
	env["CXXCOMSTR"]			  = "Compiling $TARGET"
	env["DOCBOOK_FOPCOMSTR"]	  = "Creating $TARGET"
	env["DOCBOOK_XMLLINTCOMSTR"]  = "Resolving XIncludes for $TARGET"
	env["DOCBOOK_XSLTPROCCOMSTR"] = "Transforming $TARGET"
	env["DVIPDFCOMSTR"]		   = "Converting $TARGET"
	env["F03COMSTR"]			  = "Compiling $TARGET"
	env["F03PPCOMSTR"]			= "Compiling $TARGET"
	env["F77COMSTR"]			  = "Compiling $TARGET"
	env["F77PPCOMSTR"]			= "Compiling $TARGET"
	env["F90COMSTR"]			  = "Compiling $TARGET"
	env["F90PPCOMSTR"]			= "Compiling $TARGET"
	env["F95COMSTR"]			  = "Compiling $TARGET"
	env["F95PPCOMSTR"]			= "Compiling $TARGET"
	env["FORTRANCOMSTR"]		  = "Compiling $TARGET"
	env["FORTRANPPCOMSTR"]		= "Compiling $TARGET"
	env["GSCOMSTR"]			   = "Calling ghostscript $TARGET"
	env["INSTALLSTR"]			 = "Installing $SOURCE to $TARGET"
	env["JARCOMSTR"]			  = "JARchiving $SOURCES into $TARGET"
	env["JAVACCOMSTR"]			= "Compiling $SOURCES to $TARGETS"
	env["JAVAHCOMSTR"]			= "Generating header/stub file(s) $TARGETS from $SOURCES"
	env["LATEXCOMSTR"]			= "Building $TARGET"
	env["LDMODULECOMSTR"]		 = "Building $TARGET"
	env["LEXCOMSTR"]			  = "Lexing $TARGET from $SOURCES"
	env["LATEXCOMSTR"]			= "Building $TARGET"
	env["LATEXCOMSTR"]			= "Building $TARGET"
	env["LINKCOMSTR"]			 = "Linking $TARGET"
	env["M4COMSTR"]			   = "Preprocessing $TARGET"
	env["MAKEINDEXCOMSTR"]		= "Making index for $TARGET"
	env["MIDLCOMSTR"]			 = "Compiling $TARGET"
	env["MSGFMTCOMSTR"]		   = "Compiling $TARGET"
	env["MSGINITCOMSTR"]		  = "Creating $TARGET"
	env["MSGMERGECOMSTR"]		 = "Merging $SOURCE into $TARGET"
	env["P4COMSTR"]			   = "Fetching $TARGET"
	env["PCHCOMSTR"]			  = "Generating $TARGET"
	env["PDFLATEXCOMSTR"]		 = "Compiling $TARGET"
	env["PDFTEXCOMSTR"]		   = "Compiling $TARGET"
	env["PSCOMSTR"]			   = "Converting $TARGET"
	env["QT_MOCFROMCXXCOMSTR"]	= "Generating $TARGET"
	env["QT_MOCFROMHCOMSTR"]	  = "Generating $TARGET"
	env["QT_UICCOMSTR"]		   = "Generating $TARGET"
	env["RANLIBCOMSTR"]		   = "Indexing $TARGET"
	env["RCCOMSTR"]			   = "Building $TARGET"
	env["RCS_COCOMSTR"]		   = "Fetching $TARGET"
	env["REGSVRCOMSTR"]		   = "Registering $TARGET"
	env["RMICCOMSTR"]			 = "Generating stub/skeleton class files $TARGETS from $SOURCES"
	env["SCCSCOMSTR"]			 = "Fetching $TARGET"
	env["SHCCCOMSTR"]			 = "Compiling $TARGET"
	env["SHCXXCOMSTR"]			= "Compiling $TARGET"
	env["SHF03COMSTR"]			= "Compiling $TARGET"
	env["SHF03PPCOMSTR"]		  = "Compiling $TARGET"
	env["SHF77COMSTR"]			= "Compiling $TARGET"
	env["SHF77PPCOMSTR"]		  = "Compiling $TARGET"
	env["SHF90COMSTR"]			= "Compiling $TARGET"
	env["SHF90PPCOMSTR"]		  = "Compiling $TARGET"
	env["SHF95COMSTR"]			= "Compiling $TARGET"
	env["SHF95PPCOMSTR"]		  = "Compiling $TARGET"
	env["SHFORTRANCOMSTR"]		= "Compiling $TARGET"
	env["SHFORTRANPPCOMSTR"]	  = "Compiling $TARGET"
	env["SHLINKCOMSTR"]		   = "Linking $TARGET"
	env["SWIGCOMSTR"]			 = "Calling $TARGET"
	env["TARCOMSTR"]			  = "Archiving $TARGET"
	env["TEXCOMSTR"]			  = "Building $TARGET"
	env["XGETTEXTCOMSTR"]		 = "Extracting translations"
	env["YACCCOMSTR"]			 = "Generating $TARGET"
	env["ZIPCOMSTR"]			  = "Zipping $TARGET"

# Copied from https://stackoverflow.com/questions/1006289/how-to-find-out-the-number-of-cpus-in-python
def availableCpus():
	""" Number of available virtual or physical CPUs on this system, i.e.
	user/real as output by time(1) when called with an optimally scaling
	userspace-only program"""

	# cpuset
	# cpuset may restrict the number of *available* processors
	try:
		m = re.search(r'(?m)^Cpus_allowed:\s*(.*)$',
					  open('/proc/self/status').read())
		if m:
			res = bin(int(m.group(1).replace(',', ''), 16)).count('1')
			if res > 0:
				return res
	except IOError:
		pass

	# Python 2.6+
	try:
		import multiprocessing
		return multiprocessing.cpu_count()
	except (ImportError, NotImplementedError):
		pass

	# http://code.google.com/p/psutil/
	try:
		import psutil
		return psutil.NUM_CPUS
	except (ImportError, AttributeError):
		pass

	# POSIX
	try:
		res = int(os.sysconf('SC_NPROCESSORS_ONLN'))

		if res > 0:
			return res
	except (AttributeError, ValueError):
		pass

	# Windows
	try:
		res = int(os.environ['NUMBER_OF_PROCESSORS'])

		if res > 0:
			return res
	except (KeyError, ValueError):
		pass

	# jython
	try:
		from java.lang import Runtime
		runtime = Runtime.getRuntime()
		res = runtime.availableProcessors()
		if res > 0:
			return res
	except ImportError:
		pass

	# BSD
	try:
		sysctl = subprocess.Popen(['sysctl', '-n', 'hw.ncpu'],
								  stdout=subprocess.PIPE)
		scStdout = sysctl.communicate()[0]
		res = int(scStdout)

		if res > 0:
			return res
	except (OSError, ValueError):
		pass

	# Linux
	try:
		res = open('/proc/cpuinfo').read().count('processor\t:')

		if res > 0:
			return res
	except IOError:
		pass

	# Solaris
	try:
		pseudoDevices = os.listdir('/devices/pseudo/')
		res = 0
		for pd in pseudoDevices:
			if re.match(r'^cpuid@[0-9]+$', pd):
				res += 1

		if res > 0:
			return res
	except OSError:
		pass

	# Other UNIXes (heuristic)
	try:
		try:
			dmesg = open('/var/run/dmesg.boot').read()
		except IOError:
			dmesgProcess = subprocess.Popen(['dmesg'], stdout=subprocess.PIPE)
			dmesg = dmesgProcess.communicate()[0]

		res = 0
		while '\ncpu' + str(res) + ':' in dmesg:
			res += 1

		if res > 0:
			return res
	except OSError:
		pass

	return 1
	raise Exception('Can not determine number of CPUs on this system')

def addSubfolder(name, createMSVSProj = True):
	global msvsFolders
	SConscript("{0}/SConscript".format(name), variant_dir = "{0}/{1}".format(buildPrefix, name), duplicate = 0)
	if createMSVSProj:
		msvsFolders += name

# Prepares to create a MSVS solution.
# Returns true if it should be created and all SConscripts should be rerun.
# Otherwise false is returned.
def doMSVS():
	global msvs
	if not msvsReal:
		return False
	msvs = msvsReal
	Export("msvs")
	for proj in msvsFolders:
		SConscript("{0}/SConscript".format(proj))
	return True

# Function that should get called after the SConscripts have been executed.
# It adds the MSVS solution and some aliases for convenience.
def finish():
	global everything
	Import("program")
	if iswin:
		sol = env.MSVSSolution(target = "{0}.sln".format(project),
			projects = msvsprojs,
			variant = buildType)
		env.Alias("msvs", sol + msvsprojs)
	if "everything" in globals():
		everything += program
	else:
		everything = program
	env.Alias("all", everything)
	env.Alias("install", install)
	Default(program)










# Initialization
# Use md5 only if timestamp has changed
Decider("MD5-timestamp")

# Setup environment
if "enableGettext" in globals() and enableGettext:
	env = Environment(tools = ["default", "gettext"], PROJECT = project)
elif "winUse32" in globals() and winUse32:
	env = Environment(PROJECT = project, TARGET_ARCH='x86')
else:
	env = Environment(PROJECT = project)

iswin = env["PLATFORM"] == "win32"
if iswin:
	installPrefix = "/Program Files"
else:
	installPrefix = "/usr"

# Available options
vars = Variables(".buildvars.py", ARGUMENTS)
vars.AddVariables(
	BoolVariable("release",       "Build in release mode", False),
	BoolVariable("optimize",      "Build in optimized mode", False),
	BoolVariable("verbose",       "Output build commands", False),
	BoolVariable("noinstall",     "Build for local use", True),
	PathVariable("installPrefix", "Define installation directory", installPrefix, PathVariable.PathIsDirCreate))
if iswin:
	vars.AddVariables(
		("root", "Define the root directory for include/ and lib/, can be a list separated by ;", False),
		("include", "Define the include directory, can be a list separated by ;", False),
		("lib", "Define the lib directory, can be a list separated by ;", False))
else:
	vars.AddVariables(
		("root", "Define the root directory for include/ and lib/, can be a list separated by ;", "/usr"))
	if os.system("command -v clang++ > /dev/null 2>&1") == 0:
		vars.AddVariables(
			BoolVariable("clang", "Compile with clang/llvm", True))

if env.get("release"):
	buildPrefix = "bin-release"
if env.get("optimize"):
	buildPrefix = "bin-optimize"
else:
	buildPrefix = "bin"
vars.AddVariables(PathVariable("buildPrefix", "Define build directory", buildPrefix, PathVariable.PathIsDirCreate))
vars.Update(env)
# Save current set variables (that differ from the default) to .buildvars.py
vars.Save(".buildvars.py", env)

# Create help text
if "addHelp" not in globals():
	addHelp = {}
addHelp.update({
	"msvs":     "Create a solution for Visual Studio",
	"all":      "Builds nearly everything",
	"install":  "Install application"
})
helpString = ""
for k, v in addHelp.iteritems():
	helpString += "\t{0:15} {1}\n".format(k, v)
Help(vars.GenerateHelpText(env) + "\nTargets:\n" + helpString)

# Set number of CPUs
if not iswin:
	SetOption("num_jobs", availableCpus())
	if env.get("verbose") and GetOption("num_jobs") > 1:
		print "Use", GetOption("num_jobs"), "threads"

if not env.get("verbose"):
	setCommandStrings()

if iswin:
	env.Append(CCFLAGS = ["/wd4068"], CPPDEFINES = ["_UNICODE", "WXUSINGDLL"])
else:
	env.Append(CXXFLAGS = ["-std=c++11"], CCFLAGS = ["-Wall", "-Wextra", "-Wfatal-errors", "-fdiagnostics-color=always"])

# Setup clang if it should be used
if env.get("clang"):
	env.Replace(CC = "clang", CXX = "clang++")
	if "enableClangLib" in globals() and enableClangLib:
		# Enable llvm std library only if no incompatible libraries are linked
		env.Append(CXXFLAGS = ["-stdlib=libc++"], LINKFLAGS = ["-lc++abi"])

if env.get("release"):
	# Release build
	buildType = "Release"
	env.Append(CPPDEFINES = ["NDEBUG"])
	if iswin:
		env.Append(CCFLAGS = ["/O2", "/MD"])
	else:
		env.Append(CCFLAGS = ["-O2", "-flto"])
elif env.get("optimize"):
	# Optimized release build
	buildType = "Optimize"
	env.Append(CPPDEFINES = ["NDEBUG"])
	if iswin:
		env.Append(CCFLAGS = ["/O3", "/MD"])
	else:
		env.Append(CCFLAGS = ["-O3", "-flto", "-mtune=native", "-march=native"])
else:
	# Debug build
	buildType = "Debug"
	if iswin:
		env.Append(CCFLAGS = ["/DEBUG", "/MDd", "/Zi", "/Od"], LINKFLAGS = ["/DEBUG"])
	else:
		env.Append(CCFLAGS = ["-g"])
if iswin:
	env.Append(CCFLAGS = ["/EHsc", "/MP"])

if env.get("noinstall"):
	env.Append(CPPDEFINES= ["NO_INSTALL"])

# Add user defined paths
if env.get("root"):
	libpath = []
	incpath = []
	for path in env.get("root").split(";"):
		libpath += ["{0}/lib".format(path)]
		incpath += ["{0}/include".format(path)]
	env.Append(LIBPATH = libpath,
		CPPPATH = incpath)
if env.get("lib"):
	env.Append(LIBPATH = env.get("lib").split(";"))
if env.get("include"):
	env.Append(CPPPATH = env.get("include").split(";"))

# Get the current version of the project
if "versionFile" in globals():
	src_file = open(versionFile, "r")
	lines = src_file.readlines()
	foundVersion = False
	for line in lines:
		index = line.find("const char *VERSION")
		if index != -1:
			index = line.find("\"", index)
			index = index + 1
			version = line[index : line.find("\"", index)]
			foundVersion = True
			break
	if foundVersion:
		Export("version")
	else:
		print "The version couldn't be found"

msvsReal = "msvs" in COMMAND_LINE_TARGETS
msvs = False
install = []
msvsprojs = []
msvsFolders = []
Export("env iswin installPrefix buildPrefix project msvs msvsprojs install buildType rootDirectory")