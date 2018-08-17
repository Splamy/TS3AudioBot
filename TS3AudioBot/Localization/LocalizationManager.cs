// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

namespace TS3AudioBot.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Reflection;
	using System.Threading;

	public static class LocalizationManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly HashSet<string> loadedLanguage = new HashSet<string>();

		static LocalizationManager()
		{
			loadedLanguage.Add("en");
		}

		public static E<string> LoadLanguage(string lang)
		{
			CultureInfo culture;
			try { culture = new CultureInfo(lang); }
			catch (CultureNotFoundException) { return "Language not found"; }

			if (!loadedLanguage.Contains(culture.Name))
			{
				var result = LoadLanguageAssembly(culture);
				if (!result.Ok)
				{
					Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
					return result.Error;
				}
				loadedLanguage.Add(culture.Name);
			}

			Thread.CurrentThread.CurrentUICulture = culture;
			return R.Ok;
		}

		private static E<string> LoadLanguageAssembly(CultureInfo culture)
		{
			if (strings.ResourceManager.GetResourceSet(culture, true, false) != null)
				return R.Ok;

			CultureInfo currentResolveCulture = culture;
			while (currentResolveCulture != CultureInfo.InvariantCulture)
			{
				if (strings.ResourceManager.GetResourceSet(currentResolveCulture, true, false) != null)
					return R.Ok;
				currentResolveCulture = currentResolveCulture.Parent;
			}

			currentResolveCulture = culture;
			bool loadOk = false;
			while (currentResolveCulture != CultureInfo.InvariantCulture)
			{
				string tryPath = Path.Combine(currentResolveCulture.Name, "TS3AudioBot.resources.dll");
				try
				{
					Assembly.LoadFrom(tryPath);
					loadOk = true;
					break;
				}
				catch (Exception ex)
				{
					Log.Trace(ex, "Failed trying to load language from '{0}'", tryPath);
					currentResolveCulture = currentResolveCulture.Parent;
				}
			}

			if (loadOk)
				return R.Ok;
			else
				return "Could not find language file";
		}

		public static string GetString(string name)
		{
			return strings.ResourceManager.GetString(name);
		}
	}
}
