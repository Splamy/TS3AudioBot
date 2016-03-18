namespace TS3AudioBot.CommandSystem
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	public class OverloadedFunctionCommand : ICommand
	{
		/// <summary>
		/// The order of types, the first item has the highest priority, items not in the list have lower priority.
		/// </summary>
		static Type[] typeOrder = { typeof(int), typeof(float), typeof(double), typeof(string) };

		readonly List<FunctionCommand> functions;

		public OverloadedFunctionCommand(IEnumerable<FunctionCommand> functionsArg)
		{
			functions = functionsArg.ToList();
			functions.Sort((f1, f2) =>
			{
				// The first function in the list should be the most specialiced.
				// If the execute the command we will iterate through the list from the beginning
				// and choose the first matching function.

				// Sort out special arguments
				// and remove the nullable wrapper
				List<Type> params1 = (from p in f1.CommandParameter
									  where !FunctionCommand.SpecialTypes.Contains(p)
									  select FunctionCommand.UnwrapType(p)).ToList();

				List<Type> params2 = (from p in f2.CommandParameter
									  where !FunctionCommand.SpecialTypes.Contains(p)
									  select FunctionCommand.UnwrapType(p)).ToList();

				for (int i = 0; i < params1.Count; i++)
				{
					// Prefer functions with higher parameter count
					if (i >= params2.Count)
						return -1;
					int i1 = Array.IndexOf(typeOrder, params1[i]);
					if (i1 == -1)
						i1 = typeOrder.Length;
					int i2 = Array.IndexOf(typeOrder, params2[i]);
					if (i2 == -1)
						i2 = typeOrder.Length;
					// Prefer lower argument
					if (i1 < i2)
						return -1;
					if (i1 > i2)
						return 1;
				}
				if (params2.Count > params1.Count)
					return 1;

				return 0;
			});
		}

		public override ICommandResult Execute(ExecutionInformation info, IEnumerable<ICommand> arguments, IEnumerable<CommandResultType> returnTypes)
		{
			// Make arguments lazy, we only want to execute them once
			arguments = arguments.Select(c => new LazyCommand(c));
			foreach (FunctionCommand f in functions)
			{
				try
				{
					// Try to call this overload
					return f.Execute(info, arguments, returnTypes);
				}
				catch (CommandException)
				{
					// Do nothing, just move on to the next function
				}
			}
			throw new CommandException("No matching function could be found");
		}
	}
}
