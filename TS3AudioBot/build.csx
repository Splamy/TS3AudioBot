#r "nuget: SimpleExec, 6.2.0"
#r "nuget: Newtonsoft.Json, 12.0.3"

using Newtonsoft.Json;
using static SimpleExec.Command;

if (Args.Count == 0) {
	WriteLine("This script is itended to be used in the build pipeline");
	return 1;
}

string outFile = Args[0];
string buildConfig = Args[1];

string json = Read("dotnet", "gitversion");

var version = JsonConvert.DeserializeAnonymousType(json, new {
	FullSemVer = "",
	BranchName = "",
	Sha = "",

	AssemblySemVer = "",
	AssemblySemFileVer = "",
	InformationalVersion = "",
});

var genFile = $@"
[assembly: System.Reflection.AssemblyVersion(""{version.AssemblySemVer}"")]
[assembly: System.Reflection.AssemblyFileVersion(""{version.AssemblySemFileVer}"")]
[assembly: System.Reflection.AssemblyInformationalVersion(""{version.InformationalVersion}"")]

namespace TS3AudioBot.Environment
{{
	partial class BuildData {{
		partial void GetDataInternal() {{
			this.Version = ""{version.FullSemVer}"";
			this.Branch = ""{version.BranchName}"";
			this.CommitSha = ""{version.Sha}"";

			this.BuildConfiguration = ""{buildConfig}"";
		}}
	}}
}}
";

Console.WriteLine("Generated Version {0}", version.FullSemVer);
var writeFull = Path.GetFullPath(outFile);
//WriteLine("Writing to {0}", writeFull);
File.WriteAllText(writeFull, genFile);
