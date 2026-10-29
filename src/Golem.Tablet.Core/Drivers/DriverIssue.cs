namespace Golem.Tablet.Core.Drivers;

public enum DriverSeverity
{
    Ok,
    Warning,
    Error
}

public sealed class DriverIssue
{
    public DriverIssue(string deviceName, string pnpDeviceId, uint problemCode, string description, DriverSeverity severity)
    {
        DeviceName = deviceName;
        PnpDeviceId = pnpDeviceId;
        ProblemCode = problemCode;
        Description = description;
        Severity = severity;
    }

    public string DeviceName { get; }

    public string PnpDeviceId { get; }

    public uint ProblemCode { get; }

    public string Description { get; }

    public DriverSeverity Severity { get; }

    public string CodeText => $"Code {ProblemCode}";
}

public sealed class DriverReport
{
    public DriverReport(IReadOnlyList<DriverIssue> issues, int inspectedCount, bool isElevated, string? error)
    {
        Issues = issues;
        InspectedCount = inspectedCount;
        IsElevated = isElevated;
        Error = error;
    }

    public IReadOnlyList<DriverIssue> Issues { get; }

    public int InspectedCount { get; }

    public bool IsElevated { get; }

    public string? Error { get; }

    public int ProblemCount => Issues.Count;
}
