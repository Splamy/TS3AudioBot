// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Dependency
{
	using System;
	using System.Text;

	internal class Module
	{
		public InitState Status { get; private set; }
		public object Obj { get; }
		public Type Type => Obj.GetType();
		// object SyncContext;
		private readonly Action<object> initializer;

		public Module(object obj, Action<object> initializer)
		{
			Status = initializer is null ? InitState.SetOnly : InitState.SetAndInit;
			this.initializer = initializer;
			Obj = obj;
		}

		public void SetInitalized()
		{
			if (Status == InitState.Initializing)
				return;
			if (Status == InitState.SetAndInit)
			{
				Status = InitState.Initializing;
				initializer?.Invoke(Obj);
			}
			Status = InitState.Done;
		}

		public override string ToString()
		{
			var strb = new StringBuilder();
			strb.Append(Type.Name);
			switch (Status)
			{
			case InitState.Done: strb.Append("+"); break;
			case InitState.SetOnly: strb.Append("*"); break;
			case InitState.SetAndInit: strb.Append("-"); break;
			case InitState.Initializing: strb.Append("-i"); break;
			default: throw new ArgumentOutOfRangeException();
			}
			return strb.ToString();
		}
	}

	internal enum InitState
	{
		Done,
		SetOnly,
		SetAndInit,
		Initializing,
	}
}
