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
}