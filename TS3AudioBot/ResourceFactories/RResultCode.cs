namespace TS3AudioBot.ResourceFactories
{
	enum RResultCode // Ressource Result Code
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
