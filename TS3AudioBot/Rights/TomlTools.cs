// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

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
				return new[] { retSingleVal };
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
			case TomlObjectType.Bool: strb.Append(((TomlBool) obj).Value); break;
			case TomlObjectType.Int: strb.Append(((TomlInt) obj).Value); break;
			case TomlObjectType.Float: strb.Append(((TomlFloat) obj).Value); break;
			case TomlObjectType.String: strb.Append(((TomlString) obj).Value); break;
			case TomlObjectType.DateTime: strb.Append(((TomlDateTime) obj).Value); break;
			case TomlObjectType.TimeSpan: strb.Append(((TomlTimeSpan) obj).Value); break;
			case TomlObjectType.Array:
				strb.Append("[ ")
					.Append(string.Join(", ", ((TomlArray)obj).Items.Select(ToString)))
					.Append(" ]");
				break;
			case TomlObjectType.Table:
				foreach (var kvp in (TomlTable)obj)
					strb.Append(kvp.Key).Append(" : { ").Append(ToString(kvp.Value)).AppendLine(" }");
				break;
			case TomlObjectType.ArrayOfTables:
				strb.Append("[ ")
					.Append(string.Join(", ", ((TomlTableArray)obj).Items.Select(ToString)))
					.Append(" ]");
				break;
			}
			return strb.ToString();
		}
	}
}
