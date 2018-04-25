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
	using CommandSystem;
	using Dependency;
	using Helper;
	using ResourceFactories;
	using System;
	using System.CodeDom.Compiler;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Security.Cryptography;
	using System.Text;

	internal class Plugin : ICommandBag
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public CoreInjector CoreInjector { get; set; }
		public ResourceFactoryManager FactoryManager { get; set; }
		public Rights.RightsManager RightsManager { get; set; }
		public CommandManager CommandManager { get; set; }

		private byte[] md5CacheSum;
		private ICorePlugin pluginObject;
		private Dictionary<Bot, IBotPlugin> pluginObjectList;
		private IFactory factoryObject;
		private Type coreType;
		private readonly bool writeStatus;
		private PluginStatus status;

		internal PluginType Type { get; private set; }
		public int Id { get; }
		public FileInfo File { get; }

		public Plugin(FileInfo file, int id, bool writeStatus)
		{
			pluginObject = null;
			this.writeStatus = writeStatus;
			File = file;
			Id = id;
			status = PluginStatus.Off;
			Type = PluginType.None;
		}

		public string Name
		{
			get
			{
				if (CheckStatus(null) == PluginStatus.Error)
					return $"Error ({File.Name})";

				switch (Type)
				{
				case PluginType.Factory:
					return "Factory: " + (factoryObject?.FactoryFor ?? coreType?.Name ?? "<unknown>");
				case PluginType.BotPlugin:
					return "BotPlugin: " + (coreType?.Name ?? "<unknown>");
				case PluginType.CorePlugin:
					return "CorePlugin: " + (coreType?.Name ?? "<unknown>");
				case PluginType.Commands:
					return "Commands: " + (coreType?.Name ?? "<unknown>");
				case PluginType.None:
					return $"Unknown ({File.Name})";
				default:
					throw Util.UnhandledDefault(Type);
				}
			}
		}

		public bool PersistentEnabled
		{
			get
			{
				if (!System.IO.File.Exists(File.FullName + ".status"))
					return false;
				return System.IO.File.ReadAllText(File.FullName + ".status") != "0";
			}
			set
			{
				if (!System.IO.File.Exists(File.FullName + ".status"))
					return;
				System.IO.File.WriteAllText(File.FullName + ".status", value ? "1" : "0");
			}
		}

		public IEnumerable<BotCommand> ExposedCommands { get; private set; }
		public IEnumerable<string> ExposedRights => ExposedCommands.Select(x => x.RequiredRight);

		public PluginStatus CheckStatus(Bot bot)
		{
			if (Type != PluginType.BotPlugin)
				return status;
			if (status == PluginStatus.Disabled
				|| status == PluginStatus.Error
				|| status == PluginStatus.Off)
				return status;
			if (bot == null)
				return PluginStatus.NotAvailable;
			if (status == PluginStatus.Ready)
				return pluginObjectList.ContainsKey(bot) ? PluginStatus.Active : PluginStatus.Ready;
			if (status == PluginStatus.Active)
				throw new InvalidOperationException("BotPlugin must not be active");
			throw Util.UnhandledDefault(status);
		}

		public PluginResponse Load()
		{
			try
			{
				if (PluginManager.IsIgnored(File))
					return PluginResponse.Disabled;

				var locStatus = CheckStatus(null);
				if (locStatus != PluginStatus.Off && Md5EqualsCache())
				{
					return locStatus == PluginStatus.Ready || locStatus == PluginStatus.Active
						? PluginResponse.Ok
						: PluginResponse.UnknownError;
				}

				Unload();

				PluginResponse result;
				switch (File.Extension)
				{
				case ".cs":
#if NET46
					result = PrepareSource();
#else
					result = PluginResponse.NotSupported;
#endif
					break;

				case ".dll":
				case ".exe":
					result = PrepareBinary();
					break;
				default:
					throw new InvalidProgramException();
				}

				status = result == PluginResponse.Ok ? PluginStatus.Ready : PluginStatus.Error;
				return result;
			}
			catch (BadImageFormatException bifex)
			{
				Log.Warn("Plugin \"{0}\" has an invalid format: {1} (Add a \"{0}.ignore\" file to ignore this file)",
					File.Name,
					bifex.InnerException?.Message ?? bifex.Message);
				status = PluginStatus.Error;
				return PluginResponse.InvalidBinary;
			}
			catch (Exception ex)
			{
				Log.Warn("Plugin \"{0}\" failed to prepare: {1}",
					File.Name,
					ex.Message);
				status = PluginStatus.Error;
				return PluginResponse.Crash;
			}
		}

		private bool Md5EqualsCache()
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = System.IO.File.OpenRead(File.FullName))
				{
					var newHashSum = md5.ComputeHash(stream);
					if (md5CacheSum == null)
					{
						md5CacheSum = newHashSum;
						return false;
					}
					var equals = md5CacheSum.SequenceEqual(newHashSum);
					md5CacheSum = newHashSum;
					return equals;
				}
			}
		}

		private PluginResponse PrepareBinary()
		{
			var asmBin = System.IO.File.ReadAllBytes(File.FullName);
			var assembly = Assembly.Load(asmBin);
			return InitlializeAssembly(assembly);
		}

