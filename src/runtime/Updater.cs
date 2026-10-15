using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;

/// <summary>
/// Atualizador automático do yLauncher baseado em GitHub Releases.
///
/// Fluxo:
///   1. Alguns segundos após abrir (e depois a cada 6 horas) consulta
///      https://api.github.com/repos/{repo}/releases/latest.
///   2. Se a tag for mais nova que <see cref="AppVersion.Display"/>, localiza o
///      asset "Instalador_yLauncher*.exe" e o hash SHA-256 dele (campo "digest"
///      da API ou o asset "checksums.sha256").
///   3. Baixa em segundo plano para %LOCALAPPDATA%\yLauncher\updates, valida o
///      tamanho e o SHA-256 e marca a atualização como pronta.
///   4. A instalação acontece quando o launcher fecha (ou imediatamente se o
///      usuário clicar em "Reiniciar agora"), executando o instalador em modo
///      silencioso com /update, que espera o yLauncher encerrar e preserva a
///      biblioteca, as capas e as configurações.
///
/// Cópias portáteis (fora da pasta registrada pelo instalador) apenas avisam
/// que há uma nova versão e apontam para a página da release.
/// </summary>
internal static class Updater
{
	public const string StateIdle = "idle";
	public const string StateChecking = "checking";
	public const string StateUpToDate = "up-to-date";
	public const string StateAvailable = "available";
	public const string StateDownloading = "downloading";
	public const string StateReady = "ready";
	public const string StateError = "error";

	private static readonly object _sync = new object();
	private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
	private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(8);
	private const long MaxInstallerBytes = 200L * 1024 * 1024;

	private static string _state = StateIdle;
	private static string _latestVersion = "";
	private static string _notes = "";
	private static string _releaseUrl = "";
	private static string _error = "";
	private static int _progress;
	private static DateTime _lastCheckUtc = DateTime.MinValue;
	private static string _readyInstallerPath = "";

	private static string _assetUrl = "";
	private static string _assetName = "";
	private static long _assetSize;
	private static string _assetSha256 = "";

	private static Thread _worker;
	private static readonly AutoResetEvent _wake = new AutoResetEvent(false);
	private static volatile bool _stopRequested;
	private static volatile bool _downloadRequested;

	public static string DataDirectory
	{
		get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yLauncher"); }
	}

	public static string UpdatesDirectory
	{
		get { return Path.Combine(DataDirectory, "updates"); }
	}

	private static string ConfigPath
	{
		get { return Path.Combine(DataDirectory, "updater.cfg"); }
	}

	public static bool IsReady
	{
		get
		{
			lock (_sync)
			{
				return _state == StateReady && File.Exists(_readyInstallerPath);
			}
		}
	}

	/// <summary>Atualização automática ligada (padrão: sim).</summary>
	public static bool AutoUpdateEnabled
	{
		get
		{
			try
			{
				if (File.Exists(ConfigPath))
				{
					foreach (string line in File.ReadAllLines(ConfigPath))
					{
						string trimmed = line.Trim();
						if (trimmed.StartsWith("auto=", StringComparison.OrdinalIgnoreCase))
						{
							return trimmed.Substring(5).Trim() != "0";
						}
					}
				}
			}
			catch
			{
			}
			return true;
		}
		set
		{
			try
			{
				Directory.CreateDirectory(DataDirectory);
				File.WriteAllText(ConfigPath, "auto=" + (value ? "1" : "0") + Environment.NewLine, new UTF8Encoding(false));
			}
			catch
			{
			}
			if (value)
			{
				_wake.Set();
			}
		}
	}

