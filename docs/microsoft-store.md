# Publishing Earlo in the Microsoft Store

Everything Earlo needs for the Store is already in the repository. Each CI run builds an unsigned
`Earlo-store.msixbundle` (x64 and arm64); the Store signs it after upload, so no certificate is needed.

The Store version has no noise-control driver (Store apps may not install drivers) and no self-updater
(the Store updates it). Autostart uses the package's startup task instead of the registry.

## 1. Create the developer account (free)

1. Go to <https://storedeveloper.microsoft.com> and sign up as an individual or company. Registration is free.
2. Complete identity verification.
3. For a paid app: fill in **Payout and tax profiles** in Partner Center, otherwise you cannot set a price.

## 2. Reserve the name

1. Partner Center > **Apps and games** > **New product** > **MSIX or PWA app**.
2. Reserve **Earlo** (or a variant if it is taken). Don't use "AirPods" in the app name: it is Apple's trademark.
3. Open **Product management** > **Product identity** and copy these three values into [`store/identity.json`](../store/identity.json):
   - `Package/Identity/Name` → `IdentityName`
   - `Package/Identity/Publisher` → `Publisher` (starts with `CN=`)
   - `Package/Properties/PublisherDisplayName` → `PublisherDisplayName`
4. Commit and push. The next CI run builds the bundle with your real identity.

### Renaming from PodPilot

Earlo was published as PodPilot. The product keeps its identity (`KyliandeGroot.PodPilot`), so people who
installed PodPilot get Earlo as a normal update. Only the visible name changes:

1. In the PodPilot product: **Product management** > **Manage app names** > reserve **Earlo**.
2. In the next submission's **Store listing**, choose **Earlo** as the product name, and upload a package
   built from this repository (its manifest says `Earlo`; a package whose name is not reserved is rejected).
3. Once Earlo is live, you can delete the reserved name PodPilot under **Manage app names**.

## 3. Fill in the submission

| Section | What to enter |
| --- | --- |
| Pricing and availability | Price tier €0.99 (or whatever you choose), all markets |
| Properties | Category: **Utilities & tools**. Privacy policy URL: `https://github.com/kylianxdev/Earlo/blob/main/PRIVACY.md` |
| Age ratings | Questionnaire: no user content, no purchases, no location → usually rated 3+ |
| Packages | Upload `Earlo-store.msixbundle` from the **store** artifact of the latest successful GitHub Actions run |
| Store listing | Text below, screenshots from [`store/Screenshots`](../store/Screenshots), logo `store/Assets/StoreLogo.scale-200.png` |
| Submission options | The package uses the restricted capability `runFullTrust`. Explain it with the text below |

**runFullTrust justification**

> Earlo is a WPF desktop app packaged as MSIX. It needs full trust to run as a desktop app with a notification-area icon,
> read Bluetooth LE advertisements from the user's wireless earbuds and pause or resume media playback when an earbud is removed.

## Store listing text

The Store search looks at the product name, the description and the search terms, so the device names people
type ("AirPods battery", "Galaxy Buds") are in the description itself, not only in the keywords.

**Product name:** Earlo

**Short description**

> The iPhone battery popup for Windows: live battery levels, ear detection and one-click connect for AirPods, Beats and Galaxy Buds.

**Description**

> Earlo brings the AirPods experience you know from the iPhone to Windows 10 and Windows 11, and works with Beats
> and Samsung Galaxy Buds too.
>
> • A battery popup as soon as you open the case: left earbud, right earbud and the case, with charging state
> • Live battery level of your earbuds and headphones in the notification area (system tray)
> • See which earbud is in your ear, out of your ear or in the case
> • Automatic ear detection: take an earbud out and your music pauses, put it back in and it plays again (Spotify, YouTube, Netflix, browsers and more)
> • Connect and disconnect your earbuds in one click
> • A "Connected" notice in the top right corner when your earbuds connect, with their battery levels
> • Fix the muffled sound: Windows switches AirPods to call quality as soon as an app uses their microphone; one switch keeps them in stereo
> • A volume indicator when you swipe the stem to change the volume
> • Low battery warnings for each earbud and the case
> • Works with AirPods 1, 2, 3 and 4, AirPods Pro 1, 2 and 3, AirPods Max, Beats earbuds and headphones (Powerbeats Pro, Beats Fit Pro, Studio Buds, Solo Buds, Studio Pro and more) and Samsung Galaxy Buds (Buds, Buds+, Buds Live, Buds Pro, Buds2, Buds2 Pro, Buds FE, Buds3, Buds3 Pro, Buds3 FE, Buds Core)
> • Starts with Windows, light and dark mode, Windows 11 design, English and Dutch
> • No account, no ads, no tracking
>
> Earlo was called PodPilot before. It is open source: https://github.com/kylianxdev/Earlo
> Buying it in the Store supports development and funds signing the noise-control driver.
>
> AirPods and Beats are trademarks of Apple Inc., Galaxy Buds of Samsung. Earlo is not affiliated with Apple or Samsung.

**Search terms** (up to 7, at most 30 characters each; they are not shown to users)

| # | Term |
| --- | --- |
| 1 | AirPods battery |
| 2 | earbuds battery level |
| 3 | Galaxy Buds |
| 4 | ear detection |
| 5 | Bluetooth headphones battery |
| 6 | battery popup |
| 7 | earbuds sound quality |

The old name is only mentioned in the description: another app in the Store is called PodPilot, so using it as
a search term could count as misleading. If certification objects to a device brand in the search terms,
replace it with a generic term such as "noise cancelling".

## Updating

Bump `<Version>` in `src/Earlo.App/Earlo.App.csproj`, tag the release (`v1.2.3`), and upload the new
`Earlo-store.msixbundle` as a new submission. The Store version number must always go up.

## Testing the package locally (optional)

An unsigned MSIX cannot be installed. To try it before submitting, sign it with a self-signed certificate whose
subject equals `Publisher` in `store/identity.json`, trust that certificate, and install the bundle; or enable
Developer Mode and use `Add-AppxPackage -Register AppxManifest.xml` from the staging folder in `publish/msix`.
