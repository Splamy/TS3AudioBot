using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;

namespace TSLibAutogen
{
	class TsErrorGenerator
	{
		public static IEnumerable<GenFile> Build(GeneratorExecutionContext context, Model model)
		{
			var sourceBuilder = new CodeBuilder();

			sourceBuilder.AppendLine("using System;");
			sourceBuilder.AppendLine("namespace TSLib {");
			sourceBuilder.AppendLine("/// <summary>Source: http://forum.teamspeak.com/threads/102276-Server-query-error-id-list");
			sourceBuilder.AppendLine("public enum TsErrorCode : uint {");

			foreach (var err in model.Errors)
			{
				sourceBuilder.AppendFormatLine("/// <summary>{0}</summary>", err.Doc);
				sourceBuilder.AppendFormatLine("{0} = {1},", err.Name, err.Value);
			}
			sourceBuilder.AppendLine("/// <summary>For own custom errors</summary>");
			sourceBuilder.AppendLine("custom_error = 0xFFFF,");

			sourceBuilder.PopCloseBrace();
			sourceBuilder.PopCloseBrace();

			return new[] { new GenFile("Errors", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8)) };
		}
	}
}