#if NET46
		private static CompilerParameters GenerateCompilerParameter()
		{
			var cp = new CompilerParameters();
			foreach (var asmb in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (asmb.IsDynamic) continue;
				cp.ReferencedAssemblies.Add(asmb.Location);
			}
			cp.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);

			// set preferences
			cp.WarningLevel = 3;
			cp.CompilerOptions = "/target:library /optimize";
			cp.GenerateExecutable = false;
			cp.GenerateInMemory = true;
			return cp;
		}

		private PluginResponse PrepareSource()
		{
			var provider = CodeDomProvider.CreateProvider("CSharp");
			var cp = GenerateCompilerParameter();
			var result = provider.CompileAssemblyFromFile(cp, File.FullName);

			if (result.Errors.Count > 0)
			{
				bool containsErrors = false;
				var strb = new StringBuilder();
				strb.AppendFormat("Plugin_{0} compiler notifications:\n", Id);
				foreach (CompilerError error in result.Errors)
				{
					containsErrors |= !error.IsWarning;
					strb.AppendFormat("{0} L{1}/C{2}: {3}\n",
						error.IsWarning ? "Warning" : "Error",
						error.Line,
						error.Column,
						error.ErrorText);
				}
				strb.Length -= 1; // remove last linebreak
				Log.Warn(strb.ToString());

				if (containsErrors)
				{
					status = PluginStatus.Error;
					return PluginResponse.CompileError;
				}
			}
			return InitlializeAssembly(result.CompiledAssembly);
		}
