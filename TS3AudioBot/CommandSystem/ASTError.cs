namespace TS3AudioBot.CommandSystem
{
	using System.Text;

	class ASTError : ASTNode
	{
		public override ASTType Type => ASTType.Error;

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
}
