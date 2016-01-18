namespace TS3AudioBot.Algorithm
{
	interface ICommandFilter<T> where T : class
	{
		void Add(string key, T value);
		bool TryGetValue(string key, out T value);
	}
}
