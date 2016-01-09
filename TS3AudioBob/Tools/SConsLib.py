# Some functions that are usefull for scons
import os

def getFilePath(file):
	return os.path.dirname(os.path.realpath(file))

def getAbsolutePath(path):
	return os.path.join(rootDirectory, path)

def getDirs(path):
    result = [name for name in os.listdir(path) if os.path.isdir(os.path.join(path, name)) and name[0] != '.' ]
    result.sort()
    return result

def recursiveGlob(endings):
	path = rootDirectory + folder
	paths = sum([[os.path.join(p, ending) for ending in endings] for p in getDirs(path)], [])
	return sum([Glob(p) for p in paths], []) + sum([Glob(ending) for ending in endings], [])
