using System;
using System.Text;
using System.Collections.Generic;

namespace TS3AudioBot.Helper
{
	public class Trie<T> where T : class
	{
		protected const int CHARS_IN_ALPHABET = 26;
		protected TrieNode root;

		public Trie()
		{
			root = GetNewTrieNode(null, '+');
			root.unique = false;
		}

		public virtual void Add(string key, T value)
		{
			TrieNode current = root;
			foreach (char c in key)
			{
				int index = ValidateChar(c);
				if (current.children == null)
				{
					current.children = new TrieNode[CHARS_IN_ALPHABET];
				}
				else
				{
					if (!current.hasData)
						current.Data = null;
					current.unique = false;
				}
				TrieNode child = current.children[index];
				if (child == null)
				{
					child = GetNewTrieNode(current, c);
					child.Data = value;
					current.children[index] = child;
				}
				current = child;
			}
			current.Data = value;
			current.hasData = true;
		}

		protected virtual TrieNode GetNewTrieNode(TrieNode parent, char index)
		{
			return new TrieNode(parent, index);
		}

		public virtual bool TryGetValue(string key, out T value)
		{
			TrieNode node = TryGetNode(key);
			if (node != null && (node.hasData || node.unique))
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
			key = key.ToLower();
			TrieNode current = root;
			foreach (char c in key)
			{
				int index = ValidateChar(c);
				if (index == -1 || current.children == null || (current = current.children[index]) == null)
					return null;
				if (current.unique) break;
			}
			return current;
		}

		protected static int ValidateChar(char c)
		{
			if (c >= 'a' && c <= 'z')
				return c - 'a';
			return -1;
		}

		public override string ToString()
		{
			StringBuilder strb = new StringBuilder();
			ToStringGen(root, strb);
			return strb.ToString();
		}

		protected void ToStringGen(TrieNode tn, StringBuilder strb)
		{
			strb.Append(tn.charId);
			if (tn.hasData)
			{
				strb.Append("[");
				strb.Append(tn.Data);
				strb.Append("]");
			}
			else if (tn.unique)
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
			readonly public TrieNode parent;
			public TrieNode[] children;
			readonly public char charId;
			public bool hasData;
			public bool unique;

			public TrieNode(TrieNode parent, char charId)
			{
				this.charId = charId;
				this.parent = parent;
				Data = null;
				hasData = false;
				unique = true;
				children = null;
			}
		}
	}

	public class MulTrie<T> : Trie<T> where T : class
	{
		public override void Add(string key, T value)
		{
			TrieNode node = TryGetNode(key);
			if (node != null && node.hasData) // key exists already
				return; // maby update key

			MulNode mulRoot = (MulNode)root;

			HashSet<MulNode> useNodes = new HashSet<MulNode>();
			//useNodes.Add(mulRoot);
			for (int i = 0; i < key.Length; i++)
			{
				string subKey = key.Substring(i);
				MulNode current = mulRoot;
				foreach (char c in subKey)
				{
					int index = ValidateChar(c);
					if (current.children == null)
					{
						current.children = new TrieNode[CHARS_IN_ALPHABET];
					}
					MulNode child = (MulNode)current.children[index];
					if (child == null)
					{
						child = (MulNode)GetNewTrieNode(current, c);
						current.children[index] = child;
						useNodes.Add(child);
					}
					else
					{
						if (!useNodes.Contains(child))
							useNodes.Add(child);
					}
					current = child;
				}
				if (i == 0)
				{
					current.Data = value;
					current.hasData = true;
				}
			}
			foreach (var useNode in useNodes)
			{
				useNode.subReferences.Add(value);
			}
		}

		protected override TrieNode GetNewTrieNode(TrieNode parent, char index)
		{
			return new MulNode(parent, index);
		}

		public override bool TryGetValue(string key, out T value)
		{
			MulNode node = (MulNode)TryGetNode(key);
			if (node != null)
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

		public IList<T> GetValues(string key)
		{
			MulNode node = (MulNode)TryGetNode(key);
			if (node != null)
				return node.subReferences.AsReadOnly();
			else
				return null;
		}

		protected class MulNode : TrieNode
		{
			public List<T> subReferences;

			public MulNode(TrieNode parent, char index) : base(parent, index)
			{
				subReferences = new List<T>();
				unique = false;
			}
		}
	}
}
