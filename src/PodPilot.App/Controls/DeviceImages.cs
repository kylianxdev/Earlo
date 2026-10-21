using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PodPilot.App.Controls;

/// <summary>Photorealistic renders per model family (Assets/Devices). Unknown models fall back to vector art.</summary>
public static class DeviceImages
{
    static readonly Dictionary<ushort, string> Files = new()
    {
        [0x0220] = "airpods-2",
        [0x0F20] = "airpods-2",
        [0x1320] = "airpods-3",
        [0x1920] = "airpods-4",
        [0x1B20] = "airpods-4",
        [0x0E20] = "airpods-pro",
        [0x1420] = "airpods-pro-2",
        [0x2420] = "airpods-pro-2",
        [0x2720] = "airpods-pro-2",
        [0x0A20] = "airpods-max",
        [0x1F20] = "airpods-max",
        [0x1220] = "beats-fit-pro",
        [0x1120] = "beats-studio-buds",
        [0x1620] = "beats-studio-buds",
        [0x2620] = "beats-studio-buds",
        [0x0B20] = "powerbeats-pro",
        [0x1D20] = "powerbeats-pro",
        [0x0620] = "beats-headphones",
        [0x0920] = "beats-headphones",
        [0x0C20] = "beats-headphones",
        [0x1720] = "beats-headphones",
        [0x2520] = "beats-headphones",
        [0x0320] = "beats-flex",
        [0x0520] = "beats-flex",
        [0x0D20] = "beats-flex",
        [0x1020] = "beats-flex",
    };

    static readonly Dictionary<string, ImageSource?> Cache = [];

    public static ImageSource? For(ushort modelId)
    {
        if (!Files.TryGetValue(modelId, out var name))
            return null;
        if (Cache.TryGetValue(name, out var cached))
            return cached;

        ImageSource? image = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Devices/{name}.png");
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
        }
        catch (Exception)
        {
            // Missing resource: use the vector art.
        }
        Cache[name] = image;
        return image;
    }
}
