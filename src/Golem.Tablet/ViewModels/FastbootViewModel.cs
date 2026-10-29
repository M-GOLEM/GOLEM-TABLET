using System.Collections.ObjectModel;
using System.Windows.Input;
using Golem.Tablet.Core.Adb;
using Golem.Tablet.Core.Fastboot;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class FastbootViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private FastbootDevice? _selectedDevice;
    private string _fastbootPath = string.Empty;
    private string _output = "No command has run yet.";
    private bool _isBusy;

    public FastbootViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDevicesAsync, onError: ReportUnexpected);
        VersionCommand = new AsyncRelayCommand(
            () => RunAsync(FastbootActions.VersionArguments(), destructive: false, "fastboot --version"),
            onError: ReportUnexpected);
        ReadVariablesCommand = new AsyncRelayCommand(ReadVariablesAsync, onError: ReportUnexpected);
        RebootSystemCommand = new AsyncRelayCommand(() => RebootAsync("system"), onError: ReportUnexpected);
        RebootBootloaderCommand = new AsyncRelayCommand(() => RebootAsync("bootloader"), onError: ReportUnexpected);
        RebootRecoveryCommand = new AsyncRelayCommand(() => RebootAsync("recovery"), onError: ReportUnexpected);
        RebootFastbootdCommand = new AsyncRelayCommand(() => RebootAsync("fastbootd"), onError: ReportUnexpected);
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, onError: ReportUnexpected);

        _services.SettingsChanged += (_, _) => ResolveFastbootPath();
        ResolveFastbootPath();
    }

    public ObservableCollection<FastbootDevice> Devices { get; } = new();

    public ObservableCollection<FastbootVariable> Variables { get; } = new();

    public FastbootDevice? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public string FastbootPath
    {
        get => _fastbootPath;
        private set
        {
            if (SetProperty(ref _fastbootPath, value))
            {
                OnPropertyChanged(nameof(IsFastbootAvailable));
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public bool IsFastbootAvailable => FastbootPath.Length > 0;

    public string Status => IsFastbootAvailable
        ? FastbootPath
        : "fastboot was not found. Install Android platform-tools or set the ADB path in Settings.";

    public string Output
    {
        get => _output;
        private set => SetProperty(ref _output, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand RefreshDevicesCommand { get; }

    public ICommand VersionCommand { get; }

    public ICommand ReadVariablesCommand { get; }

    public ICommand RebootSystemCommand { get; }

    public ICommand RebootBootloaderCommand { get; }

    public ICommand RebootRecoveryCommand { get; }

    public ICommand RebootFastbootdCommand { get; }

    public ICommand ContinueCommand { get; }

    public async Task RefreshDevicesAsync()
    {
        if (!EnsureFastboot())
        {
            return;
        }

        string? previous = SelectedDevice?.Serial;
        ProcessResult result = await _services.Fastboot
            .RunAsync(FastbootPath, FastbootActions.DevicesArguments(), _services.CommandTimeout, CancellationToken.None)
            .ConfigureAwait(true);

        if (!result.Succeeded)
        {
            string reason = DescribeFailure(result);
            _services.Log.Error("Fastboot", "Device list failed: " + reason);
            Output = reason;
            return;
        }

        IReadOnlyList<FastbootDevice> parsed = FastbootOutputParser.ParseDevices(result.StandardOutput);
        Devices.Clear();
        foreach (FastbootDevice device in parsed)
        {
            Devices.Add(device);
        }

        SelectedDevice = previous is null
            ? Devices.FirstOrDefault(d => d.IsOnline) ?? Devices.FirstOrDefault()
            : Devices.FirstOrDefault(d => d.Serial == previous) ?? Devices.FirstOrDefault();

        Variables.Clear();

        _services.Log.Info("Fastboot", parsed.Count == 0
            ? "No fastboot devices found."
            : $"{parsed.Count} fastboot device(s): " + string.Join(", ", parsed.Select(d => d.Summary)));

        Output = parsed.Count == 0
            ? "No fastboot devices. Reboot the tablet into bootloader mode and reconnect."
            : string.Join(Environment.NewLine, parsed.Select(d => d.Summary));
    }

    private async Task ReadVariablesAsync()
    {
        ProcessResult? result = await RunAsync(
            FastbootActions.GetvarAllArguments(SelectedDevice?.Serial), false, "getvar all").ConfigureAwait(true);

        if (result is null || !result.Started || result.Cancelled || result.TimedOut)
        {
            return;
        }

        IReadOnlyList<FastbootVariable> parsed =
            FastbootOutputParser.ParseVariables(result.StandardOutput + Environment.NewLine + result.StandardError);

        Variables.Clear();
        foreach (FastbootVariable variable in parsed)
        {
            Variables.Add(variable);
        }

        Output = parsed.Count == 0 ? "No bootloader variables were returned." : $"{parsed.Count} variable(s) read.";
        _services.Log.Info("Fastboot", Output);
    }

    private async Task RebootAsync(string target)
    {
        string label = target == "system" ? "reboot" : "reboot " + target;
        await RunAsync(FastbootActions.RebootArguments(target, SelectedDevice?.Serial), true, label)
            .ConfigureAwait(true);
    }

    private Task ContinueAsync() =>
        RunAsync(FastbootActions.ContinueArguments(SelectedDevice?.Serial), true, "continue boot");

    private async Task<ProcessResult?> RunAsync(
        IReadOnlyList<string> arguments,
        bool destructive,
        string label)
    {
        if (destructive && _services.Settings.ConfirmDestructiveActions)
        {
            bool confirmed = _dialogs.Confirm("Confirm operation",
                $"Run \"{label}\" on {SelectedDevice?.Serial ?? "the connected device"}?");
            if (!confirmed)
            {
                _services.Log.Info("Fastboot", "Cancelled by user: " + label);
                return null;
            }
        }

        if (!EnsureFastboot())
        {
            return null;
        }

        IReadOnlyList<string> fullArguments = arguments;
        _services.Log.Info("Fastboot", AdbService.FormatCommand(FastbootPath, fullArguments));
        IsBusy = true;

        try
        {
            ProcessResult result = await _services.Fastboot
                .RunAsync(FastbootPath, fullArguments, _services.CommandTimeout, CancellationToken.None)
                .ConfigureAwait(true);

            Report(result);
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Report(ProcessResult result)
    {
        if (!result.Started)
        {
            Output = "fastboot could not be started: " + result.StandardError;
            _services.Log.Error("Fastboot", Output);
            return;
        }

        if (result.Cancelled)
        {
            _services.Log.Warn("Fastboot", "Command cancelled.");
            return;
        }

        if (result.TimedOut)
        {
            Output = "Command timed out.";
            _services.Log.Error("Fastboot", Output);
            return;
        }

        string combined = result.StandardOutput;
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            combined = combined.TrimEnd() + Environment.NewLine + "[stderr] " + result.StandardError.Trim();
        }

        combined = combined.Trim();
        Output = combined.Length == 0 ? "(no output)" : Truncate(combined, 20000);
        LogBody(combined);

        if (result.Succeeded)
        {
            _services.Log.Info("Fastboot", "Command completed.");
        }
        else
        {
            _services.Log.Error("Fastboot", $"fastboot exited with code {result.ExitCode?.ToString() ?? "unknown"}.");
        }
    }

    private void LogBody(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        const int maxLines = 60;
        foreach (string line in lines.Take(maxLines))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                _services.Log.Info("fastboot", line);
            }
        }

        if (lines.Length > maxLines)
        {
            _services.Log.Info("fastboot", $"… {lines.Length - maxLines} more line(s) not shown.");
        }
    }

    private bool EnsureFastboot()
    {
        ResolveFastbootPath();
        if (IsFastbootAvailable)
        {
            return true;
        }

        Output = Status;
        _services.Log.Error("Fastboot", "fastboot.exe was not found. Install Android platform-tools.");
        return false;
    }

    private void ResolveFastbootPath()
    {
        FastbootPath = _services.Fastboot.ResolveExecutable(_services.Settings) ?? string.Empty;
    }

    private static string DescribeFailure(ProcessResult result)
    {
        if (!result.Started)
        {
            return result.StandardError.Length > 0 ? result.StandardError : "fastboot could not be started.";
        }

        if (result.TimedOut)
        {
            return "fastboot timed out.";
        }

        string stderr = result.StandardError.Trim();
        if (stderr.Length > 0)
        {
            return stderr;
        }

        return $"fastboot exited with code {result.ExitCode?.ToString() ?? "unknown"}.";
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("Fastboot", "Unexpected failure: " + ex.Message);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + Environment.NewLine + "… (truncated)";
}
