namespace TS3Query
{
	// http://media.teamspeak.com/ts3_literature/TeamSpeak%203%20Server%20Query%20Manual.pdf
	// public_definitions.h from the ts3 plugin library

	public enum HostMessageMode
	{
		/// <summary>Display message in chatlog.</summary>
		Log = 1,
		/// <summary>Display message in modal dialog.</summary>
		Modal,
		/// <summary>Display message in modal dialog and close connection.</summary>
		ModalQuit
	}
	
	public enum HostBannerMode
	{
		///<summary>Do not adjust.</summary>
		NoAdjust = 0,
		///<summary>Adjust but ignore aspect ratio (like TeamSpeak 2).</summary>
		IgnoreAspect,
		///<summary>Adjust and keep aspect ratio.</summary>
		KeepAspect
	}

	public enum Codec
	{
		///<summary>mono, 16bit, 8kHz</summary>
		SpeexNarrowband = 0,
		///<summary>mono, 16bit, 16kHz</summary>
		SpeexWideband,
		///<summary>mono, 16bit, 32kHz</summary>
		SpeexUltraWideband,
		///<summary>mono, 16bit, 48kHz</summary>
		CeltMono,
		///<summary>mono, 16bit, 48kHz, optimized for voice</summary>
		OpusVoice,
		///<summary>stereo, 16bit, 48kHz, optimized for music</summary>
		OpusMusic, 
	}
	
	public enum CodecEncryptionMode
	{
		///<summary>Configure per channel.</summary>
		Individual = 0,
		///<summary>Globally disabled.</summary>
		Disabled,
		///<summary>Globally enabled.</summary>
		Enabled
	}

	public enum LogLevel
	{
		///<summary>Everything that is really bad.</summary>
		Error = 1,
		///<summary>Everything that might be bad.</summary>
		Warning,
		///<summary>Output that might help find a problem.</summary>
		Debug,
		///<summary>Informational output.</summary>
		Info
	}

	public enum PermissionGroupDatabaseType
	{
		///<summary>Template group (used for new virtual servers).</summary>
		Template = 0,
		///<summary>Regular group (used for regular clients).</summary>
		Regular,
		///<summary>Global query group (used for ServerQuery clients).</summary>
		Query
	}

	public enum PermissionGroupType
	{
		///<summary>Server group permission.</summary>
		ServerGroup = 0,
		///<summary>Client specific permission.</summary>
		GlobalClient,
		///<summary>Channel specific permission.</summary>
		Channel,
		///<summary>Channel group permission.</summary>
		ChannelGroup,
		///<summary>Channel-client specific permission.</summary>
		ChannelClient
	}

	public enum TokenType
	{
		///<summary>Server group token (id1={groupID} id2=0).</summary>
		ServerGroup = 0,
		///<summary>Channel group token (id1={groupID} id2={channelID}).</summary>
		ChannelGroup
	}
}
