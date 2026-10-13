using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class UninstallProgram
{
	[STAThread]
	private static void Main(string[] args)
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		bool silent = false;
		bool purgeUserData = false;
		foreach (string argument in args)
		{
			if (argument.Equals("/silent", StringComparison.OrdinalIgnoreCase)
				|| argument.Equals("/s", StringComparison.OrdinalIgnoreCase)
				|| argument.Equals("-silent", StringComparison.OrdinalIgnoreCase))
			{
				silent = true;
			}
			if (argument.Equals("/purge", StringComparison.OrdinalIgnoreCase))
			{
				purgeUserData = true;
			}
		}

		if (silent)
		{
			PerformUninstall(purgeUserData);
			return;
		}
		Application.Run(new UninstallForm());
	}

	public static void PerformUninstall(bool deleteUserData)
	{
		StopLauncherProcesses();
		RemoveShortcuts();
		RemoveRegistryEntries();
		ScheduleFileCleanup(deleteUserData);
	}

	private static void StopLauncherProcesses()
	{
		try
		{
			int currentProcessId = Process.GetCurrentProcess().Id;
			foreach (Process process in Process.GetProcessesByName("yLauncher"))
			{
				if (process.Id == currentProcessId) continue;
				try
				{
					process.Kill();
					process.WaitForExit(1500);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}

		string profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yLauncher", "BrowserProfile");
		try
		{
			using (var search = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'msedge.exe'"))
			{
				foreach (ManagementBaseObject item in search.Get())
				{
					string commandLine = item["CommandLine"] as string;
					if (string.IsNullOrEmpty(commandLine) || commandLine.IndexOf(profileDir, StringComparison.OrdinalIgnoreCase) < 0) continue;
					try
					{
						Process.GetProcessById(Convert.ToInt32(item["ProcessId"])).Kill();
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

	private static void RemoveShortcuts()
	{
		string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
		string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
		string[] shortcutPaths =
		{
			Path.Combine(desktop, "yLauncher.lnk"),
			Path.Combine(desktop, "yLauncher v2.0 Pro.lnk"),
			Path.Combine(startMenu, "yLauncher.lnk"),
			Path.Combine(startMenu, "yLauncher v2.0 Pro.lnk"),
			Path.Combine(startMenu, "Desinstalar yLauncher.lnk"),
			Path.Combine(startMenu, "Desinstalar yLauncher v2.0 Pro.lnk")
		};
		foreach (string shortcutPath in shortcutPaths)
		{
			try
			{
				if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
			}
			catch
			{
			}
		}
	}

	private static void RemoveRegistryEntries()
	{
		try
		{
			using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
			{
				if (runKey != null) runKey.DeleteValue("yLauncher_v2", false);
			}
		}
		catch
		{
		}
		try
		{
			Registry.CurrentUser.DeleteSubKeyTree("Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\yLauncher_v2", false);
		}
		catch
		{
		}

		RemoveProtocolIfOwned("ylauncher");
		RemoveProtocolIfOwned("gamehub");
	}

	private static void RemoveProtocolIfOwned(string protocol)
	{
		try
		{
			string keyPath = "Software\\Classes\\" + protocol;
			using (RegistryKey commandKey = Registry.CurrentUser.OpenSubKey(keyPath + "\\shell\\open\\command"))
			{
				string command = commandKey == null ? null : commandKey.GetValue(null) as string;
				if (string.IsNullOrEmpty(command) || command.IndexOf("yLauncher_v2", StringComparison.OrdinalIgnoreCase) < 0) return;
			}
			Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
		}
		catch
		{
		}
	}

	private static void ScheduleFileCleanup(bool deleteUserData)
	{
		string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string userDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "yLauncher");
		string cleanupScript = Path.Combine(Path.GetTempPath(), "ylauncher_uninstall_" + Process.GetCurrentProcess().Id + ".bat");

		try
		{
			var script = new StringBuilder();
			script.AppendLine("@echo off");
			script.AppendLine("chcp 65001 >nul");
			script.AppendLine("timeout /t 2 /nobreak >nul");
			if (deleteUserData)
			{
				script.AppendLine("if exist \"" + installDir + "\" rmdir /s /q \"" + installDir + "\"");
				script.AppendLine("if exist \"" + userDataDir + "\" rmdir /s /q \"" + userDataDir + "\"");
			}
			else
			{
				string[] appFiles = { "yLauncher.exe", "index.html", "yLauncher.ico", "yLauncher.manifest", "Desinstalar_yLauncher.exe" };
				foreach (string appFile in appFiles)
				{
					script.AppendLine("if exist \"" + Path.Combine(installDir, appFile) + "\" del /f /q \"" + Path.Combine(installDir, appFile) + "\"");
				}
			}
			script.AppendLine("del /f /q \"%~f0\"");
			File.WriteAllText(cleanupScript, script.ToString(), Encoding.Default);

			Process.Start(new ProcessStartInfo
			{
				FileName = cleanupScript,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				UseShellExecute = true
			});
		}
		catch
		{
		}
	}
}
