using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Proxy com cache para a API pública da loja Steam (detalhes e busca),
/// usada na página de detalhes do jogo. Só acessa store.steampowered.com.
/// </summary>
internal static class SteamStore
{
	private static string CacheDir
	{
		get { return Path.Combine(Updater.DataDirectory, "cache\\steam"); }
	}

	private static string MapLang(string lang)
	{
		switch ((lang ?? "").ToLowerInvariant())
		{
			case "pt": return "brazilian";
			case "ru": return "russian";
			default: return "english";
		}
	}

	private static string MapCountry(string lang)
	{
		switch ((lang ?? "").ToLowerInvariant())
		{
			case "pt": return "BR";
			case "ru": return "RU";
			default: return "US";
		}
	}

	public static string AppDetails(string appid, string lang)
	{
		if (string.IsNullOrEmpty(appid) || !Regex.IsMatch(appid, "^\\d{1,10}$"))
		{
			return Json.Error("appid inválido");
		}
		string l = MapLang(lang);
		string cacheFile = Path.Combine(CacheDir, "app_" + appid + "_" + l + ".json");
		string cached = ReadCache(cacheFile, TimeSpan.FromDays(7));
		if (cached != null)
		{
			return cached;
		}
		string url = "https://store.steampowered.com/api/appdetails?appids=" + appid + "&l=" + l + "&cc=" + MapCountry(lang);
		string body = Get(url);
		WriteCache(cacheFile, body);
		return body;
	}

	public static string Search(string term, string lang)
	{
		term = (term ?? "").Trim();
		if (term.Length < 2 || term.Length > 120)
		{
			return Json.Error("termo inválido");
		}
		string key = Regex.Replace(term.ToLowerInvariant(), "[^a-z0-9]+", "_");
		if (key.Length > 60)
		{
			key = key.Substring(0, 60);
		}
		string cacheFile = Path.Combine(CacheDir, "search_" + key + ".json");
		string cached = ReadCache(cacheFile, TimeSpan.FromDays(14));
		if (cached != null)
		{
			return cached;
		}
		string url = "https://store.steampowered.com/api/storesearch/?term=" + Uri.EscapeDataString(term) + "&l=english&cc=" + MapCountry(lang);
		string body = Get(url);
		WriteCache(cacheFile, body);
		return body;
	}

	private static string Get(string url)
	{
		var request = (HttpWebRequest)WebRequest.Create(url);
		request.UserAgent = "yLauncher/" + AppVersion.Display;
		request.Timeout = 10000;
		request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
		using (var response = (HttpWebResponse)request.GetResponse())
		using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
		{
			return reader.ReadToEnd();
		}
	}

	private static string ReadCache(string file, TimeSpan maxAge)
	{
		try
		{
			if (File.Exists(file) && DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < maxAge)
			{
				return File.ReadAllText(file, Encoding.UTF8);
			}
		}
		catch
		{
		}
		return null;
	}

	private static void WriteCache(string file, string body)
	{
		try
		{
			if (string.IsNullOrEmpty(body) || body.Length > 2 * 1024 * 1024)
			{
				return;
			}
			Directory.CreateDirectory(CacheDir);
			File.WriteAllText(file, body, new UTF8Encoding(false));
		}
		catch
		{
		}
	}
}
