using System.Text;

namespace TS3AudioBot.Algorithm
{
	public class Trie<T> where T : class
	{
		protected const int CHARS_IN_ALPHABET = 26;
		protected TrieNode root;

		public Trie()
		{
			root = GetNewTrieNode('+');
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
					child = GetNewTrieNode(c);
					child.Data = value;
					current.children[index] = child;
				}
				current = child;
			}
			current.Data = value;
			current.hasData = true;
		}

		protected virtual TrieNode GetNewTrieNode(char index)
		{
			return new TrieNode(index);
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

		protected virtual int ValidateChar(char c)
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
			public TrieNode[] children;
			readonly public char charId;
			public bool hasData;
			public bool unique;

			public TrieNode(char charId)
			{
				this.charId = charId;
				Data = null;
				hasData = false;
				unique = true;
				children = null;
			}
		}
	}
}
