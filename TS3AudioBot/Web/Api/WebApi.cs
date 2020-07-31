// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Algorithm;
using TS3AudioBot.Audio;
using TS3AudioBot.CommandSystem;
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.Config;
using TS3AudioBot.Dependency;
using TS3AudioBot.Environment;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;
using TS3AudioBot.Sessions;
using TSLib;
using TSLib.Helper;

namespace TS3AudioBot.Web.Api
{
	public sealed class WebApi
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		private static readonly Uri Dummy = new Uri("http://dummy/");

		private const string ErrorNoUserOrToken = "Unknown user or no active token found";
		private const string ErrorAuthFailure = "Authentication failed";
		private const string ErrorAnonymousDisabled = "This bot does not allow anonymous api requests";
		private const string ErrorUnsupportedScheme = "Unsupported authentication scheme";

		public bool AllowAnonymousRequest { get; set; } = true;
		private readonly ConfWebApi config;
		private readonly CoreInjector coreInjector;
		private readonly TokenManager tokenManager;
		private readonly Stats stats;

		public WebApi(ConfWebApi config, CoreInjector coreInjector, TokenManager tokenManager, Stats stats)
		{
			this.config = config;
			this.coreInjector = coreInjector;
			this.tokenManager = tokenManager;
			this.stats = stats;
		}

		public async Task ProcessApiV1Call(HttpContext context)
		{
			var request = context.Request;
			var response = context.Response;

			response.ContentType = "application/json";
			response.Headers["Access-Control-Allow-Origin"] = "*";
			response.Headers["CacheControl"] = "no-cache, no-store, must-revalidate";

			var authResult = Authenticate(context.Request);
			if (!authResult.Ok)
			{
				Log.Debug("Authorization failed!");
				await ReturnError(new CommandException(authResult.Error, CommandExceptionReason.Unauthorized), response);
				return;
			}
			if (!AllowAnonymousRequest && authResult.Value.ClientUid == Uid.Null)
			{
				Log.Debug("Unauthorized request!");
				await ReturnError(new CommandException(ErrorAnonymousDisabled, CommandExceptionReason.Unauthorized), response);
				return;
			}

			var apiCallData = authResult.Value;
			var remoteAddress = context.Connection?.RemoteIpAddress;
			if (remoteAddress is null)
			{
				Log.Warn("Remote has no IP, ignoring request");
				return;
			}

			if (IPAddress.IsLoopback(remoteAddress)
				&& request.Headers.TryGetValue("X-Real-IP", out var realIpStr)
				&& IPAddress.TryParse(realIpStr, out var realIp))
			{
				remoteAddress = realIp;
			}
			apiCallData.IpAddress = remoteAddress;
			apiCallData.RequestUrl = new Uri(Dummy, context.Features.Get<IHttpRequestFeature>().RawTarget);

			Log.Info("{0} Requested: {1}", remoteAddress, apiCallData.RequestUrl.PathAndQuery);

			var command = BuildCommand(apiCallData.RequestUrl);

			if (!(await ProcessBodyData(request, apiCallData)).GetOk(out var err))
			{
				await ReturnError(err, response);
				return;
			}

			var execInfo = BuildContext(apiCallData);

			try
			{
				stats.TrackCommandApiCall();
				Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
				var res = await command.Execute(execInfo, Array.Empty<ICommand>());

				if (res is null)
				{
					response.StatusCode = (int)HttpStatusCode.NoContent;
				}
				else if (res is DataStream data)
				{
					response.StatusCode = (int)HttpStatusCode.OK;
					using (response.Body)
					{
						await data.WriteOut(response);
					}
				}
				else
				{
					if (!(res is JsonObject json))
						json = JsonValue.Create(res);

					var returnString = json.Serialize();
					response.StatusCode = returnString.Length == 0 ? (int)HttpStatusCode.NoContent : (int)HttpStatusCode.OK;
					using var responseStream = new StreamWriter(response.Body);
					await responseStream.WriteAsync(returnString);
				}
			}
			catch (Exception ex)
			{
				await ReturnError(ex, response);
			}
		}

