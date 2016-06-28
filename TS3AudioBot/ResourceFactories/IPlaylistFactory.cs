namespace TS3AudioBot.ResourceFactories
{
	using System;
	using Helper;

	interface IPlaylistFactory
	{
		string SubCommandName { get; }

		bool MatchLink(string uri);

		R<Playlist> GetPlaylist(string url);
	}
}