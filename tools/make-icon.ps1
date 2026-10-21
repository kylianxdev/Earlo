# Generates src/PodPilot.App/Assets/podpilot.ico (and a 256px PNG for the README).
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

function New-IconBitmap([int]$size) {
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
$pngs = foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($size -eq 256) { $bmp.Save((Join-Path $root 'docs/icon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    , $ms.ToArray()
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
