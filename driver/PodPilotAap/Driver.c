/*
 * PodPilot AAP bridge driver.
 *
 * A KMDF Bluetooth profile driver that loads on the AirPods' vendor service (BTHENUM) and
 * opens Apple's AAP channel (L2CAP PSM 0x1001), which Windows does not expose to user mode.
 * It only moves raw packets between the channel and user mode; all protocol logic lives in
 * PodPilot (src/PodPilot.Core/Aap).
 *
 * Structure follows Microsoft's Bluetooth echo profile driver sample (bthecho).
 */

#include "Driver.h"

#ifdef ALLOC_PRAGMA
#pragma alloc_text(INIT, DriverEntry)
#pragma alloc_text(PAGE, PpAapEvtDeviceAdd)
#endif

NTSTATUS
DriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, PpAapEvtDeviceAdd);
    return WdfDriverCreate(DriverObject, RegistryPath, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}

NTSTATUS
PpAapEvtDeviceAdd(_In_ WDFDRIVER Driver, _Inout_ PWDFDEVICE_INIT DeviceInit)
{
    NTSTATUS status;
    WDFDEVICE device;
    PDEVICE_CONTEXT ctx;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_PNPPOWER_EVENT_CALLBACKS pnp;
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_WORKITEM_CONFIG workItemConfig;

    UNREFERENCED_PARAMETER(Driver);
    PAGED_CODE();

    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&pnp);
    pnp.EvtDeviceSelfManagedIoInit = PpAapEvtSelfManagedIoInit;
    pnp.EvtDeviceSelfManagedIoCleanup = PpAapEvtSelfManagedIoCleanup;
    WdfDeviceInitSetPnpPowerEventCallbacks(DeviceInit, &pnp);

    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, DEVICE_CONTEXT);
    status = WdfDeviceCreate(&DeviceInit, &attributes, &device);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    ctx = GetDeviceContext(device);
    ctx->Device = device;
    ctx->IoTarget = WdfDeviceGetIoTarget(device);
    KeInitializeEvent(&ctx->ReadIdle, NotificationEvent, TRUE);

    status = WdfFdoQueryForInterface(device,
                                     &GUID_BTHDDI_PROFILE_DRIVER_INTERFACE,
                                     (PINTERFACE)&ctx->Bth,
                                     sizeof(ctx->Bth),
                                     BTHDDI_PROFILE_DRIVER_INTERFACE_VERSION_FOR_QI,
                                     NULL);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ParentObject = device;
    status = WdfWaitLockCreate(&attributes, &ctx->ControlLock);
    if (!NT_SUCCESS(status)) {
        return status;
    }
    status = WdfSpinLockCreate(&attributes, &ctx->Lock);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    status = WdfRequestCreate(&attributes, ctx->IoTarget, &ctx->ReadRequest);
    if (!NT_SUCCESS(status)) {
        return status;
    }
    status = WdfMemoryCreatePreallocated(&attributes, &ctx->ReadBrb, sizeof(ctx->ReadBrb), &ctx->ReadBrbMemory);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WDF_WORKITEM_CONFIG_INIT(&workItemConfig, PpAapDisconnectWorkItem);
    status = WdfWorkItemCreate(&workItemConfig, &attributes, &ctx->DisconnectWorkItem);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    // Default queue: IOCTLs from user mode, handled at PASSIVE_LEVEL in the caller's thread.
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchParallel);
    queueConfig.EvtIoDeviceControl = PpAapEvtIoDeviceControl;
    status = WdfIoQueueCreate(device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    // Manual queue for RECEIVE requests that wait for a packet.
    WDF_IO_QUEUE_CONFIG_INIT(&queueConfig, WdfIoQueueDispatchManual);
    status = WdfIoQueueCreate(device, &queueConfig, WDF_NO_OBJECT_ATTRIBUTES, &ctx->ReceiveQueue);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    return WdfDeviceCreateDeviceInterface(device, &GUID_DEVINTERFACE_PODPILOT_AAP, NULL);
}

