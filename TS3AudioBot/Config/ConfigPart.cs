// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Config
{
	using Nett;
	using Newtonsoft.Json;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using static TS3AudioBot.Helper.TomlTools;

	[DebuggerDisplay("unknown:{Key}")]
	public abstract class ConfigPart
	{
		public string Documentation { get; set; }
		public string Key { get; set; }
		// must be a field otherwise it will be found as a child for ConfigTable
		public ConfigEnumerable Parent;

		protected ConfigPart() { }
		protected ConfigPart(string key)
		{
			Key = key;
		}

		public abstract void FromToml(TomlObject tomlObject);
		public abstract void ToToml(bool writeDefaults, bool writeDocumentation);
		public abstract void Derive(ConfigPart derived);
		public abstract void ToJson(JsonWriter writer);

		protected void CreateDocumentation(TomlObject tomlObject)
		{
			TomlComment[] docs = tomlObject.Comments.Where(x => x.Text.StartsWith("#")).ToArray();
			tomlObject.ClearComments();
			if (!string.IsNullOrEmpty(Documentation))
			{
				tomlObject.AddComment(Documentation, CommentLocation.UseDefault);
			}
			if ((docs?.Length ?? 0) > 0)
				tomlObject.AddComments(docs);
		}

		// TODO remove when all suppor FromJson too and move IJsonConfig to here
		public string ToJson()
		{
			var sb = new StringBuilder();
			var sw = new StringWriter(sb);
			using (var writer = new JsonTextWriter(sw))
			{
				writer.Formatting = Formatting.Indented;
				ToJson(writer);
			}
			return sb.ToString();
		}

		// *** Path accessor ***

		public IEnumerable<ConfigPart> ByPath(string path)
		{
			var pathM = path.AsMemory();
			return ProcessIdentifier(pathM);
		}

		private IEnumerable<ConfigPart> ProcessIdentifier(ReadOnlyMemory<char> pathM)
		{
			if (pathM.IsEmpty)
				return Enumerable.Empty<ConfigPart>();

			var path = pathM.Span;
			switch (path[0])
			{
			case '*':
				{
					var rest = pathM.Slice(1);
					if (rest.IsEmpty)
						return GetAllSubItems();

					if (IsArray(rest.Span))
						return GetAllSubItems().SelectMany(x => x.ProcessArray(rest));
					else if (IsDot(rest.Span))
						return GetAllSubItems().SelectMany(x => x.ProcessDot(rest));
					else
						throw new ArgumentException(nameof(path), "Invalid expression after wildcard");
				}

			case '[':
				throw new ArgumentException(nameof(path), "Invalid array open bracket");
			case ']':
				throw new ArgumentException(nameof(path), "Invalid array close bracket");
			case '.':
				throw new ArgumentException(nameof(path), "Invalid dot");

			default:
				{
					var subItemName = path;
					var rest = ReadOnlyMemory<char>.Empty;
					bool cont = false;
					for (int i = 0; i < path.Length; i++)
					{
						// todo allow in future
						if (path[i] == '*')
							throw new ArgumentException(nameof(path), "Invalid wildcard position");

						var currentSub = path.Slice(i);
						if (!IsIdentifier(currentSub)) // if (!IsName)
						{
							cont = true;
							subItemName = path.Slice(0, i);
							rest = pathM.Slice(i);
							break;
						}
					}
					var item = GetSubItemByName(subItemName);
					if (item == null)
						return Enumerable.Empty<ConfigPart>();

					if (cont)
					{
						if (IsArray(rest.Span))
							return item.ProcessArray(rest);
						else if (IsDot(rest.Span))
							return item.ProcessDot(rest);
						else
							throw new ArgumentException(nameof(path), "Invalid expression name identifier");
					}
					return new[] { item };
				}
			}
		}

		private IEnumerable<ConfigPart> ProcessArray(ReadOnlyMemory<char> pathM)
		{
			var path = pathM.Span;
			if (path[0] != '[')
				throw new ArgumentException(nameof(path), "Expected array open breacket");
			for (int i = 1; i < path.Length; i++)
			{
				if (path[i] == ']')
				{
					if (i == 0)
						throw new ArgumentException(nameof(path), "Empty array indexer");
					var indexer = path.Slice(1, i - 1);
					var rest = pathM.Slice(i + 1);
					bool cont = rest.Length > 0;

					// select
					if (indexer.Length == 1 && indexer[0] == '*')
					{
						var ret = GetAllArrayItems();
						if (cont)
						{
							if (IsArray(rest.Span))
								return ret.SelectMany(x => x.ProcessArray(rest));
							else if (IsDot(rest.Span))
								return ret.SelectMany(x => x.ProcessDot(rest));
							else
								throw new ArgumentException(nameof(path), "Invalid expression after array indexer");
						}

						return ret;
					}
					else
					{
						var ret = GetArrayItemByIndex(indexer);
						if (ret == null)
							return Enumerable.Empty<ConfigPart>();

						if (cont)
						{
							if (IsArray(rest.Span))
								return ret.ProcessArray(rest);
							else if (IsDot(rest.Span))
								return ret.ProcessDot(rest);
							else
								throw new ArgumentException(nameof(path), "Invalid expression after array indexer");
						}
						return new[] { ret };
					}
				}
			}
			throw new ArgumentException(nameof(path), "Missing array close bracket");
		}

		private IEnumerable<ConfigPart> ProcessDot(ReadOnlyMemory<char> pathM)
		{
			var path = pathM.Span;
			if (!IsDot(path))
				throw new ArgumentException(nameof(path), "Expected dot");

			var rest = pathM.Slice(1);
			if (!IsIdentifier(rest.Span))
				throw new ArgumentException(nameof(path), "Expected identifier after dot");

			return ProcessIdentifier(rest);
		}

		private ConfigPart GetArrayItemByIndex(ReadOnlySpan<char> index)
		{
			var indexNum = new string(index.ToArray());

			//if (!System.Buffers.Text.Utf8Parser.TryParse(index, out int indexNum, out int bytesConsumed))
			//throw new ArgumentException("Invalid array indexer");
			if (this is ConfigEnumerable table)
			{
				return table.GetChild(indexNum);
			}
			/*else if (this is ConfigValue<[]ARRAY> array)
			{
				// TODO
			}*/
			return null;
		}

		private IEnumerable<ConfigPart> GetAllArrayItems()
		{
			if (this is ConfigEnumerable table)
				return table.GetAllChildren();
			return Enumerable.Empty<ConfigPart>();
		}

		private ConfigPart GetSubItemByName(ReadOnlySpan<char> name)
		{
			var indexNum = new string(name.ToArray());
			if (this is ConfigEnumerable table)
				return table.GetChild(indexNum);
			return null;
		}

		private IEnumerable<ConfigPart> GetAllSubItems()
		{
			if (this is ConfigEnumerable table)
				return table.GetAllChildren();
			return Enumerable.Empty<ConfigPart>();
		}
	}
}
