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
using TS3AudioBot.CommandSystem.Ast;
using TS3AudioBot.CommandSystem.CommandResults;
using TS3AudioBot.CommandSystem.Commands;
using TS3AudioBot.CommandSystem.Text;
using TS3AudioBot.Web.Api;
using TSLib.Helper;

namespace TS3AudioBot.CommandSystem
{
	public class XCommandSystem
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

		public RootCommand RootCommand { get; }

		public XCommandSystem()
		{
			RootCommand = new RootCommand();
		}

		internal ICommand AstToCommandResult(AstNode node)
		{
			switch (node.Type)
			{
			case AstType.Error:
				throw new CommandException("Found an unconvertable ASTNode of type Error", CommandExceptionReason.InternalError);
			case AstType.Command:
				var cmd = (AstCommand)node;
				var arguments = new ICommand[cmd.Parameter.Count];
				int tailCandidates = 0;
				for (int i = cmd.Parameter.Count - 1; i >= 1; i--)
				{
					var para = cmd.Parameter[i];
					if (!(para is AstValue astVal) || astVal.StringType != StringType.FreeString)
						break;

					arguments[i] = new TailStringAutoConvertCommand(new TailString(astVal.Value, astVal.TailString));
					tailCandidates++;
				}
				for (int i = 0; i < cmd.Parameter.Count - tailCandidates; i++)
					arguments[i] = AstToCommandResult(cmd.Parameter[i]);
				return new AppliedCommand(RootCommand, arguments);
			case AstType.Value:
				var astNode = (AstValue)node;
				// Quoted strings are always strings, the rest gets automatically converted
				if (astNode.StringType == StringType.FreeString)
					return new AutoConvertResultCommand(astNode.Value);
				else
					return new ResultCommand(new PrimitiveResult<string>(astNode.Value));
			default:
				throw Tools.UnhandledDefault(node.Type);
			}
		}

		public object Execute(ExecutionInformation info, string command, IReadOnlyList<Type> returnTypes)
		{
			var ast = CommandParser.ParseCommandRequest(command);
			var cmd = AstToCommandResult(ast);
			return cmd.Execute(info, Array.Empty<ICommand>(), returnTypes);
		}

		public object Execute(ExecutionInformation info, IReadOnlyList<ICommand> arguments, IReadOnlyList<Type> returnTypes)
			=> RootCommand.Execute(info, arguments, returnTypes);

		public string ExecuteCommand(ExecutionInformation info, string command)
			=> CastResult(Execute(info, command, ReturnStringOrNothing));

		public string ExecuteCommand(ExecutionInformation info, IReadOnlyList<ICommand> arguments)
			=> CastResult(Execute(info, arguments, ReturnStringOrNothing));

		private static string CastResult(object result)
		{
			if (result is IPrimitiveResult<string> s)
				return s.Get();
			if (result == null)
				return null;
			throw new CommandException("Expected a string or nothing as result", CommandExceptionReason.NoReturnMatch);
		}

		public static object GetEmpty(IReadOnlyList<Type> resultTypes)
		{
			foreach (var item in resultTypes)
			{
				if (item == null)
					return null;
				else if (item == typeof(string))
					return string.Empty;
			}
			throw new CommandException("No empty return type available", CommandExceptionReason.NoReturnMatch);
		}

		public static string GetTree(ICommand com)
		{
			var strb = new TextModBuilder();
			GetTree(com, strb, 0);
			return strb.ToString();
		}

		private static void GetTree(ICommand com, TextModBuilder strb, int indent)
		{
			switch (com)
			{
			case CommandGroup group:
				strb.AppendFormat("<group>\n".Mod().Color(Color.Red));
				foreach (var subCom in group.Commands)
				{
					strb.Append(new string(' ', (indent + 1) * 2)).Append(subCom.Key);
					GetTree(subCom.Value, strb, indent + 1);
				}
				break;

			case FunctionCommand _:
				strb.AppendFormat("<func>\n".Mod().Color(Color.Green));
				break;

			case OverloadedFunctionCommand ofunc:
				strb.AppendFormat($"<overload({ofunc.Functions.Count})>\n".Mod().Color(Color.Blue));
				break;

			case AliasCommand _:
				strb.AppendFormat($"<alias>\n".Mod().Color(Color.Yellow));
				break;

			default:
				strb.AppendFormat("\n");
				break;
			}
		}
	}
}
