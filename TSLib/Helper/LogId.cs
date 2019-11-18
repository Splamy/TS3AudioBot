// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;

namespace TSLib.Helper
{
	public readonly struct Id : IEquatable<Id>
	{
		public static readonly Id Null = new Id(null);

		public int Value { get; }

		private Id(object _)
		{
			Value = -1;
		}

		public Id(int id)
		{
			Value = id;
		}

		public static implicit operator int(Id id) => id.Value;

		public override string ToString() => Value.ToString();

		public override bool Equals(object obj) => obj is Id id && Equals(id);
		public bool Equals(Id other) => Value == other.Value;
		public override int GetHashCode() => Value;

		public static bool operator ==(Id id, Id other) => id.Value == other.Value;
		public static bool operator !=(Id id, Id other) => id.Value != other.Value;
	}
}
