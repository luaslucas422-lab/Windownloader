using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace WinDownloader.Views;

/// <summary>One list page used for Windows updates, drivers and apps.</summary>
public partial class UpdateListView : UserControl
{
    public static readonly DependencyProperty HeadingProperty =
        DependencyProperty.Register(nameof(Heading), typeof(string), typeof(UpdateListView), new PropertyMetadata(""));
    public static readonly DependencyProperty BlurbProperty =
        DependencyProperty.Register(nameof(Blurb), typeof(string), typeof(UpdateListView), new PropertyMetadata(""));
    public static readonly DependencyProperty EmptyTextProperty =
        DependencyProperty.Register(nameof(EmptyText), typeof(string), typeof(UpdateListView), new PropertyMetadata(""));
    public static readonly DependencyProperty GroupProperty =
        DependencyProperty.Register(nameof(Group), typeof(string), typeof(UpdateListView), new PropertyMetadata(""));
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(UpdateListView), new PropertyMetadata(null));

    public string Heading { get => (string)GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string Blurb { get => (string)GetValue(BlurbProperty); set => SetValue(BlurbProperty, value); }
    public string EmptyText { get => (string)GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
    public string Group { get => (string)GetValue(GroupProperty); set => SetValue(GroupProperty, value); }
    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    public UpdateListView() => InitializeComponent();
}
