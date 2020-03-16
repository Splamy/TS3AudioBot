// TSLib - A free TeamSpeak 3 and 5 client library
// Copyright (C) 2017  TSLib contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System;
using System.Collections.Generic;
using System.Linq;

namespace TSLib.Messages
{
	public partial class CommandError
	{
		public static CommandError TimeOut { get; } = Custom("Connection closed");

		public static CommandError NoResult { get; } = Custom("Result is empty");

		public static CommandError Parser { get; } = Custom("Result could not be parsed");

		public static CommandError Custom(string message) => new CommandError { Id = TsErrorCode.custom_error, Message = message };

		public string ErrorFormat()
		{
			if (MissingPermissionId != TsPermission.unknown && MissingPermissionId != TsPermission.undefined)
				return $"{Id}: the command failed to execute: {Message} (missing permission:{MissingPermissionId})";
			else
				return $"{Id}: the command failed to execute: {Message}";
		}
	}

	/// <summary>Provides useful extension methods for error formatting.</summary>
	public static class CommandErrorExtensions
	{
		public static R<T, CommandError> WrapSingle<T>(in this R<T[], CommandError> result) where T : IMessage
		{
			if (result.Ok)
				return WrapSingle(result.Value);
			return R<T, CommandError>.Err(result.Error);
		}

		internal static R<T, CommandError> WrapSingle<T>(this IEnumerable<T> enu) where T : IMessage
		{
			var first = enu.FirstOrDefault();
			if (first != null)
				return R<T, CommandError>.OkR(first);
			return R<T, CommandError>.Err(CommandError.NoResult);
		}

		public static R<T[], CommandError> UnwrapNotification<T>(in this R<LazyNotification, CommandError> result) where T : class, IMessage
		{
			if (!result.Ok)
				return result.Error;
			return R<T[], CommandError>.OkR((T[])result.Value.Notifications);
		}

		public static R<TI, CommandError> WrapInterface<TC, TI>(in this R<TC, CommandError> result) where TC : class, IMessage, TI
		{
			if (!result.Ok)
				return result.Error;
			return result.Value;
		}
	}
}
