namespace Golem.Tablet.Core.Configuration;

public sealed class AppSettings
{
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 600;

    /// <summary>Full path to adb.exe. Empty means "search PATH".</summary>
    public string AdbPath { get; set; } = string.Empty;

    /// <summary>Preferred serial port for AT commands, e.g. COM7. Empty means "ask".</summary>
    public string PreferredSerialPort { get; set; } = string.Empty;

    /// <summary>Directory used for log export. Empty means "documents".</summary>
    public string LogExportDirectory { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public bool ConfirmDestructiveActions { get; set; } = true;

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() => new()
    {
        AdbPath = AdbPath,
        PreferredSerialPort = PreferredSerialPort,
        LogExportDirectory = LogExportDirectory,
        CommandTimeoutSeconds = CommandTimeoutSeconds,
        ConfirmDestructiveActions = ConfirmDestructiveActions
    };
}

public sealed class SettingsValidationResult
{
    public SettingsValidationResult(IReadOnlyList<string> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }

    public bool IsValid => Errors.Count == 0;
}
