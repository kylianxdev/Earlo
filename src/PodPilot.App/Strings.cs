using System.Globalization;

namespace PodPilot.App;

/// <summary>
/// Every text in the UI, in English and Dutch. PodPilot follows the Windows display language.
/// XAML reads these with <c>{x:Static app:Strings.Name}</c>.
/// </summary>
public static class Strings
{
    public static bool Dutch { get; } = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "nl";

    static string T(string en, string nl) => Dutch ? nl : en;

    // Device panel
    public static string Left => T("Left", "Links");
    public static string Right => T("Right", "Rechts");
    public static string Case => T("Case", "Case");
    public static string Battery => T("Battery", "Batterij");
    public static string InEar => T("In ear", "In oor");
    public static string OutOfEar => T("Out of ear", "Uit oor");
    public static string InCase => T("In case", "In case");
    public static string StaleBattery => T("Last known level. The case reports its battery when an earbud is in it.",
        "Laatst bekende stand. De case geeft zijn batterij door als er een oortje in zit.");
    public static string NoAdapterTitle => T("No Bluetooth adapter", "Geen Bluetooth-adapter");
    public static string NoAdapterText => T("PodPilot needs a Bluetooth 4.0 (LE) adapter.", "PodPilot heeft een Bluetooth 4.0 (LE) adapter nodig.");
    public static string RadioOffTitle => T("Bluetooth is off", "Bluetooth staat uit");
    public static string RadioOffText => T("Turn on Bluetooth in Windows settings.", "Zet Bluetooth aan in de Windows-instellingen.");
    public static string ScanErrorTitle => T("Bluetooth error", "Bluetooth-fout");
    public static string ScanErrorText => T("Scanning stopped. PodPilot keeps retrying.", "Zoeken is gestopt. PodPilot blijft het opnieuw proberen.");
    public static string LookingTitle => T("Looking for your earbuds", "Zoeken naar je oortjes");
    public static string LookingText => T("Open the case next to your PC, or put your AirPods in your ears.",
        "Open de case naast je pc, of doe je AirPods in.");
    public static string Nearby => T("Nearby", "In de buurt");
    public static string UnknownModel => T("Unknown model, shown as generic headphones", "Onbekend model, getoond als gewone koptelefoon");
    public static string ConnectedToPc => T("Connected to this PC", "Verbonden met deze pc");
    public static string NotConnectedToPc => T("Not connected to this PC", "Niet verbonden met deze pc");
    public static string InBothEars => T("in both ears", "in beide oren");
    public static string InOneEar => T("in one ear", "in één oor");
    public static string Connect => T("Connect", "Verbinden");
    public static string Connecting => T("Connecting...", "Verbinden...");
    public static string Disconnect => T("Disconnect", "Verbreken");
    public static string Disconnecting => T("Disconnecting...", "Verbreken...");
    public static string CaseUnknown => T("Put an earbud in the case to see it", "Leg een oortje in de case om dit te zien");
    public static string CaseUnknownTip => T("AirPods only report the case battery while an earbud is in it.",
        "AirPods geven de batterij van de case alleen door als er een oortje in zit.");

    // Noise control
    public static string ModeOff => T("Off", "Uit");
    public static string ModeOffTip => T("Noise control off", "Ruisregeling uit");
    public static string ModeAnc => T("Noise Cancel", "Ruisonderdr.");
    public static string ModeAncTip => T("Active noise cancellation", "Actieve ruisonderdrukking");
    public static string ModeTransparency => T("Transparency", "Transparant");
    public static string ModeTransparencyTip => T("Hear your surroundings", "Hoor je omgeving");
    public static string ModeAdaptive => T("Adaptive", "Adaptief");
    public static string ModeAdaptiveTip => T("Adapts to your environment", "Past zich aan je omgeving aan");

    // Popup
    public static string SettingsTip => T("Settings", "Instellingen");
    public static string Close => T("Close", "Sluiten");

