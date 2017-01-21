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

		private TickWorker sendTick;
		private float volume = 1;
		private Process ffmpegProcess;
		private AudioEncoder encoder;
		private PreciseAudioTimer audioTimer;
		private byte[] audioBuffer;
		private Dictionary<ulong, SubscriptionData> channelSubscriptionsSetup;
		private List<ulong> channelSubscriptions;
		private List<ushort> clientSubscriptions;
		private Ts3FullClientData ts3FullClientData;

		public Ts3Full(Ts3FullClientData tfcd) : base(ClientType.Full)
		{
			ts3FullClientData = tfcd;
			Util.Init(ref channelSubscriptionsSetup);
			Util.Init(ref channelSubscriptions);
			Util.Init(ref clientSubscriptions);
			tsFullClient = (Ts3FullClient)tsBaseClient;
			sendTick = TickPool.RegisterTick(AudioSend, sendCheckInterval, false);
			encoder = new AudioEncoder(SendCodec);
			audioTimer = new PreciseAudioTimer(encoder.SampleRate, encoder.BitsPerSample, encoder.Channels);
		}

		public override void Connect()
		{
			IdentityData identity;
			if (string.IsNullOrEmpty(ts3FullClientData.identity))
			{
				identity = Ts3Crypt.GenerateNewIdentity();
				ts3FullClientData.identity = identity.PrivateKeyString;
				ts3FullClientData.identityoffset = identity.ValidKeyOffset;
			}
			else
			{
				identity = Ts3Crypt.LoadIdentity(ts3FullClientData.identity, ts3FullClientData.identityoffset);
			}

			tsFullClient.OnErrorEvent += (s, e) => { Log.Write(Log.Level.Debug, e.ErrorFormat()); };
			tsFullClient.Connect(new ConnectionDataFull
			{
				Username = "AudioBot",
				Hostname = ts3FullClientData.host,
				Port = ts3FullClientData.port,
				Identity = identity,
			});
		}

		public override ClientData GetSelf()
		{
			var cd = Generator.ActivateResponse<ClientData>();
			var data = tsBaseClient.ClientInfo(tsFullClient.ClientId);
			cd.ChannelId = data.ChannelId;
			cd.DatabaseId = data.DatabaseId;
			cd.ClientId = tsFullClient.ClientId;
			cd.NickName = data.NickName;
			cd.ClientType = tsBaseClient.ClientType;
			return cd;
		}

		private void AudioSend()
		{
			if (ffmpegProcess == null)
				return;

			if ((audioBuffer?.Length ?? 0) < encoder.OptimalPacketSize)
				audioBuffer = new byte[encoder.OptimalPacketSize];

			while (audioTimer.BufferLength < audioBufferLength)
			{
				int read = ffmpegProcess.StandardOutput.BaseStream.Read(audioBuffer, 0, encoder.OptimalPacketSize);
				if (read == 0)
				{
					AudioStop();
					OnSongEnd?.Invoke(this, new EventArgs());
					return;
				}

				AudioModifier.AdjustVolume(audioBuffer, read, volume);
				encoder.PushPCMAudio(audioBuffer, read);
				audioTimer.PushBytes(read);

				Tuple<byte[], int> encodedArr = null;
				while ((encodedArr = encoder.GetPacket()) != null)
				{
					tsFullClient.SendAudioWhisper(encodedArr.Item1, encodedArr.Item2, encoder.Codec, channelSubscriptions, clientSubscriptions);
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
						FileName = ts3FullClientData.ffmpegpath,
						Arguments = $"-hide_banner -nostats -loglevel panic -i \"{ url }\" -ar 48000 -f s16le -acodec pcm_s16le pipe:1",
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

		public R<TimeSpan> GetLength()
		{
			throw new NotImplementedException();
		}

		public R<TimeSpan> GetPosition()
		{
			throw new NotImplementedException();
		}
		public void SetPosition(TimeSpan value)
		{
			throw new NotImplementedException();
		}

		public R<int> GetVolume() => (int)Math.Round(volume * 100);
		public void SetVolume(int value) => volume = value / 100f;

		public void Initialize() { }

		public R<bool> IsPaused() => sendTick.Active;
		public void SetPaused(bool value)
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

		public R<bool> IsPlaying() => sendTick.Active;

		public R<bool> IsRepeated()
		{
			//throw new NotImplementedException();
			return false;
		}
		public void SetRepeated(bool value)
		{
			//throw new NotImplementedException();
		}

		#endregion

		#region ITargetManager

		public void OnResourceStarted(object sender, PlayInfoEventArgs playData)
		{
			RestoreSubscriptions(playData.Invoker);
		}

		public void OnResourceStopped(object sender, EventArgs e)
		{
			// TODO despawn or go back
		}

		public void WhisperChannelSubscribe(ulong channel, bool manual)
		{
			// TODO move to requested channel
			// TODO spawn new client
			SubscriptionData subscriptionData;
			if (!channelSubscriptionsSetup.TryGetValue(channel, out subscriptionData))
			{
				subscriptionData = new SubscriptionData { Id = channel, Manual = manual };
				channelSubscriptionsSetup.Add(channel, subscriptionData);
			}
			subscriptionData.Enabled = true;
			subscriptionData.Manual = subscriptionData.Manual || manual;
		}

		public void WhisperChannelUnsubscribe(ulong channel, bool manual)
		{
			SubscriptionData subscriptionData;
			if (!channelSubscriptionsSetup.TryGetValue(channel, out subscriptionData))
			{
				subscriptionData = new SubscriptionData { Id = channel, Manual = false };
				channelSubscriptionsSetup.Add(channel, subscriptionData);
			}
			if (manual)
			{
				subscriptionData.Manual = true;
				subscriptionData.Enabled = false;
			}
			else if (!subscriptionData.Manual)
			{
				subscriptionData.Enabled = false;
			}
		}

		public void WhisperClientSubscribe(ushort userId)
		{
			if (!clientSubscriptions.Contains(userId))
				clientSubscriptions.Add(userId);
		}

		public void WhisperClientUnsubscribe(ushort userId)
		{
			clientSubscriptions.Remove(userId);
		}

		private void RestoreSubscriptions(ClientData invokingUser)
		{
			WhisperChannelSubscribe(invokingUser.ChannelId, false);
			foreach (var data in channelSubscriptionsSetup)
			{
				if (data.Value.Enabled)
				{
					if (data.Value.Manual)
						WhisperChannelSubscribe(data.Value.Id, false);
					else if (!data.Value.Manual && invokingUser.ChannelId != data.Value.Id)
						WhisperChannelUnsubscribe(data.Value.Id, false);
				}
			}
			channelSubscriptions = channelSubscriptionsSetup.Values.Select(v => v.Id).ToList();
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
		[Info("the address of the TeamSpeak3 server")]
		public string host { get; set; }
		[Info("the port of the TeamSpeak3 server", "9987")]
		public ushort port { get; set; }
		[Info("the client identity", "")]
		public string identity { get; set; }
		[Info("the client identity security offset", "0")]
		public ulong identityoffset { get; set; }
		[Info("the relative or full path to ffmpeg", "ffmpeg")]
		public string ffmpegpath { get; set; }
	}
}
