namespace TS3AudioBot.CommandSystem
{
	using System.Text;

	class ASTValue : ASTNode
	{
		public override ASTType Type => ASTType.Value;
		public string Value { get; set; }

		public override void Write(StringBuilder strb, int depth) => strb.Space(depth).Append(Value);
	}
}
