global using GenerationContextType = Microsoft.CodeAnalysis.SourceProductionContext;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TSLibAutogen;

[Generator]
public class Autogen : IIncrementalGenerator
{

	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		var provider = context.AdditionalTextsProvider
			.Combine(context.AnalyzerConfigOptionsProvider)
			.Select(static (provider, _) =>
			{
				var (AdditionalFile, AnalyzerConfigOptions) = provider;
				AnalyzerConfigOptions.GetOptions(AdditionalFile)
					.TryGetValue("build_metadata.additionalfiles.TsDeclType", out string? loadTimeString);
				if (!Enum.TryParse(loadTimeString, ignoreCase: true, out AutoGenType autoGenType))
					return (AutoGenType.Unknown, null!);
				return (autoGenType, AdditionalFile);
			})
			.Where(tsfileInfo => tsfileInfo.autoGenType != AutoGenType.Unknown)
			.Collect();

		context.RegisterSourceOutput(provider, Execute);

	}

	public static void Execute(GenerationContextType context, ImmutableArray<(AutoGenType, AdditionalText)> files)
	{
		var builder = new ModelBuilder();

		foreach (var (autoGenType, file) in files)
		{
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

		var GeneratedFiles = new List<GenFile>();
		GeneratedFiles.AddRange(BookGenerator.Build(model));
		GeneratedFiles.AddRange(MessagesGenerator.Build(model));
		GeneratedFiles.AddRange(M2BGenerator.Build(context, model));
		GeneratedFiles.AddRange(TsErrorGenerator.Build(model));
		GeneratedFiles.AddRange(TsPermissionGenerator.Build(model));
		GeneratedFiles.AddRange(TsVersionGenerator.Build(context, model));
		GeneratedFiles.AddRange(NotifyEvents.Build(model));

		foreach (var genFile in GeneratedFiles)
		{
			context.AddSource($"{genFile.Name}.g.cs", genFile.SourceText);
		}
	}
}

public record GenFile(string Name, SourceText SourceText);

public enum AutoGenType
{
	Unknown,
	Book,
	Messages,
	M2B,
	Errors,
	Permissions,
	Versions,
}
