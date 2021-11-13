// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.Config;

public partial class ConfRoot
{
	private string? fileName;
	private readonly Dictionary<string, ConfBot> botConfCache = new();

	public static ConfRoot? Open(string file)
	{
		if (!Load<ConfRoot>(file).Get(out var confRoot, out var error))
		{
			Log.Error(error, "Could not load core config.");
			return null;
		}

		if (!confRoot.CheckAndSet(file))
			return null;
		return confRoot;
	}

	public static ConfRoot? Create(string file)
	{
		var newFile = CreateRoot<ConfRoot>();
		if (!newFile.CheckAndSet(file))
			return newFile;
		if (!newFile.Save(file, true).GetOk(out var saveError))
		{
			Log.Error(saveError, "Failed to save config file '{0}'.", file);
			return null;
		}
		return newFile;
	}

	public static ConfRoot? OpenOrCreate(string file) => File.Exists(file) ? Open(file) : Create(file);

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

	public bool Save() => Save(fileName!, true);

	// apply root_path to input path
	public string GetFilePath(string _)
	{
		throw new NotImplementedException();
	}

	internal R<FileInfo, LocalStr> NameToPath(string name)
	{
		if (!Util.IsSafeFileName(name).GetOk(out var nameError))
			return nameError;
		return new FileInfo(Path.Combine(Configs.BotsPath.Value, name, FilesConst.BotConfig));
	}

	public ConfBot CreateBot()
	{
		var config = CreateRoot<ConfBot>();
		InitializeBotConfig(config);
		return config;
	}

	public ConfBot[]? GetAllBots()
	{
		try
		{
			return Directory.EnumerateDirectories(Configs.BotsPath.Value)
				.Select(filePath => new DirectoryInfo(filePath).Name)
				.SelectOk(GetBotConfig)
				.ToArray();
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Could not access bot config subdirectory.");
			return null;
		}
	}

	public R<ConfBot, Exception> GetBotConfig(string name)
	{
		if (!NameToPath(name).Get(out var file, out var error))
			return new Exception(error.Str);
		if (!botConfCache.TryGetValue(name, out var botConf))
		{
			if (!Load<ConfBot>(file.FullName).Get(out botConf, out var loadError))
			{
				Log.Warn(loadError, "Failed to load bot config \"{0}\"", name);
				return loadError;
			}
			InitializeBotConfig(botConf);
			botConf.Name = name;
			botConfCache[name] = botConf;
		}
		return botConf;
	}

	private void InitializeBotConfig(ConfBot config)
	{
		Bot.Derive(config);
		config.Parent = this;
	}

	public void ClearBotConfigCache()
	{
		botConfCache.Clear();
	}

	public void ClearBotConfigCache(string name)
	{
		botConfCache.Remove(name);
	}

	internal void AddToConfigCache(ConfBot config)
	{
		var name = config.Name;
		if (!string.IsNullOrEmpty(name) && !botConfCache.ContainsKey(name))
			botConfCache[name] = config;
	}

	public E<LocalStr> CreateBotConfig(string name)
	{
		if (!NameToPath(name).Get(out var file, out var error))
			return error;
		if (file.Exists)
			return new LocalStr("The file already exists."); // LOC: TODO
		try
		{
			Directory.CreateDirectory(file.DirectoryName!);
			using (File.Open(file.FullName, FileMode.CreateNew))
				return R.Ok;
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Config file could not be created");
			return new LocalStr("Could not create config."); // LOC: TODO
		}
	}

	public E<LocalStr> DeleteBotConfig(string name)
	{
		if (!NameToPath(name).Get(out var file, out var error))
			return error;
		if (botConfCache.Remove(name, out var conf))
			conf.Name = null;
		if (!file.Exists)
			return R.Ok;
		try
		{
			file.Delete();
			Directory.Delete(file.DirectoryName!);
			return R.Ok;
		}
		catch (Exception ex)
		{
			Log.Debug(ex, "Config file could not be deleted");
			return new LocalStr("Could not delete config."); // LOC: TODO
		}
	}

	public E<LocalStr> CopyBotConfig(string from, string to)
	{
		if (!NameToPath(from).Get(out var fileFrom, out var error))
			return error;
		if (!NameToPath(to).Get(out var fileTo, out error))
			return error;

		if (!fileFrom.Exists)
			return new LocalStr("The source bot does not exist.");
		if (fileTo.Exists)
			return new LocalStr("The target bot already exists, delete it before to overwrite.");

		try
		{
			Directory.CreateDirectory(fileTo.DirectoryName!);
			File.Copy(fileFrom.FullName, fileTo.FullName, false);
			return R.Ok;
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Config file could not be copied");
			return new LocalStr("Could not copy config."); // LOC: TODO
		}
	}
}

public partial class ConfBot
{
	public string? Name { get; set; }

	public string? LocalConfigDir => Name is null ? null : Path.Combine(GetParent().Configs.BotsPath.Value, Name);

	public E<LocalStr> SaveNew(string name)
	{
		var parent = GetParent();
		if (!parent.NameToPath(name).Get(out var file, out var error))
			return error;
		if (file.Exists)
			return new LocalStr("The file already exists."); // LOC: TODO
		if (!SaveInternal(file.FullName).GetOk(out var saveError))
			return saveError;
		Name = name;
		parent.AddToConfigCache(this);
		return R.Ok;
	}

	public E<LocalStr> SaveWhenExists()
	{
		if (string.IsNullOrEmpty(Name))
			return R.Ok;

		if (!GetParent().NameToPath(Name).Get(out var file, out var error))
			return error;
		if (!file.Exists)
			return R.Ok;
		return SaveInternal(file.FullName);
	}

	private E<LocalStr> SaveInternal(string file)
	{
		if (!Save(file, false).GetOk(out var error))
		{
			Log.Error(error, "An error occurred saving the bot config.");
			return new LocalStr(string.Format("An error occurred saving the bot config.")); // LOC: TODO
		}
		return R.Ok;
	}

	internal ConfRoot GetParent() => (Parent as ConfRoot) ?? throw new InvalidOperationException("Bot is not under root");
}
