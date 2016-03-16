namespace TS3AudioBot.CommandSystem
{
	using System.Collections.Generic;
	using System.Text;

	class ASTCommand : ASTNode
	{
		public override ASTType Type => ASTType.Command;

		public List<ASTNode> Parameter { get; set; }

		public ASTCommand()
		{
			Parameter = new List<ASTNode>();
		}

		public override void Write(StringBuilder strb, int depth)
		{
			strb.Space(depth);
			if (Parameter.Count == 0)
			{
				strb.Append("<Invalid empty command>");
			}
			else
			{
				ASTValue comName = Parameter[0] as ASTValue;
				if (comName == null)
					strb.Append("<Invalid command name>");
				else
					strb.Append("!").Append(comName.Value);

				for (int i = 1; i < Parameter.Count; i++)
				{
					strb.AppendLine();
					Parameter[i].Write(strb, depth + 1);
				}
			}
		}
	}
}
