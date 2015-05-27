using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace TS3AudioBot
{
	class ConfigFile
	{
		private string path;
		Dictionary<string, string> data;
		private bool changed;

		private ConfigFile()
		{
			changed = false;
			data = new Dictionary<string, string>();
		}

		public void WriteKey(string name, string value)
		{
			changed = true;

			if (data.ContainsKey(name))
			{
				data[name] = value;
			}
			else
			{
				data.Add(name, value);
			}
		}

		public string ReadKey(string name)
		{
			string value;
			ReadKey(name, out value);
			return value;
		}

		public bool ReadKey(string name, out string value)
		{
			if (!data.ContainsKey(name))
			{
				value = null;
				return false;
			}
			else
			{
				value = data[name];
				return true;
			}
		}

		public static ConfigFile Open(string pPath)
		{
			ConfigFile cfgFile = new ConfigFile()
			{
				path = pPath,
			};

			if (!File.Exists(pPath))
			{
				Console.WriteLine("Config file does not exist");
				return null;
			}

			FileStream fs = File.Open(pPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			StreamReader input = new StreamReader(fs);
			while (!input.EndOfStream)
			{
				string s = input.ReadLine();
				if (!s.StartsWith(";") && !s.StartsWith("//") && !s.StartsWith("#"))
				{
					int index = s.IndexOf('=');
					cfgFile.data.Add(s.Substring(0, index).Trim(), s.Substring(index + 1).Trim());
				}
			}
			fs.Close();
			return cfgFile;
		}

		public static ConfigFile Create(string pPath)
		{
			try
			{
				FileStream fs = File.Create(pPath);
				fs.Close();
				return new ConfigFile()
				{
					path = pPath,
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine("Could not create ConfigFile: " + ex.Message);
				return null;
			}
		}

		public T GetStructData<T>(Type associatedClass, bool askForInput = false) where T : struct
		{
			return GetStructData<T>(this, associatedClass, askForInput);
		}

		public static T GetStructData<T>(ConfigFile cfgFile, Type associatedClass, bool askForInput = false) where T : struct
		{
			object dataStruct = new T();
			string assocName = associatedClass.Name;
			var fields = typeof(T).GetFields();
			foreach (var field in fields)
			{
				InfoAttribute iAtt = field.GetCustomAttribute<InfoAttribute>();
				string entryName = assocName + "::" + field.Name;
				string rawvalue = string.Empty;
				object inpValue = null;
				if (cfgFile == null || (!cfgFile.ReadKey(entryName, out rawvalue) && askForInput))
				{
					Console.Write("Please enter {0}: ", iAtt != null ? iAtt.Description : entryName);
					rawvalue = Console.ReadLine();
					inpValue = ReadValueFromConsole(field.FieldType, rawvalue);
					if (cfgFile != null && inpValue != null)
					{
						WriteValueToConfig(cfgFile, entryName, inpValue);
					}
				}
				field.SetValue(dataStruct, inpValue);
			}
			return (T)dataStruct;
		}

		public static object ReadValueFromConsole(Type targetType, string value)
		{
			if (targetType == typeof(string))
				return value;
			MethodInfo mi = targetType.GetMethod("TryParse", new[] { typeof(string), targetType.MakeByRefType() });
			if (mi == null)
				throw new Exception("The value of the DataStruct couldn't be parsed.");
			object[] result = new object[] { value, null };
			object success = mi.Invoke(null, result);
			if (!(bool)success)
				return null;
			return result[1];
		}

		private static bool WriteValueToConfig(ConfigFile cfgFile, string entryName, object value)
		{
			if (value == null)
				return false;
			Type tType = value.GetType();
			if (tType == typeof(string))
			{
				cfgFile.WriteKey(entryName, (string)value);
			}
			if (IsNumeric(tType) || tType == typeof(char))
			{
				cfgFile.WriteKey(entryName, value.ToString());
			}
			else if (tType == typeof(bool))
			{
				cfgFile.WriteKey(entryName, ((bool)value) ? "true" : "false");
			}
			else
			{
				return false;
			}
			return true;
		}

		private static bool IsNumeric(Type T)
		{
			return T == typeof(sbyte)
				|| T == typeof(byte)
				|| T == typeof(short)
				|| T == typeof(ushort)
				|| T == typeof(int)
				|| T == typeof(uint)
				|| T == typeof(long)
				|| T == typeof(ulong)
				|| T == typeof(float)
				|| T == typeof(double)
				|| T == typeof(decimal);
		}

		public void Close()
		{
			if (!changed)
			{
				return;
			}

			FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write);
			StreamWriter output = new StreamWriter(fs);
			foreach (string key in data.Keys)
			{
				output.Write(key);
				output.Write('=');
				output.WriteLine(data[key]);
			}
			output.Flush();
			fs.Close();
		}
	}
}
