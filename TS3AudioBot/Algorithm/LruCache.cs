// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Collections.Generic;

namespace TS3AudioBot.Algorithm
{
	public class LruCache<TK, TV>
	{
		private readonly int maxCapacity;
		private readonly Dictionary<TK, LinkedListNode<(TK key, TV value)>> cacheDict = new Dictionary<TK, LinkedListNode<(TK, TV)>>();
		private readonly LinkedList<(TK key, TV value)> lruList = new LinkedList<(TK, TV)>();

		public LruCache(int capacity)
		{
			maxCapacity = capacity;
		}

		public bool TryGetValue(TK key, out TV value)
		{
			if (cacheDict.TryGetValue(key, out var node))
			{
				Renew(node);
				value = node.Value.value;
				return true;
			}
			value = default;
			return false;
		}

		public void Set(TK key, TV value)
		{
			if (cacheDict.TryGetValue(key, out var node))
			{
				Renew(node);
				node.Value = (node.Value.key, value);
				return;
			}

			if (cacheDict.Count >= maxCapacity)
				RemoveOldest();

			node = lruList.AddLast((key, value));
			cacheDict.Add(key, node);
		}

		public void Remove(TK key) => cacheDict.Remove(key);

		private void Renew(LinkedListNode<(TK, TV)> node)
		{
			lruList.Remove(node);
			lruList.AddLast(node);
		}

		private void RemoveOldest()
		{
			var node = lruList.First;
			lruList.RemoveFirst();
			cacheDict.Remove(node.Value.key);
		}

		public void Clear()
		{
			cacheDict.Clear();
			lruList.Clear();
		}
	}
}
