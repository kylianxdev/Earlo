<p align="center">
  <img src="docs/icon.png" width="96" alt="PodPilot icon">
</p>

<h1 align="center">PodPilot</h1>

<p align="center">
  <b>Free, open-source AirPods, Beats and Galaxy Buds companion for Windows.</b><br>
  The iPhone-style battery popup, live battery levels and automatic ear detection, without paying for it.
</p>

<p align="center">
  <img src="docs/screenshots/popup-ear-detection.png" width="380" alt="PodPilot popup showing AirPods Pro battery levels and which earbud is in your ear">
</p>

## Features

- **Popup when you open the case**, like on an iPhone: model, left and right earbud, case, charging state.
- **Live battery levels** in the tray icon (the lowest level as a number) and its tooltip.
- **See each earbud**: in your ear, out of your ear or in the case, updated live.
- **Automatic ear detection**: take an AirPod out and Spotify, YouTube or any other app pauses; put it back in and playback resumes. It works with every app that shows up in the Windows media controls.
- **Connect with one click** from the popup, or automatically when you put your earbuds in (like switching on an iPhone, off by default).
- **Volume indicator for stem swipes**: Windows changes the volume silently when you swipe; PodPilot shows it, like the volume keys do.
- **Low battery warnings** for each earbud and the case.
- **Works with every AirPods and Beats model and Samsung Galaxy Buds** (see below). AirPods and Beats released after this version still show their battery levels.
- **English and Dutch**, following your Windows language.
- **Pops up next to your taskbar**, wherever it is: bottom, top, left or right, on any monitor.
- **Your own device name** ("My AirPods Pro") when the headphones are paired with your PC.
- **Updates itself**: the GitHub version installs new releases automatically (can be turned off), the Store version updates through the Store.
- Starts with Windows, follows light and dark mode, uses the Windows 11 look.
- **No drivers needed, no account, no telemetry.** It only listens to Bluetooth LE broadcasts. The only internet traffic is the update check against GitHub ([privacy policy](PRIVACY.md)).

## Supported devices

| AirPods | Beats | Samsung |
| --- | --- | --- |
| AirPods (1st and 2nd generation) | Powerbeats Pro, Powerbeats Pro 2 | Galaxy Buds, Buds+ |
| AirPods (3rd generation) | Beats Fit Pro | Galaxy Buds Live, Buds Pro |
| AirPods 4, AirPods 4 (ANC) | Beats Studio Buds, Studio Buds + | Galaxy Buds2, Buds2 Pro |
| AirPods Pro | Beats Solo Buds | Galaxy Buds FE, Buds Core |
| AirPods Pro (2nd generation, Lightning and USB-C) | Beats Flex, BeatsX, Powerbeats, Powerbeats3 | Galaxy Buds3, Buds3 Pro, Buds3 FE |
| AirPods Pro 3 | Beats Solo3, Solo Pro, Solo 4 | |
| AirPods Max (Lightning and USB-C) | Beats Studio3, Studio Pro | |

Galaxy Buds are read over their own Bluetooth serial channel, so they need to be paired and connected to your PC.
They show exact battery levels, the case, charging and which bud is in your ear.

<p align="center">
  <img src="docs/screenshots/all-models.png" width="820" alt="PodPilot popup for every supported model family">
</p>

A model that is not in the list is shown as generic headphones, with working battery levels.
If that happens, please [open an issue](../../issues) with a diagnostics log (see below) so it can be added.

## Install

1. Download from the [latest release](../../releases/latest):
   - `PodPilot-win-x64.zip`: standalone, runs on any PC (about 32 MB).
   - `PodPilot-win-x64-small.zip`: about 9 MB, but needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows offers to install it on first start).
   - `win-arm64` versions for ARM PCs.
2. Unzip it anywhere, for example your Documents folder. It is a single `PodPilot.exe`.
3. Run `PodPilot.exe`. It sits in the notification area (next to the clock).

Requirements: Windows 10 version 2004 or newer, or Windows 11, with a Bluetooth 4.0 (LE) adapter.

Pair your AirPods with Windows as usual (Settings > Bluetooth & devices > Add device, then hold the button on the case).
Pairing is optional for battery levels, but needed for your device name and for ear detection.

## How it works

