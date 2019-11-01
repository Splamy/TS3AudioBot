// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TS3AudioBot.CommandSystem.CommandResults
{
	public static class ResultHelper
	{
		public static bool IsValidResult(object result, IReadOnlyList<Type> returnTypes)
		{
			if (result == null)
				return returnTypes.Contains(null);

			return IsValidResultType(result.GetType(), returnTypes);
		}

		public static bool IsValidResultType(Type resultType, IReadOnlyList<Type> returnTypes)
		{
			return returnTypes.Any(t =>
			{
				if (t == null)
					return false;
				if (XCommandSystem.BasicTypes.Contains(t))
				{
					var genType = typeof(IPrimitiveResult<>).MakeGenericType(t);
					return genType.IsAssignableFrom(resultType);
				}
				else
					return t.IsAssignableFrom(resultType);
			});
		}

		/// <summary>
		/// Automaticall wrapes primitive results in <see cref="PrimitiveResult{T}"/>.
		/// Otherwise returns the result.
		/// </summary>
		/// <returns>The valid result.</returns>
		/// <param name="resultType">The type for the result.</param>
		/// <param name="result">The result value.</param>
		public static object ToResult(Type resultType, object result)
		{
			if (XCommandSystem.BasicTypes.Contains(resultType))
			{
				var genType = typeof(PrimitiveResult<>).MakeGenericType(resultType);
				return Activator.CreateInstance(genType, new object[] { result });
			}
			else
				return result;
		}
	}
}
