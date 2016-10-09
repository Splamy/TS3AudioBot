using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3Client.Full
{
	public class VersionSign
	{
		public string Sign { get; }
		public string Name { get; }

		public VersionSign(string name, string sign) { Name = name; Sign = sign; }

		public static readonly VersionSign VER_3_0_19_03
			= new VersionSign("3.0.19.3 [Build: 1466672534]", "a1OYzvM18mrmfUQBUgxYBxYz2DUU6y5k3/mEL6FurzU0y97Bd1FL7+PRpcHyPkg4R+kKAFZ1nhyzbgkGphDWDg==");
	}
}
