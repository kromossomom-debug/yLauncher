using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private class MEMORYSTATUSEX
	{
		public uint dwLength;

		public uint dwMemoryLoad;

		public ulong ullTotalPhys;

		public ulong ullAvailPhys;

		public ulong ullTotalPageFile;

		public ulong ullAvailPageFile;

		public ulong ullTotalVirtual;

		public ulong ullAvailVirtual;

		public ulong ullAvailExtendedVirtual;

		public MEMORYSTATUSEX()
		{
			dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
		}
	}

	public struct XINPUT_GAMEPAD
	{
		public ushort wButtons;

		public byte bLeftTrigger;

		public byte bRightTrigger;

		public short sThumbLX;

		public short sThumbLY;

		public short sThumbRX;

		public short sThumbRY;
	}

	public struct XINPUT_STATE
	{
		public uint dwPacketNumber;

		public XINPUT_GAMEPAD Gamepad;
	}

	public const ushort XINPUT_GAMEPAD_DPAD_UP = 1;

	public const ushort XINPUT_GAMEPAD_DPAD_DOWN = 2;

	public const ushort XINPUT_GAMEPAD_DPAD_LEFT = 4;

	public const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 8;

	public const ushort XINPUT_GAMEPAD_START = 16;

	public const ushort XINPUT_GAMEPAD_BACK = 32;

	public const ushort XINPUT_GAMEPAD_LEFT_THUMB = 64;

	public const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 128;

	public const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 256;

	public const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 512;

	public const ushort XINPUT_GAMEPAD_A = 4096;

	public const ushort XINPUT_GAMEPAD_B = 8192;

	public const ushort XINPUT_GAMEPAD_X = 16384;

	public const ushort XINPUT_GAMEPAD_Y = 32768;

	private static ulong _prevIdleTime = 0uL;

	private static ulong _prevKernelTime = 0uL;

	private static ulong _prevUserTime = 0uL;

	private static int _lastCpuUsage = 12;

	private static HttpListener _listener = null;

	private static volatile bool _shouldExit = false;

	private static Process _edgeProc = null;

	private static string _activeProfileDir = "";

	private static readonly List<Process> _launchedProcesses = new List<Process>();

	private static readonly string _bridgeToken = Guid.NewGuid().ToString("N");

	private static int _cachedSteamAchievements = -1;

	private static int _cachedSteamWeeklyMinutes = 0;

	private static DateTime _lastSteamScanTime = DateTime.MinValue;

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME lpIdleTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpKernelTime, out System.Runtime.InteropServices.ComTypes.FILETIME lpUserTime);

	private static ulong FileTimeToUlong(System.Runtime.InteropServices.ComTypes.FILETIME ft)
	{
		return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
	}

	public static string GetSystemStatsJson()
	{
		int value = _lastCpuUsage;
		try
		{
			if (GetSystemTimes(out var lpIdleTime, out var lpKernelTime, out var lpUserTime))
			{
				ulong num = FileTimeToUlong(lpIdleTime);
				ulong num2 = FileTimeToUlong(lpKernelTime);
				ulong num3 = FileTimeToUlong(lpUserTime);
				if (_prevIdleTime != 0)
				{
					ulong num4 = num - _prevIdleTime;
					ulong num5 = num2 - _prevKernelTime + (num3 - _prevUserTime);
					if (num5 != 0)
					{
						int num6 = (int)((num5 - num4) * 100 / num5);
						if (num6 >= 0 && num6 <= 100)
						{
							value = num6;
							_lastCpuUsage = num6;
						}
					}
				}
				_prevIdleTime = num;
				_prevKernelTime = num2;
				_prevUserTime = num3;
			}
		}
		catch
		{
		}
		uint value2 = 35u;
		double num7 = 5.6;
		double num8 = 16.0;
		try
		{
			MEMORYSTATUSEX mEMORYSTATUSEX = new MEMORYSTATUSEX();
			if (GlobalMemoryStatusEx(mEMORYSTATUSEX))
			{
				value2 = mEMORYSTATUSEX.dwMemoryLoad;
				num8 = Math.Round((double)mEMORYSTATUSEX.ullTotalPhys / 1073741824.0, 1);
				num7 = Math.Round((double)(mEMORYSTATUSEX.ullTotalPhys - mEMORYSTATUSEX.ullAvailPhys) / 1073741824.0, 1);
			}
		}
		catch
		{
		}
		int value3 = 100;
		bool flag = true;
		bool flag2 = false;
		try
		{
			PowerStatus powerStatus = SystemInformation.PowerStatus;
			flag2 = (powerStatus.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0;
			if (powerStatus.BatteryLifePercent >= 0f && powerStatus.BatteryLifePercent <= 1f)
			{
				value3 = (int)Math.Round(powerStatus.BatteryLifePercent * 100f);
			}
			flag = powerStatus.PowerLineStatus == PowerLineStatus.Online || (powerStatus.BatteryChargeStatus & BatteryChargeStatus.Charging) != 0;
		}
		catch
		{
		}
		int value4 = 0;
		lock (_launchedProcesses)
		{
			_launchedProcesses.RemoveAll(delegate(Process p)
			{
				try
				{
					return p.HasExited;
				}
				catch
				{
					return true;
				}
			});
			value4 = _launchedProcesses.Count;
		}
		StringBuilder stringBuilder = new StringBuilder(256);
		stringBuilder.Append("{\"status\":\"ok\",\"cpu\":");
		stringBuilder.Append(value);
		stringBuilder.Append(",\"ramPercent\":");
		stringBuilder.Append(value2);
		stringBuilder.Append(",\"ramUsedGb\":");
		stringBuilder.Append(num7.ToString("0.0", CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"ramTotalGb\":");
		stringBuilder.Append(num8.ToString("0.0", CultureInfo.InvariantCulture));
		stringBuilder.Append(",\"batteryPercent\":");
		stringBuilder.Append(value3);
		stringBuilder.Append(",\"isCharging\":");
		stringBuilder.Append(flag ? "true" : "false");
		stringBuilder.Append(",\"hasBattery\":");
		stringBuilder.Append(flag2 ? "true" : "false");
		stringBuilder.Append(",\"gamesRunning\":");
		stringBuilder.Append(value4);
		stringBuilder.Append(",\"steamAchievements\":");
		stringBuilder.Append(_cachedSteamAchievements >= 0 ? _cachedSteamAchievements : 0);
		stringBuilder.Append(",\"steamWeeklyPlaytimeMinutes\":");
		stringBuilder.Append(_cachedSteamWeeklyMinutes);
		stringBuilder.Append("}");
		return stringBuilder.ToString();
	}

	private static string GetSteamPath()
	{
		string text = null;
		try
		{
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
			{
				if (registryKey != null)
				{
					text = registryKey.GetValue("SteamPath") as string;
				}
			}
		}
		catch
		{
		}
		if (string.IsNullOrEmpty(text))
		{
			try
			{
				using (RegistryKey registryKey2 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Valve\\Steam"))
				{
					if (registryKey2 != null)
					{
						text = registryKey2.GetValue("InstallPath") as string;
					}
				}
			}
			catch
			{
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			try
			{
				using (RegistryKey registryKey3 = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam"))
				{
					if (registryKey3 != null)
					{
						text = registryKey3.GetValue("InstallPath") as string;
					}
				}
			}
			catch
			{
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			string text2 = "C:\\Program Files (x86)\\Steam";
			if (Directory.Exists(text2))
			{
				text = text2;
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			text = text.Replace('/', '\\');
		}
		return text;
	}

	private static int CountPatternOccurrences(byte[] source, byte[] pattern)
	{
		if (source == null || pattern == null || source.Length < pattern.Length || pattern.Length == 0)
		{
			return 0;
		}
		int count = 0;
		for (int i = 0; i <= source.Length - pattern.Length; i++)
		{
			bool match = true;
			for (int j = 0; j < pattern.Length; j++)
			{
				if (source[i + j] != pattern[j])
				{
					match = false;
					break;
				}
			}
			if (match)
			{
				count++;
				i += pattern.Length - 1;
			}
		}
		return count;
	}

	private static string GetSteamStatsJson()
	{
		if (_cachedSteamAchievements < 0 || (DateTime.UtcNow - _lastSteamScanTime).TotalSeconds > 45)
		{
			int totalAchievements = 0;
			int weeklyMinutes = 0;
			try
			{
				string steamPath = GetSteamPath();
				if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
				{
					string statsDir = Path.Combine(steamPath, "appcache\\stats");
					if (Directory.Exists(statsDir))
					{
						string[] files = Directory.GetFiles(statsDir, "UserGameStats_*.bin");
						byte[] pattern = Encoding.ASCII.GetBytes("AchievementTimes");
						foreach (string file in files)
						{
							try
							{
								byte[] fileBytes = File.ReadAllBytes(file);
								totalAchievements += CountPatternOccurrences(fileBytes, pattern);
							}
							catch
							{
							}
						}
					}
					string userdataDir = Path.Combine(steamPath, "userdata");
					if (Directory.Exists(userdataDir))
					{
						long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
						long sevenDaysSec = 7 * 86400;
						string[] userDirs = Directory.GetDirectories(userdataDir);
						foreach (string uDir in userDirs)
						{
							string localConfig = Path.Combine(uDir, "config\\localconfig.vdf");
							if (File.Exists(localConfig))
							{
								try
								{
									string vdfContent = File.ReadAllText(localConfig, Encoding.UTF8);
									var matches = System.Text.RegularExpressions.Regex.Matches(
										vdfContent,
										"\"(\\d+)\"\\s*\\{\\s*\"LastPlayed\"\\s*\"(\\d+)\"\\s*\"Playtime\"\\s*\"(\\d+)\""
									);
									foreach (System.Text.RegularExpressions.Match m in matches)
									{
										long lastPlayed = 0;
										long playtime = 0;
										if (long.TryParse(m.Groups[2].Value, out lastPlayed) && long.TryParse(m.Groups[3].Value, out playtime))
										{
											long diffSec = nowSec - lastPlayed;
											if (diffSec <= sevenDaysSec && playtime > 0)
											{
												weeklyMinutes += (int)Math.Min(playtime, 300);
											}
										}
									}
								}
								catch
								{
								}
							}
						}
					}
				}
			}
			catch
			{
			}
			_cachedSteamAchievements = totalAchievements;
			_cachedSteamWeeklyMinutes = weeklyMinutes;
			_lastSteamScanTime = DateTime.UtcNow;
		}
		return "{\"success\":true,\"totalAchievements\":" + _cachedSteamAchievements + ",\"weeklyPlaytimeMinutes\":" + _cachedSteamWeeklyMinutes + "}";
	}

	[DllImport("xinput1_4.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetState")]
	private static extern int XInputGetState14(int dwUserIndex, out XINPUT_STATE pState);

	[DllImport("xinput1_3.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetState")]
	private static extern int XInputGetState13(int dwUserIndex, out XINPUT_STATE pState);

	[DllImport("xinput9_1_0.dll", CallingConvention = CallingConvention.StdCall, EntryPoint = "XInputGetState")]
	private static extern int XInputGetState910(int dwUserIndex, out XINPUT_STATE pState);

	private static int SafeXInputGetState(int dwUserIndex, out XINPUT_STATE pState)
	{
		try
		{
			return XInputGetState14(dwUserIndex, out pState);
		}
		catch
		{
			try
			{
				return XInputGetState13(dwUserIndex, out pState);
			}
			catch
			{
				try
				{
					return XInputGetState910(dwUserIndex, out pState);
				}
				catch
				{
					pState = default(XINPUT_STATE);
					return -1;
				}
			}
		}
	}

	public static string GetGamepadJson()
	{
		for (int i = 0; i < 4; i++)
		{
			if (SafeXInputGetState(i, out var pState) == 0)
			{
				ushort wButtons = pState.Gamepad.wButtons;
				byte bLeftTrigger = pState.Gamepad.bLeftTrigger;
				byte bRightTrigger = pState.Gamepad.bRightTrigger;
				short sThumbLX = pState.Gamepad.sThumbLX;
				short sThumbLY = pState.Gamepad.sThumbLY;
				short sThumbRX = pState.Gamepad.sThumbRX;
				short sThumbRY = pState.Gamepad.sThumbRY;
				float num = ((sThumbLX < 0) ? ((float)sThumbLX / 32768f) : ((float)sThumbLX / 32767f));
				float num2 = 0f - ((sThumbLY < 0) ? ((float)sThumbLY / 32768f) : ((float)sThumbLY / 32767f));
				float num3 = (float)(int)bLeftTrigger / 255f;
				float num4 = (float)(int)bRightTrigger / 255f;
				float num5 = ((sThumbRX < 0) ? ((float)sThumbRX / 32768f) : ((float)sThumbRX / 32767f));
				float num6 = 0f - ((sThumbRY < 0) ? ((float)sThumbRY / 32768f) : ((float)sThumbRY / 32767f));
				StringBuilder stringBuilder = new StringBuilder(512);
				stringBuilder.Append("{\"connected\":true,\"id\":\"Xbox / Lenovo Legion Go (XInput)\",\"index\":");
				stringBuilder.Append(i);
				stringBuilder.Append(",\"buttons\":[");
				AppendBtn(stringBuilder, (wButtons & 0x1000) != 0, ((wButtons & 0x1000) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x2000) != 0, ((wButtons & 0x2000) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x4000) != 0, ((wButtons & 0x4000) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x8000) != 0, ((wButtons & 0x8000) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x100) != 0, ((wButtons & 0x100) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x200) != 0, ((wButtons & 0x200) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, num3 > 0.2f, num3);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, num4 > 0.2f, num4);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x20) != 0, ((wButtons & 0x20) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x10) != 0, ((wButtons & 0x10) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x40) != 0, ((wButtons & 0x40) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 0x80) != 0, ((wButtons & 0x80) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 1) != 0, ((wButtons & 1) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 2) != 0, ((wButtons & 2) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 4) != 0, ((wButtons & 4) != 0) ? 1f : 0f);
				stringBuilder.Append(",");
				AppendBtn(stringBuilder, (wButtons & 8) != 0, ((wButtons & 8) != 0) ? 1f : 0f);
				stringBuilder.Append("],\"axes\":[");
				stringBuilder.Append(num.ToString("0.000", CultureInfo.InvariantCulture));
				stringBuilder.Append(",");
				stringBuilder.Append(num2.ToString("0.000", CultureInfo.InvariantCulture));
				stringBuilder.Append(",");
				stringBuilder.Append(num5.ToString("0.000", CultureInfo.InvariantCulture));
				stringBuilder.Append(",");
				stringBuilder.Append(num6.ToString("0.000", CultureInfo.InvariantCulture));
				stringBuilder.Append("],\"packet\":");
				stringBuilder.Append(pState.dwPacketNumber);
				stringBuilder.Append("}");
				return stringBuilder.ToString();
			}
		}
		return "{\"connected\":false,\"buttons\":[],\"axes\":[]}";
	}

	private static void AppendBtn(StringBuilder sb, bool pressed, float val)
	{
		sb.Append("{\"pressed\":");
		sb.Append(pressed ? "true" : "false");
		sb.Append(",\"value\":");
		sb.Append(val.ToString("0.00", CultureInfo.InvariantCulture));
		sb.Append("}");
	}

	public static void BringEdgeToForeground()
	{
		try
		{
			if (_edgeProc != null && !_edgeProc.HasExited)
			{
				_edgeProc.Refresh();
				if (_edgeProc.MainWindowHandle != IntPtr.Zero)
				{
					ShowWindow(_edgeProc.MainWindowHandle, 3);
					SetForegroundWindow(_edgeProc.MainWindowHandle);
					return;
				}
			}
			if (string.IsNullOrEmpty(_activeProfileDir))
			{
				return;
			}
			Process[] processesByName = Process.GetProcessesByName("msedge");
			Process[] array = processesByName;
			foreach (Process process in array)
			{
				try
				{
					if (process.MainWindowHandle != IntPtr.Zero)
					{
						ShowWindow(process.MainWindowHandle, 3);
						SetForegroundWindow(process.MainWindowHandle);
						break;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	[STAThread]
	private static void Main(string[] args)
	{
		try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls; } catch {}
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 1; i < commandLineArgs.Length; i++)
		{
			if (commandLineArgs[i].StartsWith("ylauncher:", StringComparison.OrdinalIgnoreCase) || commandLineArgs[i].StartsWith("gamehub:", StringComparison.OrdinalIgnoreCase))
			{
				HandleProtocolLaunch(commandLineArgs[i]);
				return;
			}
		}
		EnsureProtocolRegistered();
		StartLocalHttpBridge();
		string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
		try
		{
			File.WriteAllText(Path.Combine(baseDirectory, "bridge_token.txt"), _bridgeToken, Encoding.UTF8);
		}
		catch
		{
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				ScanSystemGamesJson();
			}
			catch
			{
			}
		});
		string text = Path.Combine(baseDirectory, "index.html");
		string text3 = null;
		string[] array = new string[3]
		{
			"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
			"C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe",
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Edge\\Application\\msedge.exe")
		};
		string[] array2 = array;
		foreach (string text4 in array2)
		{
			if (File.Exists(text4))
			{
				text3 = text4;
				break;
			}
		}
		if (string.IsNullOrEmpty(text3))
		{
			try
			{
				using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\msedge.exe");
				if (registryKey != null)
				{
					object value = registryKey.GetValue(null);
					if (value != null && File.Exists(value.ToString()))
					{
						text3 = value.ToString();
					}
				}
			}
			catch
			{
			}
		}
		if (!File.Exists(text))
		{
			MessageBox.Show("O arquivo 'index.html' não foi encontrado na pasta do yLauncher!\n\nCertifique-se de que o yLauncher.exe e o index.html estejam na mesma pasta.", "yLauncher - Erro", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		if (string.IsNullOrEmpty(text3) || !File.Exists(text3))
		{
			MessageBox.Show("Microsoft Edge não foi encontrado neste computador.\n\nInstale o Microsoft Edge para executar o yLauncher.", "yLauncher - Erro", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		string text5 = "file:///" + text.Replace("\\", "/").Replace(" ", "%20") + "?bridgeToken=" + _bridgeToken;
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string text6 = Path.Combine(folderPath, "yLauncher\\BrowserProfile");
		try
		{
			if (!Directory.Exists(text6))
			{
				Directory.CreateDirectory(text6);
			}
		}
		catch
		{
		}
		RemoveStaleProfileLocks(text6);
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = text3;
		processStartInfo.Arguments = "--user-data-dir=\"" + text6 + "\" --app=\"" + text5 + "\" --start-fullscreen --no-first-run --no-default-browser-check --disable-background-mode --disable-extensions --disable-component-extensions-with-background-pages --disable-default-apps --disable-sync --autoplay-policy=no-user-gesture-required --allow-file-access-from-files --enable-gpu-rasterization --enable-zero-copy --ignore-gpu-blocklist --disable-renderer-backgrounding --disable-backgrounding-occluded-windows --disable-features=EnableWindowsGamingInputDataFetcher,CalculateNativeWinOcclusion,Translate,OptimizationHints,MediaRouter --enable-features=GamepadButtonAxisEvents,CanvasOopRasterization --window-position=0,0";
		processStartInfo.UseShellExecute = false;
		ProcessStartInfo startInfo = processStartInfo;
		try
		{
			Process proc = Process.Start(startInfo);
			_edgeProc = proc;
			_activeProfileDir = text6;
			ThreadPool.QueueUserWorkItem(delegate
			{
				for (int k = 0; k < 15; k++)
				{
					Thread.Sleep(400);
					try
					{
						if (proc != null && !proc.HasExited)
						{
							proc.Refresh();
							if (proc.MainWindowHandle != IntPtr.Zero)
							{
								ShowWindow(proc.MainWindowHandle, 3);
								SetForegroundWindow(proc.MainWindowHandle);
								break;
							}
						}
					}
					catch
					{
					}
				}
			});
			for (int num = 0; num < 20; num++)
			{
				if (IsProfileInUse(text6, proc))
				{
					break;
				}
				if (_shouldExit)
				{
					break;
				}
				Thread.Sleep(500);
			}
			int num2 = 0;
			while (!_shouldExit)
			{
				Thread.Sleep(800);
				bool flag = false;
				lock (_launchedProcesses)
				{
					_launchedProcesses.RemoveAll(delegate(Process p)
					{
						try
						{
							return p.HasExited;
						}
						catch
						{
							return true;
						}
					});
					flag = _launchedProcesses.Count > 0;
				}
				if (flag)
				{
					num2 = 0;
					continue;
				}
				if (IsProfileInUse(text6, proc))
				{
					num2 = 0;
					continue;
				}
				num2++;
				if (num2 < 35)
				{
					continue;
				}
				break;
			}
			StopLocalHttpBridge();
			CleanupProfileProcesses(text6);
			RemoveStaleProfileLocks(text6);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Erro ao abrir yLauncher: " + ex.Message, "yLauncher", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private static void RemoveStaleProfileLocks(string profileDir)
	{
		try
		{
			string[] array = new string[4]
			{
				Path.Combine(profileDir, "lockfile"),
				Path.Combine(profileDir, "LOCK"),
				Path.Combine(profileDir, "Default\\LOCK"),
				Path.Combine(profileDir, "Default\\lockfile")
			};
			string[] array2 = array;
			foreach (string path in array2)
			{
				if (File.Exists(path))
				{
					try
					{
						File.Delete(path);
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

	private static void PurgeUnwantedExtensions(string profileDir)
	{
		try
		{
			string[] array = new string[4]
			{
				Path.Combine(profileDir, "Default\\Extensions"),
				Path.Combine(profileDir, "Default\\Local Extension Settings"),
				Path.Combine(profileDir, "Default\\Sync Extension Settings"),
				Path.Combine(profileDir, "Default\\DNR Extension Rules")
			};
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (!Directory.Exists(text))
				{
					continue;
				}
				string path = Path.Combine(text, "kiilhncajadbgbmdbdcopdpnmdhlbdle");
				if (Directory.Exists(path))
				{
					try
					{
						Directory.Delete(path, recursive: true);
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

	private static bool IsProfileInUse(string profileDir, Process proc)
	{
		try
		{
			if (proc != null && !proc.HasExited)
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			Process[] processesByName = Process.GetProcessesByName("msedge");
			if (processesByName == null || processesByName.Length == 0)
			{
				return false;
			}
			using ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE Name = 'msedge.exe'");
			foreach (ManagementBaseObject item in managementObjectSearcher.Get())
			{
				string text = item["CommandLine"] as string;
				if (!string.IsNullOrEmpty(text) && text.IndexOf(profileDir, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static void StartLocalHttpBridge()
	{
		Thread thread = new Thread(BridgeWorker);
		thread.IsBackground = true;
		thread.Start();
	}

	private static void StopLocalHttpBridge()
	{
		try
		{
			if (_listener != null && _listener.IsListening)
			{
				_listener.Stop();
				_listener.Close();
			}
		}
		catch
		{
		}
	}

	private static void BridgeWorker()
	{
		int[] array = new int[3] { 48123, 48124, 48125 };
		int[] array2 = array;
		foreach (int num in array2)
		{
			try
			{
				_listener = new HttpListener();
				_listener.Prefixes.Add("http://127.0.0.1:" + num + "/");
				_listener.Start();
			}
			catch
			{
				_listener = null;
				continue;
			}
			break;
		}
		if (_listener == null)
		{
			return;
		}
		while (_listener != null && _listener.IsListening && !_shouldExit)
		{
			try
			{
				HttpListenerContext context = _listener.GetContext();
				ThreadPool.QueueUserWorkItem(HandleBridgeRequest, context);
			}
			catch
			{
				if (_listener == null || !_listener.IsListening || _shouldExit)
				{
					break;
				}
			}
		}
	}

	private static bool SecureEquals(string left, string right)
	{
		if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right) || left.Length != right.Length)
		{
			return false;
		}
		int difference = 0;
		for (int i = 0; i < left.Length; i++)
		{
			difference |= left[i] ^ right[i];
		}
		return difference == 0;
	}

	private static void HandleBridgeRequest(object state)
	{
		HttpListenerContext httpListenerContext = (HttpListenerContext)state;
		if (httpListenerContext == null)
		{
			return;
		}
		HttpListenerRequest request = httpListenerContext.Request;
		HttpListenerResponse response = httpListenerContext.Response;
		try
		{
			string origin = request.Headers["Origin"];
			string requestToken = request.QueryString["bridgeToken"] ?? request.Headers["X-Bridge-Token"];
			bool originAllowed = string.IsNullOrEmpty(origin) || string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase) || origin.StartsWith("file://", StringComparison.OrdinalIgnoreCase) || origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase) || origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase);
			string rawUrl = request.RawUrl ?? "";
			bool isSafeReadEndpoint = rawUrl.StartsWith("/ping", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/focus", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/gamepad-state", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/gamepad-stream", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/installed-games", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/installed_games.json", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/scan-games", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/system-stats", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/steam-stats", StringComparison.OrdinalIgnoreCase)
				|| rawUrl.StartsWith("/steamgrid", StringComparison.OrdinalIgnoreCase);

			bool tokenValid = SecureEquals(requestToken, _bridgeToken);
			if (!originAllowed || (!tokenValid && !(request.IsLocal && isSafeReadEndpoint)))
			{
				response.StatusCode = 403;
				byte[] denied = Encoding.UTF8.GetBytes("{\"success\":false,\"error\":\"Unauthorized bridge request\"}");
				response.ContentType = "application/json; charset=utf-8";
				response.ContentLength64 = denied.Length;
				response.OutputStream.Write(denied, 0, denied.Length);
				response.Close();
				return;
			}
			response.Headers.Add("Access-Control-Allow-Origin", string.IsNullOrEmpty(origin) ? "*" : origin);
			response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
			response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-Bridge-Token");
			response.Headers.Add("Vary", "Origin");
			if (request.HttpMethod == "OPTIONS")
			{
				response.StatusCode = 204;
				response.Close();
				return;
			}
			rawUrl = request.RawUrl ?? "";
			if (rawUrl.StartsWith("/gamepad-stream", StringComparison.OrdinalIgnoreCase))
			{
				response.ContentType = "text/event-stream";
				response.Headers.Add("Cache-Control", "no-cache");
				response.Headers.Add("Connection", "keep-alive");
				try
				{
					using StreamWriter streamWriter = new StreamWriter(response.OutputStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
					while (!_shouldExit)
					{
						string gamepadJson = GetGamepadJson();
						streamWriter.Write("data: " + gamepadJson + "\n\n");
						streamWriter.Flush();
						Thread.Sleep(33);
					}
					return;
				}
				catch
				{
					return;
				}
			}
			string s = "{\"status\":\"ok\"}";
			if (rawUrl.StartsWith("/ping", StringComparison.OrdinalIgnoreCase))
			{
				s = "{\"status\":\"ok\",\"service\":\"yLauncherBridge\"}";
			}
			else if (rawUrl.StartsWith("/focus", StringComparison.OrdinalIgnoreCase))
			{
				BringEdgeToForeground();
				s = "{\"status\":\"ok\"}";
			}
			else if (rawUrl.StartsWith("/exit", StringComparison.OrdinalIgnoreCase))
			{
				s = "{\"status\":\"exiting\"}";
				_shouldExit = true;
			}
			else if (rawUrl.StartsWith("/gamepad-state", StringComparison.OrdinalIgnoreCase))
			{
				s = GetGamepadJson();
			}
			else if (rawUrl.StartsWith("/installed-games", StringComparison.OrdinalIgnoreCase) || rawUrl.StartsWith("/installed_games.json", StringComparison.OrdinalIgnoreCase))
			{
				string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed_games.json");
				bool hasContent = false;
				if (File.Exists(path))
				{
					try
					{
						string textCached = File.ReadAllText(path, Encoding.UTF8);
						if (textCached.IndexOf("\"games\":[]", StringComparison.OrdinalIgnoreCase) < 0 && textCached.Length > 60)
						{
							s = textCached;
							hasContent = true;
						}
					}
					catch
					{
					}
				}
				if (!hasContent)
				{
					s = ScanSystemGamesJson();
				}
			}
			else if (rawUrl.StartsWith("/launch", StringComparison.OrdinalIgnoreCase))
			{
				string text = "";
				int num = rawUrl.IndexOf("path=");
				if (num >= 0)
				{
					text = rawUrl.Substring(num + 5);
					int num2 = text.IndexOf('&');
					if (num2 >= 0)
					{
						text = text.Substring(0, num2);
					}
				}
				try
				{
					ExecuteGameDirect(text);
					s = "{\"success\":true,\"message\":\"Game launched successfully\"}";
				}
				catch (Exception ex)
				{
					response.StatusCode = 500;
					s = "{\"success\":false,\"error\":\"" + ex.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
				}
			}
			else if (rawUrl.StartsWith("/browse-exe", StringComparison.OrdinalIgnoreCase))
			{
				string picked = "";
				string pName = "";
				Thread thread = new Thread((ThreadStart)delegate
				{
					try
					{
						using OpenFileDialog openFileDialog = new OpenFileDialog();
						openFileDialog.Title = "Selecione o executável do jogo (.exe, .lnk, .bat)";
						openFileDialog.Filter = "Jogos & Executáveis (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Todos os Arquivos (*.*)|*.*";
						openFileDialog.CheckFileExists = true;
						if (openFileDialog.ShowDialog() == DialogResult.OK)
						{
							picked = openFileDialog.FileName;
							pName = Path.GetFileNameWithoutExtension(picked);
						}
					}
					catch
					{
					}
				});
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
				s = (string.IsNullOrEmpty(picked) ? "{\"success\":false,\"cancelled\":true}" : ("{\"success\":true,\"path\":\"" + picked.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"name\":\"" + pName.Replace("\"", "\\\"") + "\"}"));
			}
			else if (rawUrl.StartsWith("/steamgrid", StringComparison.OrdinalIgnoreCase))
			{
				string text2 = request.QueryString["endpoint"] ?? "";
				string text3 = request.QueryString["key"] ?? "";
				if (string.IsNullOrWhiteSpace(text2) || string.IsNullOrWhiteSpace(text3) || text2.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					response.StatusCode = 400;
					s = "{\"success\":false,\"error\":\"SteamGridDB endpoint and API key are required\"}";
				}
				else
				{
					try
					{
					string requestUriString = "https://www.steamgriddb.com/api/v2/" + text2.TrimStart('/');
					HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(requestUriString);
					httpWebRequest.Headers.Add("Authorization", "Bearer " + text3);
					httpWebRequest.UserAgent = "yLauncher/2.1";
					httpWebRequest.Timeout = 10000;
					using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
					using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
					s = streamReader.ReadToEnd();
					}
					catch (WebException ex2)
					{
					if (ex2.Response != null)
					{
						using StreamReader streamReader = new StreamReader(ex2.Response.GetResponseStream(), Encoding.UTF8);
						s = streamReader.ReadToEnd();
					}
					else
					{
						s = "{\"success\":false,\"error\":\"" + ex2.Message.Replace("\"", "\\\"") + "\"}";
					}
				}
			}
			}
			else if (rawUrl.StartsWith("/scan-games", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					s = ScanSystemGamesJson();
				}
				catch (Exception ex3)
				{
					s = "{\"success\":false,\"error\":\"" + ex3.Message.Replace("\"", "\\\"") + "\"}";
				}
			}
			else if (rawUrl.StartsWith("/system-stats", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					s = GetSystemStatsJson();
				}
				catch
				{
					s = "{\"status\":\"ok\",\"cpu\":10,\"ramPercent\":35,\"ramUsedGb\":5.6,\"ramTotalGb\":16.0,\"batteryPercent\":100,\"isCharging\":true,\"hasBattery\":false,\"gamesRunning\":0}";
				}
			}
			else if (rawUrl.StartsWith("/steam-stats", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					s = GetSteamStatsJson();
				}
				catch (Exception exSteam)
				{
					s = "{\"success\":false,\"error\":\"" + exSteam.Message.Replace("\"", "\\\"") + "\",\"totalAchievements\":0,\"weeklyPlaytimeMinutes\":0}";
				}
			}
			else if (rawUrl.StartsWith("/open-folder", StringComparison.OrdinalIgnoreCase))
			{
				string text = "";
				int num = rawUrl.IndexOf("path=");
				if (num >= 0)
				{
					text = rawUrl.Substring(num + 5);
					int num2 = text.IndexOf('&');
					if (num2 >= 0)
					{
						text = text.Substring(0, num2);
					}
					try
					{
						text = Uri.UnescapeDataString(text.Replace("+", "%20"));
					}
					catch
					{
					}
				}
				try
				{
					text = Environment.ExpandEnvironmentVariables(text).Trim('"', '\'');
					if (File.Exists(text))
					{
						Process.Start("explorer.exe", "/select,\"" + text + "\"");
						s = "{\"success\":true}";
					}
					else if (Directory.Exists(text))
					{
						Process.Start("explorer.exe", "\"" + text + "\"");
						s = "{\"success\":true}";
					}
					else
					{
						string directoryName = Path.GetDirectoryName(text);
						if (!string.IsNullOrEmpty(directoryName) && Directory.Exists(directoryName))
						{
							Process.Start("explorer.exe", "\"" + directoryName + "\"");
							s = "{\"success\":true}";
						}
						else
						{
							s = "{\"success\":false,\"error\":\"Pasta nao encontrada\"}";
						}
					}
				}
				catch (Exception ex)
				{
					s = "{\"success\":false,\"error\":\"" + ex.Message.Replace("\"", "\\\"") + "\"}";
				}
			}
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			response.ContentType = "application/json; charset=utf-8";
			response.ContentLength64 = bytes.Length;
			response.OutputStream.Write(bytes, 0, bytes.Length);
			response.Close();
		}
		catch
		{
		}
	}

	public static void ExecuteGameDirect(string rawPath)
	{
		if (string.IsNullOrWhiteSpace(rawPath))
		{
			return;
		}
		string text = rawPath.Trim();
		try
		{
			text = Uri.UnescapeDataString(text.Replace("+", "%20"));
		}
		catch
		{
		}
		text = Environment.ExpandEnvironmentVariables(text).Trim();
		string text2 = "";
		string arguments = "";
		bool flag = text.IndexOf("://", StringComparison.OrdinalIgnoreCase) > 0;
		if (flag)
		{
			text2 = text.Trim('"', '\'');
			arguments = "";
		}
		else if (text.StartsWith("\""))
		{
			int num = text.IndexOf('"', 1);
			if (num > 0)
			{
				text2 = text.Substring(1, num - 1).Trim();
				arguments = text.Substring(num + 1).Trim();
			}
			else
			{
				text2 = text.Trim('"');
			}
			text2 = text2.Replace('/', '\\').Trim('"', '\'');
		}
		else
		{
			string text3 = text.Trim('"', '\'');
			if (File.Exists(text3) || Directory.Exists(text3))
			{
				text2 = text3;
			}
			else
			{
				bool flag2 = false;
				string[] array = new string[4] { ".exe", ".bat", ".cmd", ".lnk" };
				string[] array2 = array;
				foreach (string text4 in array2)
				{
					int num2 = text.IndexOf(text4, StringComparison.OrdinalIgnoreCase);
					if (num2 > 0)
					{
						string text5 = text.Substring(0, num2 + text4.Length).Trim('"', '\'');
						if (File.Exists(text5) || text.Length > num2 + text4.Length)
						{
							text2 = text5;
							arguments = text.Substring(num2 + text4.Length).Trim().Trim('"', '\'');
							flag2 = true;
							break;
						}
					}
				}
				if (!flag2)
				{
					text2 = text.Trim('"', '\'');
				}
			}
			text2 = text2.Replace('/', '\\').Trim('"', '\'');
		}
		ProcessStartInfo processStartInfo = new ProcessStartInfo();
		processStartInfo.FileName = text2;
		processStartInfo.Arguments = arguments;
		processStartInfo.UseShellExecute = true;
		ProcessStartInfo processStartInfo2 = processStartInfo;
		try
		{
			if (!flag && File.Exists(text2))
			{
				string directoryName = Path.GetDirectoryName(text2);
				if (!string.IsNullOrEmpty(directoryName))
				{
					processStartInfo2.WorkingDirectory = directoryName;
				}
			}
		}
		catch
		{
		}
		Process gameProc = null;
		try
		{
			gameProc = Process.Start(processStartInfo2);
		}
		catch (Exception ex)
		{
			throw ex;
		}
		if (gameProc == null)
		{
			return;
		}
		lock (_launchedProcesses)
		{
			_launchedProcesses.Add(gameProc);
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				gameProc.WaitForExit();
			}
			catch
			{
			}
			finally
			{
				lock (_launchedProcesses)
				{
					_launchedProcesses.Remove(gameProc);
				}
				Thread.Sleep(800);
				BringEdgeToForeground();
			}
		});
	}

	private static void CleanupProfileProcesses(string profileDir)
	{
		try
		{
			using (ManagementObjectSearcher processSearch = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'msedge.exe'"))
			{
				foreach (ManagementBaseObject processInfo in processSearch.Get())
				{
					string commandLine = processInfo["CommandLine"] as string;
					if (string.IsNullOrEmpty(commandLine) || commandLine.IndexOf(profileDir, StringComparison.OrdinalIgnoreCase) < 0)
					{
						continue;
					}
					try
					{
						int processId = Convert.ToInt32(processInfo["ProcessId"]);
						Process.GetProcessById(processId).Kill();
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

	private static void HandleProtocolLaunch(string uriString)
	{
		try
		{
			if (uriString.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				string profileDir = Path.Combine(folderPath, "yLauncher\\BrowserProfile");
				CleanupProfileProcesses(profileDir);
			}
			else if (uriString.IndexOf("browse-exe", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Thread thread = new Thread((ThreadStart)delegate
				{
					try
					{
						using OpenFileDialog openFileDialog = new OpenFileDialog();
						openFileDialog.Title = "Selecione o executável do jogo (.exe, .lnk, .bat)";
						openFileDialog.Filter = "Jogos & Executáveis (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Todos os Arquivos (*.*)|*.*";
						openFileDialog.CheckFileExists = true;
						if (openFileDialog.ShowDialog() == DialogResult.OK)
						{
							string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
							string path = Path.Combine(baseDirectory, "picked_exe.txt");
							File.WriteAllText(path, openFileDialog.FileName);
							string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
							string path2 = Path.Combine(folderPath2, "yLauncher\\picked_exe.txt");
							try
							{
								File.WriteAllText(path2, openFileDialog.FileName);
								return;
							}
							catch
							{
								return;
							}
						}
					}
					catch
					{
					}
				});
				thread.SetApartmentState(ApartmentState.STA);
				thread.Start();
				thread.Join();
			}
			else
			{
				MessageBox.Show("Comando de protocolo não reconhecido.", "yLauncher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Não foi possível iniciar o jogo:\n" + ex.Message, "yLauncher", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private static void EnsureProtocolRegistered()
	{
		try
		{
			string executablePath = Application.ExecutablePath;
			string[] array = new string[2] { "ylauncher", "gamehub" };
			string[] array2 = array;
			foreach (string text in array2)
			{
				using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + text);
				if (registryKey != null)
				{
					registryKey.SetValue("", "URL:" + text + " Protocol");
					registryKey.SetValue("URL Protocol", "");
					using RegistryKey registryKey2 = registryKey.CreateSubKey("shell\\open\\command");
					registryKey2?.SetValue("", "\"" + executablePath + "\" \"%1\"");
				}
			}
		}
		catch
		{
		}
	}

	public static string ScanSystemGamesJson()
	{
		List<string> list = new List<string>();
		HashSet<string> hashSet = new HashSet<string>();
		try
		{
			List<string> list2 = new List<string>();
			string text = null;
			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam");
				if (registryKey != null)
				{
					text = registryKey.GetValue("SteamPath") as string;
				}
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				try
				{
					using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Valve\\Steam");
					if (registryKey != null)
					{
						text = registryKey.GetValue("InstallPath") as string;
					}
				}
				catch
				{
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				try
				{
					using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam");
					if (registryKey != null)
					{
						text = registryKey.GetValue("InstallPath") as string;
					}
				}
				catch
				{
				}
			}
			if (string.IsNullOrEmpty(text))
			{
				string text2 = "C:\\Program Files (x86)\\Steam";
				if (Directory.Exists(text2))
				{
					text = text2;
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				text = text.Replace('/', '\\');
				list2.Add(text);
				string path = Path.Combine(text, "steamapps\\libraryfolders.vdf");
				if (File.Exists(path))
				{
					try
					{
						string[] array = File.ReadAllLines(path, Encoding.UTF8);
						string[] array2 = array;
						foreach (string text3 in array2)
						{
							string text4 = text3.Trim();
							if (!text4.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							int num = text4.IndexOf('"', 6);
							if (num <= 0)
							{
								continue;
							}
							int num2 = text4.IndexOf('"', num + 1);
							if (num2 > num)
							{
								string text5 = text4.Substring(num + 1, num2 - num - 1).Replace("\\\\", "\\").Trim();
								if (Directory.Exists(text5) && !list2.Contains(text5))
								{
									list2.Add(text5);
								}
							}
						}
					}
					catch
					{
					}
				}
			}
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo driveInfo in drives)
			{
				try
				{
					if (driveInfo.IsReady)
					{
						string text2 = Path.Combine(driveInfo.RootDirectory.FullName, "SteamLibrary");
						if (Directory.Exists(text2) && !list2.Contains(text2))
						{
							list2.Add(text2);
						}
					}
				}
				catch
				{
				}
			}
			foreach (string item2 in list2)
			{
				string path2 = Path.Combine(item2, "steamapps");
				if (!Directory.Exists(path2))
				{
					continue;
				}
				string[] files = Directory.GetFiles(path2, "appmanifest_*.acf");
				string[] array2 = files;
				foreach (string path3 in array2)
				{
					try
					{
						string text6 = null;
						string text7 = null;
						string text8 = null;
						string text9 = null;
						long result = 0L;
						string[] array = File.ReadAllLines(path3, Encoding.UTF8);
						string[] array3 = array;
						foreach (string text3 in array3)
						{
							string text10 = text3.Trim();
							if (text10.StartsWith("\"appid\"", StringComparison.OrdinalIgnoreCase))
							{
								text6 = ExtractAcfValue(text10);
							}
							else if (text10.StartsWith("\"name\"", StringComparison.OrdinalIgnoreCase))
							{
								text7 = ExtractAcfValue(text10);
							}
							else if (text10.StartsWith("\"installdir\"", StringComparison.OrdinalIgnoreCase))
							{
								text8 = ExtractAcfValue(text10);
							}
							else if (text10.StartsWith("\"StateFlags\"", StringComparison.OrdinalIgnoreCase))
							{
								text9 = ExtractAcfValue(text10);
							}
							else if (text10.StartsWith("\"SizeOnDisk\"", StringComparison.OrdinalIgnoreCase))
							{
								long.TryParse(ExtractAcfValue(text10), out result);
							}
						}
						if (string.IsNullOrEmpty(text6) || string.IsNullOrEmpty(text7))
						{
							continue;
						}
						switch (text6)
						{
						case "250820":
							continue;
						case "1070560":
							continue;
						case "1391110":
							continue;
						}
						if (text7.IndexOf("Steamworks", StringComparison.OrdinalIgnoreCase) < 0 && text7.IndexOf("Proton", StringComparison.OrdinalIgnoreCase) < 0 && text7.IndexOf("Runtime", StringComparison.OrdinalIgnoreCase) < 0 && text7.IndexOf("Soundtrack", StringComparison.OrdinalIgnoreCase) < 0 && !hashSet.Contains(text6))
						{
							hashSet.Add(text6);
							double num3 = Math.Round((double)result / 1073741824.0, 1);
							string text11 = ((num3 > 0.0) ? (num3.ToString("0.0", CultureInfo.InvariantCulture) + " GB") : "");
							string text12 = "https://shared.steamstatic.com/store_item_assets/steam/apps/" + text6 + "/library_600x900.jpg";
							string text13 = "https://shared.steamstatic.com/store_item_assets/steam/apps/" + text6 + "/library_hero.jpg";
							string text14 = "steam://rungameid/" + text6;
							string item = "{\"id\":\"" + text6 + "\",\"name\":\"" + EscapeJsonStr(text7) + "\",\"platform\":\"steam\",\"genre\":\"Steam Game\",\"cover\":\"" + text12 + "\",\"hero\":\"" + text13 + "\",\"launch\":\"" + text14 + "\",\"isInstalled\":true,\"sizeText\":\"" + text11 + "\"}";
							list.Add(item);
						}
					}
					catch
					{
					}
				}
			}
			try
			{
				if (!string.IsNullOrEmpty(text))
				{
					string text15 = Path.Combine(text, "appcache\\appinfo.vdf");
					string path4 = Path.Combine(text, "appcache\\librarycache");
					if (File.Exists(text15) && Directory.Exists(path4))
					{
						Dictionary<uint, string> dictionary = ParseSteamAppInfo(text15);
						string[] directories = Directory.GetDirectories(path4);
						string[] array2 = directories;
						foreach (string path5 in array2)
						{
							string fileName = Path.GetFileName(path5);
							uint result2 = 0u;
							if (!uint.TryParse(fileName, out result2) || result2 == 0)
							{
								continue;
							}
							string text16 = result2.ToString();
							if (!hashSet.Contains(text16))
							{
								string text17 = null;
								if (dictionary.ContainsKey(result2))
								{
									text17 = dictionary[result2];
								}
								if (!string.IsNullOrEmpty(text17) && text17.IndexOf("Dedicated Server", StringComparison.OrdinalIgnoreCase) < 0 && text17.IndexOf("Steamworks", StringComparison.OrdinalIgnoreCase) < 0 && text17.IndexOf("Soundtrack", StringComparison.OrdinalIgnoreCase) < 0 && text17.IndexOf("SDK", StringComparison.OrdinalIgnoreCase) < 0)
								{
									hashSet.Add(text16);
									string text12 = "https://shared.steamstatic.com/store_item_assets/steam/apps/" + text16 + "/library_600x900.jpg";
									string text13 = "https://shared.steamstatic.com/store_item_assets/steam/apps/" + text16 + "/library_hero.jpg";
									string text14 = "steam://rungameid/" + text16;
									string item = "{\"id\":\"" + text16 + "\",\"name\":\"" + EscapeJsonStr(text17) + "\",\"platform\":\"steam\",\"genre\":\"Steam Game\",\"cover\":\"" + text12 + "\",\"hero\":\"" + text13 + "\",\"launch\":\"" + text14 + "\",\"isInstalled\":false,\"sizeText\":\"\"}";
									list.Add(item);
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
		try
		{
			string path6 = "C:\\ProgramData\\Epic\\EpicGamesLauncher\\Data\\Manifests";
			if (Directory.Exists(path6))
			{
				string[] files2 = Directory.GetFiles(path6, "*.item");
				string[] array2 = files2;
				foreach (string path7 in array2)
				{
					try
					{
						string json = File.ReadAllText(path7);
						string text18 = ExtractJsonVal(json, "DisplayName");
						string text19 = ExtractJsonVal(json, "AppName");
						string text20 = ExtractJsonVal(json, "InstallLocation");
						string text21 = ExtractJsonVal(json, "LaunchExecutable");
						if (!string.IsNullOrEmpty(text18) && !string.IsNullOrEmpty(text20))
						{
							string text22 = Path.Combine(text20, text21 ?? "");
							if (File.Exists(text22) || Directory.Exists(text20))
							{
								string item = "{\"id\":\"epic_" + EscapeJsonStr(text19 ?? text18) + "\",\"name\":\"" + EscapeJsonStr(text18) + "\",\"platform\":\"ea\",\"genre\":\"Epic Games\",\"cover\":\"https://via.placeholder.com/600x900/14141e/06b6d4?text=" + Uri.EscapeDataString(text18) + "\",\"hero\":\"https://via.placeholder.com/1920x1080/0d0d16/06b6d4?text=" + Uri.EscapeDataString(text18) + "\",\"launch\":\"" + EscapeJsonStr(text22) + "\",\"isInstalled\":true,\"sizeText\":\"\"}";
								list.Add(item);
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
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("{\"success\":true,\"total\":" + list.Count + ",\"games\":[");
		for (int k = 0; k < list.Count; k++)
		{
			if (k > 0)
			{
				stringBuilder.Append(",");
			}
			stringBuilder.Append(list[k]);
		}
		stringBuilder.Append("]}");
		string text23 = stringBuilder.ToString();
		try
		{
			File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed_games.json"), text23, Encoding.UTF8);
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string path8 = Path.Combine(folderPath, "yLauncher\\installed_games.json");
			if (Directory.Exists(Path.GetDirectoryName(path8)))
			{
				File.WriteAllText(path8, text23, Encoding.UTF8);
			}
		}
		catch
		{
		}
		return text23;
	}

	private static Dictionary<uint, string> ParseSteamAppInfo(string appinfoPath)
	{
		Dictionary<uint, string> dictionary = new Dictionary<uint, string>();
		try
		{
			byte[] buffer = File.ReadAllBytes(appinfoPath);
			using MemoryStream memoryStream = new MemoryStream(buffer);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			binaryReader.ReadUInt32();
			binaryReader.ReadUInt32();
			long num = binaryReader.ReadInt64();
			if (num <= 16 || num >= memoryStream.Length)
			{
				return dictionary;
			}
			memoryStream.Position = num;
			uint num2 = binaryReader.ReadUInt32();
			List<string> list = new List<string>((int)Math.Min(num2, 50000u));
			for (int i = 0; i < num2; i++)
			{
				StringBuilder stringBuilder = new StringBuilder();
				while (memoryStream.Position < memoryStream.Length)
				{
					byte b = binaryReader.ReadByte();
					if (b == 0)
					{
						break;
					}
					stringBuilder.Append((char)b);
				}
				list.Add(stringBuilder.ToString());
			}
			int num3 = list.IndexOf("name");
			int num4 = list.IndexOf("type");
			memoryStream.Position = 16L;
			while (memoryStream.Position < num)
			{
				uint num5 = binaryReader.ReadUInt32();
				if (num5 == 0)
				{
					break;
				}
				uint num6 = binaryReader.ReadUInt32();
				long num7 = memoryStream.Position + num6;
				memoryStream.Position += 40L;
				string text = null;
				string text2 = null;
				while (memoryStream.Position < num7)
				{
					byte b2 = binaryReader.ReadByte();
					if (b2 == 8)
					{
						continue;
					}
					uint num8 = binaryReader.ReadUInt32();
					if (b2 != 0)
					{
						if (b2 == 1)
						{
							StringBuilder stringBuilder = new StringBuilder();
							while (memoryStream.Position < num7)
							{
								byte b = binaryReader.ReadByte();
								if (b == 0)
								{
									break;
								}
								stringBuilder.Append((char)b);
							}
							string text3 = stringBuilder.ToString();
							if (num8 == (uint)num3 && text == null)
							{
								text = text3;
							}
							else if (num8 == (uint)num4 && text2 == null)
							{
								text2 = text3;
							}
						}
						else if (b2 == 2 || b2 == 3)
						{
							memoryStream.Position += 4L;
						}
						else if (b2 == 7)
						{
							memoryStream.Position += 8L;
						}
					}
					if (text == null || text2 == null)
					{
						continue;
					}
					break;
				}
				if (!string.IsNullOrEmpty(text) && (text2 == null || string.Equals(text2, "game", StringComparison.OrdinalIgnoreCase)))
				{
					dictionary[num5] = text;
				}
				memoryStream.Position = num7;
			}
		}
		catch
		{
		}
		return dictionary;
	}

	private static string ExtractAcfValue(string acfLine)
	{
		int num = acfLine.LastIndexOf('"');
		if (num > 0)
		{
			int num2 = acfLine.LastIndexOf('"', num - 1);
			if (num2 >= 0)
			{
				return acfLine.Substring(num2 + 1, num - num2 - 1);
			}
		}
		return "";
	}

	private static string ExtractJsonVal(string json, string field)
	{
		int num = json.IndexOf("\"" + field + "\"", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return null;
		}
		int num2 = json.IndexOf(':', num);
		if (num2 < 0)
		{
			return null;
		}
		int num3 = json.IndexOf('"', num2);
		if (num3 < 0)
		{
			return null;
		}
		int num4 = json.IndexOf('"', num3 + 1);
		if (num4 < 0)
		{
			return null;
		}
		return json.Substring(num3 + 1, num4 - num3 - 1);
	}

	private static string EscapeJsonStr(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "")
			.Replace("\n", "");
	}
}
