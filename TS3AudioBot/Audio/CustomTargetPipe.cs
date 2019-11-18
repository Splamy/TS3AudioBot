// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;
using TSLib;
using TSLib.Audio;
using TSLib.Full;

namespace TS3AudioBot.Audio
{
	internal class CustomTargetPipe : IVoiceTarget, IAudioPassiveConsumer
	{
		public TargetSendMode SendMode { get; set; } = TargetSendMode.None;
		public ulong GroupWhisperTargetId { get; private set; }
		public GroupWhisperType GroupWhisperType { get; private set; }
		public GroupWhisperTarget GroupWhisperTarget { get; private set; }

		public IReadOnlyCollection<ClientId> WhisperClients
		{
			get { lock (subscriptionLockObj) { return clientSubscriptionsSetup.ToArray(); } }
		}
		public IReadOnlyCollection<ChannelId> WhisperChannel
		{
			get { lock (subscriptionLockObj) { return channelSubscriptionsSetup.Keys.ToArray(); } }
		}

		public bool Active
		{
			get
			{
				switch (SendMode)
				{
				case TargetSendMode.None:
					return false;
				case TargetSendMode.Whisper:
					UpdatedSubscriptionCache();
					return channelSubscriptionsCache.Length > 0 || clientSubscriptionsCache.Length > 0;
				default:
					return true;
				}
			}
		}

		private readonly Dictionary<ChannelId, bool> channelSubscriptionsSetup = new Dictionary<ChannelId, bool>();
		private readonly HashSet<ClientId> clientSubscriptionsSetup = new HashSet<ClientId>();
		private ChannelId[] channelSubscriptionsCache;
		private ClientId[] clientSubscriptionsCache;
		private bool subscriptionSetupChanged;
		private readonly object subscriptionLockObj = new object();

		private readonly TsFullClient client;

		public CustomTargetPipe(TsFullClient client)
		{
			this.client = client;
			subscriptionSetupChanged = true;
		}

		public void Write(Span<byte> data, Meta meta)
		{
			UpdatedSubscriptionCache();

			var codec = meta?.Codec ?? Codec.OpusMusic; // XXX a bit hacky
			switch (SendMode)
			{
			case TargetSendMode.None:
				break;
			case TargetSendMode.Voice:
				client.SendAudio(data, codec);
				break;
			case TargetSendMode.Whisper:
				client.SendAudioWhisper(data, codec, channelSubscriptionsCache, clientSubscriptionsCache);
				break;
			case TargetSendMode.WhisperGroup:
				client.SendAudioGroupWhisper(data, codec, GroupWhisperType, GroupWhisperTarget, GroupWhisperTargetId);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(SendMode), "Unknown send target");
			}
		}

		#region ITargetManager

		public void SetGroupWhisper(GroupWhisperType type, GroupWhisperTarget target, ulong targetId = 0)
		{
			GroupWhisperType = type;
			GroupWhisperTarget = target;
			GroupWhisperTargetId = targetId;
		}

		public void WhisperChannelSubscribe(bool temp, params ChannelId[] channels)
		{
			lock (subscriptionLockObj)
			{
				foreach (var channel in channels)
				{
					if (channelSubscriptionsSetup.TryGetValue(channel, out var subscriptionTemp))
					{
						channelSubscriptionsSetup[channel] = !subscriptionTemp || !temp;
					}
					else
					{
						channelSubscriptionsSetup[channel] = !temp;
						subscriptionSetupChanged = true;
					}
				}
			}
		}

		public void WhisperChannelUnsubscribe(bool temp, params ChannelId[] channels)
		{
			lock (subscriptionLockObj)
			{
				foreach (var channel in channels)
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
		}

		public void WhisperClientSubscribe(params ClientId[] userId)
		{
			lock (subscriptionLockObj)
			{
				clientSubscriptionsSetup.UnionWith(userId);
				subscriptionSetupChanged = true;
			}
		}

		public void WhisperClientUnsubscribe(params ClientId[] userId)
		{
			lock (subscriptionLockObj)
			{
				clientSubscriptionsSetup.ExceptWith(userId);
				subscriptionSetupChanged = true;
			}
		}

		public void ClearTemporary()
		{
			lock (subscriptionLockObj)
			{
				var removeList = channelSubscriptionsSetup
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
}
