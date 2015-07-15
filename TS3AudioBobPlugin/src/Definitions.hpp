#ifndef DEFINITIONS_HPP
#define DEFINITIONS_HPP

// Define macros for exporting and importing functions to/from a library
#if defined _WIN32 || defined __CYGWIN__
	#ifdef BUILDING_DLL
		#ifdef __GNUC__
			#define DLL_PUBLIC __attribute__ ((dllexport))
		#else
			#define DLL_PUBLIC __declspec(dllexport)
		#endif
	#else
		#ifdef __GNUC__
			#define DLL_PUBLIC __attribute__ ((dllimport))
		#else
			#define DLL_PUBLIC __declspec(dllimport)
		#endif
	#endif
	#define DLL_LOCAL
#else
	#if __GNUC__ >= 4
		#define DLL_PUBLIC __attribute__ ((visibility ("default")))
		#define DLL_LOCAL  __attribute__ ((visibility ("hidden")))
	#else
		#define DLL_PUBLIC
		#define DLL_LOCAL
	#endif
#endif

// Defines override if it's not defined
#ifdef _WIN32
	#if __cplusplus < 201103L
		//#define override
	#endif
#else
	#if defined __GNUC__ && (__GNUC__ <= 4 || (__GNUC__ == 4 && __GNUC_MINOR__ < 7))
		#define override
	#endif
#endif

#endif
