using System.Text.Json;

namespace Golem.Tablet.Core.Configuration;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directory;

    public SettingsStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Golem",
            "TABLET");
    }

    public string FilePath => Path.Combine(_directory, "settings.json");

    /// <summary>
    /// Loads settings. Missing or unreadable files yield defaults and set
    /// <paramref name="error"/> so the caller can surface the reason.
    /// </summary>
    public AppSettings Load(out string? error)
    {
        error = null;
        if (!File.Exists(FilePath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            string json = File.ReadAllText(FilePath);
            AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings is null)
            {
                error = "Settings file was empty.";
                return AppSettings.CreateDefault();
            }

            SettingsValidationResult validation = Validate(settings);
            if (!validation.IsValid)
            {
                error = "Stored settings were invalid: " + string.Join("; ", validation.Errors);
                return AppSettings.CreateDefault();
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            error = "Could not read settings: " + ex.Message;
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        SettingsValidationResult validation = Validate(settings);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "Refusing to save invalid settings: " + string.Join("; ", validation.Errors));
        }

        Directory.CreateDirectory(_directory);
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        string tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, FilePath, overwrite: true);
    }

    public void Reset() => Save(AppSettings.CreateDefault());

    public static SettingsValidationResult Validate(AppSettings settings)
    {
        var errors = new List<string>();

        if (settings.CommandTimeoutSeconds is < AppSettings.MinTimeoutSeconds or > AppSettings.MaxTimeoutSeconds)
        {
            errors.Add(
                $"Timeout must be between {AppSettings.MinTimeoutSeconds} and {AppSettings.MaxTimeoutSeconds} seconds.");
        }

        if (!string.IsNullOrWhiteSpace(settings.AdbPath))
        {
            if (!Path.IsPathRooted(settings.AdbPath))
            {
                errors.Add("ADB path must be an absolute path.");
            }
            else if (!File.Exists(settings.AdbPath))
            {
                errors.Add("ADB path does not point to an existing file.");
            }
            else if (!string.Equals(Path.GetFileName(settings.AdbPath), "adb.exe", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("ADB path must point to adb.exe.");
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.PreferredSerialPort) &&
            !System.Text.RegularExpressions.Regex.IsMatch(settings.PreferredSerialPort, @"^COM\d{1,3}$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            errors.Add("Serial port must look like COM1 … COM999.");
        }

        if (!string.IsNullOrWhiteSpace(settings.LogExportDirectory) &&
            !Path.IsPathRooted(settings.LogExportDirectory))
        {
            errors.Add("Log export directory must be an absolute path.");
        }

        return new SettingsValidationResult(errors);
    }
}
