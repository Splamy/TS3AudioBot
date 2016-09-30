using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3Client.Full
{
	class IncomingPacket : BasePacket
	{
		public IncomingPacket(byte[] raw)
		{
			Raw = raw;
			Header = new byte[3];
		}
	}
}
