namespace TS3Query
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	static class Extensions
	{
		public static string GetQueryString(this Enum valueEnum)
		{
			if (valueEnum == null) throw new ArgumentNullException(nameof(valueEnum));

			Type enumType = valueEnum.GetType();
			var valueField = enumType.GetField(Enum.GetName(enumType, valueEnum));
			var fieldAttributes = valueField.GetCustomAttributes(typeof(QueryStringAttribute), false);
			var fieldAttribute = fieldAttributes.Cast<QueryStringAttribute>().FirstOrDefault();
			if (fieldAttribute == null)
				throw new InvalidOperationException("This enum doesn't contain the QueryString attribute");
			return fieldAttribute.QueryString;
		}

		public static IEnumerable<Enum> GetFlags(this Enum input) => Enum.GetValues(input.GetType()).Cast<Enum>().Where(enu => input.HasFlag(enu));
	}
}
