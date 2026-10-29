using Golem.Tablet.Core.Adb;
using Golem.Tablet.Core.At;
using Golem.Tablet.Core.Configuration;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Core.Drivers;
using Golem.Tablet.Core.Fastboot;
using Golem.Tablet.Core.Logging;

namespace Golem.Tablet.Services;

/// <summary>Composition container for the running application. Wired once at startup.</summary>
public sealed class AppServices
{
    public AppServices(
        SettingsStore settingsStore,
        AppSettings settings,
        LogService log,
        DeviceService devices,
        AdbService adb,
        FastbootService fastboot,
        AtCommandService at,
        DriverInspector drivers)
    {
        SettingsStore = settingsStore;
        Settings = settings;
        Log = log;
        Devices = devices;
        Adb = adb;
        Fastboot = fastboot;
        At = at;
        Drivers = drivers;
    }

    public SettingsStore SettingsStore { get; }

    public AppSettings Settings { get; private set; }

    public LogService Log { get; }

    public DeviceService Devices { get; }

    public AdbService Adb { get; }

    public FastbootService Fastboot { get; }

    public AtCommandService At { get; }

    public DriverInspector Drivers { get; }

    public event EventHandler? SettingsChanged;

    public void ApplySettings(AppSettings settings)
    {
        SettingsStore.Save(settings);
        Settings = settings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public TimeSpan CommandTimeout =>
        TimeSpan.FromSeconds(Math.Clamp(Settings.CommandTimeoutSeconds, AppSettings.MinTimeoutSeconds,
            AppSettings.MaxTimeoutSeconds));
}
