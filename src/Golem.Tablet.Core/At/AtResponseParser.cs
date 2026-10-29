namespace Golem.Tablet.Core.At;

/// <summary>Parses modem AT responses: strips echo/terminators, detects result codes.</summary>
public static class AtResponseParser
{
    public static AtResponse Parse(string? rawText, string? command = null)
    {
        string raw = rawText ?? string.Empty;
        string[] allLines = raw
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        var body = new List<string>();
        bool ok = false;
        string? error = null;

        foreach (string line in allLines)
        {
            if (command is not null && string.Equals(line, command, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(line, "OK", StringComparison.OrdinalIgnoreCase))
            {
                ok = true;
                continue;
            }

            if (line.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("+CME ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("+CMS ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("NO CARRIER", StringComparison.OrdinalIgnoreCase))
            {
                error = line;
                continue;
            }

            body.Add(line);
        }

        bool succeeded = ok && error is null;
        if (!succeeded && error is null)
        {
            error = body.Count == 0 ? "No response from the modem." : "Response did not end with OK.";
        }

        return new AtResponse(succeeded, body, raw, error);
    }
}
