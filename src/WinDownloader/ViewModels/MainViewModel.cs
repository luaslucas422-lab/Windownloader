using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using WinDownloader.Models;
using WinDownloader.Services;

namespace WinDownloader.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly WindowsUpdateService _wu = new();
    private readonly DeviceService _devices = new();
    private readonly WingetService _winget = new();
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        Settings = AppSettings.Load();
        Os = SystemInfoService.Detect();
        WindowsVersionLabel = SystemInfoService.ResolveVersionLabel(Settings.WindowsVersion, Os);
        Score = Settings.LastScore;
        LastScanText = Settings.LastScan.HasValue ? "Last scan " + Settings.LastScan.Value.ToString("ddd d MMM, HH:mm") : "Never scanned";
        StatusText = Score < 0 ? "Run a scan to see how up to date this PC is." : ScoreHeadline(Score);

        ScanCommand = new RelayCommand(async () => await ScanAsync(), () => !IsBusy);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        InstallAllCommand = new RelayCommand(async () => await InstallAsync(AllPending()), () => !IsBusy && AllPending().Any());
        InstallSelectedCommand = new RelayCommand(async p => await InstallAsync(Selected(p as string)), p => !IsBusy && Selected(p as string).Any());
        SelectAllCommand = new RelayCommand(p => ToggleAll(p as string, true));
        SelectNoneCommand = new RelayCommand(p => ToggleAll(p as string, false));
        NavigateCommand = new RelayCommand(p => Page = p as string ?? "Dashboard");
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        RescanHardwareCommand = new RelayCommand(async () => await RescanHardwareAsync(), () => !IsBusy);
        RestartCommand = new RelayCommand(RestartNow);
        OpenWindowsUpdateCommand = new RelayCommand(() => Open("ms-settings:windowsupdate"));
        OpenDeviceManagerCommand = new RelayCommand(() => Open("devmgmt.msc"));
        OpenMicrosoftStoreCommand = new RelayCommand(() => Open("ms-windows-store://downloadsandupdates"));
        OpenFolderCommand = new RelayCommand(() => Open(AppSettings.Directory));
        ToggleUsbFilterCommand = new RelayCommand(() => UsbOnly = !UsbOnly);
    }

    // ───────── State ─────────
    public AppSettings Settings { get; }
    public OsInfo Os { get; }

    public ObservableCollection<UpdateItem> WindowsUpdates { get; } = new();
    public ObservableCollection<UpdateItem> DriverUpdates { get; } = new();
    public ObservableCollection<UpdateItem> AppUpdates { get; } = new();
    public ObservableCollection<DeviceInfo> Devices { get; } = new();
    public ObservableCollection<HistoryEntry> History { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    public IEnumerable<DeviceInfo> VisibleDevices => UsbOnly ? Devices.Where(d => d.IsUsb) : Devices;

    private string _page = "Dashboard";
    public string Page { get => _page; set => Set(ref _page, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set { if (Set(ref _isBusy, value)) { OnPropertyChanged(nameof(IsIdle)); RelayCommand.Refresh(); } } }
    public bool IsIdle => !IsBusy;

    private string _statusText = "";
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    private string _busyText = "";
    public string BusyText { get => _busyText; set => Set(ref _busyText, value); }

    private string _lastScanText = "";
    public string LastScanText { get => _lastScanText; set => Set(ref _lastScanText, value); }

    private int _score = -1;
    public int Score
    {
        get => _score;
        set { if (Set(ref _score, value)) { OnPropertyChanged(nameof(ScoreText)); OnPropertyChanged(nameof(ScoreHeadlineText)); OnPropertyChanged(nameof(ScoreForGauge)); OnPropertyChanged(nameof(HasScore)); } }
    }
    public bool HasScore => Score >= 0;
    public string ScoreText => Score < 0 ? "–" : Score.ToString();
    public double ScoreForGauge => Score < 0 ? 0 : Score;
    public string ScoreHeadlineText => Score < 0 ? "Not scanned yet" : ScoreHeadline(Score);

    private bool _rebootRequired;
    public bool RebootRequired { get => _rebootRequired; set => Set(ref _rebootRequired, value); }

    private bool _usbOnly;
    public bool UsbOnly { get => _usbOnly; set { if (Set(ref _usbOnly, value)) OnPropertyChanged(nameof(VisibleDevices)); } }

    private bool _wingetAvailable = true;
    public bool WingetAvailable { get => _wingetAvailable; set => Set(ref _wingetAvailable, value); }

    private string _windowsVersionLabel = "";
    public string WindowsVersionLabel { get => _windowsVersionLabel; set => Set(ref _windowsVersionLabel, value); }

    private string _settingsSavedText = "";
    public string SettingsSavedText { get => _settingsSavedText; set => Set(ref _settingsSavedText, value); }

    public int WindowsPending => WindowsUpdates.Count(u => u.Status == UpdateStatus.Pending || u.Status == UpdateStatus.Failed);
    public int DriversPending => DriverUpdates.Count(u => u.Status == UpdateStatus.Pending || u.Status == UpdateStatus.Failed);
    public int AppsPending => AppUpdates.Count(u => u.Status == UpdateStatus.Pending || u.Status == UpdateStatus.Failed);
    public int DeviceProblems => Devices.Count(d => d.HasProblem);
    public int TotalPending => WindowsPending + DriversPending + AppsPending;
    public int InstalledThisSession => WindowsUpdates.Concat(DriverUpdates).Concat(AppUpdates).Count(u => u.Status == UpdateStatus.Installed);

    public string WindowsCardText => WindowsPending == 0 ? "Up to date" : $"{WindowsPending} pending";
    public string DriversCardText => DriversPending == 0 ? "Up to date" : $"{DriversPending} available";
    public string AppsCardText => !WingetAvailable ? "winget not found" : AppsPending == 0 ? "Up to date" : $"{AppsPending} available";
    public string DevicesCardText => Devices.Count == 0 ? "Not scanned" : DeviceProblems == 0 ? $"{Devices.Count} devices OK" : $"{DeviceProblems} with problems";

    public IReadOnlyList<string> WindowsVersionChoices { get; } = new[] { "Auto", "Windows 10", "Windows 11" };

    // ───────── Commands ─────────
    public RelayCommand ScanCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand InstallAllCommand { get; }
    public RelayCommand InstallSelectedCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand RescanHardwareCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand OpenWindowsUpdateCommand { get; }
    public RelayCommand OpenDeviceManagerCommand { get; }
    public RelayCommand OpenMicrosoftStoreCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand ToggleUsbFilterCommand { get; }

    // ───────── Scan ─────────
    public async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(AddLog);
        BusyText = "Scanning…";
        StatusText = "Checking Windows Update, drivers, devices and apps…";
        AddLog("Scan started.");

        try
        {
            WindowsUpdates.Clear(); DriverUpdates.Clear(); AppUpdates.Clear(); Devices.Clear();
            RaiseCounts();

            // 1. Devices (fast, local)
            BusyText = "Reading devices…";
            var devices = await _devices.ScanAsync(log);
            foreach (var d in devices) Devices.Add(d);
            AddLog($"Found {devices.Count} devices ({devices.Count(d => d.IsUsb)} USB).");
            RaiseCounts();

            // 2. Windows Update + drivers (online)
            BusyText = "Contacting Windows Update…";
            List<UpdateItem> wuItems = new();
            try
            {
                wuItems = await _wu.SearchAsync(Settings.IncludeDrivers, Settings.IncludeOptional, Settings.UseMicrosoftUpdate, log, _cts.Token);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { AddLog("Windows Update search failed: " + WindowsUpdateService.Short(ex)); }

            foreach (var item in wuItems.OrderBy(x => x.Severity).ThenBy(x => x.Title))
            {
                if (item.Kind == UpdateKind.Driver) DriverUpdates.Add(item); else WindowsUpdates.Add(item);
            }
            DeviceService.MatchUpdates(Devices, DriverUpdates);
            RaiseCounts();

            // 3. Apps via winget
            if (Settings.IncludeApps)
            {
                BusyText = "Checking apps (winget)…";
                WingetAvailable = await Task.Run(WingetService.IsAvailable);
                if (WingetAvailable)
                {
                    var apps = await _winget.SearchAsync(log);
                    foreach (var app in apps.OrderBy(x => x.Title)) AppUpdates.Add(app);
                }
                else AddLog("winget is not installed; app updates skipped (install 'App Installer' from the Microsoft Store).");
            }
            RaiseCounts();

            // 4. History + reboot state
            BusyText = "Reading update history…";
            var history = await _wu.GetHistoryAsync();
            History.Clear();
            foreach (var h in history) History.Add(h);
            RebootRequired = await Task.Run(SystemInfoService.IsRebootPending);

            ComputeScore();
            Settings.LastScan = DateTime.Now;
            Settings.LastScore = Score;
            Settings.Save();
            LastScanText = "Last scan " + Settings.LastScan.Value.ToString("ddd d MMM, HH:mm");
            StatusText = TotalPending == 0
                ? "Everything we can check is up to date."
                : $"{TotalPending} update(s) available — {WindowsPending} Windows, {DriversPending} drivers, {AppsPending} apps.";
            AddLog($"Scan finished. Score {Score}/100.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan cancelled."; AddLog("Scan cancelled.");
        }
        catch (Exception ex)
        {
            StatusText = "Scan failed: " + ex.Message; AddLog("Scan failed: " + ex.Message);
        }
        finally
        {
            IsBusy = false; BusyText = "";
            RaiseCounts();
        }
    }

    // ───────── Install ─────────
    private IEnumerable<UpdateItem> AllPending() =>
        WindowsUpdates.Concat(DriverUpdates).Concat(AppUpdates).Where(u => u.Status is UpdateStatus.Pending or UpdateStatus.Failed);

    private IEnumerable<UpdateItem> Selected(string? group)
    {
        IEnumerable<UpdateItem> src = group switch
        {
            "Windows" => WindowsUpdates,
            "Drivers" => DriverUpdates,
            "Apps" => AppUpdates,
            _ => WindowsUpdates.Concat(DriverUpdates).Concat(AppUpdates)
        };
        return src.Where(u => u.IsSelected && u.Status is UpdateStatus.Pending or UpdateStatus.Failed);
    }

    private void ToggleAll(string? group, bool on)
    {
        IEnumerable<UpdateItem> src = group switch
        {
            "Windows" => WindowsUpdates,
            "Drivers" => DriverUpdates,
            "Apps" => AppUpdates,
            _ => WindowsUpdates.Concat(DriverUpdates).Concat(AppUpdates)
        };
        foreach (var item in src) item.IsSelected = on;
        RelayCommand.Refresh();
    }

    public async Task InstallAsync(IEnumerable<UpdateItem> items)
    {
        var list = items.ToList();
        if (IsBusy || list.Count == 0) return;
        IsBusy = true;
        _cts = new CancellationTokenSource();
        var log = new Progress<string>(AddLog);
        BusyText = $"Installing {list.Count} update(s)…";
        StatusText = BusyText;
        bool reboot = false;

        try
        {
            var wu = list.Where(u => u.Kind != UpdateKind.App).ToList();
            var apps = list.Where(u => u.Kind == UpdateKind.App).ToList();

            if (wu.Count > 0)
                reboot |= await _wu.InstallAsync(wu, log, _cts.Token);

            if (apps.Count > 0)
                await _winget.InstallAsync(apps, log, _cts.Token);

            RebootRequired |= reboot || await Task.Run(SystemInfoService.IsRebootPending);
            DeviceService.MatchUpdates(Devices, DriverUpdates.Where(d => d.Status != UpdateStatus.Installed));
            ComputeScore();
            Settings.LastScore = Score; Settings.Save();

            int ok = list.Count(u => u.Status == UpdateStatus.Installed);
            int failed = list.Count(u => u.Status == UpdateStatus.Failed);
            StatusText = failed == 0 ? $"Installed {ok} update(s)." : $"Installed {ok}, {failed} failed.";
            if (RebootRequired) StatusText += " Restart to finish.";
            AddLog(StatusText);

            if (RebootRequired && Settings.AskBeforeRestart)
            {
                var r = MessageBox.Show("Some updates need a restart to finish installing.\n\nRestart now?", "WinDownloader",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes) RestartNow();
            }
        }
        catch (OperationCanceledException) { StatusText = "Install cancelled."; }
        catch (Exception ex) { StatusText = "Install failed: " + ex.Message; AddLog(StatusText); }
        finally { IsBusy = false; BusyText = ""; RaiseCounts(); }
    }

    private async Task RescanHardwareAsync()
    {
        IsBusy = true; BusyText = "Rescanning hardware…";
        try
        {
            AddLog(await DeviceService.RescanHardwareAsync());
            var devices = await _devices.ScanAsync(new Progress<string>(AddLog));
            Devices.Clear();
            foreach (var d in devices) Devices.Add(d);
            DeviceService.MatchUpdates(Devices, DriverUpdates);
            OnPropertyChanged(nameof(VisibleDevices));
        }
        finally { IsBusy = false; BusyText = ""; RaiseCounts(); }
    }

    // ───────── Score ─────────
    /// <summary>
    /// 100 = everything we can see is installed. Each missing item costs points according to how much it matters:
    /// critical Windows update −15, important −8, optional −3, driver −6, app −2, pending restart −5, device with a problem −4 (max −12).
    /// </summary>
    private void ComputeScore()
    {
        int score = 100;
        foreach (var item in WindowsUpdates.Where(x => x.Status != UpdateStatus.Installed))
            score -= item.Severity switch { UpdateSeverity.Critical => 15, UpdateSeverity.Important => 8, _ => 3 };
        score -= 6 * DriverUpdates.Count(u => u.Status != UpdateStatus.Installed);
        score -= 2 * AppUpdates.Count(u => u.Status != UpdateStatus.Installed);
        if (RebootRequired) score -= 5;
        score -= Math.Min(12, 4 * DeviceProblems);
        Score = Math.Clamp(score, 0, 100);
    }

    public static string ScoreHeadline(int score) => score switch
    {
        >= 100 => "Fully updated",
        >= 85 => "Almost there",
        >= 60 => "Needs attention",
        _ => "Out of date"
    };

    // ───────── Settings ─────────
    private void SaveSettings()
    {
        Settings.Save();
        WindowsVersionLabel = SystemInfoService.ResolveVersionLabel(Settings.WindowsVersion, Os);
        SettingsSavedText = "Saved " + DateTime.Now.ToString("HH:mm:ss");
        AddLog("Settings saved.");
    }

    // ───────── Helpers ─────────
    private void RaiseCounts()
    {
        foreach (var n in new[] { nameof(WindowsPending), nameof(DriversPending), nameof(AppsPending), nameof(DeviceProblems), nameof(TotalPending),
                                  nameof(InstalledThisSession), nameof(WindowsCardText), nameof(DriversCardText), nameof(AppsCardText), nameof(DevicesCardText), nameof(VisibleDevices) })
            OnPropertyChanged(n);
        RelayCommand.Refresh();
    }

    private void AddLog(string line)
    {
        void Add()
        {
            Log.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");
            while (Log.Count > 300) Log.RemoveAt(Log.Count - 1);
        }
        if (Application.Current?.Dispatcher.CheckAccess() ?? true) Add();
        else Application.Current!.Dispatcher.Invoke(Add);
    }

    private static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); } catch { }
    }

    private static void RestartNow()
    {
        try { Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 10 /c \"WinDownloader: restarting to finish installing updates.\"") { UseShellExecute = false, CreateNoWindow = true }); } catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(n); return true;
    }
}
