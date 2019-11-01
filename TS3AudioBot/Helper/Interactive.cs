// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TS3AudioBot.Helper
{
	public static class Interactive
	{
		public static bool UserAgree(bool defaultTo = true)
		{
			while (true)
			{
				var key = Console.ReadKey(true).Key;
				if (key == ConsoleKey.Y || (defaultTo && key == ConsoleKey.Enter))
					return true;
				if (key == ConsoleKey.N || (!defaultTo && key == ConsoleKey.Enter))
					return false;
			}
		}

		public static string LoopAction(string question, Func<string, bool> action)
		{
			string text;
			do
			{
				Console.WriteLine(question);
				text = Console.ReadLine();
				if (text is null)
					return null;
			}
			while (!action(text));
			return text;
		}
	}
}
