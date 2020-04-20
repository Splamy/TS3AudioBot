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
using Microsoft.CodeAnalysis.Emit;
using TSLib.Helper;

namespace TS3AudioBot.Plugins
{
	internal class Plugin
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public CoreInjector CoreInjector { get; set; }
		public ResourceResolver ResourceResolver { get; set; }
		public BotManager BotManager { get; set; }

		private byte[] md5CacheSum;
		private PluginObjects corePlugin;
		private readonly Dictionary<Bot, PluginObjects> botPluginList = new Dictionary<Bot, PluginObjects>();
		private IResolver factoryObject;
		private Type pluginType;
		private PluginStatus status;

		internal PluginType Type { get; private set; }
		public int Id { get; }
		public FileInfo File { get; }
		// TODO remove after plugin rework
		internal PluginObjects CorePlugin => corePlugin;

		public Plugin(FileInfo file, int id)
		{
			corePlugin = null;
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

				var name = pluginType?.Name ?? File.Name;

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
				return botPluginList.ContainsKey(bot) ? PluginStatus.Active : PluginStatus.Ready;
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
			var pdbFile = File.FullName.Substring(0, File.FullName.Length - File.Extension.Length) + ".pdb";
			byte[] pdbBin = null;
			try
			{
				if (System.IO.File.Exists(pdbFile))
					pdbBin = System.IO.File.ReadAllBytes(pdbFile);
			}
			catch (Exception ex) { Log.Debug(ex, "No pdb file found"); }
			var assembly = Assembly.Load(asmBin, pdbBin);
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

				using (var ms_assembly = new MemoryStream())
				using (var ms_pdb = new MemoryStream())
				{
					var result = compilation.Emit(ms_assembly, ms_pdb,
						options: new EmitOptions()
							.WithDebugInformationFormat(DebugInformationFormat.PortablePdb)
						);

					if (result.Success)
					{
						ms_assembly.Seek(0, SeekOrigin.Begin);
						ms_pdb.Seek(0, SeekOrigin.Begin);
						var assembly = Assembly.Load(ms_assembly.ToArray(), ms_pdb.ToArray());
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
				var pluginTypes = allTypes.Where(t => typeof(ITabPlugin).IsAssignableFrom(t)).ToArray();
				var factoryTypes = allTypes.Where(t => typeof(IResolver).IsAssignableFrom(t)).ToArray();
#pragma warning disable CS0618 // Type or member is obsolete
				var commandsTypes = allTypes.Where(t => t.GetCustomAttribute<StaticPluginAttribute>() != null).ToArray();
#pragma warning restore CS0618 // Type or member is obsolete

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
					pluginType = pluginTypes[0];
					if (typeof(IBotPlugin).IsAssignableFrom(pluginType))
						Type = PluginType.BotPlugin;
					else if (typeof(ICorePlugin).IsAssignableFrom(pluginType))
						Type = PluginType.CorePlugin;
					else
						throw new InvalidOperationException("Do not inherit from 'ITabPlugin', instead use 'IBotPlugin' or 'ICorePlugin'");
				}
				else if (factoryTypes.Length == 1)
				{
					pluginType = factoryTypes[0];
					Type = PluginType.Factory;
				}
				else if (commandsTypes.Length == 1)
				{
					pluginType = commandsTypes[0];
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
					if (botPluginList.ContainsKey(bot))
						throw new InvalidOperationException("Plugin is already instantiated on this bot");

					var botPluginObjs = CreatePluginObjects(bot.Injector, false);
					botPluginList.Add(bot, botPluginObjs);
					botPluginObjs.Plugin.Initialize();
					break;

				case PluginType.CorePlugin:
					corePlugin = CreatePluginObjects(CoreInjector, false);
					BotManager.IterateAll(b =>
					{
						try
						{
							if (b.Injector.TryGet<CommandManager>(out var commandManager))
								commandManager.RegisterCollection(corePlugin.Bag);
						}
						catch (Exception ex) { Log.Error(ex, "Faile to register commands from plugin '{0}' for bot '{1}'", Name, b.Id); }
					});
					corePlugin.Plugin.Initialize();
					break;

				case PluginType.Factory:
					factoryObject = (IResolver)Activator.CreateInstance(pluginType);
					ResourceResolver.AddResolver(factoryObject);
					break;

				case PluginType.Commands:
					corePlugin = CreatePluginObjects(CoreInjector, true);
					break;

				default:
					throw Tools.UnhandledDefault(Type);
				}
			}
			catch (Exception ex)
			{
				if (ex is MissingMethodException)
					Log.Error(ex, "Factories needs a parameterless constructor.");
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

		// Note, the 'isStatic' flag is only temporary while StaticPlugins are being
		// deprecated, after that this distinction is not necessary anymore and
		// can be removed.
		public PluginObjects CreatePluginObjects(IInjector injector, bool isStatic)
		{
			object pluginInstance = null;
			if (!isStatic)
			{
				if (!injector.TryCreate(pluginType, out pluginInstance))
					return null; // TODO
				injector.FillProperties(pluginInstance);
			}
			if (!injector.TryGet<CommandManager>(out var commandManager))
				return null; //TODO

			var pluginObjs = new PluginObjects
			{
				Plugin = (ITabPlugin)pluginInstance,
				Bag = new PluginCommandBag(pluginInstance, pluginType),
				CommandManager = commandManager,
			};

			pluginObjs.CommandManager.RegisterCollection(pluginObjs.Bag);
			return pluginObjs;
		}

		/// <summary>
		/// Stops the plugin and removes all its functionality available in the bot.
		/// Changes the status from <see cref="PluginStatus.Active"/> to <see cref="PluginStatus.Ready"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		/// <param name="bot">The bot instance where this plugin should be stopped. Can be null when not required.</param>
		public PluginResponse Stop(Bot bot)
		{
			switch (Type)
			{
			case PluginType.None:
				break;

			case PluginType.BotPlugin:
				if (bot is null)
				{
					foreach (var pluginObjs in botPluginList.Values)
						DestroyPluginObjects(pluginObjs);
					botPluginList.Clear();
				}
				else
				{
					if (botPluginList.TryGetValue(bot, out var pluginObjs))
					{
						botPluginList.Remove(bot);
						DestroyPluginObjects(pluginObjs);
					}
				}
				break;

			case PluginType.CorePlugin:
				if (corePlugin != null)
				{
					BotManager.IterateAll(b =>
					{
						if (b.Injector.TryGet<CommandManager>(out var commandManager))
							commandManager.UnregisterCollection(corePlugin.Bag);
					});
					DestroyPluginObjects(corePlugin);
					corePlugin = null;
				}
				break;

			case PluginType.Factory:
				ResourceResolver.RemoveResolver(factoryObject);
				break;

			case PluginType.Commands:
				if (corePlugin != null)
					DestroyPluginObjects(corePlugin);
				break;

			default:
				throw Tools.UnhandledDefault(Type);
			}

			status = PluginStatus.Ready;

			return PluginResponse.Ok;
		}

		private void DestroyPluginObjects(PluginObjects pluginObjs)
		{
			pluginObjs.CommandManager.UnregisterCollection(pluginObjs.Bag);

			try
			{
				pluginObjs.Plugin?.Dispose();
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Plugin '{0}' threw an exception while disposing", Name);
			}
		}

		public void Unload()
		{
			Stop(null);

			pluginType = null;

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
