using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS3AudioBot.Helper
{
	internal class AsyncLazy<T>
	{
		protected T Result;
		protected Func<Task<T>> LazyMethod;
		public bool Evaluated { get; protected set; }

		private AsyncLazy(Func<Task<T>> method)
		{
			LazyMethod = method;
		}

		public static AsyncLazy<T> CreateAsyncLazy(Func<Task<T>> method)
		{
			return new AsyncLazy<T>(method);
		}

		public static AsyncLazy<T> CreateAsyncLazy<TIn1>(Func<TIn1, Task<T>> method, TIn1 param1)
		{
			return new AsyncLazy<T>(() => method(param1));
		}

		public async Task<T> GetValue()
		{
			if (Evaluated)
			{
				return Result;
			}
			else
			{
				Result = await LazyMethod();
				Evaluated = true;
				return Result;
			}
		}
	}
}
