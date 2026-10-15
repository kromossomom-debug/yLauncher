using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

/// <summary>
/// Mede o tempo de jogo real.
///
/// Quando o launcher abre um jogo, inicia-se uma sessão com o id do jogo,
/// a pasta de instalação e (se houver) o processo iniciado. A cada 3 s o
/// rastreador verifica se existe algum processo rodando a partir dessa pasta
/// — isso funciona também para jogos abertos por steam://, Epic, GOG etc.,
/// em que o processo iniciado é o da loja e não o do jogo.
///
/// Os totais ficam em %LOCALAPPDATA%\yLauncher\playtime.json.
/// </summary>
internal static class PlaytimeTracker
{
	private sealed class Session
	{
		public string GameId = "";
		public string Name = "";
		public string Dir = "";
		public string Exe = "";
		public int RootPid;
		public DateTime StartedUtc;
		public DateTime FirstSeenUtc = DateTime.MinValue;
		public DateTime LastSeenUtc = DateTime.MinValue;
		public DateTime LastTickUtc = DateTime.MinValue;
		public double Seconds;
		public readonly HashSet<int> Pids = new HashSet<int>();
	}

	private sealed class GameStat
	{
		public long Seconds;
		public int Sessions;
		public long LastPlayed;
	}

	private static readonly object _sync = new object();
	private static readonly Dictionary<string, GameStat> _stats = new Dictionary<string, GameStat>(StringComparer.Ordinal);
	private static readonly Dictionary<string, long> _daily = new Dictionary<string, long>(StringComparer.Ordinal);
	private static Session _active;
	private static Thread _worker;
	private static volatile bool _stop;
	private static DateTime _lastSaveUtc = DateTime.MinValue;
	private static bool _loaded;

	/// <summary>Disparado quando o jogo aparece rodando (true) ou termina (false).</summary>
	public static event Action<bool> RunningChanged;

	private static string StatsPath
	{
		get { return Path.Combine(Updater.DataDirectory, "playtime.json"); }
	}

	public static void Start()
	{
		Load();
		if (_worker != null)
		{
			return;
		}
		_worker = new Thread(Loop) { IsBackground = true, Name = "yLauncher-Playtime" };
		_worker.Start();
	}

	public static void Stop()
	{
		_stop = true;
		lock (_sync)
		{
			if (_active != null && _active.FirstSeenUtc != DateTime.MinValue)
			{
				Commit(_active);
			}
			_active = null;
			Save();
		}
	}

	/// <summary>True quando um jogo rastreado está realmente em execução.</summary>
	public static bool IsGameRunning
	{
		get
		{
			lock (_sync)
			{
				return _active != null && _active.FirstSeenUtc != DateTime.MinValue;
			}
		}
	}

	public static void Begin(string gameId, string name, string installDir, string exePath, Process launched)
	{
		if (string.IsNullOrWhiteSpace(gameId))
		{
			return;
		}
		lock (_sync)
		{
			if (_active != null)
			{
				if (_active.FirstSeenUtc != DateTime.MinValue)
				{
					Commit(_active);
				}
				_active = null;
			}
			var s = new Session
			{
				GameId = gameId.Trim(),
				Name = name ?? "",
				Dir = NormalizeDir(installDir),
				Exe = exePath ?? "",
				StartedUtc = DateTime.UtcNow
			};
			if (string.IsNullOrEmpty(s.Dir) && !string.IsNullOrEmpty(s.Exe))
			{
				try
				{
					if (File.Exists(s.Exe))
					{
						s.Dir = NormalizeDir(Path.GetDirectoryName(s.Exe));
					}
				}
				catch
				{
				}
			}
			// Não rastrear pastas genéricas demais (ex.: C:\ ou Program Files)
			if (!string.IsNullOrEmpty(s.Dir) && s.Dir.Split('\\').Length < 3)
			{
				s.Dir = "";
			}
			if (launched != null)
			{
				try
				{
					s.RootPid = launched.Id;
					s.Pids.Add(launched.Id);
				}
				catch
				{
				}
			}
			_active = s;
			GetStat(s.GameId).LastPlayed = UnixMs(DateTime.UtcNow);
		}
	}

