using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot
{
	class CommandParser
	{
		const char CommandChar = '!';

		// This switch follows more or less a DEA to this EBNF
		// COMMAND-EBNF := COMMAND

		// COMMAND      := (!NAME \s+ (COMMAND|FREESTRING|QUOTESTRING|\s)*)
		// NAME         := [a-z]+
		// FREESTRING   := [^()]+
		// QUOTESTRING  := "[<anything but '\"'>]+"

		public static ASTCommand ParseCommandRequest(string request)
		{
			ASTCommand root = null;
			Stack<ASTCommand> comAst = new Stack<ASTCommand>();
			BuildStatus build = BuildStatus.ParseCommand;
			StringBuilder strb = new StringBuilder();

			for (int i = 0; i < request.Length;)
			{
				char c;
				switch (build)
				{
				case BuildStatus.ParseCommand:
					SkipSpace(ref i, request);

					ASTCommand buildCom = new ASTCommand();

					if (request[i] == '(') i++;
					if (!ValidateChar(ref i, request, CommandChar)) throw new InvalidOperationException();

					if (root == null) root = buildCom;
					else comAst.Peek().Parameter.Add(buildCom);
					comAst.Push(buildCom);

					strb.Clear();
					for (; i < request.Length; i++)
					{
						c = request[i];
						if (c >= 'a' && c <= 'z')
							strb.Append(c);
						else if (char.IsWhiteSpace(c))
							break;
					}
					buildCom.Command = strb.ToString();
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.SelectParam:
					SkipSpace(ref i, request);
					if (i >= request.Length)
						build = BuildStatus.End;
					else
					{
						c = request[i];
						if (c == '"')
							build = BuildStatus.ParseQuotedString;
						else if (c == '(')
						{
							if (i + 1 >= request.Length)
								build = BuildStatus.ParseFreeString;
							else if (request[i + 1] == '!')
								build = BuildStatus.ParseCommand;
							else
								build = BuildStatus.ParseFreeString;
						}
						else if (c == ')')
						{
							if (!comAst.Any())
								build = BuildStatus.End;
							else
							{
								comAst.Pop();
								if (!comAst.Any())
									build = BuildStatus.End;
							}
							i++;
						}
						else
							build = BuildStatus.ParseFreeString;
					}
					break;

				case BuildStatus.ParseFreeString:
					strb.Clear();

					for (; i < request.Length; i++)
					{
						c = request[i];
						if ((c == '(' && i + 1 < request.Length && request[i + 1] == '!')
							|| c == ')')
							break;
						strb.Append(c);
						if (char.IsWhiteSpace(c))
							break;
					}
					buildCom = comAst.Peek();
					buildCom.Parameter.Add(new ASTValue() { Value = strb.ToString() });
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.ParseQuotedString:
					strb.Clear();

					if (!ValidateChar(ref i, request, '"')) throw new InvalidOperationException();

					bool escaped = false;
					for (; i < request.Length; i++)
					{
						c = request[i];
						if (c == '\\') escaped = true;
						else if (c == '"')
						{
							if (escaped) strb.Length--;
							else { i++; break; }
							escaped = false;
						}
						else escaped = false;
						strb.Append(c);
					}
					buildCom = comAst.Peek();
					buildCom.Parameter.Add(new ASTValue() { Value = strb.ToString() });
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.End:
					i = request.Length + 1;
					break;

				default: throw new InvalidOperationException();
				}
			}

			return root;
		}

		private static void SkipSpace(ref int i, string text)
		{
			while (i < text.Length && char.IsWhiteSpace(text[i]))
				i++;
		}

		private static bool ValidateChar(ref int i, string text, char c)
		{
			if (i >= text.Length)
				return false;
			bool ok = text[i] == c;
			if (ok) i++;
			return ok;
		}

		enum BuildStatus
		{
			ParseCommand,
			SelectParam,
			ParseFreeString,
			ParseQuotedString,
			End,
		}
	}

	class ParseError
	{
		public int Position { get; set; }
		public int Length { get; set; }
		public string Description { get; set; }
	}

	abstract class ASTNode
	{
		public abstract NodeType Type { get; }

		public abstract void Write(StringBuilder strb, int depth);
		public override sealed string ToString()
		{
			StringBuilder strb = new StringBuilder();
			Write(strb, 0);
			return strb.ToString();
		}
	}

	class ASTCommand : ASTNode
	{
		public override NodeType Type => NodeType.Command;

		public string Command { get; set; }
		public List<ASTNode> Parameter { get; set; }

		public bool Resolved { get; set; }
		public string Value { get; set; }

		public ASTCommand()
		{
			Command = string.Empty;
			Parameter = new List<ASTNode>();
		}

		public override void Write(StringBuilder strb, int depth)
		{
			strb.Append(' ', depth);
			strb.Append('!').Append(Command);
			if (Resolved)
				strb.Append(" : ").Append(Value);
			else
				strb.Append("<not executed>");
			strb.AppendLine();
			foreach (var para in Parameter)
				para.Write(strb, depth + 1);
		}
	}

	class ASTValue : ASTNode
	{
		public override NodeType Type => NodeType.Value;
		public string Value { get; set; }

		public override void Write(StringBuilder strb, int depth) => strb.Append(' ', depth).AppendLine(Value);
	}

	enum NodeType
	{
		Command,
		Value,
	}
}
