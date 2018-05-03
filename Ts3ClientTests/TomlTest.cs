using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nett;

namespace Ts3ClientTests
{
	class TomlTest
	{
		static void Main(string[] args)
		{
			var toml = Toml.ReadFile("conf.toml");

			var struc = toml.Get<TStruc>();

			Toml.WriteFile(toml, "conf_out.toml");
		}
	}

	class Config
	{
		public void GetAll()
		{

		}
	}

	public static class TomlPath
	{
		public static IEnumerable<TomlObject> ByPath(this TomlObject obj, string path) => ByPath(obj, path.AsMemory());

		private static IEnumerable<TomlObject> ByPath(this TomlObject obj, ReadOnlyMemory<char> pathM)
		{
			if (pathM.IsEmpty)
				return Enumerable.Empty<TomlObject>();

			var path = pathM.Span;
			switch (path[0])
			{
			case '*':
				{
					if (path.Length == 1)
					{
						return obj.GetAllSubItems();
					}
					else if (path.Length > 1 && path[1] == '.')
					{
						var rest = pathM.Slice(2);
						return obj.GetAllSubItems().SelectMany(x => x.ByPath(rest));
					}
					throw new ArgumentException(nameof(path), "Invalid expression after wildcard");
				}

			case '[':
				for (int i = 1; i < path.Length; i++)
				{
					if (path[i] == ']')
					{
						if (i == 0)
							throw new ArgumentException(nameof(path), "Empty array indexer");
						var indexer = path.Slice(1, i - 1);
						// select
						if (indexer.Length == 1 && indexer[0] == '*')
						{
							return obj.GetAllArrayItems();
						}

						var rest = pathM.Slice(i + 1);
						if (rest.Length > 0 && rest.Span[0] == '.')
						{
							rest = rest.Slice(1);
							return obj.GetAllArrayItems().SelectMany(x => obj.ByPath(rest));
						}
						throw new ArgumentException(nameof(path), "Invalid expression after array indexer");
					}
				}
				throw new ArgumentException(nameof(path), "Missing array close bracket");

			case ']':
				throw new ArgumentException(nameof(path), "Invalid array close bracket");

			default:
				{
					var subItemName = path;
					var rest = ReadOnlyMemory<char>.Empty;
					for (int i = 1; i < path.Length; i++)
					{
						if (path[i] == '*')
							throw new ArgumentException(nameof(path), "Invalid wildcard position");

						if (path[i] == '.' || path[0] == '[')
						{
							subItemName = path.Slice(0, i);
							rest = pathM.Slice(i);
							break;
						}
					}
					var item = obj.GetSubItemByName(subItemName);
					if (item == null)
						return Enumerable.Empty<TomlObject>();

					return item.ByPath(rest);
				}
			}
		}

		private static TomlObject GetArrayItemByIndex(this TomlObject obj, ReadOnlySpan<char> index)
		{
			int indexNum = int.Parse(new string(index.ToArray()));
			//if (!System.Buffers.Text.Utf8Parser.TryParse(index, out int indexNum, out int bytesConsumed))
			//throw new ArgumentException("Invalid array indexer");

			switch (obj.TomlType)
			{
			case TomlObjectType.Bool:
			case TomlObjectType.Int:
			case TomlObjectType.Float:
			case TomlObjectType.String:
			case TomlObjectType.DateTime:
			case TomlObjectType.TimeSpan:
				return null; // Invalid access
			case TomlObjectType.Array:
				var tomlTable = (TomlArray)obj;
				return tomlTable[indexNum];
			case TomlObjectType.Table:
				return null;
			case TomlObjectType.ArrayOfTables:
				var tomlTableArray = (TomlTableArray)obj;
				return tomlTableArray[indexNum];
			default:
				throw new ArgumentOutOfRangeException(nameof(obj.TomlType));
			}
		}

		private static IEnumerable<TomlObject> GetAllArrayItems(this TomlObject obj)
		{
			switch (obj.TomlType)
			{
			case TomlObjectType.Bool:
			case TomlObjectType.Int:
			case TomlObjectType.Float:
			case TomlObjectType.String:
			case TomlObjectType.DateTime:
			case TomlObjectType.TimeSpan:
				return Enumerable.Empty<TomlObject>();
			case TomlObjectType.Array:
				var tomlTable = (TomlArray)obj;
				return tomlTable.Items;
			case TomlObjectType.Table:
				return Enumerable.Empty<TomlObject>();
			case TomlObjectType.ArrayOfTables:
				var tomlTableArray = (TomlTableArray)obj;
				return tomlTableArray.Items;
			default:
				throw new ArgumentOutOfRangeException(nameof(obj.TomlType));
			}
		}

		private static TomlObject GetSubItemByName(this TomlObject obj, ReadOnlySpan<char> name)
		{
			switch (obj.TomlType)
			{
			case TomlObjectType.Bool:
			case TomlObjectType.Int:
			case TomlObjectType.Float:
			case TomlObjectType.String:
			case TomlObjectType.DateTime:
			case TomlObjectType.TimeSpan:
				return null; // Invalid access
			case TomlObjectType.Array:
				return null; // TODO, not applicable ?
			case TomlObjectType.Table:
				var tomlTable = (TomlTable)obj;
				return tomlTable[new string(name.ToArray())];
			case TomlObjectType.ArrayOfTables:
				return null;
			default:
				throw new ArgumentOutOfRangeException(nameof(obj.TomlType));
			}
		}

		private static IEnumerable<TomlObject> GetAllSubItems(this TomlObject obj)
		{
			switch (obj.TomlType)
			{
			case TomlObjectType.Bool:
			case TomlObjectType.Int:
			case TomlObjectType.Float:
			case TomlObjectType.String:
			case TomlObjectType.DateTime:
			case TomlObjectType.TimeSpan:
				return Enumerable.Empty<TomlObject>();
			case TomlObjectType.Array:
				return Enumerable.Empty<TomlObject>();
			case TomlObjectType.Table:
				var tomlTable = (TomlTable)obj;
				return tomlTable.Values;
			case TomlObjectType.ArrayOfTables:
				return Enumerable.Empty<TomlObject>();
			default:
				throw new ArgumentOutOfRangeException(nameof(obj.TomlType));
			}
		}
	}

	class ConfRoot
	{
		public ConfCore core { get; set; }
	}

	class ConfCore
	{
		public string config_root { get; set; }
	}

	class ConfHistory
	{
		public bool enabled { get; set; }
		public string file { get; set; }
		public bool fill_deleted_ids { get; set; }
	}

	class TStruc
	{
		public TKey main { get; set; }
		public TKey second { get; set; }
	}

	class TKey
	{
		public string key { get; set; }
	}
}
