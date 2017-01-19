namespace TS3AudioBot.Web
{
	using System;
	using System.Collections.Specialized;
	using System.Web;

	[Serializable]
	public class UriExt : Uri
	{
		private NameValueCollection queryParam = null;
		public NameValueCollection QueryParam => queryParam ?? (queryParam = HttpUtility.ParseQueryString(Query));
		public UriExt(Uri copy) : base(copy.OriginalString) { }
	}
}
