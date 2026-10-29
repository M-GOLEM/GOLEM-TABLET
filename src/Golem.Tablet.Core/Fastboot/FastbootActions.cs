namespace Golem.Tablet.Core.Fastboot;

/// <summary>Builds fastboot argument lists. Arguments are never passed through a shell.</summary>
public static class FastbootActions
{
    public static readonly string[] RebootTargets = { "system", "bootloader", "recovery", "fastbootd" };

    public static bool IsValidRebootTarget(string? target) =>
        target is "system" or "bootloader" or "recovery" or "fastbootd";

    public static IReadOnlyList<string> DevicesArguments() => new[] { "devices" };

    public static IReadOnlyList<string> VersionArguments() => new[] { "--version" };

    public static IReadOnlyList<string> GetvarAllArguments(string? serial) =>
        WithDevice(serial, new[] { "getvar", "all" });

    public static IReadOnlyList<string> RebootArguments(string target, string? serial)
    {
        if (!IsValidRebootTarget(target))
        {
            throw new ArgumentException("Unsupported reboot target.", nameof(target));
        }

        return target == "system"
            ? WithDevice(serial, new[] { "reboot" })
            : WithDevice(serial, new[] { "reboot", target });
    }

    public static IReadOnlyList<string> ContinueArguments(string? serial) =>
        WithDevice(serial, new[] { "continue" });

    public static IReadOnlyList<string> WithDevice(string? serial, IReadOnlyList<string> arguments)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return arguments.ToArray();
        }

        var list = new List<string> { "-s", serial.Trim() };
        list.AddRange(arguments);
        return list;
    }
}

public sealed record FastbootDevice(string Serial, string State)
{
    public bool IsOnline => State is "fastboot" or "fastbootd";

    public string Summary => Serial + " · " + State;
}

public sealed record FastbootVariable(string Name, string Value);

/// <summary>Parses fastboot console output: device lists and <c>getvar all</c> key/value lines.</summary>
public static class FastbootOutputParser
{
    public static IReadOnlyList<FastbootDevice> ParseDevices(string? output)
    {
        var devices = new List<FastbootDevice>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return devices;
        }

        foreach (string raw in output.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 ||
                line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("fastboot:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith('?') ||
                line.StartsWith('<') ||
                line.StartsWith('*'))
            {
                continue;
            }

            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string serial = parts[0];
            string state = parts[1];
            if (serial.Length == 0 || state.Length == 0)
            {
                continue;
            }

            devices.Add(new FastbootDevice(serial, state));
        }

        return devices;
    }

    public static IReadOnlyList<FastbootVariable> ParseVariables(string? output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<FastbootVariable>();
        }

        foreach (string raw in output.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            const string prefix = "(bootloader)";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                line = line[prefix.Length..].Trim();
            }

            if (line.Length == 0 ||
                line.StartsWith("finished.", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("getvar:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("fastboot:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("fastbootd:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            string name = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            if (name.Length == 0 || name.Contains(' '))
            {
                continue;
            }

            values[name] = value;
        }

        return values
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new FastbootVariable(pair.Key, pair.Value))
            .ToList();
    }
}