AirPods and Beats constantly broadcast a small Bluetooth LE message (Apple's "proximity pairing" message, manufacturer data `0x004C`, type `0x07`).
It contains the model, the battery level of each earbud and the case in steps of 10%, charging flags, in-ear sensors and the lid state.
PodPilot reads it with the standard Windows Bluetooth APIs. This is the same data your iPhone uses for its popup.

Because the levels come in 10% steps (for example "80 to 89%"), PodPilot shows the middle of the range (85%), like MagicPods does.

| Part | File |
| --- | --- |
| Message parser | [`ProximityPairingMessage.cs`](src/PodPilot.Core/Protocol/ProximityPairingMessage.cs) |
| Model list | [`DeviceCatalog.cs`](src/PodPilot.Core/Devices/DeviceCatalog.cs) |
| Smoothing, device selection, events | [`PodsTracker.cs`](src/PodPilot.Core/Tracking/PodsTracker.cs) |
| Bluetooth LE scanning | [`AdvertisementScanner.cs`](src/PodPilot.Core/Bluetooth/AdvertisementScanner.cs) |
| Media pause/resume | [`MediaController.cs`](src/PodPilot.Core/Media/MediaController.cs) |
| Galaxy Buds protocol | [`GalaxyBudsMessage.cs`](src/PodPilot.Core/Samsung/GalaxyBudsMessage.cs) |
| Connect button, volume indicator | [`AudioEndpoints.cs`](src/PodPilot.App/Services/AudioEndpoints.cs) |

### Noise control: it works, but Windows makes it hard

Switching noise control (Off, Noise Cancellation, Transparency, Adaptive), Conversation Awareness, Adaptive strength
and exact 1% battery levels need Apple's AAP protocol over a Bluetooth L2CAP channel (PSM `0x1001`).
Windows does not let normal apps open L2CAP channels, so PodPilot has its own open-source kernel driver for it: see [`driver/`](driver/README.md).

<p align="center">
  <img src="docs/screenshots/popup-noise-control.png" width="380" alt="Popup with noise control buttons">
</p>

**The driver works.** Installing it is the hard part: Windows only loads drivers signed by Microsoft, and this one is not (yet, see below).
On a PC with Secure Boot (almost every modern PC) you have to start Windows with "Disable driver signature enforcement" (F7 in Startup Settings),
and you have to do that again after every restart. The GitHub version of PodPilot has an **Install noise control driver** button in Settings
that does the rest for you. Without the driver, everything else in PodPilot works as usual.

## Help unlock noise control for everyone

We figured out how to switch AirPods noise control on Windows, the thing people have wanted for years. The driver is written, open source and it works.
The only thing between you and a one-click install is a signature from Microsoft.

To sign a driver, Microsoft requires an EV code signing certificate, and those cost up to **about €700 per year**, every year. Blame Microsoft.
With that signature the driver installs with one click, Secure Boot stays on and it keeps working after a restart. PodPilot even switches to the signed driver automatically.

**Want to help?** Get PodPilot from the [Microsoft Store](#microsoft-store) for about €1. You get automatic updates through the Store,
and every purchase goes towards the signing certificate. PodPilot stays free and open source on GitHub, so it is your choice.

<a id="microsoft-store"></a>
> **Microsoft Store:** coming soon. The link will be here as soon as the listing is live.

| Part | File |
| --- | --- |
| AAP packets and parsing | [`AapPacket.cs`](src/PodPilot.Core/Aap/AapPacket.cs), [`AapMessage.cs`](src/PodPilot.Core/Aap/AapMessage.cs) |
| Session (handshake, state, commands) | [`AapSession.cs`](src/PodPilot.Core/Aap/AapSession.cs) |
| Driver client | [`DriverAapTransport.cs`](src/PodPilot.Core/Aap/DriverAapTransport.cs) |
| Kernel driver (KMDF) | [`driver/PodPilotAap`](driver/PodPilotAap) |

## Command-line options

| Option | What it does |
| --- | --- |
| `--minimized` | Still accepted for older autostart entries; PodPilot always starts in the tray now. |
| `--demo` | Feed fake AirPods data, to try the app without headphones. |
| `--log <file>` | Write every received AirPods message to a text file. Attach this to bug reports. |
| `--render-preview <folder>` | Render the popup for several models to PNG files. |

## Building from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/kylianxdev/PodPilot.git
cd PodPilot
dotnet test
dotnet run --project src/PodPilot.App
```

Publish a self-contained build:

```bash
dotnet publish src/PodPilot.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Roadmap

- [x] AAP protocol layer: noise control, Conversation Awareness, exact battery levels
- [x] AAP driver working on real AirPods
- [x] Automatic updates
- [ ] Microsoft Store release
- [ ] Sign the driver through Microsoft, so no F7 boot is needed ([help fund it](#help-unlock-noise-control-for-everyone))
- [x] Translations (English, Dutch)
- [x] Samsung Galaxy Buds
- [ ] Installer and winget package

Contributions are welcome. Please open an issue first for larger changes.

## Credits

The AAP byte formats come from the public protocol notes of [LibrePods](https://github.com/kavishdevar/librepods) (no code was copied).
The message format is documented by the reverse-engineering work of the community, including
[OpenPods](https://github.com/adolfintel/OpenPods), [AirPodsDesktop](https://github.com/SpriteOvO/AirPodsDesktop),
[LibrePods](https://github.com/kavishdevar/librepods) and [furiousMAC/continuity](https://github.com/furiousMAC/continuity).
The Galaxy Buds message format is documented by [GalaxyBudsClient](https://github.com/timschneeb/GalaxyBudsClient) (no code was copied).
PodPilot is inspired by MagicPods, but shares no code with it.
The product renders in `src/PodPilot.App/Assets/Devices` are AI-generated illustrations, not Apple photos.

## License

[MIT](LICENSE). AirPods, AirPods Pro, AirPods Max and Beats are trademarks of Apple Inc. Galaxy Buds is a trademark of Samsung.
PodPilot is an independent project and is not affiliated with or endorsed by Apple or Samsung.
