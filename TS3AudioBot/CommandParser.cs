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

		public static ASTCommand ParseCommandRequest(string request)
		{
			Stack<ASTNode> comAst = new Stack<ASTNode>();
			BuildStatus build = BuildStatus.Init;

			for (int i = 0; i < request.Length; i++)
			{
				char c = request[i];
				switch (build)
				{
				case BuildStatus.Init:
					ASTCommand buildCom = null;
					if (c == CommandChar)
					{
						buildCom = new ASTCommand();
						comAst.Push(buildCom);
						build = BuildStatus.GetCommand;
					}
					break;

				case BuildStatus.GetCommand:
					StringBuilder strb = new StringBuilder();
					for (; i < request.Length; i++)
					{
						c = request[i];
						if (c >= 'a' && c <= 'z')
							strb.Append(c);
						else if (char.IsWhiteSpace(c))
							break;
					}
					ASTNode node = comAst.Peek();
					if (node.Type != NodeType.Command)
						throw new InvalidOperationException();
					((ASTCommand)node).Command = strb.ToString();
					build = BuildStatus.SkipSpace;
					break;

				case BuildStatus.SkipSpace:
					while (i < request.Length && char.IsWhiteSpace(request[i])) i++;
					build = BuildStatus.ParseParam;
					break;

				case BuildStatus.ParseParam:
					node = comAst.Peek();
					if (node.Type != NodeType.Command)
						throw new InvalidOperationException();
					ASTCommand comCur = (ASTCommand)node;

					strb = new StringBuilder();
					while (true)
					{
						if (i >= request.Length)
						{
							comCur.Parameter.Add(new ASTValue() { Value = strb.ToString() });
							break;
						}
						else if (c != '(')
						{
							strb.Append(c);
						}
						else
						{
							if (i + 1 >= request.Length)
								break;

							if (request[i + 1] != '!')
								continue;

							comCur.Parameter.Add(new ASTValue() { Value = strb.ToString() });
							buildCom = new ASTCommand();
							comCur.Parameter.Add(buildCom);
							comAst.Push(buildCom);
							build = BuildStatus.GetCommand;
							break;
						}

						i++;
					}

					break;

				case BuildStatus.End:
					i = request.Length + 1;
					break;

				default: throw new InvalidOperationException();
				}
			}

			ASTNode lowCom = null;
			while (comAst.Any())
				comAst.Pop();
			if (lowCom == null || lowCom.Type != NodeType.Command)
				return null;
			return (ASTCommand)lowCom;
		}

		enum BuildStatus
		{
			Init,
			GetCommand,
			SkipSpace,
			ParseParam,
			GetStringValue,
			End,
		}
	}

	abstract class ASTNode
	{
		public abstract NodeType Type { get; }
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
	}

	class ASTValue : ASTNode
	{
		public override NodeType Type => NodeType.Value;
		public string Value { get; set; }
	}

	enum NodeType
	{
		Command,
		Value,
	}
}
