using System.Collections.ObjectModel;
using System.Windows.Input;
using Golem.Tablet.Core.Drivers;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class DriversViewModel : ObservableObject
{
    private readonly AppServices _services;
    private bool _isBusy;
    private bool _isElevated;
    private bool _hasInspected;
    private int _inspectedCount;
    private string _status = "Run a check to inspect device drivers.";
    private string? _error;

    public DriversViewModel(AppServices services)
    {
        _services = services;
        InspectCommand = new AsyncRelayCommand(InspectAsync, onError: ReportUnexpected);
    }

    public ObservableCollection<DriverIssue> Issues { get; } = new();

    public string Guidance => DriverProblemCodes.Guidance;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsElevated
    {
        get => _isElevated;
        private set
        {
            if (SetProperty(ref _isElevated, value))
            {
                OnPropertyChanged(nameof(ElevationText));
            }
        }
    }

    public string ElevationText => IsElevated
        ? "Running with administrator rights."
        : "Not elevated. Full driver detail may require running as administrator.";

    public bool HasInspected
    {
        get => _hasInspected;
        private set => SetProperty(ref _hasInspected, value);
    }

    public int InspectedCount
    {
        get => _inspectedCount;
        private set => SetProperty(ref _inspectedCount, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public ICommand InspectCommand { get; }

    private async Task InspectAsync()
    {
        IsBusy = true;
        _services.Log.Info("Drivers", "Inspecting device driver state.");
        try
        {
            DriverReport report = await _services.Drivers.InspectAsync().ConfigureAwait(true);

            Issues.Clear();
            foreach (DriverIssue issue in report.Issues)
            {
                Issues.Add(issue);
            }

            InspectedCount = report.InspectedCount;
            IsElevated = report.IsElevated;
            Error = report.Error;

            if (report.Error is not null)
            {
                Status = "Inspection failed.";
                _services.Log.Error("Drivers", report.Error);
                return;
            }

            HasInspected = true;
            Status = $"{report.InspectedCount} USB device(s) inspected, {report.ProblemCount} with problems.";

            foreach (DriverIssue issue in report.Issues)
            {
                string line = $"{issue.DeviceName}: {issue.Description} ({issue.CodeText})";
                if (issue.Severity == DriverSeverity.Error)
                {
                    _services.Log.Error("Drivers", line);
                }
                else
                {
                    _services.Log.Warn("Drivers", line);
                }
            }

            if (report.ProblemCount == 0)
            {
                _services.Log.Info("Drivers", "No driver problems reported.");
            }

            _services.Log.Info("Drivers", Status);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("Drivers", "Unexpected failure: " + ex.Message);
}
