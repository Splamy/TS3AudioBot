// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace TSLib.Helper
{
	internal static class NativeLibraryLoader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string dllToLoad);

		public static bool DirectLoadLibrary(string lib, Action dummyLoad = null)
		{
			if (Tools.IsLinux)
			{
				if (dummyLoad != null)
				{
					try
					{
						dummyLoad.Invoke();
					}
					catch (DllNotFoundException ex)
					{
						Log.Error(ex, "Failed to load library \"{0}\".", lib);
						return false;
					}
				}
			}
			else
			{
				var libPath = Path.Combine("lib", ArchFolder, lib);
				Log.Debug("Loading \"{0}\" from \"{1}\"", lib, libPath);
				var handle = LoadLibrary(libPath);
				if (handle == IntPtr.Zero)
				{
					Log.Error("Failed to load library \"{0}\" from \"{1}\", error: {2}", lib, libPath, Marshal.GetLastWin32Error());
					return false;
				}
			}
			return true;
		}

		public static string ArchFolder
		{
			get
			{
				if (IntPtr.Size == 8)
					return "x64";
				if (IntPtr.Size == 4)
					return "x86";
				return "xOther";
			}
		}
	}
}
