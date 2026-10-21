# Turns a render on a pure green background into a trimmed PNG with transparency.
# Usage: powershell -ExecutionPolicy Bypass -File tools/chroma-key.ps1 <input> <output> [maxWidth]
param(
    [Parameter(Mandatory)] [string] $InputPath,
    [Parameter(Mandatory)] [string] $OutputPath,
    [int] $MaxWidth = 720
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class ChromaKey
{
    public static Bitmap Apply(Bitmap source)
    {
        int w = source.Width, h = source.Height;
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp)) g.DrawImage(source, 0, 0, w, h);

        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var px = new byte[data.Stride * h];
        Marshal.Copy(data.Scan0, px, 0, px.Length);

        for (int i = 0; i < px.Length; i += 4)
        {
            int b = px[i], gr = px[i + 1], r = px[i + 2];
            int other = Math.Max(r, b);
            int spill = gr - other;                    // how much greener than the other channels
            double alpha = 1.0 - Clamp((spill - 25) / 80.0);
            if (spill > 0) gr = other;                 // remove green spill from the edges
            px[i + 1] = (byte)gr;
            px[i + 3] = (byte)(alpha * 255);
        }

        Marshal.Copy(px, 0, data.Scan0, px.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    public static Rectangle Bounds(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height, minX = w, minY = h, maxX = 0, maxY = 0;
        var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new byte[data.Stride * h];
        Marshal.Copy(data.Scan0, px, 0, px.Length);
        bmp.UnlockBits(data);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * data.Stride + x * 4 + 3] > 24)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < minX) return new Rectangle(0, 0, w, h);
        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    static double Clamp(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
}
"@

$src = [System.Drawing.Bitmap]::FromFile((Resolve-Path $InputPath))
$keyed = [ChromaKey]::Apply($src)
$src.Dispose()

$bounds = [ChromaKey]::Bounds($keyed)
$pad = [int]([Math]::Max($bounds.Width, $bounds.Height) * 0.04)
$crop = [System.Drawing.Rectangle]::FromLTRB(
    [Math]::Max(0, $bounds.Left - $pad), [Math]::Max(0, $bounds.Top - $pad),
    [Math]::Min($keyed.Width, $bounds.Right + $pad), [Math]::Min($keyed.Height, $bounds.Bottom + $pad))

$scale = [Math]::Min(1.0, $MaxWidth / $crop.Width)
$outW = [int]($crop.Width * $scale); $outH = [int]($crop.Height * $scale)
$out = New-Object System.Drawing.Bitmap $outW, $outH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($out)
$g.InterpolationMode = 'HighQualityBicubic'
$g.PixelOffsetMode = 'HighQuality'
$g.DrawImage($keyed, (New-Object System.Drawing.Rectangle 0, 0, $outW, $outH), $crop, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$out.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$out.Dispose(); $keyed.Dispose()
Write-Host "Wrote $OutputPath ($outW x $outH)"
