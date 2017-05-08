using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;

namespace PluginTests
{
	class Program
	{
		static void Main(string[] args)
		{
			var mc = new MainClass();
			var load = mc.LoadPlugin(new FileInfo("PluginA.cs"));
			if(load.response == PluginResponse.Ok)
				Console.WriteLine("OK!");
			else
				Console.WriteLine("ERROR! : " + load.response);

			Console.WriteLine("End");
			Console.ReadLine();
		}
	}

	public class MainClass
	{
		public void Feature1() { }
		public void Feature2(int value) { int x = value + 10; var x2 = x.ToString().Split('1'); }
		public int Feature3() { return 42; }
		public MainClass Feature4() { return this; }

		private static readonly FileInfo ts3File = new FileInfo(typeof(IPlugin).Assembly.Location);
		public AppDomain domain;

		private IFactory factHolder;

		public PluginHolder LoadPlugin(FileInfo file)
		{
			try
			{
				if (file.Extension != ".cs" && file.Extension != ".dll" && file.Extension != ".exe")
					return new PluginHolder(PluginResponse.UnsupportedFile);

				domain = AppDomain.CreateDomain(
					"Plugin_" + file.Name,
					AppDomain.CurrentDomain.Evidence,
					new AppDomainSetup
					{
						ShadowCopyFiles = "true",
						ShadowCopyDirectories = ts3File.Directory.FullName,
						ApplicationBase = ts3File.Directory.FullName,
						PrivateBinPath = "Plugin/..;Plugin",
						PrivateBinPathProbe = "",
					});
				domain.UnhandledException += (s, e) => { Console.WriteLine("Plugin unex: {0}", e.ExceptionObject); };

				//domain.Load()

				Assembly result;
				if (file.Extension == ".cs")
					result = PrepareSource(file);
				else if (file.Extension == ".dll" || file.Extension == ".exe")
					result = PrepareBinary(file);
				else
					throw new InvalidProgramException();

				if (result == null)
					return new PluginHolder(PluginResponse.CompileError);

				var plugins = result.ExportedTypes.Where(t => typeof(IPlugin).IsAssignableFrom(t)).ToArray();
				var facts = result.ExportedTypes.Where(t => typeof(IFactory).IsAssignableFrom(t)).ToArray();

				if (plugins.Length <= 0 && facts.Length <= 0)
					return new PluginHolder(PluginResponse.NoTypeMatch);

				if (plugins.Length > 1)
					return new PluginHolder(PluginResponse.TooManyPlugins);

				var plugin = (IPlugin)Activator.CreateInstance(plugins[0]);

				factHolder = (IFactory)Activator.CreateInstance(facts[0]);

				plugin.Initialize(this);
				factHolder.Process(this);

				AppDomain.Unload(domain);

				factHolder.Process(this);

				return new PluginHolder(PluginResponse.Ok, plugin);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Possible plugin failed to load: {0}", ex);
				return new PluginHolder(PluginResponse.Crash);
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
			cp.GenerateInMemory = true;
			return cp;
		}

		private Assembly PrepareSource(FileInfo file)
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
					Console.WriteLine("Plugin_{0}: {1} L{2}/C{3}: {4}\n",
						file.Name,
						error.IsWarning ? "Warning" : "Error",
						error.Line,
						error.Column,
						error.ErrorText);
				}

				if (containsErrors)
					return null;
			}

			return result.CompiledAssembly;
		}

		private Assembly PrepareBinary(FileInfo file)
		{
			return null;
		}
	}

	public class PluginHolder
	{
		public IPlugin plugin;
		public PluginResponse response;

		public PluginHolder(PluginResponse resp)
		{
			response = resp;
			plugin = null;
		}

		public PluginHolder(PluginResponse resp, IPlugin plug)
		{
			response = resp;
			plugin = plug;
		}
	}

	public interface IPlugin
	{
		void Initialize(MainClass mc);
	}

	public interface IFactory
	{
		void Process(object stuff);
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
}
