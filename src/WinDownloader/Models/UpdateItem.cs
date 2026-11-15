using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinDownloader.Models;

public enum UpdateKind { Windows, Driver, App }

public enum UpdateSeverity { Critical, Important, Optional }

public enum UpdateStatus { Pending, Downloading, Installing, Installed, Failed, Skipped }

/// <summary>One thing that can be updated: a Windows update, a driver, or an app.</summary>
public sealed class UpdateItem : INotifyPropertyChanged
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public UpdateKind Kind { get; init; }
    public UpdateSeverity Severity { get; init; } = UpdateSeverity.Optional;
    public long SizeBytes { get; init; }
    public string Kb { get; init; } = "";
    public string Category { get; init; } = "";
    public string CurrentVersion { get; init; } = "";
    public string AvailableVersion { get; init; } = "";
    public string HardwareId { get; init; } = "";
    public bool RebootRequired { get; set; }

    /// <summary>The underlying COM IUpdate (Windows / driver) or the winget package id (apps).</summary>
    public object? Native { get; init; }

    private bool _isSelected = true;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private UpdateStatus _status = UpdateStatus.Pending;
    public UpdateStatus Status
    {
        get => _status;
        set { if (Set(ref _status, value)) { OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsBusy)); } }
    }

    private int _progress;
    public int Progress { get => _progress; set => Set(ref _progress, value); }

    private string _message = "";
    public string Message { get => _message; set { if (Set(ref _message, value)) OnPropertyChanged(nameof(StatusText)); } }

    public bool IsBusy => Status is UpdateStatus.Downloading or UpdateStatus.Installing;

    public string StatusText => Status switch
    {
        UpdateStatus.Pending => "Ready to install",
        UpdateStatus.Downloading => "Downloading…",
        UpdateStatus.Installing => "Installing…",
        UpdateStatus.Installed => RebootRequired ? "Installed · restart required" : "Installed",
        UpdateStatus.Failed => string.IsNullOrWhiteSpace(Message) ? "Failed" : "Failed · " + Message,
        UpdateStatus.Skipped => "Skipped",
        _ => ""
    };

    public string SeverityText => Severity switch
    {
        UpdateSeverity.Critical => "CRITICAL",
        UpdateSeverity.Important => "IMPORTANT",
        _ => "OPTIONAL"
    };

    public string SizeText => SizeBytes <= 0 ? "" : FormatBytes(SizeBytes);

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Kb)) parts.Add("KB" + Kb);
            if (!string.IsNullOrEmpty(Category)) parts.Add(Category);
            if (!string.IsNullOrEmpty(CurrentVersion) && !string.IsNullOrEmpty(AvailableVersion))
                parts.Add($"{CurrentVersion} → {AvailableVersion}");
            if (SizeBytes > 0) parts.Add(SizeText);
            return string.Join("  ·  ", parts);
        }
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {units[i]}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(n); return true;
    }
}
