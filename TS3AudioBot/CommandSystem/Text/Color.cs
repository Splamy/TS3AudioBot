// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.Text
{
	using System.Text;

	public readonly struct Color
	{
		public byte R { get; }
		public byte G { get; }
		public byte B { get; }

		public static readonly Color Black = new Color(0, 0, 0);
		public static readonly Color DarkGray = new Color(64, 64, 64);
		public static readonly Color Gray = new Color(128, 128, 128);
		public static readonly Color LigtGray = new Color(192, 192, 192);
		public static readonly Color Red = new Color(255, 0, 0);
		public static readonly Color Green = new Color(0, 255, 0);
		public static readonly Color Blue = new Color(0, 0, 255);
		public static readonly Color Yellow = new Color(255, 255, 0);
		public static readonly Color Cyan = new Color(0, 255, 255);
		public static readonly Color Pink = new Color(255, 0, 255);
		public static readonly Color Orange = new Color(255, 128, 0);
		public static readonly Color White = new Color(255, 255, 255);

		public Color(byte r, byte g, byte b)
		{
			R = r;
			G = g;
			B = b;
		}

		public void GetL(StringBuilder strb) =>
			strb.AppendFormat("[COLOR=#{0:X2}{1:X2}{2:X2}]", R, G, B);
	}
}
