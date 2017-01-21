// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	class CommandParser
	{
		const char DefaultCommandChar = '!';
		const char DefaultDelimeterChar = ' ';

		// This switch follows more or less a DEA to this EBNF
		// COMMAND-EBNF := COMMAND

		// COMMAND      := (!ARGUMENT \s+ (ARGUMENT|\s)*)
		// ARGUMENT     := COMMAND|FREESTRING|QUOTESTRING
		// FREESTRING   := [^)]+
		// QUOTESTRING  := "[<anything but ", \" is ok>]+"

		public static ASTNode ParseCommandRequest(string request, char commandChar = DefaultCommandChar, char delimeterChar = DefaultDelimeterChar)
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
					if (strPtr.Char == commandChar)
						strPtr.Next(commandChar);

					if (root == null) root = buildCom;
					else comAst.Peek().Parameter.Add(buildCom);
					comAst.Push(buildCom);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.SelectParam:
					strPtr.SkipChar(delimeterChar);

					if (strPtr.End)
						build = BuildStatus.End;
					else
					{
						switch (strPtr.Char)
						{
						case '"':
							build = BuildStatus.ParseQuotedString;
							//goto case BuildStatus.ParseQuotedString;
							break;
						case '(':
							if (!strPtr.HasNext)
								build = BuildStatus.ParseFreeString;
							else if (strPtr.IsNext(commandChar))
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
							if ((strPtr.Char == '(' && strPtr.HasNext && strPtr.IsNext(commandChar))
								|| strPtr.Char == ')'
								|| strPtr.Char == delimeterChar)
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

			public void SkipChar(char c = ' ')
			{
				while (index < text.Length && text[index] == c)
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
