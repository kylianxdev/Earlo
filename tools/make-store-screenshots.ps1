# Builds 1920x1080 Store screenshots in store/Screenshots from the popup renders in docs/screenshots.
# Run tools after: Earlo.exe --render-preview docs\screenshots
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'store/Screenshots'
New-Item -ItemType Directory -Force $out | Out-Null

function New-Shot([string]$image, [string]$caption, [string]$file) {
    $bmp = New-Object System.Drawing.Bitmap 1920, 1080
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point 1920, 1080), ([System.Drawing.Color]::FromArgb(255, 44, 45, 50)), ([System.Drawing.Color]::FromArgb(255, 12, 12, 14))
    $g.FillRectangle($bg, 0, 0, 1920, 1080)

    $font = New-Object System.Drawing.Font 'Segoe UI Semibold', 54
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $g.DrawString($caption, $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 70, 1920, 120), $format)

    $img = [System.Drawing.Image]::FromFile((Join-Path $root "docs/screenshots/$image"))
    $scale = [Math]::Min(1600 / $img.Width, 820 / $img.Height)
    $w = [int]($img.Width * $scale); $h = [int]($img.Height * $scale)
    $g.DrawImage($img, [int]((1920 - $w) / 2), [int](210 + (820 - $h) / 2), $w, $h)
    $img.Dispose(); $g.Dispose()
    $bmp.Save((Join-Path $out $file), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

New-Shot 'popup-airpods-pro.png' 'The iPhone battery popup, on Windows' '1-popup.png'
New-Shot 'popup-ear-detection.png' 'See every earbud, live' '2-ear-detection.png'
New-Shot 'settings.png' 'Connect in one click, pause when you take them out' '3-settings.png'
New-Shot 'popup-galaxy-buds.png' 'Galaxy Buds too' '4-galaxy-buds.png'
New-Shot 'all-models.png' 'AirPods, Beats and Galaxy Buds' '5-all-models.png'
Write-Host "Wrote screenshots to $out"
