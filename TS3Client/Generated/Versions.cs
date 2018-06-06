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
	/// Describes a triple of version, platform and a cryptographical signature (usually distributed by "TeamSpeak Systems").
	/// Each triple has to match and is not interchangeable with other triple parts.
	/// </summary>
	public sealed class VersionSign
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

		public bool CheckValid() => Ts3Crypt.EdCheck(this);

		// ReSharper disable InconsistentNaming, UnusedMember.Global
		public static VersionSign VER_IOS_3_X_X { get; } = new VersionSign("3.?.? [Build: 5680278000]", ClientPlattform.Ios, "XrAf+Buq6Eb0ehEW/niFp06YX+nGGOS0Ke4MoUBzn+cX9q6G5C0A/d5XtgcNMe8r9jJgV/adIYVpsGS3pVlSAA==");
		public static VersionSign VER_AND_3_X_X { get; } = new VersionSign("3.?.? [Build: 5680278000]", ClientPlattform.Android, "AWb948BY32Z7bpIyoAlQguSmxOGcmjESPceQe1DpW5IZ4+AW1KfTk2VUIYNfUPsxReDJMCtlhVKslzhR2lf0AA==");
		public static VersionSign VER_WIN_3_X_X { get; } = new VersionSign("3.?.? [Build: 5680278000]", ClientPlattform.Windows, "DX5NIYLvfJEUjuIbCidnoeozxIDRRkpq3I9vVMBmE9L2qnekOoBzSenkzsg2lC9CMv8K5hkEzhr2TYUYSwUXCg==");
		public static VersionSign VER_WIN_3_1_9 { get; } = new VersionSign("3.1.9 [Build: 1525442084]", ClientPlattform.Windows, "2SLjPTFXM9hQyNkeEGYIzs0fkBffyhsh5z+ZuaCcZdDfM8vgRM5lrAU6KNspFjLddcvw8cXw6gxRY73ZHsRVBg==");
		public static VersionSign VER_OSX_3_1_9 { get; } = new VersionSign("3.1.9 [Build: 1525442084]", ClientPlattform.Osx, "WVaMmYPig4eG2JUM8cMMW2MA7+IoRoPUSr74CPe7oS8TLHGjYxPr1FP88op6YsFFQrPJysWmIsnGR7BiFXjHCQ==");
		public static VersionSign VER_WIN_3_1_8 { get; } = new VersionSign("3.1.8 [Build: 1516614607]", ClientPlattform.Windows, "gDEgQf/BiOQZdAheKccM1XWcMUj2OUQqt75oFuvF2c0MQMXyv88cZQdUuckKbcBRp7RpmLInto4PIgd7mPO7BQ==");
		public static VersionSign VER_LIN_3_1_8 { get; } = new VersionSign("3.1.8 [Build: 1516614607]", ClientPlattform.Linux, "LJ5q+KWT4KwBX7oR/9j9A12hBrq5ds5ony99f9kepNmqFskhT7gfB51bAJNgAMOzXVCeaItNmc10F2wUNktqCw==");
		public static VersionSign VER_LIN_3_1_7 { get; } = new VersionSign("3.1.7 [Build: 1513163251]", ClientPlattform.Linux, "/j5TZqPuOU8yMYPdGehvijYvU74KefRrKO5sgTUrkpeslNFiy4XfU7quKW0diLHQoPQn1t3KArdfzOAMk8dlAg==");
		public static VersionSign VER_OSX_3_1_7 { get; } = new VersionSign("3.1.7 [Build: 1512141423]", ClientPlattform.Osx, "PP+/cBUDtSyV0k7lm8aYvYWAs28KL+KmXa+f0pUpDqjQDKy8dnDzJp16F4YGJxJ+2ODGPkp5YQYwts3m8T7+CA==");
		public static VersionSign VER_WIN_3_1_7 { get; } = new VersionSign("3.1.7 [Build: 1513163251]", ClientPlattform.Windows, "tdNngCAZ1ImAf7BxJzO4RXv5nBRsUERsrSOnMKVUFNQg6BS4Bzag0RFgLVzs2DRj19AC8+q5cXgH+5Ms50mTCA==");
		public static VersionSign VER_WIN_3_1_6 { get; } = new VersionSign("3.1.6 [Build: 1502873983]", ClientPlattform.Windows, "73fB82Jt1lmIRHKBFaE8h1JKPGFbnt6/yrXOHwTS93Oo7Adx1usY5TzNg+8BKy9nmmA2FEBnRmz5cRfXDghnBA==");
		public static VersionSign VER_LIN_3_1_6 { get; } = new VersionSign("3.1.6 [Build: 1502873983]", ClientPlattform.Linux, "o+l92HKfiUF+THx2rBsuNjj/S1QpxG1fd5o3Q7qtWxkviR3LI3JeWyc26eTmoQoMTgI3jjHV7dCwHsK1BVu6Aw==");
		public static VersionSign VER_WIN_3_1_5 { get; } = new VersionSign("3.1.5 [Build: 1500537355]", ClientPlattform.Windows, "O9WqHB9oX0qe9AXIYmJm0+mzl6VLxNvrGF0lGlovLaig5MXUIwd6T00NkCj62OkBbzM3eECs9FUuJk7N8V0dCg==");
		public static VersionSign VER_WIN_3_1_4_2 { get; } = new VersionSign("3.1.4.2 [Build: 1498644101]", ClientPlattform.Windows, "WtscrpvJG13kbF6aoVzsGwQuE/WwR1b8++ydDc8IpmiXLw+zFC6zFUvLinOeE0zZgh2Hs5Amp3DZoPJSynOWBg==");
		public static VersionSign VER_WIN_3_1_4 { get; } = new VersionSign("3.1.4 [Build: 1491993378]", ClientPlattform.Windows, "rwdyEwnJCzbVfNCqbxMrRyhL5BSYqYSzKQkeZ6m5KImc1F8VB8wEkwwwyxoG7SimC/sxIyy4h27CjBFP6rcgBQ==");
		public static VersionSign VER_WIN_3_1_3 { get; } = new VersionSign("3.1.3 [Build: 1490279472]", ClientPlattform.Windows, "7RPY2bzJmMdgVX24VuKD3lTnYYb6yHWqfn2x21tFOjXL9q+2t7tU9Vy8Bh5/IpeiqklUHTWc23mWpYOCoW9eCA==");
		public static VersionSign VER_WIN_3_1_2 { get; } = new VersionSign("3.1.2 [Build: 1489662774]", ClientPlattform.Windows, "5Aaj21gGFtrjW9424ezfLa1SMQBpZvgQgcJLZmrLoNMe4XebBPV2s8rxEDAIodfFpruLxLFbFpH63A/BGnJyDw==");
		public static VersionSign VER_WIN_3_1_1_1 { get; } = new VersionSign("3.1.1.1 [Build: 1487668590]", ClientPlattform.Windows, "CchjMitGiVGfRlGph0D1mDjOCJCnkVxR/WuYvNHdPyeQUCncRWML8jYxYfnhRF6CzViwYRnsmZkN+W5oenB2CQ==");
		public static VersionSign VER_WIN_3_1_1 { get; } = new VersionSign("3.1.1 [Build: 1486712038]", ClientPlattform.Windows, "sryyx++NhRWKDAo+Tnwv9N+IrOaQBP0XjjDszY0BBv0YIMr4jmdHtgrwzWkUqhU7kfql7qBWIhlb/r0l1ZHeBw==");
		public static VersionSign VER_WIN_3_1_0_1 { get; } = new VersionSign("3.1.0.1 [Build: 1484223040]", ClientPlattform.Windows, "oaaorJ4co/sS2m5JT5oRiu9AieW6kfFY+RENqPfp26iP4pbWbf9GcZj+JhDA+/JyLpfueCcSulZSRRbash2JCw==");
		public static VersionSign VER_WIN_3_1 { get; } = new VersionSign("3.1 [Build: 1481795005]", ClientPlattform.Windows, "3TpZZM0V+PKHELFnsfRPoKjEFfvfHUL/6mUP5LHbI3nvmdOjRqEEKi4ndXZG6OpWOKQ3VeadHDH0KBfD8EI2Cg==");
		public static VersionSign VER_AND_3_0_23 { get; } = new VersionSign("3.0.23 [Build: 1463662487]", ClientPlattform.Android, "RN+cwFI+jSHJEhggucIuUyEteWNVFy4iw0QDp3qn2UzfopypFVE9BPZqJjBUGeoCN7Q/SfYL4RNIRzJEQaZUCA==");
		public static VersionSign VER_WIN_3_0_20 { get; } = new VersionSign("3.0.20 [Build: 1465542546]", ClientPlattform.Windows, "vDK31sOwOvDpTXgqAJzmR1NzeUeSDG9dLMgIz5LCX+KpDSVD/qU60mzScz9tuc9AsLyrL8DxHpDDO3eQD+hYCA==");
		public static VersionSign VER_WIN_3_0_19_4 { get; } = new VersionSign("3.0.19.4 [Build: 1468491418]", ClientPlattform.Windows, "ldWL49uDKC3N9uxdgWRMTOzUuiG1nBqUiOa+Nal5HvdxJiN4fsTnmmPo5tvglN7WqoVoFfuuKuYq1LzodtEtCg==");
		public static VersionSign VER_OSX_3_0_19_4 { get; } = new VersionSign("3.0.19.4 [Build: 1468491418]", ClientPlattform.Osx, "Pvcizdk3HRQMzTLt7goUYBmmS5nbAS1g2E6HIypLU+9eXTqGTBLim0UUtKc0s867TFHbK91GroDrTtv0aMUGAw==");
		public static VersionSign VER_LIN_3_0_19_4 { get; } = new VersionSign("3.0.19.4 [Build: 1468491418]", ClientPlattform.Linux, "jvhhk75EV3nCGeewx4Y5zZmiZSN07q5ByKZ9Wlmg85aAbnw7c1jKq5/Iq0zY6dfGwCEwuKod0I5lQcVLf2NTCg==");
		public static VersionSign VER_WIN_3_0_19_3 { get; } = new VersionSign("3.0.19.3 [Build: 1466672534]", ClientPlattform.Windows, "a1OYzvM18mrmfUQBUgxYBxYz2DUU6y5k3/mEL6FurzU0y97Bd1FL7+PRpcHyPkg4R+kKAFZ1nhyzbgkGphDWDg==");
		public static VersionSign VER_WIN_3_0_19_2 { get; } = new VersionSign("3.0.19.2 [Build: 1466597785]", ClientPlattform.Windows, "sDOzu7rCGb7kBID2WbBk35DjPijKkXzujnsAtLhXxhkQ+am0JlDOpuU1ISHhq9gCl/Qo0dzc723o0AIPI+yoCQ==");
		public static VersionSign VER_WIN_3_0_19_1 { get; } = new VersionSign("3.0.19.1 [Build: 1461588969]", ClientPlattform.Windows, "KYo52MA89dowkYpFU1KixgHngjbJ6F2Yi++5tbaqBlBpz9YikX2gI3sqmU1kP1ghsKCLKM7o0patDH1hv9bmAg==");
		public static VersionSign VER_WIN_3_0_19 { get; } = new VersionSign("3.0.19 [Build: 1459504131]", ClientPlattform.Windows, "JoHyZHF4k/a3+QH1zPNSEzc40487fzbpssyRZtoWB5kbQorAJgwlpcScA08J4vjGoUbdaTZsT0vCw56wo/Q9Ag==");
		public static VersionSign VER_WIN_3_0_18_2 { get; } = new VersionSign("3.0.18.2 [Build: 1445512488]", ClientPlattform.Windows, "F0hY25Dtja0wcU6dzC39rNuYbhnDAbIwPHC3VO9Oicf13kUY2I2g6scPZ3p195Cw9gUYdBIRYm8ucHEhtSeWCw==");
		public static VersionSign VER_WIN_3_0_18_1 { get; } = new VersionSign("3.0.18.1 [Build: 1444491275]", ClientPlattform.Windows, "xqfa3CUd2GFiTqjJWYzcu9ZbxVVLng8qIMKlVxMqWdiM8JrTRiXBAaTBDd8Xc+flVe+rGSIOZTkXRsz1rqjiAA==");
		public static VersionSign VER_WIN_3_0_18 { get; } = new VersionSign("3.0.18 [Build: 1442998335]", ClientPlattform.Windows, "vUgm8mJoeVLBG6qB2HcYF7YNG4D+H/4edILaZbHze2Unua6mrBvNmbtRkRtmRyDZSd7sVQHMApinRDgGT1mUBw==");
		public static VersionSign VER_WIN_3_0_17 { get; } = new VersionSign("3.0.17 [Build: 1438673913]", ClientPlattform.Windows, "znDjHvCgmQF/jQKTK49X8tnXqF7AGXfS2XYcogww4XxNTBxp2tf1aFc/jgboKco9EuVa0ku2cf/xg9wW3Cm7AQ==");
		public static VersionSign VER_WIN_3_0_16 { get; } = new VersionSign("3.0.16 [Build: 1407159763]", ClientPlattform.Windows, "Y1DuQGXo/8/rYznEGyeQHgpvZMuiCH4FYm4QVyAgLYyMpNpc/LM7XetVWhDQxGsNejkN/2olI7GVJkt4X+ooDg==");
		public static VersionSign VER_LIN_3_0_16 { get; } = new VersionSign("3.0.16 [Build: 1407159763]", ClientPlattform.Linux, "8776GitHAgkFPfOLxEh5x+Luuh4NrYPEJUdsUzNKndcAuWMYjwQTZkmeZOeG/swdn/p2Cg2pRfZfsIFSOAUWCQ==");
		public static VersionSign VER_WIN_3_0_15_1 { get; } = new VersionSign("3.0.15.1 [Build: 1405341092]", ClientPlattform.Windows, "b+hr0KQWOVW2WEn49BmNb08R9zimsJcThm2gEeF7EAgRUeUDYzeplh5HrHmda0ftbbnrzWV33U/GOo2LAs/rAg==");
		public static VersionSign VER_WIN_3_0_15 { get; } = new VersionSign("3.0.15 [Build: 1403250090]", ClientPlattform.Windows, "FKKAHPwV1swKwH6mqHqdcGuYm8o5mZw4WreBxJrQjOprC3NXXcJviPe0p7EZPI810HOWMfmQRUgFpggoRL8kAQ==");
		public static VersionSign VER_WIN_3_0_14 { get; } = new VersionSign("3.0.14 [Build: 1394624943]", ClientPlattform.Windows, "F0WIO9sBVzG893AtX2Jfd98cH6yZPAnfMBNvBlQbAIfvfyiq+cbjZ31AUngEjq7UPIYdnYSsdRX9hczwdBrKAQ==");
		public static VersionSign VER_WIN_3_0_13_1 { get; } = new VersionSign("3.0.13.1 [Build: 1382530211]", ClientPlattform.Windows, "bCIfLPUgTM6C0kNkesvhcxaDPvV9h6qLbYVy9cQVSP5lzaYebZaeDzAOOHsdjKcRTa6LU1oHEdz9D/d+2gxJCw==");
		public static VersionSign VER_WIN_3_0_13 { get; } = new VersionSign("3.0.13 [Build: 1380283653]", ClientPlattform.Windows, "7dA+6EbVyMevol4gE3/Cu1WonRjqu1C6pTWF+txApbaiTgKtZ/ky+NVxluPkSDnCxXN1pOR4uGdF6B7LUqQgDQ==");
		public static VersionSign VER_WIN_3_0_12 { get; } = new VersionSign("3.0.12 [Build: 1378715177]", ClientPlattform.Windows, "x6wFA5xqjenf6kbAh36IC4CkrbT8/uSBpgjM9juSt9oxGCXLqHOC2oaYlB1zZSJZjT4sOrnp0M+uOdVjYCzLCg==");
		public static VersionSign VER_WIN_3_0_11_1 { get; } = new VersionSign("3.0.11.1 [Build: 1375773286]", ClientPlattform.Windows, "Qfvcn4uQmKETDsD4LbtdbZR8rDetJ26Z/bVbu5SZJjMjGlYEMSbJnR4PtOBshdMSEwEsAJf1G+5tjx+onm2fDA==");
		public static VersionSign VER_WIN_3_0_11 { get; } = new VersionSign("3.0.11 [Build: 1375083581]", ClientPlattform.Windows, "54wPDkfv0kT56UE0lv/LFkFJObH+Q4Irmo4Brfz1EcvjVhj8hJ+RCHcVTZsdKU2XvVvh+VLJpURulEHsAOsyBw==");
		// ReSharper restore InconsistentNaming, UnusedMember.Global
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
