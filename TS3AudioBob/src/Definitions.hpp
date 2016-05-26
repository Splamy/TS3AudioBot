// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#ifndef DEFINITIONS_HPP
#define DEFINITIONS_HPP

// Define macros for exporting and importing functions to/from a library
#if defined _WIN32 || defined __CYGWIN__
	#ifdef __GNUC__
		#define DLL_PUBLIC __attribute__ ((dllexport))
	#else
		#define DLL_PUBLIC __declspec(dllexport)
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
