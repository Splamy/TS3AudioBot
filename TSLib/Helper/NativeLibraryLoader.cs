// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace TSLib.Helper;

internal static class NativeLibraryLoader
{
	private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

	public static bool DirectLoadLibrary(string lib, Action? dummyLoad = null)
	{
		if (Tools.IsLinux)
		{
			try
			{
				dummyLoad?.Invoke();
			}
			catch (DllNotFoundException ex)
			{
				Log.Error(ex, "Failed to load library \"{0}\".", lib);
				return false;
			}
		}
		else
		{
			foreach (var libPath in LibPathOptions(lib))
			{
				Log.Debug("Loading \"{0}\" from \"{1}\"", lib, libPath);
				if (NativeLibrary.TryLoad(libPath, out _))
					return true;
			}
			Log.Error("Failed to load library \"{0}\", error: {1}", lib, Marshal.GetLastWin32Error());
			return false;
		}
		return true;
	}

	private static IEnumerable<string> LibPathOptions(string lib)
	{
		var fullPath = Directory.GetCurrentDirectory();
		yield return Path.Combine(fullPath, "lib", ArchFolder, lib);
		yield return Path.Combine(fullPath, "lib", lib);
		var asmPath = Path.GetDirectoryName(AppContext.BaseDirectory)!;
		yield return Path.Combine(asmPath, "lib", ArchFolder, lib);
		yield return Path.Combine(asmPath, "lib", lib);
	}

	public static string ArchFolder => IntPtr.Size switch
	{
		8 => "x64",
		4 => "x86",
		_ => "xOther"
	};
}
