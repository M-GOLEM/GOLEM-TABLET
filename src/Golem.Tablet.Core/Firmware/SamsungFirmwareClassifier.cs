namespace Golem.Tablet.Core.Firmware;

/// <summary>Classifies TAR entries by their Samsung package component (AP, BL, CP, CSC, …).</summary>
public static class SamsungFirmwareClassifier
{
    public static readonly string[] KindOrder =
        { "AP", "BL", "CP", "CSC", "HOME CSC", "USERDATA", "PIT", "Other" };

    public static string Classify(string entry)
    {
        string name = Path.GetFileName(entry).ToUpperInvariant();
        if (name.Length == 0)
        {
            return "Other";
        }

        if (name.EndsWith(".PIT", StringComparison.Ordinal))
        {
            return "PIT";
        }

        if (name.Contains("HOME_CSC", StringComparison.Ordinal))
        {
            return "HOME CSC";
        }

        if (HasToken(name, "CSC"))
        {
            return "CSC";
        }

        if (HasToken(name, "AP"))
        {
            return "AP";
        }

        if (HasToken(name, "BL"))
        {
            return "BL";
        }

        if (HasToken(name, "CP"))
        {
            return "CP";
        }

        if (HasToken(name, "USERDATA"))
        {
            return "USERDATA";
        }

        return "Other";
    }

    public static string Summarize(IEnumerable<string> entries)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        int total = 0;
        foreach (string entry in entries)
        {
            string kind = Classify(entry);
            counts[kind] = counts.TryGetValue(kind, out int current) ? current + 1 : 1;
            total++;
        }

        if (total == 0)
        {
            return "No entries";
        }

        IEnumerable<string> parts = KindOrder
            .Where(kind => counts.ContainsKey(kind))
            .Select(kind => kind + " " + counts[kind]);

        return string.Join(" · ", parts);
    }

    private static bool HasToken(string name, string token)
    {
        int index = 0;
        while (index < name.Length)
        {
            int found = name.IndexOf(token, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            bool startOk = found == 0 || IsSeparator(name[found - 1]);
            int after = found + token.Length;
            bool endOk = after == name.Length || IsSeparator(name[after]);
            if (startOk && endOk)
            {
                return true;
            }

            index = found + 1;
        }

        return false;
    }

    private static bool IsSeparator(char value) => value is '_' or '.' or '-' or ' ';
}
