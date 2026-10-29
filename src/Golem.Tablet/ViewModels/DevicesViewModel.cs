using System.Collections.ObjectModel;
using System.Windows.Input;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class DevicesViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly RelayCommand _cancelCommand;
    private CancellationTokenSource? _cancellation;
    private DeviceInfo? _selectedDevice;
    private bool _isBusy;
    private string _emptyMessage = "Press Refresh to scan for USB devices.";

    public DevicesViewModel(AppServices services)
    {
        _services = services;
        RefreshCommand = new AsyncRelayCommand(
            RefreshAsync,
            onError: ex => _services.Log.Error("Devices", "Scan failed: " + ex.Message));
        _cancelCommand = new RelayCommand(CancelScan, () => IsBusy);
    }

    public ObservableCollection<DeviceInfo> Devices { get; } = new();

    public ICommand RefreshCommand { get; }

    public ICommand CancelCommand => _cancelCommand;

    public DeviceInfo? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value))
            {
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => SelectedDevice is not null;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _cancelCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public int DeviceCount => Devices.Count;

    public int AdbCount => Devices.Count(device => device.Mode == ConnectionMode.Adb);

    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    public string StatusText => IsBusy
        ? "Scanning…"
        : $"{DeviceCount} device(s) · {AdbCount} in ADB mode";

    public async Task RefreshAsync()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        CancellationToken token = _cancellation.Token;

        IsBusy = true;
        EmptyMessage = "Scanning for USB devices…";
        _services.Log.Info("Devices", "Scanning USB devices.");

        try
        {
            DeviceScanResult result = await _services.Devices.RefreshAsync(token).ConfigureAwait(true);

            string? previousSerial = SelectedDevice?.Record.Serial;
            Devices.Clear();
            foreach (DeviceInfo device in result.Devices)
            {
                Devices.Add(device);
            }

            SelectedDevice = previousSerial is null
                ? Devices.FirstOrDefault()
                : Devices.FirstOrDefault(d => d.Record.Serial == previousSerial) ?? Devices.FirstOrDefault();

            if (result.Error is not null)
            {
                EmptyMessage = "Scan failed: " + result.Error;
                _services.Log.Error("Devices", "Scan failed: " + result.Error);
            }
            else if (Devices.Count == 0)
            {
                EmptyMessage = "No USB devices detected. Connect a tablet and use Refresh.";
                _services.Log.Info("Devices", "No USB devices detected.");
            }
            else
            {
                EmptyMessage = string.Empty;
                _services.Log.Info("Devices", $"{Devices.Count} device(s) detected; {AdbCount} in ADB mode.");
            }
        }
        catch (OperationCanceledException)
        {
            EmptyMessage = "Scan cancelled.";
            _services.Log.Warn("Devices", "Scan cancelled.");
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(DeviceCount));
            OnPropertyChanged(nameof(AdbCount));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void CancelScan()
    {
        _cancellation?.Cancel();
    }
}
