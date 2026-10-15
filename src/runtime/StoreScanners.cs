using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

/// <summary>
/// Detecção de jogos instalados nas lojas além da Steam:
/// Epic Games, GOG Galaxy, Battle.net, EA app e Ubisoft Connect.
/// Cada scanner é independente e tolerante a falhas: um erro em uma loja
/// nunca impede a detecção das outras.
/// </summary>
internal static class StoreScanners
{
	public static void ScanAll(List<string> output, HashSet<string> seenIds)
	{
		Run(() => ScanEpic(output, seenIds));
		Run(() => ScanGog(output, seenIds));
		Run(() => ScanBattleNet(output, seenIds));
		Run(() => ScanEaApp(output, seenIds));
		Run(() => ScanUbisoft(output, seenIds));
	}

	private static void Run(Action scanner)
	{
		try
		{
			scanner();
		}
		catch
		{
		}
	}

	public static string GameJson(string id, string name, string platform, string genre, string cover, string hero, string launch, bool installed, string sizeText, string installDir)
	{
		var sb = new StringBuilder(384);
		sb.Append("{\"id\":\"").Append(Json.Escape(id))
			.Append("\",\"name\":\"").Append(Json.Escape(name))
			.Append("\",\"platform\":\"").Append(Json.Escape(platform))
			.Append("\",\"genre\":\"").Append(Json.Escape(genre))
			.Append("\",\"cover\":\"").Append(Json.Escape(cover))
			.Append("\",\"hero\":\"").Append(Json.Escape(hero))
			.Append("\",\"launch\":\"").Append(Json.Escape(launch))
			.Append("\",\"isInstalled\":").Append(installed ? "true" : "false")
			.Append(",\"sizeText\":\"").Append(Json.Escape(sizeText))
			.Append("\",\"installDir\":\"").Append(Json.Escape(installDir))
			.Append("\",\"autoDetected\":true}");
		return sb.ToString();
	}

	private static bool Add(List<string> output, HashSet<string> seenIds, string id, string json)
	{
		if (string.IsNullOrEmpty(id) || seenIds.Contains(id))
		{
			return false;
		}
		seenIds.Add(id);
		output.Add(json);
		return true;
	}

	// ── Epic Games ──────────────────────────────────────────
	private static void ScanEpic(List<string> output, HashSet<string> seenIds)
	{
		string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
		string manifests = Path.Combine(programData, "Epic\\EpicGamesLauncher\\Data\\Manifests");
		if (!Directory.Exists(manifests))
		{
			return;
		}
		var serializer = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
		foreach (string file in Directory.GetFiles(manifests, "*.item"))
		{
			try
			{
				var item = serializer.DeserializeObject(File.ReadAllText(file, Encoding.UTF8)) as Dictionary<string, object>;
				if (item == null)
				{
					continue;
				}
				string displayName = Str(item, "DisplayName");
				string appName = Str(item, "AppName");
				string mainApp = Str(item, "MainGameAppName");
				string installLocation = Str(item, "InstallLocation");
				string ns = Str(item, "CatalogNamespace");
				string catalogItem = Str(item, "CatalogItemId");
				if (string.IsNullOrEmpty(displayName) || string.IsNullOrEmpty(appName))
				{
					continue;
				}
				if (Bool(item, "bIsIncompleteInstall"))
				{
					continue;
				}
				// DLCs/add-ons possuem MainGameAppName diferente do AppName.
				if (!string.IsNullOrEmpty(mainApp) && !string.Equals(mainApp, appName, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				if (!HasCategory(item, "games") && HasAnyCategory(item))
				{
					continue;
				}
				if (string.IsNullOrEmpty(installLocation) || !Directory.Exists(installLocation))
				{
					continue;
				}

				string launch = (!string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(catalogItem))
					? "com.epicgames.launcher://apps/" + Uri.EscapeDataString(ns) + "%3A" + Uri.EscapeDataString(catalogItem) + "%3A" + Uri.EscapeDataString(appName) + "?action=launch&silent=true"
					: "com.epicgames.launcher://apps/" + Uri.EscapeDataString(appName) + "?action=launch&silent=true";

				string size = FormatSize(Long(item, "InstallSize"));
				Add(output, seenIds, "epic_" + appName,
					GameJson("epic_" + appName, displayName, "epic", "Epic Games", "", "", launch, true, size, installLocation));
			}
			catch
			{
			}
		}
	}

	// ── GOG Galaxy ──────────────────────────────────────────
	private static void ScanGog(List<string> output, HashSet<string> seenIds)
	{
		foreach (string root in new[] { "SOFTWARE\\WOW6432Node\\GOG.com\\Games", "SOFTWARE\\GOG.com\\Games" })
		{
			using (RegistryKey games = Registry.LocalMachine.OpenSubKey(root))
			{
				if (games == null)
				{
					continue;
				}
				foreach (string sub in games.GetSubKeyNames())
				{
					try
					{
						using (RegistryKey game = games.OpenSubKey(sub))
						{
							if (game == null)
							{
								continue;
							}
							string gameId = (game.GetValue("gameID") as string) ?? sub;
							string name = game.GetValue("gameName") as string;
							string path = game.GetValue("path") as string;
							string exe = game.GetValue("exe") as string;
							string launchParam = game.GetValue("launchParam") as string;
							string dependsOn = game.GetValue("dependsOn") as string;
							if (!string.IsNullOrEmpty(dependsOn))
							{
								continue; // DLC
							}
							if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path) || !Directory.Exists(path))
							{
								continue;
							}

							string launch;
							string galaxy = FindGogGalaxyClient();
							if (!string.IsNullOrEmpty(galaxy))
							{
								launch = "\"" + galaxy + "\" /command=runGame /gameId=" + gameId + " /path=\"" + path + "\"";
							}
							else if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
							{
								launch = "\"" + exe + "\"" + (string.IsNullOrEmpty(launchParam) ? "" : " " + launchParam);
							}
							else
							{
								continue;
							}

							Add(output, seenIds, "gog_" + gameId,
								GameJson("gog_" + gameId, name, "gog", "GOG", "", "", launch, true, "", path));
						}
					}
					catch
					{
					}
				}
			}
		}
	}

