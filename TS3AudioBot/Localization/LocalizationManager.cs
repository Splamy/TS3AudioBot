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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot.Helper;

namespace TS3AudioBot.Localization
{
	public class LocalizationManager
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Dictionary<string, LanguageData> loadedLanguage = new Dictionary<string, LanguageData>();
		private static readonly DynamicResourceManager dynResMan;

		private CultureInfo? culture;
		public bool LanguageLoaded => culture != null;

		static LocalizationManager()
		{
			loadedLanguage.Add("en", new LanguageData
			{
				IsInternal = true,
				LoadedSuccessfully = true,
			});

			var resManField = typeof(strings).GetField("resourceMan", BindingFlags.NonPublic | BindingFlags.Static)!;
			var currentResMan = resManField.GetValue(null);
			(currentResMan as ResourceManager)?.ReleaseAllResources();
			dynResMan = new DynamicResourceManager("TS3AudioBot.Localization.strings", typeof(strings).Assembly);
			resManField.SetValue(null, dynResMan);
		}

		public async ValueTask<E<string>> LoadLanguage(string lang, bool forceDownload)
		{
			try { culture = CultureInfo.GetCultureInfo(lang); }
			catch (CultureNotFoundException) { return "Language not found"; }

			var languageDataInfo = loadedLanguage.GetOrNew(culture.Name);

			if (!languageDataInfo.LoadedSuccessfully)
			{
				var result = await LoadLanguageAssembly(languageDataInfo, culture, forceDownload);
				if (!result.Ok)
				{
					culture = CultureInfo.InvariantCulture;
					return result.Error;
				}
				languageDataInfo.LoadedSuccessfully = true;
			}

			return R.Ok;
		}

		public void ApplyLanguage()
		{
			var applyCulture = culture ?? CultureInfo.InvariantCulture;

			Thread.CurrentThread.CurrentCulture =
			Thread.CurrentThread.CurrentUICulture =
			CultureInfo.CurrentCulture =
			CultureInfo.CurrentUICulture = applyCulture;
		}

		private static async Task<E<string>> LoadLanguageAssembly(LanguageData languageDataInfo, CultureInfo culture, bool forceDownload)
		{
			Task<HashSet<string>?>? avaliableToDownload = null;
			var triedDownloading = languageDataInfo.TriedDownloading;

			foreach (var currentResolveCulture in GetWithFallbackCultures(culture))
			{
				// Try loading the resource set from memory
				if (strings.ResourceManager.GetResourceSet(currentResolveCulture, true, false) != null)
					return R.Ok;

				// Do not attempt to download or load integrated languages
				if (languageDataInfo.IsInternal)
					continue;

				var tryFile = GetCultureFileInfo(currentResolveCulture);

				// Check if we need to download the resource
				if (forceDownload || (!tryFile.Exists && !triedDownloading))
				{
					if (avaliableToDownload is null)
					{
						avaliableToDownload = DownloadAvaliableLanguages();
					}
					var list = await avaliableToDownload;
					if (list is null || !list.Contains(currentResolveCulture.Name))
					{
						if (list != null)
							Log.Info("Language \"{0}\" is not available on the server", currentResolveCulture.Name);
						continue;
					}

					try
					{
						languageDataInfo.TriedDownloading = true;
						Directory.CreateDirectory(tryFile.DirectoryName);
						Log.Info("Downloading the resource pack for the language '{0}'", currentResolveCulture.Name);
						await WebWrapper.Request($"https://splamy.de/api/language/project/ts3ab/language/{currentResolveCulture.Name}/dll").ToAction(async response =>
						{
							using var fs = File.Open(tryFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
							await response.Content.CopyToAsync(fs);
						});
					}
					catch (Exception ex)
					{
						Log.Warn(ex, "Failed trying to download language '{0}'", currentResolveCulture.Name);
					}
				}

				// Try loading the resource set from file
				try
				{
					var asm = Assembly.LoadFrom(tryFile.FullName);
					var resStream = asm.GetManifestResourceStream($"TS3AudioBot.Localization.strings.{currentResolveCulture.Name}.resources") ?? throw new NullReferenceException("No stream found");
					var rr = new ResourceReader(resStream);
					var set = new ResourceSet(rr);
					dynResMan.SetResourceSet(currentResolveCulture, set);

					if (strings.ResourceManager.GetResourceSet(currentResolveCulture, true, false) != null)
						return R.Ok;

					Log.Error("The resource set was not found after initialization");
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "Failed to load language file '{0}'", tryFile.FullName);
				}
			}

			return "Could not find language file";
		}

		private static IEnumerable<CultureInfo> GetWithFallbackCultures(CultureInfo culture)
		{
			CultureInfo currentResolveCulture = culture;
			while (currentResolveCulture != CultureInfo.InvariantCulture)
			{
				yield return currentResolveCulture;
				currentResolveCulture = currentResolveCulture.Parent;
			}
		}

		private static FileInfo GetCultureFileInfo(CultureInfo culture)
			=> new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), culture.Name, "TS3AudioBot.resources.dll"));

		private static async Task<HashSet<string>?> DownloadAvaliableLanguages()
		{
			try
			{
				Log.Info("Checking for requested language online");
				var arr = await WebWrapper.Request("https://splamy.de/api/language/project/ts3ab/languages").AsJson<string[]>();
				return new HashSet<string>(arr);
			}
			catch (Exception ex)
			{
				Log.Warn(ex, "Failed to download language overview list");
			}
			return null;
		}

		public static string? GetString(string name)
		{
			return strings.ResourceManager.GetString(name);
		}

		private class LanguageData
		{
			public bool IsInternal { get; set; } = false;
			public bool LoadedSuccessfully { get; set; } = false;
			public bool TriedDownloading { get; set; } = false;
		}
	}
}
