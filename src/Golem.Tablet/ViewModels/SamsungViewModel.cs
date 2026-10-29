using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Core.Firmware;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class SamsungViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private string _firmwarePath = string.Empty;
    private bool _verifyChecksum = true;
    private bool _isBusy;
    private string _formatLabel = "—";
    private string _summary = "No package inspected.";
    private string _componentSummary = string.Empty;
    private bool _hasResult;

    public SamsungViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        BrowseCommand = new RelayCommand(Browse);
        InspectCommand = new AsyncRelayCommand(InspectAsync, onError: ReportUnexpected);
        DetectDevicesCommand = new AsyncRelayCommand(DetectDevicesAsync, onError: ReportUnexpected);
    }

    public ObservableCollection<FirmwareIssue> Issues { get; } = new();

    public ObservableCollection<PackageEntry> Entries { get; } = new();

    public ObservableCollection<DeviceInfo> DetectedDevices { get; } = new();

    public string RequiredFiles =>
        "Expected package: .tar or .tar.md5 containing AP / BL / CP / CSC images (and optionally a .pit).";

    public string FlashingNotice =>
        "This build validates firmware packages only. Flashing is not implemented: it requires a " +
        "licensed Odin protocol implementation that is not available for redistribution. " +
        "Use Samsung's official tools for flashing.";

    public string FirmwarePath
    {
        get => _firmwarePath;
        set => SetProperty(ref _firmwarePath, value);
    }

    public bool VerifyChecksum
    {
        get => _verifyChecksum;
        set => SetProperty(ref _verifyChecksum, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string FormatLabel
    {
        get => _formatLabel;
        private set => SetProperty(ref _formatLabel, value);
    }

    public string ComponentSummary
    {
        get => _componentSummary;
        private set => SetProperty(ref _componentSummary, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool HasResult
    {
        get => _hasResult;
        private set => SetProperty(ref _hasResult, value);
    }

    public bool HasIssues => Issues.Count > 0;

    public bool HasEntries => Entries.Count > 0;

    public string EmptyHint =>
        "Choose a .tar or .tar.md5 package, then select Inspect to validate it.";

    public ICommand BrowseCommand { get; }

    public ICommand InspectCommand { get; }

    public ICommand DetectDevicesCommand { get; }

    private void Browse()
    {
        string? path = _dialogs.PickOpenFile(
            "Select Samsung firmware",
            "Samsung firmware (*.tar;*.tar.md5;*.md5)|*.tar;*.md5|All files (*.*)|*.*");
        if (path is not null)
        {
            FirmwarePath = path;
        }
    }

    private async Task InspectAsync()
    {
        if (string.IsNullOrWhiteSpace(FirmwarePath))
        {
            _services.Log.Warn("Samsung", "No firmware file selected.");
            return;
        }

        if (!File.Exists(FirmwarePath))
        {
            _services.Log.Error("Samsung", "Firmware file not found: " + FirmwarePath);
            return;
        }

        IsBusy = true;
        _services.Log.Info("Samsung", $"Inspecting {Path.GetFileName(FirmwarePath)} (MD5 verify: {VerifyChecksum}).");
        try
        {
            bool verify = VerifyChecksum;
            string path = FirmwarePath;
            FirmwareInspection inspection = await Task.Run(() => SamsungFirmwareInspector.Inspect(path, verify))
                .ConfigureAwait(true);

            Issues.Clear();
            foreach (FirmwareIssue issue in inspection.Issues)
            {
                Issues.Add(issue);
                WriteIssue(issue);
            }

            Entries.Clear();
            foreach (string entry in inspection.Entries)
            {
                Entries.Add(new PackageEntry(entry, SamsungFirmwareClassifier.Classify(entry)));
            }

            FormatLabel = inspection.FormatLabel;
            ComponentSummary = SamsungFirmwareClassifier.Summarize(inspection.Entries);
            Summary = inspection.Summary;
            HasResult = true;
            OnPropertyChanged(nameof(HasIssues));
            OnPropertyChanged(nameof(HasEntries));
            _services.Log.Info("Samsung", inspection.Summary);
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
        foreach (DeviceInfo device in scan.Devices.Where(d => d.Family == DeviceFamily.Samsung))
        {
            DetectedDevices.Add(device);
        }

        if (scan.Error is not null)
        {
            _services.Log.Error("Samsung", "Device scan failed: " + scan.Error);
            return;
        }

        _services.Log.Info("Samsung",
            DetectedDevices.Count == 0
                ? "No Samsung devices detected."
                : $"{DetectedDevices.Count} Samsung device(s) detected.");
    }

    private void WriteIssue(FirmwareIssue issue)
    {
        switch (issue.Severity)
        {
            case FirmwareSeverity.Error:
                _services.Log.Error("Samsung", issue.Message);
                break;
            case FirmwareSeverity.Warning:
                _services.Log.Warn("Samsung", issue.Message);
                break;
            default:
                _services.Log.Info("Samsung", issue.Message);
                break;
        }
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("Samsung", "Unexpected failure: " + ex.Message);
}

public sealed record PackageEntry(string Name, string Kind);
