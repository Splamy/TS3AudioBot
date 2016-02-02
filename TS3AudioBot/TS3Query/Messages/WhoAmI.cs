namespace TS3Query.Messages
{
    public class WhoAmI : Response
	{
        [QuerySerialized("virtualserver_status")]
        public string VirtualServerStatus;

        [QuerySerialized("virtualserver_id")]
        public int VirtualServerId;

        [QuerySerialized("virtualserver_unique_identifier")]
        public string VirtualServerUid;

        [QuerySerialized("virtualserver_port")]
        public ushort VirtualServerPort;

        [QuerySerialized("client_id")]
        public ushort ClientId;

        [QuerySerialized("client_channel_id")]
        public int ChannelId;

        [QuerySerialized("client_nickname")]
        public string NickName;

        [QuerySerialized("client_database_id")]
        public ulong DatabaseId;

        [QuerySerialized("client_login_name")]
        public string LoginName;

        [QuerySerialized("client_unique_identifier")]
        public string Uid;

        [QuerySerialized("client_origin_server_id")]
        public int OriginServerId;
    }
}
