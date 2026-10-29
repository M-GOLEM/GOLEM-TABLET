using System.Collections.ObjectModel;
using System.Windows.Input;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class SpreadtrumViewModel : ObservableObject
{
    private readonly AppServices _services;
    private bool _isBusy;
    private string _status = "Detect devices to inspect Spreadtrum hardware.";

    public SpreadtrumViewModel(AppServices services)
    {
        _services = services;
        DetectDevicesCommand = new AsyncRelayCommand(DetectAsync, onError: ReportUnexpected);
    }

    public ObservableCollection<DeviceInfo> DetectedDevices { get; } = new();

    public string FlashingNotice =>
        "This build detects Spreadtrum devices and reports their state. Flashing is not implemented: " +
        "it requires a signed download agent (DA) and protocol implementation that are not available for redistribution.";

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand DetectDevicesCommand { get; }

    private async Task DetectAsync()
    {
        IsBusy = true;
        try
        {
            DeviceScanResult scan = await _services.Devices.RefreshAsync().ConfigureAwait(true);
            DetectedDevices.Clear();
            foreach (DeviceInfo device in scan.Devices.Where(d => d.Family == DeviceFamily.Spreadtrum))
            {
                DetectedDevices.Add(device);
            }

            if (scan.Error is not null)
            {
                Status = "Scan failed: " + scan.Error;
                _services.Log.Error("Spreadtrum", "Device scan failed: " + scan.Error);
                return;
            }

            Status = DetectedDevices.Count == 0
                ? "No Spreadtrum devices detected."
                : $"{DetectedDevices.Count} Spreadtrum device(s) detected.";

            _services.Log.Info("Spreadtrum", Status);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("Spreadtrum", "Unexpected failure: " + ex.Message);
}
