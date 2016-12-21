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
	using System.Globalization;
	using System.IO;
	using System.Reflection;
	using System.Text;
	using System.Linq;

	public abstract class ConfigFile
	{
		private static readonly char[] splitChar = new[] { '=' };
		private const string nameSeperator = "::";
		private bool changed;
		private List<ConfigData> confObjects;

		public ConfigFile()
		{
			confObjects = new List<ConfigData>();
		}

		public static ConfigFile OpenOrCreate(string path)
		{
			NormalConfigFile cfgFile = new NormalConfigFile(path);
			return cfgFile.Open() ? cfgFile : null;
		}

		/// <summary> Creates a dummy object, all setting will be in memory only.
		/// Its only purpose is to show the console dialog and create a DataStruct.</summary>
		/// <returns>Returns a dummy-ConfigFile</returns>
		public static ConfigFile CreateDummy()
		{
			return new MemoryConfigFile();
		}

		/// <summary>Reads an object from the currently loaded file.</summary>
		/// <returns>A new struct instance with the read values.</returns>
		/// <param name="associatedClass">Class the DataStruct is associated to.</param>
		/// <param name="defaultIfPossible">If set to <c>true</c> the method will use the default value from the InfoAttribute if it exists,
		/// if no default value exists or set to <c>false</c> it will ask for the value on the console.</param>
		/// <typeparam name="T">Struct to be read from the file.</typeparam>
		public T GetDataStruct<T>(string associatedClass, bool defaultIfPossible) where T : ConfigData, new()
		{
			if (string.IsNullOrEmpty(associatedClass))
				throw new ArgumentNullException(nameof(associatedClass));

			T dataStruct = new T();
			var fields = typeof(T).GetProperties();
			foreach (var field in fields)
			{
				InfoAttribute iAtt = field.GetCustomAttribute<InfoAttribute>();
				string entryName = associatedClass + nameSeperator + field.Name;
				string rawValue = string.Empty;
				object parsedValue = null;

				// determine the raw data string, whether from Console or File
				if (!ReadKey(entryName, out rawValue))
				{
					changed = true;
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
				try
				{
					parsedValue = Convert.ChangeType(rawValue, field.PropertyType, CultureInfo.InvariantCulture);
				}
				catch (Exception ex) when (ex is FormatException || ex is OverflowException)
				{
					Console.WriteLine("Input parse of {0} failed [Ignoring]", entryName);
					continue;
				}

				WriteValueToConfig(entryName, parsedValue);

				// finally set the value to our object
				field.SetValue(dataStruct, parsedValue);
			}

			dataStruct.AssociatedClass = associatedClass;
			RegisterConfigObj(dataStruct);

			return dataStruct;
		}

		protected virtual void RegisterConfigObj(ConfigData obj)
		{
			confObjects.Add(obj);
		}

		public R SetSetting(string key, string value)
		{
			if (string.IsNullOrEmpty(value))
				throw new ArgumentNullException(nameof(value));

			string[] keyParam = key.Split(new[] { nameSeperator }, StringSplitOptions.None);
			var filteredObjects = confObjects.Where(co => co.AssociatedClass == keyParam[0]);
			if (!filteredObjects.Any())
				return "No active entries found for this key";

			PropertyInfo prop = null;
			object convertedValue = null;
			foreach (var co in filteredObjects)
			{
				if (prop == null)
				{
					prop = co.GetType().GetProperty(keyParam[1]);
					try
					{
						convertedValue = Convert.ChangeType(value, prop.PropertyType, CultureInfo.InvariantCulture);
					}
					catch (Exception ex) when (ex is FormatException || ex is OverflowException)
					{
						return "The value could not be parsed";
					}
				}
				prop.SetValue(co, convertedValue);
			}
			WriteValueToConfig(key, convertedValue);
			return R.OkR;
		}

		protected void WriteValueToConfig(string entryName, object value)
			=> WriteKey(entryName, Convert.ToString(value, CultureInfo.InvariantCulture));

		protected abstract void WriteKey(string key, string value);
		protected abstract bool ReadKey(string key, out string value);
		public abstract void Close();

		public abstract IEnumerable<KeyValuePair<string, string>> GetConfigMap();

		private class NormalConfigFile : ConfigFile
		{
			private string path;
			private List<LineData> fileLines;
			private readonly Dictionary<string, int> data;
			private List<object> registeredObjects = new List<object>();
			private bool open;
			private readonly object writeLock = new object();

			public NormalConfigFile(string path)
			{
				this.path = path;
				changed = false;
				data = new Dictionary<string, int>();
			}

			public bool Open()
			{
				if (!File.Exists(path))
				{
					Console.WriteLine("Config file does not exist...");
					return true;
				}
				open = true;

				var strLines = File.ReadAllLines(path, new UTF8Encoding(false));
				fileLines = new List<LineData>(strLines.Length);
				for (int i = 0; i < strLines.Length; i++)
				{
					var s = strLines[i];
					if (s.StartsWith(";", StringComparison.Ordinal)
						|| s.StartsWith("//", StringComparison.Ordinal)
						|| s.StartsWith("#", StringComparison.Ordinal)
						|| string.IsNullOrWhiteSpace(s))
					{
						fileLines.Add(new LineData(s));
					}
					else
					{
						string[] kvp = s.Split(splitChar, 2);
						if (kvp.Length < 2) { Console.WriteLine("Invalid log entry: \"{0}\"", s); continue; }
						WriteKey(kvp[0], kvp[1]);
					}
				}
				return true;
			}

			protected override void WriteKey(string key, string value)
			{
				if (!open)
					changed = true;

				int line;
				if (data.TryGetValue(key, out line))
				{
					fileLines[line].Value = value;
				}
				else
				{
					line = fileLines.Count;
					fileLines.Add(new LineData(key, value));
					data.Add(key, line);
				}
				FlushToFile();
			}

			protected override bool ReadKey(string key, out string value)
			{
				int line;
				if (data.TryGetValue(key, out line))
				{
					value = fileLines[line].Value;
					return true;
				}
				else
				{
					value = null;
					return false;
				}
			}

			protected override void RegisterConfigObj(ConfigData obj)
			{
				base.RegisterConfigObj(obj);
				obj.PropertyChanged += ConfigDataPropertyChanged;
			}

			private void ConfigDataPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				ConfigData cd = sender as ConfigData;
				if (cd == null)
					return;

				string key = cd.AssociatedClass + nameSeperator + e.PropertyName;
				var property = cd.GetType().GetProperty(e.PropertyName);
				WriteValueToConfig(key, property.GetValue(cd));
			}

			public override void Close()
			{
				open = false;
				FlushToFile();
			}

			private void FlushToFile()
			{
				if (open || !changed)
					return;

				lock (writeLock)
				{
					try
					{
						using (StreamWriter output = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write)))
						{
							foreach (var line in fileLines)
							{
								if (line.Comment)
								{
									output.WriteLine(line.Value);
								}
								else
								{
									output.Write(line.Key);
									output.Write('=');
									output.WriteLine(line.Value);
								}
							}
							output.Flush();
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine("Could not create ConfigFile: " + ex.Message);
					}
				}

				changed = false;
			}

			public override IEnumerable<KeyValuePair<string, string>> GetConfigMap()
				=> fileLines.Where(e => !e.Comment).Select(e => new KeyValuePair<string, string>(e.Key, e.Value));

			private class LineData
			{
				public bool Comment { get; }
				public string Key { get; set; }
				public string Value { get; set; }

				public LineData(string comment) { Value = comment; Comment = true; }
				public LineData(string key, string value) { Key = key; Value = value; Comment = false; }
				public override string ToString() => Comment ? "#" + Value : Key + " = " + Value;
			}
		}

		private class MemoryConfigFile : ConfigFile
		{
			private readonly Dictionary<string, string> data;

			public MemoryConfigFile()
			{
				data = new Dictionary<string, string>();
			}

			protected override bool ReadKey(string key, out string value) => data.TryGetValue(key, out value);
			protected override void WriteKey(string key, string value) => data[key] = value;
			public override void Close() { }

			public override IEnumerable<KeyValuePair<string, string>> GetConfigMap() => data;
		}
	}

	[ImplementPropertyChanged]
	public class ConfigData : INotifyPropertyChanged
	{
		[DoNotNotify]
		internal string AssociatedClass { get; set; }
		public event PropertyChangedEventHandler PropertyChanged;
	}
}
