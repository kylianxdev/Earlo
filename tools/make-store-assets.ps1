# Generates the MSIX/Store logos in store/Assets from tools/icon-source.png.
# Run from anywhere: powershell -ExecutionPolicy Bypass -File tools/make-store-assets.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$source = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'icon-source.png'))
$out = Join-Path $root 'store/Assets'
New-Item -ItemType Directory -Force $out | Out-Null

function Save-Logo([string]$name, [int]$width, [int]$height, [double]$iconScale = 1.0) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.PixelOffsetMode = 'HighQuality'
    $g.SmoothingMode = 'AntiAlias'
    $size = [int]([Math]::Min($width, $height) * $iconScale)
    $g.DrawImage($source, [int](($width - $size) / 2), [int](($height - $size) / 2), $size, $size)
    $g.Dispose()
    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

foreach ($scale in 100, 200) {
    $f = $scale / 100
    Save-Logo "Square44x44Logo.scale-$scale.png" (44 * $f) (44 * $f)
    Save-Logo "Square150x150Logo.scale-$scale.png" (150 * $f) (150 * $f) 0.66
    Save-Logo "Wide310x150Logo.scale-$scale.png" (310 * $f) (150 * $f) 0.66
    Save-Logo "StoreLogo.scale-$scale.png" (50 * $f) (50 * $f)
}
# Taskbar and Start list icons without the tile plate.
foreach ($size in 16, 24, 32, 48, 256) {
    Save-Logo "Square44x44Logo.targetsize-$size.png" $size $size
    Save-Logo "Square44x44Logo.targetsize-${size}_altform-unplated.png" $size $size
}
$source.Dispose()
Write-Host "Wrote logos to $out"
