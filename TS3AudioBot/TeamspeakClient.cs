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
	using CSCore;
	using CSCore.Codecs;
	using CSCore.Streams;
	using Helper;
	using System;
	using TS3Client;
	using TS3Client.Full;
	using TS3Client.Messages;
	using System.Collections.Generic;

	class TeamspeakClient : TeamspeakControl, IPlayerConnection, ITargetManager
	{
		protected TS3FullClient tsFullClient;

		private const Codec SendCodec = Codec.OpusMusic;
		private readonly TimeSpan sendCheckInterval = TimeSpan.FromMilliseconds(50);
		private readonly TimeSpan audioBufferLength = TimeSpan.FromSeconds(1);

		private TickWorker sendTick;
		private VolumeSource audioVolume;
		private IWaveSource audioStream;
		private AudioEncoder encoder;
		private PreciseAudioTimer audioTimer;
		private byte[] audioBuffer;
		private Dictionary<ulong, SubscriptionData> channelSubscriptions;

		public TeamspeakClient() : base(ClientType.Full)
		{
            Util.Init(ref channelSubscriptions);
			tsFullClient = (TS3FullClient)tsBaseClient;
			sendTick = TickPool.RegisterTick(AudioSend, sendCheckInterval, false);
			encoder = new AudioEncoder(SendCodec);
			audioTimer = new PreciseAudioTimer(encoder.SampleRate, encoder.BitsPerSample, encoder.Channel);
		}

		public override void Connect()
		{
			tsFullClient.Connect(new ConnectionData
			{
				UserName = "AudioBot",
				Hostname = "splamy.de",
				Port = 9987,
				PrivateKey = "MG8DAgeAAgEgAiEA76LIMLxiti7JTkl4yeNRPiApiGyIRqF9km3ByalVZd8CIQDGz9jUYZIXgkSsyCYVywl0HTKoP+0Ch8OG+ia4boW0UAIgSY/aeQNjq0ryRiaifd6SMKbG9+KuoN/oXEu/lyr+SNg=",
				KeyOffset = 57451630,
				LastCheckedKeyOffset = 57451630,
			});
		}

		public override ClientData GetSelf()
		{
			// TODO call to clientinfo with own id
			throw new NotImplementedException();
		}

		private void AudioSend()
		{
			if ((audioBuffer?.Length ?? 0) < encoder.OptimalPacketSize)
				audioBuffer = new byte[encoder.OptimalPacketSize];

			while (audioTimer.BufferLength < audioBufferLength)
			{
				int read = audioStream.Read(audioBuffer, 0, encoder.OptimalPacketSize);
				if (read == 0)
				{
					OnSongEnd?.Invoke(this, new EventArgs());
					return;
				}

				encoder.PushPCMAudio(audioBuffer, read);
				audioTimer.PushBytes(read);

				Tuple<byte[], int> encodedArr = null;
				while ((encodedArr = encoder.GetPacket()) != null)
				{
					tsFullClient.SendAudio(encodedArr.Item1, encodedArr.Item2, encoder.Codec);
				}
			}
		}

		#region IPlayerConnection

		public event EventHandler OnSongEnd;

		public R AudioStart(string url)
		{
			try
			{
				var baseStream = CodecFactory.Instance.GetCodec(new Uri(url));
				var sampleStream = baseStream.ToSampleSource();
				if (audioVolume == null)
					audioVolume = new VolumeSource(sampleStream);
				else
					audioVolume.BaseSource = sampleStream;
				audioStream = audioVolume.ChangeSampleRate(48000).ToWaveSource(16);

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
			return R.OkR;
		}

		public R<TimeSpan> GetLength()
		{
			var len = audioStream.Length;
			if (len == 0) return "No length could be returned";
			else return TimeSpan.FromSeconds(len); // TODO Unit ???????
		}

		public R<TimeSpan> GetPosition()
		{
			throw new NotImplementedException();
		}
		public void SetPosition(TimeSpan value)
		{
			throw new NotImplementedException();
		}

		public R<int> GetVolume()
		{
			if (audioVolume == null) return "No active stream for volume";
			return (int)(audioVolume.Volume * 100);
		}
		public void SetVolume(int value)
		{
			if (audioVolume != null)
				audioVolume.Volume = value / 100f;
		}

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
			throw new NotImplementedException();
			RestoreSubscriptions(playData.Invoker);
		}

		public void OnResourceStopped(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		public void WhisperChannelSubscribe(ulong channel, bool manual)
		{
			throw new NotImplementedException();
			SubscriptionData subscriptionData;
			if (!channelSubscriptions.TryGetValue(channel, out subscriptionData))
			{
				subscriptionData = new SubscriptionData { Id = channel, Manual = manual };
				channelSubscriptions.Add(channel, subscriptionData);
			}
			subscriptionData.Enabled = true;
			subscriptionData.Manual = subscriptionData.Manual || manual;
		}

		public void WhisperChannelUnsubscribe(ulong channel, bool manual)
		{
			throw new NotImplementedException();
			SubscriptionData subscriptionData;
			if (!channelSubscriptions.TryGetValue(channel, out subscriptionData))
			{
				subscriptionData = new SubscriptionData { Id = channel, Manual = false };
				channelSubscriptions.Add(channel, subscriptionData);
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
			throw new NotImplementedException();
		}

		public void WhisperClientUnsubscribe(ushort userId)
		{
			throw new NotImplementedException();	
		}

		private void RestoreSubscriptions(ClientData invokingUser)
		{
			WhisperChannelSubscribe(invokingUser.ChannelId, false);
			foreach (var data in channelSubscriptions)
			{
				if (data.Value.Enabled)
				{
					if (data.Value.Manual)
						WhisperChannelSubscribe(data.Value.Id, false);
					else if (!data.Value.Manual && invokingUser.ChannelId != data.Value.Id)
						WhisperChannelUnsubscribe(data.Value.Id, false);
				}
			}
		}

		#endregion
	}
}
