namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	class CommandParser
	{
		const char CommandChar = '!';

		// This switch follows more or less a DEA to this EBNF
		// COMMAND-EBNF := COMMAND

		// COMMAND      := (!NAME \s+ (COMMAND|FREESTRING|QUOTESTRING|\s)*)
		// NAME         := [a-z]+
		// FREESTRING   := [^()]+
		// QUOTESTRING  := "[<anything but '\"'>]+"

		public static ASTNode ParseCommandRequest(string request)
		{
			ASTCommand root = null;
			Stack<ASTCommand> comAst = new Stack<ASTCommand>();
			BuildStatus build = BuildStatus.ParseCommand;
			StringBuilder strb = new StringBuilder();
			StringPtr strPtr = new StringPtr(request);

			while (!strPtr.End)
			{
				switch (build)
				{
				case BuildStatus.ParseCommand:
					strPtr.SkipSpace();

					ASTCommand buildCom = new ASTCommand();
					using (strPtr.TrackNode(buildCom))
					{
						if (strPtr.Char == '(') strPtr.Next();
						strPtr.Next(CommandChar);

						if (root == null) root = buildCom;
						else comAst.Peek().Parameter.Add(buildCom);
						comAst.Push(buildCom);

						strb.Clear();
						for (; !strPtr.End; strPtr.Next())
						{
							if (strPtr.Char >= 'a' && strPtr.Char <= 'z')
								strb.Append(strPtr.Char);
							else if (char.IsWhiteSpace(strPtr.Char) || strPtr.Char == '(' || strPtr.Char == ')' || strPtr.Char == '"')
								break;
							else
								return new ASTError(buildCom, "The command can only contain lowercase letters a-z.");
						}
						buildCom.Command = strb.ToString();
						if (string.IsNullOrWhiteSpace(buildCom.Command))
							return new ASTError(buildCom, "A command must have a name");
					}
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.SelectParam:
					strPtr.SkipSpace();

					if (strPtr.End)
						build = BuildStatus.End;
					else
					{
						if (strPtr.Char == '"')
							build = BuildStatus.ParseQuotedString;
						else if (strPtr.Char == '(')
						{
							if (!strPtr.HasNext)
								build = BuildStatus.ParseFreeString;
							else if (strPtr.IsNext('!'))
								build = BuildStatus.ParseCommand;
							else
								build = BuildStatus.ParseFreeString;
						}
						else if (strPtr.Char == ')')
						{
							if (!comAst.Any())
								build = BuildStatus.End;
							else
							{
								comAst.Pop();
								if (!comAst.Any())
									build = BuildStatus.End;
							}
							strPtr.Next();
						}
						else
							build = BuildStatus.ParseFreeString;
					}
					break;

				case BuildStatus.ParseFreeString:
					strb.Clear();

					var valFreeAst = new ASTValue();
					using (strPtr.TrackNode(valFreeAst))
					{
						for (; !strPtr.End; strPtr.Next())
						{
							if ((strPtr.Char == '(' && strPtr.HasNext && strPtr.IsNext('!'))
								|| strPtr.Char == ')'
								|| char.IsWhiteSpace(strPtr.Char))
								break;
							strb.Append(strPtr.Char);
						}
					}
					valFreeAst.Value = strb.ToString();
					buildCom = comAst.Peek();
					buildCom.Parameter.Add(valFreeAst);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.ParseQuotedString:
					strb.Clear();

					strPtr.Next('"');

					var valQuoAst = new ASTValue();
					using (strPtr.TrackNode(valQuoAst))
					{
						bool escaped = false;
						for (; !strPtr.End; strPtr.Next())
						{
							if (strPtr.Char == '\\') escaped = true;
							else if (strPtr.Char == '"')
							{
								if (escaped) strb.Length--;
								else { strPtr.Next(); break; }
								escaped = false;
							}
							else escaped = false;
							strb.Append(strPtr.Char);
						}
					}
					valQuoAst.Value = strb.ToString();
					buildCom = comAst.Peek();
					buildCom.Parameter.Add(valQuoAst);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.End:
					strPtr.JumpToEnd();
					break;

				default: throw new InvalidOperationException();
				}
			}

			return root;
		}

		private static bool ValidateChar(ref int i, string text, char c)
		{
			if (i >= text.Length)
				return false;
			bool ok = text[i] == c;
			if (ok) i++;
			return ok;
		}

		private class StringPtr
		{
			private string text;
			private int index;
			private ASTNode astnode;
			private NodeTracker curTrack;

			public char Char => text[index];
			public bool End => index >= text.Length;
			public bool HasNext => index + 1 < text.Length;

			public StringPtr(string str)
			{
				text = str;
				index = 0;
			}

			public void Next()
			{
				index++;
				if (astnode != null)
					astnode.Length++;
			}

			public void Next(char mustBe)
			{
				if (Char != mustBe)
					throw new InvalidOperationException();
				Next();
			}

			public bool IsNext(char what) => text[index + 1] == what;

			public void SkipSpace()
			{
				while (index < text.Length && char.IsWhiteSpace(text[index]))
					index++;
			}

			public void JumpToEnd() => index = text.Length + 1;

			public NodeTracker TrackNode(ASTNode node)
			{
				if (curTrack != null)
					throw new InvalidOperationException("Previous tracker must be freed");

				astnode = node;
				if (node != null)
				{
					astnode.FullRequest = text;
					astnode.Position = index;
					astnode.Length = 0;
				}
				return (curTrack = new NodeTracker(this));
			}
			private void UntrackNode()
			{
				curTrack = null;
				astnode = null;
			}

			public class NodeTracker : IDisposable
			{
				private StringPtr parent;
				public NodeTracker(StringPtr p) { parent = p; }
				public void Dispose() => parent.UntrackNode();
			}
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

	abstract class ASTNode
	{
		public abstract NodeType Type { get; }

		public string FullRequest { get; set; }
		public int Position { get; set; }
		public int Length { get; set; }

		protected const int SpacePerTab = 2;
		protected StringBuilder Space(StringBuilder strb, int depth) => strb.Append(' ', depth * SpacePerTab);
		public abstract void Write(StringBuilder strb, int depth);
		public override sealed string ToString()
		{
			StringBuilder strb = new StringBuilder();
			Write(strb, 0);
			return strb.ToString();
		}
	}

	class ASTError : ASTNode
	{
		public override NodeType Type => NodeType.Error;

		public string Description { get; }

		public ASTError(ASTNode referenceNode, string description)
		{
			FullRequest = referenceNode.FullRequest;
			Position = referenceNode.Position;
			Length = referenceNode.Length;
			Description = description;
		}

		public ASTError(string request, int pos, int len, string description)
		{
			FullRequest = request;
			Position = pos;
			Length = len;
			Description = description;
		}

		public override void Write(StringBuilder strb, int depth)
		{
			strb.AppendLine(FullRequest);
			if (Position == 1) strb.Append('.');
			else if (Position > 1) strb.Append(' ', Position);
			strb.Append('~', Length).Append('^').AppendLine();
			strb.Append("Error: ").AppendLine(Description);
		}
	}

	class ASTCommand : ASTNode
	{
		public override NodeType Type => NodeType.Command;

		public string Command { get; set; }
		public List<ASTNode> Parameter { get; set; }

		public BotCommand BotCommand { get; set; }
		public string Value { get; set; }

		public ASTCommand()
		{
			Parameter = new List<ASTNode>();
		}

		public override void Write(StringBuilder strb, int depth)
		{
			Space(strb, depth).Append('!').Append(Command);
			strb.Append(" : ").Append(Value ?? string.Empty);
			strb.AppendLine();
			foreach (var para in Parameter)
				para.Write(strb, depth + 1);
		}
	}

	class ASTValue : ASTNode
	{
		public override NodeType Type => NodeType.Value;
		public string Value { get; set; }

		public override void Write(StringBuilder strb, int depth) => Space(strb, depth).AppendLine(Value);
	}

	enum NodeType
	{
		Command,
		Value,
		Error,
	}
}
