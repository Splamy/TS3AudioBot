namespace TS3Client.Full
{
	using Commands;
	using Helper;
	using Messages;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Net;
	using CmdR = System.E<Messages.CommandError>;

	public class Ts3Server : IDisposable
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Ts3Crypt ts3Crypt;
		private readonly PacketHandler<C2S, S2C> packetHandler;
		private readonly AsyncMessageProcessor msgProc;
		private readonly IEventDispatcher dispatcher;
		bool initCheckDone = false;
		public bool Init { get; set; } = false;

		private ConnectionContext context;
		private readonly object statusLock = new object();

		public Ts3Server()
		{
			ts3Crypt = new Ts3Crypt();
			ts3Crypt.Identity = Ts3Crypt.GenerateNewIdentity(0);
			packetHandler = new PacketHandler<C2S, S2C>(ts3Crypt);
			msgProc = new AsyncMessageProcessor(MessageHelper.GetToServerNotificationType);
			dispatcher = EventDispatcherHelper.Create(EventDispatchType.AutoThreadPooled);
		}

		private void InvokeEvent(LazyNotification lazyNotification)
		{
			if (!initCheckDone)
				return;

			var notification = lazyNotification.Notifications;
			switch (lazyNotification.NotifyType)
			{
			case NotificationType.ClientInit: { var result = lazyNotification.WrapSingle<ClientInit>(); if (result.Ok) ProcessClientInit(result.Value); } break;
			case NotificationType.ClientInitIv: { var result = lazyNotification.WrapSingle<ClientInitIv>(); if (result.Ok) ProcessClientInitIv(result.Value); } break;
			case NotificationType.Unknown:
				throw Util.UnhandledDefault(lazyNotification.NotifyType);
			}
		}

		public void Listen(IPEndPoint addr)
		{
			packetHandler.Listen(addr);
			context = new ConnectionContext();
			dispatcher.Init(NetworkLoop, InvokeEvent, context);
			dispatcher.EnterEventLoop();
		}

		private void NetworkLoop(object ctxObject)
		{
			var ctx = (ConnectionContext)ctxObject;
			packetHandler.PacketEvent += (ref Packet<C2S> packet) => { PacketEvent(ctx, ref packet); };

			packetHandler.FetchPackets();
		}

		private void PacketEvent(ConnectionContext ctx, ref Packet<C2S> packet)
		{
			if (ctx.WasExit)
				return;
			switch (packet.PacketType)
			{
			case PacketType.Command:
			case PacketType.CommandLow:
				var result = msgProc.PushMessage(packet.Data);
				if (result.HasValue)
					dispatcher.Invoke(result.Value);
				break;

			case PacketType.Init1:
				if (packet.Data.Length >= 301 && packet.Data[4] == 4)
				{
					initCheckDone = true;
					var resultI = msgProc.PushMessage(packet.Data.AsMemory(301, packet.Data.Length - 301));
					if (resultI.HasValue)
						dispatcher.Invoke(resultI.Value);
				}
				break;
			}
		}

		[DebuggerStepThrough]
		protected CmdR SendNoResponsed(Ts3Command command)
			=> SendCommand<ResponseVoid>(command.ExpectsResponse(false));

		public R<T[], CommandError> SendCommand<T>(Ts3Command com) where T : IResponse, new()
		{
			using (var wb = new WaitBlock(msgProc.Deserializer, false))
			{
				var result = SendCommandBase(wb, com);
				if (!result.Ok)
					return result.Error;
				if (com.ExpectResponse)
					return wb.WaitForMessage<T>();
				else
					// This might not be the nicest way to return in this case
					// but we don't know what the response is, so this acceptable.
					return Util.NoResultCommandError;
			}
		}

		private E<CommandError> SendCommandBase(WaitBlock wb, Ts3Command com)
		{
			if (context.WasExit || com.ExpectResponse)
				return Util.TimeOutCommandError;

			var message = com.ToString();
			byte[] data = Util.Encoder.GetBytes(message);
			packetHandler.AddOutgoingPacket(data, PacketType.Command);
			return R.Ok;
		}

		private void ProcessClientInitIv(ClientInitIv clientInitIv)
		{
			Log.Info("clientinitiv in");
			lock (statusLock)
			{
				if (ts3Crypt.CryptoInitComplete)
					return;

				var beta = new byte[10];
				Util.Random.NextBytes(beta);
				var betaStr = Convert.ToBase64String(beta);

				InitIvExpand(clientInitIv.Alpha, betaStr, ts3Crypt.Identity.PublicKeyString);

				ts3Crypt.CryptoInit(clientInitIv.Alpha, betaStr, clientInitIv.Omega);
			}
		}

		private void ProcessClientInit(ClientInit clientInit)
		{
			Init = true;
			Console.WriteLine("init!");
			File.AppendAllText("sign.out", $"{clientInit.ClientVersion},{clientInit.ClientPlatform},{clientInit.ClientVersionSign}\n");
		}

		public CmdR InitIvExpand(string alpha, string beta, string omega)
			=> SendNoResponsed(
				new Ts3Command("initivexpand", new List<ICommandPart> {
					new CommandParameter("alpha", alpha),
					new CommandParameter("beta", beta),
					new CommandParameter("omega", omega) }));

		public void Dispose()
		{
			packetHandler.Stop();
		}
	}
}
