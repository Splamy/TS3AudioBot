using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSLibAutogen
{
	class M2BGenerator
	{
		public static IEnumerable<GenFile> Build(GeneratorExecutionContext context, Model model)
		{
			var src = new CodeBuilder();

			src.AppendLine("using TSLib.Messages;");
			src.AppendLine(Util.ConversionSet);

			src.AppendLine("#pragma warning disable CS0472, CS8073");
			src.AppendLine("namespace TSLib.Full.Book {");
			src.AppendLine("partial class Connection {");
			src.AppendLine("internal const byte __DummyGenM2B = 0;");

			foreach (var rulegroup in model.M2B.GroupBy(r => r.From))
			{
				var rulegroupList = rulegroup.ToArray();
				var msg = rulegroup.Key;
				src.AppendFormatLine("public void Apply{0}({0} msg) {{", msg.Name);

				foreach (var rule in rulegroupList)
				{
					var bookTo = rule.To;
					var idStr = string.Join(", ", rule.id.Select(x => $"msg.{x}"));
					if (rulegroupList.Length > 1) src.AppendLine("{");

					switch (rule.operation)
					{
					case "add":
					case "update":
						if (rule.operation == "add" || bookTo.Optional)
						{
							src.AppendLine($"var obj = new {bookTo.Name}();");
						}
						else
						{
							src.AppendFormatLine("var obj = Get{0}({1});", bookTo.Name, idStr);
							src.AppendLine("if (obj == null) {");
							src.AppendFormatLine("Log.Warn(\"Internal Book protocol error. 'Apply{0}' has no local object ({{$msg}})\", msg);", msg.Name);
							src.AppendLine("return;");
							src.PopCloseBrace();
						}

						foreach (var prop in rule.properties.OrderBy(x => x.to))
						{
							void WriteMove(string from, string to)
							{
								var bookProp = bookTo.Properties.FirstOrDefault(x => x.name == to);
								if (bookProp is null)
									throw context.ParseError($"No property found: '{to}'");

								if (prop.operation is null)
								{
									switch (bookProp.mod)
									{
									case null:
										if (from == "null")
											src.AppendLine($"obj.{to} = null;");
										else
											src.AppendLine($"{{ var tmpv = {from}; if (tmpv != null) obj.{to} = ({bookProp.type})tmpv; }}");
										break;

									case "set":
										src.AppendLine($"{{ var tmpa = {from}; if (tmpa != null) {{ obj.{to}.Clear(); obj.{to}.UnionWith(tmpa); }} }}");
										break;

									case "array":
										src.AppendLine($"{{ var tmpa = {from}; if (tmpa != null) {{ obj.{to} = tmpa; }} }}");
										break;

									case "map":
										src.AppendFormatLine("// map update to {0}.{1}", bookTo.Name, bookProp.name);
										break;

									default:
										throw context.ParseError("Unknown mod type: " + bookProp.mod);
									}
								}
								else if (prop.operation == "add")
								{
									// Currently hacky; Better check:
									//  [update:single->single]
									//  [add/remove/update:single->array]
									//  [update:array->array]
									src.AppendLine($"obj.{to}.Add({from});");
								}
								else if (prop.operation == "remove")
								{
									// Same here
									src.AppendLine($"obj.{to}.Remove({from});");
								}
								else
									throw new Exception("Unknown operation: " + prop.operation);
							}

							if (prop.from is not null)
							{
								if (prop.to is null) throw context.ParseError($"Rule '{rule.From.Name}->{rule.To.Name}' has prop 'from:{prop.from}' but no 'to'");
								WriteMove($"msg.{prop.from}", prop.to);
							}
							else if (prop.function is not null)
							{
								if (prop.tolist is null) throw context.ParseError($"Rule '{rule.From.Name}->{rule.To.Name}' has function 'function:{prop.function}' but no 'tolist'");

								if (prop.function == "ReturnNone")
									WriteMove($"null", prop.tolist[0]);
								else if (prop.function == "VoidFun") { /* Do Nothing */ }
								else if (prop.tolist.Length == 0)
									src.AppendLine($"{prop.function}(msg);");
								else if (prop.tolist.Length == 1)
									WriteMove($"{prop.function}(msg)", prop.tolist[0]);
								else
								{
									src.AppendLine("{");
									src.AppendLine($"var tmp = {prop.function}(msg);");
									for (int i = 0; i < prop.tolist.Length; i++)
										WriteMove($"tmp.Item{(i + 1)}", prop.tolist[i]);
									src.PopCloseBrace();
								}
							}
						}
						if (rule.operation == "add")
						{
							src.AppendFormatLine("Set{0}(obj{1});",
								bookTo.Name,
								string.IsNullOrEmpty(idStr) ? "" : (", " + idStr));
						}
						break;

					case "remove":
						src.AppendFormatLine("Remove{0}({1});", bookTo.Name, idStr);
						break;
					}
					if (rulegroupList.Length > 1) src.PopCloseBrace();
				}
				src.AppendFormatLine("Post{0}(msg);", msg.Name);
				src.PopCloseBrace(); // fn Apply

				src.AppendFormatLine("partial void Post{0}({0} msg);", msg.Name);
				src.AppendLine();
			}

			src.PopCloseBrace(); // class
			src.PopCloseBrace(); // namespace

			return new[] { new GenFile("MessagesToBook", SourceText.From(src.ToString(), Encoding.UTF8)) };
		}
	}
}
