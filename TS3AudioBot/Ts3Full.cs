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
	using Helper;
	using Helper.Environment;
	using System;
	using System.Linq;
	using System.Reflection;
	using TS3Client;
	using TS3Client.Audio;
	using TS3Client.Full;
	using TS3Client.Helper;
	using TS3Client.Messages;

	public sealed class Ts3Full : TeamspeakControl, IPlayerConnection
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private readonly Ts3FullClient tsFullClient;
		private ClientData self;

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

		private bool connecting = false;
		public bool HasConnection => tsFullClient.Connected || tsFullClient.Connecting || connecting;

		public override event EventHandler<EventArgs> OnBotConnected;
		public override event EventHandler OnBotDisconnect;

		private readonly Ts3FullClientData ts3FullClientData;

		private IdentityData identity;

		private readonly StallCheckPipe stallCheckPipe;
		private readonly VolumePipe volumePipe;
		private readonly FfmpegProducer ffmpegProducer;
		private readonly PreciseTimedPipe timePipe;
		private readonly PassiveMergePipe mergePipe;
		private readonly EncoderPipe encoderPipe;
		internal CustomTargetPipe TargetPipe { get; private set; }

		public Ts3Full(Ts3FullClientData tfcd) : base(ClientType.Full)
		{
			tsFullClient = (Ts3FullClient)tsBaseClient;

			ts3FullClientData = tfcd;
			tfcd.PropertyChanged += Tfcd_PropertyChanged;

			ffmpegProducer = new FfmpegProducer(tfcd);
			stallCheckPipe = new StallCheckPipe();
			volumePipe = new VolumePipe();
			encoderPipe = new EncoderPipe(SendCodec) { Bitrate = ts3FullClientData.AudioBitrate * 1000 };
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

		private void Tfcd_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Ts3FullClientData.AudioBitrate) && sender is Ts3FullClientData tfcd)
			{
				var value = tfcd.AudioBitrate;
				if (value <= 0 || value >= 256)
					return;
				encoderPipe.Bitrate = value * 1000;
			}
		}

		public override void Connect()
		{
			// get or compute identity
			if (string.IsNullOrEmpty(ts3FullClientData.Identity))
			{
				identity = Ts3Crypt.GenerateNewIdentity();
				ts3FullClientData.Identity = identity.PrivateKeyString;
				ts3FullClientData.IdentityOffset = identity.ValidKeyOffset;
			}
			else
			{
				var identityResult = Ts3Crypt.LoadIdentityDynamic(ts3FullClientData.Identity, ts3FullClientData.IdentityOffset);
				if (!identityResult.Ok)
				{
					Log.Error("The identity from the config file is corrupted. Remove it to generate a new one next start; or try to repair it.");
					return;
				}
				identity = identityResult.Value;
				if (ts3FullClientData.Identity != identity.PrivateKeyString)
					ts3FullClientData.Identity = identity.PrivateKeyString;
				if (ts3FullClientData.IdentityOffset != identity.ValidKeyOffset)
					ts3FullClientData.IdentityOffset = identity.ValidKeyOffset;
			}

			// check required security level
			if (ts3FullClientData.IdentityLevel == "auto") { }
			else if (int.TryParse(ts3FullClientData.IdentityLevel, out int targetLevel))
				UpdateIndentityToSecurityLevel(targetLevel);
			else
				Log.Warn("Invalid value for QueryConnection::IdentityLevel, enter a number or \"auto\".");

			// get or compute password
			if (!string.IsNullOrEmpty(ts3FullClientData.ServerPassword)
				&& ts3FullClientData.ServerPasswordAutoHash
				&& !ts3FullClientData.ServerPasswordIsHashed)
			{
				ts3FullClientData.ServerPassword = Ts3Crypt.HashPassword(ts3FullClientData.ServerPassword);
				ts3FullClientData.ServerPasswordIsHashed = true;
			}

			tsFullClient.QuitMessage = QuitMessages[Util.Random.Next(0, QuitMessages.Length)];
			tsFullClient.OnErrorEvent += TsFullClient_OnErrorEvent;
			tsFullClient.OnConnected += TsFullClient_OnConnected;
			tsFullClient.OnDisconnected += TsFullClient_OnDisconnected;
			ConnectClient();
		}

		private void ConnectClient()
		{
			VersionSign verionSign = null;
			if (!string.IsNullOrEmpty(ts3FullClientData.ClientVersion))
			{
				var splitData = ts3FullClientData.ClientVersion.Split('|').Select(x => x.Trim()).ToArray();
				if (splitData.Length == 3)
				{
					verionSign = new VersionSign(splitData[0], splitData[1], splitData[2]);
				}
				else if (splitData.Length == 1)
				{
					var signType = typeof(VersionSign).GetField("VER_" + ts3FullClientData.ClientVersion,
						BindingFlags.IgnoreCase | BindingFlags.Static | BindingFlags.Public);
					if (signType != null)
						verionSign = (VersionSign)signType.GetValue(null);
				}

				if (verionSign == null)
				{
					Log.Warn("Invalid version sign, falling back to unknown :P");
					verionSign = VersionSign.VER_WIN_3_X_X;
				}
			}
			else if (SystemData.IsLinux)
				verionSign = VersionSign.VER_LIN_3_1_8;
			else
				verionSign = VersionSign.VER_WIN_3_1_8;

			connecting = true;
			tsFullClient.Connect(new ConnectionDataFull
			{
				Username = ts3FullClientData.DefaultNickname,
				ServerPassword = ts3FullClientData.ServerPasswordIsHashed
					? Password.FromHash(ts3FullClientData.ServerPassword)
					: Password.FromPlain(ts3FullClientData.ServerPassword),
				Address = ts3FullClientData.Address,
				Identity = identity,
				VersionSign = verionSign,
				DefaultChannel = ts3FullClientData.DefaultChannel,
			});
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
					if (ts3FullClientData.IdentityLevel == "auto")
					{
						int targetSecLevel = int.Parse(error.ExtraMessage);
						UpdateIndentityToSecurityLevel(targetSecLevel);
						ConnectClient();
						return; // skip triggering event, we want to reconnect
					}
					else
					{
						Log.Warn("The server reported that the security level you set is not high enough." +
								"Increase the value to \"{0}\" or set it to \"auto\" to generate it on demand when connecting.", error.ExtraMessage);
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
			}

			connecting = false;
			OnBotDisconnect?.Invoke(this, EventArgs.Empty);
		}

		private void TsFullClient_OnConnected(object sender, EventArgs e)
		{
			connecting = false;
			OnBotConnected?.Invoke(this, EventArgs.Empty);
		}

		private void UpdateIndentityToSecurityLevel(int targetLevel)
		{
			if (Ts3Crypt.GetSecurityLevel(identity) < targetLevel)
			{
				Log.Info("Calculating up to required security level: {0}", targetLevel);
				Ts3Crypt.ImproveSecurity(identity, targetLevel);
				ts3FullClientData.IdentityOffset = identity.ValidKeyOffset;
			}
		}

		public override R<ClientData> GetSelf()
		{
			if (self != null)
				return self;

			var result = tsBaseClient.WhoAmI();
			if (!result.Ok)
				return $"Could not get self ({result.Error.ErrorFormat()})";
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

		public R AudioStart(string url)
		{
			var result = ffmpegProducer.AudioStart(url);
			if (result)
				timePipe.Paused = false;
			return result;
		}

		public R AudioStop()
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
				if (value < 0 || value > AudioValues.MaxVolume)
					throw new ArgumentOutOfRangeException(nameof(value));
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
			timePipe?.Dispose();
			ffmpegProducer?.Dispose();
			encoderPipe?.Dispose();
			base.Dispose();
		}
	}

	public class Ts3FullClientData : ConfigData
	{
		[Info("The address (and port, default: 9987) of the TeamSpeak3 server")]
		public string Address { get; set; }
		[Info("| DO NOT MAKE THIS KEY PUBLIC | The client identity", "")]
		public string Identity { get; set; }
		[Info("The client identity security offset", "0")]
		public ulong IdentityOffset { get; set; }
		[Info("The client identity security level which should be calculated before connecting, or \"auto\" to generate on demand.", "auto")]
		public string IdentityLevel { get; set; }
		[Info("The server password. Leave empty for none.")]
		public string ServerPassword { get; set; }
		[Info("Set this to true, if the server password is hashed.", "false")]
		public bool ServerPasswordIsHashed { get; set; }
		[Info("Enable this to automatically hash and store unhashed passwords.\n" +
			"# (Be careful since this will overwrite the 'ServerPassword' field with the hashed value once computed)", "false")]
		public bool ServerPasswordAutoHash { get; set; }
		[Info("The path to ffmpeg", "ffmpeg")]
		public string FfmpegPath { get; set; }
		[Info("Specifies the bitrate (in kbps) for sending audio.\n" +
			"# Values between 8 and 98 are supported, more or less can work but without guarantees.\n" +
			"# Reference values: 32 - ok (~5KiB/s), 48 - good (~7KiB/s), 64 - very good (~9KiB/s), 92 - superb (~13KiB/s)", "48")]
		public int AudioBitrate { get; set; }
		[Info("Version for the client in the form of <version build>|<platform>|<version sign>\n" +
			"# Leave empty for default.", "")]
		public string ClientVersion { get; set; }
		[Info("Default Nickname when connecting", "AudioBot")]
		public string DefaultNickname { get; set; }
		[Info("Default Channel when connectiong\n" +
			"# Use a channel path or '/<id>', examples: 'Home/Lobby', '/5', 'Home/Afk \\/ Not Here'", "")]
		public string DefaultChannel { get; set; }
		[Info("The password for the default channel. Leave empty for none. Not required with permission b_channel_join_ignore_password", "")]
		public string DefaultChannelPassword { get; set; }
		[Info("The client badges. You can set a comma seperate string with max three GUID's. Here is a list: http://yat.qa/ressourcen/abzeichen-badges/", "overwolf=0:badges=")]
		public string ClientBadges { get; set; }
	}
}
