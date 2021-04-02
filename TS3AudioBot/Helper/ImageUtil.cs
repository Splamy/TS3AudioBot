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
using System.Threading.Tasks;

namespace TS3AudioBot.Helper
{
	internal static class ImageUtil
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

		public const int ResizeMaxWidthDefault = 320;

		public static async Task<ImageHolder> ResizeImageSave(Stream imgStream, int resizeMaxWidth = ResizeMaxWidthDefault)
		{
			try
			{
				using var limitStream = new LimitStream(imgStream, Limits.MaxImageStreamSize);
				using var mem = new MemoryStream();
				await limitStream.CopyToAsync(mem);
				mem.Seek(0, SeekOrigin.Begin);
				return ResizeImage(mem, resizeMaxWidth);
			}
			catch (ImageFormatException)
			{
				Log.Debug("Dropping image because of unknown format");
				throw Error.LocalStr("Dropping image because of unknown format"); // TODO
			}
			catch (EntityTooLargeException)
			{
				Log.Debug("Dropping image because too large");
				throw Error.LocalStr("Dropping image because too large"); // TODO
			}
		}

		private static ImageHolder ResizeImage(Stream imgStream, int resizeMaxWidth)
		{
			using var img = Image.Load(imgStream);
			if (img.Width > Limits.MaxImageDimension || img.Height > Limits.MaxImageDimension
				|| img.Width == 0 || img.Height == 0)
				throw Error.LocalStr("Dropping image because too large"); // TODO

			if (img.Width <= resizeMaxWidth)
				return SaveAdaptive(img);

			float ratio = img.Width / (float)img.Height;
			img.Mutate(x => x.Resize(resizeMaxWidth, (int)(resizeMaxWidth / ratio)));

			return SaveAdaptive(img);
		}

		private static ImageHolder SaveAdaptive(Image img)
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
			var mime = format.DefaultMimeType;
			var mem = new MemoryStream();
			img.Save(mem, format);
			mem.Seek(0, SeekOrigin.Begin);
			return new ImageHolder(mem, mime);
		}
	}

	class ImageHolder : IDisposable
	{
		public Stream Stream { get; }
		public string Mime { get; }

		public ImageHolder(Stream stream, string mime)
		{
			Stream = stream;
			Mime = mime;
		}

		public void Dispose() => Stream.Dispose();
	}
}
