using System.IO;
using System.Windows.Input;
using Golem.Tablet.Core.Configuration;
using Golem.Tablet.Mvvm;
using Golem.Tablet.Services;

namespace Golem.Tablet.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;

    private string _adbPath = string.Empty;
    private string _preferredSerialPort = string.Empty;
    private string _logExportDirectory = string.Empty;
    private string _timeoutText = "30";
    private bool _confirmDestructiveActions = true;
    private string _message = string.Empty;

    public SettingsViewModel(AppServices services, IDialogService dialogs)
    {
        _services = services;
        _dialogs = dialogs;

        BrowseAdbCommand = new RelayCommand(BrowseAdb);
        BrowseLogDirectoryCommand = new RelayCommand(BrowseLogDirectory);
        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(Reset);

        LoadFromSettings();
    }

    public string AdbPath
    {
        get => _adbPath;
        set => SetProperty(ref _adbPath, value);
    }

    public string PreferredSerialPort
    {
        get => _preferredSerialPort;
        set => SetProperty(ref _preferredSerialPort, value);
    }

    public string LogExportDirectory
    {
        get => _logExportDirectory;
        set => SetProperty(ref _logExportDirectory, value);
    }

    public string TimeoutText
    {
        get => _timeoutText;
        set => SetProperty(ref _timeoutText, value);
    }

    public bool ConfirmDestructiveActions
    {
        get => _confirmDestructiveActions;
        set => SetProperty(ref _confirmDestructiveActions, value);
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    public bool HasMessage => Message.Length > 0;

    public string SettingsFilePath => _services.SettingsStore.FilePath;

    public string TimeoutRange =>
        $"{AppSettings.MinTimeoutSeconds}–{AppSettings.MaxTimeoutSeconds} seconds";

    public ICommand BrowseAdbCommand { get; }

    public ICommand BrowseLogDirectoryCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand ResetCommand { get; }

    private void LoadFromSettings()
    {
        AppSettings settings = _services.Settings;
        AdbPath = settings.AdbPath;
        PreferredSerialPort = settings.PreferredSerialPort;
        LogExportDirectory = settings.LogExportDirectory;
        TimeoutText = settings.CommandTimeoutSeconds.ToString();
        ConfirmDestructiveActions = settings.ConfirmDestructiveActions;
    }

    private void BrowseAdb()
    {
        string? path = _dialogs.PickOpenFile("Select adb.exe", "ADB (adb.exe)|adb.exe|Executables (*.exe)|*.exe");
        if (path is not null)
        {
            AdbPath = path;
        }
    }

    private void BrowseLogDirectory()
    {
        string? folder = _dialogs.PickFolder("Select log export folder");
        if (folder is not null)
        {
            LogExportDirectory = folder;
        }
    }

    private void Save()
    {
        if (!int.TryParse(TimeoutText.Trim(), out int timeout))
        {
            Message = "Timeout must be a whole number.";
            _services.Log.Warn("Settings", Message);
            return;
        }

        var candidate = new AppSettings
        {
            AdbPath = AdbPath.Trim(),
            PreferredSerialPort = PreferredSerialPort.Trim().ToUpperInvariant(),
            LogExportDirectory = LogExportDirectory.Trim(),
            CommandTimeoutSeconds = timeout,
            ConfirmDestructiveActions = ConfirmDestructiveActions
        };

        SettingsValidationResult validation = SettingsStore.Validate(candidate);
        if (!validation.IsValid)
        {
            Message = string.Join(Environment.NewLine, validation.Errors);
            _services.Log.Warn("Settings", "Validation failed: " + string.Join("; ", validation.Errors));
            return;
        }

        try
        {
            _services.ApplySettings(candidate);
            LoadFromSettings();
            Message = "Settings saved.";
            _services.Log.Info("Settings", "Settings saved to " + _services.SettingsStore.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Message = "Could not save settings: " + ex.Message;
            _services.Log.Error("Settings", Message);
        }
    }

    private void Reset()
    {
        bool confirmed = _dialogs.Confirm("Restore defaults",
            "Reset all settings to their default values?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            AppSettings defaults = AppSettings.CreateDefault();
            _services.ApplySettings(defaults);
            LoadFromSettings();
            Message = "Default settings restored.";
            _services.Log.Info("Settings", "Default settings restored.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Message = "Could not reset settings: " + ex.Message;
            _services.Log.Error("Settings", Message);
        }
    }
}
