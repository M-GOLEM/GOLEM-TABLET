namespace Golem.Tablet.Core.At;

public sealed class AtResponse
{
    public AtResponse(bool succeeded, IReadOnlyList<string> lines, string rawText, string? error)
    {
        Succeeded = succeeded;
        Lines = lines;
        RawText = rawText ?? string.Empty;
        Error = error;
    }

    public bool Succeeded { get; }

    /// <summary>Response lines with echo and the final result code removed.</summary>
    public IReadOnlyList<string> Lines { get; }

    public string RawText { get; }

    public string? Error { get; }

    public string DisplayText => Lines.Count > 0 ? string.Join(Environment.NewLine, Lines) : "<no data>";

    public static AtResponse Failure(string error) =>
        new(false, Array.Empty<string>(), string.Empty, error);
}
