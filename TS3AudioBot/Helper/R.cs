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

		private R(string message) { isError = true; Message = message; }
		/// <summary>Creates a new failed result with a message</summary>
		/// <param name="message">The message</param>
		public static R Err(string message) => new R(message);

		public static implicit operator bool(R result) => result.Ok;
		public static implicit operator string(R result) => result.Message;

		public static implicit operator R(string message) => new R(message);

		public override string ToString() => Message;
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
		public T Value { get; }

		private R(T value) { isError = false; Message = null; Value = value; }
		private R(string message) { isError = true; Message = message; Value = default(T); }

		/// <summary>Creates a new failed result with a message</summary>
		/// <param name="message">The message</param>
		public static R<T> Err(string message) => new R<T>(message);
		/// <summary>Creates a new successful result with a value</summary>
		/// <param name="value">The value</param>
		public static R<T> OkR(T value) => new R<T>(value);

		public static implicit operator bool(R<T> result) => result.Ok;
		public static implicit operator string(R<T> result) => result.Message;

		public static implicit operator R<T>(T result) => new R<T>(result);
		public static implicit operator R<T>(string message) => new R<T>(message);

		public override string ToString() => Message;
	}
}
