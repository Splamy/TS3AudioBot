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
	using ResourceFactories;
	using System;
	using System.CodeDom.Compiler;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Security.Cryptography;

	internal class Plugin
	{
		private readonly MainBot mainBot;

		private Assembly assembly;
		private byte[] md5CacheSum;
		private ITabPlugin pluginObject;
		private Type pluginType;

		public Plugin(FileInfo pluginFile, MainBot parent, int id)
		{
			mainBot = parent;
			PluginFile = pluginFile;
			Id = id;
			Status = PluginStatus.Off;
		}

		public int Id { get; }
		public FileInfo PluginFile { get; }
		public PluginStatus Status { get; private set; }

		public string Name => pluginType?.Name;

		public IReadOnlyCollection<BotCommand> ExposedCommands { get; private set; }
		private string[] ExposedRights => ExposedCommands.Select(x => x.RequiredRight).ToArray();

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
				foreach (CompilerError error in result.Errors)
				{
					containsErrors |= !error.IsWarning;
					Log.Write(Log.Level.Warning, "Plugin_{0}: {1} L{2}/C{3}: {4}\n",
						Id,
						error.IsWarning ? "Warning" : "Error",
						error.Line,
						error.Column,
						error.ErrorText);
				}

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

				if (pluginTypes.Length > 1)
				{
					Log.Write(Log.Level.Warning, "Any source or binary plugin file may contain one plugin at most.");
					return PluginResponse.TooManyPlugins;
				}

				bool loadedAnything = false;
				if (pluginTypes.Length == 1)
				{
					pluginType = pluginTypes[0];
					loadedAnything = true;
				}

				var factoryTypes = allTypes.Where(t => typeof(IFactory).IsAssignableFrom(t)).ToArray();
				if (factoryTypes.Length > 0)
				{
					// TODO
					loadedAnything = true;
				}

				return loadedAnything
					? PluginResponse.Ok
					: PluginResponse.NoTypeMatch;
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

			// Todo register rights

			try
			{
				pluginObject = (ITabPlugin)Activator.CreateInstance(pluginType);

				var comBuilds = CommandManager.GetCommandMethods(pluginObject);
				ExposedCommands = CommandManager.GetBotCommands(comBuilds).ToList();

				mainBot.CommandManager.RegisterPlugin(this);

				mainBot.RightsManager.RegisterRights(ExposedRights);

				pluginObject.Initialize(mainBot);
			}
			catch (MissingMethodException mmex)
			{
				Log.Write(Log.Level.Error, "The plugin needs a parameterless constructor ({0}).", mmex.Message);
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
			// todo unload everything

			if (Status != PluginStatus.Active)
				return;

			mainBot.CommandManager.UnregisterPlugin(this);

			mainBot.RightsManager.UnregisterRights(ExposedRights);

			if (pluginObject != null)
			{
				pluginObject.Dispose();
				pluginObject = null;
			}

			Status = PluginStatus.Ready;
		}

		public void Unload()
		{
			Stop();

			assembly = null;
			pluginType = null;

			if (Status == PluginStatus.Ready)
				Status = PluginStatus.Off;
		}
	}
}
