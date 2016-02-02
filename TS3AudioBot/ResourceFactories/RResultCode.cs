namespace TS3AudioBot.ResourceFactories
{
	public enum RResultCode // Resource Result Code
	{
		UnknowError,
		Success,
		MediaInvalidUri,
		MediaUnknownUri,
		MediaNoWebResponse,
		MediaFileNotFound,
		YtIdNotFound,
		YtNoVideosExtracted,
		YtNoFMTS,
		ScInvalidLink,
		TwitchInvalidUrl,
		TwitchMalformedM3u8File,
		TwitchNoStreamsExtracted,

		// general errors
		NoConnection,
	}
}
