using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Earlo.App.Services;

/// <summary>
/// Turns the Hands-Free service of paired earbuds off and on, which is the fix for the muffled sound
/// many people get on Windows: as soon as an app opens the earbuds' microphone (Teams, Discord, a
/// browser), Windows switches them from stereo music (A2DP) to the Hands-Free profile, which is mono
/// and sounds like a phone call. Without the service Windows can only use stereo, at the price of the
/// earbuds' microphone. It is the "Handsfree Telephony" box under Services in the classic Bluetooth
/// device properties. Changing it can need admin rights; Earlo then runs an elevated copy of itself
/// with --hands-free, like <see cref="DriverInstaller"/>.
/// </summary>
public static class HandsFree
{
    const string Argument = "--hands-free";
    const int ERROR_SUCCESS = 0, ERROR_ACCESS_DENIED = 5, ERROR_NOT_FOUND = 1168;
    const int BLUETOOTH_SERVICE_DISABLE = 0, BLUETOOTH_SERVICE_ENABLE = 1;

    static readonly Guid HandsfreeService = new("0000111E-0000-1000-8000-00805F9B34FB");
    static readonly Guid HeadsetService = new("00001108-0000-1000-8000-00805F9B34FB");

    public static bool IsElevatedCommand(string? argument) => argument == Argument;

    /// <summary>Whether the earbuds' Hands-Free (or Headset) service is on; null when Windows cannot tell.</summary>
    public static bool? IsEnabled(ulong address)
    {
        if (address == 0)
            return null;
        bool? enabled = null;
        try
        {
            OnDevice(address, (radio, info) =>
            {
                var services = new Guid[64];
                uint count = (uint)services.Length;
                int code = BluetoothEnumerateInstalledServices(radio, ref info, ref count, services);
                if (code != ERROR_SUCCESS)
                    return code;
                enabled = services.Take((int)count).Any(s => s == HandsfreeService || s == HeadsetService);
                return ERROR_SUCCESS;
            });
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // No Bluetooth stack.
        }
        return enabled;
    }

    /// <summary>Turns the service on or off, asking for admin rights (UAC) when Windows wants them. False when that failed.</summary>
    public static Task<bool> SetEnabledAsync(ulong address, bool enabled) => Task.Run(() =>
    {
        int code = Apply(address, enabled);
        if (code != ERROR_ACCESS_DENIED)
            return code == ERROR_SUCCESS;
        try
        {
            string state = enabled ? "on" : "off";
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"{Argument} {state} {address:X12}")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (process is null)
                return false;
            process.WaitForExit();
            return process.ExitCode == ERROR_SUCCESS;
        }
        catch (Win32Exception)
        {
            return false; // the user declined the UAC prompt
        }
    });

    /// <summary>Entry point of the elevated copy: --hands-free on|off &lt;address&gt;. Returns the process exit code.</summary>
    public static int RunElevatedCommand(string[] args)
    {
        try
        {
            if (args.Length < 3 || args[1] is not ("on" or "off")
                || !ulong.TryParse(args[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong address))
                return 1;
            return Apply(address, enabled: args[1] == "on");
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return 1;
        }
    }

    /// <returns>0, or the Windows error for the Hands-Free service.</returns>
    static int Apply(ulong address, bool enabled)
    {
        int code = OnDevice(address, (radio, info) =>
        {
            int handsFree = SetService(radio, info, HandsfreeService, enabled);
            SetService(radio, info, HeadsetService, enabled); // most earbuds have no Headset profile
            return handsFree;
        });
        // Windows also answers with an error when the service already was in that state.
        return code != ERROR_SUCCESS && code != ERROR_ACCESS_DENIED && IsEnabled(address) == enabled ? ERROR_SUCCESS : code;
    }

    static int SetService(IntPtr radio, BLUETOOTH_DEVICE_INFO info, Guid service, bool enabled) =>
        BluetoothSetServiceState(radio, ref info, ref service, enabled ? BLUETOOTH_SERVICE_ENABLE : BLUETOOTH_SERVICE_DISABLE);

    /// <summary>Runs the action with the radio the device is paired with.</summary>
    static int OnDevice(ulong address, Func<IntPtr, BLUETOOTH_DEVICE_INFO, int> action)
    {
        var find = new BLUETOOTH_FIND_RADIO_PARAMS { dwSize = Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>() };
        IntPtr search = BluetoothFindFirstRadio(ref find, out IntPtr radio);
        if (search == IntPtr.Zero)
            return ERROR_NOT_FOUND;
        try
        {
            do
            {
                try
                {
                    var info = new BLUETOOTH_DEVICE_INFO { dwSize = Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(), Address = address, szName = string.Empty };
                    if (BluetoothGetDeviceInfo(radio, ref info) == ERROR_SUCCESS)
                        return action(radio, info);
                }
                finally
                {
                    CloseHandle(radio);
                }
            }
            while (BluetoothFindNextRadio(search, out radio));
        }
        finally
        {
            BluetoothFindRadioClose(search);
        }
        return ERROR_NOT_FOUND;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct BLUETOOTH_FIND_RADIO_PARAMS
    {
        public int dwSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEMTIME
    {
        public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct BLUETOOTH_DEVICE_INFO
    {
        public int dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        public int fConnected;
        public int fRemembered;
        public int fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string szName;
    }

    [DllImport("BluetoothApis.dll", SetLastError = true)]
    static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS parameters, out IntPtr radio);

    [DllImport("BluetoothApis.dll", SetLastError = true)]
    static extern bool BluetoothFindNextRadio(IntPtr search, out IntPtr radio);

    [DllImport("BluetoothApis.dll")]
    static extern bool BluetoothFindRadioClose(IntPtr search);

    [DllImport("BluetoothApis.dll")]
    static extern int BluetoothGetDeviceInfo(IntPtr radio, ref BLUETOOTH_DEVICE_INFO info);

    [DllImport("BluetoothApis.dll")]
    static extern int BluetoothSetServiceState(IntPtr radio, ref BLUETOOTH_DEVICE_INFO info, ref Guid service, int flags);

    [DllImport("BluetoothApis.dll")]
    static extern int BluetoothEnumerateInstalledServices(IntPtr radio, ref BLUETOOTH_DEVICE_INFO info, ref uint count, [In, Out] Guid[] services);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
}
