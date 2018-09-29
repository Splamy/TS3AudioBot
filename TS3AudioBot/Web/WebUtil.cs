// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Web
{
	internal static class WebUtil
	{
		public const string Default404 =
@"<!doctype html>

<html lang=""en"">
	<head>
		<meta charset = ""utf-8"">
		<title>TS3AudioBot - 404</title>
	</head>
	<body>
		<h1>Not Found</h1>
	</body>
</html>
";

		public static readonly byte[] Default404Data = System.Text.Encoding.UTF8.GetBytes(Default404);
	}
}
