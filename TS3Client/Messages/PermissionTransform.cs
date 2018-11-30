// TS3Client - A free TeamSpeak3 client implementation
// Copyright (C) 2017  TS3Client contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3Client.Messages
{
	using System;

	public interface IPermissionTransform
	{
		ushort GetId(Ts3Permission name);
		Ts3Permission GetName(ushort id);
	}

	public class DummyPermissionTransform : IPermissionTransform
	{
		public static readonly IPermissionTransform Instance = new DummyPermissionTransform();

		public ushort GetId(Ts3Permission name) => 0;
		public Ts3Permission GetName(ushort id) => Ts3Permission.undefined;
	}

	public class TablePermissionTransform : IPermissionTransform
	{
		private readonly Ts3Permission[] nameTable;
		private readonly ushort[] idTable;

		public TablePermissionTransform(Ts3Permission[] nameTable)
		{
			this.nameTable = nameTable;
			idTable = new ushort[Enum.GetValues(typeof(Ts3Permission)).Length];
			for (ushort i = 0; i < nameTable.Length; i++)
			{
				idTable[(int)nameTable[i]] = i;
			}
		}

		public ushort GetId(Ts3Permission name) => (int)name < idTable.Length ? idTable[(int)name] : (ushort)0;
		public Ts3Permission GetName(ushort id) => id < nameTable.Length ? nameTable[id] : Ts3Permission.undefined;
	}
}
