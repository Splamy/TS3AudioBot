// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.Web.Api;

namespace TS3AudioBot.CommandSystem
{
	public static class CommandSystemTypes
	{
		public static readonly Type[] ReturnJson = { typeof(JsonObject) };
		public static readonly Type[] ReturnJsonOrDataOrNothing = { typeof(JsonObject), typeof(DataStream), null };
		public static readonly Type[] ReturnString = { typeof(string) };
		public static readonly Type[] ReturnStringOrNothing = { typeof(string), null };
		public static readonly Type[] ReturnCommandOrString = { typeof(ICommand), typeof(string) };
		public static readonly Type[] ReturnAnyPreferNothing = { null, typeof(string), typeof(JsonObject), typeof(ICommand) };

		/// <summary>
		/// The order of types, the first item has the highest priority,
		/// items not in the list have higher priority as they are special types.
		/// </summary>
		public static readonly Type[] TypeOrder = {
			typeof(bool),
			typeof(sbyte), typeof(byte),
			typeof(short), typeof(ushort),
			typeof(int), typeof(uint),
			typeof(long), typeof(ulong),
			typeof(float), typeof(double),
			typeof(TimeSpan), typeof(DateTime),
			typeof(string) };
		public static readonly HashSet<Type> BasicTypes = new HashSet<Type>(TypeOrder);

		public static readonly HashSet<Type> AdvancedTypes = new HashSet<Type>(new Type[] {
			typeof(IAudioResourceResult),
			typeof(System.Collections.IEnumerable),
			typeof(ResourceFactories.AudioResource),
			typeof(History.AudioLogEntry),
			typeof(Playlists.PlaylistItem),
		});
	}
}
