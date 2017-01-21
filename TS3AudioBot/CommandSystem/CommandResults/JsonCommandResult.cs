// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

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

	public abstract class JsonObject
	{
		[ScriptIgnore]
		public string AsStringResult { get; }

		protected JsonObject(string stringResult)
		{
			AsStringResult = stringResult;
		}

		public override string ToString() => AsStringResult;
	}

	public class JsonSingleObj<T> : JsonObject
	{
		public T Value { get; }

		public JsonSingleObj(string msg, T value) : base(msg)
		{
			Value = value;
		}
	}
}
