namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Reflection;

	class ConfigFile
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
		public T GetDataStruct<T>(Type associatedClass, bool defaultIfPossible) where T : struct
		{
			object dataStruct = new T();
			string assocName = associatedClass.Name;
			var fields = typeof(T).GetFields();
			foreach (var field in fields)
			{
				InfoAttribute iAtt = field.GetCustomAttribute<InfoAttribute>();
				string entryName = assocName + "::" + field.Name;
				string rawValue = string.Empty;
				object parsedValue = null;

				// determine the raw data string, wether from Console or File
				bool gotInput = ReadKey(entryName, out rawValue);
				bool manualInput = false;
				if (!gotInput)
				{
					// Check if we can use the default value
					if (iAtt != null && defaultIfPossible && iAtt.HasDefault)
						rawValue = iAtt.DefaultValue;
					else
					{
						Console.Write("Please enter {0}: ", iAtt != null ? iAtt.Description : entryName);
						rawValue = Console.ReadLine();
						manualInput = true;
					}
					gotInput = true;
				}

				// Try to parse it and save if necessary
				parsedValue = ParseToType(field.FieldType, rawValue);
				if (parsedValue == null)
				{
					Console.WriteLine("Input parse failed [Ignoring]");
					continue;
				}
				if (manualInput)
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

		protected object ParseToType(Type targetType, string value)
		{
			if (targetType == typeof(string))
				return value;
			MethodInfo mi = targetType.GetMethod("TryParse", new[] { typeof(string), targetType.MakeByRefType() });
			if (mi == null)
				throw new Exception("The value of the DataStruct couldn't be parsed.");
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

		private class DummyConfigFile : ConfigFile
		{
			public override void Close() { }
			public override bool ReadKey(string name, out string value) { value = null; return false; }
			public override void WriteKey(string name, string value) { }
		}
	}
}
