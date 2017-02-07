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

namespace TS3Client.Full
{
	public class VersionSign
	{
		private static readonly string[] plattforms = new[] { "Windows", "Linux", "OS X", "Android" };

		public string Sign { get; }
		public string Name { get; }
		ClientPlattform Plattform { get; }
		public string PlattformName { get; }

		public VersionSign(string name, ClientPlattform plattform, string sign)
		{
			Name = name;
			Sign = sign;
			Plattform = plattform;
			PlattformName = plattforms[(int)plattform];
		}

		// Many ids implemented from here: https://r4p3.net/threads/client-builds.499/

		public static readonly VersionSign VER_WIN_3_0_19_3
			= new VersionSign("3.0.19.3 [Build: 1466672534]", ClientPlattform.Windows, "a1OYzvM18mrmfUQBUgxYBxYz2DUU6y5k3/mEL6FurzU0y97Bd1FL7+PRpcHyPkg4R+kKAFZ1nhyzbgkGphDWDg==");
		public static readonly VersionSign VER_WIN_3_0_19_4
			= new VersionSign("3.0.19.4 [Build: 1468491418]", ClientPlattform.Windows, "ldWL49uDKC3N9uxdgWRMTOzUuiG1nBqUiOa+Nal5HvdxJiN4fsTnmmPo5tvglN7WqoVoFfuuKuYq1LzodtEtCg==");
		public static readonly VersionSign VER_LIN_3_0_19_4
			= new VersionSign("3.0.19.4 [Build: 1468491418]", ClientPlattform.Linux, "jvhhk75EV3nCGeewx4Y5zZmiZSN07q5ByKZ9Wlmg85aAbnw7c1jKq5/Iq0zY6dfGwCEwuKod0I5lQcVLf2NTCg==");
		public static readonly VersionSign VER_OSX_3_0_19_4
			= new VersionSign("3.0.19.4 [Build: 1468491418]", ClientPlattform.Osx, "Pvcizdk3HRQMzTLt7goUYBmmS5nbAS1g2E6HIypLU+9eXTqGTBLim0UUtKc0s867TFHbK91GroDrTtv0aMUGAw==");
		public static readonly VersionSign VER_WIN_3_0_20
			= new VersionSign("3.0.20 [Build: 1465542546]", ClientPlattform.Windows, "vDK31sOwOvDpTXgqAJzmR1NzeUeSDG9dLMgIz5LCX+KpDSVD/qU60mzScz9tuc9AsLyrL8DxHpDDO3eQD+hYCA==");
		public static readonly VersionSign VER_AND_3_0_23
			= new VersionSign("3.0.23 [Build: 1463662487]", ClientPlattform.Android, "RN+cwFI+jSHJEhggucIuUyEteWNVFy4iw0QDp3qn2UzfopypFVE9BPZqJjBUGeoCN7Q/SfYL4RNIRzJEQaZUCA==");
		public static readonly VersionSign VER_WIN_3_1
			= new VersionSign("3.1 [Build: 1471417187]", ClientPlattform.Windows, "Vr9F7kbVorcrkV5b/Iw+feH9qmDGvfsW8tpa737zhc1fDpK5uaEo6M5l2DzgaGqqOr3GKl5A7PF9Sj6eTM26Aw==");
	}

	public enum ClientPlattform
	{
		Windows = 0,
		Linux,
		Osx,
		Android,
	}
}
