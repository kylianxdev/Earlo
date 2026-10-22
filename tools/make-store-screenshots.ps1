# Builds 1920x1080 Store screenshots from the app's own renders, with a caption in the listing's language.
#
#   Earlo.exe --render-preview renders-nl --lang nl
#   pwsh -File tools/make-store-screenshots.ps1 -Renders renders-nl -Lang nl -Out store-screenshots/nl
#
# Images missing from -Renders (all-models.png) come from docs/screenshots. The Store version has no
# noise-control driver, so no screenshot shows noise control. CI builds both languages and puts them in
# the "store" artifact next to Earlo-store.msixbundle.
param(
    [string] $Renders,
    [ValidateSet('en', 'nl')] [string] $Lang = 'en',
    [string] $Out
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$docs = Join-Path $root 'docs/screenshots'
if (-not $Renders) { $Renders = $docs }
if (-not $Out) { $Out = "store-screenshots/$Lang" }
New-Item -ItemType Directory -Force $Out | Out-Null

# The first screenshots are the ones people see in search results, so they carry the words people search for.
$captions = @{
    en = @{
        popup  = 'The AirPods battery popup, now on Windows'
        sound  = 'Muffled AirPods sound on Windows? One switch'
        ear    = 'See every earbud, pause when you take one out'
        galaxy = 'Galaxy Buds and Beats too'
        models = 'AirPods, Beats and Galaxy Buds'
    }
    nl = @{
        popup  = 'De AirPods-batterijpopup, nu op Windows'
        sound  = 'Dof geluid van je AirPods? Eén schakelaar'
        ear    = 'Zie elk oortje, muziek pauzeert als je er een uitdoet'
        galaxy = 'Ook Galaxy Buds en Beats'
        models = 'AirPods, Beats en Galaxy Buds'
    }
}[$Lang]

function Find-Image([string]$name) {
    foreach ($folder in $Renders, $docs) {
        $file = Join-Path $folder $name
        if (Test-Path $file) { return $file }
    }
    throw "$name not found in $Renders or $docs"
}

function New-Shot([string]$image, [string]$caption, [string]$file) {
    $bmp = New-Object System.Drawing.Bitmap 1920, 1080
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 1920, 1080), ([System.Drawing.Color]::FromArgb(255, 44, 45, 50)), ([System.Drawing.Color]::FromArgb(255, 12, 12, 14))
    $g.FillRectangle($bg, 0, 0, 1920, 1080)

    # Shrink long captions (Dutch runs longer) so they stay on one line.
    $size = 54
    do {
        $font = New-Object System.Drawing.Font 'Segoe UI Semibold', $size
        $width = $g.MeasureString($caption, $font).Width
        $size -= 2
    } while ($width -gt 1800 -and $size -gt 30)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $g.DrawString($caption, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 70, 1920, 120), $format)

    $img = [System.Drawing.Image]::FromFile((Find-Image $image))
    $scale = [Math]::Min(1600 / $img.Width, 820 / $img.Height)
    $w = [int]($img.Width * $scale); $h = [int]($img.Height * $scale)
    $g.DrawImage($img, [int]((1920 - $w) / 2), [int](210 + (820 - $h) / 2), $w, $h)
    $img.Dispose(); $g.Dispose()
    $bmp.Save((Join-Path $Out $file), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

New-Shot 'popup-airpods-pro.png' $captions.popup '1-popup.png'
New-Shot 'settings-store.png' $captions.sound '2-sound.png'
New-Shot 'popup-ear-detection.png' $captions.ear '3-ear-detection.png'
New-Shot 'popup-galaxy-buds.png' $captions.galaxy '4-galaxy-buds.png'
New-Shot 'all-models.png' $captions.models '5-all-models.png'
Write-Host "Wrote $Lang screenshots to $Out"
