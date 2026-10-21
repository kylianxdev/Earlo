# Publishing PodPilot in the Microsoft Store

Everything PodPilot needs for the Store is already in the repository. Each CI run builds an unsigned
`PodPilot-store.msixbundle` (x64 and arm64); the Store signs it after upload, so no certificate is needed.

The Store version has no noise-control driver (Store apps may not install drivers) and no self-updater
(the Store updates it). Autostart uses the package's startup task instead of the registry.

## 1. Create the developer account (free)

1. Go to <https://storedeveloper.microsoft.com> and sign up as an individual or company. Registration is free.
2. Complete identity verification.
3. For a paid app: fill in **Payout and tax profiles** in Partner Center, otherwise you cannot set a price.

## 2. Reserve the name

1. Partner Center > **Apps and games** > **New product** > **MSIX or PWA app**.
2. Reserve **PodPilot** (or a variant if it is taken). Don't use "AirPods" in the app name: it is Apple's trademark.
3. Open **Product management** > **Product identity** and copy these three values into [`store/identity.json`](../store/identity.json):
   - `Package/Identity/Name` → `IdentityName`
   - `Package/Identity/Publisher` → `Publisher` (starts with `CN=`)
   - `Package/Properties/PublisherDisplayName` → `PublisherDisplayName`
4. Commit and push. The next CI run builds the bundle with your real identity.

## 3. Fill in the submission

| Section | What to enter |
| --- | --- |
| Pricing and availability | Price tier €0.99 (or whatever you choose), all markets |
| Properties | Category: **Utilities & tools**. Privacy policy URL: `https://github.com/kylianxdev/PodPilot/blob/main/PRIVACY.md` |
| Age ratings | Questionnaire: no user content, no purchases, no location → usually rated 3+ |
| Packages | Upload `PodPilot-store.msixbundle` from the **store** artifact of the latest successful GitHub Actions run |
| Store listing | Text below, screenshots from [`store/Screenshots`](../store/Screenshots), logo `store/Assets/StoreLogo.scale-200.png` |
| Submission options | The package uses the restricted capability `runFullTrust`. Explain it with the text below |

**runFullTrust justification**

> PodPilot is a WPF desktop app packaged as MSIX. It needs full trust to run as a desktop app with a notification-area icon,
> read Bluetooth LE advertisements from the user's wireless earbuds and pause or resume media playback when an earbud is removed.

## Store listing text

**Short description**

> The iPhone battery popup for Windows: live battery levels and automatic ear detection for your wireless earbuds.

**Description**

> PodPilot brings the experience you know from the iPhone to Windows.
>
> • A popup with the battery level of the left and right earbud and the case as soon as you open the case
> • Live battery level in the notification area
> • See which earbud is in your ear, out of your ear or in the case
> • Automatic ear detection: take an earbud out and your music pauses, put it back in and it plays again (Spotify, YouTube, browsers and more)
> • Low battery warnings
> • Works with AirPods 1, 2, 3, 4, AirPods Pro 1, 2 and 3, AirPods Max and Beats earbuds and headphones
> • Starts with Windows, light and dark mode, Windows 11 design
> • No account, no ads, no tracking
>
> PodPilot is open source: https://github.com/kylianxdev/PodPilot
> Buying it in the Store supports development and funds signing the noise-control driver.
>
> AirPods and Beats are trademarks of Apple Inc. PodPilot is not affiliated with Apple.

**Keywords:** AirPods, battery, earbuds, Beats, Bluetooth, headphones, ear detection

## Updating

Bump `<Version>` in `src/PodPilot.App/PodPilot.App.csproj`, tag the release (`v1.2.3`), and upload the new
`PodPilot-store.msixbundle` as a new submission. The Store version number must always go up.

## Testing the package locally (optional)

An unsigned MSIX cannot be installed. To try it before submitting, sign it with a self-signed certificate whose
subject equals `Publisher` in `store/identity.json`, trust that certificate, and install the bundle; or enable
Developer Mode and use `Add-AppxPackage -Register AppxManifest.xml` from the staging folder in `publish/msix`.
