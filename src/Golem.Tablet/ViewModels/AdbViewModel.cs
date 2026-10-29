using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Golem.Tablet.Core.Adb;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class AdbViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private AdbDevice? _selectedDevice;
    private string _adbPath = string.Empty;
    private string _output = "No command has run yet.";
    private string _commandText = "getprop ro.product.model";
    private string _packageName = string.Empty;
    private string _remotePath = AdbActions.DefaultRemoteDirectory;
    private bool _isBusy;

    public AdbViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        RefreshDevicesCommand = new AsyncRelayCommand(RefreshDeviceListAsync, onError: ReportUnexpected);
        CheckVersionCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "version" }, destructive: false, "adb version"), onError: ReportUnexpected);
        PropertiesCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "shell", "getprop" }, destructive: false, "getprop"), onError: ReportUnexpected);
        AndroidIdCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "shell", "settings", "get", "secure", "android_id" }, false, "android_id"),
            onError: ReportUnexpected);
        DiskUsageCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "shell", "df", "-h" }, false, "df"), onError: ReportUnexpected);
        RebootSystemCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "reboot" }, true, "reboot"), onError: ReportUnexpected);
        RebootRecoveryCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "reboot", "recovery" }, true, "reboot recovery"), onError: ReportUnexpected);
        RebootBootloaderCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "reboot", "bootloader" }, true, "reboot bootloader"), onError: ReportUnexpected);
        RebootEdlCommand = new AsyncRelayCommand(
            () => RunAdbAsync(AdbActions.RebootArguments("edl"), true, "reboot edl"), onError: ReportUnexpected);
        RebootDownloadCommand = new AsyncRelayCommand(
            () => RunAdbAsync(AdbActions.RebootArguments("download"), true, "reboot download"), onError: ReportUnexpected);
        ListPackagesCommand = new AsyncRelayCommand(
            () => RunAdbAsync(AdbActions.ListThirdPartyPackagesArguments(), false, "pm list packages -3"),
            onError: ReportUnexpected);
        UninstallPackageCommand = new AsyncRelayCommand(UninstallPackageAsync, onError: ReportUnexpected);
        LogcatCommand = new AsyncRelayCommand(
            () => RunAdbAsync(AdbActions.LogcatArguments(400), false, "logcat -d -t 400"), onError: ReportUnexpected);
        ScreenshotCommand = new AsyncRelayCommand(CaptureScreenshotAsync, onError: ReportUnexpected);
        PushFileCommand = new AsyncRelayCommand(PushFileAsync, onError: ReportUnexpected);
        PullFileCommand = new AsyncRelayCommand(PullFileAsync, onError: ReportUnexpected);
        InstallApkCommand = new AsyncRelayCommand(InstallApkAsync, onError: ReportUnexpected);
        ExtractApkCommand = new AsyncRelayCommand(ExtractApkAsync, onError: ReportUnexpected);
        RecordScreenCommand = new AsyncRelayCommand(RecordScreenAsync, onError: ReportUnexpected);
        ProfileCommand = new AsyncRelayCommand(ShowProfileAsync, onError: ReportUnexpected);
        RunShellCommand = new AsyncRelayCommand(
            () => RunAdbAsync(new[] { "shell", CommandText }, false, "shell " + CommandText), onError: ReportUnexpected);

        _services.SettingsChanged += (_, _) => ResolveAdbPath();
        ResolveAdbPath();
    }

    public ObservableCollection<AdbDevice> Devices { get; } = new();

    public AdbDevice? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public string AdbPath
    {
        get => _adbPath;
        private set
        {
            if (SetProperty(ref _adbPath, value))
            {
                OnPropertyChanged(nameof(AdbStatus));
                OnPropertyChanged(nameof(IsAdbAvailable));
            }
        }
    }

    public bool IsAdbAvailable => AdbPath.Length > 0;

    public string AdbStatus => IsAdbAvailable
        ? AdbPath
        : "ADB was not found. Set the full path to adb.exe in Settings.";

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

    public string PackageName
    {
        get => _packageName;
        set => SetProperty(ref _packageName, value);
    }

    public string RemotePath
    {
        get => _remotePath;
        set => SetProperty(ref _remotePath, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public ICommand RefreshDevicesCommand { get; }

    public ICommand CheckVersionCommand { get; }

    public ICommand PropertiesCommand { get; }

    public ICommand AndroidIdCommand { get; }

    public ICommand DiskUsageCommand { get; }

    public ICommand RebootSystemCommand { get; }

    public ICommand RebootRecoveryCommand { get; }

    public ICommand RebootBootloaderCommand { get; }

    public ICommand RebootEdlCommand { get; }

    public ICommand RebootDownloadCommand { get; }

    public ICommand ListPackagesCommand { get; }

    public ICommand UninstallPackageCommand { get; }

    public ICommand LogcatCommand { get; }

    public ICommand ScreenshotCommand { get; }

    public ICommand PushFileCommand { get; }

    public ICommand PullFileCommand { get; }

    public ICommand InstallApkCommand { get; }

    public ICommand ExtractApkCommand { get; }

    public ICommand RecordScreenCommand { get; }

    public ICommand ProfileCommand { get; }

    public ICommand RunShellCommand { get; }

    public Task InitializeAsync() => RefreshDeviceListAsync();

    public async Task RefreshDeviceListAsync()
    {
        if (!EnsureAdb())
        {
            return;
        }

        string? previous = SelectedDevice?.Serial;
        var result = await _services.Adb
            .ListDevicesAsync(AdbPath, _services.CommandTimeout, CancellationToken.None)
            .ConfigureAwait(true);

        if (!result.Succeeded)
        {
            string reason = DescribeFailure(result);
            _services.Log.Error("ADB", "Device list failed: " + reason);
            Output = reason;
            return;
        }

        IReadOnlyList<AdbDevice> parsed = AdbOutputParser.ParseDevices(result.StandardOutput);
        Devices.Clear();
        foreach (AdbDevice device in parsed)
        {
            Devices.Add(device);
        }

        SelectedDevice = previous is null
            ? Devices.FirstOrDefault(d => d.IsOnline) ?? Devices.FirstOrDefault()
            : Devices.FirstOrDefault(d => d.Serial == previous) ?? Devices.FirstOrDefault();

        _services.Log.Info("ADB", parsed.Count == 0
            ? "No ADB devices found."
            : $"{parsed.Count} ADB device(s): " + string.Join(", ", parsed.Select(d => d.Summary)));

        Output = parsed.Count == 0
            ? "No devices listed. Enable USB debugging and accept the authorization prompt."
            : string.Join(Environment.NewLine, parsed.Select(d => d.Summary));
    }

    private async Task RunAdbAsync(IReadOnlyList<string> arguments, bool destructive, string label) =>
        await ExecuteAsync(arguments, destructive, label).ConfigureAwait(true);

    private async Task<ProcessResult?> ExecuteAsync(
        IReadOnlyList<string> arguments,
        bool destructive,
        string label,
        TimeSpan? timeout = null)
    {
        if (destructive && _services.Settings.ConfirmDestructiveActions)
        {
            bool confirmed = _dialogs.Confirm("Confirm operation",
                $"Run \"{label}\" on {SelectedDevice?.Serial ?? "the connected device"}?");
            if (!confirmed)
            {
                _services.Log.Info("ADB", "Cancelled by user: " + label);
                return null;
            }
        }

        if (!EnsureAdb())
        {
            return null;
        }

        IReadOnlyList<string> fullArguments = BuildArguments(arguments);
        _services.Log.Info("ADB", AdbService.FormatCommand(AdbPath, fullArguments));
        IsBusy = true;

        try
        {
            ProcessResult result = await _services.Adb
                .RunAsync(AdbPath, fullArguments, timeout ?? _services.CommandTimeout, CancellationToken.None)
                .ConfigureAwait(true);

            Report(result);
            return result;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UninstallPackageAsync()
    {
        string package = PackageName.Trim();
        if (!AdbActions.IsValidPackageName(package))
        {
            Output = "Enter a valid package name (letters, digits, dots, underscores).";
            _services.Log.Warn("ADB", "Uninstall skipped: invalid package name.");
            return;
        }

        await RunAdbAsync(AdbActions.UninstallArguments(package), true, "uninstall " + package).ConfigureAwait(true);
    }

    private async Task CaptureScreenshotAsync()
    {
        string? target = _dialogs.PickSaveFile(
            "Save screenshot",
            "PNG image|*.png",
            $"screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        if (target is null)
        {
            return;
        }

        string remote = AdbActions.DefaultScreenshotRemotePath;
        ProcessResult? capture = await ExecuteAsync(AdbActions.ScreenshotArguments(remote), false, "screencap")
            .ConfigureAwait(true);
        if (capture is null || !capture.Succeeded)
        {
            return;
        }

        ProcessResult? pull = await ExecuteAsync(AdbActions.PullArguments(remote, target), false, "pull screenshot")
            .ConfigureAwait(true);
        await ExecuteAsync(AdbActions.DeleteRemoteFileArguments(remote), false, "rm screenshot")
            .ConfigureAwait(true);

        if (pull is not null && pull.Succeeded)
        {
            Output = "Screenshot saved to " + target;
            _services.Log.Info("ADB", Output);
        }
    }

    private async Task PushFileAsync()
    {        string? local = _dialogs.PickOpenFile("Select a file to push", "All files|*.*");
        if (local is null)
        {
            return;
        }

        await RunAdbAsync(AdbActions.PushArguments(local, RemotePath), false, "push " + Path.GetFileName(local))
            .ConfigureAwait(true);
    }

    private async Task PullFileAsync()
    {
        string remote = RemotePath.Trim();
        if (remote.Length == 0 || remote.EndsWith('/'))
        {
            Output = "Enter the full remote file path, for example /sdcard/Download/file.zip.";
            _services.Log.Warn("ADB", "Pull skipped: remote path is missing a file name.");
            return;
        }

        string fileName = AdbActions.RemoteFileName(remote);
        string? local = _dialogs.PickSaveFile("Save pulled file", "All files|*.*", fileName);
        if (local is null)
        {
            return;
        }

        await RunAdbAsync(AdbActions.PullArguments(remote, local), false, "pull " + fileName).ConfigureAwait(true);
    }

    private async Task InstallApkAsync()
    {
        string? local = _dialogs.PickOpenFile(
            "Select an APK to install",
            "Android packages (*.apk)|*.apk|All files (*.*)|*.*");
        if (local is null)
        {
            return;
        }

        if (!File.Exists(local))
        {
            Output = "The selected APK no longer exists.";
            _services.Log.Warn("ADB", Output);
            return;
        }

        await RunAdbAsync(AdbActions.InstallArguments(local), true, "install " + Path.GetFileName(local))
            .ConfigureAwait(true);
    }

    private async Task ExtractApkAsync()
    {
        string package = PackageName.Trim();
        if (!AdbActions.IsValidPackageName(package))
        {
            Output = "Enter a valid package name before extracting its APK.";
            _services.Log.Warn("ADB", "Extract skipped: invalid package name.");
            return;
        }

        ProcessResult? query = await ExecuteAsync(
            AdbActions.PackagePathArguments(package), false, "pm path " + package).ConfigureAwait(true);
        if (query is null || !query.Started || !query.Succeeded)
        {
            return;
        }

        string? remote = AdbActions.ParsePackagePath(query.StandardOutput);
        if (remote is null)
        {
            Output = $"No APK path was returned for {package}.";
            _services.Log.Warn("ADB", Output);
            return;
        }

        string? local = _dialogs.PickSaveFile(
            "Save APK", "Android packages (*.apk)|*.apk", package + ".apk");
        if (local is null)
        {
            return;
        }

        ProcessResult? pull = await ExecuteAsync(
            AdbActions.PullArguments(remote, local), false, "pull " + package + ".apk").ConfigureAwait(true);
        if (pull is not null && pull.Succeeded)
        {
            Output = "APK saved to " + local;
            _services.Log.Info("ADB", Output);
        }
    }

    private async Task RecordScreenAsync()
    {
        string? target = _dialogs.PickSaveFile(
            "Save screen recording",
            "MP4 video (*.mp4)|*.mp4",
            $"screenrecord-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
        if (target is null)
        {
            return;
        }

        string remote = AdbActions.DefaultScreenRecordRemotePath;
        int seconds = AdbActions.ScreenRecordSeconds;
        TimeSpan timeout = _services.CommandTimeout + TimeSpan.FromSeconds(seconds + 15);

        ProcessResult? record = await ExecuteAsync(
            AdbActions.ScreenRecordArguments(seconds, remote), false, $"screenrecord {seconds} s", timeout)
            .ConfigureAwait(true);
        if (record is null || !record.Succeeded)
        {
            return;
        }

        ProcessResult? pull = await ExecuteAsync(
            AdbActions.PullArguments(remote, target), false, "pull recording").ConfigureAwait(true);
        await ExecuteAsync(AdbActions.DeleteRemoteFileArguments(remote), false, "rm recording")
            .ConfigureAwait(true);

        if (pull is not null && pull.Succeeded)
        {
            Output = "Screen recording saved to " + target;
            _services.Log.Info("ADB", Output);
        }
    }

    private async Task ShowProfileAsync()
    {
        ProcessResult? result = await ExecuteAsync(
            new[] { "shell", "getprop" }, false, "getprop (profile)").ConfigureAwait(true);
        if (result is null || !result.Started || !result.Succeeded)
        {
            return;
        }

        DeviceProfile profile = GetpropParser.Describe(result.StandardOutput);
        Output = profile.ToText();
        _services.Log.Info("ADB", "Device profile: " + profile.Summary);
    }

    private IReadOnlyList<string> BuildArguments(IReadOnlyList<string> arguments)
    {
        if (SelectedDevice is null)
        {
            return arguments;
        }

        var full = new List<string> { "-s", SelectedDevice.Serial };
        full.AddRange(arguments);
        return full;
    }

    private void Report(ProcessResult result)
    {
        if (!result.Started)
        {
            Output = "ADB could not be started: " + result.StandardError;
            _services.Log.Error("ADB", Output);
            return;
        }

        if (result.Cancelled)
        {
            _services.Log.Warn("ADB", "Command cancelled.");
            return;
        }

        if (result.TimedOut)
        {
            Output = "Command timed out.";
            _services.Log.Error("ADB", Output);
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
            _services.Log.Info("ADB", "Command completed.");
        }
        else
        {
            _services.Log.Error("ADB", $"adb exited with code {result.ExitCode?.ToString() ?? "unknown"}.");
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
                _services.Log.Info("adb", line);
            }
        }

        if (lines.Length > maxLines)
        {
            _services.Log.Info("adb", $"… {lines.Length - maxLines} more line(s) not shown.");
        }
    }

    private bool EnsureAdb()
    {
        ResolveAdbPath();
        if (IsAdbAvailable)
        {
            return true;
        }

        Output = AdbStatus;
        _services.Log.Error("ADB", "adb.exe was not found. Configure it in Settings.");
        return false;
    }

    private void ResolveAdbPath()
    {
        AdbPath = _services.Adb.ResolveExecutable(_services.Settings) ?? string.Empty;
    }

    private static string DescribeFailure(ProcessResult result)
    {
        if (!result.Started)
        {
            return result.StandardError.Length > 0 ? result.StandardError : "adb could not be started.";
        }

        if (result.TimedOut)
        {
            return "adb timed out.";
        }

        string stderr = result.StandardError.Trim();
        if (stderr.Length > 0)
        {
            return stderr;
        }

        return $"adb exited with code {result.ExitCode?.ToString() ?? "unknown"}.";
    }

    private void ReportUnexpected(Exception ex) =>
        _services.Log.Error("ADB", "Unexpected failure: " + ex.Message);

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + Environment.NewLine + "… (truncated)";
}
