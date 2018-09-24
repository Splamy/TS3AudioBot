// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem
{
	using Ast;
	using System;
	using System.Collections.Generic;
	using System.Text;

	internal static class CommandParser
	{
		public const char DefaultCommandChar = '!';
		public const char DefaultDelimeterChar = ' ';

		// This switch follows more or less a DEA to this EBNF
		// COMMAND-EBNF := COMMAND

		// COMMAND      := (!ARGUMENT \s+ (ARGUMENT|\s)*)
		// ARGUMENT     := COMMAND|FREESTRING|QUOTESTRING
		// FREESTRING   := [^)]+
		// QUOTESTRING  := "[<anything but ", \" is ok>]+"

		public static AstNode ParseCommandRequest(string request, char commandChar = DefaultCommandChar, char delimeterChar = DefaultDelimeterChar)
		{
			AstCommand root = null;
			var comAst = new Stack<AstCommand>();
			var build = BuildStatus.ParseCommand;
			var strb = new StringBuilder();
			var strPtr = new StringPtr(request);

			while (!strPtr.End)
			{
				AstCommand buildCom;
				switch (build)
				{
				case BuildStatus.ParseCommand:
					// Got a command
					buildCom = new AstCommand();
					// Consume CommandChar if left over
					if (strPtr.Char == commandChar)
						strPtr.Next(commandChar);

					if (root is null) root = buildCom;
					else comAst.Peek().Parameter.Add(buildCom);
					comAst.Push(buildCom);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.SelectParam:
					strPtr.SkipChar(delimeterChar);

					if (strPtr.End)
					{
						build = BuildStatus.End;
					}
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
							{
								build = BuildStatus.ParseFreeString;
							}
							else if (strPtr.IsNext(commandChar))
							{
								strPtr.Next('(');
								build = BuildStatus.ParseCommand;
							}
							else
							{
								build = BuildStatus.ParseFreeString;
							}
							break;

						case ')':
							if (comAst.Count <= 0)
							{
								build = BuildStatus.End;
							}
							else
							{
								comAst.Pop();
								if (comAst.Count <= 0)
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
					var valFreeAst = new AstValue();
					using (strPtr.TrackNode(valFreeAst))
					{
						for (; !strPtr.End; strPtr.Next())
						{
							if ((strPtr.Char == '(' && strPtr.HasNext && strPtr.IsNext(commandChar))
								|| strPtr.Char == ')'
								|| strPtr.Char == delimeterChar)
							{
								break;
							}
						}
					}
					valFreeAst.BuildValue();
					buildCom = comAst.Peek();
					buildCom.Parameter.Add(valFreeAst);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.ParseQuotedString:
					strb.Clear();

					strPtr.Next('"');

					var valQuoAst = new AstValue();
					using (strPtr.TrackNode(valQuoAst))
					{
						bool escaped = false;
						for (; !strPtr.End; strPtr.Next())
						{
							if (strPtr.Char == '\\')
							{
								escaped = true;
							}
							else if (strPtr.Char == '"')
							{
								if (escaped) { strb.Length--; }
								else { strPtr.Next(); break; }
								escaped = false;
							}
							else
							{
								escaped = false;
							}

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

		private class StringPtr
		{
			private readonly string text;
			private int index;
			private AstNode astnode;
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

			public void SkipChar(char c = ' ')
			{
				while (index < text.Length && text[index] == c)
					index++;
			}

			public void JumpToEnd() => index = text.Length + 1;

			public NodeTracker TrackNode(AstNode node)
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
				return curTrack = new NodeTracker(this);
			}

			private void UntrackNode()
			{
				curTrack = null;
				astnode = null;
			}

			public class NodeTracker : IDisposable
			{
				private readonly StringPtr parent;
				public NodeTracker(StringPtr p) { parent = p; }
				public void Dispose() => parent.UntrackNode();
			}
		}

		private enum BuildStatus
		{
			ParseCommand,
			SelectParam,
			ParseFreeString,
			ParseQuotedString,
			End,
		}
	}
}