	private static void Loop()
	{
		while (!_stop)
		{
			Thread.Sleep(3000);
			try
			{
				Tick();
			}
			catch
			{
			}
		}
	}

	private static void Tick()
	{
		Session s;
		lock (_sync)
		{
			s = _active;
		}
		if (s == null)
		{
			return;
		}

		bool running = IsSessionAlive(s);
		DateTime now = DateTime.UtcNow;
		bool startedNow = false;
		bool endedNow = false;

		lock (_sync)
		{
			if (_active != s)
			{
				return;
			}
			if (running)
			{
				if (s.FirstSeenUtc == DateTime.MinValue)
				{
					s.FirstSeenUtc = now;
					s.LastTickUtc = now;
					GetStat(s.GameId).Sessions++;
					startedNow = true;
				}
				double delta = Math.Min(10, Math.Max(0, (now - s.LastTickUtc).TotalSeconds));
				s.Seconds += delta;
				GetStat(s.GameId).Seconds += (long)Math.Round(delta);
				GetStat(s.GameId).LastPlayed = UnixMs(now);
				string day = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
				long current;
				_daily.TryGetValue(day, out current);
				_daily[day] = current + (long)Math.Round(delta);
				s.LastTickUtc = now;
				s.LastSeenUtc = now;
				if ((now - _lastSaveUtc).TotalSeconds > 60)
				{
					Save();
				}
			}
			else
			{
				s.LastTickUtc = now;
				if (s.FirstSeenUtc == DateTime.MinValue)
				{
					// O jogo nunca apareceu (cancelado, atualização longa da loja...)
					if ((now - s.StartedUtc).TotalMinutes > 4)
					{
						_active = null;
					}
				}
				else if ((now - s.LastSeenUtc).TotalSeconds > 12)
				{
					Commit(s);
					_active = null;
					endedNow = true;
				}
			}
		}

		Action<bool> handler = RunningChanged;
		if (handler != null)
		{
			if (startedNow)
			{
				handler(true);
			}
			if (endedNow)
			{
				handler(false);
			}
		}
	}

	private static void Commit(Session s)
	{
		GetStat(s.GameId).LastPlayed = UnixMs(DateTime.UtcNow);
		Save();
	}

	private static bool IsSessionAlive(Session s)
	{
		bool alive = false;
		var stale = new List<int>();
		foreach (int pid in s.Pids)
		{
			try
			{
				Process p = Process.GetProcessById(pid);
				if (!p.HasExited)
				{
					alive = true;
				}
				else
				{
					stale.Add(pid);
				}
			}
			catch
			{
				stale.Add(pid);
			}
		}
		foreach (int pid in stale)
		{
			s.Pids.Remove(pid);
		}

		if (!string.IsNullOrEmpty(s.Dir))
		{
			foreach (KeyValuePair<int, string> proc in EnumerateProcessPaths())
			{
				if (proc.Value.StartsWith(s.Dir + "\\", StringComparison.OrdinalIgnoreCase))
				{
					s.Pids.Add(proc.Key);
					alive = true;
				}
			}
		}
		return alive;
	}

