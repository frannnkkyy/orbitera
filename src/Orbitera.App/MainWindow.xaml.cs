using System.Windows;
using Orbitera.App.ViewModels;

namespace Orbitera.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var isCompact = e.NewSize.Width < 1220;
        SidebarColumn.Width = new GridLength(isCompact ? 204 : 232);
        BrandTagline.Visibility = isCompact ? Visibility.Collapsed : Visibility.Visible;
        PageContentHost.Margin = isCompact
            ? new Thickness(22, 20, 22, 28)
            : new Thickness(30, 26, 30, 34);
    }
}