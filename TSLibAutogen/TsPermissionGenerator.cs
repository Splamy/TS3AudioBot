using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;

namespace TSLibAutogen;

class TsPermissionGenerator
{
	public static IEnumerable<GenFile> Build(Model model)
	{
		var sourceBuilder = new CodeBuilder();

		sourceBuilder.AppendLine("using System;");
		sourceBuilder.AppendLine("using TSLib.Helper;");
		sourceBuilder.AppendLine("namespace TSLib {");
		sourceBuilder.AppendLine("/// <summary>Source: https://www.tsviewer.com/index.php?page=faq&id=12&newlanguage=en</summary>");
		sourceBuilder.AppendLine("public enum TsPermission {");
		sourceBuilder.AppendLine("undefined,");

		foreach (var perm in model.Permissions)
		{
			sourceBuilder.AppendFormatLine("/// <summary>{0}</summary>", perm.Doc);
			sourceBuilder.AppendFormatLine("{0},", perm.Name);
		}

		sourceBuilder.PopCloseBrace();
		sourceBuilder.AppendLine();

		sourceBuilder.AppendLine("public static partial class TsPermissionHelper {");
		sourceBuilder.AppendLine("public static string GetDescription(TsPermission permid) {");
		sourceBuilder.AppendLine("switch (permid) {");

		sourceBuilder.AppendLine("case TsPermission.undefined: return \"Undefined permission\";");
		foreach (var perm in model.Permissions)
		{
			sourceBuilder.AppendFormatLine("case TsPermission.{0}: return \"{1}\";",
				perm.Name,
				perm.Doc);
		}
		sourceBuilder.AppendLine("default: throw Tools.UnhandledDefault(permid);");

		sourceBuilder.PopCloseBrace(); // switch
		sourceBuilder.PopCloseBrace(); // fn GetDescription
		sourceBuilder.PopCloseBrace(); // class
		sourceBuilder.PopCloseBrace(); // namespace

		return new[] { new GenFile("Permissions", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8)) };
	}
}
