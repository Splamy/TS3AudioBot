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

namespace TS3Client
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	static class Extensions
	{
		public static string GetQueryString(this Enum valueEnum)
		{
			if (valueEnum == null) throw new ArgumentNullException(nameof(valueEnum));

			Type enumType = valueEnum.GetType();
			var valueField = enumType.GetField(Enum.GetName(enumType, valueEnum));
			var fieldAttributes = valueField.GetCustomAttributes(typeof(TS3SerializableAttribute), false);
			var fieldAttribute = fieldAttributes.Cast<TS3SerializableAttribute>().FirstOrDefault();
			if (fieldAttribute == null)
				throw new InvalidOperationException("This enum doesn't contain the QueryString attribute");
			return fieldAttribute.QueryString;
		}

		public static IEnumerable<Enum> GetFlags(this Enum input) => Enum.GetValues(input.GetType()).Cast<Enum>().Where(enu => input.HasFlag(enu));
	}
}