		private ICommand BuildCommand(Uri requestUrl)
		{
			string apirequest = requestUrl.OriginalString.Substring(requestUrl.GetLeftPart(UriPartial.Authority).Length + "/api".Length);
			var ast = CommandParser.ParseCommandRequest(apirequest, '/', '/');
			UnescapeAstTree(ast);
			Log.Trace(ast.ToString);
			return CommandManager.AstToCommandResult(ast);
		}

		private ExecutionInformation BuildContext(ApiCall apiCallData)
		{
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
			return execInfo;
		}

		private async Task<E<Exception>> ProcessBodyData(HttpRequest request, ApiCall apiCallData)
		{
			if (request.ContentType != "application/json")
				return R.Ok;

			try
			{
				using (var sr = new StreamReader(request.Body, Tools.Utf8Encoder))
					apiCallData.Body = await sr.ReadToEndAsync();
				return R.Ok;
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to parse request Body");
				return ex;
			}
		}

		private static async Task ReturnError(Exception ex, HttpResponse response)
		{
			Log.Debug(ex, "Api Exception");

			try
			{
				JsonError? jsonError = null;

				switch (ex)
				{
				case CommandException cex:
					jsonError = ReturnCommandError(cex, response);
					break;

				case AudioBotException abex:
					jsonError = new JsonError(abex.Message, CommandExceptionReason.CommandError);
					break;

				case NotImplementedException _:
					response.StatusCode = (int)HttpStatusCode.NotImplemented;
					break;

				case EntityTooLargeException _:
					response.StatusCode = (int)HttpStatusCode.RequestEntityTooLarge;
					break;

				default:
					Log.Error(ex, "Unknown command error");
					response.StatusCode = (int)HttpStatusCode.InternalServerError;
					break;
				}

				jsonError ??= new JsonError(ex.Message, CommandExceptionReason.Unknown);
				using var responseStream = new StreamWriter(response.Body);
				await responseStream.WriteAsync(jsonError.Serialize());
			}
			catch (Exception htex) { Log.Warn(htex, "Failed to respond to HTTP request."); }
		}

		private static JsonError ReturnCommandError(CommandException ex, HttpResponse response)
		{
			var jsonError = new JsonError(ex.Message, ex.Reason);

			switch (ex.Reason)
			{
			case CommandExceptionReason.Unknown:
			case CommandExceptionReason.InternalError:
				response.StatusCode = (int)HttpStatusCode.InternalServerError;
				break;

			case CommandExceptionReason.Unauthorized:
				jsonError.HelpMessage += "You have to authenticate yourself to call this method.";
				jsonError.HelpLink = "https://github.com/Splamy/TS3AudioBot/wiki/WebAPI#authentication";
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
						jsonError.HelpMessage += strings.error_not_available_from_api;
					}
					else if (mcex.MissingType == typeof(UserSession))
					{
						jsonError.HelpMessage += "Creating UserSessions via api is currently not implemented yet.";
					}
					else if (mcex.MissingType == typeof(Bot) || mcex.MissingType == typeof(Player)
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
				throw Tools.UnhandledDefault(ex.Reason);
			}

			return jsonError;
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
				throw Tools.UnhandledDefault(node.Type);
			}
		}

		private R<ApiCall, string> Authenticate(HttpRequest request)
		{
			if (!request.Headers.TryGetValue("Authorization", out var headerVal))
				return ApiCall.CreateAnonymous();

			var authParts = headerVal.ToString().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
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
				userUid = Tools.Utf8Encoder.GetString(data, 0, index);
				token = Tools.Utf8Encoder.GetString(data, index + 1, data.Length - (index + 1));
			}
			catch (Exception) { return "Malformed base64 string"; }

			var dbToken = tokenManager.GetToken(userUid);
			if (dbToken is null)
				return ErrorNoUserOrToken;

			if (dbToken.Value != token)
				return ErrorAuthFailure;

			return new ApiCall((Uid)userUid, token: dbToken.Value);
		}
	}
}
