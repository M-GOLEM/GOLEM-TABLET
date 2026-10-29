using Golem.Tablet.Core.Logging;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class LogServiceTests : IDisposable
{
    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public void Send(Action action) => action();
    }

    private readonly string _directory;

    public LogServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "golem-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Entries_capture_level_category_and_timestamp()
    {
        var log = new LogService(new ImmediateDispatcher());

        log.Info("Devices", "scanning");
        log.Warn("ADB", "missing");

        Assert.Equal(2, log.Entries.Count);
        Assert.Equal(LogLevel.Info, log.Entries[0].Level);
        Assert.Equal("Devices", log.Entries[0].Category);
        Assert.Equal("INF", log.Entries[0].LevelText);
        Assert.Equal("WRN", log.Entries[1].LevelText);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}\.\d{3}$", log.Entries[0].TimestampText);
    }

    [Fact]
    public void Credentials_are_redacted_before_storage()
    {
        var log = new LogService(new ImmediateDispatcher());

        log.Info("Auth", "password=letmein token: abc");

        Assert.DoesNotContain("letmein", log.Entries[0].Message);
        Assert.DoesNotContain("abc", log.Entries[0].Message);
    }

    [Fact]
    public void Buffer_is_bounded()
    {
        var log = new LogService(new ImmediateDispatcher(), maxEntries: 100);

        for (int i = 0; i < 150; i++)
        {
            log.Debug("Test", i.ToString());
        }

        Assert.Equal(100, log.Entries.Count);
        Assert.Equal("50", log.Entries[0].Message);
    }

    [Fact]
    public void Clear_empties_the_buffer()
    {
        var log = new LogService(new ImmediateDispatcher());
        log.Info("App", "one");

        log.Clear();

        Assert.Empty(log.Entries);
    }

    [Fact]
    public void Export_writes_header_and_entries()
    {
        var log = new LogService(new ImmediateDispatcher());
        log.Info("App", "first line");
        log.Error("ADB", "second line");
        string path = Path.Combine(_directory, "session.log");

        string written = log.Export(path);

        Assert.Equal(path, written);
        string text = File.ReadAllText(written);
        Assert.StartsWith("# GOLEM TABLET log", text);
        Assert.Contains("[INF] App: first line", text);
        Assert.Contains("[ERR] ADB: second line", text);
    }

    [Fact]
    public void Snapshot_returns_a_copy()
    {
        var log = new LogService(new ImmediateDispatcher());
        log.Info("App", "one");

        IReadOnlyList<LogEntry> snapshot = log.Snapshot();
        log.Info("App", "two");

        Assert.Single(snapshot);
        Assert.Equal(2, log.Entries.Count);
    }
}
