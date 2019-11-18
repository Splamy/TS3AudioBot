// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;
using TSLib;
using TSLib.Audio;

namespace TS3AudioBot.Audio
{
	/// <summary>Used to specify playing mode and active targets to send to.</summary>
	public interface IVoiceTarget
	{
		TargetSendMode SendMode { get; set; }
		ulong GroupWhisperTargetId { get; }
		GroupWhisperType GroupWhisperType { get; }
		GroupWhisperTarget GroupWhisperTarget { get; }
		void SetGroupWhisper(GroupWhisperType type, GroupWhisperTarget target, ulong targetId);

		IReadOnlyCollection<ClientId> WhisperClients { get; }
		IReadOnlyCollection<ChannelId> WhisperChannel { get; }

		/// <summary>Adds a channel to the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="temp">When set to true this channel will be cleared with
		/// the next <see cref="ClearTemporary"/> call (unless overwritten with false).</param>
		void WhisperChannelSubscribe(bool temp, params ChannelId[] channel);
		/// <summary>Removes a channel from the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="temp">When set to true this channel will be cleared with
		/// the next <see cref="ClearTemporary"/> call (unless overwritten with false).</param>
		void WhisperChannelUnsubscribe(bool temp, params ChannelId[] channel);
		void ClearTemporary();
		void WhisperClientSubscribe(params ClientId[] userId);
		void WhisperClientUnsubscribe(params ClientId[] userId);
	}
}
