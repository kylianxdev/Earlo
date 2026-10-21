using System.Runtime.InteropServices;

namespace PodPilot.Core.Aap;

/// <summary>
/// The PodPilot AAP bridge driver interface. Mirrors driver/PodPilotAap/Public.h; keep both in sync.
/// </summary>
public static class DriverProtocol
{
    public static readonly Guid InterfaceGuid = new("1e9fa4ac-dd4c-4d83-a2a0-a5767809ccba");

    public const int Version = 1;
    public const int MaxPacket = 1024;

    const uint FileDeviceUnknown = 0x22;
    const uint MethodBuffered = 0;
    const uint FileReadAccess = 1;
    const uint FileWriteAccess = 2;

    public static readonly uint IoctlGetInfo = CtlCode(0x800, FileReadAccess);
    public static readonly uint IoctlConnect = CtlCode(0x801, FileReadAccess | FileWriteAccess);
    public static readonly uint IoctlDisconnect = CtlCode(0x802, FileReadAccess | FileWriteAccess);
    public static readonly uint IoctlSend = CtlCode(0x803, FileWriteAccess);
    public static readonly uint IoctlReceive = CtlCode(0x804, FileReadAccess);

    /// <summary>AQS selector for enabled PodPilot AAP device interfaces.</summary>
    public static string DeviceSelector =>
        $"System.Devices.InterfaceClassGuid:=\"{{{InterfaceGuid}}}\" AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True";

    static uint CtlCode(uint function, uint access) => (FileDeviceUnknown << 16) | (access << 14) | (function << 2) | MethodBuffered;

    [StructLayout(LayoutKind.Sequential)]
    public struct Info
    {
        public uint Version;
        public uint State;
        public ulong BluetoothAddress;
        public uint MaxPacketSize;
        public uint Reserved;
    }
}
