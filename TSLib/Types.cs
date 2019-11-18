// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TSLib.Full;

namespace TSLib
{
	public partial struct Uid
	{
		public static bool IsValid(string uid)
		{
			if (uid == "anonymous" || uid == "serveradmin")
				return true;
			var result = TsCrypt.Base64Decode(uid);
			return result.Ok && result.Value.Length == 20;
		}
	}
}
