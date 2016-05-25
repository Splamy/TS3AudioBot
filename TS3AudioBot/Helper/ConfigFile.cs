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

namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Reflection;

	public class ConfigFile : MarshalByRefObject
	{
		private string path;
		private readonly Dictionary<string, string> data;
		private bool changed;

		private ConfigFile()
		{
			changed = false;
			data = new Dictionary<string, string>();
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

			using (FileStream fs = File.Open(pPath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (StreamReader input = new StreamReader(fs))
				{
					while (!input.EndOfStream)
					{
						string s = input.ReadLine();
						if (!s.StartsWith(";") && !s.StartsWith("//") && !s.StartsWith("#"))
						{
							int index = s.IndexOf('=');
							cfgFile.data.Add(s.Substring(0, index).Trim(), s.Substring(index + 1).Trim());
						}
					}
				}
			}
			return cfgFile;
		}

		public static ConfigFile Create(string pPath)
		{
			try
			{
				FileStream fs = File.Create(pPath);
				fs.Close();
				return new ConfigFile
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

		/// <summary> Creates a dummy object which cannot save or read values.
		/// Its only purpose is to show the console dialog and create a DataStruct </summary>
		/// <returns>Returns a dummy-ConfigFile</returns>
		public static ConfigFile GetDummy()
		{
			return new DummyConfigFile();
		}

		public virtual void WriteKey(string name, string value)
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

		public virtual bool ReadKey(string name, out string value)
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

		/// <summary>Reads an object from the currently loaded file.</summary>
		/// <returns>A new struct instance with the read values.</returns>
		/// <param name="associatedClass">Class the DataStruct is associated to.</param>
		/// <param name="defaultIfPossible">If set to <c>true</c> the method will use the default value from the InfoAttribute if it exists,
		/// if no default value exists or set to <c>false</c> it will ask for the value on the console.</param>
		/// <typeparam name="T">Struct to be read from the file.</typeparam>
		public T GetDataStruct<T>(MemberInfo associatedClass, bool defaultIfPossible) where T : struct
		{
			if (associatedClass == null)
				throw new ArgumentNullException(nameof(associatedClass));

			object dataStruct = new T();
			string assocName = associatedClass.Name;
			var fields = typeof(T).GetFields();
			foreach (var field in fields)
			{
				InfoAttribute iAtt = field.GetCustomAttribute<InfoAttribute>();
				string entryName = assocName + "::" + field.Name;
				string rawValue = string.Empty;
				object parsedValue = null;

				// determine the raw data string, whether from Console or File
				if (!ReadKey(entryName, out rawValue))
				{
					// Check if we can use the default value
					if (iAtt != null && defaultIfPossible && iAtt.HasDefault)
						rawValue = iAtt.DefaultValue;
					else
					{
						Console.Write("Please enter {0}: ", iAtt != null ? iAtt.Description : entryName);
						rawValue = Console.ReadLine();
					}
				}

				// Try to parse it and save if necessary
				parsedValue = ParseToType(field.FieldType, rawValue);
				if (parsedValue == null)
				{
					Console.WriteLine("Input parse failed [Ignoring]");
					continue;
				}
				WriteValueToConfig(entryName, parsedValue);
				//TODO write outcommented line inf config file

				// finally set the value to our object
				field.SetValue(dataStruct, parsedValue);
			}
			return (T)dataStruct;
		}

		protected bool WriteValueToConfig(string entryName, object value)
		{
			if (value == null)
				return false;
			Type tType = value.GetType();
			if (tType == typeof(string))
			{
				WriteKey(entryName, (string)value);
			}
			if (tType == typeof(bool) || IsNumeric(tType) || tType == typeof(char))
			{
				WriteKey(entryName, value.ToString());
			}
			else
			{
				return false;
			}
			return true;
		}

		protected static object ParseToType(Type targetType, string value)
		{
			if (targetType == typeof(string))
				return value;
			MethodInfo mi = targetType.GetMethod("TryParse", new[] { typeof(string), targetType.MakeByRefType() });
			if (mi == null)
				throw new ArgumentException("The value of the DataStruct couldn't be parsed.");
			object[] result = { value, null };
			object success = mi.Invoke(null, result);
			if (!(bool)success)
				return null;
			return result[1];
		}

		protected static bool IsNumeric(Type type)
		{
			return type == typeof(sbyte)
				|| type == typeof(byte)
				|| type == typeof(short)
				|| type == typeof(ushort)
				|| type == typeof(int)
				|| type == typeof(uint)
				|| type == typeof(long)
				|| type == typeof(ulong)
				|| type == typeof(float)
				|| type == typeof(double)
				|| type == typeof(decimal);
		}

		public virtual void Close()
		{
			if (!changed)
			{
				return;
			}

			using (FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write))
			{
				using (StreamWriter output = new StreamWriter(fs))
				{
					foreach (string key in data.Keys)
					{
						output.Write(key);
						output.Write('=');
						output.WriteLine(data[key]);
					}
					output.Flush();
				}
			}
		}

		private class DummyConfigFile : ConfigFile
		{
			public override void Close() { }
			public override bool ReadKey(string name, out string value) { value = null; return false; }
			public override void WriteKey(string name, string value) { }
		}
	}
}
