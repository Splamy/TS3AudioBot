// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Reflection;

namespace TS3AudioBot.CommandSystem.CommandResults
{
	public class Pick<T> : IWrappedResult
	{
		private readonly string pickPath;
		private readonly T baseObj;
		private bool isPicked;
		private object? pickedValue;

		public object? Content
		{
			get
			{
				if (!isPicked)
				{
					isPicked = true;
					pickedValue = null;
					pickedValue = DoPick();
				}
				return pickedValue;
			}
		}

		public Pick(T obj, string pickPath)
		{
			baseObj = obj;
			this.pickPath = pickPath;
		}

		private object? DoPick()
		{
			if (baseObj == null)
				return null; // TODO maybe error ?
			if (string.IsNullOrEmpty(pickPath))
				return baseObj;
			var type = baseObj.GetType();
			var prop = type.GetProperty(pickPath, BindingFlags.Public | BindingFlags.Instance);
			if (prop is null)
				throw new CommandException("Property not found" /* TODO LOC */);
			return prop.GetValue(baseObj);
		}
	}
}
