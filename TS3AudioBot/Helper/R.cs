namespace TS3AudioBot.Helper
{
	/// <summary>
	/// The R result wrapper.
	/// The functionality is quite similar to the optional-patter.
	/// It either represents success or an error + message
	/// </summary>
	public struct R
	{
		public static readonly R OkR = new R();

		// using default false bool so Ok is true on default
		private bool isError;
		public bool Ok => !isError;
		public string Message { get; }

		public R(string message) { isError = true; Message = message; }

		public static implicit operator bool(R result) => result.Ok;
		public static implicit operator string(R result) => result.Message;

		public static implicit operator R(string message) => new R(message);
	}

	/// <summary>
	/// The R&lt;T&gt; result wrapper.
	/// The functionality is quite similar to the optional-patter.
	/// It either represents success + value or an error + message
	/// </summary>
	public struct R<T>
	{
		private bool isError;
		public bool Ok => !isError;
		public string Message { get; }
		public T Result { get; }

		public R(T result) { isError = false; Message = null; Result = result; }
		public R(string message) { isError = true; Message = message; Result = default(T); }

		public static implicit operator bool(R<T> result) => result.Ok;
		public static implicit operator string(R<T> result) => result.Message;

		public static implicit operator R<T>(T result) => new R<T>(result);
		public static implicit operator R<T>(string message) => new R<T>(message);
	}
}
