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
	using Helper;
	using System;
	using System.Collections.Generic;

	public class SiteMapper
	{
		private readonly MapNode rootNode = new MapNode("", "");

		public void Map(string target, IProvider provider)
		{
			if (string.IsNullOrEmpty(target)) throw new ArgumentNullException(nameof(target));
			if (provider is null) throw new ArgumentNullException(nameof(provider));

			var nodesPath = target.Split('/');
			var currentNode = rootNode;
			for (int i = 0; i < nodesPath.Length - 1; i++)
			{
				if (!currentNode.childNodes.TryGetValue(nodesPath[i], out var childNode))
				{
					childNode = new MapNode(nodesPath[i], currentNode.FullPath + "/" + nodesPath[i]);
					currentNode.childNodes.Add(childNode.Name, childNode);
				}
				currentNode = childNode;
			}

			switch (provider)
			{
				case ISiteProvider site:
					currentNode.fileMap.Add(nodesPath[nodesPath.Length - 1], site);
					break;
				case IFolderProvider folder:
					currentNode.childFolder.Add(folder);
					break;
				default:
					throw new InvalidOperationException($"Unknown web provider type: {provider.GetType()}");
			}
		}

		// public void Map(string target, FileInfo directory){ } // => ISiteProvider
		// public void Map(string target, DirectoryInfo directory){ } // => IFolderProvider

		public ISiteProvider TryGetSite(Uri path)
		{
			var currentNode = rootNode;
			var parts = path.AbsolutePath.Split('/');

			foreach (var part in parts)
			{
				var site = currentNode.TryGetSite(path.AbsolutePath);
				if (site != null)
					return site;

				if (!currentNode.childNodes.TryGetValue(part, out currentNode))
					return null;
			}

			return null;
		}

		private class MapNode
		{
			public readonly List<IFolderProvider> childFolder;
			public readonly Dictionary<string, ISiteProvider> fileMap;
			public readonly Dictionary<string, MapNode> childNodes;
			public string Name { get; }
			public string FullPath { get; }

			public MapNode(string name, string fullPath)
			{
				Name = name;
				FullPath = fullPath;
				Util.Init(out childFolder);
				Util.Init(out fileMap);
				Util.Init(out childNodes);
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
