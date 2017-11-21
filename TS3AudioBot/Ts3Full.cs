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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using TS3Client;
	using TS3Client.Full;
	using TS3Client.Messages;

	internal sealed class Ts3Full : TeamspeakControl, IPlayerConnection, ITargetManager
	{
		private readonly Ts3FullClient tsFullClient;
		private ClientData self;

		private const Codec SendCodec = Codec.OpusMusic;
		private readonly TimeSpan sendCheckInterval = TimeSpan.FromMilliseconds(5);
		private readonly TimeSpan audioBufferLength = TimeSpan.FromMilliseconds(20);
		private const uint StallCountInterval = 10;
		private const uint StallNoErrorCountMax = 5;
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

		private const string PreLinkConf = "-hide_banner -nostats -i \"";
		private const string PostLinkConf = "\" -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1";
		private string lastLink;
		private static readonly Regex FindDurationMatch = new Regex(@"^\s*Duration: (\d+):(\d\d):(\d\d).(\d\d)", Util.DefaultRegexConfig);
		private TimeSpan? parsedSongLength;
		private readonly object ffmpegLock = new object();
		private readonly TimeSpan retryOnDropBeforeEnd = TimeSpan.FromSeconds(10);
		private bool hasTriedToReconnectAudio;

		private readonly Ts3FullClientData ts3FullClientData;
		private float volume = 1;
		public TargetSendMode SendMode { get; set; } = TargetSendMode.None;
		public ulong GroupWhisperTargetId { get; set; }
		public GroupWhisperType GroupWhisperType { get; set; }
		public GroupWhisperTarget GroupWhisperTarget { get; set; }

		private TickWorker sendTick;
		private Process ffmpegProcess;
		private AudioEncoder encoder;
		private readonly PreciseAudioTimer audioTimer;
		private byte[] audioBuffer;
		private bool isStall;
		private uint stallCount;
		private uint stallNoErrorCount;
		private IdentityData identity;

		private readonly Dictionary<ulong, bool> channelSubscriptionsSetup;
		private readonly List<ushort> clientSubscriptionsSetup;
		private ulong[] channelSubscriptionsCache;
		private ushort[] clientSubscriptionsCache;
		private bool subscriptionSetupChanged;
		private readonly object subscriptionLockObj = new object();

		public Ts3Full(Ts3FullClientData tfcd) : base(ClientType.Full)
		{
			tsFullClient = (Ts3FullClient)tsBaseClient;

			ts3FullClientData = tfcd;
			tfcd.PropertyChanged += Tfcd_PropertyChanged;

			sendTick = TickPool.RegisterTick(AudioSend, sendCheckInterval, false);
			encoder = new AudioEncoder(SendCodec) { Bitrate = ts3FullClientData.AudioBitrate * 1000 };
			audioTimer = new PreciseAudioTimer(encoder.SampleRate, encoder.BitsPerSample, encoder.Channels);
			isStall = false;
			stallCount = 0;
			identity = null;

			Util.Init(ref channelSubscriptionsSetup);
			Util.Init(ref clientSubscriptionsSetup);
			subscriptionSetupChanged = true;
		}

		public override T GetLowLibrary<T>()
		{
			if (typeof(T) == typeof(Ts3FullClient) && tsFullClient != null)
				return tsFullClient as T;
			return base.GetLowLibrary<T>();
		}

		private void Tfcd_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(Ts3FullClientData.AudioBitrate))
			{
				var value = (int)typeof(Ts3FullClientData).GetProperty(e.PropertyName).GetValue(sender);
				if (value <= 0 || value >= 256)
					return;
				encoder.Bitrate = value * 1000;
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
				identity = Ts3Crypt.LoadIdentity(ts3FullClientData.Identity, ts3FullClientData.IdentityOffset);
			}

			// check required security level
			if (ts3FullClientData.IdentityLevel == "auto") { }
			else if (int.TryParse(ts3FullClientData.IdentityLevel, out int targetLevel))
			{
				Log.Write(Log.Level.Info, "Calculating up to required security level: {0}", targetLevel);
				Ts3Crypt.ImproveSecurity(identity, targetLevel);
				ts3FullClientData.IdentityOffset = identity.ValidKeyOffset;
			}
			else
			{
				Log.Write(Log.Level.Warning, "Invalid value for QueryConnection::IdentityLevel, enter a number or \"auto\".");
			}


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

				if(verionSign == null)
				{
					Log.Write(Log.Level.Warning, "Invalid version sign, falling back to unknown :P");
					verionSign = VersionSign.VER_WIN_3_UNKNOWN;
				}
			}
			else if (Util.IsLinux)
				verionSign = VersionSign.VER_LIN_3_0_19_4;
			else
				verionSign = VersionSign.VER_WIN_3_0_19_4;

			tsFullClient.Connect(new ConnectionDataFull
			{
				Username = ts3FullClientData.DefaultNickname,
				Password = ts3FullClientData.ServerPassword,
				Address = ts3FullClientData.Address,
				Identity = identity,
				IsPasswordHashed = ts3FullClientData.ServerPasswordIsHashed,
				VersionSign = verionSign,
				DefaultChannel = ts3FullClientData.DefaultChannel,
			});
		}

		private void TsFullClient_OnErrorEvent(object sender, CommandError e)
		{
			switch (e.Id)
			{
			case Ts3ErrorCode.whisper_no_targets:
				stallNoErrorCount = 0;
				isStall = true;
				break;

			case Ts3ErrorCode.client_could_not_validate_identity:
				if (ts3FullClientData.IdentityLevel == "auto")
				{
					int targetSecLevel = int.Parse(e.ExtraMessage);
					Log.Write(Log.Level.Info, "Calculating up to required security level: {0}", targetSecLevel);
					Ts3Crypt.ImproveSecurity(identity, targetSecLevel);
					ts3FullClientData.IdentityOffset = identity.ValidKeyOffset;

					ConnectClient();
				}
				else
				{
					Log.Write(Log.Level.Warning, "The server reported that the security level you set is not high enough." +
												 "Increase the value to \"{0}\" or set it to \"auto\" to generate it on demand when connecting.", e.ExtraMessage);
				}
				break;

			default:
				Log.Write(Log.Level.Debug, "Got ts3 error event: {0}", e.ErrorFormat());
				break;
			}
		}

		public override R<ClientData> GetSelf()
		{
			if (self != null)
				return self;

			try
			{
				var data = tsBaseClient.WhoAmI();
				var cd = new ClientData
				{
					Uid = identity.ClientUid,
					ChannelId = data.ChannelId,
					ClientId = tsFullClient.ClientId,
					NickName = data.NickName,
					ClientType = tsBaseClient.ClientType
				};

				var response = tsBaseClient
					.Send("clientgetdbidfromuid", new TS3Client.Commands.CommandParameter("cluid", identity.ClientUid))
					.FirstOrDefault();
				if (response != null && ulong.TryParse(response["cldbid"], out var dbId))
					cd.DatabaseId = dbId;

				self = cd;
				return cd;
			}
			catch (Ts3CommandException)
			{
				return "Could not get self";
			}
		}

		private void AudioSend()
		{
			lock (ffmpegLock)
			{
				if (ffmpegProcess == null)
					return;

				if (audioBuffer == null || audioBuffer.Length < encoder.OptimalPacketSize)
					audioBuffer = new byte[encoder.OptimalPacketSize];

				UpdatedSubscriptionCache();

				while (audioTimer.RemainingBufferDuration < audioBufferLength)
				{
					int read = ffmpegProcess.StandardOutput.BaseStream.Read(audioBuffer, 0, encoder.OptimalPacketSize);
					if (read == 0)
					{
						// check for premature connection drop
						if (ffmpegProcess.HasExited && !hasTriedToReconnectAudio)
						{
							var expectedStopLength = GetCurrentSongLength();
							if (expectedStopLength != TimeSpan.Zero)
							{
								var actualStopPosition = audioTimer.SongPosition;
								if (actualStopPosition + retryOnDropBeforeEnd < expectedStopLength)
								{
									Log.Write(Log.Level.Debug, "Connection to song lost, retrying at {0}", actualStopPosition);
									hasTriedToReconnectAudio = true;
									Position = actualStopPosition;
									return;
								}
							}
						}

						if (ffmpegProcess.HasExited
							&& audioTimer.RemainingBufferDuration < TimeSpan.Zero
							&& !encoder.HasPacket)
						{
							AudioStop();
							OnSongEnd?.Invoke(this, new EventArgs());
						}
						return;
					}

					hasTriedToReconnectAudio = false;
					audioTimer.PushBytes(read);

					bool doSend = true;

					switch (SendMode)
					{
					case TargetSendMode.None:
						doSend = false;
						break;
					case TargetSendMode.Voice:
						break;
					case TargetSendMode.Whisper:
					case TargetSendMode.WhisperGroup:
						if (isStall)
						{
							if (++stallCount % StallCountInterval == 0)
							{
								stallNoErrorCount++;
								if (stallNoErrorCount > StallNoErrorCountMax)
								{
									stallCount = 0;
									isStall = false;
								}
							}
							else
							{
								doSend = false;
							}
						}
						if (SendMode == TargetSendMode.Whisper)
							doSend &= channelSubscriptionsCache.Length > 0 || clientSubscriptionsCache.Length > 0;
						break;
					default:
						throw new InvalidOperationException();
					}

					// Save cpu when we know there is noone to send to
					if (!doSend)
						break;

					AudioModifier.AdjustVolume(audioBuffer, read, volume);
					encoder.PushPcmAudio(audioBuffer, read);

					while (encoder.HasPacket)
					{
						var packet = encoder.GetPacket();
						switch (SendMode)
						{
						case TargetSendMode.Voice:
							tsFullClient.SendAudio(packet.Array, packet.Length, encoder.Codec);
							break;
						case TargetSendMode.Whisper:
							tsFullClient.SendAudioWhisper(packet.Array, packet.Length, encoder.Codec, channelSubscriptionsCache, clientSubscriptionsCache);
							break;
						case TargetSendMode.WhisperGroup:
							tsFullClient.SendAudioGroupWhisper(packet.Array, packet.Length, encoder.Codec, GroupWhisperType, GroupWhisperTarget);
							break;
						}
						encoder.ReturnPacket(packet.Array);
					}
				}
			}
		}

		#region IPlayerConnection

		public event EventHandler OnSongEnd;

		public void SetGroupWhisper(GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			GroupWhisperType = type;
			GroupWhisperTarget = target;
			GroupWhisperTargetId = targetId;
		}

		public R AudioStart(string url) => StartFfmpegProcess(url);

		public R AudioStop()
		{
			sendTick.Active = false;
			audioTimer.Stop();
			StopFfmpegProcess();
			return R.OkR;
		}

		public TimeSpan Length => GetCurrentSongLength();

		public TimeSpan Position
		{
			get => audioTimer.SongPosition;
			set
			{
				if (value < TimeSpan.Zero || value > Length)
					throw new ArgumentOutOfRangeException(nameof(value));
				AudioStop();
				StartFfmpegProcess(lastLink,
					$"-ss {value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}",
					$"-ss {value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)}");
				audioTimer.SongPositionOffset = value;
			}
		}

		public int Volume
		{
			get => (int)Math.Round(volume * AudioValues.MaxVolume);
			set
			{
				if (value < 0 || value > AudioValues.MaxVolume)
					throw new ArgumentOutOfRangeException(nameof(value));
				volume = value / (float)AudioValues.MaxVolume;
			}
		}

		public bool Paused
		{
			get => sendTick.Active;
			set
			{
				if (sendTick.Active == value)
				{
					sendTick.Active = !value;
					if (value)
					{
						audioTimer.SongPositionOffset = audioTimer.SongPosition;
						audioTimer.Stop();
					}
					else
						audioTimer.Start();
				}
			}
		}

		public bool Playing => sendTick.Active;

		public bool Repeated { get { return false; } set { } }

		private R StartFfmpegProcess(string url, string extraPreParam = null, string extraPostParam = null)
		{
			try
			{
				lock (ffmpegLock)
				{
					StopFfmpegProcess();

					ffmpegProcess = new Process
					{
						StartInfo = new ProcessStartInfo
						{
							FileName = ts3FullClientData.FfmpegPath,
							Arguments = string.Concat(extraPreParam, " ", PreLinkConf, url, PostLinkConf, " ", extraPostParam),
							RedirectStandardOutput = true,
							RedirectStandardInput = true,
							RedirectStandardError = true,
							UseShellExecute = false,
							CreateNoWindow = true,
						}
					};
					ffmpegProcess.Start();

					lastLink = url;
					parsedSongLength = null;

					audioTimer.SongPositionOffset = TimeSpan.Zero;
					audioTimer.Start();
					sendTick.Active = true;
					return R.OkR;
				}
			}
			catch (Exception ex) { return $"Unable to create stream ({ex.Message})"; }
		}

		private void StopFfmpegProcess()
		{
			lock (ffmpegLock)
			{
				if (ffmpegProcess == null)
					return;

				try
				{
					if (!ffmpegProcess.HasExited)
						ffmpegProcess.Kill();
					else
						ffmpegProcess.Close();
				}
				catch (InvalidOperationException) { }
				ffmpegProcess = null;
			}
		}

		private TimeSpan GetCurrentSongLength()
		{
			lock (ffmpegLock)
			{
				if (ffmpegProcess == null)
					return TimeSpan.Zero;

				if (parsedSongLength.HasValue)
					return parsedSongLength.Value;

				Match match = null;
				while (ffmpegProcess.StandardError.Peek() > -1)
				{
					var infoLine = ffmpegProcess.StandardError.ReadLine();
					if (string.IsNullOrEmpty(infoLine))
						continue;
					match = FindDurationMatch.Match(infoLine);
					if (match.Success)
						break;
				}
				if (match == null || !match.Success)
					return TimeSpan.Zero;

				int hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
				int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
				int seconds = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
				int millisec = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) * 10;
				parsedSongLength = new TimeSpan(0, hours, minutes, seconds, millisec);
				return parsedSongLength.Value;
			}
		}

		#endregion

		#region ITargetManager

		public void WhisperChannelSubscribe(ulong channel, bool temp)
		{
			// TODO move to requested channel
			// TODO spawn new client
			lock (subscriptionLockObj)
			{
				if (channelSubscriptionsSetup.TryGetValue(channel, out var subscriptionTemp))
					channelSubscriptionsSetup[channel] = !subscriptionTemp || !temp;
				else
				{
					channelSubscriptionsSetup[channel] = !temp;
					subscriptionSetupChanged = true;
				}
			}
		}

		public void WhisperChannelUnsubscribe(ulong channel, bool temp)
		{
			lock (subscriptionLockObj)
			{
				if (!temp)
				{
					subscriptionSetupChanged |= channelSubscriptionsSetup.Remove(channel);
				}
				else
				{
					if (channelSubscriptionsSetup.TryGetValue(channel, out bool subscriptionTemp) && subscriptionTemp)
					{
						channelSubscriptionsSetup.Remove(channel);
						subscriptionSetupChanged = true;
					}
				}
			}
		}

		public void WhisperClientSubscribe(ushort userId)
		{
			lock (subscriptionLockObj)
			{
				if (!clientSubscriptionsSetup.Contains(userId))
					clientSubscriptionsSetup.Add(userId);
				subscriptionSetupChanged = true;
			}
		}

		public void WhisperClientUnsubscribe(ushort userId)
		{
			lock (subscriptionLockObj)
			{
				clientSubscriptionsSetup.Remove(userId);
				subscriptionSetupChanged = true;
			}
		}

		public void ClearTemporary()
		{
			lock (subscriptionLockObj)
			{
				ulong[] removeList = channelSubscriptionsSetup
					.Where(kvp => kvp.Value)
					.Select(kvp => kvp.Key)
					.ToArray();
				foreach (var chan in removeList)
				{
					channelSubscriptionsSetup.Remove(chan);
					subscriptionSetupChanged = true;
				}
			}
		}

		private void UpdatedSubscriptionCache()
		{
			if (!subscriptionSetupChanged)
				return;
			lock (subscriptionLockObj)
			{
				if (!subscriptionSetupChanged)
					return;
				channelSubscriptionsCache = channelSubscriptionsSetup.Keys.ToArray();
				clientSubscriptionsCache = clientSubscriptionsSetup.ToArray();
				subscriptionSetupChanged = false;
			}
		}

		#endregion
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
		[Info("Version for the client in the form of <version build>|<plattform>|<version sign>\n" +
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
