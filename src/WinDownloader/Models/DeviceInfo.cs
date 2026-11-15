using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinDownloader.Models;

/// <summary>A hardware device and the driver currently installed for it.</summary>
public sealed class DeviceInfo : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string DeviceClass { get; init; } = "";
    public string DeviceId { get; init; } = "";
    public string DriverVersion { get; init; } = "";
    public string DriverProvider { get; init; } = "";
    public DateTime? DriverDate { get; init; }
    public bool IsUsb { get; init; }
    public bool IsSigned { get; init; } = true;
    public int ProblemCode { get; init; }
    public string[] HardwareIds { get; init; } = Array.Empty<string>();

    private UpdateItem? _availableUpdate;
    public UpdateItem? AvailableUpdate
    {
        get => _availableUpdate;
        set { _availableUpdate = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUpdate)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusKind)); }
    }

    public bool HasUpdate => AvailableUpdate != null;
    public bool HasProblem => ProblemCode != 0;

    public string DriverDateText => DriverDate.HasValue ? DriverDate.Value.ToString("yyyy-MM-dd") : "unknown";

    public double DriverAgeYears => DriverDate.HasValue ? (DateTime.Now - DriverDate.Value).TotalDays / 365.25 : 0;

    /// <summary>"ok", "update", "problem", "old" — used by the UI to colour the row.</summary>
    public string StatusKind => HasProblem ? "problem" : HasUpdate ? "update" : DriverAgeYears > 5 ? "old" : "ok";

    public string StatusText => HasProblem
        ? $"Problem (code {ProblemCode})"
        : HasUpdate ? "Update available"
        : DriverAgeYears > 5 ? "Working · driver is old"
        : "Up to date";

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(DeviceClass)) parts.Add(DeviceClass);
            if (!string.IsNullOrEmpty(Manufacturer)) parts.Add(Manufacturer);
            if (!string.IsNullOrEmpty(DriverVersion)) parts.Add("v" + DriverVersion);
            parts.Add("driver " + DriverDateText);
            if (IsUsb) parts.Add("USB");
            return string.Join("  ·  ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class HistoryEntry
{
    public string Title { get; init; } = "";
    public DateTime Date { get; init; }
    public bool Succeeded { get; init; }
    public string Operation { get; init; } = "Install";
    public string ResultText => Succeeded ? "Succeeded" : "Failed";
    public string DateText => Date.ToString("yyyy-MM-dd HH:mm");
}