    // Settings window
    public static string ShowPopup => T("Show popup", "Pop-up tonen");
    public static string BluetoothSettings => T("Bluetooth settings", "Bluetooth-instellingen");
    public static string Settings => T("Settings", "Instellingen");
    public static string General => T("General", "Algemeen");
    public static string StartWithWindows => T("Start PodPilot with Windows", "PodPilot starten met Windows");
    public static string TrayShowsBattery => T("Show battery level in the tray icon", "Batterijniveau tonen in het systeemvak");
    public static string Popup => T("Popup", "Pop-up");
    public static string PopupOnLidOpen => T("Show popup when the case is opened", "Pop-up tonen als de case opengaat");
    public static string PopupOnConnect => T("Show popup when headphones connect", "Pop-up tonen als de oortjes verbinden");
    public static string HideAfter => T("Hide after", "Verbergen na");
    public static string Connection => T("Connection", "Verbinding");
    public static string AutoConnect => T("Connect to this PC when you put them in your ears", "Met deze pc verbinden als je ze indoet");
    public static string AutoConnectHint => T("Like switching on an iPhone: the earbuds move to this PC when you start wearing them. Leave it off if they should stay with your phone.",
        "Net als wisselen op een iPhone: de oortjes gaan naar deze pc zodra je ze indoet. Laat dit uit als ze bij je telefoon moeten blijven.");
    public static string VolumeOsd => T("Show the volume when it changes on the earbuds", "Volume tonen als het op de oortjes verandert");
    public static string EarDetection => T("Automatic ear detection", "Automatische oordetectie");
    public static string PauseOnRemove => T("Pause media when you take an earbud out", "Media pauzeren als je een oortje uitdoet");
    public static string PauseWhen => T("Pause when", "Pauzeren als");
    public static string PauseOne => T("one earbud is removed", "één oortje uit is");
    public static string PauseBoth => T("both earbuds are removed", "beide oortjes uit zijn");
    public static string ResumeOnInsert => T("Resume when you put them back in", "Verder afspelen als je ze weer indoet");
    public static string OnlyWhenConnected => T("Only when the earbuds are connected to this PC", "Alleen als de oortjes met deze pc verbonden zijn");
    public static string MediaHint => T("Works with Spotify, browsers, Media Player and every app that shows up in the Windows media controls.",
        "Werkt met Spotify, browsers, Mediaspeler en elke app die in de Windows-mediabediening verschijnt.");
    public static string NoiseControl => T("Noise control", "Ruisregeling");
    public static string VolumeSwipe => T("Swipe up or down on the stem to change the volume", "Veeg omhoog of omlaag over het steeltje om het volume te regelen");
    public static string ConversationAwareness => T("Conversation Awareness (lower volume when you speak)", "Gespreksdetectie (zachter als je praat)");
    public static string AdaptiveStrength => T("Adaptive strength", "Adaptieve sterkte");
    public static string AdaptiveHint => T("Lower lets in more ambient sound, higher filters more noise. Only applies in Adaptive mode.",
        "Lager laat meer omgevingsgeluid door, hoger filtert meer ruis. Alleen in de adaptieve stand.");
    public static string Notifications => T("Notifications", "Meldingen");
    public static string WarnLowBattery => T("Warn when the battery is low", "Waarschuwen als de batterij bijna leeg is");
    public static string WarnAt => T("Warn at", "Waarschuwen bij");
    public static string Advanced => T("Advanced", "Geavanceerd");
    public static string DetectionRange => T("Detection range", "Detectiebereik");
    public static string RangeHint => T("Lower values detect earbuds further away. Raise it if a colleague's AirPods show up instead of yours.",
        "Lagere waarden vinden oortjes verder weg. Zet het hoger als de AirPods van een collega verschijnen in plaats van de jouwe.");
    public static string About => T("About", "Over");
    public static string AutoUpdate => T("Install updates automatically", "Updates automatisch installeren");
    public static string CheckUpdates => T("Check for updates", "Zoeken naar updates");
    public static string AboutHint => T("PodPilot is free and open source. AirPods and Beats are trademarks of Apple Inc., Galaxy Buds of Samsung. This project is not affiliated with either.",
        "PodPilot is gratis en open source. AirPods en Beats zijn merken van Apple Inc., Galaxy Buds van Samsung. Dit project is aan geen van beide verbonden.");
    public static string StoreUpdates => T("Updates are installed by the Microsoft Store.", "Updates worden geïnstalleerd door de Microsoft Store.");
    public static string DevBuild => T("Development build: no automatic updates.", "Ontwikkelversie: geen automatische updates.");
    public static string NonePaired => T("No earbuds are paired with this PC yet. Pair them in Bluetooth settings for automatic ear detection.",
        "Er zijn nog geen oortjes gekoppeld met deze pc. Koppel ze in de Bluetooth-instellingen voor automatische oordetectie.");
    public static string Paired => T("Paired", "Gekoppeld");
    public static string ConnectedSuffix => T("connected", "verbonden");
    public static string AapNeedsDriver => T("Switching noise control needs the PodPilot AAP driver (experimental).",
        "Ruisregeling schakelen vraagt de PodPilot AAP-driver (experimenteel).");
    public static string AapWaiting => T("Driver installed. Waiting for your AirPods to connect...", "Driver geïnstalleerd. Wachten tot je AirPods verbinden...");
    public static string AapNoNoiseControl => T("Connected over AAP. These headphones have no noise control.", "Verbonden via AAP. Deze oortjes hebben geen ruisregeling.");
    public static string AapConnected => T("Connected over AAP. Switch modes with the buttons on the left.", "Verbonden via AAP. Wissel van stand met de knoppen links.");
    public static string Working => T("Working...", "Bezig...");
    public static string RemoveDriver => T("Remove noise control driver", "Ruisregeling-driver verwijderen");
    public static string InstallDriver => T("Install noise control driver", "Ruisregeling-driver installeren");
    public static string DriverInstalledNote => T("Installed. Windows asks for administrator permission to remove it.",
        "Geïnstalleerd. Windows vraagt om beheerdersrechten om hem te verwijderen.");
    public static string DriverSignedNote => T("Windows asks for administrator permission once. Secure Boot stays on.",
        "Windows vraagt één keer om beheerdersrechten. Secure Boot blijft aan.");
    public static string DriverTestNote => T("Experimental test build: the driver only loads after starting Windows with \"Disable driver signature enforcement\" (Shift + Restart > Troubleshoot > Advanced options > Startup Settings > F7).",
        "Experimentele testversie: de driver laadt alleen als Windows start met \"Afdwingen van stuurprogrammahandtekening uitschakelen\" (Shift + Opnieuw opstarten > Problemen oplossen > Geavanceerde opties > Opstartinstellingen > F7).");

