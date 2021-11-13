// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using TS3AudioBot.Dependency;

namespace TS3AudioBot.CommandSystem;

public class ExecutionInformation : ChainedInjector<BasicInjector>
{
	public ExecutionInformation() : this(NullInjector.Instance) { }
	public ExecutionInformation(IInjector parent) : base(parent, new BasicInjector())
	{
		this.AddModule(this);
	}
}
