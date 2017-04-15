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
	using System.Linq;
	using System.Collections.Generic;
	using System.IO;
	using System.Net.Sockets;

	public sealed class Ts3QueryClient : Ts3BaseFunctions
	{
		private readonly object SendQueueLock = new object();
		private readonly TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;
		private readonly MessageProcessor msgProc;
		private readonly IEventDispatcher dispatcher;

		public override ClientType ClientType => ClientType.Query;
		public override bool Connected => tcpClient.Connected;

		public override event NotifyEventHandler<TextMessage> OnTextMessageReceived;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;

		public Ts3QueryClient(EventDispatchType dispatcherType)
		{
			tcpClient = new TcpClient();
			msgProc = new MessageProcessor(true);
			dispatcher = EventDispatcherHelper.Create(dispatcherType);
		}

		public override void Connect(ConnectionData conData)
		{
			try { tcpClient.Connect(conData.Hostname, conData.Port); }
			catch (SocketException ex) { throw new Ts3Exception("Could not connect.", ex); }

			tcpStream = tcpClient.GetStream();
			tcpReader = new StreamReader(tcpStream, Util.Encoder);
			tcpWriter = new StreamWriter(tcpStream, Util.Encoder) { NewLine = "\n" };

			for (int i = 0; i < 3; i++)
				tcpReader.ReadLine();

			dispatcher.Init(NetworkLoop, InvokeEvent);
			OnConnected?.Invoke(this, new EventArgs());
		}

		public override void Disconnect()
		{
			lock (SendQueueLock)
			{
				SendRaw("quit");
				if (tcpClient.Connected)
					((IDisposable)tcpClient)?.Dispose();
			}
		}

		private void NetworkLoop()
		{
			while (true)
			{
				string line;
				try { line = tcpReader.ReadLine(); }
				catch (IOException) { line = null; }
				if (line == null) break;
				if (string.IsNullOrWhiteSpace(line)) continue;

				var message = line.Trim();
				msgProc.PushMessage(message);
			}
			OnDisconnected?.Invoke(this, new DisconnectEventArgs(MoveReason.LeftServer)); // TODO ??
		}

		private void InvokeEvent(LazyNotification lazyNotification)
		{
			var notification = lazyNotification.Notifications;
			switch (lazyNotification.NotifyType)
			{
			case NotificationType.ChannelCreated: break;
			case NotificationType.ChannelDeleted: break;
			case NotificationType.ChannelChanged: break;
			case NotificationType.ChannelEdited: break;
			case NotificationType.ChannelMoved: break;
			case NotificationType.ChannelPasswordChanged: break;
			case NotificationType.ClientEnterView: OnClientEnterView?.Invoke(this, notification.Cast<ClientEnterView>()); break;
			case NotificationType.ClientLeftView: OnClientLeftView?.Invoke(this, notification.Cast<ClientLeftView>()); break;
			case NotificationType.ClientMoved: break;
			case NotificationType.ServerEdited: break;
			case NotificationType.TextMessage: OnTextMessageReceived?.Invoke(this, notification.Cast<TextMessage>()); break;
			case NotificationType.TokenUsed: break;
			// special
			case NotificationType.Error: break;
			case NotificationType.Unknown:
			default: throw new InvalidOperationException();
			}
		}

		protected override IEnumerable<T> SendCommand<T>(Ts3Command com) // Synchronous
		{
			using (var wb = new WaitBlock())
			{
				lock (SendQueueLock)
				{
					msgProc.EnqueueRequest(wb);
					SendRaw(com.ToString());
				}

				return wb.WaitForMessage<T>();
			}
		}

		private void SendRaw(string data)
		{
			if (!tcpClient.Connected)
				return;
			tcpWriter.WriteLine(data);
			tcpWriter.Flush();
		}

		#region QUERY SPECIFIC COMMANDS

		private static readonly string[] targetTypeString = new[] { "textprivate", "textchannel", "textserver", "channel", "server" };

		public void RegisterNotification(MessageTarget target, int channel) => RegisterNotification(targetTypeString[(int)target], channel);
		public void RegisterNotification(RequestTarget target, int channel) => RegisterNotification(targetTypeString[(int)target], channel);
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
			lock (SendQueueLock)
			{
				tcpWriter?.Dispose();
				tcpWriter = null;

				tcpReader?.Dispose();
				tcpReader = null;

				msgProc.DropQueue();
				dispatcher.Dispose();
			}
		}
	}
}
