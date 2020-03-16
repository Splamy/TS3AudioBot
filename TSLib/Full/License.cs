// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using Chaos.NaCl.Ed25519Ref10;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using TSLib.Helper;

namespace TSLib.Full
{
	public class Licenses
	{
		public static readonly byte[] LicenseRootKey =
		{
			0xcd, 0x0d, 0xe2, 0xae, 0xd4, 0x63, 0x45, 0x50, 0x9a, 0x7e, 0x3c, 0xfd, 0x8f, 0x68, 0xb3, 0xdc, 0x75, 0x55, 0xb2,
			0x9d, 0xcc, 0xec, 0x73, 0xcd, 0x18, 0x75, 0x0f, 0x99, 0x38, 0x12, 0x40, 0x8a
		};

		public List<LicenseBlock> Blocks { get; set; }

		public static R<Licenses, string> Parse(ReadOnlySpan<byte> data)
		{
			if (data.Length < 1)
				return "License too short";
			var version = data[0];
			if (version != 1)
				return "Unsupported version";

			// Read licenses
			var res = new Licenses { Blocks = new List<LicenseBlock>() };
			data = data.Slice(1);
			while (data.Length > 0)
			{
				// Read next license
				var result = LicenseBlock.Parse(data);
				if (!result.Ok)
					return result.Error;
				var (license, len) = result.Value;

				// TODO Check valid times

				res.Blocks.Add(license);
				data = data.Slice(len);
			}
			return res;
		}

		public byte[] DeriveKey()
		{
			var round = LicenseRootKey; //Ed25519.DecodePoint(LicenseRootKey);
			foreach (var block in Blocks)
				round = block.DeriveKey(round);
			return round;
		}
	}

	public abstract class LicenseBlock
	{
		private const int MinBlockLen = 42;

		public abstract ChainBlockType Type { get; }
		public byte[] Key { get; set; }
		public DateTime NotValidBefore { get; set; }
		public DateTime NotValidAfter { get; set; }
		public byte[] Hash { get; set; }

		public static R<(LicenseBlock block, int read), string> Parse(ReadOnlySpan<byte> data)
		{
			if (data.Length < MinBlockLen)
			{
				return "License too short";
			}
			if (data[0] != 0)
			{
				return $"Wrong key kind {data[0]} in license";
			}

			LicenseBlock block;
			int read;
			switch (data[33])
			{
			case 0:
				var result = ReadNullString(data.Slice(46));
				if (!result.Ok) return result.Error;
				var nullStr = result.Value;
				block = new IntermediateLicenseBlock { Issuer = nullStr.str };
				read = 5 + nullStr.read;
				break;

			case 2:
				if (!Enum.IsDefined(typeof(ServerLicenseType), data[42]))
					return $"Unknown license type {data[42]}";
				result = ReadNullString(data.Slice(47));
				if (!result.Ok) return result.Error;
				nullStr = result.Value;
				block = new ServerLicenseBlock { Issuer = result.Value.str, LicenseType = (ServerLicenseType)data[42] };
				read = 6 + nullStr.read;
				break;

			case 32:
				block = new EphemeralLicenseBlock();
				read = 0;
				break;

			default:
				return $"Invalid license block type {data[33]}";
			}

			block.NotValidBefore = Tools.UnixTimeStart.AddSeconds(BinaryPrimitives.ReadUInt32BigEndian(data.Slice(34)) + 0x50e22700uL);
			block.NotValidAfter = Tools.UnixTimeStart.AddSeconds(BinaryPrimitives.ReadUInt32BigEndian(data.Slice(38)) + 0x50e22700uL);
			if (block.NotValidAfter < block.NotValidBefore)
				return "License times are invalid";

			block.Key = data.Slice(1, 32).ToArray();

			var allLen = MinBlockLen + read;
			var hash = TsCrypt.Hash512It(data.Slice(1, allLen - 1).ToArray());
			block.Hash = hash.AsSpan(0, 32).ToArray();

			return (block, allLen);
		}

		private static R<(string str, int read), string> ReadNullString(ReadOnlySpan<byte> data)
		{
			var termIndex = data.IndexOf((byte)0);
			if (termIndex >= 0)
				return (data.Slice(0, termIndex).NewUtf8String(), termIndex);
			return "Non-null-terminated issuer string";
		}

		/// <summary>
		/// Calculates a new public key by processing an existing one with this license bock.
		/// The key is calculated as following: <code>new_pub_key = pub_key * hash + parent</code>.
		/// Where <code>pub_key</code> and <code>parent</code> are public keys, and <code>hash</code> a private key.
		/// </summary>
		/// <param name="parent">The preceeding key (from the previous block or root key).</param>
		/// <returns>The new public key after processing it with this block.</returns>
		public byte[] DeriveKey(ReadOnlySpan<byte> parent)
		{
			ScalarOperations.sc_clamp(Hash);
			GroupOperations.ge_frombytes_negate_vartime(out var pubkey, Key);
			GroupOperations.ge_frombytes_negate_vartime(out var parkey, parent);

			GroupOperations.ge_scalarmult_vartime(out GroupElementP1P1 res, Hash, pubkey);
			GroupOperations.ge_p3_to_cached(out var pargrp, parkey);

			GroupOperations.ge_p1p1_to_p3(out var r, res);
			GroupOperations.ge_add(out var a, r, pargrp);
			GroupOperations.ge_p1p1_to_p3(out var r2, a);
			var final = new byte[32];
			GroupOperations.ge_p3_tobytes(final, r2);
			final[31] ^= 0x80;

			return final;
		}
	}

	#region BlockTypes

	public class IntermediateLicenseBlock : LicenseBlock
	{
		public override ChainBlockType Type => ChainBlockType.Intermediate;
		public string Issuer { get; set; }
	}

	public class WebsiteLicenseBlock : LicenseBlock
	{
		public override ChainBlockType Type => ChainBlockType.Website;
		public string Issuer { get; set; }
	}

	public class CodeLicenseBlock : LicenseBlock
	{
		public override ChainBlockType Type => ChainBlockType.Code;
		public string Issuer { get; set; }
	}

	public class ServerLicenseBlock : LicenseBlock
	{
		public override ChainBlockType Type => ChainBlockType.Server;
		public string Issuer { get; set; }
		public ServerLicenseType LicenseType { get; set; }
	}

	public class EphemeralLicenseBlock : LicenseBlock
	{
		public override ChainBlockType Type => ChainBlockType.Ephemeral;
	}

	public enum ChainBlockType : byte
	{
		Intermediate = 0,
		Website = 1,
		Server = 2,
		Code = 3,
		// (Not used in license parser)
		//Token = 4,
		//LicenseSign = 5,
		//MyTsIdSign = 6,
		Ephemeral = 32,
	}

	#endregion

	public enum ServerLicenseType : byte
	{
		None = 0,
		Offline,
		Sdk,
		SdkOffline,
		Npl,
		Athp,
		Aal,
		Default,
	}
}
