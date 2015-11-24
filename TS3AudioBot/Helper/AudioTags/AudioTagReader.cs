using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace TS3AudioBot.Helper.AudioTags
{
	public static class AudioTagReader
	{
		private static Dictionary<string, Tag> tagDict;

		static AudioTagReader()
		{
			tagDict = new Dictionary<string, Tag>();
			Register(new Id3_1());
			Register(new Id3_2());
		}

		private static void Register(Tag tagHeader)
		{
			tagDict.Add(tagHeader.TagID, tagHeader);
		}

		public static string GetTitle(Stream fileStream)
		{
			var sr = new BinaryReader(fileStream);
			string tag = Encoding.ASCII.GetString(sr.ReadBytes(3));
			Tag tagHeader;
			if (tagDict.TryGetValue(tag, out tagHeader))
			{
				try { return tagHeader.GetTitle(sr).TrimEnd('\0'); }
				catch (IOException) { }
				catch (FormatException) { }
			}
			return null;
		}

		abstract class Tag
		{
			public abstract string TagID { get; }
			public abstract string GetTitle(BinaryReader fileStream);
		}

		class Id3_1 : Tag
		{
			public override string TagID { get { return "TAG"; } }

			public override string GetTitle(BinaryReader fileStream)
			{
				const int TITLE_LENGTH = 30;

				// 3 bytes skipped for TagID
				string title = Encoding.ASCII.GetString(fileStream.ReadBytes(TITLE_LENGTH));

				// ignore other blocks

				return title;
			}
		}

		class Id3_2 : Tag
		{
			private readonly int v2_TT2 = FrameIdV2("TT2"); // Title
			private readonly uint v3_TIT2 = FrameIdV3("TIT2"); // Title

			public override string TagID { get { return "ID3"; } }

			public override string GetTitle(BinaryReader fileStream)
			{
				// using the official id3 tag documentation
				// http://id3.org/id3v2.3.0#ID3_tag_version_2.3.0

				int read_count = 10;

				// read + validate header                                    [10 bytes]
				// skipped for TagID                                         >03 bytes
				byte version_major = fileStream.ReadByte(); //               >01 bytes
				byte version_minor = fileStream.ReadByte(); //               >01 bytes
				byte data_flags = fileStream.ReadByte(); //                  >01 bytes
				byte[] tag_size = fileStream.ReadBytes(4); //                >04 bytes
				int tag_size_int = 0;
				for (int i = 0; i < 4; i++)
					tag_size_int |= tag_size[3 - i] << (i * 7);
				read_count += 10;

				#region ID3v2											     
				if (version_major == 2)
				{
					while (read_count < tag_size_int + 10)
					{
						// frame header                                      [06 bytes]
						int frame_id = fileStream.ReadInt24BE(); //          >03 bytes
						int frame_size = fileStream.ReadInt24BE(); //        >03 bytes
						read_count += 6;

						if (frame_id == v2_TT2)
						{
							string title;
							byte[] textBuffer = fileStream.ReadBytes(frame_size);
							if (textBuffer[0] == 0)
								title = Encoding.GetEncoding(28591).GetString(textBuffer, 1, frame_size - 1);
							else
								throw new FormatException("The id3 tag is damaged");
							return title;
						}
						else
						{
							fileStream.ReadBytes(frame_size);
							read_count += frame_size;
						}
					}
				}
				#endregion
				#region ID3v3/4
				else if (version_major == 3 || version_major == 4)
				{
					while (read_count < tag_size_int + 10)
					{
						// frame header                                      [10 bytes]
						uint frame_id = fileStream.ReadUInt32BE(); //        >04 bytes
						int frame_size = fileStream.ReadInt32BE(); //        >04 bytes
						ushort frame_flags = fileStream.ReadUInt16BE(); //   >02 bytes 
						read_count += 10;

						// content
						if (frame_id == v3_TIT2)
						{
							string title;
							byte[] textBuffer = fileStream.ReadBytes(frame_size);
							// is a string, so the first byte is a indicator byte
							switch (textBuffer[0])
							{
							case 0:
								title = Encoding.GetEncoding(28591).GetString(textBuffer, 1, frame_size - 1); break;
							case 1:
								title = Encoding.Unicode.GetString(textBuffer, 1, frame_size - 1); break;
							case 2:
								title = new UnicodeEncoding(true, false).GetString(textBuffer, 1, frame_size - 1); break;
							case 3:
								title = Encoding.UTF8.GetString(textBuffer, 1, frame_size - 1); break;
							default:
								throw new FormatException("The id3 tag is damaged");
							}
							return title;
						}
						else if (frame_id == 0)
							break;
						else
						{
							fileStream.ReadBytes(frame_size);
							read_count += frame_size;
						}
					}
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
	}
}
