// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web.Interface
{
	using System;

	internal class FileRedirect : IFolderProvider
	{
		private string source;
		private SiteMapper map;
		private Uri redirect;

		public FileRedirect(SiteMapper map, string source, string target)
		{
			this.source = source;
			this.map = map;
			redirect = new Uri(WebComponent.Dummy, target);
		}

		public ISiteProvider GetFile(string subPath)
		{
			if (subPath == source)
				return map.TryGetSite(redirect);
			return null;
		}
	}
}
