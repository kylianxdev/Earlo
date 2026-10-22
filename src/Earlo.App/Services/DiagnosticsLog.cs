using System;
using System.IO;
using Earlo.Core.Aap;
using Earlo.Core.Bluetooth;
using Earlo.Core.Devices;

namespace Earlo.App.Services;

/// <summary>
/// Writes every received proximity pairing message to a text file (start with <c>--log &lt;file&gt;</c>).
/// Attach this file to bug reports, for example for a model Earlo does not recognise yet.
/// </summary>
public static class DiagnosticsLog
{
    static readonly object Gate = new();

    public static void Attach(AdvertisementScanner scanner, AapDeviceManager aap, string file)
    {
        Write(file, $"Earlo diagnostics started {DateTimeOffset.Now:O}");
        aap.PacketTraced += (incoming, packet) =>
            Write(file, $"{DateTimeOffset.Now:HH:mm:ss.fff} aap {(incoming ? "<-" : "->")} {Convert.ToHexString(packet.Span)}");
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
