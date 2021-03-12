using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TSLibAutogen
{
	[Generator]
	public class Autogen : ISourceGenerator
	{
		public static Guid Guid = Guid.NewGuid();
		public readonly List<GenFile> GeneratedFiles = new();
		public int CInit = 0;
		public int CExec = 0;
		public int lastCount = -1;

		public void Initialize(GeneratorInitializationContext context)
		{
			//File.AppendAllText(@"D:\VMRec\tslog.txt", $"Init {Guid} {CInit++:00} {DateTime.Now}\n");
		}

		public void Execute(GeneratorExecutionContext context)
		{
			//File.AppendAllText(@"D:\VMRec\tslog.txt", $"Exec {Guid} {CExec++:00} {DateTime.Now}\n");

			if (lastCount != context.AdditionalFiles.Length)
			{
				//File.AppendAllText(@"D:\VMRec\tslog.txt", $"Reca {Guid} __ {DateTime.Now}\n");
				lastCount = context.AdditionalFiles.Length;

				var builder = new ModelBuilder();

				foreach (var file in context.AdditionalFiles)
				{
					context.AnalyzerConfigOptions.GetOptions(file)
						.TryGetValue("build_metadata.additionalfiles.TsDeclType", out string? loadTimeString);
					if (!Enum.TryParse(loadTimeString, ignoreCase: true, out AutoGenType autoGenType))
						continue;

					var src = file.GetText(context.CancellationToken)?.ToString();
					if (src is null)
						continue;

					switch (autoGenType)
					{
					case AutoGenType.Book: builder.AddBook(src); break;
					case AutoGenType.Messages: builder.AddMessages(src); break;
					case AutoGenType.M2B: builder.AddM2B(src); break;
					case AutoGenType.Errors: builder.AddErrors(src); break;
					case AutoGenType.Permissions: builder.AddPermissions(src); break;
					case AutoGenType.Versions: builder.AddVersions(src); break;
					default:
						// TODO add warning
						break;
					}
				}

				var model = builder.Build(context);

				GeneratedFiles.AddRange(BookGenerator.Build(context, model));
				GeneratedFiles.AddRange(MessagesGenerator.Build(context, model));
				GeneratedFiles.AddRange(M2BGenerator.Build(context, model));
				GeneratedFiles.AddRange(TsErrorGenerator.Build(context, model));
				GeneratedFiles.AddRange(TsPermissionGenerator.Build(context, model));
				GeneratedFiles.AddRange(TsVersionGenerator.Build(context, model));
				GeneratedFiles.AddRange(NotifyEvents.Build(context, model));
			}
			else
			{
				//File.AppendAllText(@"D:\VMRec\tslog.txt", $"Cach {Guid} {GeneratedFiles.Count}F {DateTime.Now}\n");
			}

			foreach (var genFile in GeneratedFiles)
			{
				context.AddSource(genFile.Name, genFile.SourceText);
			}
		}
	}

	public record GenFile(string Name, SourceText SourceText);

	enum AutoGenType
	{
		Book,
		Messages,
		M2B,
		Errors,
		Permissions,
		Versions,
	}
}
