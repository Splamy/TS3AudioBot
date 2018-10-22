// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Rights.Matchers
{
	using System.Collections.Generic;
	using System.Linq;
	using TS3Client;

	internal class MatchPermission : Matcher
	{
		private readonly Dictionary<PermissionId, (PermCompare, int)> permissions;

		public MatchPermission(string[] permissions)
		{
			this.permissions = new Dictionary<PermissionId, (PermCompare, int)>(permissions.Length);
			foreach (var permOp in permissions.Select(x => (PermCompare.Equal, 0)))
				this.permissions.Add(PermissionId.b_channel_create_child, permOp);
		}

		public override bool Matches(ExecuteContext ctx) => false; // TODO
	}
}