/* Reads the AirPods' Bluetooth address from BTHENUM once the device has started. */
NTSTATUS
PpAapEvtSelfManagedIoInit(_In_ WDFDEVICE Device)
{
    PDEVICE_CONTEXT ctx = GetDeviceContext(Device);
    BTH_DEVICE_INFO info;
    WDF_MEMORY_DESCRIPTOR output;
    NTSTATUS status;

    RtlZeroMemory(&info, sizeof(info));
    WDF_MEMORY_DESCRIPTOR_INIT_BUFFER(&output, &info, sizeof(info));

    status = WdfIoTargetSendInternalIoctlSynchronously(ctx->IoTarget, NULL, IOCTL_INTERNAL_BTHENUM_GET_DEVINFO,
                                                       NULL, &output, NULL, NULL);
    if (NT_SUCCESS(status)) {
        ctx->RemoteAddress = info.address;
    }
    return status;
}

VOID
PpAapEvtSelfManagedIoCleanup(_In_ WDFDEVICE Device)
{
    PpAapCloseChannel(GetDeviceContext(Device));
}

/* --- Packet delivery ---------------------------------------------------------------- */

static VOID
PpAapDeliver(_In_ PDEVICE_CONTEXT Ctx, _In_reads_bytes_(Length) const UCHAR* Data, _In_ ULONG Length)
{
    WDFREQUEST request = NULL;
    PVOID buffer;
    size_t bufferSize;
    NTSTATUS status;

    if (Length > PODPILOT_AAP_MAX_PACKET) {
        Length = PODPILOT_AAP_MAX_PACKET;
    }

    WdfSpinLockAcquire(Ctx->Lock);
    if (!NT_SUCCESS(WdfIoQueueRetrieveNextRequest(Ctx->ReceiveQueue, &request))) {
        // Nobody is waiting: keep the packet, dropping the oldest when full.
        ULONG index = (Ctx->RingHead + Ctx->RingCount) % RING_CAPACITY;
        if (Ctx->RingCount == RING_CAPACITY) {
            index = Ctx->RingHead;
            Ctx->RingHead = (Ctx->RingHead + 1) % RING_CAPACITY;
        } else {
            Ctx->RingCount++;
        }
        Ctx->Ring[index].Length = Length;
        RtlCopyMemory(Ctx->Ring[index].Data, Data, Length);
        request = NULL;
    }
    WdfSpinLockRelease(Ctx->Lock);

    if (request != NULL) {
        status = WdfRequestRetrieveOutputBuffer(request, 1, &buffer, &bufferSize);
        if (NT_SUCCESS(status)) {
            ULONG copy = (ULONG)min(bufferSize, (size_t)Length);
            RtlCopyMemory(buffer, Data, copy);
            WdfRequestCompleteWithInformation(request, STATUS_SUCCESS, copy);
        } else {
            WdfRequestComplete(request, status);
        }
    }
}

static VOID
PpAapFailPendingReceives(_In_ PDEVICE_CONTEXT Ctx)
{
    WDFREQUEST request;
    while (NT_SUCCESS(WdfIoQueueRetrieveNextRequest(Ctx->ReceiveQueue, &request))) {
        WdfRequestComplete(request, STATUS_DEVICE_NOT_CONNECTED);
    }
}

/* --- Inbound transfers ---------------------------------------------------------------- */

static NTSTATUS
PpAapSubmitRead(_In_ PDEVICE_CONTEXT Ctx)
{
    WDF_REQUEST_REUSE_PARAMS reuse;
    NTSTATUS status;

    WDF_REQUEST_REUSE_PARAMS_INIT(&reuse, WDF_REQUEST_REUSE_NO_FLAGS, STATUS_NOT_SUPPORTED);
    WdfRequestReuse(Ctx->ReadRequest, &reuse);

    Ctx->Bth.BthReuseBrb((PBRB)&Ctx->ReadBrb, BRB_L2CA_ACL_TRANSFER);
    Ctx->ReadBrb.BtAddress = Ctx->RemoteAddress;
    Ctx->ReadBrb.ChannelHandle = Ctx->Channel;
    Ctx->ReadBrb.TransferFlags = ACL_TRANSFER_DIRECTION_IN | ACL_SHORT_TRANSFER_OK;
    Ctx->ReadBrb.BufferSize = sizeof(Ctx->ReadBuffer);
    Ctx->ReadBrb.Buffer = Ctx->ReadBuffer;
    Ctx->ReadBrb.BufferMDL = NULL;
    Ctx->ReadBrb.Timeout = 0;

    status = WdfIoTargetFormatRequestForInternalIoctlOthers(Ctx->IoTarget, Ctx->ReadRequest,
                                                            IOCTL_INTERNAL_BTH_SUBMIT_BRB,
                                                            Ctx->ReadBrbMemory, NULL, NULL, NULL, NULL, NULL);
    if (!NT_SUCCESS(status)) {
        return status;
    }

    WdfRequestSetCompletionRoutine(Ctx->ReadRequest, PpAapReadCompletion, Ctx);
    KeClearEvent(&Ctx->ReadIdle);
    Ctx->ReadActive = TRUE;
    if (!WdfRequestSend(Ctx->ReadRequest, Ctx->IoTarget, WDF_NO_SEND_OPTIONS)) {
        Ctx->ReadActive = FALSE;
        KeSetEvent(&Ctx->ReadIdle, IO_NO_INCREMENT, FALSE);
        return WdfRequestGetStatus(Ctx->ReadRequest);
    }
    return STATUS_SUCCESS;
}

