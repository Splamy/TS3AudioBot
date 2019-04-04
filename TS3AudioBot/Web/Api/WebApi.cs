// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web.Api
{
	using Audio;
	using CommandSystem;
	using CommandSystem.Ast;
	using CommandSystem.CommandResults;
	using CommandSystem.Commands;
	using Config;
	using Dependency;
	using Helper;
	using Newtonsoft.Json;
	using Sessions;
	using System;
	using System.IO;
	using System.Net;
	using System.Text;

	public sealed class WebApi : WebComponent
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly ApiCall apiCallDummy = new ApiCall();

		private const string ErrorNoUserOrToken = "Unknown user or no active token found";
		private const string ErrorAuthFailure = "Authentication failed";
		private const string ErrorAnonymousDisabled = "This bot does not allow anonymous api requests";
		private const string ErrorUnsupportedScheme = "Unsupported authentication scheme";

		public bool AllowAnonymousRequest { get; set; } = true;
		private readonly ConfWebApi config;

		public CoreInjector CoreInjector { get; set; }
		public CommandManager CommandManager { get; set; }
		public TokenManager TokenManager { get; set; }

		private static readonly JsonSerializerSettings ErrorSerializeSettings = new JsonSerializerSettings
		{
			NullValueHandling = NullValueHandling.Ignore,
		};

		public WebApi(ConfWebApi config)
		{
			this.config = config;
		}

		public override bool DispatchCall(Unosquare.Labs.EmbedIO.IHttpContext context)
		{
			var response = context.Response;

			// TOD0 rework responses to track length, so we can use keep-=alive
			response.KeepAlive = false;
			response.ContentType = "application/json";
			response.Headers["Access-Control-Allow-Origin"] = "*";
			response.Headers["CacheControl"] = "no-cache, no-store, must-revalidate";

			var authResult = Authenticate(context);
			if (!authResult.Ok)
			{
				Log.Debug("Authorization failed!");
				ReturnError(new CommandException(authResult.Error, CommandExceptionReason.Unauthorized), response);
				return true;
			}
			if (!AllowAnonymousRequest && authResult.Value.IsAnonymous)
			{
				Log.Debug("Unauthorized request!");
				ReturnError(new CommandException(ErrorAnonymousDisabled, CommandExceptionReason.Unauthorized), response);
				return true;
			}

			var requestUrl = new Uri(Dummy, context.Request.RawUrl);
			ProcessApiV1Call(requestUrl, response, authResult.Value);
			return true;
		}

		private void ProcessApiV1Call(Uri uri, Unosquare.Labs.EmbedIO.IHttpResponse response, InvokerData invoker)
		{
			string apirequest = uri.OriginalString.Substring(uri.GetLeftPart(UriPartial.Authority).Length + "/api".Length);
			var ast = CommandParser.ParseCommandRequest(apirequest, '/', '/');
			UnescapeAstTree(ast);
			Log.Trace(ast.ToString);

			var command = CommandManager.CommandSystem.AstToCommandResult(ast);

			var execInfo = new ExecutionInformation(CoreInjector.CloneRealm<CoreInjector>());
			execInfo.AddDynamicObject(new CallerInfo(apirequest, true) { CommandComplexityMax = config.CommandComplexity });
			execInfo.AddDynamicObject(invoker);
			execInfo.AddDynamicObject(apiCallDummy);
			// todo creating token usersessions is now possible

			try
			{
				var res = command.Execute(execInfo, Array.Empty<ICommand>(), XCommandSystem.ReturnJsonOrNothing);

				if (res.ResultType == CommandResultType.Empty)
				{
					response.StatusCode = (int)HttpStatusCode.NoContent;
				}
				else if (res.ResultType == CommandResultType.Json)
				{
					response.StatusCode = (int)HttpStatusCode.OK;
					var returnJson = (JsonCommandResult)res;
					var returnString = returnJson.JsonObject.Serialize();
					using (var responseStream = new StreamWriter(response.OutputStream))
						responseStream.Write(returnString);
				}
			}
			catch (CommandException ex)
			{
				try { ReturnError(ex, response); }
				catch (Exception htex) { Log.Error(htex, "Failed to respond to HTTP request."); }
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Unexpected command error");
				try
				{
					if (ex is NotImplementedException)
						response.StatusCode = (int)HttpStatusCode.NotImplemented;
					else
						response.StatusCode = (int)HttpStatusCode.InternalServerError;

					using (var responseStream = new StreamWriter(response.OutputStream))
						responseStream.Write(new JsonError(ex.Message, CommandExceptionReason.Unknown).Serialize());
				}
				catch (Exception htex) { Log.Error(htex, "Failed to respond to HTTP request."); }
			}
		}

		private static void ReturnError(CommandException ex, Unosquare.Labs.EmbedIO.IHttpResponse response)
		{
			var jsonError = new JsonError(ex.Message, ex.Reason);

			switch (ex.Reason)
			{
			case CommandExceptionReason.Unknown:
			case CommandExceptionReason.InternalError:
				response.StatusCode = (int)HttpStatusCode.InternalServerError;
				return;

			case CommandExceptionReason.Unauthorized:
				response.StatusCode = (int)HttpStatusCode.Unauthorized;
				break;

			case CommandExceptionReason.MissingRights:
				jsonError.HelpLink = "https://github.com/Splamy/TS3AudioBot/wiki/FAQ#missing-rights";
				response.StatusCode = (int)HttpStatusCode.Forbidden;
				break;

			case CommandExceptionReason.AmbiguousCall:
			case CommandExceptionReason.MissingParameter:
			case CommandExceptionReason.NotSupported:
				response.StatusCode = (int)HttpStatusCode.BadRequest;
				break;

			case CommandExceptionReason.MissingContext:
				if (ex is MissingContextCommandException mcex)
				{
					if (mcex.MissingType == typeof(InvokerData))
					{
						jsonError.HelpMessage += "You have to authenticate yourself to call this method.";
						jsonError.HelpLink = "https://github.com/Splamy/TS3AudioBot/wiki/WebAPI#authentication";
					}
					else if (mcex.MissingType == typeof(UserSession))
					{
						jsonError.HelpMessage += "Creating UserSessions via api is currently not implemented yet.";
					}
					else if (mcex.MissingType == typeof(Bot) || mcex.MissingType == typeof(IPlayerConnection)
						|| mcex.MissingType == typeof(PlayManager) || mcex.MissingType == typeof(Ts3Client)
						|| mcex.MissingType == typeof(IVoiceTarget) || mcex.MissingType == typeof(IVoiceTarget)
						|| mcex.MissingType == typeof(ConfBot))
					{
						jsonError.HelpMessage += "You are trying to call a command which is specific to a bot. " +
							"Use '!bot use' to switch to a bot instance";
						jsonError.HelpLink = "https://github.com/Splamy/TS3AudioBot/wiki/FAQ#api-missing-context";
					}
				}
				goto case CommandExceptionReason.CommandError;

			case CommandExceptionReason.CommandError:
			case CommandExceptionReason.NoReturnMatch:
				response.StatusCode = 422; // Unprocessable Entity
				break;

			case CommandExceptionReason.FunctionNotFound:
				response.StatusCode = (int)HttpStatusCode.NotFound;
				break;

			default:
				throw Util.UnhandledDefault(ex.Reason);
			}

			using (var responseStream = new StreamWriter(response.OutputStream))
			{
				responseStream.Write(JsonConvert.SerializeObject(jsonError, ErrorSerializeSettings));
			}
		}

		private static void UnescapeAstTree(AstNode node)
		{
			switch (node.Type)
			{
			case AstType.Command:
				var astCom = (AstCommand)node;
				foreach (var child in astCom.Parameter)
					UnescapeAstTree(child);
				break;
			case AstType.Value:
				var astVal = (AstValue)node;
				astVal.Value = Uri.UnescapeDataString(astVal.Value);
				break;
			case AstType.Error: break;
			default:
				throw Util.UnhandledDefault(node.Type);
			}
		}

		private R<InvokerData, string> Authenticate(Unosquare.Labs.EmbedIO.IHttpContext context)
		{
			var headerVal = context.Request.Headers["Authorization"];
			if (string.IsNullOrEmpty(headerVal))
				return InvokerData.Anonymous;

			var authParts = headerVal.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
			if (authParts.Length < 2)
				return ErrorAuthFailure;

			if (!string.Equals(authParts[0], "BASIC", StringComparison.OrdinalIgnoreCase))
				return ErrorUnsupportedScheme;

			string userUid;
			string token;
			try
			{
				var data = Convert.FromBase64String(authParts[1]);
				var index = Array.IndexOf(data, (byte)':');

				if (index < 0)
					return ErrorAuthFailure;
				userUid = Encoding.UTF8.GetString(data, 0, index);
				token = Encoding.UTF8.GetString(data, index + 1, data.Length - (index + 1));
			}
			catch (Exception) { return "Malformed base64 string"; }

			var result = TokenManager.GetToken(userUid);
			if (!result.Ok)
				return ErrorNoUserOrToken;

			var dbToken = result.Value;
			if (dbToken.Value != token)
				return ErrorAuthFailure;

			return new InvokerData(userUid, token: dbToken.Value);
		}
	}
}
