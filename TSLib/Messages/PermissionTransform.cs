// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Messages
{
	public interface IPermissionTransform
	{
		ushort GetId(TsPermission name);
		TsPermission GetName(ushort id);
	}

	public class DummyPermissionTransform : IPermissionTransform
	{
		public static readonly IPermissionTransform Instance = new DummyPermissionTransform();

		public ushort GetId(TsPermission name) => 0;
		public TsPermission GetName(ushort id) => TsPermission.undefined;
	}

	public class TablePermissionTransform : IPermissionTransform
	{
		private readonly TsPermission[] nameTable;
		private readonly ushort[] idTable;

		public TablePermissionTransform(TsPermission[] nameTable)
		{
			this.nameTable = nameTable;
			idTable = new ushort[Enum.GetValues(typeof(TsPermission)).Length];
			for (ushort i = 0; i < nameTable.Length; i++)
			{
				idTable[(int)nameTable[i]] = i;
			}
		}

		public ushort GetId(TsPermission name) => (int)name < idTable.Length ? idTable[(int)name] : (ushort)0;
		public TsPermission GetName(ushort id) => id < nameTable.Length ? nameTable[id] : TsPermission.undefined;
	}
}
