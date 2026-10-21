/*
 * PodPilot AAP bridge: interface shared between the kernel driver and user mode.
 * Mirrored in C# by src/PodPilot.Core/Aap/DriverProtocol.cs. Keep both in sync.
 *
 * One device interface exists per paired AirPods (the driver loads on the AirPods' AAP
 * service). User mode opens it, sends CONNECT to open L2CAP PSM 0x1001, then exchanges
 * raw AAP packets with SEND and RECEIVE. RECEIVE pends until the AirPods send a packet.
 */
#pragma once

#include <initguid.h>

// {1e9fa4ac-dd4c-4d83-a2a0-a5767809ccba}
DEFINE_GUID(GUID_DEVINTERFACE_PODPILOT_AAP,
    0x1e9fa4ac, 0xdd4c, 0x4d83, 0xa2, 0xa0, 0xa5, 0x76, 0x78, 0x09, 0xcc, 0xba);

#define PODPILOT_AAP_VERSION          1
#define PODPILOT_AAP_PSM              0x1001
#define PODPILOT_AAP_MAX_PACKET       1024

#define IOCTL_PODPILOT_AAP_GET_INFO   CTL_CODE(FILE_DEVICE_UNKNOWN, 0x800, METHOD_BUFFERED, FILE_READ_ACCESS)
#define IOCTL_PODPILOT_AAP_CONNECT    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x801, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS)
#define IOCTL_PODPILOT_AAP_DISCONNECT CTL_CODE(FILE_DEVICE_UNKNOWN, 0x802, METHOD_BUFFERED, FILE_READ_ACCESS | FILE_WRITE_ACCESS)
#define IOCTL_PODPILOT_AAP_SEND       CTL_CODE(FILE_DEVICE_UNKNOWN, 0x803, METHOD_BUFFERED, FILE_WRITE_ACCESS)
#define IOCTL_PODPILOT_AAP_RECEIVE    CTL_CODE(FILE_DEVICE_UNKNOWN, 0x804, METHOD_BUFFERED, FILE_READ_ACCESS)

#define PODPILOT_AAP_STATE_CLOSED     0
#define PODPILOT_AAP_STATE_OPEN       1

typedef struct _PODPILOT_AAP_INFO {
    ULONG     Version;           // PODPILOT_AAP_VERSION
    ULONG     State;             // PODPILOT_AAP_STATE_*
    ULONGLONG BluetoothAddress;  // remote address, 48 bits
    ULONG     MaxPacketSize;     // PODPILOT_AAP_MAX_PACKET
    ULONG     Reserved;
} PODPILOT_AAP_INFO, *PPODPILOT_AAP_INFO;
