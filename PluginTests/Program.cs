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
			mc.LoadPlugin(new FileInfo("PluginA.cs"));
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

		public PluginResponse LoadPlugin(FileInfo file)
		{
			try
			{
				if (file.Extension != ".cs" && file.Extension != ".dll" && file.Extension != ".exe")
					return PluginResponse.UnsupportedFile;

				domain = AppDomain.CreateDomain(
					"Plugin_" + file.Name,
					AppDomain.CurrentDomain.Evidence,
					new AppDomainSetup
					{
						ShadowCopyFiles = "true",
						ShadowCopyDirectories = ts3File.Directory.FullName,
						ApplicationBase = ts3File.Directory.FullName,
						PrivateBinPath = "Plugin/..;Plugin",
						PrivateBinPathProbe = ""
					});
				domain.UnhandledException += (s, e) => { Console.WriteLine("Plugin unex: {0}", e.ExceptionObject); };

				//domain.Load()

				Assembly result;
				if (file.Extension == ".cs")
					result = PrepareSource(file);
				else if (file.Extension == ".dll" || file.Extension == ".exe")
					result = PrepareBinary(file);
				else throw new InvalidProgramException();
				
				return PluginResponse.Ok;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Possible plugin failed to load: ", ex);
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

	public interface IPlugin
	{
		void Initialize(MainClass mc);
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
