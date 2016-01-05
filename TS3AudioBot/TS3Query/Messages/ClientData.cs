namespace TS3Query.Messages
{
	// clientlist
	public class ClientData : Response
	{
        [QuerySerialized("clid")]
        public int Id;

        [QuerySerialized("cid")]
        public int ChannelId;

        [QuerySerialized("client_database_id")]
        public int DatabaseId;

        [QuerySerialized("client_nickname")]
        public string NickName;

        [QuerySerialized("client_type")]
        public ClientType Type;
    }
}