VOID
PpAapReadCompletion(_In_ WDFREQUEST Request, _In_ WDFIOTARGET Target,
                    _In_ PWDF_REQUEST_COMPLETION_PARAMS Params, _In_ WDFCONTEXT Context)
{
    PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)Context;
    NTSTATUS status = Params->IoStatus.Status;
    BOOLEAN open;

    UNREFERENCED_PARAMETER(Request);
    UNREFERENCED_PARAMETER(Target);

    if (NT_SUCCESS(status) && ctx->ReadBrb.BufferSize > 0) {
        // On completion BufferSize holds the number of bytes received.
        PpAapDeliver(ctx, ctx->ReadBuffer, ctx->ReadBrb.BufferSize);
    }

    WdfSpinLockAcquire(ctx->Lock);
    open = ctx->Open;
    WdfSpinLockRelease(ctx->Lock);

    if (NT_SUCCESS(status) && open && NT_SUCCESS(PpAapSubmitRead(ctx))) {
        return;
    }

    ctx->ReadActive = FALSE;
    KeSetEvent(&ctx->ReadIdle, IO_NO_INCREMENT, FALSE);

    if (!NT_SUCCESS(status) && status != STATUS_CANCELLED && open) {
        // The channel failed underneath us; clean up at PASSIVE_LEVEL.
        WdfWorkItemEnqueue(ctx->DisconnectWorkItem);
    }
}

/* --- Channel management ------------------------------------------------------------- */

static NTSTATUS
PpAapSubmitBrbSync(_In_ PDEVICE_CONTEXT Ctx, _In_ PBRB Brb, _In_ ULONG Size)
{
    WDF_MEMORY_DESCRIPTOR desc;
    WDF_MEMORY_DESCRIPTOR_INIT_BUFFER(&desc, Brb, Size);
    return WdfIoTargetSendInternalIoctlOthersSynchronously(Ctx->IoTarget, NULL, IOCTL_INTERNAL_BTH_SUBMIT_BRB,
                                                           &desc, NULL, NULL, NULL, NULL);
}

