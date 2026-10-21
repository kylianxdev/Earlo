# Generates src/PodPilot.App/Assets/podpilot.ico (and a 256px PNG for the README).
# Uses tools/icon-source.png when present, otherwise draws a simple vector icon.
# Run from the repository root: powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1
Add-Type -AssemblyName System.Drawing

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$source = Join-Path $PSScriptRoot 'icon-source.png'
$sourceImage = if (Test-Path $source) { [System.Drawing.Image]::FromFile($source) } else { $null }

function New-IconBitmap([int]$size) {
    if ($sourceImage) {
        # Downscale the rendered icon (tools/icon-source.png, transparent background).
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'
        $g.PixelOffsetMode = 'HighQuality'
        $g.SmoothingMode = 'AntiAlias'
        $g.DrawImage($sourceImage, 0, 0, $size, $size)
        $g.Dispose()
        return $bmp
    }
    return New-VectorIconBitmap $size
}

function New-VectorIconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $s = $size / 256.0

    $bg = New-RoundedRect (8 * $s) (8 * $s) (240 * $s) (240 * $s) (56 * $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $size, $size), ([System.Drawing.Color]::FromArgb(255, 10, 132, 255)), ([System.Drawing.Color]::FromArgb(255, 94, 92, 230))
    $g.FillPath($brush, $bg)

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $shade = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 10, 40, 120))
    foreach ($mirror in @($false, $true)) {
        $state = $g.Save()
        if ($mirror) {
            $g.TranslateTransform($size, 0)
            $g.ScaleTransform(-1, 1)
        }
        # Pod head and stem
        $g.FillEllipse($white, 52 * $s, 58 * $s, 64 * $s, 58 * $s)
        $stem = New-RoundedRect (90 * $s) (84 * $s) (26 * $s) (118 * $s) (13 * $s)
        $g.FillPath($white, $stem)
        $g.FillEllipse($shade, 60 * $s, 74 * $s, 16 * $s, 22 * $s)
        $g.Restore($state)
    }
    $g.Dispose()
    return $bmp
}

$root = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $root 'src/PodPilot.App/Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
# 256px is stored as PNG; smaller sizes as 32-bit DIBs, because GDI+ (tray icons) cannot
# decode PNG entries at small sizes.
function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $size = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    # BITMAPINFOHEADER; height is doubled to include the AND mask
    $w.Write([uint32]40); $w.Write([int32]$size); $w.Write([int32]($size * 2))
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]0)
    $w.Write([uint32]0); $w.Write([int32]0); $w.Write([int32]0); $w.Write([uint32]0); $w.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $w.Write([byte]$c.B); $w.Write([byte]$c.G); $w.Write([byte]$c.R); $w.Write([byte]$c.A)
        }
    }
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $w.Write((New-Object byte[] ($maskStride * $size)))
    $w.Flush()
    return , $ms.ToArray()
}

$pngs = foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    if ($size -eq 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Save((Join-Path $root 'docs/icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    } else {
        $bytes = Get-DibBytes $bmp
    }
    $bmp.Dispose()
    , $bytes
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $w.Write($png) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'podpilot.ico'), $out.ToArray())
Write-Host "Wrote $assets\podpilot.ico"
