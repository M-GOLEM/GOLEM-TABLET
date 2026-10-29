using System.Collections.ObjectModel;
using System.Windows.Input;
using Golem.Tablet.Core.At;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class MediaTekViewModel : ObservableObject
{
    private readonly AppServices _services;
    private string? _selectedPort;
    private string _commandText = "ATI";
    private string _output = "No command has run yet.";
    private bool _isBusy;
    private string _status = "Select a serial port and send a command.";

    public MediaTekViewModel(AppServices services)
    {
        _services = services;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        DetectDevicesCommand = new AsyncRelayCommand(DetectDevicesAsync, onError: ReportUnexpected);
        SendCommand = new AsyncRelayCommand(() => SendAsync(CommandText), onError: ReportUnexpected);

        AtiCommand = new AsyncRelayCommand(() => SendAsync("ATI"), onError: ReportUnexpected);
        ModelCommand = new AsyncRelayCommand(() => SendAsync("AT+CGMM"), onError: ReportUnexpected);
        RevisionCommand = new AsyncRelayCommand(() => SendAsync("AT+CGMR"), onError: ReportUnexpected);
        SerialNumberCommand = new AsyncRelayCommand(() => SendAsync("AT+CGSN"), onError: ReportUnexpected);
        SignalCommand = new AsyncRelayCommand(() => SendAsync("AT+CSQ"), onError: ReportUnexpected);
        RegistrationCommand = new AsyncRelayCommand(() => SendAsync("AT+CREG?"), onError: ReportUnexpected);

        _services.SettingsChanged += (_, _) => RefreshPorts();
        RefreshPorts();
    }

    public ObservableCollection<string> Ports { get; } = new();

    public ObservableCollection<DeviceInfo> DetectedDevices { get; } = new();

    public string? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    public string CommandText
    {
        get => _commandText;
        set => SetProperty(ref _commandText, value);
    }

    public string Output
    {
        get => _output;
        private set => SetProperty(ref _output, value);
    }

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

    public ICommand RefreshPortsCommand { get; }

    public ICommand DetectDevicesCommand { get; }

    public ICommand SendCommand { get; }

    public ICommand AtiCommand { get; }

    public ICommand ModelCommand { get; }

    public ICommand RevisionCommand { get; }

    public ICommand SerialNumberCommand { get; }

    public ICommand SignalCommand { get; }

    public ICommand RegistrationCommand { get; }

    private void RefreshPorts()
    {
        string? previous = SelectedPort;
        Ports.Clear();
        foreach (string port in _services.At.GetPorts())
        {
            Ports.Add(port);
        }

        string preferred = _services.Settings.PreferredSerialPort;
        SelectedPort = (previous is not null && Ports.Contains(previous))
            ? previous
            : Ports.FirstOrDefault(p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase))
              ?? Ports.FirstOrDefault();

        Status = Ports.Count == 0
            ? "No serial ports found. Connect the tablet and install the vendor modem driver."
            : $"{Ports.Count} port(s) available.";

        _services.Log.Info("MediaTek", $"Serial ports: {(Ports.Count == 0 ? "none" : string.Join(", ", Ports))}");
    }

    private async Task SendAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _services.Log.Warn("MediaTek", "No serial port selected.");
            Output = "Select a serial port first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            _services.Log.Warn("MediaTek", "Command is empty.");
            return;
        }

        string port = SelectedPort!;
        IsBusy = true;
        _services.Log.Info("AT", $"{port} > {command.Trim()}");

        try
        {
            AtResponse response = await _services.At
                .SendAsync(port, command, _services.CommandTimeout, CancellationToken.None)
                .ConfigureAwait(true);

            Output = response.Succeeded ? response.DisplayText : response.Error ?? "No response.";
            if (response.Succeeded)
            {
                _services.Log.Info("AT", $"{port} < OK");
                foreach (string line in response.Lines.Take(30))
                {
                    _services.Log.Info("at", line);
                }

                Status = "Last command succeeded.";
            }
            else
            {
                _services.Log.Error("AT", $"{port} < {response.Error}");
                Status = "Last command failed.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DetectDevicesAsync()
    {
        DeviceScanResult scan = await _services.Devices.RefreshAsync().ConfigureAwait(true);
        DetectedDevices.Clear();
        foreach (DeviceInfo device in scan.Devices.Where(d => d.Family == DeviceFamily.MediaTek))
        {
            DetectedDevices.Add(device);
        }

        if (scan.Error is not null)
        {
            _services.Log.Error("MediaTek", "Device scan failed: " + scan.Error);
            return;
        }

        _services.Log.Info("MediaTek",
            DetectedDevices.Count == 0
                ? "No MediaTek devices detected."
                : $"{DetectedDevices.Count} MediaTek device(s) detected.");
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("MediaTek", "Unexpected failure: " + ex.Message);
}
