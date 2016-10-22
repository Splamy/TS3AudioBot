namespace TS3AudioBot.ResourceFactories
{
	using Helper;

	public interface IPlaylistFactory
	{
		string SubCommandName { get; }
		AudioType FactoryFor { get; }

		bool MatchLink(string uri);

		R<Playlist> GetPlaylist(string url);
	}
}