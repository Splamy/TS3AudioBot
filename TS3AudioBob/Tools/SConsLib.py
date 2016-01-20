# Some functions that are usefull for scons
import os

def getFilePath(file):
	return os.path.dirname(os.path.realpath(file))

def getAbsolutePath(path):
	return os.path.join(rootDirectory, path)

def getDirs(path):
    result = [name for name in os.listdir(path) if os.path.isdir(os.path.join(path, name)) and len(name) > 0 and name[0] != '.' ]
    result.sort()
    return result

def getDirsRecursive(path):
	paths = getDirs(path)
	paths2 = paths
	done = False
	while not done:
		done = True
		paths2 = sum([[os.path.join(p, p2) for p2 in getDirs(os.path.join(path, p))] for p in paths2], [])
		for p in paths2:
			if p not in paths:
				done = False
				paths.append(p)
	return paths

def recursiveGlob(endings, subPath = ""):
	path = os.path.join(rootDirectory, folder, subPath)
	paths = sum([[os.path.join(subPath, p, ending) for ending in endings] for p in
		getDirsRecursive(path) + [""]], [])
	return sum([Glob(p) for p in paths], [])
