// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Text;
using TS3AudioBot.CommandSystem.Ast;

namespace TS3AudioBot.CommandSystem
{
	internal static class CommandParser
	{
		public const char DefaultCommandChar = '!';
		public const char DefaultDelimeterChar = ' ';

		// This switch follows more or less a DEA to this EBNF
		// COMMAND-EBNF := <COMMAND> | $.*^

		// COMMAND      := '!' <ARGUMENT> (\s+ <ARGUMENT>)*
		// ARGUMENT     := '(' <COMMAND> ')'? | <FREESTRING> | <QUOTESTRING>
		// FREESTRING   := [^)]+
		// QUOTESTRING  := '"' [<anything but ", \" is ok>]* '"'

		public static AstNode ParseCommandRequest(string request, char commandChar = DefaultCommandChar, char delimeterChar = DefaultDelimeterChar)
		{
			AstCommand root = null;
			var comAst = new Stack<AstCommand>();
			var build = BuildStatus.ParseCommand;
			var strb = new StringBuilder();
			var strPtr = new StringPtr(request);

			var startTrim = request.AsSpan().TrimStart();
			if (startTrim.IsEmpty || startTrim[0] != commandChar)
			{
				return new AstValue
				{
					FullRequest = request,
					Length = request.Length,
					Position = 0,
					Value = request,
					StringType = StringType.FreeString,
				};
			}

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
						case '\'':
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
								buildCom = comAst.Pop();
								foreach (var param in buildCom.Parameter)
									if (param is AstValue astVal)
										astVal.TailLength = strPtr.Index - param.Position;
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
					var valFreeAst = new AstValue() { FullRequest = request, StringType = StringType.FreeString };
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
					buildCom = comAst.Peek();
					buildCom.Parameter.Add(valFreeAst);
					build = BuildStatus.SelectParam;
					break;

				case BuildStatus.ParseQuotedString:
					strb.Clear();

					char quoteChar;
					if (strPtr.TryNext('"'))
						quoteChar = '"';
					else if (strPtr.TryNext('\''))
						quoteChar = '\'';
					else
						throw new Exception("Parser error");

					var valQuoAst = new AstValue() { FullRequest = request, StringType = StringType.QuotedString };
					using (strPtr.TrackNode(valQuoAst))
					{
						bool escaped = false;
						for (; !strPtr.End; strPtr.Next())
						{
							if (strPtr.Char == '\\')
							{
								escaped = true;
							}
							else if (strPtr.Char == quoteChar)
							{
								if (!escaped)
								{
									strPtr.Next();
									break;
								}
								else
								{
									strb.Length--;
									escaped = false;
								}
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

			public char Char => text[Index];
			public bool End => Index >= text.Length;
			public bool HasNext => Index + 1 < text.Length;
			public int Index { get; private set; }

			public StringPtr(string str)
			{
				text = str;
				Index = 0;
			}

			public void Next()
			{
				Index++;
			}

			public void Next(char mustBe)
			{
				if (Char != mustBe)
					throw new InvalidOperationException();
				Next();
			}

			public bool TryNext(char mustBe)
			{
				if (Char != mustBe)
					return false;
				Next();
				return true;
			}

			public bool IsNext(char what) => HasNext && text[Index + 1] == what;

			public void SkipChar(char c = ' ')
			{
				while (Index < text.Length && text[Index] == c)
					Index++;
			}

			public void JumpToEnd() => Index = text.Length + 1;

			public NodeTracker TrackNode(AstNode node = null)
			{
				return new NodeTracker(this, node);
			}

			public struct NodeTracker : IDisposable
			{
				private readonly int indexStart;
				private readonly StringPtr parent;
				private readonly AstNode node;
				public NodeTracker(StringPtr p, AstNode node = null)
				{
					parent = p;
					indexStart = parent.Index;
					this.node = node;
				}

				public void Apply(AstNode node)
				{
					node.Position = indexStart;
					node.Length = parent.Index - indexStart;
				}

				public (int start, int end) Done() => (indexStart, parent.Index);

				public void Dispose() { if (node != null) Apply(node); }
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
