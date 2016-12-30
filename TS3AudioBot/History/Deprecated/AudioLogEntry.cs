// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.History.Deprecated
{
	using ResourceFactories;
	using System;
	using System.Globalization;
	using System.Text;

	public class AudioLogEntry
	{
		/// <summary>A unique id for each <see cref="AudioRessource"/>, given by the history system.</summary>
		public uint Id { get; }
		/// <summary>The dbid of the teamspeak user, who played this song first.</summary>
		public uint UserInvokeId { get; set; }
		/// <summary>How often the song has been played.</summary>
		public uint PlayCount { get; set; }
		/// <summary>The last time this song has been played.</summary>
		public DateTime Timestamp { get; set; }
		/// <summary>Zero based offset this entry is stored in the history file.</summary>
		public long FilePosIndex { get; set; }

		public AudioResource AudioResource { get; private set; }

		public AudioLogEntry(uint id, AudioResource resource)
		{
			Id = id;
			PlayCount = 0;
			AudioResource = resource;
		}

		public AudioLogEntry(uint id, string resourceId, string resourceTitle, AudioType type)
			: this(id, new AudioResource(resourceId, resourceTitle, type)) { }

		public void SetName(string newName)
		{
			AudioResource = AudioResource.WithName(newName);
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
			strb.Append(AudioResource.AudioType.ToString());
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(AudioResource.ResourceId));
			strb.Append(",");
			strb.Append(Uri.EscapeDataString(AudioResource.ResourceTitle));

			return strb.ToString();
		}

		public static AudioLogEntry Parse(string line, long readIndex)
		{
			string[] strParts = line.TrimEnd(' ').Split(',');
			if (strParts.Length != 7)
				return null;
			// Array.ForEach(strParts) // check if spacetrims are needed
			int index = 0;
			uint id = uint.Parse(strParts[index++], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			uint userInvId = uint.Parse(strParts[index++], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			uint playCount = uint.Parse(strParts[index++], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			long dtStamp = long.Parse(strParts[index++], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			DateTime dateTime = DateTime.FromFileTime(dtStamp);
			AudioType audioType;
			if (!Enum.TryParse(strParts[index++], out audioType))
				return null;
			string resId = Uri.UnescapeDataString(strParts[index++]);
			string title = Uri.UnescapeDataString(strParts[index++]);
			return new AudioLogEntry(id, resId, title, audioType)
			{
				PlayCount = playCount,
				Timestamp = dateTime,
				UserInvokeId = userInvId,
				FilePosIndex = readIndex,
			};
		}

		private static string AsHex(uint num) => num.ToString("X8", CultureInfo.InvariantCulture);
		private static string AsHex(long num) => num.ToString("X16", CultureInfo.InvariantCulture);

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "[{0}] @ {1} by {2}: {3}, ({4})", Id, Timestamp, UserInvokeId, AudioResource.ResourceTitle, AudioResource);
		}
	}

}