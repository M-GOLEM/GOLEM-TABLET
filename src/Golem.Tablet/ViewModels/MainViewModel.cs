using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Golem.Tablet.Core.Logging;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private NavigationItem _selectedNavigation;
    private bool _isLogVisible = true;
    private bool _isLogPinned = true;

    public MainViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        Devices = new DevicesViewModel(services);
        Adb = new AdbViewModel(services, dialogs);
        Fastboot = new FastbootViewModel(services, dialogs);
        Samsung = new SamsungViewModel(services, dialogs);
        Qualcomm = new QualcommViewModel(services, dialogs);
        MediaTek = new MediaTekViewModel(services);
        Spreadtrum = new SpreadtrumViewModel(services);
        Drivers = new DriversViewModel(services);
        Logs = new LogsViewModel(services, dialogs);
        Settings = new SettingsViewModel(services, dialogs);
        About = new AboutViewModel(dialogs);

        Navigation = new ObservableCollection<NavigationItem>
        {
            new("01", "Devices", "USB inventory and connection modes", Devices),
            new("02", "ADB", "Android Debug Bridge operations", Adb),
            new("03", "Fastboot", "Bootloader variables and reboot actions", Fastboot),
            new("04", "Samsung", "Odin firmware package validation", Samsung),
            new("05", "Qualcomm", "EDL firmware package validation", Qualcomm),
            new("06", "MediaTek", "AT command console and diagnostics", MediaTek),
            new("07", "Spreadtrum", "Device detection and diagnostics", Spreadtrum),
            new("08", "Drivers", "Driver and device problem report", Drivers),
            new("09", "Logs", "Session log, clear and export", Logs),
            new("10", "Settings", "Tool paths and preferences", Settings),
            new("11", "About", "Product and developer information", About)
        };

        _selectedNavigation = Navigation[0];

        ToggleLogCommand = new RelayCommand(() => IsLogVisible = !IsLogVisible);
        ClearLogCommand = new RelayCommand(ClearLog);
        ExportLogCommand = new RelayCommand(ExportLog);
        RefreshDevicesCommand = new AsyncRelayCommand(RefreshAllAsync,
            onError: ex => _services.Log.Error("App", "Refresh failed: " + ex.Message));

        Devices.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HeaderStatus));
            OnPropertyChanged(nameof(HasDevices));
        };

        _services.Log.Info("App", "GOLEM TABLET started.");
        _services.Log.Info("App", "Local-only build: no update service, no telemetry, no network access.");
    }

    public AppServices Services => _services;

    public LogService Log => _services.Log;

    public DevicesViewModel Devices { get; }

    public AdbViewModel Adb { get; }

    public FastbootViewModel Fastboot { get; }

    public SamsungViewModel Samsung { get; }

    public QualcommViewModel Qualcomm { get; }

    public MediaTekViewModel MediaTek { get; }

    public SpreadtrumViewModel Spreadtrum { get; }

    public DriversViewModel Drivers { get; }

    public LogsViewModel Logs { get; }

    public SettingsViewModel Settings { get; }

    public AboutViewModel About { get; }

    public ObservableCollection<NavigationItem> Navigation { get; }

    public NavigationItem SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (SetProperty(ref _selectedNavigation, value))
            {
                OnPropertyChanged(nameof(CurrentViewModel));
            }
        }
    }

    public object CurrentViewModel => SelectedNavigation.ViewModel;

    public bool IsLogVisible
    {
        get => _isLogVisible;
        set => SetProperty(ref _isLogVisible, value);
    }

    public bool IsLogPinned
    {
        get => _isLogPinned;
        set => SetProperty(ref _isLogPinned, value);
    }

    public bool HasDevices => Devices.DeviceCount > 0;

    public string HeaderStatus
    {
        get
        {
            int count = Devices.DeviceCount;
            int adb = Devices.AdbCount;
            return count == 0
                ? "No USB devices detected"
                : $"{count} USB device(s) · {adb} in ADB mode";
        }
    }

    public ICommand ToggleLogCommand { get; }

    public ICommand ClearLogCommand { get; }

    public ICommand ExportLogCommand { get; }

    public ICommand RefreshDevicesCommand { get; }

    public async Task InitializeAsync()
    {
        await Devices.RefreshAsync().ConfigureAwait(true);
        await Adb.InitializeAsync().ConfigureAwait(true);
    }

    private void ClearLog()
    {
        _services.Log.Clear();
        _services.Log.Info("Log", "Log cleared by user.");
    }

    private void ExportLog()
    {
        string defaultDirectory = string.IsNullOrWhiteSpace(_services.Settings.LogExportDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : _services.Settings.LogExportDirectory;

        string fileName = $"golem-tablet-{DateTime.Now:yyyyMMdd-HHmmss}.log";
        string? path = _dialogs.PickSaveFile("Export log", "Log files (*.log)|*.log|All files (*.*)|*.*",
            Path.Combine(defaultDirectory, fileName));

        if (path is null)
        {
            return;
        }

        try
        {
            _services.Log.Export(path);
            _services.Log.Info("Log", "Log exported to " + path);
            _dialogs.ShowInfo("Export log", "Log written to:" + Environment.NewLine + path);
        }
        catch (Exception ex)
        {
            _services.Log.Error("Log", "Export failed: " + ex.Message);
            _dialogs.ShowInfo("Export log", "Export failed: " + ex.Message);
        }
    }

    private async Task RefreshAllAsync()
    {
        await Devices.RefreshAsync().ConfigureAwait(true);
        await Adb.RefreshDeviceListAsync().ConfigureAwait(true);
    }
}
