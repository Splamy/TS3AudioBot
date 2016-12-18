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
