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

namespace TS3AudioBot
{
	using Audio;
	using Helper;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using TS3Client;
	using TS3Client.Full;
	using TS3Client.Messages;

	class Ts3Full : TeamspeakControl, IPlayerConnection, ITargetManager
	{
		protected Ts3FullClient tsFullClient;

		private const Codec SendCodec = Codec.OpusMusic;
		private readonly TimeSpan sendCheckInterval = TimeSpan.FromMilliseconds(5);
		private readonly TimeSpan audioBufferLength = TimeSpan.FromMilliseconds(20);
		private const uint stallCountInterval = 50;
		private const uint stallNoErrorCountMax = 2;
		private static readonly string[] quitMessages = new[]
		{ "I'm outta here", "You're boring", "Have a nice day", "Bye", "Good night",
		  "Nothing to do here", "Taking a break", "Lorem ipsum dolor sit amet…",
		  "Nothing can hold me back", "It's getting quiet", "Drop the bazzzzzz",
		  "Never gonna give you up", "Never gonna let you down", "Keep rockin' it",
		  "?", "c(ꙩ_Ꙩ)ꜿ", "I'll be back", "Your advertisement could be here"};

		private Ts3FullClientData ts3FullClientData;
		private float volume = 1;

		private TickWorker sendTick;
		private Process ffmpegProcess;
		private AudioEncoder encoder;
		private PreciseAudioTimer audioTimer;
		private byte[] audioBuffer;
		private bool isStall;
		private uint stallCount;
		private uint stallNoErrorCount;

		private Dictionary<ulong, bool> channelSubscriptionsSetup;
		private List<ushort> clientSubscriptionsSetup;
		private ulong[] channelSubscriptionsCache;
		private ushort[] clientSubscriptionsCache;
		private bool subscriptionSetupChanged;
		private readonly object subscriptionLockObj = new object();

		public Ts3Full(Ts3FullClientData tfcd) : base(ClientType.Full)
		{
			tsFullClient = (Ts3FullClient)tsBaseClient;

			ts3FullClientData = tfcd;

			sendTick = TickPool.RegisterTick(AudioSend, sendCheckInterval, false);
			encoder = new AudioEncoder(SendCodec);
			audioTimer = new PreciseAudioTimer(encoder.SampleRate, encoder.BitsPerSample, encoder.Channels);
			isStall = false;
			stallCount = 0;

			Util.Init(ref channelSubscriptionsSetup);
			Util.Init(ref clientSubscriptionsSetup);
			subscriptionSetupChanged = true;
		}

		public override void Connect()
		{
			IdentityData identity;
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

			tsFullClient.QuitMessage = quitMessages[Util.Random.Next(0, quitMessages.Length)];
			tsFullClient.OnErrorEvent += TsFullClient_OnErrorEvent;
			tsFullClient.Connect(new ConnectionDataFull
			{
				Username = "AudioBot",
				Hostname = ts3FullClientData.Host,
				Port = ts3FullClientData.Port,
				Identity = identity,
			});

		}

		private void TsFullClient_OnErrorEvent(object sender, TS3Client.Commands.CommandError e)
		{
			const int whisper_no_targets = 0x070c;

			if (e.Id == whisper_no_targets)
			{
				stallNoErrorCount = 0;
				isStall = true;
			}
			else
				Log.Write(Log.Level.Debug, e.ErrorFormat());
		}

		public override ClientData GetSelf()
		{
			var data = tsBaseClient.ClientInfo(tsFullClient.ClientId);
			var cd = new ClientData
			{
				ChannelId = data.ChannelId,
				DatabaseId = data.DatabaseId,
				ClientId = tsFullClient.ClientId,
				NickName = data.NickName,
				ClientType = tsBaseClient.ClientType
			};
			return cd;
		}

		private void AudioSend()
		{
			if (ffmpegProcess == null)
				return;

			if ((audioBuffer?.Length ?? 0) < encoder.OptimalPacketSize)
				audioBuffer = new byte[encoder.OptimalPacketSize];

			UpdatedSubscriptionCache();

			while (audioTimer.BufferLength < audioBufferLength)
			{
				int read = ffmpegProcess.StandardOutput.BaseStream.Read(audioBuffer, 0, encoder.OptimalPacketSize);
				if (read == 0)
				{
					if (audioTimer.BufferLength < TimeSpan.Zero && !encoder.HasPacket)
					{
						AudioStop();
						OnSongEnd?.Invoke(this, new EventArgs());
					}
					return;
				}

				audioTimer.PushBytes(read);
				if (isStall)
				{
					stallCount++;
					if (stallCount % stallCountInterval == 0)
					{
						stallNoErrorCount++;
					}
					if (stallNoErrorCount > stallNoErrorCountMax)
					{
						stallCount = 0;
						isStall = false;
						break;
					}
				}

				AudioModifier.AdjustVolume(audioBuffer, read, volume);
				encoder.PushPCMAudio(audioBuffer, read);

				Tuple<byte[], int> encodedArr = null;
				while ((encodedArr = encoder.GetPacket()) != null)
				{
					if (channelSubscriptionsCache.Length == 0 && clientSubscriptionsCache.Length == 0)
						tsFullClient.SendAudio(encodedArr.Item1, encodedArr.Item2, encoder.Codec);
					else
						tsFullClient.SendAudioWhisper(encodedArr.Item1, encodedArr.Item2, encoder.Codec, channelSubscriptionsCache, clientSubscriptionsCache);
				}
			}
		}

