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
		YtNoConnection,
		YtNoVideosExtracted,
		YtNoFMTS,
		ScInvalidLink,
	}
}
