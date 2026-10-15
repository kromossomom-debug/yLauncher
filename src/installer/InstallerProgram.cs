using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class InstallerProgram
{
	public const string UninstallKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\yLauncher_v2";
	private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
	private const string RunValueName = "yLauncher_v2";

	public static string DefaultInstallDir
	{
		get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yLauncher_v2"); }
	}

	private static string LogPath
	{
		get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yLauncher\\updates\\install.log"); }
	}

	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);

		bool silent = false;
		bool update = false;
		bool relaunch = false;
		int waitPid = 0;
		foreach (string arg in args)
		{
			if (arg.Equals("/silent", StringComparison.OrdinalIgnoreCase) || arg.Equals("/s", StringComparison.OrdinalIgnoreCase) || arg.Equals("-silent", StringComparison.OrdinalIgnoreCase))
			{
				silent = true;
			}
			else if (arg.Equals("/update", StringComparison.OrdinalIgnoreCase))
			{
				update = true;
			}
			else if (arg.Equals("/relaunch", StringComparison.OrdinalIgnoreCase))
			{
				relaunch = true;
			}
			else if (arg.StartsWith("/waitpid=", StringComparison.OrdinalIgnoreCase))
			{
				int.TryParse(arg.Substring(9), out waitPid);
			}
		}

		string existingDir = GetExistingInstallDir();
		string targetDir = existingDir ?? DefaultInstallDir;

		if (!silent)
		{
			Application.Run(new InstallerForm(targetDir, existingDir != null));
			return;
		}

		Log("Instalação silenciosa " + AppVersion.Display + " (update=" + update + ", relaunch=" + relaunch + ") em " + targetDir);
		WaitForLauncherExit(targetDir, waitPid);

		bool ok;
		if (update && existingDir != null)
		{
			// Atualização automática: mantém exatamente as escolhas da instalação original.
			string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "yLauncher.lnk");
			ok = PerformInstallation(targetDir, File.Exists(desktopLnk), true, IsAutoStartEnabled(), true);
		}
		else
		{
			ok = PerformInstallation(targetDir, createDesktop: true, createStartMenu: true, autoStart: false, registerProtocols: true);
		}
		Log(ok ? "Instalação concluída." : "Falha na instalação.");

		if (ok && relaunch)
		{
			try
			{
				string exe = Path.Combine(targetDir, "yLauncher.exe");
				Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = targetDir });
			}
			catch (Exception ex)
			{
				Log("Falha ao reabrir: " + ex.Message);
			}
		}
		Environment.ExitCode = ok ? 0 : 1;
	}

	public static string GetExistingInstallDir()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath))
			{
				string location = key == null ? null : key.GetValue("InstallLocation") as string;
				if (!string.IsNullOrEmpty(location) && Directory.Exists(location))
				{
					return location;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static string GetInstalledVersion()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath))
			{
				return key == null ? null : key.GetValue("DisplayVersion") as string;
			}
		}
		catch
		{
			return null;
		}
	}

	private static bool IsAutoStartEnabled()
	{
		try
		{
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
			{
				return key != null && key.GetValue(RunValueName) != null;
			}
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Espera o yLauncher (e o processo que disparou a atualização) encerrar
	/// para que os arquivos possam ser substituídos sem conflito.
	/// </summary>
	private static void WaitForLauncherExit(string targetDir, int waitPid)
	{
		if (waitPid > 0)
		{
			try
			{
				Process.GetProcessById(waitPid).WaitForExit(60000);
			}
			catch
			{
			}
		}

		string targetExe = Path.Combine(targetDir, "yLauncher.exe");
		DateTime deadline = DateTime.UtcNow.AddSeconds(45);
		while (DateTime.UtcNow < deadline)
		{
			bool running = false;
			foreach (Process process in Process.GetProcessesByName("yLauncher"))
			{
				try
				{
					string path = process.MainModule.FileName;
					if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(targetExe), StringComparison.OrdinalIgnoreCase))
					{
						running = true;
					}
				}
				catch
				{
					running = true;
				}
			}
			if (!running)
			{
				return;
			}
			Thread.Sleep(500);
		}
		Log("yLauncher ainda em execução após 45s; tentando instalar mesmo assim.");
	}

	public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
	{
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			if (typeFromProgID != null)
			{
				dynamic shell = Activator.CreateInstance(typeFromProgID);
				dynamic shortcut = shell.CreateShortcut(shortcutPath);
				shortcut.TargetPath = targetPath;
				shortcut.WorkingDirectory = workingDir;
				shortcut.IconLocation = iconPath;
				shortcut.Description = description;
				shortcut.Save();
			}
		}
		catch
		{
		}
	}

	/// <summary>
	/// Extrai um recurso embutido. Grava em um arquivo temporário e troca no
	/// final, com novas tentativas caso o destino ainda esteja bloqueado.
	/// </summary>
	public static bool ExtractEmbeddedResource(string resourceName, string targetFilePath)
	{
		string tempPath = targetFilePath + ".new";
		for (int attempt = 1; attempt <= 8; attempt++)
		{
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
				{
					if (stream == null)
					{
						return false;
					}
					using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
					{
						stream.CopyTo(destination);
					}
				}
				if (new FileInfo(tempPath).Length == 0)
				{
					return false;
				}
				if (File.Exists(targetFilePath))
				{
					File.Delete(targetFilePath);
				}
				File.Move(tempPath, targetFilePath);
				return true;
			}
			catch (Exception ex)
			{
				Log("Tentativa " + attempt + " de extrair " + resourceName + ": " + ex.Message);
				Thread.Sleep(750);
			}
		}
		try
		{
			if (File.Exists(tempPath))
			{
				File.Delete(tempPath);
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool PerformInstallation(string targetDir, bool createDesktop, bool createStartMenu, bool autoStart, bool registerProtocols)
	{
		try
		{
			if (!Directory.Exists(targetDir))
			{
				Directory.CreateDirectory(targetDir);
			}
			string launcherExe = Path.Combine(targetDir, "yLauncher.exe");
			string indexHtml = Path.Combine(targetDir, "index.html");
			string iconFile = Path.Combine(targetDir, "yLauncher.ico");
			string manifestFile = Path.Combine(targetDir, "yLauncher.manifest");
			string uninstallerExe = Path.Combine(targetDir, "Desinstalar_yLauncher.exe");
			string libraryFile = Path.Combine(targetDir, "installed_games.json");

			bool extracted = ExtractEmbeddedResource("yLauncher.exe", launcherExe)
				&& ExtractEmbeddedResource("index.html", indexHtml)
				&& ExtractEmbeddedResource("yLauncher.ico", iconFile)
				&& ExtractEmbeddedResource("yLauncher.manifest", manifestFile)
				&& ExtractEmbeddedResource("Desinstalar_yLauncher.exe", uninstallerExe);
			if (!File.Exists(libraryFile))
			{
				extracted = ExtractEmbeddedResource("installed_games.json", libraryFile) && extracted;
			}
			if (!extracted)
			{
				throw new InvalidOperationException("Não foi possível extrair todos os arquivos do aplicativo.");
			}

			string description = "yLauncher " + AppVersion.Display;
			if (createDesktop)
			{
				string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
				CreateShortcut(Path.Combine(desktop, "yLauncher.lnk"), launcherExe, targetDir, launcherExe + ",0", description);
			}
			if (createStartMenu)
			{
				string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
				CreateShortcut(Path.Combine(programs, "yLauncher.lnk"), launcherExe, targetDir, launcherExe + ",0", description);
				CreateShortcut(Path.Combine(programs, "Desinstalar yLauncher.lnk"), uninstallerExe, targetDir, uninstallerExe + ",0", "Desinstalar yLauncher");
			}
			try
			{
				using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true))
				{
					if (runKey != null)
					{
						if (autoStart)
						{
							runKey.SetValue(RunValueName, "\"" + launcherExe + "\"", RegistryValueKind.String);
						}
						else
						{
							runKey.DeleteValue(RunValueName, throwOnMissingValue: false);
						}
					}
				}
			}
			catch
			{
			}
			if (registerProtocols)
			{
				try
				{
					foreach (string protocol in new[] { "ylauncher", "gamehub" })
					{
						using (RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + protocol))
						{
							protocolKey.SetValue("", "URL:" + protocol + " Protocol");
							protocolKey.SetValue("URL Protocol", "");
							using (RegistryKey commandKey = protocolKey.CreateSubKey("shell\\open\\command"))
							{
								commandKey.SetValue("", "\"" + launcherExe + "\" \"%1\"");
							}
						}
					}
				}
				catch
				{
				}
			}
			try
			{
				using (RegistryKey uninstallKey = Registry.CurrentUser.CreateSubKey(UninstallKeyPath))
				{
					uninstallKey.SetValue("DisplayName", "yLauncher");
					uninstallKey.SetValue("DisplayVersion", AppVersion.Display);
					uninstallKey.SetValue("Publisher", "yLauncher");
					uninstallKey.SetValue("DisplayIcon", launcherExe + ",0");
					uninstallKey.SetValue("InstallLocation", targetDir);
					uninstallKey.SetValue("URLInfoAbout", "https://github.com/" + AppVersion.GitHubRepo);
					uninstallKey.SetValue("URLUpdateInfo", "https://github.com/" + AppVersion.GitHubRepo + "/releases");
					uninstallKey.SetValue("UninstallString", "\"" + uninstallerExe + "\"");
					uninstallKey.SetValue("QuietUninstallString", "\"" + uninstallerExe + "\" /silent");
					long totalBytes = new FileInfo(launcherExe).Length + new FileInfo(indexHtml).Length + new FileInfo(uninstallerExe).Length;
					uninstallKey.SetValue("EstimatedSize", Math.Max(1, (int)(totalBytes / 1024)), RegistryValueKind.DWord);
					uninstallKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
					uninstallKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
				}
			}
			catch
			{
			}
			return true;
		}
		catch (Exception ex)
		{
			Log("Erro: " + ex);
			return false;
		}
	}

	private static void Log(string message)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
			File.AppendAllText(LogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, new UTF8Encoding(false));
		}
		catch
		{
		}
	}
}
