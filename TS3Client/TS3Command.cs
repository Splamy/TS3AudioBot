namespace TS3Client
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;

	public class TS3Command
	{
		private static readonly Regex CommandMatch = new Regex(@"[a-z0-9_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ECMAScript);
		public static readonly CommandParameter[] NoParameter = new CommandParameter[0];
		public static readonly CommandOption[] NoOptions = new CommandOption[0];

		private string command;
		private CommandParameter[] parameter;
		private CommandOption[] options;

		public TS3Command(string command) :
			this(command, NoParameter)
		{ }

		public TS3Command(string command, params CommandParameter[] parameter) :
			this(command, parameter, NoOptions)
		{ }

		public TS3Command(string command, CommandParameter[] parameter, params CommandOption[] options)
		{
			this.command = command;
			this.parameter = parameter;
			this.options = options;
		}


		public override string ToString() => BuildToString(command, parameter, options);

		public static string BuildToString(string command, CommandParameter[] parameter, CommandOption[] options)
		{
			if (string.IsNullOrWhiteSpace(command))
				throw new ArgumentNullException(nameof(command));
			if (!CommandMatch.IsMatch(command))
				throw new ArgumentException("Invalid command characters", nameof(command));

			StringBuilder strb = new StringBuilder(TS3String.Escape(command));

			foreach (var param in parameter)
				strb.Append(' ').Append(param.QueryString);

			foreach (var option in options)
				strb.Append(option.Value);

			return strb.ToString();
		}
	}
}
