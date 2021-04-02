using System;
using System.Linq;
using System.Threading.Tasks;

namespace TSLib.Helper
{
	// Normal EventHandler:
	// public delegate void EventHandler(object? sender, EventArgs e);

	public delegate Task AsyncEventHandler(object? sender, EventArgs e);
	public delegate Task AsyncEventHandler<T>(object? sender, T value);

	public static class AsyncEventExtensions
	{
		public static Task InvokeAsync<T>(this AsyncEventHandler<T>? ev, object? sender, T value)
		{
			if (ev == null)
				return Task.CompletedTask;

			var invList = ev.GetInvocationList();
			if (invList.Length == 1)
			{
				return ((AsyncEventHandler<T>)invList[0]).Invoke(sender, value);
			}

			return Task.WhenAll(invList.Select(func => ((AsyncEventHandler<T>)func).Invoke(sender, value)));
		}

		public static Task InvokeAsync(this AsyncEventHandler? ev, object? sender)
			=> InvokeAsync(ev, sender, EventArgs.Empty);
		public static Task InvokeAsync(this AsyncEventHandler? ev, object? sender, EventArgs e)
		{
			if (ev == null)
				return Task.CompletedTask;

			var invList = ev.GetInvocationList();
			if (invList.Length == 1)
			{
				return ((AsyncEventHandler)invList[0]).Invoke(sender, e);
			}

			return Task.WhenAll(invList.Select(func => ((AsyncEventHandler)func).Invoke(sender, e)));
		}
	}
}