#endif

		private PluginResponse InitlializeAssembly(Assembly assembly)
		{
			try
			{
				var allTypes = assembly.GetExportedTypes();
				var pluginTypes = allTypes.Where(t => typeof(ICorePlugin).IsAssignableFrom(t)).ToArray();
				var factoryTypes = allTypes.Where(t => typeof(IFactory).IsAssignableFrom(t)).ToArray();
				var commandsTypes = allTypes.Where(t => t.GetCustomAttribute<StaticPluginAttribute>() != null).ToArray();

				if (pluginTypes.Length + factoryTypes.Length + commandsTypes.Length > 1)
				{
					Log.Warn("Any source or binary plugin file may contain one plugin or factory at most.");
					return PluginResponse.TooManyPlugins;
				}
				if (pluginTypes.Length + factoryTypes.Length + commandsTypes.Length == 0)
				{
					Log.Warn("Any source or binary plugin file must contain at least one plugin or factory.");
					return PluginResponse.NoTypeMatch;
				}

				if (pluginTypes.Length == 1)
				{
					coreType = pluginTypes[0];
					if (typeof(IBotPlugin).IsAssignableFrom(coreType))
					{
						Type = PluginType.BotPlugin;
						Util.Init(out pluginObjectList);
					}
					else
						Type = PluginType.CorePlugin;
				}
				else if (factoryTypes.Length == 1)
				{
					coreType = factoryTypes[0];
					Type = PluginType.Factory;
				}
				else if (commandsTypes.Length == 1)
				{
					coreType = commandsTypes[0];
					Type = PluginType.Commands;
				}
				else
				{
					Type = PluginType.None;
					throw new InvalidOperationException();
				}

				return PluginResponse.Ok;
			}
			catch (TypeLoadException tlex)
			{
				Log.Warn(nameof(InitlializeAssembly) + " failed, The file \"{0}\" seems to be missing some dependecies ({1})", File.Name, tlex.Message);
				return PluginResponse.MissingDependency;
			}
			catch (Exception ex)
			{
				Log.Error(ex, nameof(InitlializeAssembly) + " failed");
				return PluginResponse.Crash;
			}
		}

		/// <summary>
		/// Starts the plugin to have all its functionality available in the bot.
		/// This call requires this plugin to be in the <see cref="PluginStatus.Ready"/> state.
		/// Changes the status to <see cref="PluginStatus.Active"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		public PluginResponse Start(Bot bot)
		{
			if (writeStatus)
				PersistentEnabled = true;

			switch (CheckStatus(bot))
			{
			case PluginStatus.Disabled:
				return PluginResponse.Disabled;

			case PluginStatus.Off:
				var response = Load();
				if (response != PluginResponse.Ok)
					return response;
				goto case PluginStatus.Ready;

			case PluginStatus.Ready:
				try
				{
					StartInternal(bot);
					return PluginResponse.Ok;
				}
				catch (Exception ex)
				{
					Stop(bot);
					Log.Warn("Plugin \"{0}\" failed to load: {1}", File.Name, ex);
					return PluginResponse.UnknownError;
				}
			case PluginStatus.Active:
				return PluginResponse.Ok;

			case PluginStatus.Error:
				return PluginResponse.UnknownError;

			case PluginStatus.NotAvailable:
				return PluginResponse.MissingContext;

			default:
				throw new ArgumentOutOfRangeException();
			}
		}

		private void StartInternal(Bot bot)
		{
			if (CheckStatus(bot) != PluginStatus.Ready)
				throw new InvalidOperationException("This plugin has not yet been prepared");

			try
			{
				switch (Type)
				{
				case PluginType.None:
					throw new InvalidOperationException("A 'None' plugin cannot be loaded");

				case PluginType.BotPlugin:
					if (bot == null)
					{
						Log.Error("This plugin needs to be activated on a bot instance.");
						status = PluginStatus.Error;
						return;
					}
					if (pluginObjectList.ContainsKey(bot))
						throw new InvalidOperationException("Plugin is already instantiated on this bot");
					var pluginInstance = (IBotPlugin)Activator.CreateInstance(coreType);
					if (pluginObjectList.Count == 0)
						StartRegisterCommands(pluginInstance, coreType);
					pluginObjectList.Add(bot, pluginInstance);
					if (!bot.Injector.TryInject(pluginInstance))
						Log.Warn("Some dependencies are missing for this plugin");
					pluginInstance.Initialize();
					break;

				case PluginType.CorePlugin:
					pluginObject = (ICorePlugin)Activator.CreateInstance(coreType);
					StartRegisterCommands(pluginObject, coreType);
					if (!CoreInjector.TryInject(pluginObject))
						Log.Warn("Some dependencies are missing for this plugin");
					pluginObject.Initialize();
					break;

				case PluginType.Factory:
					factoryObject = (IFactory)Activator.CreateInstance(coreType);
					FactoryManager.AddFactory(factoryObject);
					break;

				case PluginType.Commands:
					StartRegisterCommands(null, coreType);
					break;

				default:
					throw Util.UnhandledDefault(Type);
				}
			}
			catch (MissingMethodException mmex)
			{
				Log.Error(mmex, "Plugins and Factories needs a parameterless constructor.");
				status = PluginStatus.Error;
				return;
			}

			if (Type != PluginType.BotPlugin)
				status = PluginStatus.Active;
		}

		private void StartRegisterCommands(object obj, Type t)
		{
			var cmdBuildList = CommandManager.GetCommandMethods(obj, t);
			ExposedCommands = CommandManager.GetBotCommands(cmdBuildList).ToList();
			RightsManager.RegisterRights(ExposedRights);
			CommandManager.RegisterCollection(this);
		}

		private void StopUnregisterCommands()
		{
			CommandManager.UnregisterCollection(this);
			RightsManager.UnregisterRights(ExposedRights);
			ExposedCommands = null;
		}

		/// <summary>
		/// Stops the plugin and removes all its functionality available in the bot.
		/// Changes the status from <see cref="PluginStatus.Active"/> to <see cref="PluginStatus.Ready"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		public PluginResponse Stop(Bot bot)
		{
			if (writeStatus)
				PersistentEnabled = false;

			if (CheckStatus(bot) != PluginStatus.Active)
				return PluginResponse.Ok;

			switch (Type)
			{
			case PluginType.None:
				break;

			case PluginType.BotPlugin:
				if (bot == null)
				{
					foreach (var plugin in pluginObjectList.Values)
						plugin.Dispose();
					pluginObjectList.Clear();
				}
				else
				{
					if (!pluginObjectList.TryGetValue(bot, out var plugin))
						throw new InvalidOperationException("Plugin active but no instance found");
					plugin.Dispose();
					pluginObjectList.Remove(bot);
				}
				if (pluginObjectList.Count == 0)
					StopUnregisterCommands();
				break;

			case PluginType.CorePlugin:
				StopUnregisterCommands();
				pluginObject.Dispose();
				pluginObject = null;
				break;

			case PluginType.Factory:
				FactoryManager.RemoveFactory(factoryObject);
				break;

			case PluginType.Commands:
				StopUnregisterCommands();
				break;

			default:
				throw Util.UnhandledDefault(Type);
			}

			status = PluginStatus.Ready;

			return PluginResponse.Ok;
		}

		public void Unload()
		{
			Stop(null);

			coreType = null;

			if (CheckStatus(null) == PluginStatus.Ready)
				status = PluginStatus.Off;
		}

		public override string ToString() => Name;
	}

	public enum PluginType
	{
		None,
		BotPlugin,
		CorePlugin,
		Factory,
		Commands,
	}
}
