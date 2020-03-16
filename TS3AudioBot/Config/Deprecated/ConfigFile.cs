// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using TS3AudioBot.Localization;
using TSLib.Helper;

namespace TS3AudioBot.Config.Deprecated
{
	public abstract class ConfigFile
	{
		private const char SplitChar = '=';
		private static readonly char[] SplitCharArr = { SplitChar };
		private const string CommentSeq = "#";
		private static readonly string[] CommentSeqArr = { CommentSeq, ";", "//" };
		private const string NameSeperator = "::";
		private bool changed;
		private readonly Dictionary<string, ConfigData> confObjects = new Dictionary<string, ConfigData>();

		public static ConfigFile OpenOrCreate(string path)
		{
			var cfgFile = new NormalConfigFile(path);
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

			if (confObjects.TryGetValue(associatedClass, out var co))
				return (T)co;

			var dataStruct = new T();
			var fields = typeof(T).GetProperties();
			foreach (var field in fields)
			{
				InfoAttribute iAtt = field.GetCustomAttribute<InfoAttribute>();
				string entryName = associatedClass + NameSeperator + field.Name;
				object parsedValue;
				bool newKey = false;

				// determine the raw data string, whether from Console or File
				if (!ReadKey(entryName, out var rawValue))
				{
					newKey = true;
					changed = true;
					// Check if we can use the default value
					if (iAtt != null && defaultIfPossible && iAtt.HasDefault)
					{
						rawValue = iAtt.DefaultValue;
					}
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

				if (newKey && iAtt != null)
					WriteComment(iAtt.Description);
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
			confObjects.Add(obj.AssociatedClass, obj);
		}

		public E<LocalStr> SetSetting(string key, string value)
		{
			if (string.IsNullOrEmpty(value))
				throw new ArgumentNullException(nameof(value));

			string[] keyParam = key.Split(new[] { NameSeperator }, StringSplitOptions.None);
			if (!confObjects.TryGetValue(keyParam[0], out var co))
				return new LocalStr(strings.error_config_no_key_found);

			object convertedValue;
			PropertyInfo prop = co.GetType().GetProperty(keyParam[1], BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);
			try
			{
				convertedValue = Convert.ChangeType(value, prop.PropertyType, CultureInfo.InvariantCulture);
			}
			catch (Exception ex) when (ex is FormatException || ex is OverflowException)
			{
				return new LocalStr(strings.error_config_value_parse_error);
			}
			co.SetUnsafe(prop.Name, convertedValue);
			WriteValueToConfig(key, convertedValue);
			return R.Ok;
		}

		protected void WriteValueToConfig(string entryName, object value)
			=> WriteKey(entryName, Convert.ToString(value, CultureInfo.InvariantCulture));

		protected abstract void WriteComment(string text);
		protected abstract void WriteKey(string key, string value);
		protected abstract bool ReadKey(string key, out string value);
		public abstract void Close();

		public abstract IEnumerable<KeyValuePair<string, string>> GetConfigMap();

		protected static bool IsComment(string text) =>
			CommentSeqArr.Any(seq => text.StartsWith(seq, StringComparison.Ordinal)) || string.IsNullOrWhiteSpace(text);

		private class NormalConfigFile : ConfigFile
		{
			private readonly string path;
			private readonly List<LineData> fileLines;
			private readonly Dictionary<string, int> data;
			private bool open;
			private readonly object writeLock = new object();

			public NormalConfigFile(string path)
			{
				this.path = path;
				changed = false;
				data = new Dictionary<string, int>();
				fileLines = new List<LineData>();
			}

			public bool Open()
			{
				if (!File.Exists(path))
				{
					Console.WriteLine("Config file does not exist...");
					return true;
				}
				open = true;

				var strLines = File.ReadAllLines(path, Tools.Utf8Encoder);
				fileLines.Clear();
				for (int i = 0; i < strLines.Length; i++)
				{
					var s = strLines[i];
					if (IsComment(s))
					{
						fileLines.Add(new LineData(s));
					}
					else
					{
						string[] kvp = s.Split(SplitCharArr, 2);
						if (kvp.Length < 2)
						{
							Console.WriteLine("Invalid log entry: \"{0}\"", s);
							continue;
						}
						WriteKey(kvp[0], kvp[1]);
					}
				}
				return true;
			}

			protected override void WriteComment(string text)
			{
				if (!open)
					changed = true;

				fileLines.Add(new LineData(CommentSeq + " " + text));
			}

			protected override void WriteKey(string key, string value)
			{
				if (!open)
					changed = true;

				string lowerKey = key.ToLowerInvariant();

				if (data.TryGetValue(lowerKey, out int line))
				{
					fileLines[line].Value = value;
				}
				else
				{
					line = fileLines.Count;
					fileLines.Add(new LineData(key, value));
					data.Add(lowerKey, line);
				}
				FlushToFile();
			}

			protected override bool ReadKey(string key, out string value)
			{
				if (data.TryGetValue(key.ToLowerInvariant(), out int line))
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
				if (!(sender is ConfigData cd))
					return;

				string key = cd.AssociatedClass + NameSeperator + e.PropertyName;
				if (cd.TryGet(e.PropertyName, out var propValue))
					WriteValueToConfig(key, propValue);
				else
					throw new InvalidOperationException($"No config entry with this value found '{e.PropertyName}'");
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
						using (var output = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write)))
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
									output.Write(SplitChar);
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
				public string Key { get; }
				public string Value { get; set; }

				public LineData(string comment) { Value = comment; Comment = true; }
				public LineData(string key, string value)
				{
					Key = key;
					Value = value;
					Comment = IsComment(key);

					if (!Comment && value is null)
						throw new ArgumentNullException(nameof(value));
				}
				public override string ToString() => Comment ? Value : Key + SplitChar + Value;
			}
		}

		private class MemoryConfigFile : ConfigFile
		{
			private readonly Dictionary<string, string> data;

			public MemoryConfigFile()
			{
				data = new Dictionary<string, string>();
			}

			protected override void WriteComment(string text) { }
			protected override bool ReadKey(string key, out string value) => data.TryGetValue(key.ToLowerInvariant(), out value);
			protected override void WriteKey(string key, string value) => data[key.ToLowerInvariant()] = value;
			public override void Close() { }

			public override IEnumerable<KeyValuePair<string, string>> GetConfigMap() => data;
		}
	}

	public class ConfigData
	{
		internal string AssociatedClass { get; set; }
		private readonly Dictionary<string, object> Values = new Dictionary<string, object>();
		internal event PropertyChangedEventHandler PropertyChanged;

		protected T Get<T>([System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
		{
			if (Values.TryGetValue(memberName.ToUpperInvariant(), out var data))
				return (T)data;
			return default;
		}

		internal bool TryGet(string memberName, out object value) => Values.TryGetValue(memberName.ToUpperInvariant(), out value);

		protected void Set<T>(T value,
			[System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
		{
			Values[memberName.ToUpperInvariant()] = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
		}

		internal void SetUnsafe(string memberName, object value)
		{
			Values[memberName.ToUpperInvariant()] = value;
		}
	}
}
