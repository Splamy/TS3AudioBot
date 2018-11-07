// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Helper
{
	using CommandSystem;
	using Localization;
	using Newtonsoft.Json;
	using Newtonsoft.Json.Linq;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;

	public static class Util
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		public const RegexOptions DefaultRegexConfig = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ECMAScript;

		private static readonly Regex SafeFileNameMatcher = new Regex(@"^[\w-]+$", DefaultRegexConfig);

		/// <summary>Blocks the thread while the predicate returns false or until the timeout runs out.</summary>
		/// <param name="predicate">Check function that will be called every millisecond.</param>
		/// <param name="timeout">Timeout in milliseconds.</param>
		public static void WaitOrTimeout(Func<bool> predicate, TimeSpan timeout)
		{
			var msTimeout = (int)timeout.TotalMilliseconds;
			while (!predicate() && msTimeout-- > 0)
				Thread.Sleep(1);
		}

		public static void WaitForThreadEnd(Thread thread, TimeSpan timeout)
		{
			if (thread?.IsAlive == true)
			{
				WaitOrTimeout(() => thread.IsAlive, timeout);
				if (thread.IsAlive)
				{
					thread.Abort();
				}
			}
		}

		public static DateTime GetNow() => DateTime.Now;

		public static readonly DateTime UnixTimeStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

		public static uint ToUnix(this DateTime dateTime) => (uint)(dateTime - UnixTimeStart).TotalSeconds;

		public static void Init<T>(out T obj) where T : new() => obj = new T();

		public static Random Random { get; } = new Random();

		public static Encoding Utf8Encoder { get; } = new UTF8Encoding(false, false);

		public static bool IsAdmin
		{
			get
			{
#if NET46
				try
				{
					using (var user = System.Security.Principal.WindowsIdentity.GetCurrent())
					{
						var principal = new System.Security.Principal.WindowsPrincipal(user);
						return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
					}
				}
				catch (UnauthorizedAccessException) { return false; }
				catch (Exception)
				{
					Log.Warn("Uncatched admin check.");
					return false;
				}
#else
				return false;
#endif
			}
		}

		public static int MathMod(int x, int mod) => ((x % mod) + mod) % mod;

		private static long Pow(long b, int pow)
		{
			long ret = 1;
			while (pow != 0)
			{
				if ((pow & 1) == 1)
					ret *= b;
				b *= b;
				pow >>= 1;
			}
			return ret;
		}

		public static string FromSeed(int seed)
		{
			var seedstr = new char[7];
			uint plainseed = unchecked((uint)seed);
			for (int i = 0; i < 7; i++)
			{
				if (plainseed > 0)
				{
					plainseed--;
					var remainder = plainseed % 26;
					seedstr[i] = (char)(remainder + 'a');
					plainseed = (plainseed - remainder) / 26;
				}
				else
				{
					seedstr[i] = '\0';
				}
			}
			return new string(seedstr).TrimEnd('\0');
		}

		public static int ToSeed(string seed)
		{
			long finalValue = 0;

			for (int i = 0; i < seed.Length; i++)
			{
				long powVal = (seed[i] - 'a' + 1) * Pow(26, i % 7);
				finalValue += powVal;
				finalValue %= ((long)uint.MaxValue + 1);
			}
			var uval = (uint)finalValue;
			return unchecked((int)uval);
		}

		public static void UnwrapThrow(this E<LocalStr> r)
		{
			if (!r.Ok)
				throw new CommandException(r.Error.Str, CommandExceptionReason.CommandError);
		}

		public static T UnwrapThrow<T>(this R<T, LocalStr> r)
		{
			if (r.Ok)
				return r.Value;
			else
				throw new CommandException(r.Error.Str, CommandExceptionReason.CommandError);
		}

		public static bool UnwrapToLog(this E<LocalStr> r, NLog.Logger logger)
		{
			if (!r.Ok)
				logger.Warn("Could not write message: {0}", r.Error.Str);
			return r.Ok;
		}

		public static string UnrollException(this Exception ex)
		{
			var strb = new StringBuilder();
			while (ex != null)
			{
				strb.AppendFormat("MSG: {0}\nTYPE:{1}\nSTACK:{2}\n", ex.Message, ex.GetType().Name, ex.StackTrace);
				ex = ex.InnerException;
			}
			return strb.ToString();
		}

		public static Exception UnhandledDefault<T>(T value) where T : struct { return new MissingEnumCaseException(typeof(T).Name, value.ToString()); }

		public static Stream GetEmbeddedFile(string name)
		{
			var assembly = Assembly.GetExecutingAssembly();
			return assembly.GetManifestResourceStream(name);
		}

		public static R<T> TryCast<T>(this JToken token, string key)
		{
			if (token is null)
				return R.Err;
			var value = token.SelectToken(key);
			if (value is null)
				return R.Err;
			try { return value.ToObject<T>(); }
			catch (JsonReaderException) { return R.Err; }
		}

		public static E<LocalStr> IsSafeFileName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return new LocalStr(strings.error_playlist_name_invalid_empty); // TODO change to more generic error
			if (name.Length >= 64)
				return new LocalStr(strings.error_playlist_name_invalid_too_long);
			if (!SafeFileNameMatcher.IsMatch(name))
				return new LocalStr(strings.error_playlist_name_invalid_character);
			return R.Ok;
		}

		public static IEnumerable<TResult> SelectOk<TSource, TResult, TErr>(this IEnumerable<TSource> source, Func<TSource, R<TResult, TErr>> selector)
			=> source.Select(selector).Where(x => x.Ok).Select(x => x.Value);
	}

	public class MissingEnumCaseException : Exception
	{
		public MissingEnumCaseException(string enumTypeName, string valueName) : base($"The switch does not handle the value \"{valueName}\" from \"{enumTypeName}\".") { }
		public MissingEnumCaseException(string message, Exception inner) : base(message, inner) { }
	}
}