	/// <summary>Encerra todos os processos do jogo em execução.</summary>
	public static int CloseActiveGame()
	{
		Session s;
		lock (_sync)
		{
			s = _active;
		}
		if (s == null)
		{
			return 0;
		}
		IsSessionAlive(s);
		int killed = 0;
		foreach (int pid in new List<int>(s.Pids))
		{
			try
			{
				Process p = Process.GetProcessById(pid);
				string name = p.ProcessName;
				if (string.Equals(name, "steam", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(name, "EpicGamesLauncher", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(name, "GalaxyClient", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(name, "Battle.net", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(name, "explorer", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				p.Kill();
				killed++;
			}
			catch
			{
			}
		}
		return killed;
	}

	// ── Processos (funciona com processos 64-bit a partir do launcher 32-bit) ──
	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool CloseHandle(IntPtr handle);

	private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

	private static IEnumerable<KeyValuePair<int, string>> EnumerateProcessPaths()
	{
		var result = new List<KeyValuePair<int, string>>();
		int self = Process.GetCurrentProcess().Id;
		foreach (Process p in Process.GetProcesses())
		{
			int pid;
			try
			{
				pid = p.Id;
			}
			catch
			{
				continue;
			}
			if (pid <= 4 || pid == self)
			{
				continue;
			}
			string path = GetProcessPath(pid);
			if (!string.IsNullOrEmpty(path))
			{
				result.Add(new KeyValuePair<int, string>(pid, path));
			}
		}
		return result;
	}

	public static string GetProcessPath(int pid)
	{
		IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
		if (h == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			var sb = new StringBuilder(1024);
			int size = sb.Capacity;
			return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
		}
		finally
		{
			CloseHandle(h);
		}
	}

	// ── Persistência ────────────────────────────────────────
	private static GameStat GetStat(string id)
	{
		GameStat stat;
		if (!_stats.TryGetValue(id, out stat))
		{
			stat = new GameStat();
			_stats[id] = stat;
		}
		return stat;
	}

	private static void Load()
	{
		lock (_sync)
		{
			if (_loaded)
			{
				return;
			}
			_loaded = true;
			try
			{
				if (!File.Exists(StatsPath))
				{
					return;
				}
				var root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(StatsPath, Encoding.UTF8)) as Dictionary<string, object>;
				if (root == null)
				{
					return;
				}
				object gamesObj;
				var games = root.TryGetValue("games", out gamesObj) ? gamesObj as Dictionary<string, object> : null;
				if (games != null)
				{
					foreach (var kv in games)
					{
						var g = kv.Value as Dictionary<string, object>;
						if (g == null)
						{
							continue;
						}
						_stats[kv.Key] = new GameStat
						{
							Seconds = ToLong(g, "seconds"),
							Sessions = (int)ToLong(g, "sessions"),
							LastPlayed = ToLong(g, "lastPlayed")
						};
					}
				}
				object dailyObj;
				var daily = root.TryGetValue("daily", out dailyObj) ? dailyObj as Dictionary<string, object> : null;
				if (daily != null)
				{
					foreach (var kv in daily)
					{
						try
						{
							_daily[kv.Key] = Convert.ToInt64(kv.Value, CultureInfo.InvariantCulture);
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
	}

	private static void Save()
	{
		try
		{
			// Mantém só os últimos 120 dias no histórico diário
			string cutoff = DateTime.Now.AddDays(-120).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			var oldDays = new List<string>();
			foreach (string day in _daily.Keys)
			{
				if (string.CompareOrdinal(day, cutoff) < 0)
				{
					oldDays.Add(day);
				}
			}
			foreach (string day in oldDays)
			{
				_daily.Remove(day);
			}
			Directory.CreateDirectory(Updater.DataDirectory);
			string tmp = StatsPath + ".tmp";
			File.WriteAllText(tmp, BuildJson(false), new UTF8Encoding(false));
			if (File.Exists(StatsPath))
			{
				File.Delete(StatsPath);
			}
			File.Move(tmp, StatsPath);
			_lastSaveUtc = DateTime.UtcNow;
		}
		catch
		{
		}
	}

	public static string GetJson()
	{
		lock (_sync)
		{
			return BuildJson(true);
		}
	}

	private static string BuildJson(bool includeRuntime)
	{
		var sb = new StringBuilder(4096);
		sb.Append("{\"success\":true,\"games\":{");
		bool first = true;
		foreach (var kv in _stats)
		{
			if (!first)
			{
				sb.Append(',');
			}
			first = false;
			sb.Append('"').Append(Json.Escape(kv.Key)).Append("\":{\"seconds\":").Append(kv.Value.Seconds)
				.Append(",\"sessions\":").Append(kv.Value.Sessions)
				.Append(",\"lastPlayed\":").Append(kv.Value.LastPlayed).Append('}');
		}
		sb.Append("},\"daily\":{");
		first = true;
		foreach (var kv in _daily)
		{
			if (!first)
			{
				sb.Append(',');
			}
			first = false;
			sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
		}
		sb.Append('}');
		if (includeRuntime)
		{
			Session s = _active;
			if (s != null)
			{
				sb.Append(",\"active\":{\"gameId\":\"").Append(Json.Escape(s.GameId))
					.Append("\",\"name\":\"").Append(Json.Escape(s.Name))
					.Append("\",\"running\":").Append(s.FirstSeenUtc != DateTime.MinValue ? "true" : "false")
					.Append(",\"seconds\":").Append((long)s.Seconds)
					.Append(",\"waitingSeconds\":").Append((long)(DateTime.UtcNow - s.StartedUtc).TotalSeconds)
					.Append('}');
			}
			else
			{
				sb.Append(",\"active\":null");
			}
			sb.Append(",\"steam\":").Append(SteamPlaytime.GetJson());
		}
		sb.Append('}');
		return sb.ToString();
	}

	private static string NormalizeDir(string dir)
	{
		if (string.IsNullOrWhiteSpace(dir))
		{
			return "";
		}
		try
		{
			string full = Path.GetFullPath(dir.Trim().Trim('"').Replace('/', '\\')).TrimEnd('\\');
			return Directory.Exists(full) ? full : "";
		}
		catch
		{
			return "";
		}
	}

	private static long UnixMs(DateTime utc)
	{
		return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
	}

	private static long ToLong(Dictionary<string, object> obj, string key)
	{
		object v;
		if (obj.TryGetValue(key, out v) && v != null)
		{
			try
			{
				return Convert.ToInt64(v, CultureInfo.InvariantCulture);
			}
			catch
			{
			}
		}
		return 0;
	}
}

/// <summary>
/// Lê o tempo de jogo que a própria Steam registra (userdata\*\config\localconfig.vdf),
/// para que jogos da Steam já cheguem com o histórico completo.
/// </summary>
internal static class SteamPlaytime
{
	private static string _cache = "{}";
	private static DateTime _cacheTime = DateTime.MinValue;
	private static readonly object _sync = new object();

	public static string GetJson()
	{
		lock (_sync)
		{
			if ((DateTime.UtcNow - _cacheTime).TotalSeconds < 90)
			{
				return _cache;
			}
			_cacheTime = DateTime.UtcNow;
			var minutes = new Dictionary<string, long>();
			var last = new Dictionary<string, long>();
			try
			{
				string steam = Program.FindSteamPath();
				string userdata = string.IsNullOrEmpty(steam) ? null : Path.Combine(steam, "userdata");
				if (userdata != null && Directory.Exists(userdata))
				{
					var block = new Regex("\"(\\d+)\"\\s*\\{([^{}]*)\\}", RegexOptions.Compiled);
					var playtime = new Regex("\"Playtime\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
					var lastPlayed = new Regex("\"LastPlayed\"\\s*\"(\\d+)\"", RegexOptions.IgnoreCase);
					foreach (string user in Directory.GetDirectories(userdata))
					{
						string cfg = Path.Combine(user, "config\\localconfig.vdf");
						if (!File.Exists(cfg))
						{
							continue;
						}
						string text = File.ReadAllText(cfg, Encoding.UTF8);
						foreach (Match m in block.Matches(text))
						{
							string body = m.Groups[2].Value;
							Match pt = playtime.Match(body);
							if (!pt.Success)
							{
								continue;
							}
							string appid = m.Groups[1].Value;
							long mins = long.Parse(pt.Groups[1].Value, CultureInfo.InvariantCulture);
							long prev;
							if (!minutes.TryGetValue(appid, out prev) || mins > prev)
							{
								minutes[appid] = mins;
							}
							Match lp = lastPlayed.Match(body);
							if (lp.Success)
							{
								long ts = long.Parse(lp.Groups[1].Value, CultureInfo.InvariantCulture) * 1000;
								long prevTs;
								if (!last.TryGetValue(appid, out prevTs) || ts > prevTs)
								{
									last[appid] = ts;
								}
							}
						}
					}
				}
			}
			catch
			{
			}
			var sb = new StringBuilder("{");
			bool first = true;
			foreach (var kv in minutes)
			{
				if (!first)
				{
					sb.Append(',');
				}
				first = false;
				long ts;
				last.TryGetValue(kv.Key, out ts);
				sb.Append('"').Append(kv.Key).Append("\":{\"minutes\":").Append(kv.Value).Append(",\"lastPlayed\":").Append(ts).Append('}');
			}
			sb.Append('}');
			_cache = sb.ToString();
			return _cache;
		}
	}
}