	/// <summary>
	/// True quando este executável é a cópia registrada pelo instalador
	/// (e portanto pode ser atualizada pelo instalador silencioso).
	/// </summary>
	public static bool IsInstalledCopy
	{
		get
		{
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\yLauncher_v2"))
				{
					string location = key == null ? null : key.GetValue("InstallLocation") as string;
					if (string.IsNullOrEmpty(location))
					{
						return false;
					}
					string a = Path.GetFullPath(location).TrimEnd('\\', '/');
					string b = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\', '/');
					return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
				}
			}
			catch
			{
				return false;
			}
		}
	}

	public static void Start()
	{
		if (_worker != null)
		{
			return;
		}
		CleanupOldDownloads();
		_worker = new Thread(WorkerLoop) { IsBackground = true, Name = "yLauncher-Updater" };
		_worker.Start();
	}

	public static void Stop()
	{
		_stopRequested = true;
		_wake.Set();
	}

	/// <summary>Força uma verificação imediata (botão "Verificar agora").</summary>
	public static void RequestCheck()
	{
		_wake.Set();
	}

	/// <summary>Baixa a atualização mesmo com o modo automático desligado.</summary>
	public static void RequestDownload()
	{
		_downloadRequested = true;
		_wake.Set();
	}

	private static void WorkerLoop()
	{
		if (_wake.WaitOne(FirstCheckDelay) && _stopRequested)
		{
			return;
		}
		while (!_stopRequested)
		{
			try
			{
				CheckAndMaybeDownload();
			}
			catch (Exception ex)
			{
				SetError(ex.Message);
			}
			_wake.WaitOne(CheckInterval);
		}
	}

	private static void CheckAndMaybeDownload()
	{
		lock (_sync)
		{
			if (_state == StateDownloading)
			{
				return;
			}
			if (_state == StateReady && File.Exists(_readyInstallerPath))
			{
				return;
			}
			_state = StateChecking;
			_error = "";
		}

		Dictionary<string, object> release = FetchLatestRelease();
		string tag = AsString(release, "tag_name");
		Version latest = ParseVersion(tag);
		Version current = ParseVersion(AppVersion.Display);
		if (latest == null || current == null)
		{
			throw new InvalidOperationException("Tag de versão inválida na release: " + tag);
		}

		lock (_sync)
		{
			_lastCheckUtc = DateTime.UtcNow;
			_latestVersion = latest.ToString(3);
			_releaseUrl = AsString(release, "html_url");
			_notes = Truncate(AsString(release, "body"), 4000);
			if (latest <= current)
			{
				_state = StateUpToDate;
				return;
			}
		}

		FindInstallerAsset(release);

		bool shouldDownload = IsInstalledCopy && (AutoUpdateEnabled || _downloadRequested);
		if (!shouldDownload)
		{
			lock (_sync)
			{
				_state = StateAvailable;
			}
			return;
		}

		_downloadRequested = false;
		DownloadAndVerify();
	}

	private static Dictionary<string, object> FetchLatestRelease()
	{
		string url = "https://api.github.com/repos/" + AppVersion.GitHubRepo + "/releases/latest";
		string json = HttpGetString(url, true);
		var serializer = new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 };
		var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
		if (release == null)
		{
			throw new InvalidOperationException("Resposta inesperada da API do GitHub.");
		}
		object draft;
		object pre;
		if ((release.TryGetValue("draft", out draft) && draft is bool && (bool)draft)
			|| (release.TryGetValue("prerelease", out pre) && pre is bool && (bool)pre))
		{
			throw new InvalidOperationException("A release mais recente não é estável.");
		}
		return release;
	}

	private static void FindInstallerAsset(Dictionary<string, object> release)
	{
		object assetsObj;
		release.TryGetValue("assets", out assetsObj);
		var assets = assetsObj as IEnumerable;
		if (assets == null)
		{
			throw new InvalidOperationException("A release não possui arquivos anexados.");
		}

		Dictionary<string, object> installer = null;
		Dictionary<string, object> checksums = null;
		var installerPattern = new Regex("^Instalador_yLauncher.*\\.exe$", RegexOptions.IgnoreCase);
		foreach (object item in assets)
		{
			var asset = item as Dictionary<string, object>;
			if (asset == null)
			{
				continue;
			}
			string name = AsString(asset, "name");
			if (installer == null && installerPattern.IsMatch(name))
			{
				installer = asset;
			}
			else if (string.Equals(name, "checksums.sha256", StringComparison.OrdinalIgnoreCase))
			{
				checksums = asset;
			}
		}

		if (installer == null)
		{
			throw new InvalidOperationException("Instalador não encontrado na release.");
		}

		string assetName = AsString(installer, "name");
		string downloadUrl = AsString(installer, "browser_download_url");
		long size = AsLong(installer, "size");
		string sha = "";

		string digest = AsString(installer, "digest");
		if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
		{
			sha = digest.Substring(7);
		}
		if (!IsSha256Hex(sha) && checksums != null)
		{
			string text = HttpGetString(AsString(checksums, "browser_download_url"), false);
			foreach (string rawLine in text.Split('\n'))
			{
				string line = rawLine.Trim();
				if (line.EndsWith(assetName, StringComparison.OrdinalIgnoreCase))
				{
					string candidate = line.Split(new[] { ' ', '\t', '*' }, StringSplitOptions.RemoveEmptyEntries)[0];
					if (IsSha256Hex(candidate))
					{
						sha = candidate;
						break;
					}
				}
			}
		}
		if (!IsSha256Hex(sha))
		{
			throw new InvalidOperationException("A release não publica o SHA-256 do instalador; atualização ignorada por segurança.");
		}
		if (!downloadUrl.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("URL de download inesperada.");
		}
		if (size <= 0 || size > MaxInstallerBytes)
		{
			throw new InvalidOperationException("Tamanho do instalador inválido.");
		}

		lock (_sync)
		{
			_assetName = assetName;
			_assetUrl = downloadUrl;
			_assetSize = size;
			_assetSha256 = sha.ToLowerInvariant();
		}
	}

	private static void DownloadAndVerify()
	{
		string url;
		long expectedSize;
		string expectedSha;
		string version;
		lock (_sync)
		{
			_state = StateDownloading;
			_progress = 0;
			url = _assetUrl;
			expectedSize = _assetSize;
			expectedSha = _assetSha256;
			version = _latestVersion;
		}

		Directory.CreateDirectory(UpdatesDirectory);
		string finalPath = Path.Combine(UpdatesDirectory, "Instalador_yLauncher_v" + version + ".exe");
		string partPath = finalPath + ".part";

		if (File.Exists(finalPath) && string.Equals(ComputeSha256(finalPath), expectedSha, StringComparison.OrdinalIgnoreCase))
		{
			MarkReady(finalPath);
			return;
		}

		try
		{
			if (File.Exists(partPath))
			{
				File.Delete(partPath);
			}
			HttpWebRequest request = CreateRequest(url, false);
			request.Timeout = 30000;
			request.ReadWriteTimeout = 60000;
			using (var response = (HttpWebResponse)request.GetResponse())
			using (Stream input = response.GetResponseStream())
			using (var output = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				byte[] buffer = new byte[81920];
				long total = 0;
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					if (_stopRequested)
					{
						throw new OperationCanceledException();
					}
					total += read;
					if (total > MaxInstallerBytes)
					{
						throw new InvalidOperationException("Download maior que o esperado.");
					}
					output.Write(buffer, 0, read);
					lock (_sync)
					{
						_progress = (int)Math.Min(99, total * 100 / Math.Max(1, expectedSize));
					}
				}
			}

			var info = new FileInfo(partPath);
			if (info.Length != expectedSize)
			{
				throw new InvalidOperationException("Tamanho do download não confere.");
			}
			string actualSha = ComputeSha256(partPath);
			if (!string.Equals(actualSha, expectedSha, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("SHA-256 do download não confere; arquivo descartado.");
			}
			if (File.Exists(finalPath))
			{
				File.Delete(finalPath);
			}
			File.Move(partPath, finalPath);
			MarkReady(finalPath);
		}
		catch
		{
			try
			{
				if (File.Exists(partPath))
				{
					File.Delete(partPath);
				}
			}
			catch
			{
			}
			throw;
		}
	}

	private static void MarkReady(string path)
	{
		lock (_sync)
		{
			_readyInstallerPath = path;
			_progress = 100;
			_state = StateReady;
		}
	}

	private static void SetError(string message)
	{
		lock (_sync)
		{
			_state = StateError;
			_error = Truncate(message ?? "Erro desconhecido", 300);
			_lastCheckUtc = DateTime.UtcNow;
		}
	}

	/// <summary>
	/// Executa o instalador baixado em modo silencioso. Ele aguarda o yLauncher
	/// encerrar antes de substituir os arquivos.
	/// </summary>
	public static bool LaunchInstaller(bool relaunch)
	{
		string path;
		lock (_sync)
		{
			if (_state != StateReady || !File.Exists(_readyInstallerPath))
			{
				return false;
			}
			path = _readyInstallerPath;
		}
		try
		{
			// Revalida o hash imediatamente antes de executar.
			if (!string.Equals(ComputeSha256(path), _assetSha256, StringComparison.OrdinalIgnoreCase))
			{
				SetError("O instalador baixado foi alterado; atualização cancelada.");
				return false;
			}
			var psi = new ProcessStartInfo(path)
			{
				Arguments = "/silent /update /waitpid=" + Process.GetCurrentProcess().Id + (relaunch ? " /relaunch" : ""),
				UseShellExecute = true,
				WorkingDirectory = UpdatesDirectory
			};
			Process.Start(psi);
			return true;
		}
		catch (Exception ex)
		{
			SetError(ex.Message);
			return false;
		}
	}

	public static string GetStatusJson()
	{
		var sb = new StringBuilder(512);
		lock (_sync)
		{
			sb.Append("{\"success\":true");
			AppendJson(sb, "current", AppVersion.Display);
			AppendJson(sb, "state", _state);
			AppendJson(sb, "latest", _latestVersion);
			AppendJson(sb, "notes", _notes);
			AppendJson(sb, "releaseUrl", _releaseUrl);
			AppendJson(sb, "error", _error);
			sb.Append(",\"progress\":").Append(_progress);
			sb.Append(",\"autoUpdate\":").Append(AutoUpdateEnabled ? "true" : "false");
			sb.Append(",\"portable\":").Append(IsInstalledCopy ? "false" : "true");
			AppendJson(sb, "lastCheck", _lastCheckUtc == DateTime.MinValue ? "" : _lastCheckUtc.ToString("o", CultureInfo.InvariantCulture));
			AppendJson(sb, "repo", AppVersion.GitHubRepo);
			sb.Append('}');
		}
		return sb.ToString();
	}

	private static void CleanupOldDownloads()
	{
		try
		{
			if (!Directory.Exists(UpdatesDirectory))
			{
				return;
			}
			Version current = ParseVersion(AppVersion.Display);
			foreach (string file in Directory.GetFiles(UpdatesDirectory))
			{
				string name = Path.GetFileName(file);
				if (name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
				{
					TryDelete(file);
					continue;
				}
				Match m = Regex.Match(name, "_v(\\d+\\.\\d+(?:\\.\\d+)?)\\.exe$", RegexOptions.IgnoreCase);
				if (m.Success)
				{
					Version v = ParseVersion(m.Groups[1].Value);
					if (v != null && current != null && v <= current)
					{
						TryDelete(file);
					}
				}
			}
		}
		catch
		{
		}
	}

	// ── Utilitários ─────────────────────────────────────────

	private static HttpWebRequest CreateRequest(string url, bool api)
	{
		var request = (HttpWebRequest)WebRequest.Create(url);
		request.UserAgent = "yLauncher-Updater/" + AppVersion.Display;
		request.AllowAutoRedirect = true;
		request.MaximumAutomaticRedirections = 5;
		request.Timeout = 15000;
		request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
		if (api)
		{
			request.Accept = "application/vnd.github+json";
			request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
		}
		return request;
	}

	private static string HttpGetString(string url, bool api)
	{
		HttpWebRequest request = CreateRequest(url, api);
		try
		{
			using (var response = (HttpWebResponse)request.GetResponse())
			using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
			{
				return reader.ReadToEnd();
			}
		}
		catch (WebException ex)
		{
			var http = ex.Response as HttpWebResponse;
			if (http != null && http.StatusCode == HttpStatusCode.NotFound)
			{
				throw new InvalidOperationException("Nenhuma release publicada em " + AppVersion.GitHubRepo + ".");
			}
			if (http != null && (int)http.StatusCode == 403)
			{
				throw new InvalidOperationException("Limite da API do GitHub atingido; nova tentativa mais tarde.");
			}
			throw new InvalidOperationException("Sem conexão com o GitHub (" + ex.Status + ").");
		}
	}

	public static Version ParseVersion(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		Match m = Regex.Match(text, "(\\d+)\\.(\\d+)(?:\\.(\\d+))?");
		if (!m.Success)
		{
			return null;
		}
		int major = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
		int minor = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
		int build = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
		return new Version(major, minor, build);
	}

	private static string ComputeSha256(string path)
	{
		using (var sha = SHA256.Create())
		using (var stream = File.OpenRead(path))
		{
			byte[] hash = sha.ComputeHash(stream);
			var sb = new StringBuilder(hash.Length * 2);
			foreach (byte b in hash)
			{
				sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
			}
			return sb.ToString();
		}
	}

	private static bool IsSha256Hex(string value)
	{
		return !string.IsNullOrEmpty(value) && Regex.IsMatch(value, "^[0-9a-fA-F]{64}$");
	}

	private static string AsString(Dictionary<string, object> obj, string key)
	{
		object value;
		return obj != null && obj.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : "";
	}

	private static long AsLong(Dictionary<string, object> obj, string key)
	{
		object value;
		if (obj != null && obj.TryGetValue(key, out value) && value != null)
		{
			try
			{
				return Convert.ToInt64(value, CultureInfo.InvariantCulture);
			}
			catch
			{
			}
		}
		return 0;
	}

	private static string Truncate(string value, int max)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		return value.Length <= max ? value : value.Substring(0, max) + "…";
	}

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch
		{
		}
	}

	private static void AppendJson(StringBuilder sb, string key, string value)
	{
		sb.Append(",\"").Append(key).Append("\":\"").Append(Json.Escape(value)).Append('"');
	}
}

/// <summary>Helpers de serialização JSON compartilhados pelo runtime.</summary>
internal static class Json
{
	public static string Escape(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		var sb = new StringBuilder(s.Length + 8);
		foreach (char c in s)
		{
			switch (c)
			{
				case '\\': sb.Append("\\\\"); break;
				case '"': sb.Append("\\\""); break;
				case '\n': sb.Append("\\n"); break;
				case '\r': sb.Append("\\r"); break;
				case '\t': sb.Append("\\t"); break;
				case '\b': sb.Append("\\b"); break;
				case '\f': sb.Append("\\f"); break;
				default:
					if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029)
					{
						sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						sb.Append(c);
					}
					break;
			}
		}
		return sb.ToString();
	}

	public static string Error(string message)
	{
		return "{\"success\":false,\"error\":\"" + Escape(message) + "\"}";
	}
}
