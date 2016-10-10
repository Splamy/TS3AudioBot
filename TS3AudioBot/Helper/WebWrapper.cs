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

namespace TS3AudioBot.Helper
{
	using System;
	using System.IO;
	using System.Net;

	internal static class WebWrapper
	{
		private static TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

		public static bool DownloadString(out string site, Uri link, params Tuple<string, string>[] optionalHeaders)
		{
			var request = WebRequest.Create(link);
			foreach (var header in optionalHeaders)
				request.Headers.Add(header.Item1, header.Item2);
			try
			{
				using (var response = request.GetResponse())
				{
					var stream = response.GetResponseStream();
					using (var reader = new StreamReader(stream))
					{
						site = reader.ReadToEnd();
						return true;
					}
				}
			}
			catch (WebException)
			{
				site = null;
				return false;
			}
		}

		//if (request.Method == "GET")
		//	request.Method = "HEAD";
		public static ValidateCode GetResponse(Uri link) => GetResponse(link, null);
		public static ValidateCode GetResponse(Uri link, TimeSpan timeout) => GetResponse(link, null, timeout);
		public static ValidateCode GetResponse(Uri link, Action<WebResponse> body) => GetResponse(link, body, DefaultTimeout);
		public static ValidateCode GetResponse(Uri link, Action<WebResponse> body, TimeSpan timeout)
		{
			var request = WebRequest.Create(link);
			try
			{
				request.Timeout = (int)timeout.TotalMilliseconds;
				using (var response = request.GetResponse())
				{
					body?.Invoke(response);
				}
				return ValidateCode.Ok;
			}
			catch (WebException webEx)
			{
				HttpWebResponse errorResponse;
				if (webEx.Status == WebExceptionStatus.Timeout)
				{
					Log.Write(Log.Level.Warning, "TH Request timed out");
					return ValidateCode.Timeout;
				}
				else if ((errorResponse = webEx.Response as HttpWebResponse) != null)
				{
					Log.Write(Log.Level.Warning, $"TH Web error: [{(int)errorResponse.StatusCode}] {errorResponse.StatusCode}");
					return ValidateCode.Restricted;
				}
				else
				{
					Log.Write(Log.Level.Warning, $"TH Unknown request error: {webEx}");
					return ValidateCode.UnknownError;
				}
			}
		}
	}

	enum ValidateCode
	{
		Ok,
		UnknownError,
		Restricted,
		Timeout,
	}
}
