<p align="center">
  <img src="docs/icon.png" width="112" alt="PodPilot icon">
</p>

<h1 align="center">PodPilot</h1>

<p align="center">
  <b>Your AirPods, Beats and Galaxy Buds, finally at home on Windows.</b><br>
  The iPhone battery popup, live battery levels, automatic ear detection and one-click connect.<br>
  Free, open source, no account, no telemetry.
</p>

<p align="center">
  <a href="../../releases/latest"><img src="https://img.shields.io/github/v/release/kylianxdev/PodPilot?label=download&color=34c759" alt="Latest release"></a>
  <a href="../../releases"><img src="https://img.shields.io/github/downloads/kylianxdev/PodPilot/total?color=0a84ff" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-1c1c1e" alt="Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/kylianxdev/PodPilot?color=6e6e73" alt="MIT license"></a>
</p>

<p align="center">
  <img src="docs/screenshots/popup-ear-detection.png" width="300" alt="PodPilot popup showing AirPods Pro battery levels and which earbud is in your ear">
  <img src="docs/screenshots/popup-noise-control.png" width="300" alt="Popup with noise control buttons">
  <img src="docs/screenshots/popup-galaxy-buds.png" width="300" alt="Popup for Samsung Galaxy Buds2 Pro">
</p>

## Why PodPilot

Windows treats your AirPods like any other Bluetooth headset: no battery levels, no popup, no ear detection.
PodPilot brings back what you are used to from an iPhone, and adds the things Windows forgot.

### See everything at a glance

- **Popup when you open the case**, just like on an iPhone: your earbuds, the case, charging state, in a clean Windows 11 card.
- **Every earbud, live**: in your ear, out of your ear or in the case, with its own battery level.
- **Battery in the tray**: hover the icon for every level, or turn on the small battery bar on the icon itself.
- **Low battery warnings** for each earbud and the case, before your music cuts out.

### It just works

- **Automatic ear detection**: take an earbud out and Spotify, YouTube, Netflix or any other app pauses. Put it back in and it plays on.
- **Connect in one click** from the popup, or let your earbuds switch to the PC by themselves when you put them in, like on an iPhone.
- **Disconnect in one click**, so your phone can have them back.
- **A volume indicator for stem swipes**: Windows changes the volume silently when you swipe; PodPilot shows it, like the volume keys do.
- **Noise control** (Off, Noise Cancellation, Transparency, Adaptive) and Conversation Awareness with the [experimental driver](#noise-control-it-works-but-windows-makes-it-hard).

### Made for Windows

- **Lives in the tray**: starts quietly with Windows, click the icon to open it.
- **Pops up next to your taskbar**, wherever it is: bottom, top, left or right, on any monitor and any DPI.
- **English and Dutch**, following your Windows language. Light and dark mode, the Windows 11 look.
- **Updates itself**: the GitHub version installs new releases automatically (verified against their checksums), the Store version updates through the Store.
- **Private by design**: no account, no telemetry, no cloud. The only internet traffic is the update check against GitHub ([privacy policy](PRIVACY.md)).
- **One file**: a single `PodPilot.exe`, no installer, no background services.

<p align="center">
  <img src="docs/screenshots/settings.png" width="760" alt="PodPilot settings window">
</p>

<p align="center">
  <img src="docs/screenshots/volume.png" width="320" alt="Volume indicator shown when you swipe on the stem">
</p>

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

<p align="center">
  <img src="docs/screenshots/all-models.png" width="860" alt="PodPilot popup for every supported model family">
</p>

AirPods and Beats released after this version still show their battery levels, as generic headphones.
If that happens, please [open an issue](../../issues) with a diagnostics log (see below) so the model can be added.
Galaxy Buds are read over their own Bluetooth channel, so they need to be paired and connected to your PC;
in return they report exact 1% battery levels.

## Install

**Microsoft Store** (automatic updates, supports development): see [below](#microsoft-store).

**GitHub** (free):

1. Download from the [latest release](../../releases/latest):
   - `PodPilot-win-x64.zip`: standalone, runs on any PC.
   - `PodPilot-win-x64-small.zip`: about 9 MB, but needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (Windows offers to install it on first start).
   - `win-arm64` versions for ARM PCs.
2. Unzip it anywhere, for example your Documents folder. It is a single `PodPilot.exe`.
3. Run `PodPilot.exe`. It lives in the notification area next to the clock (under the `^` arrow); click the icon to open it.

Requirements: Windows 10 version 2004 or newer, or Windows 11, with a Bluetooth 4.0 (LE) adapter.

Pair your earbuds with Windows as usual (Settings > Bluetooth & devices > Add device, then hold the button on the case).
Pairing is optional for AirPods battery levels, but needed for your device name, ear detection, connecting and Galaxy Buds.

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
> **Microsoft Store:** in review at Microsoft (submitted October 2026). The link will be here as soon as the listing is live.

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
| `--lang en` / `--lang nl` | Use English or Dutch instead of the Windows language. |

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
- [ ] Microsoft Store release (in certification)
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
