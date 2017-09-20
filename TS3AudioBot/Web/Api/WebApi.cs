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
	using CommandSystem;
	using Helper;
	using Sessions;
	using System;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Security.Cryptography;
	using System.Security.Principal;
	using System.Text.RegularExpressions;
	using System.Text;

	public sealed class WebApi : WebComponent
	{
		private static readonly Regex DigestMatch = new Regex(@"\s*(\w+)\s*=\s*""([^""]*)""\s*,?", Util.DefaultRegexConfig);
		private static readonly MD5 Md5Hash = MD5.Create();

		public WebApi(MainBot bot) : base(bot) { }

		public override void DispatchCall(HttpListenerContext context)
		{
			using (var response = context.Response)
			{
				response.AddHeader("Access-Control-Allow-Origin", "*");

				var invoker = Authenticate(context);
				if (invoker == null)
				{
					Log.Write(Log.Level.Debug, "Not authorized!");
					ReturnError(CommandExceptionReason.Unauthorized, "", context.Response);
					return;
				}

				var requestUrl = new Uri(Dummy, context.Request.RawUrl);
				ProcessApiV1Call(requestUrl, context.Response, invoker);
			}
		}

		private void ProcessApiV1Call(Uri uri, HttpListenerResponse response, InvokerData invoker)
		{
			string apirequest = uri.AbsolutePath.Substring("/api".Length);
			var ast = CommandParser.ParseCommandRequest(apirequest, '/', '/');
			UnescapeAstTree(ast);

			var command = MainBot.CommandManager.CommandSystem.AstToCommandResult(ast);

			invoker.IsApi = true;
			var execInfo = new ExecutionInformation(MainBot, invoker, null);
			try
			{
				var res = command.Execute(execInfo, Enumerable.Empty<ICommand>(),
					new[] { CommandResultType.Json, CommandResultType.Empty });

				if (res.ResultType == CommandResultType.Empty)
				{
					response.StatusCode = (int)HttpStatusCode.NoContent;
				}
				else if (res.ResultType == CommandResultType.Json)
				{
					response.StatusCode = (int)HttpStatusCode.OK;
					var sRes = (JsonCommandResult)res;
					using (var responseStream = new StreamWriter(response.OutputStream))
						responseStream.Write(sRes.JsonObject.Serialize());
				}
			}
			catch (CommandException ex)
			{
				ReturnError(ex, response);
			}
			catch (Exception ex)
			{
				if (ex is NotImplementedException)
					response.StatusCode = (int)HttpStatusCode.NotImplemented;
				else
					response.StatusCode = (int)HttpStatusCode.InternalServerError;
				Log.Write(Log.Level.Error, "WA Unexpected command error: {0}", ex);
				using (var responseStream = new StreamWriter(response.OutputStream))
					responseStream.Write(new JsonError(ex.Message, CommandExceptionReason.Unknown).Serialize());
			}
		}

		private static void ReturnError(CommandException ex, HttpListenerResponse response) => ReturnError(ex.Reason, ex.Message, response);

		private static void ReturnError(CommandExceptionReason reason, string message, HttpListenerResponse response)
		{
			switch (reason)
			{
			case CommandExceptionReason.Unknown:
			case CommandExceptionReason.InternalError:
				response.StatusCode = (int)HttpStatusCode.InternalServerError;
				return;

			case CommandExceptionReason.Unauthorized:
				response.StatusCode = (int)HttpStatusCode.Unauthorized;
				break;

			case CommandExceptionReason.MissingRights:
			case CommandExceptionReason.NotSupported:
				response.StatusCode = (int)HttpStatusCode.Forbidden;
				break;

			case CommandExceptionReason.CommandError:
			case CommandExceptionReason.AmbiguousCall:
			case CommandExceptionReason.MissingParameter:
			case CommandExceptionReason.NoReturnMatch:
				response.StatusCode = 422; // Unprocessable Entity
				break;

			case CommandExceptionReason.FunctionNotFound:
				response.StatusCode = (int)HttpStatusCode.NotFound;
				break;

			default:
				throw Util.UnhandledDefault(reason);
			}

			using (var responseStream = new StreamWriter(response.OutputStream))
				responseStream.Write(Util.Serializer.Serialize(new JsonError(message, reason)));
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

		private InvokerData Authenticate(HttpListenerContext context)
		{
			IIdentity identity = GetIdentity(context);
			if (identity == null)
				return null;

			var result = MainBot.SessionManager.GetToken(identity.Name);
			if (!result.Ok)
				return null;

			var token = result.Value;
			var invoker = new InvokerData(identity.Name)
			{
				IsApi = true,
				Token = token.Value,
			};

			switch (identity.AuthenticationType)
			{
			case "Basic":
				var identityBasic = (HttpListenerBasicIdentity)identity;

				if (token.Value != identityBasic.Password)
					return null;

				return invoker;
			case "Digest":
				var identityDigest = (HttpListenerDigestIdentity)identity;

				if (!identityDigest.IsAuthenticated)
				{
					var newNonce = token.CreateNonce();
					context.Response.AddHeader("WWW-Authenticate", $"Digest realm=\"{WebManager.WebRealm}\", nonce=\"{newNonce.Value}\"");
					return null;
				}

				if (identityDigest.Realm != WebManager.WebRealm)
					return null;

				if (identityDigest.Uri != context.Request.RawUrl)
					return null;

				//HA1=MD5(username:realm:password)
				//HA2=MD5(method:digestURI)
				//response=MD5(HA1:nonce:HA2)
				var ha1 = HashString($"{identity.Name}:{identityDigest.Realm}:{token.Value}");
				var ha2 = HashString($"{context.Request.HttpMethod}:{identityDigest.Uri}");
				var response = HashString($"{ha1}:{identityDigest.Nonce}:{ha2}");

				if (identityDigest.Hash != response)
					return null;

				ApiNonce nextNonce = token.UseNonce(identityDigest.Nonce);
				if (nextNonce == null)
					return null;
				context.Response.AddHeader("WWW-Authenticate", $"Digest realm=\"{WebManager.WebRealm}\", nonce=\"{nextNonce.Value}\"");

				return invoker;
			default:
				return null;
			}
		}

		private static IIdentity GetIdentity(HttpListenerContext context)
		{
			IIdentity identity = context.User?.Identity;
			if (identity != null)
				return identity;

			var headerVal = context.Request.Headers["Authorization"];
			if (string.IsNullOrEmpty(headerVal))
				return null;

			var authParts = headerVal.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
			if (authParts.Length < 2)
				return null;

			if (string.Equals(authParts[0], "DIGEST", StringComparison.OrdinalIgnoreCase))
			{
				string name = null;
				string hash = null;
				string nonce = null;
				string realm = null;
				string uri = null;

				for (var match = DigestMatch.Match(authParts[1]); match.Success; match = match.NextMatch())
				{
					var value = match.Groups[2].Value;
					switch (match.Groups[1].Value.ToUpper())
					{
					case "USERNAME": name = value; break;
					case "REALM": realm = value; break;
					case "NONCE": nonce = value; break;
					case "RESPONSE": hash = value; break;
					case "URI": uri = value; break;
					}
				}

				return new HttpListenerDigestIdentity(name, nonce, hash, realm, uri);
			}

			return null;
		}

		private static string HashString(string input)
		{
			var bytes = Util.Utf8Encoder.GetBytes(input);
			var hash = Md5Hash.ComputeHash(bytes);

			var result = new StringBuilder(hash.Length * 2);
			for (int i = 0; i < hash.Length; i++)
				result.Append(hash[i].ToString("x2"));
			return result.ToString();
		}
	}
}
