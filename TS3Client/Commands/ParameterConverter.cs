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

namespace TS3Client.Commands
{
	using System;
	using System.Globalization;

	public struct ParameterConverter
	{
		public string QueryValue { get; }

		public ParameterConverter(bool value) { QueryValue = (value ? "1" : "0"); }
		public ParameterConverter(sbyte value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(byte value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(short value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(ushort value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(int value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(uint value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(long value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(ulong value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(float value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(double value) { QueryValue = value.ToString(CultureInfo.InvariantCulture); }
		public ParameterConverter(string value) { QueryValue = Ts3String.Escape(value); }
		public ParameterConverter(TimeSpan value) { QueryValue = value.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture); }
		public ParameterConverter(DateTime value) { QueryValue = (value - Util.UnixTimeStart).TotalSeconds.ToString("F0", CultureInfo.InvariantCulture); }

		public static implicit operator ParameterConverter(bool value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(sbyte value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(byte value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(short value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(ushort value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(int value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(uint value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(long value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(ulong value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(float value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(double value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(string value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(TimeSpan value) => new ParameterConverter(value);
		public static implicit operator ParameterConverter(DateTime value) => new ParameterConverter(value);
	}
}
