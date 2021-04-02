// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TSLib.Commands;
using TSLib.Full.Book;
using TSLib.Helper;
using TSLib.Messages;
using CmdR = System.Threading.Tasks.Task<System.E<TSLib.Messages.CommandError>>;

namespace TSLib.Query
{
	public sealed partial class TsQueryClient : TsBaseFunctions
	{
		private readonly object sendQueueLock = new object();
		private readonly TcpClient tcpClient;
		private StreamReader? tcpReader;
		private StreamWriter? tcpWriter;
		private CancellationTokenSource? cts;
		private readonly SyncMessageProcessor msgProc;
		private readonly IEventDispatcher dispatcher;
		private readonly Pipe dataPipe = new Pipe();

		public override ClientType ClientType => ClientType.Query;
		public override bool Connected => tcpClient.Connected;
		private bool connecting;
		public override bool Connecting => connecting && !Connected;
		protected override Deserializer Deserializer => msgProc.Deserializer;

		public override event EventHandler<DisconnectEventArgs>? OnDisconnected;

		public TsQueryClient()
		{
			connecting = false;
			tcpClient = new TcpClient();
			msgProc = new SyncMessageProcessor(MessageHelper.GetToClientNotificationType);
			dispatcher = new ExtraThreadEventDispatcher();
		}

		public override async CmdR Connect(ConnectionData conData)
		{
			remoteAddress = await TsDnsResolver.TryResolve(conData.Address, TsDnsResolver.TsQueryDefaultPort);
			if (remoteAddress is null)
				return CommandError.Custom("Could not read or resolve address.");

			NetworkStream tcpStream;
			try
			{
				connecting = true;

				await tcpClient.ConnectAsync(remoteAddress.Address, remoteAddress.Port);

				ConnectionData = conData;

				tcpStream = tcpClient.GetStream();
				tcpReader = new StreamReader(tcpStream, Tools.Utf8Encoder);
				tcpWriter = new StreamWriter(tcpStream, Tools.Utf8Encoder) { NewLine = "\n" };

				if (await tcpReader.ReadLineAsync() != "TS3")
					return CommandError.Custom("Protocol violation. The stream must start with 'TS3'");
				if (string.IsNullOrEmpty(await tcpReader.ReadLineAsync()))
					await tcpReader.ReadLineAsync();
			}
			catch (SocketException ex) { return CommandError.Custom("Could not connect: " + ex.Message); }
			finally { connecting = false; }

			cts = new CancellationTokenSource();
			dispatcher.Init(InvokeEvent, conData.LogId);
			_ = NetworkLoop(tcpStream, cts.Token);
			return R.Ok;
		}

		public override Task Disconnect()
		{
			lock (sendQueueLock)
			{
				SendRaw("quit");
				cts?.Cancel();
				cts = null;
				if (tcpClient.Connected)
					tcpClient.Dispose();
			}
			return Task.CompletedTask;
		}

		private async Task NetworkLoop(NetworkStream tcpStream, CancellationToken cancellationToken)
		{
			await Task.WhenAll(NetworkToPipeLoopAsync(tcpStream, dataPipe.Writer, cancellationToken), PipeProcessorAsync(dataPipe.Reader, cancellationToken));
			OnDisconnected?.Invoke(this, new DisconnectEventArgs(Reason.LeftServer));
		}

		private async Task NetworkToPipeLoopAsync(NetworkStream stream, PipeWriter writer, CancellationToken cancellationToken = default)
		{
			const int minimumBufferSize = 4096;
#if !(NETSTANDARD2_1 || NETCOREAPP3_1)
			var dataReadBuffer = new byte[minimumBufferSize];
#endif

			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var mem = writer.GetMemory(minimumBufferSize);
#if NETSTANDARD2_1 || NETCOREAPP3_1
					int bytesRead = await stream.ReadAsync(mem, cancellationToken);
#else
					int bytesRead = await stream.ReadAsync(dataReadBuffer, 0, dataReadBuffer.Length, cancellationToken);
					dataReadBuffer.CopyTo(mem);
#endif
					if (bytesRead == 0)
						break;
					writer.Advance(bytesRead);
				}
				catch (IOException) { break; }

