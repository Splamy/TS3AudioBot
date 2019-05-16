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
	using Sessions;
	using System;
	using System.IO;
	using System.Net;
	using System.Text;
	using TS3AudioBot.Algorithm;

	public sealed class WebApi : WebComponent
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private const string ErrorNoUserOrToken = "Unknown user or no active token found";
		private const string ErrorAuthFailure = "Authentication failed";
		private const string ErrorAnonymousDisabled = "This bot does not allow anonymous api requests";
		private const string ErrorUnsupportedScheme = "Unsupported authentication scheme";

		public bool AllowAnonymousRequest { get; set; } = true;
		private readonly ConfWebApi config;
		private readonly CoreInjector coreInjector;
		private readonly CommandManager commandManager;
		private readonly TokenManager tokenManager;

		public WebApi(ConfWebApi config, CoreInjector coreInjector, CommandManager commandManager, TokenManager tokenManager)
		{
			this.config = config;
			this.coreInjector = coreInjector;
			this.commandManager = commandManager;
			this.tokenManager = tokenManager;
		}

		public override bool DispatchCall(Unosquare.Labs.EmbedIO.IHttpContext context)
		{
			var response = context.Response;

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
			if (!AllowAnonymousRequest && string.IsNullOrEmpty(authResult.Value.uid))
			{
				Log.Debug("Unauthorized request!");
				ReturnError(new CommandException(ErrorAnonymousDisabled, CommandExceptionReason.Unauthorized), response);
				return true;
			}

			var apiCallData = string.IsNullOrEmpty(authResult.Value.uid)
				? ApiCall.CreateAnonymous()
				: new ApiCall(authResult.Value.uid);
			apiCallData.Token = authResult.Value.token;
			apiCallData.ReuqestUrl = (Uri)context.Items["req"];
			apiCallData.IpAddress = (IPAddress)context.Items["ip"];

			ProcessApiV1Call(response, apiCallData);
			return true;
		}

		private void ProcessApiV1Call(Unosquare.Labs.EmbedIO.IHttpResponse response, ApiCall apiCallData)
		{
			string apirequest = apiCallData.ReuqestUrl.OriginalString.Substring(apiCallData.ReuqestUrl.GetLeftPart(UriPartial.Authority).Length + "/api".Length);
			var ast = CommandParser.ParseCommandRequest(apirequest, '/', '/');
			UnescapeAstTree(ast);
			Log.Trace(ast.ToString);

			var command = commandManager.CommandSystem.AstToCommandResult(ast);

			var execInfo = new ExecutionInformation(coreInjector);
			execInfo.AddModule(new CallerInfo(true)
			{
				SkipRightsChecks = false,
				CommandComplexityMax = config.CommandComplexity,
				IsColor = false,
			});
			execInfo.AddModule<InvokerData>(apiCallData);
			execInfo.AddModule(apiCallData);
			execInfo.AddModule(Filter.GetFilterByNameOrDefault(config.Matcher));

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
				catch (Exception htex) { Log.Warn(htex, "Failed to respond to HTTP request."); }
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
				catch (Exception htex) { Log.Warn(htex, "Failed to respond to HTTP request."); }
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
					if (mcex.MissingType == typeof(ClientCall))
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
				responseStream.Write(jsonError.Serialize());
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

		private R<(string uid, string token), string> Authenticate(Unosquare.Labs.EmbedIO.IHttpContext context)
		{
			var headerVal = context.Request.Headers["Authorization"];
			if (string.IsNullOrEmpty(headerVal))
				return (null, null);

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

			var result = tokenManager.GetToken(userUid);
			if (!result.Ok)
				return ErrorNoUserOrToken;

			var dbToken = result.Value;
			if (dbToken.Value != token)
				return ErrorAuthFailure;

			return (userUid, dbToken.Value);
		}
	}
}
