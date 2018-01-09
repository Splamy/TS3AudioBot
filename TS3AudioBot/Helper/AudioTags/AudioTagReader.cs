// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper.AudioTags
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	internal static class AudioTagReader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Dictionary<string, Tag> TagDict;

		static AudioTagReader()
		{
			TagDict = new Dictionary<string, Tag>();
			Register(new Id3_1());
			Register(new Id3_2());
		}

		private static void Register(Tag tagHeader)
		{
			TagDict.Add(tagHeader.TagId, tagHeader);
		}

		public static string GetTitle(Stream fileStream)
		{
			var sr = new BinaryReader(fileStream);
			string tag = Encoding.ASCII.GetString(sr.ReadBytes(3));
			if (TagDict.TryGetValue(tag, out var tagHeader))
			{
				try { return tagHeader.GetTitle(sr)?.TrimEnd('\0'); }
				catch (IOException) { }
				catch (FormatException fex) { Log.Debug(fex, "Audiotag format exception"); }
				catch (NullReferenceException) { Log.Debug("Unparsed link!"); }
			}
			return null;
		}

		// TODO the concept is very dirty.
		// Stream needs to be read twice for each factory
		public static byte[] GetImage(Stream fileStream)
		{
			var sr = new BinaryReader(fileStream);
			string tag = Encoding.ASCII.GetString(sr.ReadBytes(3));
			if (TagDict.TryGetValue(tag, out var tagHeader))
			{
				try { return tagHeader.GetImage(sr); }
				catch (IOException) { }
				catch (FormatException fex) { Log.Debug(fex, "Audiotag format exception"); }
				catch (NullReferenceException) { Log.Debug("Unparsed link!"); }
			}
			return null;
		}

		private abstract class Tag
		{
			public abstract string TagId { get; }
			public abstract string GetTitle(BinaryReader fileStream);
			public abstract byte[] GetImage(BinaryReader fileStream);
		}

		// ReSharper disable InconsistentNaming
		private class Id3_1 : Tag
		{
			private const int TitleLength = 30;
			public override string TagId => "TAG";

			public override string GetTitle(BinaryReader fileStream)
			{
				// 3 bytes skipped for TagID
				string title = Encoding.ASCII.GetString(fileStream.ReadBytes(TitleLength));

				// ignore other blocks

				return title;
			}

			public override byte[] GetImage(BinaryReader fileStream)
			{
				throw new NotSupportedException();
			}
		}

		private class Id3_2 : Tag
		{
			private readonly int v2_TT2 = FrameIdV2("TT2"); // Title
			private readonly int v2_PIC = FrameIdV2("PIC"); // Title
			private readonly uint v3_TIT2 = FrameIdV3("TIT2"); // Title
			private readonly uint v3_APIC = FrameIdV3("APIC"); // Picture

			public override string TagId => "ID3";

			private IdData GetData(BinaryReader fileStream)
			{
				var retdata = new IdData();

				// using the official id3 tag documentation
				// http://id3.org/id3v2.3.0#ID3_tag_version_2.3.0

				int readCount = 10;

				// read + validate header                                    [10 bytes]
				// skipped for TagID                                         >03 bytes
				byte versionMajor = fileStream.ReadByte(); //                >01 bytes
				/*byte version_minor =*/ fileStream.ReadByte(); //           >01 bytes
				/*byte data_flags =*/ fileStream.ReadByte(); //              >01 bytes
				byte[] tagSize = fileStream.ReadBytes(4); //                 >04 bytes
				int tagSizeInt = 0;
				for (int i = 0; i < 4; i++)
					tagSizeInt |= tagSize[3 - i] << (i * 7);
				readCount += 10;

				#region ID3v2
				if (versionMajor == 2)
				{
					while (readCount < tagSizeInt + 10)
					{
						// frame header                                      [06 bytes]
						int frameId = fileStream.ReadInt24BE(); //           >03 bytes
						int frameSize = fileStream.ReadInt24BE(); //         >03 bytes
						readCount += 6;

						if (frameId == v2_TT2)
						{
							var textBuffer = fileStream.ReadBytes(frameSize);
							retdata.Title = DecodeString(textBuffer[0], textBuffer, 1, frameSize - 1);
						}
						else if (frameId == v2_PIC)
						{
							var textEncoding = fileStream.ReadByte();
							var imageType = fileStream.ReadInt24BE(); // JPG or PNG (or other?)
							var pictureType = fileStream.ReadByte();
							var description = new List<byte>();
							byte textByte;
							while ((textByte = fileStream.ReadByte()) != 0)
								description.Add(textByte);

							retdata.Picture = fileStream.ReadBytes(frameSize - (description.Count + 5));
						}
						else if (frameId == 0) { break; }
						else
						{
							fileStream.ReadBytes(frameSize);
							readCount += frameSize;
						}
					}
				}
				#endregion
				#region ID3v3/4
				else if (versionMajor == 3 || versionMajor == 4)
				{
					while (readCount < tagSizeInt + 10)
					{
						// frame header                                        [10 bytes]
						uint frameId = fileStream.ReadUInt32BE(); //           >04 bytes
						int frameSize = fileStream.ReadInt32BE(); //           >04 bytes
																  /*ushort frame_flags =*/
						fileStream.ReadUInt16BE(); // >02 bytes
						readCount += 10;

						// content
						if (frameId == v3_TIT2)
						{
							var textBuffer = fileStream.ReadBytes(frameSize);
							// is a string, so the first byte is a indicator byte
							retdata.Title = DecodeString(textBuffer[0], textBuffer, 1, frameSize - 1);
						}
						else if (frameId == v3_APIC)
						{
							var textEncoding = fileStream.ReadByte();
							var mime = new List<byte>();
							byte textByte;
							while ((textByte = fileStream.ReadByte()) != 0)
								mime.Add(textByte);
							var pictureType = fileStream.ReadByte();
							var description = new List<byte>();
							while ((textByte = fileStream.ReadByte()) != 0)
								description.Add(textByte);

							retdata.Picture = fileStream.ReadBytes(frameSize - (description.Count + mime.Count + 2));
						}
						else if (frameId == 0) { break; }
						else
						{
							fileStream.ReadBytes(frameSize);
							readCount += frameSize;
						}
					}
				}
				#endregion
				else
					throw new FormatException("Major id3 tag version not supported");

				return retdata;
			}

			public override string GetTitle(BinaryReader fileStream)
			{
				var data = GetData(fileStream);
				return data.Title;
			}

			public override byte[] GetImage(BinaryReader fileStream)
			{
				var data = GetData(fileStream);
				return data.Picture;
			}

			private static string DecodeString(byte type, byte[] textBuffer, int offset, int length)
			{
				switch (type)
				{
				case 0:
					return Encoding.GetEncoding(28591).GetString(textBuffer, offset, length);
				case 1:
					return Encoding.Unicode.GetString(textBuffer, offset, length);
				case 2:
					return new UnicodeEncoding(true, false).GetString(textBuffer, offset, length);
				case 3:
					return Encoding.UTF8.GetString(textBuffer, offset, length);
				default:
					throw new FormatException("The id3 tag is damaged");
				}
			}

			private static int FrameIdV2(string id)
			{
				return BitConverterBigEndian.ToInt24(Encoding.ASCII.GetBytes(id));
			}

			private static uint FrameIdV3(string id)
			{
				return BitConverterBigEndian.ToUInt32(Encoding.ASCII.GetBytes(id));
			}
		}

		private struct IdData
		{
			public string Title { get; set; }
			public byte[] Picture { get; set; }
		}

		// ReSharper enable InconsistentNaming
	}
}
