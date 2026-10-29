using System.Windows;
using System.Windows.Threading;
using Golem.Tablet.Core.Adb;
using Golem.Tablet.Core.At;
using Golem.Tablet.Core.Configuration;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Core.Drivers;
using Golem.Tablet.Core.Fastboot;
using Golem.Tablet.Core.Logging;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;
using Golem.Tablet.ViewModels;

namespace Golem.Tablet;

public partial class App : Application
{
    private AppServices? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        IUiDispatcher dispatcher = new WpfUiDispatcher();
        var log = new LogService(dispatcher);
        var settingsStore = new SettingsStore();
        AppSettings settings = settingsStore.Load(out string? settingsError);
        if (settingsError is not null)
        {
            log.Warn("Settings", settingsError + " Using defaults.");
        }

        var enumerator = new WindowsDeviceEnumerator();
        var runner = new ProcessRunner();
        _services = new AppServices(
            settingsStore,
            settings,
            log,
            new DeviceService(enumerator),
            new AdbService(runner),
            new FastbootService(runner),
            new AtCommandService(),
            new DriverInspector(enumerator));

        var dialogs = new DialogService();
        var viewModel = new MainViewModel(_services, dialogs);
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();

        _ = viewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Log.Info("App", "GOLEM TABLET shutting down.");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _services?.Log.Error("App", "Unhandled error: " + e.Exception.Message);
        MessageBox.Show(
            "An unexpected error occurred:" + Environment.NewLine + e.Exception.Message,
            "GOLEM TABLET",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