NTSTATUS
PpAapOpenChannel(_In_ PDEVICE_CONTEXT Ctx)
{
    struct _BRB_L2CA_OPEN_CHANNEL* brb;
    NTSTATUS status;

    WdfWaitLockAcquire(Ctx->ControlLock, NULL);
    if (Ctx->Open) {
        WdfWaitLockRelease(Ctx->ControlLock);
        return STATUS_SUCCESS;
    }
    if (Ctx->RemoteAddress == 0) {
        WdfWaitLockRelease(Ctx->ControlLock);
        return STATUS_DEVICE_NOT_READY;
    }

    brb = (struct _BRB_L2CA_OPEN_CHANNEL*)Ctx->Bth.BthAllocateBrb(BRB_L2CA_OPEN_CHANNEL, POOL_TAG);
    if (brb == NULL) {
        WdfWaitLockRelease(Ctx->ControlLock);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    brb->BtAddress = Ctx->RemoteAddress;
    brb->Psm = PODPILOT_AAP_PSM;
    brb->ChannelFlags = CF_ROLE_EITHER;
    brb->ConfigOut.Flags = 0;
    brb->ConfigIn.Flags = 0;
    brb->CallbackFlags = CALLBACK_DISCONNECT;
    brb->Callback = PpAapIndicationCallback;
    brb->CallbackContext = Ctx;
    brb->ReferenceObject = (PVOID)WdfDeviceWdmGetDeviceObject(Ctx->Device);
    brb->IncomingQueueDepth = 50;

    status = PpAapSubmitBrbSync(Ctx, (PBRB)brb, sizeof(*brb));
    if (NT_SUCCESS(status)) {
        WdfSpinLockAcquire(Ctx->Lock);
        Ctx->Channel = brb->ChannelHandle;
        Ctx->Open = TRUE;
        Ctx->RingHead = 0;
        Ctx->RingCount = 0;
        WdfSpinLockRelease(Ctx->Lock);

        status = PpAapSubmitRead(Ctx);
    }
    Ctx->Bth.BthFreeBrb((PBRB)brb);
    WdfWaitLockRelease(Ctx->ControlLock);

    if (!NT_SUCCESS(status)) {
        PpAapCloseChannel(Ctx);
    }
    return status;
}

VOID
PpAapCloseChannel(_In_ PDEVICE_CONTEXT Ctx)
{
    struct _BRB_L2CA_CLOSE_CHANNEL* brb;
    L2CAP_CHANNEL_HANDLE channel;
    LARGE_INTEGER timeout;

    WdfWaitLockAcquire(Ctx->ControlLock, NULL);

    WdfSpinLockAcquire(Ctx->Lock);
    channel = Ctx->Channel;
    Ctx->Open = FALSE;
    Ctx->Channel = NULL;
    Ctx->RingCount = 0;
    WdfSpinLockRelease(Ctx->Lock);

    if (Ctx->ReadActive) {
        WdfRequestCancelSentRequest(Ctx->ReadRequest);
        timeout.QuadPart = -5 * 10 * 1000 * 1000; // 5 seconds
        KeWaitForSingleObject(&Ctx->ReadIdle, Executive, KernelMode, FALSE, &timeout);
    }

    if (channel != NULL) {
        brb = (struct _BRB_L2CA_CLOSE_CHANNEL*)Ctx->Bth.BthAllocateBrb(BRB_L2CA_CLOSE_CHANNEL, POOL_TAG);
        if (brb != NULL) {
            brb->BtAddress = Ctx->RemoteAddress;
            brb->ChannelHandle = channel;
            (VOID)PpAapSubmitBrbSync(Ctx, (PBRB)brb, sizeof(*brb));
            Ctx->Bth.BthFreeBrb((PBRB)brb);
        }
    }

    PpAapFailPendingReceives(Ctx);
    WdfWaitLockRelease(Ctx->ControlLock);
}

NTSTATUS
PpAapSend(_In_ PDEVICE_CONTEXT Ctx, _In_reads_bytes_(Length) PVOID Buffer, _In_ ULONG Length)
{
    struct _BRB_L2CA_ACL_TRANSFER* brb;
    NTSTATUS status;

    WdfWaitLockAcquire(Ctx->ControlLock, NULL);
    if (!Ctx->Open) {
        WdfWaitLockRelease(Ctx->ControlLock);
        return STATUS_DEVICE_NOT_CONNECTED;
    }

    brb = (struct _BRB_L2CA_ACL_TRANSFER*)Ctx->Bth.BthAllocateBrb(BRB_L2CA_ACL_TRANSFER, POOL_TAG);
    if (brb == NULL) {
        WdfWaitLockRelease(Ctx->ControlLock);
        return STATUS_INSUFFICIENT_RESOURCES;
    }

    brb->BtAddress = Ctx->RemoteAddress;
    brb->ChannelHandle = Ctx->Channel;
    brb->TransferFlags = ACL_TRANSFER_DIRECTION_OUT;
    brb->BufferSize = Length;
    brb->Buffer = Buffer; // METHOD_BUFFERED system buffer: non-paged
    brb->BufferMDL = NULL;
    brb->Timeout = 0;

    status = PpAapSubmitBrbSync(Ctx, (PBRB)brb, sizeof(*brb));
    Ctx->Bth.BthFreeBrb((PBRB)brb);
    WdfWaitLockRelease(Ctx->ControlLock);
    return status;
}

VOID
PpAapIndicationCallback(_In_ PVOID Context, _In_ INDICATION_CODE Indication, _In_ PINDICATION_PARAMETERS Parameters)
{
    PDEVICE_CONTEXT ctx = (PDEVICE_CONTEXT)Context;
    UNREFERENCED_PARAMETER(Parameters);

    if (Indication == IndicationRemoteDisconnect) {
        WdfSpinLockAcquire(ctx->Lock);
        ctx->Open = FALSE;
        WdfSpinLockRelease(ctx->Lock);
        PpAapFailPendingReceives(ctx);
        WdfWorkItemEnqueue(ctx->DisconnectWorkItem);
    }
}

VOID
PpAapDisconnectWorkItem(_In_ WDFWORKITEM WorkItem)
{
    PDEVICE_CONTEXT ctx = GetDeviceContext(WdfWorkItemGetParentObject(WorkItem));
    PpAapCloseChannel(ctx);
}

/* --- User-mode requests ------------------------------------------------------------- */

VOID
PpAapEvtIoDeviceControl(_In_ WDFQUEUE Queue, _In_ WDFREQUEST Request, _In_ size_t OutputBufferLength,
                        _In_ size_t InputBufferLength, _In_ ULONG IoControlCode)
{
    PDEVICE_CONTEXT ctx = GetDeviceContext(WdfIoQueueGetDevice(Queue));
    NTSTATUS status = STATUS_INVALID_DEVICE_REQUEST;
    ULONG_PTR information = 0;
    PVOID buffer;
    size_t size;

    UNREFERENCED_PARAMETER(OutputBufferLength);
    UNREFERENCED_PARAMETER(InputBufferLength);

    switch (IoControlCode) {
    case IOCTL_PODPILOT_AAP_GET_INFO:
        status = WdfRequestRetrieveOutputBuffer(Request, sizeof(PODPILOT_AAP_INFO), &buffer, &size);
        if (NT_SUCCESS(status)) {
            PPODPILOT_AAP_INFO info = (PPODPILOT_AAP_INFO)buffer;
            RtlZeroMemory(info, sizeof(*info));
            info->Version = PODPILOT_AAP_VERSION;
            info->State = ctx->Open ? PODPILOT_AAP_STATE_OPEN : PODPILOT_AAP_STATE_CLOSED;
            info->BluetoothAddress = ctx->RemoteAddress;
            info->MaxPacketSize = PODPILOT_AAP_MAX_PACKET;
            information = sizeof(*info);
        }
        break;

    case IOCTL_PODPILOT_AAP_CONNECT:
        status = PpAapOpenChannel(ctx);
        break;

    case IOCTL_PODPILOT_AAP_DISCONNECT:
        PpAapCloseChannel(ctx);
        status = STATUS_SUCCESS;
        break;

    case IOCTL_PODPILOT_AAP_SEND:
        status = WdfRequestRetrieveInputBuffer(Request, 1, &buffer, &size);
        if (NT_SUCCESS(status)) {
            if (size > PODPILOT_AAP_MAX_PACKET) {
                status = STATUS_INVALID_BUFFER_SIZE;
            } else {
                status = PpAapSend(ctx, buffer, (ULONG)size);
                information = NT_SUCCESS(status) ? size : 0;
            }
        }
        break;

    case IOCTL_PODPILOT_AAP_RECEIVE:
        status = WdfRequestRetrieveOutputBuffer(Request, 1, &buffer, &size);
        if (!NT_SUCCESS(status)) {
            break;
        }

        WdfSpinLockAcquire(ctx->Lock);
        if (ctx->RingCount > 0) {
            PAAP_PACKET packet = &ctx->Ring[ctx->RingHead];
            ULONG copy = (ULONG)min(size, (size_t)packet->Length);
            RtlCopyMemory(buffer, packet->Data, copy);
            ctx->RingHead = (ctx->RingHead + 1) % RING_CAPACITY;
            ctx->RingCount--;
            WdfSpinLockRelease(ctx->Lock);
            WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, copy);
            return;
        }
        if (!ctx->Open) {
            WdfSpinLockRelease(ctx->Lock);
            status = STATUS_DEVICE_NOT_CONNECTED;
            break;
        }
        // Wait for the next packet. Forwarding under the lock avoids missing one that arrives now.
        status = WdfRequestForwardToIoQueue(Request, ctx->ReceiveQueue);
        WdfSpinLockRelease(ctx->Lock);
        if (NT_SUCCESS(status)) {
            return;
        }
        break;
    }

    WdfRequestCompleteWithInformation(Request, status, information);
}
