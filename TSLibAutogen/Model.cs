using Microsoft.CodeAnalysis;
using Nett;
using NotVisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TSLibAutogen
{
	public record Model
	(
		List<Struct> Book,
		Messages Messages,
		List<M2BRule> M2B,
		List<TsError> Errors,
		List<TsPermission> Permissions,
		List<Version> Versions
	);

	public class ModelBuilder
	{
		private readonly List<TomlStruct> BookStructs = new();
		private readonly List<Field> MessagesFields = new();
		private readonly List<MsgGroup> MessagesGroups = new();
		private readonly List<TomlM2BRule> M2BRules = new();
		private readonly List<TsError> Errors = new();
		private readonly List<TsPermission> Permissions = new();
		private readonly List<Version> Versions = new();

		public void AddBook(string src)
		{
			var bookParsed = Toml.ReadString<Book>(src);
			BookStructs.AddRange(bookParsed.@struct);
		}

		public void AddMessages(string src)
		{
			var messagesParsed = Toml.ReadString<TomlMessages>(src);
			MessagesFields.AddRange(messagesParsed.fields);
			MessagesGroups.AddRange(messagesParsed.msg_group);
		}

		public void AddM2B(string src)
		{
			var m2bDecls = Toml.ReadString<M2BDeclarations>(src);
			M2BRules.AddRange(m2bDecls.rule);
		}

		public void AddErrors(string src)
		{
			using var mem = new StringReader(src);
			using var parser = new CsvTextFieldParser(mem);

			var header = parser.ReadFields();
			int iname = Array.IndexOf(header, "name");
			int idoc = Array.IndexOf(header, "doc");
			int inum = Array.IndexOf(header, "num");

			while (!parser.EndOfData)
			{
				var line = parser.ReadFields();
				Errors.Add(new(line[iname], line[idoc], line[inum]));
			}
		}

		public void AddPermissions(string src)
		{
			using var mem = new StringReader(src);
			using var parser = new CsvTextFieldParser(mem);

			var header = parser.ReadFields();
			int iname = Array.IndexOf(header, "name");
			int idoc = Array.IndexOf(header, "doc");

			while (!parser.EndOfData)
			{
				var line = parser.ReadFields();
				Permissions.Add(new(line[iname], line[idoc]));
			}
		}

		public void AddVersions(string src)
		{
			using var mem = new StringReader(src);
			using var parser = new CsvTextFieldParser(mem);

			var header = parser.ReadFields();
			int iname = Array.IndexOf(header, "version");
			int iplat = Array.IndexOf(header, "platform");
			int ihash = Array.IndexOf(header, "hash");

			while (!parser.EndOfData)
			{
				var line = parser.ReadFields();
				Versions.Add(new(line[iname], line[iplat], line[ihash]));
			}
		}

		public Model Build(GeneratorExecutionContext context)
		{
			var book = BuildBook();
			var messages = BuildMessages();
			var m2b = BuildM2B(messages, book, context);

			// Remove 'low' duplicate commands
			var lows = messages.Msgs.Where(m => m.low).ToArray();
			foreach (var low in lows)
			{
				if (messages.Msgs.Any(m => m.Name == low.Name))
				{
					messages.Msgs.Remove(low);
					m2b.RemoveAll(rule => rule.From == low);
				}
			}

			return new Model(book, messages, m2b, Errors, Permissions, Versions);
		}

		private List<Struct> BuildBook()
		{
			var bookStructs = new List<Struct>();

			foreach (var struc in BookStructs)
			{
				bookStructs.Add(new()
				{
					Name = struc.name,
					Optional = struc.opt ?? false,
					Doc = struc.doc,
					Id = struc.id,
					Properties = struc.properties
				});
			}
			// Sorting is optional
			bookStructs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
			bookStructs.ForEach(s => Array.Sort(s.Properties, (a, b) => string.CompareOrdinal(a.name, b.name)));

			return bookStructs;
		}

		private Messages BuildMessages()
		{
			var fldDict = MessagesFields.ToDictionary(x => x.map);
			var msgs = new List<Message>();
			foreach (var group in MessagesGroups)
			{
				foreach (var tomlmsg in group.msg)
				{
					var msg = new Message
					{
						Name = tomlmsg.name,
						Notify = tomlmsg.notify,
						Fields = tomlmsg.attributes.Select(a =>
						{
							var name = a.TrimEnd('?');
							var optional = a.EndsWith("?");
							if (!fldDict.TryGetValue(name, out var field))
								throw new Exception($"Failed to read messages. Could not find field '{name}' for '{tomlmsg.name}'.");
							return new FieldUse(field, optional);
						}).ToArray()
					};
					Array.Sort(msg.Fields, (a, b) => string.CompareOrdinal(a.Field.pretty, b.Field.pretty)); // Optional

					msg.s2c = tomlmsg.s2c ?? group.@default.s2c ?? throw new Exception($"No 's2c' specification for '{tomlmsg.name}'");
					msg.c2s = tomlmsg.c2s ?? group.@default.c2s ?? throw new Exception($"No 'c2s' specification for '{tomlmsg.name}'");
					msg.response = tomlmsg.response ?? group.@default.response ?? throw new Exception($"No 'response' specification for '{tomlmsg.name}'");
					msg.low = tomlmsg.low ?? group.@default.low ?? throw new Exception($"No 'low' specification for '{tomlmsg.name}'");
					msg.np = tomlmsg.np ?? group.@default.np ?? throw new Exception($"No 'np' specification for '{tomlmsg.name}'");

					msgs.Add(msg);
				}
			}

			msgs.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name)); // Optional

			return new Messages(
				msgs,
				fldDict["return_code"]
			);
		}

		private List<M2BRule> BuildM2B(Messages messages, List<Struct> book, GeneratorExecutionContext context)
		{
			var m2bs = new List<M2BRule>();

			foreach (var rule in M2BRules)
			{
				rule.properties ??= new List<M2BPropMove>();

				var msg = messages.Msgs.FirstOrDefault(x => x.Name == rule.from) ?? throw context.ParseError($"Rule {rule.from}->{rule.to} has no matching message");
				var msgProps = msg.Fields;
				var bookItem = book.FirstOrDefault(x => x.Name == rule.to) ?? throw context.ParseError($"Rule {rule.from}->{rule.to} has no matching book");
				var funcResults = new HashSet<string>(rule.properties.Where(x => x.tolist != null).SelectMany(x => x.tolist));

				// Make all implicit assignments explicit
				foreach (var (field, _) in msgProps)
				{
					if (funcResults.Contains(field.pretty))
						continue;

					if (bookItem.Properties.Any(x => x.name == field.pretty))
					{
						rule.properties.Add(new M2BPropMove
						{
							from = field.pretty,
							to = field.pretty,
						});
					}
				}

				m2bs.Add(new()
				{
					From = msg,
					To = bookItem,
					id = rule.id,
					operation = rule.operation,
					properties = rule.properties,
				});
			}

			return m2bs;
		}
	}

