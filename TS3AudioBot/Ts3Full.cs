// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot
{
	using Audio;
	using Config;
	using Helper;
	using Helper.Environment;
	using System;
	using TS3Client;
	using TS3Client.Audio;
	using TS3Client.Full;
	using TS3Client.Helper;
	using TS3Client.Messages;

	public sealed class Ts3Full : TeamspeakControl, IPlayerConnection
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private ConfBot config;
		private readonly Ts3FullClient tsFullClient;
		private ClientData self;
		private IdentityData identity;

		private const Codec SendCodec = Codec.OpusMusic;
		private static readonly string[] QuitMessages = {
			"I'm outta here", "You're boring", "Have a nice day", "Bye", "Good night",
			"Nothing to do here", "Taking a break", "Lorem ipsum dolor sit amet…",
			"Nothing can hold me back", "It's getting quiet", "Drop the bazzzzzz",
			"Never gonna give you up", "Never gonna let you down", "Keep rockin' it",
			"?", "c(ꙩ_Ꙩ)ꜿ", "I'll be back", "Your advertisement could be here",
			"connection lost", "disconnected", "Requested by API.",
			"Robert'); DROP TABLE students;--", "It works!! No, wait...",
			"Notice me, senpai", ":wq"
		};

		private bool closed = false;
		private TickWorker reconnectTick = null;
		public static readonly TimeSpan TooManyClonesReconnectDelay = TimeSpan.FromSeconds(30);
		private int reconnectCounter;
		private static readonly TimeSpan[] LostConnectionReconnectDelay = new[] {
			TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10),
			TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5) };
		private static int MaxReconnects { get; } = LostConnectionReconnectDelay.Length;

		public override event EventHandler<EventArgs> OnBotConnected;
		public override event EventHandler<DisconnectEventArgs> OnBotDisconnect;

		private readonly StallCheckPipe stallCheckPipe;
		private readonly VolumePipe volumePipe;
		private readonly FfmpegProducer ffmpegProducer;
		private readonly PreciseTimedPipe timePipe;
		private readonly PassiveMergePipe mergePipe;
		private readonly EncoderPipe encoderPipe;
		internal CustomTargetPipe TargetPipe { get; }

		public Ts3Full(ConfBot config) : base(ClientType.Full)
		{
			tsFullClient = (Ts3FullClient)tsBaseClient;
			tsFullClient.OnErrorEvent += TsFullClient_OnErrorEvent;
			tsFullClient.OnConnected += TsFullClient_OnConnected;
			tsFullClient.OnDisconnected += TsFullClient_OnDisconnected;

			int ScaleBitrate(int value) => Math.Min(Math.Max(1, value), 255) * 1000;

			this.config = config;
			this.config.Audio.Bitrate.Changed += (s, e) => encoderPipe.Bitrate = ScaleBitrate(e.NewValue);

			ffmpegProducer = new FfmpegProducer(config.GetParent().Tools.Ffmpeg);
			stallCheckPipe = new StallCheckPipe();
			volumePipe = new VolumePipe();
			Volume = config.Audio.Volume.Default;
			encoderPipe = new EncoderPipe(SendCodec) { Bitrate = ScaleBitrate(config.Audio.Bitrate) };
			timePipe = new PreciseTimedPipe { ReadBufferSize = encoderPipe.PacketSize };
			timePipe.Initialize(encoderPipe);
			TargetPipe = new CustomTargetPipe(tsFullClient);
			mergePipe = new PassiveMergePipe();

			mergePipe.Add(ffmpegProducer);
			timePipe.InStream = mergePipe;
			timePipe.Chain<ActiveCheckPipe>().Chain(stallCheckPipe).Chain(volumePipe).Chain(encoderPipe).Chain(TargetPipe);

			identity = null;
		}

		public override T GetLowLibrary<T>()
		{
			if (typeof(T) == typeof(Ts3FullClient) && tsFullClient != null)
				return tsFullClient as T;
			return base.GetLowLibrary<T>();
		}

		public override E<string> Connect()
		{
			// get or compute identity
			var identityConf = config.Connect.Identity;
			if (string.IsNullOrEmpty(identityConf.Key))
			{
				identity = Ts3Crypt.GenerateNewIdentity();
				identityConf.Key.Value = identity.PrivateKeyString;
				identityConf.Offset.Value = identity.ValidKeyOffset;
			}
			else
			{
				var identityResult = Ts3Crypt.LoadIdentityDynamic(identityConf.Key.Value, identityConf.Offset.Value);
				if (!identityResult.Ok)
				{
					Log.Error("The identity from the config file is corrupted. Remove it to generate a new one next start; or try to repair it.");
					return "Corrupted identity";
				}
				identity = identityResult.Value;
				identityConf.Key.Value = identity.PrivateKeyString;
				identityConf.Offset.Value = identity.ValidKeyOffset;
			}

			// check required security level
			if (identityConf.Level.Value >= 0 && identityConf.Level.Value <= 160)
				UpdateIndentityToSecurityLevel(identityConf.Level.Value);
			else if (identityConf.Level.Value != -1)
				Log.Warn("Invalid config value for 'Level', enter a number between '0' and '160' or '-1' to adapt automatically.");
			config.SaveWhenExists();

			tsFullClient.QuitMessage = QuitMessages[Util.Random.Next(0, QuitMessages.Length)];
			return ConnectClient();
		}

		private E<string> ConnectClient()
		{
			StopReconnectTickWorker();
			if (closed)
				return "Bot disposed";

			VersionSign versionSign = null;
			if (!string.IsNullOrEmpty(config.Connect.ClientVersion.Build.Value))
			{
				var versionConf = config.Connect.ClientVersion;
				versionSign = new VersionSign(versionConf.Build, versionConf.Platform.Value, versionConf.Sign);

				if (!versionSign.CheckValid())
				{
					Log.Warn("Invalid version sign, falling back to unknown :P");
					versionSign = VersionSign.VER_WIN_3_X_X;
				}
			}
			else if (SystemData.IsLinux)
			{
				versionSign = VersionSign.VER_LIN_3_1_8;
			}
			else
			{
				versionSign = VersionSign.VER_WIN_3_1_8;
			}

			try
			{
				var connectionConfig = new ConnectionDataFull
				{
					Username = config.Connect.Name,
					ServerPassword = config.Connect.ServerPassword.Get(),
					Address = config.Connect.Address,
					Identity = identity,
					VersionSign = versionSign,
					DefaultChannel = config.Connect.Channel,
					DefaultChannelPassword = config.Connect.ChannelPassword.Get(),
				};
				config.SaveWhenExists();

				tsFullClient.Connect(connectionConfig);
				return R.Ok;
			}
			catch (Ts3Exception qcex)
			{
				Log.Info(qcex, "There is either a problem with your connection configuration, or the bot has not all permissions it needs.");
				return "Connect error";
			}
		}

		private void TsFullClient_OnErrorEvent(object sender, CommandError error)
		{
			switch (error.Id)
			{
			case Ts3ErrorCode.whisper_no_targets:
				stallCheckPipe.SetStall();
				break;

			default:
				Log.Debug("Got ts3 error event: {0}", error.ErrorFormat());
				break;
			}
		}

		private void TsFullClient_OnDisconnected(object sender, DisconnectEventArgs e)
		{
			if (e.Error != null)
			{
				var error = e.Error;
				switch (error.Id)
				{
				case Ts3ErrorCode.client_could_not_validate_identity:
					if (config.Connect.Identity.Level.Value == -1)
					{
						int targetSecLevel = int.Parse(error.ExtraMessage);
						UpdateIndentityToSecurityLevel(targetSecLevel);
						ConnectClient();
						return; // skip triggering event, we want to reconnect
					}
					else
					{
						Log.Warn("The server reported that the security level you set is not high enough." +
							"Increase the value to '{0}' or set it to '-1' to generate it on demand when connecting.", error.ExtraMessage);
					}
					break;

				case Ts3ErrorCode.client_too_many_clones_connected:
					if (reconnectCounter++ < MaxReconnects)
					{
						Log.Warn("Seems like another client with the same identity is already connected. Waiting {0:0} seconds to reconnect.",
							TooManyClonesReconnectDelay.TotalSeconds);
						reconnectTick = TickPool.RegisterTickOnce(() => ConnectClient(), TooManyClonesReconnectDelay);
						return; // skip triggering event, we want to reconnect
					}
					break;

				default:
					Log.Warn("Could not connect: {0}", error.ErrorFormat());
					break;
				}
			}
			else
			{
				Log.Debug("Bot disconnected. Reason: {0}", e.ExitReason);

				if (reconnectCounter < LostConnectionReconnectDelay.Length && !closed)
				{
					var delay = LostConnectionReconnectDelay[reconnectCounter++];
					Log.Info("Trying to reconnect. Delaying reconnect for {0:0} seconds", delay.TotalSeconds);
					reconnectTick = TickPool.RegisterTickOnce(() => ConnectClient(), delay);
					return;
				}
			}

			if (reconnectCounter >= LostConnectionReconnectDelay.Length)
			{
				Log.Warn("Could not (re)connect after {0} tries. Giving up.", reconnectCounter);
			}
			OnBotDisconnect?.Invoke(this, e);
		}

		private void TsFullClient_OnConnected(object sender, EventArgs e)
		{
			StopReconnectTickWorker();
			reconnectCounter = 0;
			OnBotConnected?.Invoke(this, EventArgs.Empty);
		}

		private void UpdateIndentityToSecurityLevel(int targetLevel)
		{
			if (Ts3Crypt.GetSecurityLevel(identity) < targetLevel)
			{
				Log.Info("Calculating up to required security level: {0}", targetLevel);
				Ts3Crypt.ImproveSecurity(identity, targetLevel);
				config.Connect.Identity.Offset.Value = identity.ValidKeyOffset;
			}
		}

		private void StopReconnectTickWorker()
		{
			var reconnectTickLocal = reconnectTick;
			reconnectTick = null;
			if (reconnectTickLocal != null)
				TickPool.UnregisterTicker(reconnectTickLocal);
		}

		public override R<ClientData> GetSelf()
		{
			if (self != null)
				return self;

			var result = tsBaseClient.WhoAmI();
			if (!result.Ok)
				return R.Err;
			var data = result.Value;
			var cd = new ClientData
			{
				Uid = identity.ClientUid,
				ChannelId = data.ChannelId,
				ClientId = tsFullClient.ClientId,
				Name = data.Name,
				ClientType = tsBaseClient.ClientType
			};

			var response = tsBaseClient
				.Send("clientgetdbidfromuid", new TS3Client.Commands.CommandParameter("cluid", identity.ClientUid))
				.WrapSingle();
			if (response.Ok && ulong.TryParse(response.Value["cldbid"], out var dbId))
				cd.DatabaseId = dbId;

			self = cd;
			return cd;
		}

		[Obsolete(AttributeStrings.UnderDevelopment)]
		public void MixInStreamOnce(StreamAudioProducer producer)
		{
			mergePipe.Add(producer);
			producer.HitEnd += (s, e) => mergePipe.Remove(producer);
			timePipe.Paused = false;
		}

		#region IPlayerConnection

		public event EventHandler OnSongEnd
		{
			add => ffmpegProducer.OnSongEnd += value;
			remove => ffmpegProducer.OnSongEnd -= value;
		}

		public E<string> AudioStart(string url)
		{
			var result = ffmpegProducer.AudioStart(url);
			if (result)
				timePipe.Paused = false;
			return result;
		}

		public E<string> AudioStop()
		{
			// TODO clean up all mixins
			timePipe.Paused = true;
			return ffmpegProducer.AudioStop();
		}

		public TimeSpan Length => ffmpegProducer.Length;

		public TimeSpan Position
		{
			get => ffmpegProducer.Position;
			set => ffmpegProducer.Position = value;
		}

		public float Volume
		{
			get => volumePipe.Volume * AudioValues.MaxVolume;
			set
			{
				if (value < 0)
					volumePipe.Volume = 0;
				else if (value > AudioValues.MaxVolume)
					volumePipe.Volume = AudioValues.MaxVolume;
				else
					volumePipe.Volume = value / AudioValues.MaxVolume;
			}
		}

		public bool Paused
		{
			get => timePipe.Paused;
			set => timePipe.Paused = value;
		}

		public bool Playing => !timePipe.Paused;

		public bool Repeated { get => false; set { } }

		#endregion

		public override void Dispose()
		{
			closed = true;
			StopReconnectTickWorker();
			timePipe?.Dispose();
			ffmpegProducer?.Dispose();
			encoderPipe?.Dispose();
			base.Dispose();
		}
	}
}
