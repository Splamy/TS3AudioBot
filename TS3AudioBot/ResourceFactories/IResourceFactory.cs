// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3AudioBot.ResourceFactories
{
	using System;
	using Helper;

	public interface IResourceFactory : IDisposable
	{
		AudioType FactoryFor { get; }

		bool MatchLink(string uri);
		R<PlayResource> GetResource(string url);
		R<PlayResource> GetResourceById(string id, string name);
		string RestoreLink(string id);
		R<PlayResource> PostProcess(PlayData data);
	}
}
