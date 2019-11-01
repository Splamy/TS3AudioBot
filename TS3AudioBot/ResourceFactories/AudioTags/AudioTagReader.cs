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
using System.IO;
using System.Text;

namespace TS3AudioBot.ResourceFactories.AudioTags
{
	internal static class AudioTagReader
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Dictionary<string, Tag> TagDict = new Dictionary<string, Tag>();

		static AudioTagReader()
		{
			Register(new Id3_1());
			Register(new Id3_2());
		}

		private static void Register(Tag tagHeader)
		{
			TagDict.Add(tagHeader.TagId, tagHeader);
		}

		public static HeaderData GetData(Stream fileStream)
		{
			var sr = new BinaryReader(fileStream);
			string tag = Encoding.ASCII.GetString(sr.ReadBytes(3));
			if (TagDict.TryGetValue(tag, out var tagHeader))
			{
				try
				{
					var data = tagHeader.GetData(sr);
					if (data is null)
						return null;
					data.Title = data.Title?.TrimEnd('\0');
					return data;
				}
				catch (IOException) { }
				catch (FormatException fex) { Log.Debug(fex, "Audiotag has an invalid format"); }
				catch (Exception ex) { Log.Warn(ex, "Unknown error while parsing audiotag"); }
			}
			return null;
		}

		private abstract class Tag
		{
			public abstract string TagId { get; }
			public abstract HeaderData GetData(BinaryReader fileStream);
		}

		// ReSharper disable InconsistentNaming
		private class Id3_1 : Tag
		{
			private const int TitleLength = 30;
			public override string TagId => "TAG";

			public override HeaderData GetData(BinaryReader fileStream)
			{
				// 3 bytes skipped for TagID
				return new HeaderData
				{
					Title = Encoding.ASCII.GetString(fileStream.ReadBytes(TitleLength)),
					Picture = null,
				};

				// ignore other blocks
			}
		}

		private class Id3_2 : Tag
		{
			private readonly int v2_TT2 = FrameIdV2("TT2"); // Title
			private readonly int v2_PIC = FrameIdV2("PIC"); // Picture
			private readonly uint v3_TIT2 = FrameIdV3("TIT2"); // Title
			private readonly uint v3_APIC = FrameIdV3("APIC"); // Picture
			private readonly uint v3_PIC0 = FrameIdV3("PIC\0"); // Picture

			public override string TagId => "ID3";

			// ReSharper disable UnusedVariable
			public override HeaderData GetData(BinaryReader fileStream)
			{
				var retdata = new HeaderData();

				// using the official id3 tag documentation
				// http://id3.org/id3v2.3.0#ID3_tag_version_2.3.0

				// read + validate header                                    [10 bytes]
				// skipped for TagID                                         >03 bytes
				byte versionMajor = fileStream.ReadByte(); //                >01 bytes
				byte version_minor = fileStream.ReadByte(); //               >01 bytes
				byte data_flags = fileStream.ReadByte(); //                  >01 bytes
				int tagSize = fileStream.ReadId3Int(); //                    >04 bytes

				// start at 0, the header is excluded from `tagSize`
				int readCount = 0;

				#region ID3v2
				if (versionMajor == 2)
				{
					while (readCount < tagSize)
					{
						// frame header                                      [06 bytes]
						int frameId = fileStream.ReadInt24Be(); //           >03 bytes
						int frameSize = fileStream.ReadInt24Be(); //         >03 bytes
						readCount += 6;

						if (readCount + frameSize > tagSize)
							throw new FormatException("Frame position+size exceedes header size");

						if (frameId == v2_TT2)
						{
							var textBuffer = fileStream.ReadBytes(frameSize);
							retdata.Title = DecodeString(textBuffer[0], textBuffer, 1, frameSize - 1);
						}
						else if (frameId == v2_PIC)
						{
							var textEncoding = fileStream.ReadByte();
							var imageType = fileStream.ReadInt24Be(); // JPG or PNG (or other?)
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
					while (readCount < tagSize)
					{
						// frame header                                        [10 bytes]
						uint frameId = fileStream.ReadUInt32Be(); //           >04 bytes
						int frameSize = versionMajor == 4 //                   >04 bytes
							? fileStream.ReadId3Int()
							: fileStream.ReadInt32Be();
						ushort frame_flags = fileStream.ReadUInt16Be(); //     >02 bytes
						readCount += 10;

						if ((frameId & 0xFF) == 0)
						{
							// legacy tags start here which we don't support
							break;
						}

						if (frameSize <= 0 || readCount + frameSize > tagSize)
							throw new FormatException("Frame position+size exceedes header size");

						// content
						if (frameId == v3_TIT2)
						{
							var textBuffer = fileStream.ReadBytes(frameSize);
							// is a string, so the first byte is a indicator byte
							retdata.Title = DecodeString(textBuffer[0], textBuffer, 1, frameSize - 1);
						}
						else if (frameId == v3_APIC || frameId == v3_PIC0)
						{
							var textEncoding = fileStream.ReadByte(); //                                  >01 bytes
							var mimeLen = ReadNullTermString(fileStream, 0, null); //                     >?? bytes
							var pictureType = fileStream.ReadByte(); //                                   >01 bytes
							var descriptionLen = ReadNullTermString(fileStream, textEncoding, null); //   >?? bytes

							retdata.Picture = fileStream.ReadBytes(frameSize - (mimeLen + descriptionLen + 2));
						}
						else if (frameId == 0) { break; }
						else { fileStream.ReadBytes(frameSize); }
						readCount += frameSize;
					}
				}
				#endregion
				else
					throw new FormatException("Major id3 tag version not supported");

				return retdata;
			}
			// ReSharper restore UnusedVariable

			private static int ReadNullTermString(BinaryReader fileStream, byte encoding, List<byte> text)
			{
				bool unicode = encoding == 1 || encoding == 2;

				if (!unicode)
				{
					int read = 0;
					byte textByte;
					while ((textByte = fileStream.ReadByte()) > 0)
					{
						text?.Add(textByte);
						read++;
					}
					return read + 1; // +1 = null-byte
				}
				else
				{
					var buffer = new byte[2];
					int read = 0;
					while (fileStream.Read(buffer, 0, 2) == 2 && (buffer[0] != 0 || buffer[1] != 0))
					{
						text?.AddRange(buffer);
						read += 2;
					}
					return read + 2;
				}
			}

			private static readonly Encoding UnicodeBeEncoding = new UnicodeEncoding(true, false);
			private static Encoding GetEncoding(byte type)
			{
				switch (type)
				{
				case 0:
					return Encoding.GetEncoding(28591);
				case 1:
					return Encoding.Unicode;
				case 2:
					return UnicodeBeEncoding;
				case 3:
					return Encoding.UTF8;
				default:
					throw new FormatException("The id3 tag is damaged");
				}
			}

			private static string DecodeString(byte type, byte[] textBuffer, int offset, int length)
				=> GetEncoding(type).GetString(textBuffer, offset, length);

			private static int FrameIdV2(string id)
			{
				return BitConverterBigEndian.ToInt24(Encoding.ASCII.GetBytes(id));
			}

			private static uint FrameIdV3(string id)
			{
				return BitConverterBigEndian.ToUInt32(Encoding.ASCII.GetBytes(id));
			}
		}

		// ReSharper enable InconsistentNaming
	}

	internal class HeaderData
	{
		public string Title { get; set; }
		public byte[] Picture { get; set; }
	}
}
