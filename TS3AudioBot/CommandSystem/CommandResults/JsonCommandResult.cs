namespace TS3AudioBot.CommandSystem
{
	using System.Web.Script.Serialization;

	public class JsonCommandResult : ICommandResult
	{
		public override CommandResultType ResultType => CommandResultType.Json;

		public JsonObject JsonObject { get; }

		public JsonCommandResult(JsonObject jsonObj)
		{
			JsonObject = jsonObj;
		}
	}

	public class JsonObject
	{
		[ScriptIgnore]
		public bool Ok { get; }
		[ScriptIgnore]
		public string AsStringResult { get; }

		protected JsonObject(string stringResult) : this(stringResult, true) { }

		private JsonObject(string stringResult, bool ok)
		{
			AsStringResult = stringResult;
			Ok = ok;
		}

		public static JsonObject Error(string message) => new JsonObject(message, false);

		public override string ToString() => AsStringResult;
	}
}
