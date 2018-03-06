// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper
{
	using Environment;
	using System;
	using System.Collections.Generic;
	using System.Drawing;
	using System.Drawing.Drawing2D;
	using System.Drawing.Text;

	internal static class ImageUtil
	{
		public const int ResizeMaxWidthDefault = 320;

		private static readonly StringFormat AvatarTextFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };

		public static Image BuildStringImage(string str, Image img, int resizeMaxWidth = ResizeMaxWidthDefault)
		{
			img = AutoResize(img, resizeMaxWidth);

			var imgRect = new RectangleF(0, 0, img.Width, img.Height);

			using (var graphics = Graphics.FromImage(img))
			{
				if (SystemData.IsLinux)
				{
					BuildStringImageLinux(str, graphics, imgRect);
				}
				else
				{
					using (var gp = new GraphicsPath())
					{
						gp.AddString(str, FontFamily.GenericSansSerif, 0, 15, imgRect, AvatarTextFormat);

						graphics.InterpolationMode = InterpolationMode.High;
						graphics.SmoothingMode = SmoothingMode.HighQuality;
						graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
						graphics.CompositingQuality = CompositingQuality.HighQuality;

						using (Pen avatarTextOutline = new Pen(Color.Black, 4) { LineJoin = LineJoin.Round })
						{
							graphics.DrawPath(avatarTextOutline, gp);
						}
						graphics.FillPath(Brushes.White, gp);
					}
				}
			}

			return img;
		}

		private static Image AutoResize(Image img, int resizeMaxWidth)
		{
			if (img.Width <= resizeMaxWidth)
				return img;

			using (img)
			{
				float ratio = img.Width / (float)img.Height;
				var destImage = new Bitmap(resizeMaxWidth, (int)(resizeMaxWidth / ratio));

				using (var graphics = Graphics.FromImage(destImage))
				{
					graphics.CompositingMode = CompositingMode.SourceCopy;
					graphics.CompositingQuality = CompositingQuality.HighQuality;
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.SmoothingMode = SmoothingMode.HighQuality;
					graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
					graphics.DrawImage(img, new Rectangle(0, 0, destImage.Width, destImage.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
				}

				return destImage;
			}
		}

		private static void BuildStringImageLinux(string str, Graphics target, RectangleF rect)
		{
			const int maxMonoBugWidth = 150;

			using (var gp = new GraphicsPath())
			using (var builder = new Bitmap(maxMonoBugWidth, 100))
			using (var bg = Graphics.FromImage(builder))
			{
				gp.AddString("X", FontFamily.GenericMonospace, 0, 15, rect, AvatarTextFormat);
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
					for (int j = 0; j * step < line.Length; j++)
					{
						var part = line.Substring(j * step, Math.Min(step, line.Length - j * step));
						gp.Reset();
						gp.AddString(part, FontFamily.GenericMonospace, 0, 15, buildRect, AvatarTextFormat);

						bg.Clear(Color.Transparent);
						using (Pen avatarTextOutline = new Pen(Color.Black, 4) { LineJoin = LineJoin.Round })
						{
							bg.DrawPath(avatarTextOutline, gp);
						}
						bg.FillPath(Brushes.White, gp);

						target.DrawImageUnscaled(builder, (int)(rect.X + j * (maxMonoBugWidth - 5)), (int)(rect.Y + i * charH));
					}
				}
			}
		}
	}
}
