using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Orbitera.App.Services;
using Orbitera.App.ViewModels;
using Orbitera.Application;
using Orbitera.Infrastructure;

namespace Orbitera.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        _serviceProvider.GetRequiredService<MainWindow>().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<IStorageScanner, FileSystemStorageScanner>();
        services.AddSingleton<ScanStorageUseCase>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddTransient<OverviewViewModel>();
        services.AddTransient<AtlasViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<DevInsightsViewModel>();
        services.AddTransient<CleanupPlannerViewModel>();
        services.AddTransient<FileIntelligenceViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
