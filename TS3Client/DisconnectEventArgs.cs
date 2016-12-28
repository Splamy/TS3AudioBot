using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3Client
{
	public class DisconnectEventArgs : EventArgs
	{
		public MoveReason ExitReason { get; }

		public DisconnectEventArgs(MoveReason exitReason)
		{
			ExitReason = exitReason;
		}
	}
}
