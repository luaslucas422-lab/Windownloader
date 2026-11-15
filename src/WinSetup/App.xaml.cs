using System.Windows;

namespace WinSetup;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool uninstall = e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));
        var win = new SetupWindow(uninstall);
        MainWindow = win;
        win.Show();
    }
}
