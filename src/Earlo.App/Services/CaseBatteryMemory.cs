using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Earlo.Core.Tracking;

namespace Earlo.App.Services;

/// <summary>
/// Remembers the last case battery level per model, also across restarts.
/// </summary>
/// <remarks>
/// AirPods only report the case level while at least one pod is in the case, so with both pods
/// in your ears there is nothing to show. Like the iPhone, Earlo then shows the last known level.
/// </remarks>
public sealed class CaseBatteryMemory
{
    static readonly string FilePath = Path.Combine(DataFolder.Root, "case-battery.json");

    static readonly TimeSpan MaxAge = TimeSpan.FromDays(3);

    readonly Dictionary<ushort, Entry> _entries;

    public CaseBatteryMemory()
    {
        try
        {
            _entries = File.Exists(FilePath)
                ? JsonSerializer.Deserialize(File.ReadAllText(FilePath), EarloJsonContext.Default.DictionaryUInt16Entry) ?? []
                : [];
        }
        catch (Exception)
        {
            _entries = [];
        }
    }

    /// <summary>Stores a reported level, or fills in the remembered one when the case is silent.</summary>
    public PodsSnapshot Apply(PodsSnapshot snapshot)
    {
        ushort model = snapshot.Device.ModelId;
        if (!snapshot.Device.HasCase)
            return snapshot;

        if (snapshot.Case.Percent is int percent && !snapshot.Case.Stale)
        {
            if (!_entries.TryGetValue(model, out var old) || old.Percent != percent)
            {
                _entries[model] = new Entry(percent, DateTimeOffset.UtcNow);
                Save();
            }
            return snapshot;
        }

        if (_entries.TryGetValue(model, out var entry) && DateTimeOffset.UtcNow - entry.Seen < MaxAge)
            return snapshot with { Case = new BatteryState(entry.Percent, false, Stale: true) };
        return snapshot;
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_entries, EarloJsonContext.Default.DictionaryUInt16Entry));
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public sealed record Entry(int Percent, DateTimeOffset Seen);
}
