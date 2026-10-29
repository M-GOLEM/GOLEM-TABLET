using System.Collections.ObjectModel;
using System.Text;

namespace Golem.Tablet.Core.Logging;

public sealed class LogService
{
    private readonly IUiDispatcher _dispatcher;
    private readonly int _maxEntries;

    public LogService(IUiDispatcher dispatcher, int maxEntries = 5000)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _maxEntries = Math.Max(100, maxEntries);
    }

    /// <summary>Entries are only mutated on the UI thread.</summary>
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public event EventHandler<LogEntry>? EntryRaised;

    public void Write(LogLevel level, string category, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, category ?? "App", LogRedactor.Redact(message));
        _dispatcher.Post(() =>
        {
            Entries.Add(entry);
            while (Entries.Count > _maxEntries)
            {
                Entries.RemoveAt(0);
            }

            EntryRaised?.Invoke(this, entry);
        });
    }

    public void Debug(string category, string message) => Write(LogLevel.Debug, category, message);

    public void Info(string category, string message) => Write(LogLevel.Info, category, message);

    public void Warn(string category, string message) => Write(LogLevel.Warning, category, message);

    public void Error(string category, string message) => Write(LogLevel.Error, category, message);

    public void Clear() => _dispatcher.Post(Entries.Clear);

    /// <summary>Writes the current buffer to disk and returns the file path.</summary>
    public string Export(string filePath)
    {
        LogEntry[] snapshot = Array.Empty<LogEntry>();
        _dispatcher.Send(() => snapshot = Entries.ToArray());

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("# GOLEM TABLET log");
        builder.AppendLine($"# exported {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        foreach (LogEntry entry in snapshot)
        {
            builder.AppendLine(entry.ToLine());
        }

        File.WriteAllText(filePath, builder.ToString(), Encoding.UTF8);
        return filePath;
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        LogEntry[] snapshot = Array.Empty<LogEntry>();
        _dispatcher.Send(() => snapshot = Entries.ToArray());
        return snapshot;
    }
}
