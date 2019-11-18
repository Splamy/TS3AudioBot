// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.Dependency;
using TS3AudioBot.ResourceFactories;
using TSLib.Helper;

namespace TS3AudioBot.Plugins
{
	internal class Plugin : ICommandBag
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public CoreInjector CoreInjector { get; set; }
		public ResourceResolver ResourceFactory { get; set; }
		public CommandManager CommandManager { get; set; }

		private byte[] md5CacheSum;
		private ICorePlugin pluginObject;
		private Dictionary<Bot, IBotPlugin> pluginObjectList;
		private IResolver factoryObject;
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
					return $"{File.Name} (Error)";

				var name = coreType?.Name ?? File.Name;

				switch (Type)
				{
				case PluginType.Factory:
					if (factoryObject?.ResolverFor != null)
						return $"{factoryObject.ResolverFor}-factory";
					return $"{name} (Factory)";
				case PluginType.BotPlugin:
					return $"{name} (BotPlugin)";
				case PluginType.CorePlugin:
					return $"{name} (CorePlugin)";
				case PluginType.Commands:
					return $"{name} (Commands)";
				case PluginType.None:
					return $"{File.Name} (Unknown)";
				default:
					throw Tools.UnhandledDefault(Type);
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

		public IReadOnlyCollection<BotCommand> BagCommands { get; private set; }
		public IReadOnlyCollection<string> AdditionalRights => Array.Empty<string>();

		public PluginStatus CheckStatus(Bot bot)
		{
			if (Type != PluginType.BotPlugin)
				return status;
			if (status == PluginStatus.Disabled
				|| status == PluginStatus.Error
				|| status == PluginStatus.Off)
				return status;
			if (bot is null)
				return PluginStatus.NotAvailable;
			if (status == PluginStatus.Ready)
				return pluginObjectList.ContainsKey(bot) ? PluginStatus.Active : PluginStatus.Ready;
			if (status == PluginStatus.Active)
				throw new InvalidOperationException("BotPlugin must not be active");
			throw Tools.UnhandledDefault(status);
		}

		public PluginResponse Load()
		{
			try
			{
				if (PluginManager.IsIgnored(File))
					return PluginResponse.Disabled;

				var locStatus = CheckStatus(null);
				var cacheOk = Md5EqualsCache();
				if (locStatus != PluginStatus.Off && cacheOk)
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
					result = PrepareSource();
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
					if (md5CacheSum is null)
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
			// Do not use 'Assembly.LoadFile' as otherwise we cannot replace the dll
			// on windows aymore after it's opened once.
			var asmBin = System.IO.File.ReadAllBytes(File.FullName);
			var assembly = Assembly.Load(asmBin);
			return InitializeAssembly(assembly);
		}

		private PluginResponse PrepareSource()
		{
			var param = AppDomain.CurrentDomain.GetAssemblies()
				.Where(asm => !asm.IsDynamic && !string.IsNullOrEmpty(asm.Location))
				.Select(asm => MetadataReference.CreateFromFile(asm.Location))
				.Concat(new[] { MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location) }).ToArray();

			using (var pluginFileStream = System.IO.File.OpenRead(File.FullName))
			{
				var sourceTree = CSharpSyntaxTree.ParseText(SourceText.From(pluginFileStream));

				var compilation = CSharpCompilation.Create($"plugin_{File.Name}_{Tools.Random.Next()}")
					.WithOptions(new CSharpCompilationOptions(
						outputKind: OutputKind.DynamicallyLinkedLibrary,
						optimizationLevel: OptimizationLevel.Release))
					.AddReferences(param)
					.AddSyntaxTrees(sourceTree);

				using (var ms = new MemoryStream())
				{
					var result = compilation.Emit(ms);

					if (result.Success)
					{
						ms.Seek(0, SeekOrigin.Begin);
						var assembly = Assembly.Load(ms.ToArray());
						return InitializeAssembly(assembly);
					}
					else
					{
						bool containsErrors = false;
						var strb = new StringBuilder();
						strb.AppendFormat("Plugin \"{0}\" [{1}] compiler notifications:\n", File.Name, Id);
						foreach (var error in result.Diagnostics)
						{
							var position = error.Location.GetLineSpan();
							containsErrors |= error.WarningLevel == 0;
							strb.AppendFormat("{0} L{1}/C{2}: {3}\n",
								error.WarningLevel == 0 ? "Error" : ((DiagnosticSeverity)(error.WarningLevel - 1)).ToString(),
								position.StartLinePosition.Line + 1,
								position.StartLinePosition.Character,
								error.GetMessage());
						}
						strb.Length--; // remove last linebreak
						Log.Warn(strb.ToString());
						return PluginResponse.CompileError;
					}
				}
			}
		}

		private PluginResponse InitializeAssembly(Assembly assembly)
		{
			try
			{
				var allTypes = assembly.GetExportedTypes();
				var pluginTypes = allTypes.Where(t => typeof(ICorePlugin).IsAssignableFrom(t)).ToArray();
				var factoryTypes = allTypes.Where(t => typeof(IResolver).IsAssignableFrom(t)).ToArray();
				var commandsTypes = allTypes.Where(t => t.GetCustomAttribute<StaticPluginAttribute>() != null).ToArray();

				if (pluginTypes.Length + factoryTypes.Length + commandsTypes.Length > 1)
				{
					Log.Warn("Any source or binary plugin file may contain one plugin or factory at most. ({})", Name);
					return PluginResponse.TooManyPlugins;
				}
				if (pluginTypes.Length + factoryTypes.Length + commandsTypes.Length == 0)
				{
					Log.Warn("Any source or binary plugin file must contain at least one plugin or factory. ({})", Name);
					return PluginResponse.NoTypeMatch;
				}

				if (pluginTypes.Length == 1)
				{
					coreType = pluginTypes[0];
					if (typeof(IBotPlugin).IsAssignableFrom(coreType))
					{
						Type = PluginType.BotPlugin;
						pluginObjectList = new Dictionary<Bot, IBotPlugin>();
					}
					else
					{
						Type = PluginType.CorePlugin;
					}
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
				Log.Warn(nameof(InitializeAssembly) + " failed, The file \"{0}\" seems to be missing some dependecies ({1})", File.Name, tlex.Message);
				return PluginResponse.MissingDependency;
			}
			catch (Exception ex)
			{
				Log.Error(ex, nameof(InitializeAssembly) + " failed: {0}", ex.Message);
				return PluginResponse.Crash;
			}
		}

		/// <summary>
		/// Starts the plugin to have all its functionality available in the bot.
		/// This call requires this plugin to be in the <see cref="PluginStatus.Ready"/> state.
		/// Changes the status to <see cref="PluginStatus.Active"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		/// <param name="bot">The bot instance where this plugin should be started. Can be null when not required.</param>
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
				return StartInternal(bot) ? PluginResponse.Ok : PluginResponse.UnknownError;

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

		private bool StartInternal(Bot bot)
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
					if (bot is null)
					{
						Log.Error("This plugin needs to be activated on a bot instance.");
						status = PluginStatus.Error;
						return false;
					}
					if (pluginObjectList.ContainsKey(bot))
						throw new InvalidOperationException("Plugin is already instantiated on this bot");
					var pluginInstance = (IBotPlugin)Activator.CreateInstance(coreType);
					if (pluginObjectList.Count == 0)
						RegisterCommands(pluginInstance, coreType);
					pluginObjectList.Add(bot, pluginInstance);
					bot.Injector.FillProperties(pluginInstance);
					pluginInstance.Initialize();
					break;

				case PluginType.CorePlugin:
					pluginObject = (ICorePlugin)Activator.CreateInstance(coreType);
					RegisterCommands(pluginObject, coreType);
					CoreInjector.FillProperties(pluginObject);
					pluginObject.Initialize();
					break;

				case PluginType.Factory:
					factoryObject = (IResolver)Activator.CreateInstance(coreType);
					ResourceFactory.AddResolver(factoryObject);
					break;

				case PluginType.Commands:
					RegisterCommands(null, coreType);
					break;

				default:
					throw Tools.UnhandledDefault(Type);
				}
			}
			catch (Exception ex)
			{
				if (ex is MissingMethodException)
					Log.Error(ex, "Plugins and Factories needs a parameterless constructor.");
				else
					Log.Error(ex, "Plugin '{0}' failed to load: {1}.", Name, ex.Message);
				Stop(bot);
				if (Type != PluginType.BotPlugin)
					status = PluginStatus.Error;
				return false;
			}

