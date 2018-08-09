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
	using SixLabors.ImageSharp;
	using SixLabors.ImageSharp.PixelFormats;
	using SixLabors.ImageSharp.Processing;
	using System;
	using System.IO;

	internal static class ImageUtil
	{
		public const int ResizeMaxWidthDefault = 320;

		public static Stream ResizeImage(Stream imgStream, int resizeMaxWidth = ResizeMaxWidthDefault)
		{
			try
			{
				using (var img = Image.Load(imgStream))
				{
					if (img.Width <= resizeMaxWidth)
						return SaveAdaptive(img);

					float ratio = img.Width / (float)img.Height;
					img.Mutate(x => x.Resize(resizeMaxWidth, (int)(resizeMaxWidth / ratio)));

					return SaveAdaptive(img);
				}
			}
			catch (NotSupportedException)
			{
				return null;
			}
		}

		private static MemoryStream SaveAdaptive(Image<Rgba32> img)
		{
			var mem = new MemoryStream();
			if (img.Frames.Count > 1)
			{
				img.Save(mem, ImageFormats.Gif);
			}
			else
			{
				img.Save(mem, ImageFormats.Jpeg);
			}
			return mem;
		}
	}
}
