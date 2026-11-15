using Microsoft.Win32;

namespace WinDownloader.Services;

public sealed record OsInfo(string Name, string DisplayVersion, string Build, string Edition, bool IsWindows11)
{
    public string Summary => $"{Name} {DisplayVersion} · Build {Build} · {Edition}";
}

public static class SystemInfoService
{
    /// <summary>Detects the installed Windows version from the registry (ProductName lies on Windows 11, so the build number decides).</summary>
    public static OsInfo Detect()
    {
        string name = "Windows", display = "", build = "", edition = "";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                name = key.GetValue("ProductName")?.ToString() ?? name;
                display = key.GetValue("DisplayVersion")?.ToString() ?? key.GetValue("ReleaseId")?.ToString() ?? "";
                build = key.GetValue("CurrentBuild")?.ToString() ?? key.GetValue("CurrentBuildNumber")?.ToString() ?? "";
                edition = key.GetValue("EditionID")?.ToString() ?? "";
                var ubr = key.GetValue("UBR");
                if (ubr != null && !string.IsNullOrEmpty(build)) build = $"{build}.{ubr}";
            }
        }
        catch { }

        int.TryParse(build.Split('.')[0], out int buildNumber);
        bool isWin11 = buildNumber >= 22000;
        if (isWin11) name = name.Replace("Windows 10", "Windows 11");
        if (!name.Contains("Windows 1")) name = isWin11 ? "Windows 11" : "Windows 10";

        return new OsInfo(name, display, build, edition, isWin11);
    }

    /// <summary>What the user told us (or "Auto" → detected).</summary>
    public static string ResolveVersionLabel(string setting, OsInfo detected) =>
        setting is "Windows 10" or "Windows 11" ? setting : (detected.IsWindows11 ? "Windows 11" : "Windows 10");

    public static bool IsRebootPending()
    {
        try
        {
            dynamic si = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.SystemInfo", true)!)!;
            if ((bool)si.RebootRequired) return true;
        }
        catch { }
        try
        {
            using var k1 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (k1 != null) return true;
            using var k2 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            if (k2 != null) return true;
        }
        catch { }
        return false;
    }
}
