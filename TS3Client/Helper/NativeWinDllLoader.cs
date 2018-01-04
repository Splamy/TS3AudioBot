// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
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

	internal static class NativeWinDllLoader
	{
		[DllImport("kernel32.dll")]
		private static extern IntPtr LoadLibrary(string dllToLoad);

		public static void DirectLoadLibrary(string lib)
		{
			if (Util.IsLinux)
				return;

			string libPath = Path.Combine(ArchFolder, lib);
			LoadLibrary(libPath);
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
