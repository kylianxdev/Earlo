using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PodPilot.App.Services;

/// <summary>
/// Compile-time JSON serialization, so settings keep working in the trimmed standalone build
/// (reflection-based System.Text.Json is disabled there).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(Dictionary<ushort, CaseBatteryMemory.Entry>))]
internal sealed partial class PodPilotJsonContext : JsonSerializerContext
{
}
