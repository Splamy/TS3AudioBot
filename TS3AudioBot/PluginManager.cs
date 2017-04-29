// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot
{
	using System;
	using System.CodeDom.Compiler;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Text;
	using CommandSystem;
	using Helper;

	internal class PluginManager : IDisposable
	{
		private MainBot mainBot;
		private PluginManagerData pluginManagerData;
		private Dictionary<string, Plugin> plugins;
		private HashSet<int> usedIds;

		public PluginManager(MainBot bot, PluginManagerData pmd)
		{
			if (bot == null)
				throw new ArgumentNullException(nameof(bot));

			mainBot = bot;
			pluginManagerData = pmd;
			Util.Init(ref plugins);
			Util.Init(ref usedIds);
		}

		private void CheckAndClearPlugins()
		{
			ClearMissingFiles();
			CheckLocalPlugins();
		}

		/// <summary>Updates the plugin dictinary with new and changed plugins.</summary>
		private void CheckLocalPlugins()
		{
			var dir = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, pluginManagerData.PluginPath));
			if (!dir.Exists)
				return;

			foreach (var file in dir.EnumerateFiles())
			{
				Plugin plugin;
				if (plugins.TryGetValue(file.Name, out plugin))
				{
					if (plugin.status == PluginStatus.Disabled || plugin.status == PluginStatus.Active)
						continue;
					else if (plugin.status == PluginStatus.Ready || plugin.status == PluginStatus.Off)
					{
						UnloadPlugin(plugin, false);
						plugin.Prepare();
					}
				}
				else
				{
					plugin = new Plugin(file, mainBot, GetFreeId());

					if (plugin.Prepare() != PluginResponse.Ok)
						continue;

					plugins.Add(file.Name, plugin);
				}
			}
		}

		/// <summary>Unloads all Plugins which have no corresponding file anymore and removes and removes the from the index list.</summary>
		private void ClearMissingFiles()
		{
			// at first find all missing files
			var missingFiles = plugins.Where(kvp => !File.Exists(kvp.Value.file.FullName)).ToArray();

			foreach (var misFile in missingFiles)
			{
				// unload if it is loaded and remove
				usedIds.Remove(misFile.Value.Id);
				UnloadPlugin(misFile.Value, true);
				plugins.Remove(misFile.Key);
			}
		}

		public R LoadPlugin(string identifier)
		{
			CheckLocalPlugins();

			int num;
			Plugin plugin;

			if (int.TryParse(identifier, out num))
			{
				plugin = plugins.Select(kvp => kvp.Value).FirstOrDefault(p => p.Id == num);
				return LoadPlugin(plugin);
			}

			if (plugins.TryGetValue(identifier, out plugin))
				return LoadPlugin(plugin);

			plugin = plugins.Select(kvp => kvp.Value).FirstOrDefault(p => p.proxy?.Name == identifier);
			return LoadPlugin(plugin);
		}

		private R LoadPlugin(Plugin plugin)
		{
			if (plugin == null)
				return "Plugin not found";

			if (plugin.status == PluginStatus.Off || plugin.status == PluginStatus.Disabled)
			{
				var response = plugin.Prepare();
				if (response != PluginResponse.Ok)
					return response.ToString();
			}

			if (plugin.status == PluginStatus.Ready)
			{
				try
				{
					plugin.proxy.Run(mainBot);
					mainBot.CommandManager.RegisterPlugin(plugin);
					plugin.status = PluginStatus.Active;
					return R.OkR;
				}
				catch (Exception ex)
				{
					UnloadPlugin(plugin, false);
					string errMsg = "Plugin could not be loaded: " + ex.Message;
					Log.Write(Log.Level.Warning, errMsg);
					return errMsg;
				}
			}
			return "Unknown plugin error";
		}

		private int GetFreeId()
		{
			int id = 0;
			while (usedIds.Contains(id))
				id++;
			usedIds.Add(id);
			return id;
		}

		public PluginResponse UnloadPlugin(string identifier)
		{
			int num;
			Plugin plugin;

			if (int.TryParse(identifier, out num))
			{
				plugin = plugins.Select(kvp => kvp.Value).FirstOrDefault(p => p.Id == num);
				return UnloadPlugin(plugin, true);
			}

			if (plugins.TryGetValue(identifier, out plugin))
				return UnloadPlugin(plugin, true);

			plugin = plugins.Select(kvp => kvp.Value).FirstOrDefault(p => p.proxy?.Name == identifier);
			return UnloadPlugin(plugin, true);
		}

		private PluginResponse UnloadPlugin(Plugin plugin, bool keepUnloaded)
		{
			if (plugin == null)
				return PluginResponse.PluginNotFound;

			plugin.Unload();

			if (keepUnloaded)
				plugin.status = PluginStatus.Disabled;
			return PluginResponse.Ok;
		}

		public string GetPluginOverview() // TODO Api callcable
		{
			CheckAndClearPlugins();

			if (plugins.Count == 0)
			{
				return "No plugins found!";
			}
			else
			{
				var strb = new StringBuilder();
				strb.AppendLine("All available plugins:");
				int digits = (int)Math.Floor(Math.Log10(plugins.Count) + 1);
				foreach (var plugin in plugins.Values)
				{
					strb.Append("#").Append(plugin.Id.ToString("D" + digits, CultureInfo.InvariantCulture)).Append('|');
					switch (plugin.status)
					{
					case PluginStatus.Off: strb.Append("OFF"); break;
					case PluginStatus.Ready: strb.Append("RDY"); break;
					case PluginStatus.Active: strb.Append("+ON"); break;
					case PluginStatus.Disabled: strb.Append("UNL"); break;
					case PluginStatus.Error: strb.Append("ERR"); break;
					default: throw new InvalidProgramException();
					}
					strb.Append('|').AppendLine(plugin.proxy?.Name ?? "<not loaded>");
				}
				return strb.ToString();
			}
		}

		public void Dispose()
		{
			foreach (var plugin in plugins.Values)
				UnloadPlugin(plugin, true);
		}
	}

	public interface ITS3ABPlugin : IDisposable
	{
		void Initialize(MainBot bot);
	}

	public class Plugin
	{
		private MainBot mainBot;
		public int Id { get; }
		public FileInfo file;
		public PluginStatus status;

		public Plugin(FileInfo file, MainBot parent, int id)
		{
			mainBot = parent;
			this.file = file;
			Id = id;
			status = PluginStatus.Off;
		}

		public AppDomain domain;
		internal PluginProxy proxy;

		private static readonly FileInfo ts3File = new FileInfo(typeof(PluginProxy).Assembly.Location);
		private static readonly Type proxType = typeof(PluginProxy);

		public IEnumerable<BotCommand> GetWrappedCommands() => proxy.GetWrappedCommands();

		public PluginResponse Prepare()
		{
			try
			{
				if (file.Extension != ".cs" && file.Extension != ".dll" && file.Extension != ".exe")
					return PluginResponse.UnsupportedFile;

				//todo test shadowcopying
				domain = AppDomain.CreateDomain(
					"Plugin_" + file.Name,
					AppDomain.CurrentDomain.Evidence,
					new AppDomainSetup
					{
						ApplicationBase = ts3File.Directory.FullName,
						PrivateBinPath = "Plugin/..;Plugin",
						PrivateBinPathProbe = ""
					});
				domain.UnhandledException += (s, e) => Unload();
				proxy = (PluginProxy)domain.CreateInstanceAndUnwrap(
					proxType.Assembly.FullName,
					proxType.FullName);

				PluginResponse result;
				if (file.Extension == ".cs")
					result = PrepareSource();
				else if (file.Extension == ".dll" || file.Extension == ".exe")
					result = proxy.LoadAssembly(domain, file);
				else throw new InvalidProgramException();

				if (result == PluginResponse.Ok)
					status = PluginStatus.Ready;
				return result;
			}
			catch (Exception ex)
			{
				Log.Write(Log.Level.Warning, "Possible plugin failed to load: {0}", ex);
				return PluginResponse.Crash;
			}
		}

		private static CompilerParameters GenerateCompilerParameter()
		{
			var cp = new CompilerParameters();
			Assembly[] aarr = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < aarr.Length; i++)
			{
				if (aarr[i].IsDynamic) continue;
				cp.ReferencedAssemblies.Add(aarr[i].Location);
			}
			cp.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);

			// set preferences
			cp.WarningLevel = 3;
			cp.CompilerOptions = "/target:library /optimize";
			cp.GenerateExecutable = false;
			cp.GenerateInMemory = false;
			return cp;
		}

		private PluginResponse PrepareSource()
		{
			var provider = CodeDomProvider.CreateProvider("CSharp");
			var cp = GenerateCompilerParameter();
			var result = provider.CompileAssemblyFromFile(cp, file.FullName);

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
					return PluginResponse.CompileError;
			}
			return proxy.LoadAssembly(domain, result.CompiledAssembly.GetName());
		}

		public void Unload()
		{
			try
			{
				if (status == PluginStatus.Active)
					mainBot.CommandManager.UnregisterPlugin(this);

				if (proxy != null)
					proxy.Stop();

				if (domain != null)
					AppDomain.Unload(domain);
			}
			finally
			{
				proxy = null;
				domain = null;
				status = PluginStatus.Off;
			}
		}
	}

	internal class PluginProxy
	{
		private Type pluginType;
		private Assembly assembly;
		private ITS3ABPlugin pluginObject;
		private MethodInfo[] pluginMethods;

		public PluginProxy()
		{
			pluginObject = null;
		}

		public PluginResponse LoadAssembly(AppDomain domain, FileInfo resolvePlugin)
		{
			var asmBin = File.ReadAllBytes(resolvePlugin.FullName);
			assembly = domain.Load(asmBin);
			return LoadAssembly(domain, assembly);
		}

		public PluginResponse LoadAssembly(AppDomain domain, AssemblyName resolveName)
		{
			assembly = domain.Load(resolveName);
			return LoadAssembly(domain, assembly);
		}

		private PluginResponse LoadAssembly(AppDomain domain, Assembly assembly)
		{
			try
			{
				var types = assembly.GetExportedTypes().Where(t => typeof(ITS3ABPlugin).IsAssignableFrom(t));
				var pluginOk = PluginCountCheck(types);
				if (pluginOk != PluginResponse.Ok) return pluginOk;

				pluginType = types.First();
				return PluginResponse.Ok;
			}
			catch
			{
				Log.Write(Log.Level.Error, "LoadAssembly failed");
				throw;
			}
		}

		private PluginResponse PluginCountCheck(IEnumerable<Type> types)
		{
			if (!types.Any())
				return PluginResponse.NoTypeMatch;
			else if (types.Skip(1).Any())
			{
				Log.Write(Log.Level.Warning, "Any source or binary file must only contain one plugin.");
				return PluginResponse.TooManyPlugins;
			}
			else
				return PluginResponse.Ok;
		}

		public void Run(MainBot bot)
		{
			pluginObject = (ITS3ABPlugin)Activator.CreateInstance(pluginType);
			pluginObject.Initialize(bot);
		}

		public void Stop()
		{
			if (pluginObject != null)
			{
				pluginObject.Dispose();
				pluginObject = null;
			}
		}

		public List<WrappedCommand> GetWrappedCommands()
		{
			var comBuilds = CommandManager.GetCommandMethods(pluginObject);

			var pluginMethodList = new List<MethodInfo>();
			var wrappedList = new List<WrappedCommand>();
			foreach (var comData in comBuilds)
			{
				pluginMethodList.Add(comData.method);
				int index = pluginMethodList.Count - 1;
				comData.usageList = comData.method.GetCustomAttributes<UsageAttribute>();
				wrappedList.Add(new WrappedCommand(index, this, comData));
			}
			pluginMethods = pluginMethodList.ToArray();

			return wrappedList;
		}

		private static Type CreateDelegateType(Type ret, Type[] param)
		{
			var tArgs = new List<Type>(param) { ret };
			return Expression.GetDelegateType(tArgs.ToArray());
		}

		public object InvokeMethod(int num, object[] param) => pluginMethods[num].Invoke(pluginObject, param);

		public string Name => pluginType?.Name;
	}

	internal class WrappedCommand : BotCommand
	{
		private PluginProxy proxy;
		private int mId;

		public WrappedCommand(int invNum, PluginProxy wrapParent, CommandBuildInfo data) : base(data)
		{
			proxy = wrapParent;
			mId = invNum;
		}

		protected override object ExecuteFunction(object[] parameters)
		{
			try
			{
				return proxy.InvokeMethod(mId, parameters);
			}
			catch (TargetInvocationException ex)
			{
				throw ex.InnerException;
			}
		}
	}

	public enum PluginStatus
	{
		/// <summary>The plugin has just been found and is ready to be prepared.</summary>
		Off,
		/// <summary>The plugin is valid and ready to be loaded.</summary>
		Ready,
		/// <summary>The plugin is currently active.</summary>
		Active,
		/// <summary>The plugin has been plugged off intentionally and will not be prepared with the next scan.</summary>
		Disabled,
		/// <summary>The plugin failed to load.</summary>
		Error,
	}

	public enum PluginResponse
	{
		Ok,
		UnsupportedFile,
		Crash,
		NoTypeMatch,
		TooManyPlugins,
		UnknownStatus,
		PluginNotFound,
		CompileError,
	}

	public class PluginManagerData : ConfigData
	{
		[Info("The path to the pugins", "Plugins")]
		public string PluginPath { get; set; }
	}
}
