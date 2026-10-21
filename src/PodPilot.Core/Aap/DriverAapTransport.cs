using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PodPilot.Core.Aap;

/// <summary>
/// AAP channel through the PodPilot kernel driver (driver/PodPilotAap), which opens the
/// L2CAP channel that Windows does not expose to normal apps.
/// </summary>
/// <remarks>
/// Uses two synchronous handles: one for the blocking receive loop and one for commands,
/// so a pending receive never holds up a send.
/// </remarks>
public sealed partial class DriverAapTransport(string interfacePath) : IAapTransport
{
    const uint GenericRead = 0x80000000;
    const uint GenericWrite = 0x40000000;
    const uint ShareReadWrite = 0x3;
    const uint OpenExisting = 3;

    SafeFileHandle? _commandHandle;
    SafeFileHandle? _receiveHandle;
    Thread? _receiveThread;
    volatile bool _disposed;

    public string InterfacePath { get; } = interfacePath;

    public event EventHandler<ReadOnlyMemory<byte>>? PacketReceived;
    public event EventHandler? Closed;

    /// <summary>Reads the driver version, channel state and Bluetooth address.</summary>
    public DriverProtocol.Info GetInfo()
    {
        using var handle = Open();
        var buffer = new byte[Marshal.SizeOf<DriverProtocol.Info>()];
        Ioctl(handle, DriverProtocol.IoctlGetInfo, [], buffer);
        return MemoryMarshal.Read<DriverProtocol.Info>(buffer);
    }

    public Task OpenAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _commandHandle ??= Open();
        Ioctl(_commandHandle, DriverProtocol.IoctlConnect, [], []);

        _receiveHandle ??= Open();
        _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "PodPilot AAP receive" };
        _receiveThread.Start();
    }, cancellationToken);

    public Task SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var handle = _commandHandle ?? throw new InvalidOperationException("The AAP channel is not open.");
        Ioctl(handle, DriverProtocol.IoctlSend, packet.ToArray(), []);
    }, cancellationToken);

    void ReceiveLoop()
    {
        var buffer = new byte[DriverProtocol.MaxPacket];
        try
        {
            while (!_disposed && _receiveHandle is { IsInvalid: false } handle)
            {
                int length = Ioctl(handle, DriverProtocol.IoctlReceive, [], buffer);
                if (length > 0)
                    PacketReceived?.Invoke(this, buffer.AsSpan(0, length).ToArray());
            }
        }
        catch (Win32Exception)
        {
            // Channel closed, AirPods disconnected or handle cancelled.
        }
        catch (ObjectDisposedException)
        {
        }
        Closed?.Invoke(this, EventArgs.Empty);
    }

    SafeFileHandle Open()
    {
        var handle = CreateFileW(InterfacePath, GenericRead | GenericWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Cannot open the PodPilot AAP driver.");
        return handle;
    }

    static unsafe int Ioctl(SafeFileHandle handle, uint code, byte[] input, byte[] output)
    {
        fixed (byte* inPtr = input)
        fixed (byte* outPtr = output)
        {
            if (!DeviceIoControl(handle, code, inPtr, (uint)input.Length, outPtr, (uint)output.Length, out uint returned, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            return (int)returned;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_receiveHandle is { IsInvalid: false } receive)
            CancelIoEx(receive, IntPtr.Zero);
        if (_commandHandle is { IsInvalid: false } command)
        {
            try { Ioctl(command, DriverProtocol.IoctlDisconnect, [], []); } catch (Win32Exception) { }
        }

        _receiveThread?.Join(TimeSpan.FromSeconds(2));
        _receiveHandle?.Dispose();
        _commandHandle?.Dispose();
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool DeviceIoControl(SafeFileHandle device, uint code, byte* input, uint inputSize, byte* output, uint outputSize, out uint returned, IntPtr overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);
}
