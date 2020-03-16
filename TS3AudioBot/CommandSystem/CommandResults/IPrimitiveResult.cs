// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.CommandSystem.CommandResults
{
	/// <summary>
	/// A result which can safely used as a primitive like a string.
	/// 
	/// The complete list of primitive types is <see cref="XCommandSystem.BasicTypes"/>.
	/// </summary>
	public interface IPrimitiveResult<T> : IPrimitiveResult
	{
		new T Get();
	}

	public interface IPrimitiveResult
	{
		object Get();
	}
}
