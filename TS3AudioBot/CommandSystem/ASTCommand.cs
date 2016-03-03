namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Text;

	class ASTCommand : ASTNode
	{
		public override ASTType Type => ASTType.Command;

		public List<ASTNode> Parameter { get; set; }

		public BotCommand BotCommand { get; set; }
		public string Value { get; set; }

		public ASTCommand()
		{
			Parameter = new List<ASTNode>();
		}

		public override void Write(StringBuilder strb, int depth)
		{
			Space(strb, depth).Append(": ").Append(Value ?? string.Empty);
			strb.AppendLine();
			foreach (var para in Parameter)
				para.Write(strb, depth + 1);
		}
	}
}
