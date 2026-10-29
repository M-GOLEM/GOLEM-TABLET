using Golem.Tablet.Core.Configuration;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory;

    public SettingsStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "golem-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Round_trip_preserves_values()
    {
        var store = new SettingsStore(_directory);
        var settings = new AppSettings
        {
            PreferredSerialPort = "COM7",
            LogExportDirectory = Path.GetTempPath(),
            CommandTimeoutSeconds = 45,
            ConfirmDestructiveActions = false
        };

        store.Save(settings);
        AppSettings loaded = store.Load(out string? error);

        Assert.Null(error);
        Assert.Equal("COM7", loaded.PreferredSerialPort);
        Assert.Equal(45, loaded.CommandTimeoutSeconds);
        Assert.False(loaded.ConfirmDestructiveActions);
    }

    [Fact]
    public void Save_leaves_no_temporary_file()
    {
        var store = new SettingsStore(_directory);
        store.Save(AppSettings.CreateDefault());

        Assert.True(File.Exists(store.FilePath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));
    }

    [Fact]
    public void Missing_file_returns_defaults()
    {
        var store = new SettingsStore(_directory);
        AppSettings loaded = store.Load(out string? error);

        Assert.Null(error);
        Assert.Equal(30, loaded.CommandTimeoutSeconds);
        Assert.True(loaded.ConfirmDestructiveActions);
    }

    [Fact]
    public void Invalid_json_returns_defaults_with_error()
    {
        Directory.CreateDirectory(_directory);
        var store = new SettingsStore(_directory);
        File.WriteAllText(store.FilePath, "{ this is not json");

        AppSettings loaded = store.Load(out string? error);

        Assert.NotNull(error);
        Assert.Equal(30, loaded.CommandTimeoutSeconds);
    }

    [Fact]
    public void Out_of_range_timeout_fails_validation()
    {
        SettingsValidationResult result = SettingsStore.Validate(
            new AppSettings { CommandTimeoutSeconds = AppSettings.MaxTimeoutSeconds + 1 });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Timeout"));
    }

    [Fact]
    public void Save_rejects_invalid_settings()
    {
        var store = new SettingsStore(_directory);

        Assert.Throws<InvalidOperationException>(() =>
            store.Save(new AppSettings { CommandTimeoutSeconds = 1 }));
    }

    [Fact]
    public void Relative_paths_fail_validation()
    {
        SettingsValidationResult result = SettingsStore.Validate(new AppSettings
        {
            AdbPath = @"tools\adb.exe",
            LogExportDirectory = "logs"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ADB path"));
        Assert.Contains(result.Errors, e => e.Contains("Log export directory"));
    }

    [Fact]
    public void Malformed_serial_port_fails_validation()
    {
        SettingsValidationResult result = SettingsStore.Validate(
            new AppSettings { PreferredSerialPort = "USB7" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Serial port"));
    }

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(SettingsStore.Validate(AppSettings.CreateDefault()).IsValid);
    }

    [Fact]
    public void Clone_is_independent()
    {
        AppSettings original = AppSettings.CreateDefault();
        AppSettings copy = original.Clone();
        copy.CommandTimeoutSeconds = 90;

        Assert.Equal(30, original.CommandTimeoutSeconds);
        Assert.Equal(90, copy.CommandTimeoutSeconds);
    }
}
