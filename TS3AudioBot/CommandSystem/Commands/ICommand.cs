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

namespace TS3AudioBot.CommandSystem.Commands
{
	public interface ICommand
	{
		/// <summary>Execute this command.</summary>
		/// <param name="info">All global informations for this execution.</param>
		/// <param name="arguments">
		/// The arguments for this command.
		/// They are evaluated lazy which means they will only be evaluated if needed.
		/// </param>
		/// <param name="returnTypes">
		/// The possible return types that should be returned by this execution.
		/// They are ordered by priority so, if possible, the first return type should be picked, then the second and so on.
		///
		/// These types can contain primitive types, the actual return value will then be wrapped into a <see cref="CommandResults.IPrimitiveResult{T}"/>.
		/// null inside the list allows an empty result.
		/// </param>
		/// <returns>
		/// The result of this command.
		///
		/// null is an empty result.
		/// Primitive types are a special case, it should always implement <see cref="CommandResults.IPrimitiveResult{T}"/>, e.g. through the <see cref="CommandResults.PrimitiveResult{T}"/> class.
		/// The complete list of primitive types is <see cref="XCommandSystem.BasicTypes"/>.
		/// </returns>
		object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes);
	}
}
