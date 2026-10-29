namespace Golem.Tablet.Core.Adb;

public sealed class ProcessResult
{
    public ProcessResult(
        bool started,
        int? exitCode,
        string standardOutput,
        string standardError,
        bool timedOut,
        bool cancelled)
    {
        Started = started;
        ExitCode = exitCode;
        StandardOutput = standardOutput ?? string.Empty;
        StandardError = standardError ?? string.Empty;
        TimedOut = timedOut;
        Cancelled = cancelled;
    }

    public bool Started { get; }

    public int? ExitCode { get; }

    public string StandardOutput { get; }

    public string StandardError { get; }

    public bool TimedOut { get; }

    public bool Cancelled { get; }

    public bool Succeeded => Started && !TimedOut && !Cancelled && ExitCode == 0;

    public bool HasErrorOutput => !string.IsNullOrWhiteSpace(StandardError);

    public static ProcessResult NotStarted(string message) =>
        new(false, null, string.Empty, message, false, false);
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
