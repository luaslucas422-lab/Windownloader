using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Microsoft.Win32;

namespace WinSetup;

/// <summary>Everything Win Setup actually does to the machine, kept out of the UI code.</summary>
public static class Installer
{
    public const string AppName = "WinDownloader";
    public const string ExeName = "WinDownloader.exe";
    private const string PayloadName = "WinSetup.Payload.app.zip";
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinDownloader";

    public sealed record Options(string InstallDir, string WindowsVersion, bool DesktopShortcut, bool StartMenuShortcut);
    public sealed record OsInfo(string Name, string DisplayVersion, string Build, string Edition, bool IsWindows11)
    {
        public string Summary => $"{Name} {DisplayVersion} · Build {Build} · {Edition}";
    }

    public static string DefaultInstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);

    public static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName);

    public static bool HasPayload =>
        Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains(PayloadName);

    // ───────── Detection ─────────
    public static OsInfo DetectOs()
    {
        string name = "Windows", display = "", build = "", edition = "";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                name = key.GetValue("ProductName")?.ToString() ?? name;
                display = key.GetValue("DisplayVersion")?.ToString() ?? key.GetValue("ReleaseId")?.ToString() ?? "";
                build = key.GetValue("CurrentBuild")?.ToString() ?? "";
                edition = key.GetValue("EditionID")?.ToString() ?? "";
                var ubr = key.GetValue("UBR");
                if (ubr != null && build.Length > 0) build = $"{build}.{ubr}";
            }
        }
        catch { }
        int.TryParse(build.Split('.')[0], out int b);
        bool isWin11 = b >= 22000;
        if (isWin11) name = name.Replace("Windows 10", "Windows 11");
        if (!name.Contains("Windows 1")) name = isWin11 ? "Windows 11" : "Windows 10";
        return new OsInfo(name, display, build, edition, isWin11);
    }

    // ───────── Install ─────────
    public static void Install(Options o, IProgress<(int, string)> progress)
    {
        if (!HasPayload) throw new InvalidOperationException("This Win Setup has no app payload. Download WinSetup.exe from GitHub Releases.");

        progress.Report((5, "Creating folders"));
        Directory.CreateDirectory(o.InstallDir);
        Directory.CreateDirectory(SettingsDir);

        progress.Report((10, "Stopping any running copy"));
        foreach (var p in Process.GetProcessesByName("WinDownloader"))
        {
            try { p.Kill(true); p.WaitForExit(5000); } catch { }
        }

        progress.Report((20, "Unpacking WinDownloader"));
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(PayloadName)!)
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            int total = zip.Entries.Count, done = 0;
            foreach (var entry in zip.Entries)
            {
                string target = Path.GetFullPath(Path.Combine(o.InstallDir, entry.FullName));
                if (!target.StartsWith(Path.GetFullPath(o.InstallDir), StringComparison.OrdinalIgnoreCase)) continue; // zip-slip guard
                if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: true);
                done++;
                progress.Report((20 + (int)(50.0 * done / Math.Max(1, total)), $"Unpacking {entry.Name}"));
            }
        }

        progress.Report((72, "Writing settings"));
        WriteSettings(o.WindowsVersion);

        progress.Report((78, "Keeping a copy of Win Setup for uninstall"));
        string self = Environment.ProcessPath ?? "";
        string setupCopy = Path.Combine(o.InstallDir, "WinSetup.exe");
        try { if (self.Length > 0 && !self.Equals(setupCopy, StringComparison.OrdinalIgnoreCase)) File.Copy(self, setupCopy, true); } catch { }

        string exe = Path.Combine(o.InstallDir, ExeName);
        if (o.StartMenuShortcut)
        {
            progress.Report((84, "Adding Start menu shortcut"));
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
            CreateShortcut(Path.Combine(programs, AppName + ".lnk"), exe, "Keep Windows, drivers and apps up to date");
        }
        if (o.DesktopShortcut)
        {
            progress.Report((90, "Adding desktop shortcut"));
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            CreateShortcut(Path.Combine(desktop, AppName + ".lnk"), exe, "Keep Windows, drivers and apps up to date");
        }

        progress.Report((95, "Registering in Apps & features"));
        RegisterUninstall(o.InstallDir, exe, setupCopy);

        progress.Report((100, "Done"));
    }

    private static void WriteSettings(string windowsVersion)
    {
        string path = Path.Combine(SettingsDir, "settings.json");
        Dictionary<string, object?> settings = new();
        try
        {
            if (File.Exists(path))
                settings = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllText(path)) ?? new();
        }
        catch { settings = new(); }

        settings["WindowsVersion"] = windowsVersion;
        settings.TryAdd("IncludeDrivers", true);
        settings.TryAdd("IncludeOptional", true);
        settings.TryAdd("IncludeApps", true);
        settings.TryAdd("UseMicrosoftUpdate", true);
        settings.TryAdd("ScanOnLaunch", true);
        settings.TryAdd("AskBeforeRestart", true);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CreateShortcut(string lnkPath, string target, string description)
    {
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true)!)!;
            dynamic lnk = shell.CreateShortcut(lnkPath);
            lnk.TargetPath = target;
            lnk.WorkingDirectory = Path.GetDirectoryName(target);
            lnk.IconLocation = target + ",0";
            lnk.Description = description;
            lnk.Save();
        }
        catch { /* shortcuts are nice-to-have */ }
    }

    private static void RegisterUninstall(string dir, string exe, string setupCopy)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(UninstallKey);
            if (key == null) return;
            key.SetValue("DisplayName", "WinDownloader");
            key.SetValue("DisplayVersion", "1.0.0");
            key.SetValue("Publisher", "DarkerDawnFR");
            key.SetValue("DisplayIcon", exe);
            key.SetValue("InstallLocation", dir);
            key.SetValue("UninstallString", $"\"{setupCopy}\" --uninstall");
            key.SetValue("URLInfoAbout", "https://github.com/luaslucas422-lab/Windownloader");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            long size = 0;
            try { size = new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024; } catch { }
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, size), RegistryValueKind.DWord);
        }
        catch { }
    }

    // ───────── Uninstall ─────────
    public static void Uninstall(string dir, IProgress<(int, string)> progress)
    {
        progress.Report((10, "Closing WinDownloader"));
        foreach (var p in Process.GetProcessesByName("WinDownloader")) { try { p.Kill(true); p.WaitForExit(5000); } catch { } }

        progress.Report((30, "Removing shortcuts"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName + ".lnk"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), AppName + ".lnk"));
        TryDelete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk"));

        progress.Report((50, "Removing registry entries"));
        try { Registry.LocalMachine.DeleteSubKeyTree(UninstallKey, false); } catch { }

        progress.Report((70, "Removing files"));
        // Everything except the running WinSetup.exe, which a detached cmd deletes after we exit.
        string self = Environment.ProcessPath ?? "";
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            if (f.Equals(self, StringComparison.OrdinalIgnoreCase)) continue;
            TryDelete(f);
        }
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 4 >nul & rmdir /s /q \"{dir}\"")
            { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            Process.Start(psi);
        }
        catch { }
        progress.Report((100, "Done"));
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public static void Launch(string dir)
    {
        try { Process.Start(new ProcessStartInfo(Path.Combine(dir, ExeName)) { UseShellExecute = true, WorkingDirectory = dir }); } catch { }
    }
}
