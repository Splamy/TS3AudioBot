// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Config
{
	using Helper;
	using Localization;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;

	public partial class ConfRoot
	{
		private static readonly Regex BotFileMatcher = new Regex(@"^bot_(.+)\.toml$", Util.DefaultRegexConfig);

		private string fileName;
		private readonly Dictionary<string, ConfBot> botConfCaches = new Dictionary<string, ConfBot>();

		public static R<ConfRoot> Open(string file)
		{
			var loadResult = Load<ConfRoot>(file);
			if (!loadResult.Ok)
			{
				Log.Error(loadResult.Error, "Could not load core config.");
				return R.Err;
			}

			if (!loadResult.Value.CheckAndSet(file))
				return R.Err;
			return loadResult.Value;
		}

		public static R<ConfRoot> Create(string file)
		{
			var newFile = CreateRoot<ConfRoot>();
			if (!newFile.CheckAndSet(file))
				return newFile;
			var saveResult = newFile.Save(file, true);
			if (!saveResult.Ok)
			{
				Log.Error(saveResult.Error, "Failed to save config file '{0}'.", file);
				return R.Err;
			}
			return newFile;
		}

		public static R<ConfRoot> OpenOrCreate(string file) => File.Exists(file) ? Open(file) : Create(file);

		private bool CheckAndSet(string file)
		{
			fileName = file;
			if (!CheckPaths())
				return false;
			// further checks...
			return true;
		}

		private bool CheckPaths()
		{
			try
			{
				if (!Directory.Exists(Configs.BotsPath.Value))
					Directory.CreateDirectory(Configs.BotsPath.Value);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Could not create bot config subdirectory.");
				return false;
			}
			return true;
		}

		public bool Save() => Save(fileName, false);

		// apply root_path to input path
		public string GetFilePath(string file)
		{
			throw new NotImplementedException();
		}

		internal R<string, LocalStr> NameToPath(string name)
		{
			var nameResult = Util.IsSafeFileName(name);
			if (!nameResult.Ok)
				return nameResult.Error;
			return Path.Combine(Configs.BotsPath.Value, $"bot_{name}.toml");
		}

		public ConfBot CreateBot()
		{
			var config = CreateRoot<ConfBot>();
			return InitializeBotConfig(config);
		}

		public ConfBot[] GetAllBots()
		{
			try
			{
				return Directory.EnumerateFiles(Configs.BotsPath.Value, "bot_*.toml", SearchOption.TopDirectoryOnly)
					.Select(filePath =>
					{
						var fileInfo = new FileInfo(filePath);
						var botName = ExtractNameFromFile(fileInfo.Name);
						return GetBotConfig(botName);
					})
					.Where(x => x.Ok)
					.Select(x => x.Value)
					.ToArray();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Could not access bot config subdirectory.");
				return null;
			}
		}

		private static string ExtractNameFromFile(string file)
		{
			var match = BotFileMatcher.Match(file);
			if (match.Success && Util.IsSafeFileName(match.Groups[1].Value))
				return match.Groups[1].Value;
			if (Util.IsSafeFileName(file))
				return file;
			throw new ArgumentException("Invalid name");
		}

		public R<ConfBot, Exception> GetBotConfig(string name)
		{
			string botFile = NameToPath(name).UnwrapThrow();
			var botConfResult = Load<ConfBot>(botFile);
			if (!botConfResult.Ok)
				return botConfResult.Error;
			var botConf = InitializeBotConfig(botConfResult.Value);
			botConf.Name = name;
			return botConf;
		}

		private ConfBot InitializeBotConfig(ConfBot config)
		{
			Bot.Derive(config);
			config.Parent = this;
			return config;
		}
	}

	public partial class ConfBot
	{
		public string Name { get; set; }

		public E<LocalStr> SaveNew(string name)
		{
			var file = GetParent().NameToPath(name);
			if (!file.Ok)
				return file.Error;
			if (File.Exists(file.Value))
				return new LocalStr("The file already exists."); // LOC: TODO
			var result = SaveInternal(file.Value);
			if (result.Ok)
				Name = name;
			return result;
		}

		public E<LocalStr> SaveWhenExists()
		{
			if (string.IsNullOrEmpty(Name))
				return R.Ok;

			var file = GetParent().NameToPath(Name);
			if (!file.Ok)
				return file.Error;
			return SaveInternal(file.Value);
		}

		private E<LocalStr> SaveInternal(string file)
		{
			var result = Save(file, false);
			if (!result.Ok)
			{
				Log.Error(result.Error, "An error occoured saving the bot config.");
				return new LocalStr(string.Format("An error occoured saving the bot config.")); // LOC: TODO
			}
			return R.Ok;
		}

		public ConfRoot GetParent() => Parent as ConfRoot;
	}
}
