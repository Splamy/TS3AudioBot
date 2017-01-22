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

namespace TS3Client.Query
{
	using Commands;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Sockets;

	public sealed class Ts3QueryClient : Ts3BaseClient
	{
		private readonly TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;

		public override ClientType ClientType => ClientType.Query;

		public Ts3QueryClient(EventDispatchType dispatcher) : base(dispatcher)
		{
			tcpClient = new TcpClient();
		}

		protected override void ConnectInternal(ConnectionData conData)
		{
			try { tcpClient.Connect(conData.Hostname, conData.Port); }
			catch (SocketException ex) { throw new Ts3Exception("Could not connect.", ex); }

			tcpStream = tcpClient.GetStream();
			tcpReader = new StreamReader(tcpStream, Util.Encoder);
			tcpWriter = new StreamWriter(tcpStream, Util.Encoder) { NewLine = "\n" };

			for (int i = 0; i < 3; i++)
				tcpReader.ReadLine();

			ConnectDone();
		}

		protected override void DisconnectInternal()
		{
			tcpWriter?.WriteLine("quit");
			tcpWriter?.Flush();
			tcpClient.Close();
		}

		protected override void NetworkLoop()
		{
			while (true)
			{
				string line;
				try { line = tcpReader.ReadLine(); }
				catch (IOException) { line = null; }
				if (line == null) break;
				if (string.IsNullOrWhiteSpace(line)) continue;

				var message = line.Trim();
				ProcessCommand(message);
			}
			DisconnectDone(MoveReason.LeftServer); // TODO ??
		}

		protected override IEnumerable<IResponse> SendCommand(Ts3Command com, Type targetType) // Synchronous
		{
			using (WaitBlock wb = new WaitBlock(targetType))
			{
				lock (LockObj)
				{
					RequestQueue.Enqueue(wb);
					SendRaw(com.ToString());
				}

				return wb.WaitForMessage();
			}
		}

		private void SendRaw(string data)
		{
			tcpWriter.WriteLine(data);
			tcpWriter.Flush();
		}

		#region QUERY SPECIFIC COMMANDS

		public void RegisterNotification(MessageTarget target, int channel) => RegisterNotification(target.GetQueryString(), channel);
		public void RegisterNotification(RequestTarget target, int channel) => RegisterNotification(target.GetQueryString(), channel);
		private void RegisterNotification(string target, int channel)
		{
			var ev = new CommandParameter("event", target.ToLowerInvariant());
			if (target == "channel")
				Send("servernotifyregister", ev, new CommandParameter("id", channel));
			else
				Send("servernotifyregister", ev);
		}

		public void Login(string username, string password)
			=> Send("login",
			new CommandParameter("client_login_name", username),
			new CommandParameter("client_login_password", password));
		public void UseServer(int svrId)
			=> Send("use",
			new CommandParameter("sid", svrId));

		#endregion

		public override void Dispose()
		{
			base.Dispose();

			lock (LockObj)
			{
				tcpWriter?.Dispose();
				tcpWriter = null;

				tcpReader?.Dispose();
				tcpReader = null;
			}
		}
	}
}