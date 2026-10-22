# Builds the optional Store logo art (1:1 box art and 9:16 poster art) in store/Art from tools/icon-source.png.
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'store/Art'
New-Item -ItemType Directory -Force $out | Out-Null
$icon = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'icon-source.png'))

function New-Art([int]$width, [int]$height, [string]$file, [bool]$withName) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Point 0, 0), (New-Object System.Drawing.Point $width, $height), ([System.Drawing.Color]::FromArgb(255, 44, 45, 50)), ([System.Drawing.Color]::FromArgb(255, 12, 12, 14))
    $g.FillRectangle($bg, 0, 0, $width, $height)
    $size = [int]([Math]::Min($width, $height) * 0.62)
    $top = if ($withName) { [int](($height - $size) / 2 - $height * 0.06) } else { [int](($height - $size) / 2) }
    $g.DrawImage($icon, [int](($width - $size) / 2), $top, $size, $size)
    if ($withName) {
        $font = New-Object System.Drawing.Font 'Segoe UI Semibold', ([float]($width * 0.11)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
        $format = New-Object System.Drawing.StringFormat
        $format.Alignment = 'Center'
        $g.DrawString('Earlo', $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, ($top + $size + $height * 0.03), $width, ($height * 0.2)), $format)
    }
    $g.Dispose()
    $bmp.Save((Join-Path $out $file), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

New-Art 1080 1080 'box-art-1080.png' $false
New-Art 720 1080 'poster-art-720x1080.png' $true
$icon.Dispose()
Write-Host "Wrote art to $out"
