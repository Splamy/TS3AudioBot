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

		public static readonly Color Black = (0, 0, 0);
		public static readonly Color DarkGray = (64, 64, 64);
		public static readonly Color Gray = (128, 128, 128);
		public static readonly Color LightGray = (192, 192, 192);
		public static readonly Color Red = (255, 0, 0);
		public static readonly Color Green = (0, 255, 0);
		public static readonly Color Blue = (0, 0, 255);
		public static readonly Color Yellow = (255, 255, 0);
		public static readonly Color Cyan = (0, 255, 255);
		public static readonly Color Pink = (255, 0, 255);
		public static readonly Color Orange = (255, 128, 0);
		public static readonly Color White = (255, 255, 255);
		public static readonly Color Transparent = new(0, 0, 0, ColorFlags.Transparent);

		private static readonly Dictionary<Color, string> ColorOptimizer = new();

		static Color()
		{
			var colors = new (Color, string)[] {
				( (0, 255, 255), "aqua" ),
				( (240, 255, 255), "azure" ),
				( (245, 245, 220), "beige" ),
				( (255, 228, 196), "bisque" ),
				( (0, 0, 0), "black" ),
				( (0, 0, 255), "blue" ),
				( (165, 42, 42), "brown" ),
				( (255, 127, 80), "coral" ),
				( (0, 255, 255), "cyan" ),
				( (255, 215, 0), "gold" ),
				( (128, 128, 128), "gray" ),
				( (0, 128, 0), "green" ),
				( (75, 0, 130), "indigo" ),
				( (255, 255, 240), "ivory" ),
				( (240, 230, 140), "khaki" ),
				( (0, 255, 0), "lime" ),
				( (250, 240, 230), "linen" ),
				( (128, 0, 0), "maroon" ),
				( (0, 0, 128), "navy" ),
				( (128, 128, 0), "olive" ),
				( (255, 165, 0), "orange" ),
				( (218, 112, 214), "orchid" ),
				( (205, 133, 63), "peru" ),
				( (255, 192, 203), "pink" ),
				( (221, 160, 221), "plum" ),
				( (128, 0, 128), "purple" ),
				( (255, 0, 0), "red" ),
				( (250, 128, 114), "salmon" ),
				( (160, 82, 45), "sienna" ),
				( (192, 192, 192), "silver" ),
				( (255, 250, 250), "snow" ),
				( (210, 180, 140), "tan" ),
				( (0, 128, 128), "teal" ),
				( (255, 99, 71), "tomato" ),
				( (238, 130, 238), "violet" ),
				( (245, 222, 179), "wheat" ),
				( (255, 255, 255), "white" ),
				( (255, 255, 0), "yellow" ),
			};

			foreach (var (col, htmlname) in colors)
			{
				if (htmlname.Length < 4 || (htmlname.Length < 7 && (!IsDouble(col.R) || !IsDouble(col.G) || !IsDouble(col.B))))
				{
					if (!ColorOptimizer.TryGetValue(col, out var name) || name.Length > htmlname.Length)
					{
						ColorOptimizer[col] = htmlname;
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
		public static implicit operator Color((byte r, byte g, byte b) rgb) => new(rgb.r, rgb.g, rgb.b);
		public void Deconstruct(out byte r, out byte g, out byte b) => (r, g, b) = (R, G, B);

		private static bool IsDouble(byte num) => (num & 0x0F) == (num >> 4);

		public readonly void GetL(StringBuilder strb)
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

		public override readonly bool Equals(object? obj)
		{
			if (obj is Color col)
			{
				return this == col;
			}
			return false;
		}

		public override readonly int GetHashCode() => (int)Flags << 24 | R << 16 | G << 8 | B;

		public override readonly string ToString()
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
