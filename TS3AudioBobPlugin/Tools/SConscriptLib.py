execfile(rootDirectory + "Tools/SConsLib.py")
Import("env project iswin install msvs msvsprojs buildType")

curenv = env.Clone()
curenv.Append(CPPPATH = getAbsolutePath(folder))

sourceFiles = ["*.cpp", "*.c"]
headerFiles = ["*.hpp", "*.h"]

def createProgram(programName = "program", projectName = project):
	global install
	if msvs:
		Import(programName)
		# Dirty hack with globals to set a variable whos name is only knows at runtime
		prog = globals()[programName]
		msvsprojs.append(curenv.MSVSProject(target = "{0}.vcxproj".format(projectName),
			srcs = map(str, recursiveGlob(sourceFiles)),
			incs = map(str, recursiveGlob(headerFiles)),
			buildtarget = prog,
			variant = buildType,
			auto_build_solution = False))
	else:
		prog = curenv.Program(projectName, recursiveGlob(sourceFiles))
		install += curenv.Install("$installPrefix/bin", prog)
		globals()[programName] = prog
		Export(programName)

def createLibrary(programName, projectName = ""):
	global install
	if projectName == "":
		projectName = programName
	if msvs:
		Import(programName)
		# Dirty hack with globals to set a variable whos name is only knows at runtime
		prog = globals()[programName]
		msvsprojs.append(curenv.MSVSProject(target = "{0}.vcxproj".format(projectName),
			srcs = map(str, recursiveGlob(sourceFiles)),
			incs = map(str, recursiveGlob(headerFiles)),
			buildtarget = prog,
			variant = buildType,
			auto_build_solution = False))
	else:
		prog = curenv.SharedLibrary(projectName, recursiveGlob(sourceFiles))
		install += curenv.Install("$installPrefix/bin", prog)
		globals()[programName] = prog
		Export(programName)

def createStaticLibrary(programName, projectName = ""):
	global install
	if projectName == "":
		projectName = programName
	if msvs:
		Import(programName)
		# Dirty hack with globals to set a variable whos name is only knows at runtime
		prog = globals()[programName]
		msvsprojs.append(curenv.MSVSProject(target = "{0}.vcxproj".format(projectName),
			srcs = map(str, recursiveGlob(sourceFiles)),
			incs = map(str, recursiveGlob(headerFiles)),
			buildtarget = prog,
			variant = buildType,
			auto_build_solution = False))
	else:
		prog = curenv.StaticLibrary(projectName, recursiveGlob(sourceFiles))
		install += curenv.Install("$installPrefix/bin", prog)
		globals()[programName] = prog
		Export(programName)