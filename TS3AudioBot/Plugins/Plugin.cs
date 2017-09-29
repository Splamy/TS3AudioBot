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
		private readonly MainBot mainBot;

		private Assembly assembly;
		private byte[] md5CacheSum;
		private ITabPlugin pluginObject;
		private IFactory factoryObject;
		private Type coreType;
		private PluginType corePluginType;

		public Plugin(FileInfo pluginFile, MainBot parent, int id)
		{
			mainBot = parent;
			PluginFile = pluginFile;
			Id = id;
			Status = PluginStatus.Off;
			corePluginType = PluginType.None;
		}

		public int Id { get; }
		public FileInfo PluginFile { get; }
		public PluginStatus Status { get; private set; }

		public string Name
		{
			get
			{
				switch (corePluginType)
				{
				case PluginType.Factory:
					return "Factory: " + (factoryObject?.FactoryFor ?? coreType?.Name ?? "<unknown>");
				case PluginType.Plugin:
					return "Plugin: " + (coreType?.Name ?? "<unknown>");
				case PluginType.None:
					return "Unknown";
				default:
					throw Util.UnhandledDefault(corePluginType);
				}
			}
		}

		public bool PersistentEnabled
		{
			get
			{
				if (!File.Exists(PluginFile.FullName + ".status"))
					return false;
				return File.ReadAllText(PluginFile.FullName + ".status") != "0";
			}
			set
			{
				if (!File.Exists(PluginFile.FullName + ".status"))
					return;
				File.WriteAllText(PluginFile.FullName + ".status", value ? "1" : "0");
			}
		}

		public IEnumerable<BotCommand> ExposedCommands { get; private set; }
		public IEnumerable<string> ExposedRights => ExposedCommands.Select(x => x.RequiredRight);


		public PluginResponse Load()
		{
			try
			{
				if (PluginManager.IgnoreFile(PluginFile))
					return PluginResponse.Disabled;

				if (Status != PluginStatus.Off && Md5EqualsCache())
					return Status == PluginStatus.Ready || Status == PluginStatus.Active
						? PluginResponse.Ok
						: PluginResponse.UnknownError;

				Unload();

				PluginResponse result;
				if (PluginFile.Extension == ".cs")
					result = PrepareSource();
				else if (PluginFile.Extension == ".dll" || PluginFile.Extension == ".exe")
					result = PrepareBinary();
				else throw new InvalidProgramException();

				Status = result == PluginResponse.Ok ? PluginStatus.Ready : PluginStatus.Error;
				return result;
			}
			catch (BadImageFormatException bifex)
			{
				Log.Write(Log.Level.Warning, "Plugin \"{0}\" has an invalid format: {1} (Add a \"{0}.ignore\" file to ignore this file)",
					PluginFile.Name,
					bifex.InnerException?.Message ?? bifex.Message);
				Status = PluginStatus.Error;
				return PluginResponse.InvalidBinary;
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Warning, "Plugin \"{0}\" failed to prepare: {1}",
					PluginFile.Name,
					ex.Message);
				Status = PluginStatus.Error;
				return PluginResponse.Crash;
			}
		}

		private bool Md5EqualsCache()
		{
			using (var md5 = MD5.Create())
			{
				using (var stream = File.OpenRead(PluginFile.FullName))
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
			var asmBin = File.ReadAllBytes(PluginFile.FullName);
			assembly = Assembly.Load(asmBin);
			return InitlializeAssembly(assembly);
		}

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
			var result = provider.CompileAssemblyFromFile(cp, PluginFile.FullName);

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
				Log.Write(Log.Level.Warning, strb.ToString());

				if (containsErrors)
				{
					Status = PluginStatus.Error;
					return PluginResponse.CompileError;
				}
			}
			return InitlializeAssembly(result.CompiledAssembly);
		}

		private PluginResponse InitlializeAssembly(Assembly assembly)
		{
			try
			{
				this.assembly = assembly;
				var allTypes = assembly.GetExportedTypes();
				var pluginTypes = allTypes.Where(t => typeof(ITabPlugin).IsAssignableFrom(t)).ToArray();
				var factoryTypes = allTypes.Where(t => typeof(IFactory).IsAssignableFrom(t)).ToArray();

				if (pluginTypes.Length + factoryTypes.Length > 1)
				{
					Log.Write(Log.Level.Warning, "Any source or binary plugin file may contain one plugin or factory at most.");
					return PluginResponse.TooManyPlugins;
				}
				if (pluginTypes.Length + factoryTypes.Length == 0)
				{
					Log.Write(Log.Level.Warning, "Any source or binary plugin file must contain at least one plugin or factory.");
					return PluginResponse.NoTypeMatch;
				}

				if (pluginTypes.Length == 1)
				{
					coreType = pluginTypes[0];
					corePluginType = PluginType.Plugin;
				}
				else if (factoryTypes.Length == 1)
				{
					coreType = factoryTypes[0];
					corePluginType = PluginType.Factory;
				}
				else
				{
					corePluginType = PluginType.None;
					throw new InvalidOperationException();
				}

				return PluginResponse.Ok;
			}
			catch (TypeLoadException tlex)
			{
				Log.Write(Log.Level.Warning,
					$"{nameof(InitlializeAssembly)} failed, The file \"{PluginFile.Name}\" seems to be missing some dependecies ({tlex.Message})");
				return PluginResponse.MissingDependency;
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Error, $"{nameof(InitlializeAssembly)} failed ({ex})");
				return PluginResponse.Crash;
			}
		}

		/// <summary>
		/// Starts the plugin to have all its functionality available in the bot.
		/// This call requires this plugin to be in the <see cref="PluginStatus.Ready"/> state.
		/// Changes the status to <see cref="PluginStatus.Active"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		public void Start()
		{
			if (Status != PluginStatus.Ready)
				throw new InvalidOperationException("This plugin has not yet been prepared");

			try
			{
				switch (corePluginType)
				{
				case PluginType.None:
					break;

				case PluginType.Plugin:
					pluginObject = (ITabPlugin)Activator.CreateInstance(coreType);
					var comBuilds = CommandManager.GetCommandMethods(pluginObject);
					ExposedCommands = CommandManager.GetBotCommands(comBuilds).ToList();
					mainBot.RightsManager.RegisterRights(ExposedRights);
					mainBot.CommandManager.RegisterCollection(this);
					pluginObject.Initialize(mainBot);
					break;

				case PluginType.Factory:
					factoryObject = (IFactory)Activator.CreateInstance(coreType);
					mainBot.FactoryManager.AddFactory(factoryObject);
					break;

				default:
					throw Util.UnhandledDefault(corePluginType);
				}
			}
			catch (MissingMethodException mmex)
			{
				Log.Write(Log.Level.Error, "Plugins and Factories needs a parameterless constructor ({0}).", mmex.Message);
				Status = PluginStatus.Error;
				return;
			}

			Status = PluginStatus.Active;
		}

		/// <summary>
		/// Stops the plugin and removes all its functionality available in the bot.
		/// Changes the status from <see cref="PluginStatus.Active"/> to <see cref="PluginStatus.Ready"/> when successful or <see cref="PluginStatus.Error"/> otherwise.
		/// </summary>
		public void Stop()
		{
			if (Status != PluginStatus.Active)
				return;

			switch (corePluginType)
			{
			case PluginType.None:
				break;

			case PluginType.Plugin:
				mainBot.CommandManager.UnregisterCollection(this);
				mainBot.RightsManager.UnregisterRights(ExposedRights);
				ExposedCommands = null;
				pluginObject.Dispose();
				pluginObject = null;
				break;

			case PluginType.Factory:
				mainBot.FactoryManager.RemoveFactory(factoryObject);
				break;

			default:
				throw Util.UnhandledDefault(corePluginType);
			}

			Status = PluginStatus.Ready;
		}

		public void Unload()
		{
			Stop();

			assembly = null;
			coreType = null;

			if (Status == PluginStatus.Ready)
				Status = PluginStatus.Off;
		}

		public override string ToString() => Name;

		private enum PluginType
		{
			None,
			Plugin,
			Factory,
		}
	}
}
