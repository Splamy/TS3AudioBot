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

namespace TS3AudioBot.Web.Interface
{
	using Helper;
	using System;
	using System.Collections.Generic;

	internal class SiteMapper
	{
		private MapNode RootNode = new MapNode("", "");

		public void Map(string target, IProvider provider)
		{
			var nodesPath = target.Split('/');
			var currentNode = RootNode;
			for (int i = 0; i < nodesPath.Length - 1; i++)
			{
				if (!currentNode.childNodes.TryGetValue(nodesPath[i], out var childNode))
				{
					childNode = new MapNode(nodesPath[i], currentNode.FullPath + "/" + nodesPath[i]);
					currentNode.childNodes.Add(childNode.Name, childNode);
				}
				currentNode = childNode;
			}

			if (provider is ISiteProvider site)
				currentNode.fileMap.Add(nodesPath[nodesPath.Length - 1], site);
			else if (provider is IFolderProvider folder)
				currentNode.childFolder.Add(folder);
			else
				throw new InvalidOperationException();
		}

		// public void Map(string target, FileInfo directory){ } // => ISiteProvider
		// public void Map(string target, DirectoryInfo directory){ } // => IFolderProvider

		public ISiteProvider TryGetSite(Uri path)
		{
			var currentNode = RootNode;
			var parts = path.AbsolutePath.Split('/');
			int slashIndex = 0;

			for (int i = 0; i < parts.Length; i++)
			{
				var part = parts[i];

				var slashSubPart = path.AbsolutePath.Substring(slashIndex);
				var site = currentNode.TryGetSite(path.AbsolutePath);
				if (site != null)
					return site;

				if (!currentNode.childNodes.TryGetValue(part, out currentNode))
					return null;
				slashIndex += part.Length + 1;
			}

			return null;
		}

		private class MapNode
		{
			public List<IFolderProvider> childFolder;
			public Dictionary<string, ISiteProvider> fileMap;
			public Dictionary<string, MapNode> childNodes;
			public string Name { get; }
			public string FullPath { get; }

			public MapNode(string name, string fullPath)
			{
				Name = name;
				FullPath = fullPath;
				Util.Init(ref childFolder);
				Util.Init(ref fileMap);
				Util.Init(ref childNodes);
			}

			public ISiteProvider TryGetSite(string name)
			{
				var unmappedName = name.Substring(FullPath.Length);
				if (fileMap.TryGetValue(unmappedName, out var site))
					return site;
				foreach (var folder in childFolder)
				{
					site = folder.GetFile(unmappedName);
					if (site != null)
						return site;
				}
				return null;
			}
		}
	}
}
