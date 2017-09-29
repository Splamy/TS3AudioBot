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
				try { return tagHeader.GetTitle(sr).TrimEnd('\0'); }
				catch (IOException) { }
				catch (FormatException fex) { Log.Write(Log.Level.Debug, "ATR FEX: " + fex.Message); }
				catch (NullReferenceException) { Log.Write(Log.Level.Debug, "ATR Unparsed Link!"); }
			}
			return null;
		}

		private abstract class Tag
		{
			public abstract string TagId { get; }
			public abstract string GetTitle(BinaryReader fileStream);
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
		}

		private class Id3_2 : Tag
		{
			private readonly int v2_TT2 = FrameIdV2("TT2"); // Title
			private readonly uint v3_TIT2 = FrameIdV3("TIT2"); // Title

			public override string TagId => "ID3";

			public override string GetTitle(BinaryReader fileStream)
			{
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
							string title;
							byte[] textBuffer = fileStream.ReadBytes(frameSize);
							if (textBuffer[0] == 0)
								title = Encoding.GetEncoding(28591).GetString(textBuffer, 1, frameSize - 1);
							else
								throw new FormatException("The id3 tag is damaged");
							return title;
						}
						else
						{
							fileStream.ReadBytes(frameSize);
							readCount += frameSize;
						}
					}
					throw new FormatException("The id3 tag contains no title");
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
						/*ushort frame_flags =*/ fileStream.ReadUInt16BE(); // >02 bytes
						readCount += 10;

						// content
						if (frameId == v3_TIT2)
						{
							string title;
							byte[] textBuffer = fileStream.ReadBytes(frameSize);
							// is a string, so the first byte is a indicator byte
							switch (textBuffer[0])
							{
							case 0:
								title = Encoding.GetEncoding(28591).GetString(textBuffer, 1, frameSize - 1); break;
							case 1:
								title = Encoding.Unicode.GetString(textBuffer, 1, frameSize - 1); break;
							case 2:
								title = new UnicodeEncoding(true, false).GetString(textBuffer, 1, frameSize - 1); break;
							case 3:
								title = Encoding.UTF8.GetString(textBuffer, 1, frameSize - 1); break;
							default:
								throw new FormatException("The id3 tag is damaged");
							}
							return title;
						}
						else if (frameId == 0)
							break;
						else
						{
							fileStream.ReadBytes(frameSize);
							readCount += frameSize;
						}
					}
					throw new FormatException("The id3 tag contains no title");
				}
				#endregion
				return null;
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
		// ReSharper enable InconsistentNaming
	}
}
