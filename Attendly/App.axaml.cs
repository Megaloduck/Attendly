using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Attendly.Data;
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
    /// Attendly.Desktop sets this to its own admin/pairing shell.
    /// </summary>
    public static Func<AttendanceRepository, Control>? RootContentFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection()
            .AddAttendlyCore()
            .BuildServiceProvider();

        var repository = services.GetRequiredService<AttendanceRepository   >();

        // One-time table creation. Blocking here is deliberate - it's just
        // CreateTableAsync calls against a local SQLite file, fast even on
        // mobile. Revisit with a splash/loading state in Phase 5 if that
        // stops being true on real devices.
        repository.InitializeAsync().GetAwaiter().GetResult();

        Repository = repository;
        CoreServicesReady?.Invoke(repository);

        Control rootContent = RootContentFactory?.Invoke(repository)
            ?? new MainView { DataContext = new MainViewModel(repository, services.GetRequiredService<ILocalSyncService>()) };

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