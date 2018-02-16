// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Plugins
{
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;

	// Start Plugin:
	// ! Start plugins before rights system to ensure all rights are loaded
	// - Get all commands
	// - Validate
	//   - 0/1 Plugin
	//     - Command name conflict
	//   - 0+ Factory
	//     - Facory name conflict
	// - Add commands to rights system
	// - Instantiate plugin
	// - Add commands to command manager
	// - Start config to system?

	internal class PluginManager : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public Core Core { get; set; }
		public ConfigFile Config { get; set; }

		private PluginManagerData pluginManagerData;
		private readonly Dictionary<string, Plugin> plugins;
		private readonly HashSet<int> usedIds;

		public PluginManager()
		{
			Util.Init(out plugins);
			Util.Init(out usedIds);
		}

		public void Initialize()
		{
			pluginManagerData = Config.GetDataStruct<PluginManagerData>("PluginManager", true);
		}

		private void CheckAndClearPlugins()
		{
			ClearMissingFiles();
			CheckLocalPlugins();
		}

		/// <summary>Updates the plugin dictionary with new and changed plugins.</summary>
		private void CheckLocalPlugins()
		{
			var dir = new DirectoryInfo(pluginManagerData.PluginPath);
			if (!dir.Exists)
				return;

			foreach (var file in dir.EnumerateFiles())
			{
				if (plugins.TryGetValue(file.Name, out var plugin))
				{
					switch (plugin.Status)
					{
					case PluginStatus.Disabled:
					case PluginStatus.Active:
						continue;
					case PluginStatus.Ready:
					case PluginStatus.Off:
					case PluginStatus.Error:
						plugin.Load();
						break;
					default:
						throw Util.UnhandledDefault(plugin.Status);
					}
				}
				else
				{
					if (IgnoreFile(file))
						continue;

					plugin = new Plugin(file, Core, GetFreeId());

					if (plugin.Load() == PluginResponse.Disabled)
					{
						RemovePlugin(plugin);
						continue;
					}

					plugins.Add(file.Name, plugin);
				}
			}
		}

		/// <summary>Unloads all Plugins which have no corresponding file anymore and removes the from the index list.</summary>
		private void ClearMissingFiles()
		{
			// at first find all missing and ignored files
			var missingFiles = plugins.Where(kvp =>
			{
				kvp.Value.PluginFile.Refresh();
				return !kvp.Value.PluginFile.Exists || IgnoreFile(kvp.Value.PluginFile);
			}).ToArray();

			// unload if it is loaded and remove
			foreach (var misFile in missingFiles)
				RemovePlugin(misFile.Value);
		}

		public void RestorePlugins()
		{
			CheckAndClearPlugins();

			foreach (var plugin in plugins.Values)
			{
				if (plugin.PersistentEnabled)
					StartPlugin(plugin);
			}
		}

		public static bool IgnoreFile(FileInfo file) =>
			(file.Extension != ".cs" && file.Extension != ".dll" && file.Extension != ".exe")
			|| File.Exists(file.FullName + ".ignore");

		private Plugin TryGetPlugin(string identifier)
		{
			if (plugins.TryGetValue(identifier, out var plugin))
				return plugin;

			if (int.TryParse(identifier, out int num))
				return plugins.Values.FirstOrDefault(p => p.Id == num);

			return plugins.Values.FirstOrDefault(p => p.Name == identifier);
		}

		private int GetFreeId()
		{
			int id = 0;
			while (usedIds.Contains(id))
				id++;
			usedIds.Add(id);
			return id;
		}

		public PluginResponse StartPlugin(string identifier)
		{
			CheckLocalPlugins();

			return StartPlugin(TryGetPlugin(identifier));
		}

		private PluginResponse StartPlugin(Plugin plugin)
		{
			if (plugin == null)
				return PluginResponse.PluginNotFound;

			if (pluginManagerData.WriteStatusFiles)
				plugin.PersistentEnabled = true;

			switch (plugin.Status)
			{
			case PluginStatus.Disabled:
				return PluginResponse.Disabled;

			case PluginStatus.Off:
				var response = plugin.Load();
				if (response != PluginResponse.Ok)
					return response;
				goto case PluginStatus.Ready;

			case PluginStatus.Ready:
				try
				{
					plugin.Start();
					return PluginResponse.Ok;
				}
				catch (Exception ex)
				{
					StopPlugin(plugin);
					Log.Warn("Plugin \"{0}\" failed to load: {1}",
						plugin.PluginFile.Name,
						ex);
					return PluginResponse.UnknownError;
				}
			case PluginStatus.Active:
				return PluginResponse.Ok;

			case PluginStatus.Error:
				return PluginResponse.UnknownError;

			default:
				throw Util.UnhandledDefault(plugin.Status);
			}
		}

		public PluginResponse StopPlugin(string identifier) => StopPlugin(TryGetPlugin(identifier));

		private PluginResponse StopPlugin(Plugin plugin)
		{
			if (plugin == null)
				return PluginResponse.PluginNotFound;

			if (pluginManagerData.WriteStatusFiles)
				plugin.PersistentEnabled = false;

			plugin.Stop();

			return PluginResponse.Ok;
		}

		private void RemovePlugin(Plugin plugin)
		{
			usedIds.Remove(plugin.Id);
			plugins.Remove(plugin.PluginFile.Name);
			plugin.Unload();
		}

		public PluginStatusInfo[] GetPluginOverview()
		{
			CheckAndClearPlugins();

			return plugins.Values.Select(x =>
				new PluginStatusInfo(
					x.Id,
					x.Status != PluginStatus.Error ? x.Name : x.PluginFile.Name,
					x.Status)
			).ToArray();
		}

		public static string FormatOverview(ICollection<PluginStatusInfo> pluginList)
		{
			if (pluginList.Count == 0)
				return "No plugins found!";

			var strb = new StringBuilder();
			strb.AppendLine("All available plugins:");
			int digits = (int)Math.Floor(Math.Log10(pluginList.Count) + 1);
			foreach (var plugin in pluginList)
			{
				strb.Append("#").Append(plugin.Id.ToString("D" + digits, CultureInfo.InvariantCulture)).Append('|');
				switch (plugin.Status)
				{
				case PluginStatus.Off: strb.Append("OFF"); break;
				case PluginStatus.Ready: strb.Append("RDY"); break;
				case PluginStatus.Active: strb.Append("+ON"); break;
				case PluginStatus.Disabled: strb.Append("UNL"); break;
				case PluginStatus.Error: strb.Append("ERR"); break;
				default: throw Util.UnhandledDefault(plugin.Status);
				}
				strb.Append('|').AppendLine(plugin.Name ?? "<not loaded>");
			}
			return strb.ToString();
		}

		public void Dispose()
		{
			foreach (var plugin in plugins.Values)
				plugin.Unload();
		}
	}

	public class PluginStatusInfo
	{
		public int Id { get; }
		public string Name { get; }
		public PluginStatus Status { get; }

		public PluginStatusInfo(int id, string name, PluginStatus status)
		{
			Id = id;
			Name = name;
			Status = status;
		}
	}

	public class PluginManagerData : ConfigData
	{
		[Info("The absolute or relative path to the plugins folder", "Plugins")]
		public string PluginPath { get; set; }

		[Info("Write to .status files to store a plugin enable status persistently and restart them on launch.", "false")]
		public bool WriteStatusFiles { get; set; }
	}
}
