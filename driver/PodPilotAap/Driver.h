#pragma once

#include <ntddk.h>
#include <wdf.h>
#include <initguid.h>
#include <bthdef.h>
#include <ntintsafe.h>
#include <bthguid.h>
#include <bthioctl.h>
#include <sdpnode.h>
#include <bthddi.h>
#include <bthsdpddi.h>
#include <bthsdpdef.h>

#include "Public.h"

#define POOL_TAG        'aPpP'
#define RING_CAPACITY   16

typedef struct _AAP_PACKET {
    ULONG Length;
    UCHAR Data[PODPILOT_AAP_MAX_PACKET];
} AAP_PACKET, *PAAP_PACKET;

typedef struct _DEVICE_CONTEXT {
    WDFDEVICE                    Device;
    WDFIOTARGET                  IoTarget;
    BTH_PROFILE_DRIVER_INTERFACE Bth;
    BTH_ADDR                     RemoteAddress;

    // Serialises connect, disconnect and send (all at PASSIVE_LEVEL).
    WDFWAITLOCK                  ControlLock;
    // Protects the fields below; taken at up to DISPATCH_LEVEL.
    WDFSPINLOCK                  Lock;

    BOOLEAN                      Open;
    L2CAP_CHANNEL_HANDLE         Channel;

    // Pending RECEIVE requests wait here until a packet arrives.
    WDFQUEUE                     ReceiveQueue;

    // One inbound ACL transfer is kept outstanding while the channel is open.
    WDFREQUEST                   ReadRequest;
    WDFMEMORY                    ReadBrbMemory;
    struct _BRB_L2CA_ACL_TRANSFER ReadBrb;
    UCHAR                        ReadBuffer[PODPILOT_AAP_MAX_PACKET];
    BOOLEAN                      ReadActive;
    KEVENT                       ReadIdle;

    // Packets that arrived while no RECEIVE request was waiting.
    AAP_PACKET                   Ring[RING_CAPACITY];
    ULONG                        RingHead;
    ULONG                        RingCount;

    WDFWORKITEM                  DisconnectWorkItem;
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, GetDeviceContext)

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD PpAapEvtDeviceAdd;
EVT_WDF_DEVICE_SELF_MANAGED_IO_INIT PpAapEvtSelfManagedIoInit;
EVT_WDF_DEVICE_SELF_MANAGED_IO_CLEANUP PpAapEvtSelfManagedIoCleanup;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL PpAapEvtIoDeviceControl;
EVT_WDF_REQUEST_COMPLETION_ROUTINE PpAapReadCompletion;
EVT_WDF_WORKITEM PpAapDisconnectWorkItem;

_IRQL_requires_(PASSIVE_LEVEL)
NTSTATUS PpAapOpenChannel(_In_ PDEVICE_CONTEXT Ctx);

_IRQL_requires_(PASSIVE_LEVEL)
VOID PpAapCloseChannel(_In_ PDEVICE_CONTEXT Ctx);

_IRQL_requires_(PASSIVE_LEVEL)
NTSTATUS PpAapSend(_In_ PDEVICE_CONTEXT Ctx, _In_reads_bytes_(Length) PVOID Buffer, _In_ ULONG Length);

_IRQL_requires_max_(DISPATCH_LEVEL)
VOID PpAapIndicationCallback(_In_ PVOID Context, _In_ INDICATION_CODE Indication, _In_ PINDICATION_PARAMETERS Parameters);