				var result = await writer.FlushAsync(cancellationToken);
				if (result.IsCompleted || result.IsCanceled)
					break;
			}
			await writer.CompleteAsync();
		}

		private async Task PipeProcessorAsync(PipeReader reader, CancellationToken cancelationToken = default)
		{
			while (!cancelationToken.IsCancellationRequested)
			{
				var result = await reader.ReadAsync(cancelationToken);

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

			await reader.CompleteAsync();
		}

		public override Task<R<T[], CommandError>> Send<T>(TsCommand com)
		{
			using var wb = new WaitBlock(msgProc.Deserializer);
			lock (sendQueueLock)
			{
				msgProc.EnqueueRequest(wb);
				SendRaw(com.ToString());
			}

			return wb.WaitForMessageAsync<T>();
		}

		public override Task<R<T[], CommandError>> SendHybrid<T>(TsCommand com, NotificationType type)
			=> Send<T>(com);

		private void SendRaw(string data)
		{
			if (!tcpClient.Connected)
				return;
			tcpWriter?.WriteLine(data);
			tcpWriter?.Flush();
		}

		#region QUERY SPECIFIC COMMANDS

		private static readonly string[] TargetTypeString = { "(dummy)", "textprivate", "textchannel", "textserver", "channel", "server" };

		public CmdR RegisterNotification(TextMessageTargetMode target)
			=> RegisterNotification(TargetTypeString[(int)target], null);

		public CmdR RegisterNotificationChannel(ChannelId? channel = null)
			=> RegisterNotification(TargetTypeString[(int)ReasonIdentifier.Channel], channel);

		public CmdR RegisterNotificationServer()
			=> RegisterNotification(TargetTypeString[(int)ReasonIdentifier.Server], null);

		private CmdR RegisterNotification(string target, ChannelId? channel)
			=> SendVoid(new TsCommand("servernotifyregister") {
				{ "event", target },
				{ "id", channel },
			});

		public CmdR Login(string username, string password)
			=> SendVoid(new TsCommand("login") {
				{ "client_login_name", username },
				{ "client_login_password", password },
			});

		public CmdR UseServer(int serverId)
			=> SendVoid(new TsCommand("use") {
				{ "sid", serverId },
			});

		public CmdR UseServerPort(ushort port)
			=> SendVoid(new TsCommand("use") {
				{ "port", port },
			});

		// Splitted base commands

		public override async Task<R<IChannelCreateResponse, CommandError>> ChannelCreate(string name,
			string? namePhonetic = null, string? topic = null, string? description = null, string? password = null,
			Codec? codec = null, int? codecQuality = null, int? codecLatencyFactor = null, bool? codecEncrypted = null,
			int? maxClients = null, int? maxFamilyClients = null, bool? maxClientsUnlimited = null,
			bool? maxFamilyClientsUnlimited = null, bool? maxFamilyClientsInherited = null, ChannelId? order = null,
			ChannelId? parent = null, ChannelType? type = null, TimeSpan? deleteDelay = null, int? neededTalkPower = null)
		{
			var result = await Send<ChannelCreateResponse>(ChannelOp("channelcreate", null, name, namePhonetic, topic, description,
				password, codec, codecQuality, codecLatencyFactor, codecEncrypted,
				maxClients, maxFamilyClients, maxClientsUnlimited, maxFamilyClientsUnlimited,
				maxFamilyClientsInherited, order, parent, type, deleteDelay, neededTalkPower));
			return result.MapToSingle()
				.WrapInterface<ChannelCreateResponse, IChannelCreateResponse>();
		}

		public override Task<R<ServerGroupAddResponse, CommandError>> ServerGroupAdd(string name, GroupType? type = null)
			=> Send<ServerGroupAddResponse>(new TsCommand("servergroupadd") {
				{ "name", name },
				{ "type", (int?)type }
			}).MapToSingle();

		public override Task<R<FileUpload, CommandError>> FileTransferInitUpload(ChannelId channelId, string path, string channelPassword,
			ushort clientTransferId, long fileSize, bool overwrite, bool resume)
			=> Send<FileUpload>(new TsCommand("ftinitupload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "size", fileSize },
				{ "overwrite", overwrite },
				{ "resume", resume }
			}).MapToSingle();

		public override Task<R<FileDownload, CommandError>> FileTransferInitDownload(ChannelId channelId, string path, string channelPassword,
			ushort clientTransferId, long seek)
			=> Send<FileDownload>(new TsCommand("ftinitdownload") {
				{ "cid", channelId },
				{ "name", path },
				{ "cpw", channelPassword },
				{ "clientftfid", clientTransferId },
				{ "seekpos", seek }
			}).MapToSingle();

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