		#region IPlayerConnection

		public event EventHandler OnSongEnd;

		public R AudioStart(string url)
		{
			try
			{
				ffmpegProcess = new Process()
				{
					StartInfo = new ProcessStartInfo()
					{
						FileName = ts3FullClientData.FfmpegPath,
						Arguments = $"-hide_banner -nostats -loglevel panic -i \"{ url }\" -ac 2 -ar 48000 -f s16le -acodec pcm_s16le pipe:1",
						RedirectStandardOutput = true,
						RedirectStandardInput = true,
						RedirectStandardError = true,
						UseShellExecute = false,
						CreateNoWindow = true,
					}
				};
				ffmpegProcess.Start();

				audioTimer.Start();
				sendTick.Active = true;
				return R.OkR;
			}
			catch (Exception ex) { return $"Unable to create stream ({ex.Message})"; }
		}

		public R AudioStop()
		{
			sendTick.Active = false;
			audioTimer.Stop();
			try
			{
				if (!ffmpegProcess?.HasExited ?? false)
					ffmpegProcess?.Kill();
				else
					ffmpegProcess?.Close();
			}
			catch (InvalidOperationException) { }
			ffmpegProcess = null;
			return R.OkR;
		}

		public TimeSpan Length
		{
			get { throw new NotImplementedException(); }
		}

		public TimeSpan Position
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}

		public int Volume
		{
			get { return (int)Math.Round(volume * 100); }
			set { volume = value / 100f; }
		}

		public void Initialize() { }

		public bool Paused
		{
			get { return sendTick.Active; }
			set
			{
				if (sendTick.Active == value)
				{
					sendTick.Active = !value;
					if (value)
						audioTimer.Stop();
					else
						audioTimer.Start();
				}
			}
		}

		public bool Playing => sendTick.Active;

		public bool Repeated { get { return false; } set { } }


		#endregion

		#region ITargetManager

		public void OnResourceStarted(object sender, PlayInfoEventArgs playData)
		{
			if (playData.Invoker.Channel.HasValue)
				RestoreSubscriptions(playData.Invoker.Channel.Value);
		}

		public void OnResourceStopped(object sender, EventArgs e)
		{
			// TODO despawn or go back
		}

		public void WhisperChannelSubscribe(ulong channel, bool manual)
		{
			// TODO move to requested channel
			// TODO spawn new client
			lock (subscriptionLockObj)
			{
				bool subscriptionManual;
				if (channelSubscriptionsSetup.TryGetValue(channel, out subscriptionManual))
					channelSubscriptionsSetup[channel] = subscriptionManual || manual;
				else
				{
					channelSubscriptionsSetup[channel] = manual;
					subscriptionSetupChanged = true;
				}
			}
		}

		public void WhisperChannelUnsubscribe(ulong channel, bool manual)
		{
			lock (subscriptionLockObj)
			{
				if (manual)
				{
					subscriptionSetupChanged |= channelSubscriptionsSetup.Remove(channel);
				}
				else
				{
					bool subscriptionManual;
					if (channelSubscriptionsSetup.TryGetValue(channel, out subscriptionManual) && !subscriptionManual)
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

		private void RestoreSubscriptions(ulong channelId)
		{
			WhisperChannelSubscribe(channelId, false);
			lock (subscriptionLockObj)
			{
				ulong[] removeList = channelSubscriptionsSetup
					.Where(kvp => !kvp.Value && kvp.Key != channelId)
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
			if (subscriptionSetupChanged)
			{
				lock (subscriptionLockObj)
				{
					channelSubscriptionsCache = channelSubscriptionsSetup.Keys.ToArray();
					clientSubscriptionsCache = clientSubscriptionsSetup.ToArray();
					subscriptionSetupChanged = false;
				}
			}
		}

		#endregion

		public class SubscriptionData
		{
			public ulong Id { get; set; }
			public bool Enabled { get; set; }
			public bool Manual { get; set; }
		}
	}

	public class Ts3FullClientData : ConfigData
	{
		[Info("The address of the TeamSpeak3 server")]
		public string Host { get; set; }
		[Info("The port of the TeamSpeak3 server", "9987")]
		public ushort Port { get; set; }
		[Info("| DO NOT MAKE THIS KEY PUBLIC | The client identity", "")]
		public string Identity { get; set; }
		[Info("The client identity security offset", "0")]
		public ulong IdentityOffset { get; set; }
		[Info("The path to ffmpeg", "ffmpeg")]
		public string FfmpegPath { get; set; }
	}
}
