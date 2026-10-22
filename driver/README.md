# Earlo AAP bridge driver

Windows does not let normal apps open Bluetooth L2CAP channels. AirPods use one (PSM `0x1001`)
for Apple's accessory protocol (AAP): noise control, Conversation Awareness, exact battery levels and more.

This KMDF driver loads on the AirPods' vendor Bluetooth service
`{4715650b-5e9d-4ac2-b898-a4fc0aa5df78}`, opens that channel and passes raw packets to Earlo.
All protocol logic lives in the app ([`src/Earlo.Core/Aap`](../src/Earlo.Core/Aap)).

> **Status: works on real AirPods.** It is not signed by Microsoft yet, so it only loads after the F7 boot option (see below).
> Problems? Open an issue with a `--log` file from Earlo.

## Interface

One device interface (`{1e9fa4ac-dd4c-4d83-a2a0-a5767809ccba}`) exists per paired AirPods.
See [`PodPilotAap/Public.h`](PodPilotAap/Public.h).

| IOCTL | What it does |
| --- | --- |
| `GET_INFO` | Driver version, channel state, Bluetooth address |
| `CONNECT` | Opens L2CAP PSM 0x1001 to the AirPods |
| `DISCONNECT` | Closes the channel |
| `SEND` | Sends one AAP packet |
| `RECEIVE` | Waits for the next packet from the AirPods |

## Installing a test build

The driver is test-signed until it is signed through Microsoft Partner Center.
Test-signed drivers only load with Windows test signing enabled, which lowers security
(it can conflict with Secure Boot and game anti-cheat).

1. Pair your AirPods with Windows.
2. Download `Earlo-driver-x64.zip` from the GitHub Actions artifacts or a release, and unzip it.
3. Allow the test-signed driver, either way:
   - **One boot only, Secure Boot stays on:** Shift + Restart > Troubleshoot > Advanced options > Startup Settings > Restart > press **F7**
     ("Disable driver signature enforcement"). After the next normal restart everything is back to normal.
   - **Permanent:** `bcdedit /set testsigning on` and restart. With Secure Boot on, Windows refuses this
     ("protected by Secure Boot policy"); you would have to turn Secure Boot off in UEFI, which breaks some game anti-cheat (for example Riot Vanguard).
4. Easiest: in Earlo, open Settings > Noise control > **Install noise control driver**. Or, in an administrator PowerShell in the unzipped folder: `powershell -ExecutionPolicy Bypass -File install-driver.ps1` (add `-Force` after the F7 boot).
5. Start Earlo. Noise control buttons appear when the channel is open.

If Memory integrity (Windows Security > Device security > Core isolation) is on, Windows may still block the driver.
To remove it, run `uninstall-driver.ps1` (and `bcdedit /set testsigning off` if you turned it on).

## Building

You need Visual Studio 2022 with the Windows Driver Kit (WDK).

```bash
msbuild driver/PodPilotAap/PodPilotAap.vcxproj -p:Configuration=Release -p:Platform=x64
```

## Hardware IDs

The INF matches the AAP service in general, plus one hardware ID per known model
(`BTHENUM\{4715650b-...}_VID&0001004c_PID&<model>`). If your AirPods are not picked up, look up the
hardware IDs in Device Manager (Bluetooth > the device > Details > Hardware Ids) and open an issue.
