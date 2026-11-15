using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WinSetup;

public partial class SetupWindow : Window
{
    private int _step;
    private readonly bool _uninstallMode;
    private readonly Installer.OsInfo _os = Installer.DetectOs();

    public SetupWindow(bool uninstallMode)
    {
        InitializeComponent();
        _uninstallMode = uninstallMode;

        DetectedTitle.Text = $"Use what we detected: {_os.Name}";
        DetectedDetail.Text = _os.Summary;
        FolderBox.Text = Installer.DefaultInstallDir;
        PayloadWarning.Visibility = Installer.HasPayload ? Visibility.Collapsed : Visibility.Visible;

        if (_uninstallMode) StartUninstall();
        else ShowStep(0);
    }

    // ───────── Wizard navigation ─────────
    private void ShowStep(int step)
    {
        _step = step;
        PageWelcome.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageVersion.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageOptions.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageInstall.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageDone.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        var labels = new[] { Step0, Step1, Step2, Step3, Step4 };
        for (int i = 0; i < labels.Length; i++)
            labels[i].Style = (Style)FindResource(i == step ? "StepLabelActive" : "StepLabel");

        BtnBack.Visibility = step is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        BtnCancel.Visibility = step <= 2 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.IsEnabled = step != 3;
        BtnNext.Content = step switch { 0 => "Get started", 2 => "Install", 4 => "Finish", _ => "Next" };
        if (step == 0 && !Installer.HasPayload) BtnNext.IsEnabled = false;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowStep(Math.Max(0, _step - 1));

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        switch (_step)
        {
            case 0:
            case 1:
                ShowStep(_step + 1);
                break;
            case 2:
                await RunInstallAsync();
                break;
            case 4:
                if (!_uninstallMode && ChkLaunch.IsChecked == true) Installer.Launch(FolderBox.Text.Trim());
                Close();
                break;
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose the install folder", InitialDirectory = FolderBox.Text };
        if (dlg.ShowDialog(this) == true)
        {
            string chosen = dlg.FolderName;
            if (!chosen.EndsWith("WinDownloader", StringComparison.OrdinalIgnoreCase))
                chosen = Path.Combine(chosen, "WinDownloader");
            FolderBox.Text = chosen;
        }
    }

    // ───────── Install ─────────
    private async Task RunInstallAsync()
    {
        ShowStep(3);
        string dir = FolderBox.Text.Trim();
        string windowsVersion = OptWin11.IsChecked == true ? "Windows 11" : OptWin10.IsChecked == true ? "Windows 10" : "Auto";
        var options = new Installer.Options(dir, windowsVersion, ChkDesktop.IsChecked == true, ChkStart.IsChecked == true);

        var progress = new Progress<(int pct, string msg)>(p =>
        {
            InstallProgress.Value = p.pct;
            InstallStatus.Text = p.msg;
            InstallLog.Text = (p.msg + Environment.NewLine + InstallLog.Text);
        });

        try
        {
            await Task.Run(() => Installer.Install(options, progress));
            DoneTitle.Text = "WinDownloader is installed";
            DoneDetail.Text = $"Installed to {dir}\nWindows version: {(windowsVersion == "Auto" ? _os.Name + " (detected)" : windowsVersion)}\n\nYou can change the Windows version and what gets scanned in Settings inside the app.";
            ShowStep(4);
        }
        catch (Exception ex)
        {
            InstallTitle.Text = "Setup could not finish";
            InstallStatus.Text = ex.Message;
            InstallStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            BtnCancel.Visibility = Visibility.Visible;
            BtnBack.Visibility = Visibility.Visible;
        }
    }

    // ───────── Uninstall ─────────
    private async void StartUninstall()
    {
        Title = "Win Setup — Uninstall";
        Step3.Text = "4  Uninstall";
        ShowStep(3);
        InstallTitle.Text = "Removing WinDownloader…";
        var r = MessageBox.Show(this, "Remove WinDownloader from this PC?", "Win Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) { Close(); return; }

        var progress = new Progress<(int pct, string msg)>(p => { InstallProgress.Value = p.pct; InstallStatus.Text = p.msg; });
        try
        {
            string dir = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? Installer.DefaultInstallDir;
            await Task.Run(() => Installer.Uninstall(dir, progress));
            DoneTitle.Text = "WinDownloader was removed";
            DoneDetail.Text = "Your settings file in ProgramData\\WinDownloader was kept in case you reinstall.";
            ShowStep(4);
        }
        catch (Exception ex)
        {
            InstallTitle.Text = "Uninstall could not finish";
            InstallStatus.Text = ex.Message;
            BtnCancel.Visibility = Visibility.Visible;
        }
    }
}
