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

	public partial class ConfRoot
	{
		private string fileName;

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
		public string GetFilePath(string path)
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
			return CreateBotConfig(config);
		}

		public IEnumerable<string> ListAllBots()
		{
			try
			{
				return Directory.EnumerateFiles(Configs.BotsPath.Value, "bot_*.toml", SearchOption.TopDirectoryOnly)
					.Select(file =>
					{
						var fi = new FileInfo(file);
						return fi.Name;
					});
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Could not access bot config subdirectory.");
				return Array.Empty<string>();
			}
		}

		public R<ConfBot, Exception> GetBotTemplate(string name)
		{
			string botFile = NameToPath(name).UnwrapThrow();
			var botConfResult = Load<ConfBot>(botFile);
			if (!botConfResult.Ok)
				return botConfResult.Error;
			var botConf = CreateBotConfig(botConfResult.Value);
			botConf.Name = name;
			return botConf;
		}

		private ConfBot CreateBotConfig(ConfBot config)
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
			var file = GetParent().NameToPath(name).UnwrapThrow();
			if (File.Exists(file))
				return new LocalStr("The file already exists."); // LOC: TODO
			var result = SaveInternal(file);
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
