namespace TS3Query
{
	using System;

	[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
	sealed class QueryStringAttribute : Attribute
	{
		public string QueryString { get; }

		public QueryStringAttribute(string queryString)
		{
			QueryString = queryString;
		}
	}
}
