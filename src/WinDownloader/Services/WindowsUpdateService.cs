using System.Runtime.InteropServices;
using WinDownloader.Models;

namespace WinDownloader.Services;

/// <summary>
/// Talks to the Windows Update Agent (WUA) through its COM API. This is the same engine that the
/// Settings → Windows Update page uses, so everything we find and install is a genuine Microsoft update.
/// Drivers come through the same channel (Type='Driver'), including USB / chipset / GPU drivers that
/// manufacturers publish to Microsoft Update.
/// </summary>
public sealed class WindowsUpdateService
{
    private const string MicrosoftUpdateServiceId = "7971f918-a847-4430-9279-4a52d1efe18d";

    private static dynamic CreateCom(string progId) =>
        Activator.CreateInstance(Type.GetTypeFromProgID(progId, true)!)!;

    public Task<List<UpdateItem>> SearchAsync(bool includeDrivers, bool includeOptional, bool useMicrosoftUpdate, IProgress<string> log, CancellationToken ct)
        => Task.Run(() => Search(includeDrivers, includeOptional, useMicrosoftUpdate, log, ct), ct);

    private List<UpdateItem> Search(bool includeDrivers, bool includeOptional, bool useMicrosoftUpdate, IProgress<string> log, CancellationToken ct)
    {
        var items = new List<UpdateItem>();
        dynamic session = CreateCom("Microsoft.Update.Session");
        session.ClientApplicationID = "WinDownloader";
        dynamic searcher = session.CreateUpdateSearcher();
        searcher.Online = true;

        if (useMicrosoftUpdate)
        {
            try
            {
                // Register Microsoft Update (drivers, Office, etc.) in addition to plain Windows Update.
                dynamic manager = CreateCom("Microsoft.Update.ServiceManager");
                manager.ClientApplicationID = "WinDownloader";
                manager.AddService2(MicrosoftUpdateServiceId, 7, "");
                searcher.ServerSelection = 3; // ssOthers
                searcher.ServiceID = MicrosoftUpdateServiceId;
                log.Report("Using Microsoft Update (Windows + drivers + Microsoft apps).");
            }
            catch (Exception ex)
            {
                log.Report("Microsoft Update not available, falling back to Windows Update. " + Short(ex));
                searcher.ServerSelection = 2; // ssWindowsUpdate
            }
        }
        else
        {
            searcher.ServerSelection = 2;
        }

        string criteria = "IsInstalled=0 and IsHidden=0";
        if (!includeDrivers) criteria += " and Type='Software'";
        log.Report("Searching for updates… this can take a minute.");

        dynamic result = searcher.Search(criteria);
        ct.ThrowIfCancellationRequested();
        int count = (int)result.Updates.Count;
        log.Report($"Windows Update returned {count} item(s).");

        for (int i = 0; i < count; i++)
        {
            dynamic u = result.Updates.Item(i);
            try
            {
                int type = (int)u.Type; // 1 = Software, 2 = Driver
                bool autoSelect = SafeGet(() => (bool)u.AutoSelectOnWebSites, false);
                string msrc = SafeGet(() => (string)u.MsrcSeverity, "") ?? "";
                var categories = new List<string>();
                try
                {
                    int cc = (int)u.Categories.Count;
                    for (int j = 0; j < cc; j++) categories.Add((string)u.Categories.Item(j).Name);
                }
                catch { }

                bool isSecurity = categories.Any(c => c.Contains("Security", StringComparison.OrdinalIgnoreCase)) || msrc.Length > 0;
                bool isDefinition = categories.Any(c => c.Contains("Definition", StringComparison.OrdinalIgnoreCase));
                UpdateSeverity severity =
                    msrc.Equals("Critical", StringComparison.OrdinalIgnoreCase) || (isSecurity && autoSelect) ? UpdateSeverity.Critical
                    : autoSelect || isDefinition ? UpdateSeverity.Important
                    : UpdateSeverity.Optional;

                if (!includeOptional && severity == UpdateSeverity.Optional && type != 2) continue;

                string kb = "";
                try { if ((int)u.KBArticleIDs.Count > 0) kb = (string)u.KBArticleIDs.Item(0); } catch { }

                long size = 0;
                try { size = (long)Convert.ToDecimal(u.MaxDownloadSize); } catch { }

                string hardwareId = "", provider = "", model = "";
                if (type == 2)
                {
                    hardwareId = SafeGet(() => (string)u.DriverHardwareID, "") ?? "";
                    provider = SafeGet(() => (string)u.DriverProvider, "") ?? "";
                    model = SafeGet(() => (string)u.DriverModel, "") ?? "";
                }

                string category = type == 2
                    ? (string.IsNullOrEmpty(provider) ? "Driver" : provider + " driver")
                    : categories.FirstOrDefault(c => !c.StartsWith("Windows", StringComparison.OrdinalIgnoreCase)) ?? categories.FirstOrDefault() ?? "";

                items.Add(new UpdateItem
                {
                    Id = SafeGet(() => (string)u.Identity.UpdateID, Guid.NewGuid().ToString()) ?? Guid.NewGuid().ToString(),
                    Title = (string)u.Title,
                    Description = SafeGet(() => (string)u.Description, "") ?? "",
                    Kind = type == 2 ? UpdateKind.Driver : UpdateKind.Windows,
                    Severity = type == 2 ? (autoSelect ? UpdateSeverity.Important : UpdateSeverity.Optional) : severity,
                    SizeBytes = size,
                    Kb = kb,
                    Category = category,
                    HardwareId = hardwareId,
                    AvailableVersion = model,
                    RebootRequired = SafeGet(() => (bool)u.RebootRequired, false),
                    Native = u
                });
            }
            catch (Exception ex)
            {
                log.Report("Skipped one update that could not be read: " + Short(ex));
            }
        }
        return items;
    }

