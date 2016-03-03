namespace TS3AudioBot.CommandSystem
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

		// COMMAND      := (!ARGUMENT \s+ (ARGUMENT|\s)*)
		// ARGUMENT     := COMMAND|FREESTRING|QUOTESTRING
		// FREESTRING   := [^)]+
		// QUOTESTRING  := "[<anything but ", \" is ok>]+"

		public static ASTNode ParseCommandRequest(string request)
		{
			ASTCommand root = null;
			var comAst = new Stack<ASTCommand>();
			BuildStatus build = BuildStatus.ParseCommand;
			var strb = new StringBuilder();
			var strPtr = new StringPtr(request);

			while (!strPtr.End)
			{
				ASTCommand buildCom;
				switch (build)
				{
				case BuildStatus.ParseCommand:
					// Got a command
					buildCom = new ASTCommand();
					// Consume CommandChar if left over
					if (strPtr.Char == CommandChar)
						strPtr.Next(CommandChar);

					if (root == null) root = buildCom;
					else comAst.Peek().Parameter.Add(buildCom);
					comAst.Push(buildCom);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.SelectParam:
					strPtr.SkipSpace();

					if (strPtr.End)
						build = BuildStatus.End;
					else
					{
						switch (strPtr.Char)
						{
						case '"':
							build = BuildStatus.ParseQuotedString;
							break;
						case '(':
							if (!strPtr.HasNext)
								build = BuildStatus.ParseFreeString;
							else if (strPtr.IsNext(CommandChar))
							{
								strPtr.Next('(');
								build = BuildStatus.ParseCommand;
							}
							else
								build = BuildStatus.ParseFreeString;
							break;
						case ')':
							if (!comAst.Any())
								build = BuildStatus.End;
							else
							{
								comAst.Pop();
								if (!comAst.Any())
									build = BuildStatus.End;
							}
							strPtr.Next();
							break;
						default:
							build = BuildStatus.ParseFreeString;
							break;
						}
					}
					break;

				case BuildStatus.ParseFreeString:
					strb.Clear();

					var valFreeAst = new ASTValue();
					using (strPtr.TrackNode(valFreeAst))
					{
						for (; !strPtr.End; strPtr.Next())
						{
							if ((strPtr.Char == '(' && strPtr.HasNext && strPtr.IsNext(CommandChar))
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

		static bool ValidateChar(ref int i, string text, char c)
		{
			if (i >= text.Length)
				return false;
			bool ok = text[i] == c;
			if (ok) i++;
			return ok;
		}

		class StringPtr
		{
			string text;
			int index;
			ASTNode astnode;
			NodeTracker curTrack;

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

			void UntrackNode()
			{
				curTrack = null;
				astnode = null;
			}

			public class NodeTracker : IDisposable
			{
				readonly StringPtr parent;
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
}
