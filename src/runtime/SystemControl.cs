using System;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Controles do sistema para o menu rápido (volume, brilho e energia),
/// pensados principalmente para portáteis como Legion Go e ROG Ally.
/// </summary>
internal static class SystemControl
{
	// ── Volume (Core Audio) ─────────────────────────────────
	[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
	private class MMDeviceEnumeratorComObject
	{
	}

	[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDeviceEnumerator
	{
		int NotImpl1();
		[PreserveSig]
		int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
	}

	[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IMMDevice
	{
		[PreserveSig]
		int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
	}

	[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IAudioEndpointVolume
	{
		int RegisterControlChangeNotify(IntPtr notify);
		int UnregisterControlChangeNotify(IntPtr notify);
		int GetChannelCount(out uint count);
		int SetMasterVolumeLevel(float levelDb, ref Guid context);
		int SetMasterVolumeLevelScalar(float level, ref Guid context);
		int GetMasterVolumeLevel(out float levelDb);
		int GetMasterVolumeLevelScalar(out float level);
		int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
		int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
		int GetChannelVolumeLevel(uint channel, out float levelDb);
		int GetChannelVolumeLevelScalar(uint channel, out float level);
		int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
		int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
	}

	private static IAudioEndpointVolume GetEndpointVolume()
	{
		var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
		IMMDevice device;
		Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device));
		Guid iid = typeof(IAudioEndpointVolume).GUID;
		object endpoint;
		Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out endpoint));
		return (IAudioEndpointVolume)endpoint;
	}

	/// <summary>Altera o volume. set: 0-100; delta: soma; mute: "toggle"|"1"|"0".</summary>
	public static string Volume(string set, string delta, string mute)
	{
		try
		{
			IAudioEndpointVolume vol = GetEndpointVolume();
			Guid ctx = Guid.Empty;
			float level;
			vol.GetMasterVolumeLevelScalar(out level);
			int value;
			if (int.TryParse(set, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
			{
				level = Math.Max(0, Math.Min(100, value)) / 100f;
				vol.SetMasterVolumeLevelScalar(level, ref ctx);
				vol.SetMute(false, ref ctx);
			}
			else if (int.TryParse(delta, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
			{
				level = Math.Max(0f, Math.Min(1f, level + value / 100f));
				vol.SetMasterVolumeLevelScalar(level, ref ctx);
				vol.SetMute(false, ref ctx);
			}
			bool muted;
			vol.GetMute(out muted);
			if (!string.IsNullOrEmpty(mute))
			{
				bool target = mute == "toggle" ? !muted : mute == "1";
				vol.SetMute(target, ref ctx);
				muted = target;
			}
			vol.GetMasterVolumeLevelScalar(out level);
			return "{\"success\":true,\"volume\":" + (int)Math.Round(level * 100) + ",\"muted\":" + (muted ? "true" : "false") + "}";
		}
		catch (Exception ex)
		{
			return Json.Error("Volume indisponível: " + ex.Message);
		}
	}

	// ── Brilho (WMI — telas internas de notebooks e portáteis) ──
	public static string Brightness(string set, string delta)
	{
		try
		{
			int current = -1;
			using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
			{
				foreach (ManagementObject mo in searcher.Get())
				{
					current = Convert.ToInt32(mo["CurrentBrightness"], CultureInfo.InvariantCulture);
					break;
				}
			}
			if (current < 0)
			{
				return "{\"success\":false,\"supported\":false}";
			}
			int target = -1;
			int value;
			if (int.TryParse(set, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
			{
				target = value;
			}
			else if (int.TryParse(delta, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
			{
				target = current + value;
			}
			if (target >= 0)
			{
				target = Math.Max(5, Math.Min(100, target));
				using (var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
				{
					foreach (ManagementObject mo in searcher.Get())
					{
						mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)target });
						break;
					}
				}
				current = target;
			}
			return "{\"success\":true,\"supported\":true,\"brightness\":" + current + "}";
		}
		catch
		{
			return "{\"success\":false,\"supported\":false}";
		}
	}

	// ── Energia ─────────────────────────────────────────────
	[DllImport("powrprof.dll", SetLastError = true)]
	private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

	public static string Power(string action)
	{
		try
		{
			switch ((action ?? "").ToLowerInvariant())
			{
				case "sleep":
					SetSuspendState(false, false, false);
					return "{\"success\":true}";
				case "restart":
					StartHidden("shutdown.exe", "/r /t 3");
					return "{\"success\":true}";
				case "shutdown":
					StartHidden("shutdown.exe", "/s /t 3");
					return "{\"success\":true}";
				case "lock":
					StartHidden("rundll32.exe", "user32.dll,LockWorkStation");
					return "{\"success\":true}";
				default:
					return Json.Error("Ação desconhecida.");
			}
		}
		catch (Exception ex)
		{
			return Json.Error(ex.Message);
		}
	}

	private static void StartHidden(string file, string args)
	{
		Process.Start(new ProcessStartInfo(file, args) { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
	}
}
