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

namespace TS3AudioBot
{
	using System;
	using System.Text;
	using System.IO;
	using System.Collections.Generic;
	using CommandSystem;

	/// <summary>
	/// Used to generate the command overview for GitHub.
	/// </summary>
	class DocGen
	{
		static void Main(string[] args)
		{
			const bool writeSubCommands = false;

			var bot = new MainBot();
			typeof(MainBot).GetProperty(nameof(MainBot.CommandManager)).SetValue(bot, new CommandManager());
			bot.CommandManager.RegisterMain(bot);

			var strb = new StringBuilder();
			var lines = new List<string>();

			foreach (var com in bot.CommandManager.AllCommands)
			{
				if (!writeSubCommands && com.InvokeName.Contains(" "))
					continue;

				string description;
				if (string.IsNullOrEmpty(com.Description))
					description = " - no description yet -";
				else
					description = com.Description;
				lines.Add($"* *{com.InvokeName}*: {description}");
			}

			// adding commands which only have subcommands
			lines.Add("* *history*: Shows recently played songs.");

			lines.Sort();

			string final = string.Join("\n", lines);
			Console.WriteLine(final);
			File.WriteAllText("docs.txt", final);

			Console.ReadLine();
		}
	}
}
