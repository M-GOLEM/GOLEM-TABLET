namespace Golem.Tablet.Core.Logging;

public sealed class LogEntry
{
    public LogEntry(DateTime timestamp, LogLevel level, string category, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Category = category;
        Message = message;
    }

    public DateTime Timestamp { get; }

    public LogLevel Level { get; }

    public string Category { get; }

    public string Message { get; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

    public string LevelText => Level switch
    {
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        _ => "???"
    };

    public string ToLine() =>
        $"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{LevelText}] {Category}: {Message}";
}
