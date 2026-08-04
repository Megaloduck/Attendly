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

    /// <summary>Fires once core services are ready. Desktop uses this to start its LAN API host.</summary>
    public static event Action<AttendanceRepository>? CoreServicesReady;

    /// <summary>
    /// Lets a platform head override the root Window/View content. Unset
    /// (Android/iOS) falls back to the default teacher flow (MainView).
    /// Attendly.Desktop sets this to its own admin shell.
    /// </summary>
    public static Func<AttendanceRepository, IThemeService, Control>? RootContentFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection()
            .AddAttendlyCore()
            .BuildServiceProvider();

        var repository = services.GetRequiredService<AttendanceRepository>();
        var themeService = services.GetRequiredService<IThemeService>();

        // Run on a thread-pool thread so the internal awaits don't try to resume on this
        // (blocked) UI thread's SynchronizationContext - doing that directly deadlocks on startup.
        Task.Run(async () =>
        {
            await repository.InitializeAsync();
            await themeService.InitializeAsync();
        }).GetAwaiter().GetResult();

        // Applied explicitly here (we're back on the UI thread now that GetResult() returned) rather
        // than relying on ModeChanged, since that event fired from the background thread above.
        ApplyTheme(themeService.CurrentMode);
        themeService.ModeChanged += ApplyTheme; // later toggles come from UI-thread button clicks, so this is safe

        Repository = repository;
        CoreServicesReady?.Invoke(repository);

        Control rootContent = RootContentFactory?.Invoke(repository, themeService)
            ?? new MainView
            {
                DataContext = new MainViewModel(
                    repository,
                    services.GetRequiredService<ILocalSyncService>(),
                    themeService)
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