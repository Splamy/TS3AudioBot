namespace TS3AudioBot.CommandSystem
{
	using System.Text;

	abstract class ASTNode
	{
		public abstract ASTType Type { get; }

		public string FullRequest { get; set; }
		public int Position { get; set; }
		public int Length { get; set; }

		public abstract void Write(StringBuilder strb, int depth);
		public override sealed string ToString()
		{
			var strb = new StringBuilder();
			Write(strb, 0);
			return strb.ToString();
		}
	}

	static class ASTNodeExtensions
	{
		public const int SpacePerTab = 2;
		public static StringBuilder Space(this StringBuilder strb, int depth) => strb.Append(' ', depth * SpacePerTab);
	}
}
