using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinDownloader.Models;

/// <summary>User settings. Shared with Win Setup, which writes the initial file during installation.</summary>
public sealed class AppSettings
{
    /// <summary>"Auto", "Windows 10" or "Windows 11".</summary>
    public string WindowsVersion { get; set; } = "Auto";
    public bool IncludeDrivers { get; set; } = true;
    public bool IncludeOptional { get; set; } = true;
    public bool IncludeApps { get; set; } = true;
    public bool UseMicrosoftUpdate { get; set; } = true;
    public bool ScanOnLaunch { get; set; } = true;
    public bool AskBeforeRestart { get; set; } = true;
    public DateTime? LastScan { get; set; }
    public int LastScore { get; set; } = -1;

    [JsonIgnore]
    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinDownloader");

    [JsonIgnore]
    public static string FilePath => Path.Combine(Directory, "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch { /* corrupt file → defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch { /* best effort */ }
    }
}
