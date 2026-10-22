# Publishing Earlo in the Microsoft Store

Everything Earlo needs for the Store is in the repository. Each CI run builds the **store** artifact:

- `Earlo-store.msixbundle`: the unsigned package (x64 and arm64); the Store signs it after upload, so no certificate is needed
- `store-screenshots/en` and `store-screenshots/nl`: 1920×1080 screenshots with captions, rendered by the app itself

The listing text for five languages is in [`store/listing`](../store/listing). It is written for Store search (see
[How people find Earlo](#how-people-find-earlo)).

The Store version has no noise-control driver (Store apps may not install drivers) and no self-updater
(the Store updates it). Autostart uses the package's startup task instead of the registry.

## Submitting a new version (about 15 minutes)

1. Open the latest successful run of **Build** under **Actions** (for a release: the run of its `v…` tag) and download the **store** artifact.
2. Partner Center > **Apps and games** > **Earlo** (`9NB1SVRN9Z6T`) > **Start update** (or open the pending submission).
3. **Packages**: upload `Earlo-store.msixbundle` and remove the older packages.
4. **Store listings**:
   - **English**: paste [`store/listing/en.md`](../store/listing/en.md), upload `store-screenshots/en/*.png` with the captions from that file.
   - **Manage additional languages**: add Dutch, German, French and Spanish. For each, paste its file from `store/listing`
     and upload screenshots (`store-screenshots/nl` for Dutch, `store-screenshots/en` for the others).
   - Logo: `store/Assets/StoreLogo.scale-200.png`; box art and poster art from [`store/Art`](../store/Art).
5. **Submission options**: the package uses the restricted capability `runFullTrust`. Explain it with the text below.
6. **Submit to the Store.** Certification usually takes 1 to 3 working days.

From then on, releases can go to the Store automatically: see [Automatic Store submissions](#automatic-store-submissions).

**runFullTrust justification**

> Earlo is a WPF desktop app packaged as MSIX. It needs full trust to run as a desktop app with a notification-area icon,
> read Bluetooth LE advertisements from the user's wireless earbuds, pause or resume media playback when an earbud is
> removed, and turn the earbuds' Bluetooth Hands-Free service off or on when the user asks for it in Settings.

### Other submission sections

| Section | What to enter |
| --- | --- |
| Pricing and availability | Price tier €0.99 (or whatever you choose), all markets |
| Properties | Category: **Utilities & tools**. Privacy policy URL: `https://github.com/kylianxdev/Earlo/blob/main/PRIVACY.md`. Website: `https://github.com/kylianxdev/Earlo`. Support contact: `https://github.com/kylianxdev/Earlo/issues` |
| Age ratings | Questionnaire: no user content, no purchases, no location → usually rated 3+ |

## How people find Earlo

The Store search ranks on the product name, the description, the features and the search terms, and gives a boost to apps
with good ratings, many downloads and recent updates. The listings in `store/listing` are built around that:

- **The name stays "Earlo".** Store policy 10.1.1 does not allow descriptive text, keywords or other products' names
  (such as "AirPods") in the product name; a submission with them is rejected.
- **The first sentence of the description** has the words people search for: AirPods, Beats, Galaxy Buds, battery, Windows.
- **Every supported model is named** in the description (AirPods Pro 2, Powerbeats Pro, Galaxy Buds3 Pro, …), so a search
  for a specific model finds Earlo.
- **The muffled-sound fix has its own paragraph**, because "AirPods sound bad on Windows" is one of the most searched AirPods problems.
- **Each language has its own search terms** ("AirPods Akku", "AirPods batterij", "AirPods batterie"), because the Store
  matches the words in the shopper's language. Seven terms, at most 30 characters each and 21 words in total.
- **The screenshot captions** repeat the key words; the first screenshots are the ones shown in search results.
- **Limitations are stated** (no noise control in the Store version, app in English and Dutch): metadata that promises
  more than the app does is rejected, and the Store lowers the visibility of misleading listings.
- **After launch:** ask happy users for a rating and keep shipping updates; both lift the ranking.

If certification objects to a device brand in the search terms, replace it with a generic term such as "wireless earbuds".

## Automatic Store submissions

On every release tag (`v1.2.3`), the **store** job in [`build.yml`](../.github/workflows/build.yml) sends the new
`Earlo-store.msixbundle` to Partner Center as a new submission with the [Microsoft Store Developer CLI](https://github.com/microsoft/msstore-cli).
It only replaces the package; the listing, screenshots and price stay as they are in Partner Center. It runs only once
the four secrets below exist; until then it skips itself.

One-time setup:

1. Partner Center > **Account settings** (gear icon) > **User management** > **Microsoft Entra applications** >
   **Create Microsoft Entra application**. Give it the **Manager** role.
2. Open the new application and click **Add new key**. Copy the **key** (it is shown only once), the **client ID** and the **tenant ID**.
3. Partner Center > **Account settings** > **Legal info** > **Developer** tab: copy the **Seller ID**.
4. GitHub > the Earlo repository > **Settings** > **Secrets and variables** > **Actions** > **New repository secret**, four times:

   | Secret | Value |
   | --- | --- |
   | `PARTNER_CENTER_TENANT_ID` | tenant ID |
   | `PARTNER_CENTER_CLIENT_ID` | client ID |
   | `PARTNER_CENTER_CLIENT_SECRET` | key |
   | `PARTNER_CENTER_SELLER_ID` | Seller ID |

The Store API only updates a product that already has a published submission, so the first Earlo
submission (with the new name and listings) is made by hand as described above. Add the secrets once that
submission is live.

> **Note:** a new submission replaces the one that is still open. If you are editing a submission in Partner Center
> (or one is still in certification) when you tag a release, the CLI deletes it and starts a fresh one from the last
> published version. Finish or submit your manual changes before tagging.

## Reserving the name

### Renaming from PodPilot

Earlo was published as PodPilot. The product keeps its identity (`KyliandeGroot.PodPilot`), so people who
installed PodPilot get Earlo as a normal update. Only the visible name changes:

1. In the product: **Product management** > **Manage app names** > reserve **Earlo** (if it is not reserved yet).
2. In the next submission's **Store listings**, choose **Earlo** as the product name, and upload a package
   built from this repository (its manifest says `Earlo`; a package whose name is not reserved is rejected).
3. Once Earlo is live, you can delete the reserved name PodPilot under **Manage app names**.

### Starting from scratch (another account)

1. Sign up at <https://storedeveloper.microsoft.com> (free) and complete identity verification. For a paid app, fill in
   **Payout and tax profiles**.
2. Partner Center > **Apps and games** > **New product** > **MSIX or PWA app**, and reserve **Earlo**.
3. Open **Product management** > **Product identity** and copy these three values into [`store/identity.json`](../store/identity.json):
   - `Package/Identity/Name` → `IdentityName`
   - `Package/Identity/Publisher` → `Publisher` (starts with `CN=`)
   - `Package/Properties/PublisherDisplayName` → `PublisherDisplayName`
4. Change `STORE_APP_ID` in `build.yml` to the new product's Store ID, commit and push.

## Updating

Bump `<Version>` in `src/Earlo.App/Earlo.App.csproj` and tag the release (`v1.2.3`). With the secrets set up, the
Store submission happens by itself; otherwise upload the new `Earlo-store.msixbundle` as a new submission.
The Store version number must always go up. Update "What's new in this version" in the listings.

## Testing the package locally (optional)

An unsigned MSIX cannot be installed. To try it before submitting, sign it with a self-signed certificate whose
subject equals `Publisher` in `store/identity.json`, trust that certificate, and install the bundle; or enable
Developer Mode and use `Add-AppxPackage -Register AppxManifest.xml` from the staging folder in `publish/msix`.

To rebuild the Store screenshots locally: `Earlo.exe --render-preview renders` (add `--lang nl` for Dutch), then
`pwsh -File tools/make-store-screenshots.ps1 -Renders renders -Lang en -Out store-screenshots/en`.

The Hands-Free switch ("Turn off the earbuds' microphone") can need admin rights. The Store package does not declare
`allowElevation` (the Store rarely approves it); if Windows refuses, Earlo's notice explains how to turn off
"Handsfree Telephony" by hand in Control Panel.
