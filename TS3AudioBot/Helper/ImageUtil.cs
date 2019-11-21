// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using TS3AudioBot.Localization;

namespace TS3AudioBot.Helper
{
	internal static class ImageUtil
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public const int ResizeMaxWidthDefault = 320;

		public static R<Stream, LocalStr> ResizeImageSave(Stream imgStream, out string mime, int resizeMaxWidth = ResizeMaxWidthDefault)
		{
			mime = null;
			if (imgStream == null)
				return null;
			try
			{
				using (var limitStream = new LimitStream(imgStream, Limits.MaxImageStreamSize))
					return ResizeImage(limitStream, out mime, resizeMaxWidth);
			}
			catch (NotSupportedException)
			{
				Log.Debug("Dropping image because of unknown format");
				return new LocalStr("Dropping image because of unknown format"); // TODO
			}
			catch (EntityTooLargeException)
			{
				Log.Debug("Dropping image because too large");
				return new LocalStr("Dropping image because too large"); // TODO
			}
		}

		private static Stream ResizeImage(Stream imgStream, out string mime, int resizeMaxWidth = ResizeMaxWidthDefault)
		{
			mime = null;
			using (var img = Image.Load(imgStream))
			{
				if (img.Width > Limits.MaxImageDimension || img.Height > Limits.MaxImageDimension
					|| img.Width == 0 || img.Height == 0)
					return null;

				if (img.Width <= resizeMaxWidth)
					return SaveAdaptive(img, out mime);

				float ratio = img.Width / (float)img.Height;
				img.Mutate(x => x.Resize(resizeMaxWidth, (int)(resizeMaxWidth / ratio)));

				return SaveAdaptive(img, out mime);
			}
		}

		private static Stream SaveAdaptive(Image img, out string mime)
		{
			IImageFormat format;
			if (img.Frames.Count > 1)
			{
				format = GifFormat.Instance;
			}
			else
			{
				format = JpegFormat.Instance;
			}
			mime = format.DefaultMimeType;
			var mem = new MemoryStream();
			img.Save(mem, format);
			mem.Seek(0, SeekOrigin.Begin);
			return mem;
		}
	}
}
