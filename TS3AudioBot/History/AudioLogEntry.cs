namespace TS3AudioBot.History
{
	using System;
	using System.Globalization;
	using System.Text;
	using TS3AudioBot.ResourceFactories;

	public class AudioLogEntry : AudioResource
	{
		public uint Id { get; }
		public uint UserInvokeId { get; set; }
		public uint PlayCount { get; set; }
		public DateTime Timestamp { get; set; }
		public override AudioType AudioType { get; }

		public long FilePosIndex { get; set; }

		public AudioLogEntry(uint id, AudioType audioType, string resId) : base(resId, null)
		{
			Id = id;
			PlayCount = 0;
			AudioType = audioType;
		}

		public string ToFileString()
		{
			StringBuilder strb = new StringBuilder();
			// HEX STRINGS
			strb.Append(AsHex(Id));
			strb.Append(",");
			strb.Append(AsHex(UserInvokeId));
			strb.Append(",");
			strb.Append(AsHex(PlayCount));
			strb.Append(",");
			strb.Append(AsHex(Timestamp.ToFileTime()));
			strb.Append(",");

			// OTHER STRINGS
			strb.Append(AudioType.ToString());
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(ResourceId));
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(ResourceTitle));

			return strb.ToString();
		}

		public static AudioLogEntry Parse(string line, long readIndex)
		{
			string[] strParts = line.Split(',');
			if (strParts.Length != 7)
				return null;
			// Array.ForEach(strParts) // check if spacetrims are needed
			int index = 0;
			uint id = uint.Parse(strParts[index++], NumberStyles.HexNumber);
			uint userInvId = uint.Parse(strParts[index++], NumberStyles.HexNumber);
			uint playCount = uint.Parse(strParts[index++], NumberStyles.HexNumber);
			long dtStamp = long.Parse(strParts[index++], NumberStyles.HexNumber);
			DateTime dateTime = DateTime.FromFileTime(dtStamp);
			AudioType audioType;
			if (!Enum.TryParse(strParts[index++], out audioType))
				return null;
			string resId = Uri.UnescapeDataString(strParts[index++]);
			string title = Uri.UnescapeDataString(strParts[index++]);
			return new AudioLogEntry(id, audioType, resId)
			{
				PlayCount = playCount,
				Timestamp = dateTime,
				ResourceTitle = title,
				UserInvokeId = userInvId,
				FilePosIndex = readIndex,
			};
		}

		private static string AsHex(uint num) => num.ToString("X8", CultureInfo.InvariantCulture);
		private static string AsHex(long num) => num.ToString("X16", CultureInfo.InvariantCulture);

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "[{0}] @ {1} by {2}: {3}, ({4})", Id, Timestamp, UserInvokeId, ResourceTitle, ResourceId);
		}

		public override string Play()
		{
			throw new NotSupportedException();
		}
	}

}
