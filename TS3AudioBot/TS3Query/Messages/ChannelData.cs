using System;

namespace TS3Query.Messages
{
	// channellist
    public class ChannelData : Response
	{
        [QuerySerialized("id")]
        public int Id;

        [QuerySerialized("pid")]
        public int ParentChannelId;

        [QuerySerialized("channel_order")]
        public int Order;

        [QuerySerialized("channel_name")]
        public string Name;

        [QuerySerialized("channel_topic")]
        public string Topic;

        [QuerySerialized("channel_flag_password")]
        public bool IsDefaultChannel;

        [QuerySerialized("channel_flag_password")]
		public bool HasPassword;

		[QuerySerialized("channel_flag_permanent")]
        public bool IsPermanent;

        [QuerySerialized("channel_flag_semi_permanent")]
        public bool IsSemiPermanent;

        [QuerySerialized("channel_codec")]
        public Codec Codec;

        [QuerySerialized("channel_codec_quality")]
        public int CodecQuality;

        [QuerySerialized("channel_needed_talk_power")]
        public int NeededTalkPower;

        [QuerySerialized("channel_icon_id")]
        public long IconId;

        [QuerySerialized("seconds_empty")]
        public TimeSpan DurationEmpty;

        [QuerySerialized("total_clients_family")]
        public int TotalFamilyClients;

        [QuerySerialized("channel_maxclients")]
        public int MaxClients;

        [QuerySerialized("channel_maxfamilyclients")]
        public int MaxFamilyClients;

        [QuerySerialized("total_clients")]
        public int TotalClients;

        [QuerySerialized("channel_needed_subscribe_power")]
        public int NeededSubscribePower;
    }
}
