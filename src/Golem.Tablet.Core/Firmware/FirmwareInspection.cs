namespace Golem.Tablet.Core.Firmware;

public enum FirmwareSeverity
{
    Info,
    Warning,
    Error
}

public sealed class FirmwareIssue
{
    public FirmwareIssue(FirmwareSeverity severity, string message)
    {
        Severity = severity;
        Message = message;
    }

    public FirmwareSeverity Severity { get; }

    public string Message { get; }

    public string SeverityText => Severity switch
    {
        FirmwareSeverity.Error => "Error",
        FirmwareSeverity.Warning => "Warning",
        _ => "Info"
    };
}

public sealed class FirmwareInspection
{
    public FirmwareInspection(
        string source,
        string formatLabel,
        IReadOnlyList<string> entries,
        IReadOnlyList<FirmwareIssue> issues)
    {
        Source = source;
        FormatLabel = formatLabel;
        Entries = entries;
        Issues = issues;
    }

    public string Source { get; }

    public string FormatLabel { get; }

    public IReadOnlyList<string> Entries { get; }

    public IReadOnlyList<FirmwareIssue> Issues { get; }

    public bool IsValid => Issues.All(issue => issue.Severity != FirmwareSeverity.Error);

    public int ErrorCount => Issues.Count(issue => issue.Severity == FirmwareSeverity.Error);

    public int WarningCount => Issues.Count(issue => issue.Severity == FirmwareSeverity.Warning);

    public string Summary
    {
        get
        {
            string head = IsValid ? "Package looks usable." : "Package has blocking problems.";
            return $"{head} {Entries.Count} entries, {ErrorCount} error(s), {WarningCount} warning(s).";
        }
    }
}
