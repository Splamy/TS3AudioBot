// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Commands;
using TSLib.Helper;

namespace TS3AudioBot.Web.Api
{
	public static class OpenApiGenerator
	{
		private static readonly JsonSerializer seri = JsonSerializer.CreateDefault();

		static OpenApiGenerator()
		{
			seri.NullValueHandling = NullValueHandling.Ignore;
		}

		public static JObject Generate(CommandManager commandManager, BotInfo[] bots)
		{
			var paths = new JObject();

			var addedCommandPaths = new HashSet<string>();

			foreach (var command in commandManager.AllCommands)
			{
				var token = GenerateCommand(commandManager, command, addedCommandPaths);
				if (token != null)
					paths.Add(token);
			}

			const string defaultAuthSchemeName = "default_basic";

			return
			new JObject(
				new JProperty("openapi", "3.0.0"),
				JPropObj("info",
					new JProperty("version", "1.0.0"),
					new JProperty("title", "Ts3AudioBot API"),
					new JProperty("description", "The Ts3AudioBot api interface.")
				),
				new JProperty("paths",
					paths
				),
				new JProperty("servers",
					new JArray(
						new JObject(
							new JProperty("url", "/api"),
							new JProperty("description", "Your Ts3AudioBot server.")
						)
					).Chain(x =>
					{
						foreach (var bot in bots)
						{
							x.Add(new JObject(
								new JProperty("url", $"/api/bot/use/{bot.Id}/(/"),
								new JProperty("description", $"Bot {bot.Name}")
							));
						}
					})
				),
				JPropObj("components",
					JPropObj("securitySchemes",
						JPropObj(defaultAuthSchemeName,
							new JProperty("type", "http"),
							new JProperty("scheme", "basic")
						)
					)
				),
				new JProperty("security",
					new JArray(
						new JObject(
							new JProperty(defaultAuthSchemeName, new JArray())
						)
					)
				)
			);
		}

		private static JToken GenerateCommand(CommandManager commandManager, BotCommand command, HashSet<string> addedCommandPaths)
		{
			var parameters = new JArray();

			var pathBuilder = new StringBuilder();
			pathBuilder.Append("/");
			pathBuilder.Append(command.InvokeName.Replace(' ', '/'));
			foreach (var param in command.CommandParameter)
			{
				switch (param.Kind)
				{
				case ParamKind.Unknown:
					break;
				case ParamKind.SpecialArguments:
				case ParamKind.SpecialReturns:
					break;
				case ParamKind.Dependency:
					break;
				case ParamKind.NormalCommand:
				case ParamKind.NormalParam:
				case ParamKind.NormalArray:
				case ParamKind.NormalTailString:
					if (param.Kind == ParamKind.NormalArray)
						pathBuilder.Append("/{").Append(param.Name).Append("}...");
					else
						pathBuilder.Append("/{").Append(param.Name).Append("}");

					var addparam = new JObject(
						new JProperty("name", param.Name),
						new JProperty("in", "path"),
						new JProperty("description", "useful help"),
						new JProperty("required", true) // param.optional
					);

					var paramschema = NormalToSchema(param.Type);
					if (paramschema != null)
						addparam.Add("schema", JObject.FromObject(paramschema, seri));
					parameters.Add(addparam);
					break;
				default:
					throw Tools.UnhandledDefault(param.Kind);
				}
			}

			var path = pathBuilder.ToString();

			if (addedCommandPaths.Contains(path))
				return null;
			addedCommandPaths.Add(path);

			// check tag

			var tags = new JArray();
			int spaceIndex = command.InvokeName.IndexOf(' ');
			string baseName = spaceIndex >= 0 ? command.InvokeName.Substring(0, spaceIndex) : command.InvokeName;
			var commandroot = commandManager.CommandSystem.RootCommand.GetCommand(baseName);
			switch (commandroot)
			{
			case null:
				break;
			case CommandGroup group:
				tags.Add(baseName);
				break;
			}

			// build final command

			var reponseschema = NormalToSchema(command.CommandReturn);

			return
			JPropObj(path,
				JPropObj("get",
					new JProperty("tags", tags),
					new JProperty("description", command.Description),
					new JProperty("parameters", parameters),
					new JProperty("responses",
						new JObject().Chain(r =>
						{
							if (reponseschema != null)
							{
								r.Add(
									JPropObj("200",
										new JProperty("description", "Successful"),
										new JProperty("content",
											new JObject(
												JPropObj("application/json",
													new JProperty("schema", JObject.FromObject(reponseschema, seri))
												)
											)
										)
									)
								);
							}
							else
							{
								r.Add(
									JPropObj("204",
										new JProperty("description", "Successful")
									)
								);
							}
						})
					)
				)
			);
		}

		private static T Chain<T>(this T token, Action<T> func) where T : JToken
		{
			func.Invoke(token);
			return token;
		}

		private static JProperty JPropObj(string name, params object[] token)
		{
			return new JProperty(name, new JObject(token));
		}

		private static OApiSchema NormalToSchema(Type type)
		{
			type = FunctionCommand.UnwrapReturnType(type);

			if (type.IsArray)
			{
				return new OApiSchema
				{
					type = "array",
					items = NormalToSchema(type.GetElementType())
				};
			}

			if (type == typeof(bool)) return OApiSchema.FromBasic("boolean");
			else if (type == typeof(sbyte)) return OApiSchema.FromBasic("integer", "int8");
			else if (type == typeof(byte)) return OApiSchema.FromBasic("integer", "uint8");
			else if (type == typeof(short)) return OApiSchema.FromBasic("integer", "int16");
			else if (type == typeof(ushort)) return OApiSchema.FromBasic("integer", "uint16");
			else if (type == typeof(int)) return OApiSchema.FromBasic("integer", "int32");
			else if (type == typeof(uint)) return OApiSchema.FromBasic("integer", "uint32");
			else if (type == typeof(long)) return OApiSchema.FromBasic("integer", "int64");
			else if (type == typeof(ulong)) return OApiSchema.FromBasic("integer", "uint64");
			else if (type == typeof(float)) return OApiSchema.FromBasic("number", "float");
			else if (type == typeof(double)) return OApiSchema.FromBasic("number", "double");
			else if (type == typeof(TimeSpan)) return OApiSchema.FromBasic("string", "duration");
			else if (type == typeof(DateTime)) return OApiSchema.FromBasic("string", "date-time");
			else if (type == typeof(string)) return OApiSchema.FromBasic("string", null);
			else if (type == typeof(JsonEmpty) || type == typeof(void)) return null;
			else if (type == typeof(JsonObject) || type == typeof(object)) return OApiSchema.FromBasic("object");
			else if (type == typeof(ICommand)) return OApiSchema.FromBasic("λ");
			else
			{
				return OApiSchema.FromBasic(type.Name);
			}
		}

		private class OApiSchema
		{
			public string type { get; set; }
			public string format { get; set; }
			public OApiSchema additionalProperties { get; set; }
			public OApiSchema items { get; set; }

			public static OApiSchema FromBasic(string type, string format = null) => new OApiSchema { type = type, format = format };

			public OApiSchema ObjWrap() => new OApiSchema { type = "object", additionalProperties = this };
		}
	}
}
