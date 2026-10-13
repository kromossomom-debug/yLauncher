using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class InstallerProgram
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		bool flag = false;
		foreach (string text in args)
		{
			if (text.Equals("/silent", StringComparison.OrdinalIgnoreCase) || text.Equals("/s", StringComparison.OrdinalIgnoreCase) || text.Equals("-silent", StringComparison.OrdinalIgnoreCase))
			{
				flag = true;
			}
		}
		string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yLauncher_v2");
		if (flag)
		{
			PerformInstallation(text2, createDesktop: true, createStartMenu: true, autoStart: false, registerProtocols: true);
		}
		else
		{
			Application.Run(new InstallerForm(text2));
		}
	}

	public static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
	{
		try
		{
			Type typeFromProgID = Type.GetTypeFromProgID("WScript.Shell");
			if (typeFromProgID != null)
			{
				dynamic val = Activator.CreateInstance(typeFromProgID);
				dynamic val2 = val.CreateShortcut(shortcutPath);
				val2.TargetPath = targetPath;
				val2.WorkingDirectory = workingDir;
				val2.IconLocation = iconPath;
				val2.Description = description;
				val2.Save();
			}
		}
		catch
		{
		}
	}

	public static bool ExtractEmbeddedResource(string resourceName, string targetFilePath)
	{
		try
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			using Stream stream = executingAssembly.GetManifestResourceStream(resourceName);
			if (stream != null)
			{
				using (FileStream destination = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write))
				{
					stream.CopyTo(destination);
				}
				return new FileInfo(targetFilePath).Length > 0;
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
			string text = Path.Combine(targetDir, "yLauncher.exe");
			string targetFilePath = Path.Combine(targetDir, "index.html");
			string targetFilePath2 = Path.Combine(targetDir, "yLauncher.ico");
			string targetFilePath3 = Path.Combine(targetDir, "yLauncher.manifest");
			string text2 = Path.Combine(targetDir, "Desinstalar_yLauncher.exe");
			string targetFilePath4 = Path.Combine(targetDir, "installed_games.json");
			bool extracted = ExtractEmbeddedResource("yLauncher.exe", text)
				&& ExtractEmbeddedResource("index.html", targetFilePath)
				&& ExtractEmbeddedResource("yLauncher.ico", targetFilePath2)
				&& ExtractEmbeddedResource("yLauncher.manifest", targetFilePath3)
				&& ExtractEmbeddedResource("Desinstalar_yLauncher.exe", text2);
			if (!File.Exists(targetFilePath4))
			{
				extracted = ExtractEmbeddedResource("installed_games.json", targetFilePath4) && extracted;
			}
			if (!extracted)
			{
				throw new InvalidOperationException("Não foi possível extrair todos os arquivos do aplicativo.");
			}
			if (createDesktop)
			{
				string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
				string shortcutPath = Path.Combine(folderPath, "yLauncher.lnk");
				CreateShortcut(shortcutPath, text, targetDir, text + ",0", "yLauncher 2.1");
			}
			if (createStartMenu)
			{
				string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
				string shortcutPath = Path.Combine(folderPath2, "yLauncher.lnk");
				CreateShortcut(shortcutPath, text, targetDir, text + ",0", "yLauncher 2.1");
				string shortcutPath2 = Path.Combine(folderPath2, "Desinstalar yLauncher.lnk");
				CreateShortcut(shortcutPath2, text2, targetDir, text2 + ",0", "Desinstalar yLauncher");
			}
			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
				if (registryKey != null)
				{
					if (autoStart)
					{
						registryKey.SetValue("yLauncher_v2", "\"" + text + "\"", RegistryValueKind.String);
					}
					else
					{
						registryKey.DeleteValue("yLauncher_v2", throwOnMissingValue: false);
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
					string[] protocols = new string[2] { "ylauncher", "gamehub" };
					foreach (string protocol in protocols)
					{
						using (RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\" + protocol))
						{
							protocolKey.SetValue("", "URL:" + protocol + " Protocol");
							protocolKey.SetValue("URL Protocol", "");
							using (RegistryKey commandKey = protocolKey.CreateSubKey("shell\\open\\command"))
							{
								commandKey.SetValue("", "\"" + text + "\" \"%1\"");
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
				using RegistryKey registryKey2 = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\yLauncher_v2");
				registryKey2.SetValue("DisplayName", "yLauncher 2.1");
				registryKey2.SetValue("DisplayVersion", "2.1.0");
				registryKey2.SetValue("Publisher", "yLauncher Team");
				registryKey2.SetValue("DisplayIcon", text + ",0");
				registryKey2.SetValue("InstallLocation", targetDir);
				registryKey2.SetValue("UninstallString", "\"" + text2 + "\"");
				registryKey2.SetValue("QuietUninstallString", "\"" + text2 + "\" /silent");
				long totalBytes = new FileInfo(text).Length + new FileInfo(targetFilePath).Length + new FileInfo(text2).Length;
				registryKey2.SetValue("EstimatedSize", Math.Max(1, (int)(totalBytes / 1024)), RegistryValueKind.DWord);
				registryKey2.SetValue("NoModify", 1, RegistryValueKind.DWord);
				registryKey2.SetValue("NoRepair", 1, RegistryValueKind.DWord);
			}
			catch
			{
			}
			return true;
		}
		catch
		{
			return false;
		}
	}
}
