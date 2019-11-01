// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Text;

namespace TS3AudioBot.CommandSystem.Text
{
	public readonly struct Color
	{
		public byte R { get; }
		public byte G { get; }
		public byte B { get; }
		public ColorFlags Flags { get; }

		public static readonly Color Black = new Color(0, 0, 0);
		public static readonly Color DarkGray = new Color(64, 64, 64);
		public static readonly Color Gray = new Color(128, 128, 128);
		public static readonly Color LightGray = new Color(192, 192, 192);
		public static readonly Color Red = new Color(255, 0, 0);
		public static readonly Color Green = new Color(0, 255, 0);
		public static readonly Color Blue = new Color(0, 0, 255);
		public static readonly Color Yellow = new Color(255, 255, 0);
		public static readonly Color Cyan = new Color(0, 255, 255);
		public static readonly Color Pink = new Color(255, 0, 255);
		public static readonly Color Orange = new Color(255, 128, 0);
		public static readonly Color White = new Color(255, 255, 255);
		public static readonly Color Transparent = new Color(0, 0, 0, ColorFlags.Transparent);

		private static readonly Dictionary<Color, string> ColorOptimizer = new Dictionary<Color, string>();

		static Color()
		{
			var colors = new[] {
				( new Color(0, 255, 255), "aqua" ),
				( new Color(240, 255, 255), "azure" ),
				( new Color(245, 245, 220), "beige" ),
				( new Color(255, 228, 196), "bisque" ),
				( new Color(0, 0, 0), "black" ),
				( new Color(0, 0, 255), "blue" ),
				( new Color(165, 42, 42), "brown" ),
				( new Color(255, 127, 80), "coral" ),
				( new Color(0, 255, 255), "cyan" ),
				( new Color(255, 215, 0), "gold" ),
				( new Color(128, 128, 128), "gray" ),
				( new Color(0, 128, 0), "green" ),
				( new Color(75, 0, 130), "indigo" ),
				( new Color(255, 255, 240), "ivory" ),
				( new Color(240, 230, 140), "khaki" ),
				( new Color(0, 255, 0), "lime" ),
				( new Color(250, 240, 230), "linen" ),
				( new Color(128, 0, 0), "maroon" ),
				( new Color(0, 0, 128), "navy" ),
				( new Color(128, 128, 0), "olive" ),
				( new Color(255, 165, 0), "orange" ),
				( new Color(218, 112, 214), "orchid" ),
				( new Color(205, 133, 63), "peru" ),
				( new Color(255, 192, 203), "pink" ),
				( new Color(221, 160, 221), "plum" ),
				( new Color(128, 0, 128), "purple" ),
				( new Color(255, 0, 0), "red" ),
				( new Color(250, 128, 114), "salmon" ),
				( new Color(160, 82, 45), "sienna" ),
				( new Color(192, 192, 192), "silver" ),
				( new Color(255, 250, 250), "snow" ),
				( new Color(210, 180, 140), "tan" ),
				( new Color(0, 128, 128), "teal" ),
				( new Color(255, 99, 71), "tomato" ),
				( new Color(238, 130, 238), "violet" ),
				( new Color(245, 222, 179), "wheat" ),
				( new Color(255, 255, 255), "white" ),
				( new Color(255, 255, 0), "yellow" ),
			};

			foreach (var values in colors)
			{
				var col = values.Item1;
				if (values.Item2.Length < 4
					|| (values.Item2.Length < 7 && (!IsDouble(col.R) || !IsDouble(col.G) || !IsDouble(col.B))))
				{
					if (!ColorOptimizer.TryGetValue(col, out var name) || name.Length > values.Item2.Length)
					{
						ColorOptimizer[col] = values.Item2;
					}
				}
			}
		}

		public Color(byte r, byte g, byte b) : this(r, g, b, ColorFlags.Solid) { }
		public Color(byte r, byte g, byte b, ColorFlags flags)
		{
			R = r;
			G = g;
			B = b;
			Flags = flags;
		}

		private static bool IsDouble(byte num) => (num & 0x0F) == (num >> 4);

		public void GetL(StringBuilder strb)
		{
			if (Flags.HasFlag(ColorFlags.Transparent))
				strb.Append("[COLOR=transparent]");
			else if (ColorOptimizer.TryGetValue(this, out var optValue))
				strb.AppendFormat("[COLOR={0}]", optValue);
			else if (IsDouble(R) && IsDouble(G) && IsDouble(B))
				strb.AppendFormat("[COLOR=#{0:X}{1:X}{2:X}]", R & 0x0F, G & 0x0F, B & 0x0F);
			else
				strb.AppendFormat("[COLOR=#{0:X2}{1:X2}{2:X2}]", R, G, B);
		}

		public override bool Equals(object obj)
		{
			if (obj is Color col)
			{
				return this == col;
			}
			return false;
		}

		public override int GetHashCode() => (int)Flags << 24 | R << 16 | G << 8 | B;

		public override string ToString()
		{
			var strb = new StringBuilder();
			GetL(strb);
			return strb.ToString();
		}

		public static bool operator ==(Color a, Color b) => a.R == b.R && a.G == b.G && a.B == b.B && a.Flags == b.Flags;

		public static bool operator !=(Color a, Color b) => a.R != b.R || a.G != b.G || a.B != b.B || a.Flags != b.Flags;
	}

	[Flags]
	public enum ColorFlags : byte
	{
		Solid = 0,
		Transparent = 1 << 0,
	}
}
