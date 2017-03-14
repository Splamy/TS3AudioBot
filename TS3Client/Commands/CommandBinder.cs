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

namespace TS3Client.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;

	public class CommandBinder : CommandParameter
	{
		private static readonly Dictionary<Type, ConstructorInfo> ConstrBuffer = new Dictionary<Type, ConstructorInfo>();
		private static ConstructorInfo GetValueCtor(Type t)
		{
			ConstructorInfo ci;
			if (!ConstrBuffer.TryGetValue(t, out ci))
			{
				var ctor = typeof(ParameterConverter).GetConstructors().Where(c => c.GetParameters().First().ParameterType == t).FirstOrDefault();
				if (ctor == null)
					throw new InvalidCastException();
				ci = ctor;
				ConstrBuffer.Add(t, ci);
			}
			return ci;
		}
		private readonly List<string> buildList = new List<string>();
		public override string QueryString => string.Join(" ", buildList);

		public static CommandBinder NewBind<T>(string key, IEnumerable<T> parameter) => new CommandBinder().Bind(key, parameter);
		public CommandBinder Bind<T>(string key, IEnumerable<T> parameter)
		{
			var ctor = GetValueCtor(typeof(T));
			var values = parameter.Select(val => (ParameterConverter)ctor.Invoke(new object[] { val }));
			var result = string.Join("|", values.Select(v => new CommandParameter(key, v).QueryString));
			buildList.Add(result);
			return this;
		}

		public static CommandBinder NewBind(string key, IEnumerable<CommandParameter> parameter) => new CommandBinder().Bind(key, parameter);
		public CommandBinder Bind<T>(string key, IEnumerable<CommandParameter> parameter)
		{
			throw new NotImplementedException();
			//buildList.Add(result);
			//return this;
		}
	}
}
