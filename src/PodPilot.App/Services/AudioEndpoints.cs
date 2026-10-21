using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PodPilot.Core.Bluetooth;

namespace PodPilot.App.Services;

/// <summary>
/// The Windows audio endpoints of paired Bluetooth headphones. Connects and disconnects them the way
/// the Bluetooth settings page does (a one-shot reconnect on the Bluetooth audio driver), and watches
/// their volume so a stem swipe can be shown on screen.
/// </summary>
public static class AudioEndpoints
{
    const int DEVICE_STATE_ACTIVE = 0x1, DEVICE_STATE_UNPLUGGED = 0x8;
    const int eRender = 0, eAll = 2;
    const int CLSCTX_ALL = 0x17;
    const int KSPROPERTY_ONESHOT_RECONNECT = 0, KSPROPERTY_ONESHOT_DISCONNECT = 1;
    const int KSPROPERTY_TYPE_GET = 1;

    static readonly Guid KSPROPSETID_BtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    static readonly PROPERTYKEY PKEY_DeviceInterface_FriendlyName = new(new Guid("026e516e-b814-414b-83cd-856d6fef4822"), 2);
    static readonly PROPERTYKEY PKEY_Device_ContainerId = new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);

    /// <summary>Asks Windows to connect the headphones' audio. Returns false if no endpoint accepted it.</summary>
    public static bool Connect(PairedDeviceInfo device) => SendOneShot(device, KSPROPERTY_ONESHOT_RECONNECT);

    /// <summary>Disconnects the headphones' audio, like Disconnect in Bluetooth settings.</summary>
    public static bool Disconnect(PairedDeviceInfo device) => SendOneShot(device, KSPROPERTY_ONESHOT_DISCONNECT);

    /// <summary>
    /// Sends the request to every endpoint of the device, speakers and microphone: music (A2DP) and
    /// calls (Hands-Free) are separate links, and the device only counts as disconnected once both are.
    /// </summary>
    static bool SendOneShot(PairedDeviceInfo target, int property)
    {
        bool accepted = false;
        foreach (var device in Find(target, eAll, DEVICE_STATE_ACTIVE | DEVICE_STATE_UNPLUGGED))
        {
            try
            {
                if (KsControlFor(device) is not { } control)
                    continue;
                var request = new KSPROPERTY { Set = KSPROPSETID_BtAudio, Id = property, Flags = KSPROPERTY_TYPE_GET };
                if (control.KsProperty(ref request, Marshal.SizeOf<KSPROPERTY>(), IntPtr.Zero, 0, out _) >= 0)
                    accepted = true;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException)
            {
                // Not a Bluetooth audio endpoint (E_NOINTERFACE shows up as InvalidCastException), try the next one.
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        return accepted;
    }

    /// <summary>
    /// Starts watching the volume of the headphones' active endpoint. Null when it has none yet
    /// (it appears a few seconds after connecting).
    /// </summary>
    public static VolumeWatcher? WatchVolume(PairedDeviceInfo target, Action<float, bool> changed)
    {
        foreach (var device in Find(target, eRender, DEVICE_STATE_ACTIVE))
        {
            try
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var obj);
                return new VolumeWatcher((IAudioEndpointVolume)obj, changed);
            }
            catch (COMException)
            {
            }
            finally
            {
                Marshal.ReleaseComObject(device);
            }
        }
        return null;
    }

    /// <summary>Render endpoints (in the given states) that belong to the Bluetooth device.</summary>
    static List<IMMDevice> Find(PairedDeviceInfo target, int flow, int states)
    {
        var result = new List<IMMDevice>();
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.EnumAudioEndpoints(flow, states, out collection) < 0)
                return result;
            collection.GetCount(out int count);
            for (int i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) < 0)
                    continue;
                if (Matches(device, target))
                    result.Add(device);
                else
                    Marshal.ReleaseComObject(device);
            }
        }
        catch (COMException)
        {
        }
        finally
        {
            if (collection is not null)
                Marshal.ReleaseComObject(collection);
            if (enumerator is not null)
                Marshal.ReleaseComObject(enumerator);
        }
        return result;
    }

    static bool Matches(IMMDevice device, PairedDeviceInfo target)
    {
        if (device.OpenPropertyStore(0, out var store) < 0)
            return false;
        try
        {
            // Windows groups a Bluetooth device and its audio endpoints in one container. The names
            // can differ ("AirPods Pro" vs "Headphones (AirPods Pro - Find My)"), so only fall back to them.
            if (target.ContainerId is { } container)
                return ReadGuid(store, PKEY_Device_ContainerId) == container;
            string? interfaceName = ReadString(store, PKEY_DeviceInterface_FriendlyName);
            return interfaceName is not null && interfaceName.StartsWith(target.Name, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    static Guid? ReadGuid(IPropertyStore store, PROPERTYKEY key)
    {
        var value = new PROPVARIANT();
        try
        {
            const ushort VT_CLSID = 72;
            if (store.GetValue(ref key, out value) < 0 || value.vt != VT_CLSID || value.pointer == IntPtr.Zero)
                return null;
            return Marshal.PtrToStructure<Guid>(value.pointer);
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    static string? ReadString(IPropertyStore store, PROPERTYKEY key)
    {
        var value = new PROPVARIANT();
        try
        {
            if (store.GetValue(ref key, out value) < 0)
                return null;
            const ushort VT_LPWSTR = 31;
            return value.vt == VT_LPWSTR ? Marshal.PtrToStringUni(value.pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    /// <summary>The kernel streaming control of the Bluetooth audio filter behind an endpoint.</summary>
    static IKsControl? KsControlFor(IMMDevice endpoint)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        var iid = typeof(IDeviceTopology).GUID;
        endpoint.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var obj);
        var topology = (IDeviceTopology)obj;
        topology.GetConnector(0, out var connector);
        // The endpoint's connector leads to the Bluetooth driver's filter; its device hands out IKsControl.
        connector.GetDeviceIdConnectedTo(out var filterId);
        int hr = enumerator.GetDevice(filterId, out var filter);
        Marshal.ReleaseComObject(enumerator);
        if (hr < 0)
            return null;
        try
        {
            var ksIid = typeof(IKsControl).GUID;
            filter.Activate(ref ksIid, CLSCTX_ALL, IntPtr.Zero, out var control);
            return control as IKsControl;
        }
        finally
        {
            Marshal.ReleaseComObject(filter);
        }
    }

    public sealed class VolumeWatcher : IDisposable
    {
        readonly IAudioEndpointVolume _volume;
        readonly Callback _callback;

        internal VolumeWatcher(IAudioEndpointVolume volume, Action<float, bool> changed)
        {
            _volume = volume;
            _callback = new Callback(changed);
            Marshal.ThrowExceptionForHR(_volume.RegisterControlChangeNotify(_callback));
        }

        public void Dispose()
        {
            try
            {
                _volume.UnregisterControlChangeNotify(_callback);
            }
            catch (COMException)
            {
            }
            Marshal.ReleaseComObject(_volume);
        }

        /// <summary>
        /// Windows and apps pass their own event context when they set the volume (keyboard keys,
        /// the volume flyout, the mixer). A change from the headphones (AVRCP absolute volume) has none.
        /// </summary>
        sealed class Callback(Action<float, bool> changed) : IAudioEndpointVolumeCallback
        {
            public int OnNotify(IntPtr data)
            {
                var notification = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(data);
                if (notification.guidEventContext == Guid.Empty)
                    changed(notification.fMasterVolume, notification.bMuted != 0);
                return 0;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        public int bMuted;
        public float fMasterVolume;
        public uint nChannels;
    }

    [StructLayout(LayoutKind.Sequential)]
    readonly struct PROPERTYKEY(Guid fmtid, int pid)
    {
        readonly Guid _fmtid = fmtid;
        readonly int _pid = pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KSPROPERTY
    {
        public Guid Set;
        public int Id;
        public int Flags;
    }

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PROPVARIANT value);

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int Item(int index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr notifyData);
    }

    [ComImport, Guid("2A07407E-6497-4A18-9787-32F79BD0D98F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDeviceTopology
    {
        void GetConnectorCount(out int count);
        void GetConnector(int index, out IConnector connector);
    }

    [ComImport, Guid("9c2c4058-23f5-41de-877a-df3af236a09e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IConnector
    {
        void GetConnectorType(out int type);
        void GetDataFlow(out int flow);
        void ConnectTo(IConnector other);
        void Disconnect();
        void IsConnected(out int connected);
        void GetConnectedTo(out IConnector other);
        void GetConnectorIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetDeviceIdConnectedTo([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("28F54685-06FD-11D2-B27A-00A0C9223196"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IKsControl
    {
        [PreserveSig] int KsProperty(ref KSPROPERTY property, int propertyLength, IntPtr data, int dataLength, out int bytesReturned);
    }
}