			if (Type != PluginType.BotPlugin)
				status = PluginStatus.Active;

			return true;
		}

		private void RegisterCommands(object obj, Type t)
		{
			BagCommands = CommandManager.GetBotCommands(obj, t).ToArray();
			CommandManager.RegisterCollection(this);
		}

		private void UnregisterCommands()
		{
			if (BagCommands != null)
			{
				CommandManager.UnregisterCollection(this);
				BagCommands = null;
			}
		}

		/// <summary>
		/// Stops the plugin and removes all its functionality available in the bot.
		/// Changes the status from <see cref="PluginStatus.Active"/> to <see cref="PluginStatus.Ready"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		/// <param name="bot">The bot instance where this plugin should be stopped. Can be null when not required.</param>
		public PluginResponse Stop(Bot bot)
		{
			if (writeStatus)
				PersistentEnabled = false;

			switch (Type)
			{
			case PluginType.None:
				break;

			case PluginType.BotPlugin:
				if (bot is null)
				{
					foreach (var plugin in pluginObjectList.Values)
						plugin.Dispose();
					pluginObjectList.Clear();
				}
				else
				{
					if (pluginObjectList.TryGetValue(bot, out var plugin))
					{
						SaveDisposePlugin(plugin);
						pluginObjectList.Remove(bot);
					}
				}
				if (pluginObjectList.Count == 0)
					UnregisterCommands();
				break;

			case PluginType.CorePlugin:
				SaveDisposePlugin(pluginObject);
				UnregisterCommands();
				pluginObject = null;
				break;

			case PluginType.Factory:
				ResourceFactory.RemoveResolver(factoryObject);
				break;

			case PluginType.Commands:
				UnregisterCommands();
				break;

			default:
				throw Tools.UnhandledDefault(Type);
			}

			status = PluginStatus.Ready;

			return PluginResponse.Ok;
		}

		private void SaveDisposePlugin(ICorePlugin plugin)
		{
			try
			{
				plugin?.Dispose();
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Plugin '{0}' threw an exception while disposing", Name);
			}
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
