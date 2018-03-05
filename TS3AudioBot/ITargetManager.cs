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
	using System.Collections.Generic;
	using TS3Client;
	using TS3Client.Audio;

	/// <summary>Used to specify playing mode and active targets to send to.</summary>
	public interface ITargetManager
	{
		TargetSendMode SendMode { get; set; }
		ulong GroupWhisperTargetId { get; }
		GroupWhisperType GroupWhisperType { get; }
		GroupWhisperTarget GroupWhisperTarget { get; }
		void SetGroupWhisper(GroupWhisperType type, GroupWhisperTarget target, ulong targetId);

		IReadOnlyCollection<ushort> WhisperClients { get; }
		IReadOnlyCollection<ulong> WhisperChannel { get; }

		/// <summary>Adds a channel to the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="temp">When set to true this channel will be cleared with
		/// the next <see cref="ClearTemporary"/> call (unless overwritten with false).</param>
		void WhisperChannelSubscribe(ulong channel, bool temp);
		/// <summary>Removes a channel from the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="temp">When set to true this channel will be cleared with
		/// the next <see cref="ClearTemporary"/> call (unless overwritten with false).</param>
		void WhisperChannelUnsubscribe(ulong channel, bool temp);
		void ClearTemporary();
		void WhisperClientSubscribe(ushort userId);
		void WhisperClientUnsubscribe(ushort userId);
	}
}
