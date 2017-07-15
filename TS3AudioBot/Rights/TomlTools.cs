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

namespace TS3AudioBot.Rights
{
	using Nett;
	using System;
	using System.Linq;
	using System.Text;

	internal static class TomlTools
	{
		public static T[] GetValues<T>(TomlObject tomlObj)
		{
			if (TryGetValue(tomlObj, out T retSingleVal))
				return new T[] { retSingleVal };
			else if (tomlObj.TomlType == TomlObjectType.Array)
			{
				var tomlArray = (TomlArray)tomlObj;
				var retArr = new T[tomlArray.Length];
				for (int i = 0; i < tomlArray.Length; i++)
					if (!TryGetValue(tomlArray.Items[i], out retArr[i]))
						return null;
				return retArr;
			}
			return null;
		}

		public static bool TryGetValue<T>(TomlObject tomlObj, out T value)
		{
			switch (tomlObj.TomlType)
			{
			case TomlObjectType.Int:
				if (typeof(T) == typeof(long))
				{
					value = ((TomlValue<T>)tomlObj).Value;
					return true;
				}
				else if (typeof(T) == typeof(ulong)
					|| typeof(T) == typeof(uint) || typeof(T) == typeof(int)
					|| typeof(T) == typeof(ushort) || typeof(T) == typeof(short)
					|| typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
				{
					try
					{
						value = (T)Convert.ChangeType(((TomlInt)tomlObj).Value, typeof(T));
						return true;
					}
					catch (OverflowException) { }
				}
				break;

			case TomlObjectType.Bool:
			case TomlObjectType.Float:
			case TomlObjectType.DateTime:
			case TomlObjectType.TimeSpan:
				if (tomlObj is TomlValue<T> tomlValue && typeof(T) == tomlValue.Value.GetType())
				{
					value = tomlValue.Value;
					return true;
				}
				break;

			case TomlObjectType.String:
				if (typeof(T).IsEnum)
				{
					try
					{
						value = (T)Enum.Parse(typeof(T), ((TomlString)tomlObj).Value, true);
						return true;
					}
					catch (ArgumentException) { }
					catch (OverflowException) { }
				}
				else if (typeof(T) == typeof(string))
				{

					value = ((TomlValue<T>)tomlObj).Value;
					return true;
				}
				break;
			}
			value = default(T);
			return false;
		}

		private static string ToString(TomlObject obj)
		{
			var strb = new StringBuilder();
			//strb.Append(" : ");
			switch (obj.TomlType)
			{
			case TomlObjectType.Bool: strb.Append((obj as TomlBool).Value); break;
			case TomlObjectType.Int: strb.Append((obj as TomlInt).Value); break;
			case TomlObjectType.Float: strb.Append((obj as TomlFloat).Value); break;
			case TomlObjectType.String: strb.Append((obj as TomlString).Value); break;
			case TomlObjectType.DateTime: strb.Append((obj as TomlDateTime).Value); break;
			case TomlObjectType.TimeSpan: strb.Append((obj as TomlTimeSpan).Value); break;
			case TomlObjectType.Array:
				strb.Append("[ ")
					.Append(string.Join(", ", (obj as TomlArray).Items.Select(x => ToString(x))))
					.Append(" ]");
				break;
			case TomlObjectType.Table:
				var table = (obj as TomlTable);
				foreach (var kvp in table)
				{
					strb.Append(kvp.Key).Append(" : { ").Append(ToString(kvp.Value)).AppendLine(" }");
				}
				break;
			case TomlObjectType.ArrayOfTables:
				strb.Append("[ ")
					.Append(string.Join(", ", (obj as TomlTableArray).Items.Select(x => ToString(x))))
					.Append(" ]");
				break;
			default:
				break;
			}
			return strb.ToString();
		}
	}
}