#pragma warning disable CS8618, IDE1006

	// Book.toml
	public class Book
	{
		public TomlStruct[] @struct { get; set; }
	}

	public class TomlStruct
	{
		public string name { get; set; }
		public Id[] id { get; set; }
		public string doc { get; set; }
		public bool? opt { get; set; }
		public Property[] properties { get; set; }
	}

	public class Struct
	{
		public string Name { get; set; }
		public bool Optional { get; set; }
		public Id[] Id { get; set; }
		public string Doc { get; set; }
		public Property[] Properties { get; set; }
	}

	public class Id
	{
		public string @struct { get; set; }
		public string prop { get; set; }
	}

	public class Property
	{
		public string name { get; set; }
		public string type { get; set; }
		public bool? opt { get; set; }
		public string mod { get; set; }
		public string key { get; set; }
	}

	// Messages.toml
	public record FieldUse(Field Field, bool optional);
	public record Messages(List<Message> Msgs, Field ReturnCode)
	{
		public IEnumerable<Message> Notifies => Msgs.Where(x => x.Notify is not null);
	}

	public class Message
	{
		public string Name { get; set; }
		public string? Notify { get; set; }
		public FieldUse[] Fields { get; set; }

		public bool s2c { get; set; }
		public bool c2s { get; set; }
		public bool response { get; set; }
		public bool low { get; set; }
		public bool np { get; set; }
	}

	public class TomlMessages
	{
		public Field[] fields { get; set; }
		public MsgGroup[] msg_group { get; set; }
	}

	public class Field
	{
		public string map { get; set; }
		public string ts { get; set; }
		public string pretty { get; set; }
		public string type { get; set; }

		public string mod { get; set; }
		public string doc { get; set; }

		public bool isArray => mod == "array";
		public string TypeFin(bool optional)
		{
			string ltype = type switch
			{
				"PermissionId" => "Ts3Permission",
				_ => type,
			};
			return ltype + (isArray ? "[]" : "") + (optional ? "?" : "");
		}
	}

	public class MsgGroup
	{
		public Default @default { get; set; }
		public Msg[] msg { get; set; }
	}

	public class Default
	{
		public bool? s2c { get; set; }
		public bool? c2s { get; set; }
		public bool? response { get; set; }
		public bool? low { get; set; }
		public bool? np { get; set; }
	}

	public class Msg
	{
		public string name { get; set; }
		public string? notify { get; set; }
		public string[] attributes { get; set; }

		public bool? s2c { get; set; }
		public bool? c2s { get; set; }
		public bool? response { get; set; }
		public bool? low { get; set; }
		public bool? np { get; set; }
	}

	// messagesToBook.toml

	public class M2BDeclarations
	{
		public List<TomlM2BRule> rule { get; set; }
	}

	public class M2BRule
	{
		public Message From { get; set; }
		public string[] id { get; set; }
		public Struct To { get; set; }
		public string operation { get; set; }
		public List<M2BPropMove> properties { get; set; }
	}

	public class TomlM2BRule
	{
		public string from { get; set; }
		public string[] id { get; set; }
		public string to { get; set; }
		public string operation { get; set; }
		public List<M2BPropMove> properties { get; set; }
	}

	public class M2BPropMove
	{
		public string? from { get; set; }
		public string? to { get; set; }
		public string? function { get; set; }
		public string[]? tolist { get; set; }
		public string? operation { get; set; }
	}

	// Error.csv
	public record TsError(string Name, string Doc, string Value);

	// Permission.csv
	public record TsPermission(string Name, string Doc);

	// Version.csv
	public record Version(string Build, string Platform, string Hash);

#pragma warning restore CS8618, IDE1006
}
