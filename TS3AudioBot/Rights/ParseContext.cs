// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Rights
{
	using System.Collections.Generic;
	using System.Linq;

	internal class ParseContext
	{
		public List<RightsDecl> Declarations { get; }
		public RightsGroup[] Groups { get; private set; }
		public RightsRule[] Rules { get; private set; }
		public List<string> Errors { get; }
		public List<string> Warnings { get; }

		public ParseContext()
		{
			Declarations = new List<RightsDecl>();
			Errors = new List<string>();
			Warnings = new List<string>();
		}

		public void SplitDeclarations()
		{
			Groups = Declarations.OfType<RightsGroup>().ToArray();
			Rules = Declarations.OfType<RightsRule>().ToArray();
		}
	}
}
