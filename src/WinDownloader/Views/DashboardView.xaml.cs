using System.Windows.Controls;
using System.Windows.Input;
using WinDownloader.ViewModels;

namespace WinDownloader.Views;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void GoWindows(object sender, MouseButtonEventArgs e) { if (Vm != null) Vm.Page = "Windows"; }
    private void GoDrivers(object sender, MouseButtonEventArgs e) { if (Vm != null) Vm.Page = "Drivers"; }
    private void GoDevices(object sender, MouseButtonEventArgs e) { if (Vm != null) Vm.Page = "Devices"; }
    private void GoApps(object sender, MouseButtonEventArgs e) { if (Vm != null) Vm.Page = "Apps"; }
}
