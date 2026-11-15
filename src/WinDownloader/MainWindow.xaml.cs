using System.Windows;
using WinDownloader.ViewModels;

namespace WinDownloader;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) =>
        {
            if (_vm.Settings.ScanOnLaunch) await _vm.ScanAsync();
        };
    }
}
