// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Linq;
using TSLib.Commands;
using TSLib.Helper;
using TSLib.Messages;

namespace TSLib
{
	public static partial class TsPermissionHelper
	{
		public static ICommandPart GetAsParameter(IPermissionTransform permissionTransform, TsPermission permission)
		{
			if (permissionTransform is null || permissionTransform == DummyPermissionTransform.Instance)
				return new CommandParameter("permsid", permission.ToString());
			else
				return new CommandParameter("permid", permissionTransform.GetId(permission));
		}

		public static ICommandPart GetAsMultiParameter(IPermissionTransform permissionTransform, params TsPermission[] permission)
		{
			if (permissionTransform is null || permissionTransform == DummyPermissionTransform.Instance)
				return new CommandMultiParameter("permsid", permission.Select(x => x.ToString()));
			else
				return new CommandMultiParameter("permid", permission.Select(x => permissionTransform.GetId(x)));
		}

		public static PermOverview Combine(this PermOverview perm, PermOverview other)
		{
			// A pretty good documentation on permissions: http://yat.qa/ressourcen/definitionen-und-algorithmen/#permissions
			switch (perm.PermissionType)
			{
			case PermissionType.ServerGroup:
				switch (other.PermissionType)
				{
				case PermissionType.ServerGroup:
					if (perm.PermissionNegated && other.PermissionNegated)
						return perm.PermissionValue < other.PermissionValue ? perm : other;
					else if (other.PermissionNegated)
						return other;
					else
						return perm.PermissionValue > other.PermissionValue ? perm : other;

				case PermissionType.GlobalClient:
				case PermissionType.ChannelClient:
					return other;

				case PermissionType.Channel:
				case PermissionType.ChannelGroup:
					if (perm.PermissionSkip)
						return perm;
					else
						return other;

				default:
					throw Tools.UnhandledDefault(perm.PermissionType);
				}

			case PermissionType.GlobalClient:
				switch (other.PermissionType)
				{
				case PermissionType.ServerGroup:
					return Combine(other, perm);

				case PermissionType.GlobalClient:
					return perm.PermissionValue > other.PermissionValue ? perm : other;

				case PermissionType.Channel:
				case PermissionType.ChannelGroup:
					if (perm.PermissionSkip)
						return perm;
					else
						return other;

				case PermissionType.ChannelClient:
					return other;

				default:
					throw Tools.UnhandledDefault(perm.PermissionType);
				}

			case PermissionType.Channel:
				switch (other.PermissionType)
				{
				case PermissionType.ServerGroup:
				case PermissionType.GlobalClient:
					return Combine(other, perm);

				case PermissionType.Channel:
					return perm.PermissionValue > other.PermissionValue ? perm : other;

				case PermissionType.ChannelGroup:
				case PermissionType.ChannelClient:
					return other;

				default:
					throw Tools.UnhandledDefault(perm.PermissionType);
				}

			case PermissionType.ChannelGroup:
				switch (other.PermissionType)
				{
				case PermissionType.ServerGroup:
				case PermissionType.GlobalClient:
				case PermissionType.Channel:
					return perm;

				case PermissionType.ChannelGroup:
					return perm.PermissionValue > other.PermissionValue ? perm : other;

				case PermissionType.ChannelClient:
					return other;

				default:
					throw Tools.UnhandledDefault(perm.PermissionType);
				}

			case PermissionType.ChannelClient:
				switch (other.PermissionType)
				{
				case PermissionType.ServerGroup:
				case PermissionType.GlobalClient:
				case PermissionType.Channel:
				case PermissionType.ChannelGroup:
					return perm;

				case PermissionType.ChannelClient:
					return perm.PermissionValue > other.PermissionValue ? perm : other;

				default:
					throw Tools.UnhandledDefault(perm.PermissionType);
				}

			default:
				throw Tools.UnhandledDefault(perm.PermissionType);
			}
		}
	}
}
