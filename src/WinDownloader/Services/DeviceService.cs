using System.Diagnostics;
using System.Management;
using WinDownloader.Models;

namespace WinDownloader.Services;

/// <summary>Lists hardware (incl. USB devices) with the driver each one is using, via WMI.</summary>
public sealed class DeviceService
{
    public Task<List<DeviceInfo>> ScanAsync(IProgress<string> log) => Task.Run(() => Scan(log));

    private List<DeviceInfo> Scan(IProgress<string> log)
    {
        var problems = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var services = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var s = new ManagementObjectSearcher("SELECT DeviceID, Name, ConfigManagerErrorCode, Service FROM Win32_PnPEntity");
            foreach (ManagementObject o in s.Get())
            {
                string id = o["DeviceID"]?.ToString() ?? "";
                if (id.Length == 0) continue;
                problems[id] = Convert.ToInt32(o["ConfigManagerErrorCode"] ?? 0);
                names[id] = o["Name"]?.ToString() ?? "";
                services[id] = o["Service"]?.ToString() ?? "";
            }
        }
        catch (Exception ex) { log.Report("Device list: " + WindowsUpdateService.Short(ex)); }

        var list = new List<DeviceInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var s = new ManagementObjectSearcher(
                "SELECT DeviceID, DeviceName, DriverVersion, DriverDate, Manufacturer, DeviceClass, HardWareID, DriverProviderName, IsSigned FROM Win32_PnPSignedDriver");
            foreach (ManagementObject o in s.Get())
            {
                string id = o["DeviceID"]?.ToString() ?? "";
                string name = o["DeviceName"]?.ToString() ?? (names.TryGetValue(id, out var n) ? n : "");
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(id)) continue;

                DateTime? date = null;
                try { var raw = o["DriverDate"]?.ToString(); if (!string.IsNullOrEmpty(raw)) date = ManagementDateTimeConverter.ToDateTime(raw); } catch { }

                string hwid = o["HardWareID"]?.ToString() ?? "";
                string cls = o["DeviceClass"]?.ToString() ?? "";
                string svc = services.TryGetValue(id, out var sv) ? sv : "";
                bool isUsb = id.StartsWith("USB", StringComparison.OrdinalIgnoreCase)
                             || cls.Equals("USB", StringComparison.OrdinalIgnoreCase)
                             || svc.Contains("USB", StringComparison.OrdinalIgnoreCase);

                list.Add(new DeviceInfo
                {
                    Name = name,
                    Manufacturer = Clean(o["Manufacturer"]?.ToString()),
                    DeviceClass = Pretty(cls),
                    DeviceId = id,
                    DriverVersion = o["DriverVersion"]?.ToString() ?? "",
                    DriverProvider = Clean(o["DriverProviderName"]?.ToString()),
                    DriverDate = date,
                    IsUsb = isUsb,
                    IsSigned = o["IsSigned"] is bool b ? b : true,
                    ProblemCode = problems.TryGetValue(id, out var p) ? p : 0,
                    HardwareIds = string.IsNullOrEmpty(hwid) ? new[] { id } : new[] { hwid, id }
                });
            }
        }
        catch (Exception ex) { log.Report("Driver list: " + WindowsUpdateService.Short(ex)); }

        // Devices without a signed-driver row (e.g. problem devices with no driver at all).
        foreach (var kv in problems.Where(kv => kv.Value != 0 && !seen.Contains(kv.Key)))
        {
            list.Add(new DeviceInfo
            {
                Name = names.TryGetValue(kv.Key, out var n) ? n : kv.Key,
                DeviceId = kv.Key,
                DeviceClass = "Unknown",
                ProblemCode = kv.Value,
                IsUsb = kv.Key.StartsWith("USB", StringComparison.OrdinalIgnoreCase),
                HardwareIds = new[] { kv.Key }
            });
        }

        return list
            .OrderByDescending(d => d.HasProblem)
            .ThenByDescending(d => d.IsUsb)
            .ThenBy(d => d.DeviceClass)
            .ThenBy(d => d.Name)
            .ToList();
    }

    /// <summary>Pairs driver updates from Windows Update with the devices they target.</summary>
    public static void MatchUpdates(IEnumerable<DeviceInfo> devices, IEnumerable<UpdateItem> driverUpdates)
    {
        var updates = driverUpdates.Where(u => u.Kind == UpdateKind.Driver).ToList();
        foreach (var d in devices)
        {
            d.AvailableUpdate = updates.FirstOrDefault(u =>
                !string.IsNullOrEmpty(u.HardwareId) &&
                d.HardwareIds.Any(h => h.StartsWith(u.HardwareId, StringComparison.OrdinalIgnoreCase)
                                    || u.HardwareId.StartsWith(h, StringComparison.OrdinalIgnoreCase)));
        }
    }

    public static Task<string> RescanHardwareAsync() => Task.Run(() =>
    {
        try
        {
            var psi = new ProcessStartInfo("pnputil.exe", "/scan-devices")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using var p = Process.Start(psi);
            if (p == null) return "Could not start pnputil.";
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(60000);
            return output.Trim().Length == 0 ? "Hardware rescan finished." : output.Trim();
        }
        catch (Exception ex) { return "Hardware rescan failed: " + ex.Message; }
    });

    private static string Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Trim();
        return s.StartsWith("(Standard", StringComparison.OrdinalIgnoreCase) ? "Microsoft" : s;
    }

    private static string Pretty(string cls)
    {
        if (string.IsNullOrEmpty(cls)) return "";
        return cls.ToUpperInvariant() switch
        {
            "HIDCLASS" => "Human Interface Device",
            "USB" => "USB controller",
            "USBDEVICE" => "USB device",
            "DISKDRIVE" => "Disk drive",
            "NET" => "Network adapter",
            "DISPLAY" => "Display adapter",
            "MEDIA" => "Audio",
            "AUDIOENDPOINT" => "Audio endpoint",
            "BLUETOOTH" => "Bluetooth",
            "SYSTEM" => "System device",
            "MONITOR" => "Monitor",
            "KEYBOARD" => "Keyboard",
            "MOUSE" => "Mouse",
            "IMAGE" => "Camera / imaging",
            "PRINTER" => "Printer",
            "SOFTWARECOMPONENT" => "Software component",
            "SOFTWAREDEVICE" => "Software device",
            "FIRMWARE" => "Firmware",
            "PROCESSOR" => "Processor",
            "VOLUME" => "Storage volume",
            "SCSIADAPTER" => "Storage controller",
            "HDC" => "IDE/ATA controller",
            _ => char.ToUpperInvariant(cls[0]) + cls[1..].ToLowerInvariant()
        };
    }
}
