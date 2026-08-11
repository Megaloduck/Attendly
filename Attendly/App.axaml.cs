using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Attendly.Data;
using Attendly.Models;
using Attendly.Services;
using Attendly.ViewModels;
using Attendly.Views;

namespace Attendly;

public partial class App : Application
{
    public static AttendanceRepository? Repository { get; private set; }

    public static event Action<AttendanceRepository>? CoreServicesReady;

    public static Func<AttendanceRepository, IThemeService, Control>? RootContentFactory { get; set; }

    /// <summary>Lets a platform head supply a real IQrScanner before core services are built.
    /// Set by Attendly.Android's MainActivity in OnCreate(), before base.OnCreate() reaches
    /// OnFrameworkInitializationCompleted() below. Unset platforms (Desktop, iOS until it gets
    /// one) fall back to NullQrScanner via AddAttendlyCore()'s TryAddSingleton.</summary>
    public static Func<IQrScanner>? QrScannerFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();

        if (QrScannerFactory is { } qrScannerFactory)
            serviceCollection.AddSingleton(qrScannerFactory());

        var services = serviceCollection
            .AddAttendlyCore()
            .BuildServiceProvider();

        var repository = services.GetRequiredService<AttendanceRepository>();
        var themeService = services.GetRequiredService<IThemeService>();
        var qrScanner = services.GetRequiredService<IQrScanner>();

        // Run on a thread-pool thread so the internal awaits don't try to resume on this
        // (blocked) UI thread's SynchronizationContext - doing that directly deadlocks on startup.
        Task.Run(async () =>
        {
            await repository.InitializeAsync();
            await themeService.InitializeAsync();
        }).GetAwaiter().GetResult();

        ApplyTheme(themeService.CurrentMode);
        themeService.ModeChanged += ApplyTheme;

        Repository = repository;
        CoreServicesReady?.Invoke(repository);

        Control rootContent = RootContentFactory?.Invoke(repository, themeService)
            ?? new MainView
            {
                DataContext = new MainViewModel(
                    repository,
                    services.GetRequiredService<ILocalSyncService>(),
                    themeService,
                    qrScanner)
            };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow { Content = rootContent };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = rootContent;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyTheme(ThemeMode mode)
    {
        if (Current is null) return;
        Current.RequestedThemeVariant = mode == ThemeMode.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}