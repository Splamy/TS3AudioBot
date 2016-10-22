// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2016  TS3AudioBot contributors
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

namespace TS3Client
{
	using System;
	using System.Globalization;

	public interface IParameterConverter
	{
		string QueryValue { get; }
	}

	public class PrimitiveParameter : IParameterConverter
	{
		public string QueryValue { get; }
		public static readonly DateTime UnixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public PrimitiveParameter(bool value) { QueryValue = (value ? "1" : "0"); }
		public PrimitiveParameter(sbyte value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(byte value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(short value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(ushort value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(int value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(uint value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(long value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(ulong value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(float value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(double value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public PrimitiveParameter(string value) { QueryValue = TS3String.Escape(value); }
		public PrimitiveParameter(TimeSpan value) { QueryValue = value.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture); }
		public PrimitiveParameter(DateTime value) { QueryValue = (value - UnixTimeStart).TotalSeconds.ToString("F0", CultureInfo.InvariantCulture); }

		public static implicit operator PrimitiveParameter(bool value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(sbyte value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(byte value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(short value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(ushort value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(int value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(uint value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(long value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(ulong value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(float value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(double value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(string value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(TimeSpan value) => new PrimitiveParameter(value);
		public static implicit operator PrimitiveParameter(DateTime value) => new PrimitiveParameter(value);
	}
}
