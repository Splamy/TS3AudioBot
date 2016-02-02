namespace TS3AudioBot.Algorithm
{
	using System;
	using System.Globalization;
	using System.Text;

	public class Trie<T> : ICommandFilter<T> where T : class
	{
		protected const int CharsInAlphabet = 26;
		private TrieNode root = null;
		protected TrieNode Root
		{
			get
			{
				if (root == null)
				{
					root = GetNewTrieNode('+');
					root.Unique = false;
				}
				return root;
			}
		}

		public Trie() { }

		public virtual void Add(string key, T value)
		{
			if (string.IsNullOrEmpty(key))
				throw new ArgumentNullException(nameof(key));

			TrieNode current = Root;
			foreach (char c in key)
			{
				int index = ValidateChar(c);
				if (current.children == null)
				{
					current.children = new TrieNode[CharsInAlphabet];
				}
				else
				{
					if (!current.HasData)
						current.Data = null;
					current.Unique = false;
				}
				TrieNode child = current.children[index];
				if (child == null)
				{
					child = GetNewTrieNode(c);
					child.Data = value;
					current.children[index] = child;
				}
				current = child;
			}
			current.Data = value;
			current.HasData = true;
		}

		protected virtual TrieNode GetNewTrieNode(char index)
		{
			return new TrieNode(index);
		}

		public virtual bool TryGetValue(string key, out T value)
		{
			TrieNode node = TryGetNode(key);
			if (node != null && (node.HasData || node.Unique))
			{
				value = node.Data;
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		}

		protected TrieNode TryGetNode(string key)
		{
			if (key == null)
				throw new ArgumentNullException(nameof(key));

			key = key.ToLower(CultureInfo.InvariantCulture);
			TrieNode current = Root;
			foreach (char c in key)
			{
				int index = ValidateChar(c);
				if (index == -1 || current.children == null || (current = current.children[index]) == null)
					return null;
				if (current.Unique) break;
			}
			return current;
		}

		protected virtual int ValidateChar(char c)
		{
			if (c >= 'a' && c <= 'z')
				return c - 'a';
			return -1;
		}

		public override string ToString()
		{
			StringBuilder strb = new StringBuilder();
			ToStringGen(Root, strb);
			return strb.ToString();
		}

		private void ToStringGen(TrieNode tn, StringBuilder strb)
		{
			strb.Append(tn.CharId);
			if (tn.HasData)
			{
				strb.Append("[");
				strb.Append(tn.Data);
				strb.Append("]");
			}
			else if (tn.Unique)
			{
				strb.Append("*");
			}
			if (tn.children != null)
			{
				strb.Append("(");
				foreach (var tnc in tn.children)
				{
					if (tnc != null)
						ToStringGen(tnc, strb);
				}
				strb.Append(")");
			}
		}

		protected class TrieNode
		{
			public T Data { get; set; }
			public TrieNode[] children { get; set; }
			public char CharId { get; }
			public bool HasData { get; set; }
			public bool Unique { get; set; }

			public TrieNode(char charId)
			{
				CharId = charId;
				Data = null;
				HasData = false;
				Unique = true;
				children = null;
			}
		}
	}
}
