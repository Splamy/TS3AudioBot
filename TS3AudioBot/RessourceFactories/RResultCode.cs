namespace TS3AudioBot.RessourceFactories
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
