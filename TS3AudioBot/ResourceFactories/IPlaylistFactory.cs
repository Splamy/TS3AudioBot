namespace TS3AudioBot.ResourceFactories
{
	using Helper;

	interface IPlaylistFactory
	{
		bool MatchLink(string uri);

		R<Playlist> GetPlaylist(string url);
	}
}