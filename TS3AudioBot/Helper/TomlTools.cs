// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Nett;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TS3AudioBot.Helper
{
	public static class TomlTools
	{
		// *** Convenience method for getting values out of a toml object. ***

		public static T[] TryGetValueArray<T>(this TomlObject tomlObj)
		{
			if (tomlObj.TomlType == TomlObjectType.Array)
			{
				var tomlArray = (TomlArray)tomlObj;
				var retArr = new T[tomlArray.Length];
				for (int i = 0; i < tomlArray.Length; i++)
				{
					if (!tomlArray.Items[i].TryGetValue(out retArr[i]))
					{
						return null;
					}
				}
				return retArr;
			}
			else if (tomlObj.TryGetValue(out T retSingleVal))
			{
				return new[] { retSingleVal };
			}
			return null;
		}

		public static bool TryGetValue<T>(this TomlObject tomlObj, out T value)
		{
			switch (tomlObj.TomlType)
			{
			case TomlObjectType.Int:
				if (typeof(T) == typeof(long))
				{
					// The base storage type for TomlInt is long, so we can simply return it.
					value = (T)(object)((TomlInt)tomlObj).Value;
					return true;
				}
				else if (typeof(T) == typeof(ulong))
				{
					// ulong is the only type which needs to be casted so we can use it.
					// This might not be the greatest solution, but we can express ulong.MaxValue with -1 for example.
					value = (T)(object)(ulong)((TomlInt)tomlObj).Value;
					return true;
				}
				else if (typeof(T) == typeof(uint) || typeof(T) == typeof(int)
					|| typeof(T) == typeof(ushort) || typeof(T) == typeof(short)
					|| typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte)
					|| typeof(T) == typeof(float) || typeof(T) == typeof(double))
				{
					// All other types will be converted to catch overflow issues.
					try
					{
						value = (T)Convert.ChangeType(((TomlInt)tomlObj).Value, typeof(T));
						return true;
					}
					catch (OverflowException) { }
				}
				break;

			case TomlObjectType.Float:
				if (typeof(T) == typeof(double))
				{
					// Same here, double is the base type for TomlFloat.
					value = (T)(object)((TomlFloat)tomlObj).Value;
					return true;
				}
				else if (typeof(T) == typeof(float))
				{
					// double -> float cast works as we expect it.
					value = (T)(object)(float)((TomlFloat)tomlObj).Value;
					return true;
				}
				break;

			case TomlObjectType.Bool:
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
				else if (typeof(T) == typeof(TimeSpan))
				{
					try
					{
						value = (T)(object)TextUtil.ParseTime(((TomlString)tomlObj).Value);
						return true;
					}
					catch (FormatException) { }
				}
				break;
			}
			value = default;
			return false;
		}

		public static string SerializeTime(TimeSpan time)
		{
			if (time.TotalMilliseconds < 1)
				return "0s";
			var strb = new StringBuilder();
			if (time.TotalDays >= 1)
			{
				strb.Append(time.TotalDays.ToString("F0")).Append('d');
				time -= TimeSpan.FromDays(time.Days);
			}
			if (time.Hours > 0)
				strb.Append(time.Hours).Append("h");
			if (time.Minutes > 0)
				strb.Append(time.Minutes).Append("m");
			if (time.Seconds > 0)
				strb.Append(time.Seconds).Append("s");
			if (time.Milliseconds > 0)
				strb.Append(time.Milliseconds).Append("ms");
			return strb.ToString();
		}

		// *** Convenience method for setting values to a toml object. ***

		public static TomlObject Set<T>(this TomlTable tomlTable, string key, T value)
		{
			if (tomlTable is null) throw new ArgumentNullException(nameof(tomlTable));
			if (key is null) throw new ArgumentNullException(nameof(key));

			// I literally have no idea how to write it better with this toml library.

			// Note for TimeSpan: since TimeSpan as Nett (de)serializes it is not standartized we have to cast it manually

			TomlObject retobj = tomlTable.TryGetValue(key);
			if (retobj is null)
			{
				if (typeof(T) == typeof(bool)) return tomlTable.Add(key, (bool)(object)value).Added;
				else if (typeof(T) == typeof(string)) return tomlTable.Add(key, (string)(object)value).Added;
				else if (typeof(T) == typeof(double)) return tomlTable.Add(key, (double)(object)value).Added;
				else if (typeof(T) == typeof(float)) return tomlTable.Add(key, (float)(object)value).Added;
				else if (typeof(T) == typeof(ushort)) return tomlTable.Add(key, /*auto*/(ushort)(object)value).Added;
				else if (typeof(T) == typeof(int)) return tomlTable.Add(key, (int)(object)value).Added;
				else if (typeof(T) == typeof(long)) return tomlTable.Add(key, (long)(object)value).Added;
				else if (typeof(T) == typeof(ulong)) return tomlTable.Add(key, (long)(ulong)(object)value).Added;
				else if (typeof(T) == typeof(TimeSpan)) return tomlTable.Add(key, SerializeTime((TimeSpan)(object)value)).Added;
				else if (typeof(T) == typeof(DateTime)) return tomlTable.Add(key, (DateTime)(object)value).Added;
				else if (typeof(T).IsEnum) return tomlTable.Add(key, value.ToString()).Added;
				else if (value is IEnumerable<bool> enubool) return tomlTable.Add(key, enubool).Added;
				else if (value is IEnumerable<string> enustring) return tomlTable.Add(key, enustring).Added;
				else if (value is IEnumerable<double> enudouble) return tomlTable.Add(key, enudouble).Added;
				else if (value is IEnumerable<float> enufloat) return tomlTable.Add(key, enufloat).Added;
				else if (value is IEnumerable<ushort> enuushort) return tomlTable.Add(key, enuushort.Select(x => (int)x)).Added;
				else if (value is IEnumerable<int> enuint) return tomlTable.Add(key, enuint).Added;
				else if (value is IEnumerable<long> enulong) return tomlTable.Add(key, enulong).Added;
				else if (value is IEnumerable<ulong> enuulong) return tomlTable.Add(key, enuulong.Select(x => (long)x)).Added;
				else if (value is IEnumerable<TimeSpan> enuTimeSpan) return tomlTable.Add(key, enuTimeSpan.Select(SerializeTime)).Added;
				else if (value is IEnumerable<DateTime> enuDateTime) return tomlTable.Add(key, enuDateTime).Added;
			}
			else
			{
				TomlComment[] docs = null;
				if (retobj.Comments.Any())
					docs = retobj.Comments.ToArray();
				if (typeof(T) == typeof(bool)) retobj = tomlTable.Update(key, (bool)(object)value).Added;
				else if (typeof(T) == typeof(string)) retobj = tomlTable.Update(key, (string)(object)value).Added;
				else if (typeof(T) == typeof(double)) retobj = tomlTable.Update(key, (double)(object)value).Added;
				else if (typeof(T) == typeof(float)) retobj = tomlTable.Update(key, (float)(object)value).Added;
				else if (typeof(T) == typeof(ushort)) retobj = tomlTable.Update(key, /*auto*/(ushort)(object)value).Added;
				else if (typeof(T) == typeof(int)) retobj = tomlTable.Update(key, /*auto*/(int)(object)value).Added;
				else if (typeof(T) == typeof(long)) retobj = tomlTable.Update(key, (long)(object)value).Added;
				else if (typeof(T) == typeof(ulong)) retobj = tomlTable.Update(key, (long)(ulong)(object)value).Added;
				else if (typeof(T) == typeof(TimeSpan)) retobj = tomlTable.Update(key, SerializeTime((TimeSpan)(object)value)).Added;
				else if (typeof(T) == typeof(DateTime)) retobj = tomlTable.Update(key, (DateTime)(object)value).Added;
				else if (typeof(T).IsEnum) retobj = tomlTable.Update(key, value.ToString()).Added;
				else if (value is IEnumerable<bool> enubool) return tomlTable.Update(key, enubool).Added;
				else if (value is IEnumerable<string> enustring) return tomlTable.Update(key, enustring).Added;
				else if (value is IEnumerable<double> enudouble) return tomlTable.Update(key, enudouble).Added;
				else if (value is IEnumerable<float> enufloat) return tomlTable.Update(key, enufloat).Added;
				else if (value is IEnumerable<ushort> enuushort) return tomlTable.Update(key, enuushort.Select(x => (int)x)).Added;
				else if (value is IEnumerable<int> enuint) return tomlTable.Update(key, enuint).Added;
				else if (value is IEnumerable<long> enulong) return tomlTable.Update(key, enulong).Added;
				else if (value is IEnumerable<ulong> enuulong) return tomlTable.Update(key, enuulong.Select(x => (long)x)).Added;
				else if (value is IEnumerable<TimeSpan> enuTimeSpan) return tomlTable.Update(key, enuTimeSpan.Select(SerializeTime)).Added;
				else if (value is IEnumerable<DateTime> enuDateTime) return tomlTable.Update(key, enuDateTime).Added;
				else throw new NotSupportedException("The type is not supported");
				if (docs != null)
					retobj.AddComments(docs);
				return retobj;
			}
			throw new NotSupportedException("The type is not supported");
		}

		// *** TomlPath engine ***

		public static IEnumerable<TomlObject> ByPath(this TomlObject obj, string path)
		{
			var pathM = path.AsMemory();
			return ProcessIdentifier(obj, pathM);
		}

		private static IEnumerable<TomlObject> ProcessIdentifier(TomlObject obj, ReadOnlyMemory<char> pathM)
		{
			if (pathM.IsEmpty)
				return Enumerable.Empty<TomlObject>();

			var path = pathM.Span;
			switch (path[0])
			{
			case '*':
				{
					var rest = pathM.Slice(1);
					if (rest.IsEmpty)
						return obj.GetAllSubItems();

					if (IsArray(rest.Span))
						return obj.GetAllSubItems().SelectMany(x => ProcessArray(x, rest));
					else if (IsDot(rest.Span))
						return obj.GetAllSubItems().SelectMany(x => ProcessDot(x, rest));
					else
						throw new ArgumentException("Invalid expression after wildcard", nameof(pathM));
				}

			case '[':
				throw new ArgumentException("Invalid array open bracket", nameof(pathM));
			case ']':
				throw new ArgumentException("Invalid array close bracket", nameof(pathM));
			case '.':
				throw new ArgumentException("Invalid dot", nameof(pathM));

			default:
				{
					var subItemName = path;
					var rest = ReadOnlyMemory<char>.Empty;
					bool cont = false;
					for (int i = 0; i < path.Length; i++)
					{
						// todo allow in future
						if (path[i] == '*')
							throw new ArgumentException("Invalid wildcard position", nameof(pathM));

						var currentSub = path.Slice(i);
						if (!IsIdentifier(currentSub)) // if (!IsName)
						{
							cont = true;
							subItemName = path.Slice(0, i);
							rest = pathM.Slice(i);
							break;
						}
					}
					var item = obj.GetSubItemByName(subItemName);
					if (item is null)
						return Enumerable.Empty<TomlObject>();

					if (cont)
					{
						if (IsArray(rest.Span))
							return ProcessArray(item, rest);
						else if (IsDot(rest.Span))
							return ProcessDot(item, rest);
						else
							throw new ArgumentException("Invalid expression name identifier", nameof(pathM));
					}
					return new[] { item };
				}
			}
		}

		private static IEnumerable<TomlObject> ProcessArray(TomlObject obj, ReadOnlyMemory<char> pathM)
		{
			var path = pathM.Span;
			if (path[0] != '[')
				throw new ArgumentException("Expected array open breacket", nameof(pathM));
			for (int i = 1; i < path.Length; i++)
			{
				if (path[i] == ']')
				{
					if (i == 0)
						throw new ArgumentException("Empty array indexer", nameof(pathM));
					var indexer = path.Slice(1, i - 1);
					var rest = pathM.Slice(i + 1);
					bool cont = rest.Length > 0;

					// select
					if (indexer.Length == 1 && indexer[0] == '*')
					{
						var ret = obj.GetAllArrayItems();
						if (cont)
						{
							if (IsArray(rest.Span))
								return ret.SelectMany(x => ProcessArray(x, rest));
							else if (IsDot(rest.Span))
								return ret.SelectMany(x => ProcessDot(x, rest));
							else
								throw new ArgumentException("Invalid expression after array indexer", nameof(pathM));
						}

						return ret;
					}
					else
					{
						var ret = obj.GetArrayItemByIndex(indexer);
						if (ret is null)
							return Enumerable.Empty<TomlObject>();

						if (cont)
						{
							if (IsArray(rest.Span))
								return ProcessArray(ret, rest);
							else if (IsDot(rest.Span))
								return ProcessDot(ret, rest);
							else
								throw new ArgumentException("Invalid expression after array indexer", nameof(pathM));
						}
						return new[] { ret };
					}
				}
			}
			throw new ArgumentException("Missing array close bracket", nameof(pathM));
		}

		private static IEnumerable<TomlObject> ProcessDot(TomlObject obj, ReadOnlyMemory<char> pathM)
		{
			var path = pathM.Span;
			if (!IsDot(path))
				throw new ArgumentException("Expected dot", nameof(pathM));

			var rest = pathM.Slice(1);
			if (!IsIdentifier(rest.Span))
				throw new ArgumentException("Expected identifier after dot", nameof(pathM));

			return ProcessIdentifier(obj, rest);
		}

		internal static bool IsArray(ReadOnlySpan<char> name)
			=> name.Length >= 1 && (name[0] == '[');

		internal static bool IsIdentifier(ReadOnlySpan<char> name)
			=> name.Length >= 1 && (name[0] != '[' && name[0] != ']' && name[0] != '.');

		internal static bool IsDot(ReadOnlySpan<char> name)
			=> name.Length >= 1 && (name[0] == '.');

		private static TomlObject GetArrayItemByIndex(this TomlObject obj, ReadOnlySpan<char> index)
		{
			int indexNum = int.Parse(new string(index.ToArray()));
			if (indexNum < 0)
				return null;
			//if (!System.Buffers.Text.Utf8Parser.TryParse(index, out int indexNum, out int bytesConsumed))
			//throw new ArgumentException("Invalid array indexer");
			if (obj.TomlType == TomlObjectType.Array)
			{
				var tomlTable = (TomlArray)obj;
				if (indexNum < tomlTable.Length)
					return tomlTable[indexNum];
			}
			else if (obj.TomlType == TomlObjectType.ArrayOfTables)
			{
				var tomlTableArray = (TomlTableArray)obj;
				if (indexNum >= tomlTableArray.Count)
					return tomlTableArray[indexNum];
			}
			return null;
		}

		private static IEnumerable<TomlObject> GetAllArrayItems(this TomlObject obj)
		{
			if (obj.TomlType == TomlObjectType.Array)
				return ((TomlArray)obj).Items;
			else if (obj.TomlType == TomlObjectType.ArrayOfTables)
				return ((TomlTableArray)obj).Items;
			return Enumerable.Empty<TomlObject>();
		}

		private static TomlObject GetSubItemByName(this TomlObject obj, ReadOnlySpan<char> name)
		{
			if (obj.TomlType == TomlObjectType.Table)
				return ((TomlTable)obj).TryGetValue(new string(name.ToArray()));
			return null;
		}

		private static IEnumerable<TomlObject> GetAllSubItems(this TomlObject obj)
		{
			if (obj.TomlType == TomlObjectType.Table)
				return ((TomlTable)obj).Values;
			return Enumerable.Empty<TomlObject>();
		}

		// *** Toml Serializer ***

		public static string DumpToJson(this TomlObject obj)
		{
			var sb = new StringBuilder();
			var sw = new StringWriter(sb);
			using (var writer = new JsonTextWriter(sw))
			{
				writer.Formatting = Formatting.Indented;
				DumpToJson(obj, writer);
			}
			return sb.ToString();
		}

		public static void DumpToJson(this TomlObject obj, JsonWriter writer)
		{
			switch (obj.TomlType)
			{
			case TomlObjectType.Bool: writer.WriteValue(((TomlBool)obj).Value); break;
			case TomlObjectType.Int: writer.WriteValue(((TomlInt)obj).Value); break;
			case TomlObjectType.Float: writer.WriteValue(((TomlFloat)obj).Value); break;
			case TomlObjectType.String: writer.WriteValue(((TomlString)obj).Value); break;
			case TomlObjectType.DateTime: writer.WriteValue(((TomlLocalDateTime)obj).Value); break;
			case TomlObjectType.TimeSpan: writer.WriteValue(((TomlDuration)obj).Value); break;
			case TomlObjectType.Array:
			case TomlObjectType.ArrayOfTables:
				writer.WriteStartArray();
				IEnumerable<TomlObject> list;
				if (obj.TomlType == TomlObjectType.Array) list = ((TomlArray)obj).Items; else list = ((TomlTableArray)obj).Items;
				foreach (var item in list)
					DumpToJson(item, writer);
				writer.WriteEndArray();
				break;
			case TomlObjectType.Table:
				writer.WriteStartObject();
				foreach (var kvp in (TomlTable)obj)
				{
					writer.WritePropertyName(kvp.Key);
					DumpToJson(kvp.Value, writer);
				}
				writer.WriteEndObject();
				break;
			}
		}
	}
}
