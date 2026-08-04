    using System;
    using Avalonia;
    using Attendly.Desktop.Hosting;


    namespace Attendly.Desktop;

    sealed class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Attendly.App.RootContentFactory = repository =>
                new DesktopShellView { DataContext = new DesktopShellViewModel(repository) };

        Attendly.App.CoreServicesReady += repository =>
            {
                _ = AttendlyApiHost.StartAsync(repository);
            };

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }