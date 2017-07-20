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

namespace TS3AudioBot.Helper
{
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Drawing.Text;

	static class ImageUtil
	{
		private static StringFormat avatarTextFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };
		private static Pen avatarTextOutline = new Pen(Color.Black, 4) { LineJoin = LineJoin.Round };

		public static void BuildStringImage(string str, Image img, RectangleF rect)
		{
			using (var graphics = Graphics.FromImage(img))
			{
				if (Util.IsLinux)
				{
					BuildStringImageLinux(str, graphics, rect);
				}
				else
				{
					using (var gp = new GraphicsPath())
					{
						gp.AddString(str, FontFamily.GenericSansSerif, 0, 15, rect, avatarTextFormat);

						graphics.InterpolationMode = InterpolationMode.High;
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
						graphics.CompositingQuality = CompositingQuality.HighQuality;

						graphics.DrawPath(avatarTextOutline, gp);
						graphics.FillPath(Brushes.White, gp);
					}
				}
			}
		}

		private static void BuildStringImageLinux(string str, Graphics target, RectangleF rect)
		{
			const int maxMonoBugWidth = 150;

			using (var gp = new GraphicsPath())
			using (var builder = new Bitmap(maxMonoBugWidth, 100))
			using (var bg = Graphics.FromImage(builder))
			{
				gp.AddString("X", FontFamily.GenericMonospace, 0, 15, rect, avatarTextFormat);
				var bounds = gp.GetBounds();
				var charW = bounds.Width;
				var charH = bounds.Height * 2;
				if (charW < 0.1e-6)
					return;

				var buildRect = new RectangleF(0, 0, maxMonoBugWidth, charH);

				var sep = new List<string>();

				bg.InterpolationMode = InterpolationMode.High;
				bg.SmoothingMode = SmoothingMode.HighQuality;
				bg.TextRenderingHint = TextRenderingHint.AntiAlias;
				bg.CompositingQuality = CompositingQuality.HighQuality;
				target.CompositingQuality = CompositingQuality.HighQuality;

				int lastBreak = 0;
				int lastBreakOption = 0;
				for (int i = 0; i < str.Length; i++)
				{
					if (!char.IsLetterOrDigit(str[i]))
					{
						lastBreakOption = i;
					}

					if ((i - lastBreak) * charW >= rect.Width && lastBreak != lastBreakOption)
					{
						sep.Add(str.Substring(lastBreak, lastBreakOption - lastBreak));
						lastBreak = lastBreakOption;
					}
				}
				sep.Add(str.Substring(lastBreak));

				var step = (int)(maxMonoBugWidth / charW) - 1;
				for (int i = 0; i < sep.Count; i++)
				{
					var line = sep[i];
					float flLeft = 0;
					for (int j = 0; j * step < line.Length; j++)
					{
						var part = line.Substring(j * step, Math.Min(step, line.Length - j * step));
						gp.Reset();
						gp.AddString(part, FontFamily.GenericMonospace, 0, 15, buildRect, avatarTextFormat);

						bg.Clear(Color.Transparent);
						bg.DrawPath(avatarTextOutline, gp);
						bg.FillPath(Brushes.White, gp);

						target.DrawImageUnscaled(builder, (int)(rect.X + j * (maxMonoBugWidth - 5)), (int)(rect.Y + i * charH));
						flLeft += gp.GetBounds().Width;
					}
				}
			}
		}
	}
}
