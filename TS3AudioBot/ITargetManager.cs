namespace TS3AudioBot
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	public interface ITargetManager
	{
		/// <summary>Adds a channel to the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="manual">Should be true if the command was invoked by a user,
		/// or false if the channel is added automatically by a play command.</param>
		void WhisperChannelSubscribe(ulong channel, bool manual);
		/// <summary>Removes a channel from the audio streaming list.</summary>
		/// <param name="channel">The id of the channel.</param>
		/// <param name="manual">Should be true if the command was invoked by a user,
		/// or false if the channel was removed automatically by an internal stop.</param>
		void WhisperChannelUnsubscribe(ulong channel, bool manual);
		void WhisperClientSubscribe(ushort userId);
		void WhisperClientUnsubscribe(ushort userId);

		void OnResourceStarted(object sender, PlayInfoEventArgs playData);
		void OnResourceStopped(object sender, EventArgs e);
	}
}
