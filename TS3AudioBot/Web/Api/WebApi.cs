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
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using Helper;
	using History;
	using HtmlAgilityPack;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading;
	using System.Web;
	using System.Globalization;
	using TS3Client.Messages;
	using CommandSystem;

	class WebApi
	{
		private WebApiData webApiData;
		private readonly Uri localhost;
		private readonly Uri[] hostPaths;
		private HttpListener webListener;
		private Thread serverThread;
		private MainBot mainBot;

		public WebApi(MainBot mainBot, WebApiData wad)
		{
			webApiData = wad;
			this.mainBot = mainBot;

			localhost = new Uri($"http://localhost:{wad.Port}/");

			if (Util.IsAdmin || Util.IsLinux) // todo: hostlist
			{
				hostPaths = new[] {
					new Uri($"http://splamy.de:{wad.Port}/"),
					localhost,
				};
			}
			else
			{
				Log.Write(Log.Level.Warning, "App launched without elevated rights. Only localhost will be availbale as api server.");
				hostPaths = new[] { localhost };
			}
		}

		public void StartServerAsync()
		{
			serverThread = new Thread(EnterWebLoop);
			serverThread.Name = "WebInterface";
			serverThread.Start();
		}

		public void EnterWebLoop()
		{
			if (!webApiData.Enabled)
				return;

			using (webListener = new HttpListener())
			{
				foreach (var host in hostPaths)
					webListener.Prefixes.Add(host.AbsoluteUri);

				try { webListener.Start(); }
				catch (HttpListenerException ex)
				{
					Log.Write(Log.Level.Error, "The web api server could not be started ({0})", ex.Message);
					return;
				} // TODO

				while (webListener.IsListening)
				{
					HttpListenerContext context;
					try { context = webListener.GetContext(); }
					catch (HttpListenerException) { break; }
					catch (InvalidOperationException) { break; }

					using (var response = context.Response)
					{
						var requestUrl = new UriExt(new Uri(localhost, context.Request.RawUrl));
						Log.Write(Log.Level.Debug, "API Request: {0}", requestUrl.PathAndQuery);

						if (requestUrl.AbsolutePath.StartsWith("/api/", true, CultureInfo.InvariantCulture))
						{
							ProcessApiV1Call(requestUrl, response);
						}
					}
					// TODO process work here
				}
			}
		}

		private void ProcessApiV1Call(UriExt uri, HttpListenerResponse response)
		{
			string apirequest = uri.AbsolutePath.Substring("/api".Length);
			var ast = CommandParser.ParseCommandRequest(apirequest, '/', '/');
			UnescapeAstTree(ast);
			Log.Write(Log.Level.Debug, "API Request Tree:\n{0}", ast.ToString());

			var command = mainBot.CommandManager.CommandSystem.AstToCommandResult(ast);

			var cd = Generator.ActivateResponse<ClientData>();
			cd.NickName = "APITEST";
			cd.DatabaseId = 42;
			var execInfo = new ExecutionInformation(new UserSession(mainBot, cd), null, new Lazy<bool>(() => true));
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

	class WebApiData : ConfigData
	{
		[Info("the port for the api server", "8180")]
		public ushort Port { get; set; }

		[Info("if you want to start the web api server.", "false")]
		public bool Enabled { get; set; }

		[Info("a comma seperated list of all urls the web api should be possible to be accessed with", "")]
		public string HostAddress { get; set; }
	}
}