    /// <summary>Downloads and installs the given updates one by one so each row can show its own progress.</summary>
    public Task<bool> InstallAsync(IEnumerable<UpdateItem> items, IProgress<string> log, CancellationToken ct)
        => Task.Run(() => Install(items.ToList(), log, ct), ct);

    private bool Install(List<UpdateItem> items, IProgress<string> log, CancellationToken ct)
    {
        bool rebootRequired = false;
        dynamic session = CreateCom("Microsoft.Update.Session");
        session.ClientApplicationID = "WinDownloader";

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) { item.Status = UpdateStatus.Skipped; continue; }
            if (item.Native == null) { item.Status = UpdateStatus.Failed; item.Message = "no update handle"; continue; }
            dynamic u = item.Native;

            try
            {
                try { if (!(bool)u.EulaAccepted) u.AcceptEula(); } catch { }

                dynamic coll = CreateCom("Microsoft.Update.UpdateColl");
                coll.Add(u);

                bool downloaded = SafeGet(() => (bool)u.IsDownloaded, false);
                if (!downloaded)
                {
                    item.Status = UpdateStatus.Downloading; item.Progress = 10;
                    log.Report($"Downloading: {item.Title}");
                    dynamic downloader = session.CreateUpdateDownloader();
                    downloader.Updates = coll;
                    dynamic dres = downloader.Download();
                    int drc = (int)dres.ResultCode;
                    if (drc != 2 && drc != 3)
                    {
                        item.Status = UpdateStatus.Failed; item.Message = "download " + ResultName(drc) + HResultText(dres);
                        log.Report($"Download failed: {item.Title} ({item.Message})");
                        continue;
                    }
                }

                item.Status = UpdateStatus.Installing; item.Progress = 60;
                log.Report($"Installing: {item.Title}");
                dynamic installer = session.CreateUpdateInstaller();
                installer.Updates = coll;
                try { installer.ForceQuiet = true; } catch { }
                dynamic ires = installer.Install();
                int irc = (int)ires.ResultCode;
                bool ok = irc == 2 || irc == 3;
                item.Progress = 100;
                if (ok)
                {
                    bool rb = SafeGet(() => (bool)ires.RebootRequired, false);
                    item.RebootRequired = rb; rebootRequired |= rb;
                    item.Status = UpdateStatus.Installed;
                    log.Report($"Installed: {item.Title}" + (rb ? " (restart required)" : ""));
                }
                else
                {
                    item.Status = UpdateStatus.Failed; item.Message = ResultName(irc) + HResultText(ires);
                    log.Report($"Install failed: {item.Title} ({item.Message})");
                }
            }
            catch (Exception ex)
            {
                item.Status = UpdateStatus.Failed; item.Message = Short(ex);
                log.Report($"Error on {item.Title}: {Short(ex)}");
            }
        }
        return rebootRequired;
    }

    public Task<List<HistoryEntry>> GetHistoryAsync(int max = 60) => Task.Run(() =>
    {
        var list = new List<HistoryEntry>();
        try
        {
            dynamic session = CreateCom("Microsoft.Update.Session");
            dynamic searcher = session.CreateUpdateSearcher();
            int total = (int)searcher.GetTotalHistoryCount();
            int take = Math.Min(total, max);
            if (take <= 0) return list;
            dynamic hist = searcher.QueryHistory(0, take);
            int n = (int)hist.Count;
            for (int i = 0; i < n; i++)
            {
                dynamic e = hist.Item(i);
                try
                {
                    int rc = (int)e.ResultCode;
                    int op = SafeGet(() => (int)e.Operation, 1);
                    list.Add(new HistoryEntry
                    {
                        Title = (string)e.Title,
                        Date = SafeGet(() => (DateTime)e.Date, DateTime.MinValue),
                        Succeeded = rc == 2 || rc == 3,
                        Operation = op == 2 ? "Uninstall" : "Install"
                    });
                }
                catch { }
            }
        }
        catch { }
        return list;
    });

    private static string ResultName(int rc) => rc switch
    {
        0 => "not started", 1 => "in progress", 2 => "succeeded", 3 => "succeeded with errors",
        4 => "failed", 5 => "aborted", _ => "unknown"
    };

    private static string HResultText(dynamic res)
    {
        try { int hr = (int)res.HResult; if (hr != 0) return $" (0x{hr:X8})"; } catch { }
        return "";
    }

    private static T? SafeGet<T>(Func<T> f, T? fallback)
    {
        try { return f(); } catch { return fallback; }
    }

    internal static string Short(Exception ex)
    {
        if (ex is COMException c) return $"COM 0x{c.HResult:X8}";
        var m = ex.Message;
        return m.Length > 120 ? m[..120] + "…" : m;
    }
}
