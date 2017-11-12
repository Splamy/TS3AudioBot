// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Full
{
	using System;

	/// <summary>
	/// Describes a triple of version, plattform and a crytographical signature (usually distributed by "TeamSpeak Systems").
	/// Each triple has to match and is not interchangeable with other triple parts.
	/// </summary>
	public class VersionSign
	{
		private static readonly string[] Plattforms = { null, "Windows", "Linux", "OS X", "Android", "iOS" };

		public string Sign { get; }
		public string Name { get; }
		public ClientPlattform Plattform { get; }
		public string PlattformName { get; }

		public VersionSign(string name, ClientPlattform plattform, string sign)
		{
			if (plattform == ClientPlattform.Other)
				throw new ArgumentException(nameof(plattform));
			Name = name;
			Sign = sign;
			Plattform = plattform;
			PlattformName = Plattforms[(int)plattform];
		}

		public VersionSign(string name, string plattform, string sign)
		{
			Name = name;
			Sign = sign;
			Plattform = ClientPlattform.Other;
			PlattformName = plattform;
		}

		// Many ids implemented from here: https://r4p3.net/threads/client-builds.499/

		public static readonly VersionSign VER_WIN_3_0_11
			= new VersionSign("3.0.11 [Build: 1375083581]", ClientPlattform.Windows, "54wPDkfv0kT56UE0lv/LFkFJObH+Q4Irmo4Brfz1EcvjVhj8hJ+RCHcVTZsdKU2XvVvh+VLJpURulEHsAOsyBw==");
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
		public static readonly VersionSign VER_WIN_3_1_6
			= new VersionSign("3.1.6 [Build: 1502873983]", ClientPlattform.Windows, "73fB82Jt1lmIRHKBFaE8h1JKPGFbnt6/yrXOHwTS93Oo7Adx1usY5TzNg+8BKy9nmmA2FEBnRmz5cRfXDghnBA==");
		public static readonly VersionSign VER_LIN_3_1_6
			= new VersionSign("3.1.6 [Build: 1502873983]", ClientPlattform.Linux, "o+l92HKfiUF+THx2rBsuNjj/S1QpxG1fd5o3Q7qtWxkviR3LI3JeWyc26eTmoQoMTgI3jjHV7dCwHsK1BVu6Aw==");
		public static readonly VersionSign VER_WIN_3_1_7_ALPHA
			= new VersionSign("3.1.7 [Build: 1507896705]", ClientPlattform.Windows, "Iks42KIMcmFv5vzPLhziqahcPD2AHygkepr8xHNCbqx+li5n7Htbq5LE9e1YYhRhLoS4e2HqOpKkt+/+LC8EDA==");
		public static readonly VersionSign VER_OSX_3_1_7_ALPHA
			= new VersionSign("3.1.7 [Build: 1507896705]", ClientPlattform.Osx, "iM0IyUpaH9ak0gTtrHlRT0VGZa4rC51iZwSFwifK6iFqciSba/WkIQDWk9GUJN0OCCfatoc/fmlq8TPBnE5XCA==");
		public static readonly VersionSign VER_WIN_3_UNKNOWN
			= new VersionSign("3.?.? [Build: 5680278000]", ClientPlattform.Windows, "DX5NIYLvfJEUjuIbCidnoeozxIDRRkpq3I9vVMBmE9L2qnekOoBzSenkzsg2lC9CMv8K5hkEzhr2TYUYSwUXCg==");
		public static readonly VersionSign VER_AND_3_UNKNOWN
			= new VersionSign("3.?.? [Build: 5680278000]", ClientPlattform.Android, "AWb948BY32Z7bpIyoAlQguSmxOGcmjESPceQe1DpW5IZ4+AW1KfTk2VUIYNfUPsxReDJMCtlhVKslzhR2lf0AA==");
		public static readonly VersionSign VER_IOS_3_UNKNOWN
			= new VersionSign("3.?.? [Build: 5680278000]", ClientPlattform.Ios, "XrAf+Buq6Eb0ehEW/niFp06YX+nGGOS0Ke4MoUBzn+cX9q6G5C0A/d5XtgcNMe8r9jJgV/adIYVpsGS3pVlSAA==");
	}

	public enum ClientPlattform
	{
		Other = 0,
		Windows,
		Linux,
		Osx,
		Android,
		Ios,
	}
}
