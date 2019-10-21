// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Query
{
	using Commands;
	using Helper;
	using Messages;
	using System;
	using System.Buffers;
	using System.IO;
	using System.IO.Pipelines;
	using System.Linq;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;
	using ChannelIdT = System.UInt64;
	using CmdR = System.E<Messages.CommandError>;

	public sealed class Ts3QueryClient : Ts3BaseFunctions
	{
		private readonly object sendQueueLock = new object();
		private readonly TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;
		private CancellationTokenSource cts;
		private readonly SyncMessageProcessor msgProc;
		private readonly IEventDispatcher dispatcher;
		private readonly Pipe dataPipe = new Pipe();

		public override ClientType ClientType => ClientType.Query;
		public override bool Connected => tcpClient.Connected;
		private bool connecting;
		public override bool Connecting => connecting && !Connected;
		protected override Deserializer Deserializer => msgProc.Deserializer;

		public override event NotifyEventHandler<TextMessage> OnTextMessage;
		public override event NotifyEventHandler<ClientEnterView> OnClientEnterView;
		public override event NotifyEventHandler<ClientLeftView> OnClientLeftView;
		public override event EventHandler<EventArgs> OnConnected;
		public override event EventHandler<DisconnectEventArgs> OnDisconnected;

		public Ts3QueryClient()
		{
			connecting = false;
			tcpClient = new TcpClient();
			msgProc = new SyncMessageProcessor(MessageHelper.GetToClientNotificationType);
			dispatcher = new ExtraThreadEventDispatcher();
		}

		public override void Connect(ConnectionData conData)
		{
			if (!TsDnsResolver.TryResolve(conData.Address, out remoteAddress, TsDnsResolver.Ts3QueryDefaultPort))
				throw new Ts3Exception("Could not read or resolve address.");

			try
			{
				connecting = true;

				tcpClient.Connect(remoteAddress);

				ConnectionData = conData;

				tcpStream = tcpClient.GetStream();
				tcpReader = new StreamReader(tcpStream, Util.Encoder);
				tcpWriter = new StreamWriter(tcpStream, Util.Encoder) { NewLine = "\n" };

				if(tcpReader.ReadLine() != "TS3")
					throw new Ts3Exception("Protocol violation. The stream must start with 'TS3'");
				if (string.IsNullOrEmpty(tcpReader.ReadLine()))
					tcpReader.ReadLine();
			}
			catch (SocketException ex) { throw new Ts3Exception("Could not connect.", ex); }
			finally { connecting = false; }

			cts = new CancellationTokenSource();
			dispatcher.Init(InvokeEvent, conData.LogId);
			NetworkLoop(cts.Token).ConfigureAwait(false);
			OnConnected?.Invoke(this, EventArgs.Empty);
		}

		public override void Disconnect()
		{
			lock (sendQueueLock)
			{
				SendRaw("quit");
				cts.Cancel();
				if (tcpClient.Connected)
					tcpClient?.Dispose();
			}
		}

		private async Task NetworkLoop(CancellationToken cancellationToken)
		{
			await Task.WhenAll(NetworkToPipeLoopAsync(tcpStream, dataPipe.Writer, cancellationToken), PipeProcessorAsync(dataPipe.Reader, cancellationToken)).ConfigureAwait(false);
			OnDisconnected?.Invoke(this, new DisconnectEventArgs(Reason.LeftServer));
		}

		private async Task NetworkToPipeLoopAsync(NetworkStream stream, PipeWriter writer, CancellationToken cancellationToken = default)
		{
			const int minimumBufferSize = 4096;
#if !(NETCOREAPP2_2 || NETCOREAPP3_0)
			var dataReadBuffer = new byte[minimumBufferSize];
#endif

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var mem = writer.GetMemory(minimumBufferSize);
#if NETCOREAPP2_2 || NETCOREAPP3_0
					int bytesRead = await stream.ReadAsync(mem, cancellationToken).ConfigureAwait(false);
#else
					int bytesRead = await stream.ReadAsync(dataReadBuffer, 0, dataReadBuffer.Length, cancellationToken).ConfigureAwait(false);
					dataReadBuffer.CopyTo(mem);
#endif
					if (bytesRead == 0)
						break;
					writer.Advance(bytesRead);
				}
				catch (IOException) { break; }

				var result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
				if (result.IsCompleted || result.IsCanceled)
					break;
			}
			writer.Complete();
		}

		private async Task PipeProcessorAsync(PipeReader reader, CancellationToken cancelationToken = default)
		{
			while (!cancelationToken.IsCancellationRequested)
			{
				var result = await reader.ReadAsync(cancelationToken).ConfigureAwait(false);

				var buffer = result.Buffer;
				SequencePosition? position;

				do
				{
					position = buffer.PositionOf((byte)'\n');

					if (position != null)
					{
						var notif = msgProc.PushMessage(buffer.Slice(0, position.Value).ToArray());
						if (notif.HasValue)
						{
							dispatcher.Invoke(notif.Value);
						}

						// +2 = skipping \n\r
						buffer = buffer.Slice(buffer.GetPosition(2, position.Value));
					}
				} while (position != null);

				reader.AdvanceTo(buffer.Start, buffer.End);
				if (result.IsCompleted || result.IsCanceled)
					break;
			}

			reader.Complete();
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
			case NotificationType.TextMessage: OnTextMessage?.Invoke(this, notification.Cast<TextMessage>()); break;
			case NotificationType.TokenUsed: break;
			// special
			case NotificationType.CommandError: break;
			case NotificationType.Unknown:
			default: throw Util.UnhandledDefault(lazyNotification.NotifyType);
			}
		}

		public override R<T[], CommandError> Send<T>(Ts3Command com) // Synchronous
		{
			using (var wb = new WaitBlock(msgProc.Deserializer, false))
			{
				lock (sendQueueLock)
				{
					msgProc.EnqueueRequest(wb);
					SendRaw(com.ToString());
				}

				return wb.WaitForMessage<T>();
			}
		}

		public override R<T[], CommandError> SendHybrid<T>(Ts3Command com, NotificationType type)
			=> Send<T>(com);

		private void SendRaw(string data)
		{
			if (!tcpClient.Connected)
				return;
			tcpWriter.WriteLine(data);
			tcpWriter.Flush();
		}

		#region QUERY SPECIFIC COMMANDS

		private static readonly string[] TargetTypeString = { "(dummy)", "textprivate", "textchannel", "textserver", "channel", "server" };

		public CmdR RegisterNotification(TextMessageTargetMode target)
			=> RegisterNotification(TargetTypeString[(int)target], null);

		public CmdR RegisterNotificationChannel(ChannelIdT? channel = null)
			=> RegisterNotification(TargetTypeString[(int)ReasonIdentifier.Channel], channel);

		public CmdR RegisterNotificationServer()
			=> RegisterNotification(TargetTypeString[(int)ReasonIdentifier.Server], null);

		private CmdR RegisterNotification(string target, ChannelIdT? channel)
			=> Send<ResponseVoid>(new Ts3Command("servernotifyregister") {
				{ "event", target },
				{ "id", channel },
			});

		public CmdR Login(string username, string password)
			=> Send<ResponseVoid>(new Ts3Command("login") {
				{ "client_login_name", username },
				{ "client_login_password", password },
			});

		public CmdR UseServer(int serverId)
			=> Send<ResponseVoid>(new Ts3Command("use") {
				{ "sid", serverId },
			});

		public CmdR UseServerPort(ushort port)
			=> Send<ResponseVoid>(new Ts3Command("use") {
				{ "port", port },
			});

		// Splitted base commands

		public override R<ServerGroupAddResponse, CommandError> ServerGroupAdd(string name, GroupType? type = null)
			=> Send<ServerGroupAddResponse>(new Ts3Command("servergroupadd") {
				{ "name", name },
				{ "type", (int?)type }
			}).WrapSingle();

		public override R<FileUpload, CommandError> FileTransferInitUpload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long fileSize, bool overwrite, bool resume)
			=> Send<FileUpload>(new Ts3Command("ftinitupload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "size", fileSize },
				{ "overwrite", overwrite },
				{ "resume", resume }
			}).WrapSingle();

		public override R<FileDownload, CommandError> FileTransferInitDownload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long seek)
			=> Send<FileDownload>(new Ts3Command("ftinitdownload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "seekpos", seek }
			}).WrapSingle();

		#endregion

		public override void Dispose()
		{
			lock (sendQueueLock)
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
