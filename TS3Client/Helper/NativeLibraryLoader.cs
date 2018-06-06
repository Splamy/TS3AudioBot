// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Helper
{
	using System;
	using System.IO;
	using System.Runtime.InteropServices;

	internal static class NativeLibraryLoader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr LoadLibrary(string dllToLoad);

		[DllImport("libdl.so")]
		private static extern IntPtr dlopen(string fileName, int flags);
		[DllImport("libdl.so")]
		private static extern IntPtr dlerror();

		public static bool DirectLoadLibrary(string lib)
		{
			if (Util.IsLinux)
			{
				Log.Debug("Loading \"{0}\"", lib);
				dlerror();
				var handle = dlopen(lib + ".so", 2);
				var errorPtr = dlerror();
				if (errorPtr != IntPtr.Zero)
				{
					var errorStr = Marshal.PtrToStringAnsi(errorPtr);
					Log.Error("Failed to load library \"{0}\", error: {1}", lib, errorStr);
					return false;
				}
				else if (handle == IntPtr.Zero)
				{
					Log.Error("Failed to load library \"{0}\", unknown error.", lib);
					return false;
				}
			}
			else
			{
				var libPath = Path.Combine(ArchFolder, lib);
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
