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
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Pipelines;
	using System.Linq;
	using System.Net.Sockets;
	using System.Threading.Tasks;
	using ChannelIdT = System.UInt64;
	using ClientDbIdT = System.UInt64;
	using CmdR = System.E<Messages.CommandError>;
	using TSFileInfo = Messages.FileInfo;
	using Uid = System.String;

	public sealed class Ts3QueryClient : Ts3BaseFunctions
	{
		private readonly object sendQueueLock = new object();
		private readonly TcpClient tcpClient;
		private NetworkStream tcpStream;
		private StreamReader tcpReader;
		private StreamWriter tcpWriter;
		private readonly SyncMessageProcessor msgProc;
		private readonly IEventDispatcher dispatcher;
		private Pipe dataPipe = new Pipe();

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

		public Ts3QueryClient(EventDispatchType dispatcherType)
		{
			connecting = false;
			tcpClient = new TcpClient();
			msgProc = new SyncMessageProcessor(MessageHelper.GetToClientNotificationType);
			dispatcher = EventDispatcherHelper.Create(dispatcherType);
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

				for (int i = 0; i < 3; i++)
					tcpReader.ReadLine();
			}
			catch (SocketException ex) { throw new Ts3Exception("Could not connect.", ex); }
			finally { connecting = false; }

			dispatcher.Init(NetworkLoop, InvokeEvent, null);
			OnConnected?.Invoke(this, EventArgs.Empty);
			dispatcher.EnterEventLoop();
		}

		public override void Disconnect()
		{
			lock (sendQueueLock)
			{
				SendRaw("quit");
				if (tcpClient.Connected)
					tcpClient?.Dispose();
			}
		}

		private void NetworkLoop(object ctx)
		{
			Task.WhenAll(ReadLoopAsync(tcpStream, dataPipe.Writer), WriteLoopAsync(tcpStream, dataPipe.Reader)).ConfigureAwait(false).GetAwaiter().GetResult();
			OnDisconnected?.Invoke(this, new DisconnectEventArgs(Reason.LeftServer));
		}

		private async Task ReadLoopAsync(NetworkStream stream, PipeWriter writer)
		{
			const int minimumBufferSize = 4096;
			var dataReadBuffer = new byte[4096];

			while (true)
			{
				try
				{
					var mem = writer.GetMemory(minimumBufferSize);
					int bytesRead = await stream.ReadAsync(dataReadBuffer, 0, dataReadBuffer.Length).ConfigureAwait(false);
					if (bytesRead == 0)
					{
						break;
					}

					dataReadBuffer.CopyTo(mem);
					//await writer.WriteAsync(dataReadBuffer.AsMemory(0, bytesRead));
					//await writer.FlushAsync();
					writer.Advance(bytesRead);
				}
				catch (IOException) { break; }

				FlushResult result = await writer.FlushAsync().ConfigureAwait(false);

				if (result.IsCompleted)
				{
					break;
				}
			}
			writer.Complete();
		}

		private async Task WriteLoopAsync(NetworkStream stream, PipeReader reader)
		{
			var dataWriteBuffer = new byte[4096];
			while (true)
			{
				var result = await reader.ReadAsync().ConfigureAwait(false);

				ReadOnlySequence<byte> buffer = result.Buffer;
				SequencePosition? position = null;

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

				if (result.IsCompleted)
				{
					break;
				}
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

		public override R<T[], CommandError> SendCommand<T>(Ts3Command com) // Synchronous
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

		private void SendRaw(string data)
		{
			if (!tcpClient.Connected)
				return;
			tcpWriter.WriteLine(data);
			tcpWriter.Flush();
		}

		#region QUERY SPECIFIC COMMANDS

		private static readonly string[] TargetTypeString = { "textprivate", "textchannel", "textserver", "channel", "server" };

		public CmdR RegisterNotification(TextMessageTargetMode target, ChannelIdT channel)
			=> RegisterNotification(TargetTypeString[(int)target], channel);

		public CmdR RegisterNotification(ReasonIdentifier target, ChannelIdT channel)
			=> RegisterNotification(TargetTypeString[(int)target], channel);

		private CmdR RegisterNotification(string target, ChannelIdT channel)
		{
			var ev = new CommandParameter("event", target.ToLowerInvariant());
			if (target == "channel")
				return Send<ResponseVoid>("servernotifyregister", ev, new CommandParameter("id", channel));
			else
				return Send<ResponseVoid>("servernotifyregister", ev);
		}

		public CmdR Login(string username, string password)
			=> Send<ResponseVoid>("login",
			new CommandParameter("client_login_name", username),
			new CommandParameter("client_login_password", password));

		public CmdR UseServer(int serverId)
			=> Send<ResponseVoid>("use",
			new CommandParameter("sid", serverId));

		public CmdR UseServerPort(ushort port)
			=> Send<ResponseVoid>("use",
			new CommandParameter("port", port));

		// Splitted base commands

		public override R<ServerGroupAddResponse, CommandError> ServerGroupAdd(string name, GroupType? type = null)
			=> Send<ServerGroupAddResponse>("servergroupadd",
				type.HasValue
				? new List<ICommandPart> { new CommandParameter("name", name), new CommandParameter("type", (int)type.Value) }
				: new List<ICommandPart> { new CommandParameter("name", name) }).WrapSingle();

		public override R<ServerGroupsByClientId[], CommandError> ServerGroupsByClientDbId(ulong clDbId)
			=> Send<ServerGroupsByClientId>("servergroupsbyclientid",
			new CommandParameter("cldbid", clDbId));

		public override R<FileUpload, CommandError> FileTransferInitUpload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long fileSize, bool overwrite, bool resume)
			=> Send<FileUpload>("ftinitupload",
			new CommandParameter("cid", channelId),
			new CommandParameter("name", path),
			new CommandParameter("cpw", channelPassword),
			new CommandParameter("clientftfid", clientTransferId),
			new CommandParameter("size", fileSize),
			new CommandParameter("overwrite", overwrite),
			new CommandParameter("resume", resume)).WrapSingle();

		public override R<FileDownload, CommandError> FileTransferInitDownload(ChannelIdT channelId, string path, string channelPassword,
			ushort clientTransferId, long seek)
			=> Send<FileDownload>("ftinitdownload",
			new CommandParameter("cid", channelId),
			new CommandParameter("name", path),
			new CommandParameter("cpw", channelPassword),
			new CommandParameter("clientftfid", clientTransferId),
			new CommandParameter("seekpos", seek)).WrapSingle();

		public override R<FileTransfer[], CommandError> FileTransferList()
			=> Send<FileTransfer>("ftlist");

		public override R<FileList[], CommandError> FileTransferGetFileList(ChannelIdT channelId, string path, string channelPassword = "")
			=> Send<FileList>("ftgetfilelist",
			new CommandParameter("cid", channelId),
			new CommandParameter("path", path),
			new CommandParameter("cpw", channelPassword));

		public override R<TSFileInfo[], CommandError> FileTransferGetFileInfo(ChannelIdT channelId, string[] path, string channelPassword = "")
			=> Send<TSFileInfo>("ftgetfileinfo",
			new CommandParameter("cid", channelId),
			new CommandParameter("cpw", channelPassword),
			new CommandMultiParameter("name", path));

		public override R<ClientDbIdFromUid, CommandError> ClientGetDbIdFromUid(Uid clientUid)
			=> Send<ClientDbIdFromUid>("clientgetdbidfromuid",
			new CommandParameter("cluid", clientUid)).WrapSingle();

		public override R<ClientIds[], CommandError> GetClientIds(Uid clientUid)
			=> Send<ClientIds>("clientgetids",
			new CommandParameter("cluid", clientUid));

		public override R<PermOverview[], CommandError> PermOverview(ClientDbIdT clientDbId, ChannelIdT channelId, params Ts3Permission[] permission)
			=> Send<PermOverview>("permoverview",
			new CommandParameter("cldbid", clientDbId),
			new CommandParameter("cid", channelId),
			Ts3PermissionHelper.GetAsMultiParameter(msgProc.Deserializer.PermissionTransform, permission));

		public override R<PermList[], CommandError> PermissionList()
			=> Send<PermList>("permissionlist");

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
