namespace TS3Query.Messages
{
	// clientlist
	public class ClientData : Response
	{
		[QuerySerialized("clid")]
		public ushort Id;

		[QuerySerialized("cid")]
		public int ChannelId;

		[QuerySerialized("client_database_id")]
		public ulong DatabaseId;

		[QuerySerialized("client_nickname")]
		public string NickName;

		[QuerySerialized("client_type")]
		public ClientType Type;
	}
}