    // Tray
    public static string ShowBattery => T("Show battery", "Batterij tonen");
    public static string TrayStartWithWindows => T("Start with Windows", "Starten met Windows");
    public static string Exit => T("Exit", "Afsluiten");
    public static string NothingNearby => T("PodPilot: no earbuds nearby", "PodPilot: geen oortjes in de buurt");
    public static string ShortLeft => T("L", "L");
    public static string ShortRight => T("R", "R");

    // Notifications
    public static string LowSingle(string name) => T($"{name} battery is low", $"Batterij van {name} is bijna leeg");
    public static string LowLeft => T("Left earbud battery is low", "Linker oortje is bijna leeg");
    public static string LowRight => T("Right earbud battery is low", "Rechter oortje is bijna leeg");
    public static string LowCase => T("Charging case battery is low", "Oplaadcase is bijna leeg");
    public static string Remaining(string name, int percent) => T($"{name}: {percent}% remaining.", $"{name}: nog {percent}%.");
    public static string Updated => T("PodPilot updated", "PodPilot bijgewerkt");
    public static string UpdatedText(string version) => T($"You now have PodPilot {version}.", $"Je hebt nu PodPilot {version}.");
    public static string Checking => T("Checking...", "Zoeken...");
    public static string UpToDate => T("PodPilot is up to date.", "PodPilot is up-to-date.");
    public static string UpdateAvailable(string version) => T($"Version {version} is available.", $"Versie {version} is beschikbaar.");
    public static string UpdateAvailableTitle => T("PodPilot update available", "PodPilot-update beschikbaar");
    public static string UpdateReady(string version) => T($"Version {version} is ready. Open Settings to install it.", $"Versie {version} staat klaar. Open Instellingen om te installeren.");
    public static string Installing(string version) => T($"Installing {version}...", $"{version} installeren...");
    public static string UpdateFailed => T("The update could not be installed. Download it from GitHub.", "De update kon niet worden geïnstalleerd. Download hem via GitHub.");
    public static string GitHubUnreachable => T("Could not reach GitHub.", "GitHub is niet bereikbaar.");
    public static string DriverRemoved => T("The noise control driver was removed.", "De ruisregeling-driver is verwijderd.");
    public static string DriverInstalled => T("The noise control driver is installed.", "De ruisregeling-driver is geïnstalleerd.");
    public static string DriverRemoveFailed => T("The driver could not be removed.", "De driver kon niet worden verwijderd.");
    public static string DriverInstallFailed => T("The driver was not installed.", "De driver is niet geïnstalleerd.");
    public static string ConnectFailed(string name) => T($"{name} did not connect. Make sure they are out of the case and nearby.",
        $"{name} kon niet verbinden. Zorg dat ze uit de case en dichtbij zijn.");
    public static string Volume => T("Volume", "Volume");
    public static string FirstRun => T("PodPilot runs in the notification area. Click its icon to open it.",
        "PodPilot staat in het systeemvak (bij de klok). Klik op het icoon om het te openen.");
}
