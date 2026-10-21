using System;
using System.IO;
using PodPilot.Core.Bluetooth;
using PodPilot.Core.Devices;

namespace PodPilot.App.Services;

/// <summary>
/// Writes every received proximity pairing message to a text file (start with <c>--log &lt;file&gt;</c>).
/// Attach this file to bug reports, for example for a model PodPilot does not recognise yet.
/// </summary>
public static class DiagnosticsLog
{
    static readonly object Gate = new();

    public static void Attach(AdvertisementScanner scanner, string file)
    {
        Write(file, $"PodPilot diagnostics started {DateTimeOffset.Now:O}");
        scanner.StatusChanged += (_, status) => Write(file, $"scanner: {status}");
        scanner.Received += (_, a) =>
        {
            var m = a.Message;
            var device = DeviceCatalog.Resolve(m.ModelId, m.ReportsPods);
            Write(file,
                $"{DateTimeOffset.Now:HH:mm:ss.fff} rssi={a.Rssi,4} addr={a.Address:X12} model=0x{m.ModelId:X4} ({device.Name}) " +
                $"status=0x{m.Status:X2} L={m.LeftBattery?.ToString() ?? "-"}{(m.LeftCharging ? "+" : "")} " +
                $"R={m.RightBattery?.ToString() ?? "-"}{(m.RightCharging ? "+" : "")} " +
                $"case={m.CaseBattery?.ToString() ?? "-"}{(m.CaseCharging ? "+" : "")} " +
                $"inEar={(m.LeftInEar ? "L" : "")}{(m.RightInEar ? "R" : "")} lid={(m.LidOpen ? "open" : "closed")} color={m.Color}");
        };
    }

    static void Write(string file, string line)
    {
        lock (Gate)
        {
            try
            {
                File.AppendAllText(file, line + Environment.NewLine);
            }
            catch (IOException)
            {
            }
        }
    }
}
