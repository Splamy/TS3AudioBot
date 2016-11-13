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
	using PropertyChanged;
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Reflection;
	using System.Text;

	public abstract class ConfigFile
	{
		private static readonly char[] splitChar = new[] { '=' };

		public static ConfigFile OpenFile(string pPath)
		{
			NormalConfigFile cfgFile = new NormalConfigFile(pPath);
			return cfgFile.Open() ? cfgFile : null;
		}

		public static ConfigFile CreateFile(string path)
		{
			try
			{
				using (FileStream fs = File.Create(path)) { }
				return new NormalConfigFile(path);
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
		public static ConfigFile CreateDummy()
		{
			return new MemoryConfigFile();
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


		/// <summary>Reads an object from the currently loaded file.</summary>
		/// <returns>A new struct instance with the read values.</returns>
		/// <param name="associatedClass">Class the DataStruct is associated to.</param>
		/// <param name="defaultIfPossible">If set to <c>true</c> the method will use the default value from the InfoAttribute if it exists,
		/// if no default value exists or set to <c>false</c> it will ask for the value on the console.</param>
		/// <typeparam name="T">Struct to be read from the file.</typeparam>
		public T GetDataStruct<T>(string associatedClass, bool defaultIfPossible) where T : ConfigData, new()
		{
			if (associatedClass == null)
				throw new ArgumentNullException(nameof(associatedClass));

			T dataStruct = new T();
			var fields = typeof(T).GetFields();
			foreach (var field in fields)
			{
				InfoAttribute iAtt = field.GetCustomAttribute<InfoAttribute>();
				string entryName = associatedClass + "::" + field.Name;
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
			return dataStruct;
		}
		protected virtual void RegisterConfigObj<T>(T obj) where T : ConfigData { }
		protected bool WriteValueToConfig(string entryName, object value)
		{
			if (value == null)
				return false;
			Type tType = value.GetType();
			if (tType == typeof(string))
			{
				WriteKey(entryName, (string)value);
			}
			else if (tType == typeof(bool) || IsNumeric(tType) || tType == typeof(char))
			{
				WriteKey(entryName, value.ToString());
			}
			else
			{
				return false;
			}
			return true;
		}

		protected abstract bool Open();
		protected abstract void WriteKey(string name, string value);
		protected abstract bool ReadKey(string name, out string value);
		public abstract void Close();


		private class NormalConfigFile : ConfigFile
		{
			private string path;
			private string[] fileLines;
			private readonly Dictionary<string, string> data;
			private bool changed;

			public NormalConfigFile(string path)
			{
				this.path = path;
				changed = false;
				data = new Dictionary<string, string>();
			}

			protected override bool Open()
			{
				if (!File.Exists(path))
				{
					Console.WriteLine("Config file does not exist");
					return false;
				}

				fileLines = File.ReadAllLines(path, new UTF8Encoding(false));
				for (int i = 0; i < fileLines.Length; i++)
				{
					var s = fileLines[i];
					if (!s.StartsWith(";", StringComparison.Ordinal)
						&& !s.StartsWith("//", StringComparison.Ordinal)
						&& !s.StartsWith("#", StringComparison.Ordinal))
					{
						string[] kvp = s.Split(splitChar, 2);
						if (kvp.Length < 2) { Console.WriteLine("Invalid log entry: \"{0}\"", s); continue; }
						data.Add(kvp[0], kvp[1]);
					}
				}
				return true;
			}

			protected override void WriteKey(string name, string value)
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

			protected override bool ReadKey(string name, out string value)
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

			protected override void RegisterConfigObj<T>(T obj)
			{
				obj.PropertyChanged += ConfigDataPropertyChanged;
			}

			private void ConfigDataPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				//e.PropertyName;
			}

			public override void Close()
			{
				if (!changed)
				{
					return;
				}

				using (StreamWriter output = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write)))
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

			private class LineData
			{
				public int Line { get; set; }
				public string Content { get; set; }
			}
		}

		private class MemoryConfigFile : ConfigFile
		{
			public override void Close() { }

			protected override bool Open() { return true; }
			protected override bool ReadKey(string name, out string value) { value = null; return false; }
			protected override void WriteKey(string name, string value) { }
		}
	}

	[ImplementPropertyChanged]
	public class ConfigData : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;
	}
}
