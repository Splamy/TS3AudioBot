namespace TS3AudioBot.ResourceFactories.Youtube
{
#pragma warning disable CS0649, CS0169, IDE1006
	// ReSharper disable ClassNeverInstantiated.Local, InconsistentNaming
	public class JsonVideoListResponse // # youtube#videoListResponse
	{
		public string nextPageToken { get; set; }
		public JsonVideo[] items { get; set; }
	}
	public class JsonVideo // youtube#video
	{
		public JsonContentDetails contentDetails { get; set; }
		public JsonSnippet snippet { get; set; }
	}
	public class JsonSearchListResponse // youtube#searchListResponse
	{
		public JsonSearchResult[] items { get; set; }
	}
	public class JsonSearchResult // youtube#searchResult
	{
		public JsonContentDetails id { get; set; }
		public JsonSnippet snippet { get; set; }
	}
	public class JsonContentDetails
	{
		public string videoId { get; set; }
	}
	public class JsonSnippet
	{
		public string title { get; set; }
		public JsonThumbnailList thumbnails { get; set; }
	}
	public class JsonThumbnailList
	{
		public JsonThumbnail @default { get; set; }
		public JsonThumbnail medium { get; set; }
		public JsonThumbnail high { get; set; }
		public JsonThumbnail standard { get; set; }
		public JsonThumbnail maxres { get; set; }
	}
	public class JsonThumbnail
	{
		public string url { get; set; }
		public int heigth { get; set; }
		public int width { get; set; }
	}
	// Custom json
	public class JsonPlayerResponse
	{
		public JsonStreamingData streamingData { get; set; }
		public JsonVideoDetails videoDetails { get; set; }
	}
	public class JsonStreamingData
	{
		public string dashManifestUrl { get; set; }
		public string hlsManifestUrl { get; set; }
	}
	public class JsonVideoDetails
	{
		public string title { get; set; }
		public bool? isLive { get; set; }
		public bool useCipher { get; set; }
		public bool isLiveContent { get; set; }
	}
	public class JsonPlayFormat
	{
		public string mimeType { get; set; }
		public int bitrate { get; set; }
		public string cipher { get; set; }
		public string url { get; set; }
	}
	// ReSharper enable ClassNeverInstantiated.Local, InconsistentNaming
#pragma warning restore CS0649, CS0169, IDE1006
}
