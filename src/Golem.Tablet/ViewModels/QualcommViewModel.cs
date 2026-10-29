using System.Collections.ObjectModel;
using System.Windows.Input;
using Golem.Tablet.Core.Devices;
using Golem.Tablet.Core.Firmware;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class QualcommViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private string _folderPath = string.Empty;
    private bool _isBusy;
    private string _formatLabel = "—";
    private string _summary = "No package inspected.";
    private bool _hasResult;

    public QualcommViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        BrowseCommand = new RelayCommand(Browse);
        InspectCommand = new AsyncRelayCommand(InspectAsync, onError: ReportUnexpected);
        DetectDevicesCommand = new AsyncRelayCommand(DetectDevicesAsync, onError: ReportUnexpected);
    }

    public ObservableCollection<FirmwareIssue> Issues { get; } = new();

    public ObservableCollection<string> Partitions { get; } = new();

    public ObservableCollection<DeviceInfo> DetectedDevices { get; } = new();

    public string RequiredFiles =>
        "Expected folder: rawprogram*.xml, patch*.xml, and a firehose programmer (prog_firehose*.elf/.mbn).";

    public string FlashingNotice =>
        "This build inspects EDL firmware packages only. Flashing is not implemented: firehose " +
        "programmers are proprietary per-OEM artifacts and no redistributable implementation is bundled.";

    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
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

    public bool HasPartitions => Partitions.Count > 0;

    public string EmptyHint =>
        "Choose a folder containing rawprogram*.xml, patch*.xml and a firehose programmer, then select Inspect.";

    public ICommand BrowseCommand { get; }

    public ICommand InspectCommand { get; }

    public ICommand DetectDevicesCommand { get; }

    private void Browse()
    {
        string? folder = _dialogs.PickFolder("Select EDL firmware folder");
        if (folder is not null)
        {
            FolderPath = folder;
        }
    }

    private async Task InspectAsync()
    {
        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            _services.Log.Warn("Qualcomm", "No firmware folder selected.");
            return;
        }

        IsBusy = true;
        _services.Log.Info("Qualcomm", $"Inspecting folder {FolderPath}.");
        try
        {
            string folder = FolderPath;
            FirmwareInspection inspection = await Task.Run(() => QualcommFirmwareInspector.Inspect(folder))
                .ConfigureAwait(true);

            Issues.Clear();
            foreach (FirmwareIssue issue in inspection.Issues)
            {
                Issues.Add(issue);
                WriteIssue(issue);
            }

            Partitions.Clear();
            foreach (string partition in inspection.Entries)
            {
                Partitions.Add(partition);
            }

            FormatLabel = inspection.FormatLabel;
            Summary = inspection.Summary;
            HasResult = true;
            OnPropertyChanged(nameof(HasIssues));
            OnPropertyChanged(nameof(HasPartitions));
            _services.Log.Info("Qualcomm", inspection.Summary);
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
        foreach (DeviceInfo device in scan.Devices.Where(d => d.Family == DeviceFamily.Qualcomm))
        {
            DetectedDevices.Add(device);
        }

        if (scan.Error is not null)
        {
            _services.Log.Error("Qualcomm", "Device scan failed: " + scan.Error);
            return;
        }

        bool hasEdl = DetectedDevices.Any(d => d.Mode == ConnectionMode.QualcommEdl);
        _services.Log.Info("Qualcomm",
            DetectedDevices.Count == 0
                ? "No Qualcomm devices detected."
                : $"{DetectedDevices.Count} Qualcomm device(s) detected" + (hasEdl ? " (EDL present)." : "."));
    }

    private void WriteIssue(FirmwareIssue issue)
    {
        switch (issue.Severity)
        {
            case FirmwareSeverity.Error:
                _services.Log.Error("Qualcomm", issue.Message);
                break;
            case FirmwareSeverity.Warning:
                _services.Log.Warn("Qualcomm", issue.Message);
                break;
            default:
                _services.Log.Info("Qualcomm", issue.Message);
                break;
        }
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("Qualcomm", "Unexpected failure: " + ex.Message);
}
