using System.Diagnostics;
using System.Text;
using WinDownloader.Models;

namespace WinDownloader.Services;

/// <summary>App updates through the Windows Package Manager (winget), which ships with Windows 10/11.</summary>
public sealed class WingetService
{
    public static bool IsAvailable()
    {
        try
        {
            var r = Run("--version", 15000);
            return r.exitCode == 0 && r.output.Contains('v');
        }
        catch { return false; }
    }

    public Task<List<UpdateItem>> SearchAsync(IProgress<string> log) => Task.Run(() =>
    {
        var items = new List<UpdateItem>();
        try
        {
            var r = Run("upgrade --include-unknown --accept-source-agreements --disable-interactivity", 120000);
            var lines = r.output.Replace("\r", "").Split('\n');

            int header = Array.FindIndex(lines, l => l.Contains("Name") && l.Contains("Id") && l.Contains("Version") && l.Contains("Available"));
            if (header < 0) { log.Report("winget: no upgradable apps found."); return items; }

            string h = lines[header];
            int idCol = h.IndexOf("Id", StringComparison.Ordinal);
            int verCol = h.IndexOf("Version", StringComparison.Ordinal);
            int availCol = h.IndexOf("Available", StringComparison.Ordinal);
            int srcCol = h.IndexOf("Source", StringComparison.Ordinal);

            for (int i = header + 2; i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.Trim().Length == 0 || l.Length <= availCol) continue;
                if (char.IsDigit(l.TrimStart()[0]) && l.Contains("upgrade", StringComparison.OrdinalIgnoreCase)) break;
                if (l.StartsWith("The following", StringComparison.OrdinalIgnoreCase)) break;

                string name = Slice(l, 0, idCol);
                string id = Slice(l, idCol, verCol);
                string ver = Slice(l, verCol, availCol);
                string avail = srcCol > availCol ? Slice(l, availCol, srcCol) : l[availCol..].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(avail) || avail.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) continue;
                if (id.Contains(' ')) continue; // mis-parsed line

                items.Add(new UpdateItem
                {
                    Id = id,
                    Title = name,
                    Kind = UpdateKind.App,
                    Severity = UpdateSeverity.Optional,
                    Category = "App",
                    CurrentVersion = ver,
                    AvailableVersion = avail,
                    Native = id
                });
            }
            log.Report($"winget found {items.Count} app update(s).");
        }
        catch (Exception ex) { log.Report("winget: " + ex.Message); }
        return items;
    });

    public Task InstallAsync(IEnumerable<UpdateItem> apps, IProgress<string> log, CancellationToken ct) => Task.Run(() =>
    {
        foreach (var app in apps)
        {
            if (ct.IsCancellationRequested) { app.Status = UpdateStatus.Skipped; continue; }
            app.Status = UpdateStatus.Installing; app.Progress = 30;
            log.Report($"Upgrading app: {app.Title}");
            try
            {
                var r = Run($"upgrade --id \"{app.Id}\" --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", 15 * 60 * 1000);
                app.Progress = 100;
                if (r.exitCode == 0) { app.Status = UpdateStatus.Installed; log.Report($"Upgraded: {app.Title}"); }
                else
                {
                    app.Status = UpdateStatus.Failed;
                    app.Message = $"winget exit {r.exitCode}";
                    log.Report($"App upgrade failed: {app.Title} (exit {r.exitCode})");
                }
            }
            catch (Exception ex) { app.Status = UpdateStatus.Failed; app.Message = ex.Message; }
        }
    }, ct);

    private static string Slice(string line, int from, int to)
    {
        if (from >= line.Length) return "";
        to = Math.Min(to, line.Length);
        return to <= from ? "" : line[from..to].Trim();
    }

    private static (int exitCode, string output) Run(string args, int timeoutMs)
    {
        var psi = new ProcessStartInfo("winget.exe", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("winget could not be started");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } }
        return (p.HasExited ? p.ExitCode : -1, stdout.Result + "\n" + stderr.Result);
    }
}
