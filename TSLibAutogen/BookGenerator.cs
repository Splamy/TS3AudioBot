using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;

namespace TSLibAutogen;

public class BookGenerator
{
	public static IEnumerable<GenFile> Build(Model model)
	{
		var src = new CodeBuilder();

		src.AppendLine("using System;");
		src.AppendLine("using System.Collections.Generic;");
		src.AppendLine("using System.Text.Json;");
		src.AppendLine("using System.Text.Json.Serialization;");
		src.AppendLine(Util.ConversionSet);

		src.AppendLine("#pragma warning disable CS8618");
		src.AppendLine("namespace TSLib.Full.Book {");

		foreach (var struc in model.Book)
		{
			src.AppendFormatLine("public sealed partial class {0} {{", struc.Name);

			foreach (var prop in struc.Properties)
			{
				var converterAttribute = prop.mod switch
				{
					"map" => $"[JsonConverter(typeof({prop.key}.DictConverter<{prop.type}>))]",
					_ => null,
				};
				if (!string.IsNullOrEmpty(converterAttribute))
					src.AppendLine(converterAttribute!);

				var type = prop.mod switch
				{
					"set" => $"HashSet<{prop.type}>",
					"array" => $"{prop.type}[]",
					"map" => $"Dictionary<{prop.key},{prop.type}>",
					_ => prop.type,
				};
				src.AppendFormatLine("public {0}{1} {2} {{ get; set; }}{3}",
					type,
					prop.opt == true ? "?" : "",
					prop.name,
					prop.mod is "set" or "map" ? " = new();" : "");
			}

			src.PopCloseBrace(); // class
			src.AppendLine();
		}

		src.PopCloseBrace(); // namespace

		return new[] { new GenFile("Book", SourceText.From(src.ToString(), Encoding.UTF8)) };
	}
}