	private static string _gogGalaxyClient;

	private static string FindGogGalaxyClient()
	{
		if (_gogGalaxyClient != null)
		{
			return _gogGalaxyClient;
		}
		_gogGalaxyClient = "";
		foreach (string keyPath in new[] { "SOFTWARE\\WOW6432Node\\GOG.com\\GalaxyClient\\paths", "SOFTWARE\\GOG.com\\GalaxyClient\\paths" })
		{
			try
			{
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
				{
					string client = key == null ? null : key.GetValue("client") as string;
					if (!string.IsNullOrEmpty(client))
					{
						string exe = Path.Combine(client, "GalaxyClient.exe");
						if (File.Exists(exe))
						{
							_gogGalaxyClient = exe;
							break;
						}
					}
				}
			}
			catch
			{
			}
		}
		return _gogGalaxyClient;
	}

	// ── Battle.net ──────────────────────────────────────────
	// UID do desinstalador da Blizzard → código de lançamento do Battle.net.
	private static readonly Dictionary<string, string> BattleNetCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "wow", "WoW" }, { "wow_classic", "WoW_wow_classic" }, { "wow_classic_era", "WoW_wow_classic_era" },
		{ "fenris", "Fen" }, { "diablo3", "D3" }, { "osi", "OSI" }, { "anbs", "ANBS" },
		{ "prometheus", "Pro" }, { "hs_beta", "WTCG" }, { "heroes", "Hero" },
		{ "s1", "S1" }, { "s2", "S2" }, { "w3", "W3" }, { "rtro", "RTRO" }, { "wlby", "WLBY" },
		{ "viper", "VIPR" }, { "odin", "ODIN" }, { "lazarus", "LAZR" }, { "zeus", "ZEUS" },
		{ "fore", "Fore" }, { "auks", "AUKS" }, { "spot", "SPOT" }, { "aqua", "AQUA" }, { "pinta", "PNTA" }
	};

	private static void ScanBattleNet(List<string> output, HashSet<string> seenIds)
	{
		string battleNetExe = null;
		var candidates = new List<KeyValuePair<string, RegistryInfo>>();
		foreach (RegistryInfo entry in EnumerateUninstallEntries())
		{
			if (string.Equals(entry.DisplayName, "Battle.net", StringComparison.OrdinalIgnoreCase))
			{
				if (!string.IsNullOrEmpty(entry.InstallLocation))
				{
					string exe = Path.Combine(entry.InstallLocation, "Battle.net.exe");
					if (File.Exists(exe))
					{
						battleNetExe = exe;
					}
				}
				continue;
			}
			if (entry.Publisher.IndexOf("Blizzard", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			Match m = Regex.Match(entry.UninstallString, "--uid=([A-Za-z0-9_]+)");
			if (!m.Success)
			{
				continue;
			}
			candidates.Add(new KeyValuePair<string, RegistryInfo>(m.Groups[1].Value, entry));
		}

		if (battleNetExe == null)
		{
			string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			string guess = Path.Combine(pf86, "Battle.net\\Battle.net.exe");
			if (File.Exists(guess))
			{
				battleNetExe = guess;
			}
		}

		foreach (var pair in candidates)
		{
			string uid = pair.Key;
			RegistryInfo entry = pair.Value;
			string code;
			if (!BattleNetCodes.TryGetValue(uid, out code))
			{
				string baseUid = uid.Split('_')[0];
				if (!BattleNetCodes.TryGetValue(baseUid, out code))
				{
					continue;
				}
			}
			string launch = battleNetExe != null
				? "\"" + battleNetExe + "\" --exec=\"launch " + code + "\""
				: "battlenet://" + code;
			Add(output, seenIds, "bnet_" + uid.ToLowerInvariant(),
				GameJson("bnet_" + uid.ToLowerInvariant(), entry.DisplayName, "bnet", "Battle.net", "", "", launch, true, entry.SizeText, entry.InstallLocation));
		}
	}

	// ── EA app ──────────────────────────────────────────────
	private static void ScanEaApp(List<string> output, HashSet<string> seenIds)
	{
		foreach (RegistryInfo entry in EnumerateUninstallEntries())
		{
			if (entry.Publisher.IndexOf("Electronic Arts", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			if (entry.KeyName.StartsWith("Steam App", StringComparison.OrdinalIgnoreCase))
			{
				continue; // jogo EA comprado na Steam — já detectado pelo scanner da Steam
			}
			if (entry.DisplayName.IndexOf("EA app", StringComparison.OrdinalIgnoreCase) >= 0
				|| entry.DisplayName.IndexOf("Origin", StringComparison.OrdinalIgnoreCase) == 0)
			{
				continue;
			}
			if (string.IsNullOrEmpty(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation))
			{
				continue;
			}
			string installerData = Path.Combine(entry.InstallLocation, "__Installer\\installerdata.xml");
			if (!File.Exists(installerData))
			{
				continue;
			}
			string contentId = null;
			try
			{
				Match m = Regex.Match(File.ReadAllText(installerData), "<contentID>\\s*([^<\\s]+)\\s*</contentID>", RegexOptions.IgnoreCase);
				if (m.Success)
				{
					contentId = m.Groups[1].Value.Trim();
				}
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(contentId))
			{
				continue;
			}
			string launch = "origin2://game/launch?offerIds=" + Uri.EscapeDataString(contentId) + "&autoDownload=1";
			Add(output, seenIds, "ea_" + contentId,
				GameJson("ea_" + contentId, entry.DisplayName, "ea", "EA app", "", "", launch, true, entry.SizeText, entry.InstallLocation));
		}
	}

	// ── Ubisoft Connect ─────────────────────────────────────
	private static void ScanUbisoft(List<string> output, HashSet<string> seenIds)
	{
		using (RegistryKey installs = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Ubisoft\\Launcher\\Installs"))
		{
			if (installs == null)
			{
				return;
			}
			var names = new Dictionary<string, RegistryInfo>(StringComparer.OrdinalIgnoreCase);
			foreach (RegistryInfo entry in EnumerateUninstallEntries())
			{
				Match m = Regex.Match(entry.KeyName, "^Uplay Install (\\d+)$", RegexOptions.IgnoreCase);
				if (m.Success)
				{
					names[m.Groups[1].Value] = entry;
				}
			}
			foreach (string id in installs.GetSubKeyNames())
			{
				try
				{
					using (RegistryKey game = installs.OpenSubKey(id))
					{
						string dir = game == null ? null : game.GetValue("InstallDir") as string;
						if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
						{
							continue;
						}
						RegistryInfo info;
						string name = names.TryGetValue(id, out info) && !string.IsNullOrEmpty(info.DisplayName)
							? info.DisplayName
							: new DirectoryInfo(dir.TrimEnd('/', '\\')).Name;
						Add(output, seenIds, "ubi_" + id,
							GameJson("ubi_" + id, name, "ubisoft", "Ubisoft Connect", "", "", "uplay://launch/" + id + "/0", true, info != null ? info.SizeText : "", dir));
					}
				}
				catch
				{
				}
			}
		}
	}

	// ── Registro do Windows: entradas de desinstalação ──────
	private sealed class RegistryInfo
	{
		public string KeyName = "";
		public string DisplayName = "";
		public string Publisher = "";
		public string InstallLocation = "";
		public string UninstallString = "";
		public string SizeText = "";
	}

	private static List<RegistryInfo> _uninstallCache;

	private static IEnumerable<RegistryInfo> EnumerateUninstallEntries()
	{
		if (_uninstallCache != null)
		{
			return _uninstallCache;
		}
		var list = new List<RegistryInfo>();
		var roots = new[]
		{
			new KeyValuePair<RegistryKey, string>(Registry.LocalMachine, "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
			new KeyValuePair<RegistryKey, string>(Registry.LocalMachine, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
			new KeyValuePair<RegistryKey, string>(Registry.CurrentUser, "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall")
		};
		foreach (var root in roots)
		{
			try
			{
				using (RegistryKey parent = root.Key.OpenSubKey(root.Value))
				{
					if (parent == null)
					{
						continue;
					}
					foreach (string sub in parent.GetSubKeyNames())
					{
						try
						{
							using (RegistryKey key = parent.OpenSubKey(sub))
							{
								if (key == null)
								{
									continue;
								}
								var info = new RegistryInfo
								{
									KeyName = sub,
									DisplayName = ((key.GetValue("DisplayName") as string) ?? "").Trim(),
									Publisher = ((key.GetValue("Publisher") as string) ?? "").Trim(),
									InstallLocation = ((key.GetValue("InstallLocation") as string) ?? "").Trim().Trim('"'),
									UninstallString = (key.GetValue("UninstallString") as string) ?? ""
								};
								object size = key.GetValue("EstimatedSize");
								if (size is int && (int)size > 0)
								{
									info.SizeText = FormatSize((long)(int)size * 1024L);
								}
								if (!string.IsNullOrEmpty(info.DisplayName))
								{
									list.Add(info);
								}
							}
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
		}
		_uninstallCache = list;
		return list;
	}

	/// <summary>Descarta o cache do registro para que um novo scan veja instalações recentes.</summary>
	public static void ResetCaches()
	{
		_uninstallCache = null;
		_gogGalaxyClient = null;
	}

	// ── Helpers ─────────────────────────────────────────────
	public static string FormatSize(long bytes)
	{
		if (bytes <= 0)
		{
			return "";
		}
		double gb = bytes / 1073741824.0;
		if (gb >= 1)
		{
			return gb.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " GB";
		}
		return (bytes / 1048576.0).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " MB";
	}

	private static string Str(Dictionary<string, object> obj, string key)
	{
		object value;
		return obj.TryGetValue(key, out value) && value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : "";
	}

	private static long Long(Dictionary<string, object> obj, string key)
	{
		object value;
		if (obj.TryGetValue(key, out value) && value != null)
		{
			try
			{
				return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
			}
			catch
			{
			}
		}
		return 0;
	}

	private static bool Bool(Dictionary<string, object> obj, string key)
	{
		object value;
		return obj.TryGetValue(key, out value) && value is bool && (bool)value;
	}

	private static bool HasAnyCategory(Dictionary<string, object> item)
	{
		object value;
		var list = item.TryGetValue("AppCategories", out value) ? value as IEnumerable : null;
		if (list == null || value is string)
		{
			return false;
		}
		foreach (object _ in list)
		{
			return true;
		}
		return false;
	}

	private static bool HasCategory(Dictionary<string, object> item, string category)
	{
		object value;
		var list = item.TryGetValue("AppCategories", out value) ? value as IEnumerable : null;
		if (list == null || value is string)
		{
			return false;
		}
		foreach (object c in list)
		{
			if (string.Equals(Convert.ToString(c), category, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
