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

namespace TS3AudioBot.Web.Api
{
	using CommandSystem;
	using Helper;
	using System;
	using System.IO;
	using System.Linq;
	using System.Net;
	using TS3Client.Messages;

	public sealed class WebApi : WebComponent
	{
		public WebApi(MainBot bot) : base(bot) { }

		public override void DispatchCall(HttpListenerContext context)
		{
			using (var response = context.Response)
			{
				var requestUrl = new UriExt(new Uri(dummy, context.Request.RawUrl));
				ProcessApiV1Call(requestUrl, response);
			}
			// TODO process work here
		}

		private void ProcessApiV1Call(UriExt uri, HttpListenerResponse response)
		{
			string apirequest = uri.AbsolutePath.Substring("/api".Length);
			var ast = CommandParser.ParseCommandRequest(apirequest, '/', '/');
			UnescapeAstTree(ast);
			Log.Write(Log.Level.Debug, "API Request Tree:\n{0}", ast.ToString());

			var command = MainBot.CommandManager.CommandSystem.AstToCommandResult(ast);

			var cd = Generator.ActivateResponse<ClientData>();
			cd.NickName = "APITEST";
			cd.DatabaseId = 42;
			var execInfo = new ExecutionInformation(new UserSession(MainBot, cd), null, new Lazy<bool>(() => true));
			execInfo.SetApiCall();

			using (var token = execInfo.Session.GetLock())
			{
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
		}

		private static void ReturnError(CommandException ex, HttpListenerResponse response)
		{
			switch (ex.Reason)
			{
			case CommandExceptionReason.Unknown:
			case CommandExceptionReason.InternalError:
				response.StatusCode = (int)HttpStatusCode.InternalServerError;
				return;

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
				Log.Write(Log.Level.Debug, "WA Missing Web Error Type");
				break;
			}

			using (var responseStream = new StreamWriter(response.OutputStream))
				responseStream.Write(Util.Serializer.Serialize(new JsonError(ex.Message, ex.Reason)));
		}

		private void UnescapeAstTree(ASTNode node)
		{
			switch (node.Type)
			{
			case ASTType.Command:
				var astCom = (ASTCommand)node;
				foreach (var child in astCom.Parameter)
					UnescapeAstTree(child);
				break;
			case ASTType.Value:
				var astVal = (ASTValue)node;
				astVal.Value = Uri.UnescapeDataString(astVal.Value);
				break;
			case ASTType.Error: break;
			default: break;
			}
		}
	}
}
