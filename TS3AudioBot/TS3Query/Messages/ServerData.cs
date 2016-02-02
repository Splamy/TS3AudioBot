namespace TS3Query.Messages
{
	using System;

	// serverlist
	public class ServerData : Response
	{
		[QuerySerialized("virtualserver_id")]
		public int Id;

		[QuerySerialized("virtualserver_port")]
		public ushort Port;

		[QuerySerialized("virtualserver_status")]
		public string Status;

		[QuerySerialized("virtualserver_clientsonline")]
		public int ClientsOnline;

		[QuerySerialized("virtualserver_queryclientsonline")]
		public int QueriesOnline;

		[QuerySerialized("virtualserver_maxclients")]
		public int MaxClients;

		[QuerySerialized("virtualserver_uptime")]
		public TimeSpan Uptime;

		[QuerySerialized("virtualserver_name")]
		public string Name;

		[QuerySerialized("virtualserver_autostart")]
		public bool Autostart;

		[QuerySerialized("virtualserver_machine_id")]
		public string MachineId;

		[QuerySerialized("virtualserver_unique_identifier")]
		public string Uid;
	}
}
