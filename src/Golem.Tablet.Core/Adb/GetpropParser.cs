namespace Golem.Tablet.Core.Adb;

/// <summary>A parsed subset of <c>getprop</c> describing the connected device.</summary>
public sealed record DeviceProfile(
    string Model,
    string Manufacturer,
    string AndroidVersion,
    string Sdk,
    string BuildFingerprint,
    string SecurityPatch,
    string Bootloader,
    string Abi)
{
    public bool IsEmpty =>
        Model.Length == 0 && Manufacturer.Length == 0 && AndroidVersion.Length == 0 &&
        BuildFingerprint.Length == 0 && Bootloader.Length == 0;

    public string Summary => Model.Length == 0
        ? "Unknown device"
        : $"{Manufacturer} {Model} · Android {AndroidVersion} (SDK {Sdk})".Replace("  ", " ");

    public string ToText()
    {
        var lines = new List<string>();
        Add("Model", Model);
        Add("Manufacturer", Manufacturer);
        Add("Android", AndroidVersion.Length == 0 ? string.Empty : $"{AndroidVersion} (SDK {Sdk})");
        Add("Build", BuildFingerprint);
        Add("Security patch", SecurityPatch);
        Add("Bootloader", Bootloader);
        Add("ABI", Abi);

        if (lines.Count == 0)
        {
            lines.Add("No device properties were returned.");
        }

        return string.Join(Environment.NewLine, lines);

        void Add(string label, string value)
        {
            if (value.Length > 0)
            {
                lines.Add(label + ": " + value);
            }
        }
    }
}

/// <summary>Parses <c>getprop</c> output lines of the form <c>[key]: [value]</c>.</summary>
public static class GetpropParser
{
    public static IReadOnlyDictionary<string, string> Parse(string? output)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(output))
        {
            return properties;
        }

        foreach (string raw in output.Replace("\r\n", "\n").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length < 5 || line[0] != '[')
            {
                continue;
            }

            int keyEnd = line.IndexOf("]: [", StringComparison.Ordinal);
            if (keyEnd <= 1)
            {
                continue;
            }

            int valueStart = keyEnd + 4;
            int valueEnd = line.LastIndexOf(']');
            if (valueEnd < valueStart)
            {
                continue;
            }

            string key = line[1..keyEnd];
            if (key.Length == 0)
            {
                continue;
            }

            properties[key] = line[valueStart..valueEnd];
        }

        return properties;
    }

    public static DeviceProfile Describe(string? output) => FromProperties(Parse(output));

    public static DeviceProfile FromProperties(IReadOnlyDictionary<string, string> properties)
    {
        string Get(params string[] keys)
        {
            foreach (string key in keys)
            {
                if (properties.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        return new DeviceProfile(
            Get("ro.product.model", "ro.product.vendor.model"),
            Get("ro.product.manufacturer", "ro.product.vendor.manufacturer"),
            Get("ro.build.version.release"),
            Get("ro.build.version.sdk"),
            Get("ro.build.fingerprint", "ro.vendor.build.fingerprint"),
            Get("ro.build.version.security_patch"),
            Get("ro.bootloader", "ro.boot.bootloader"),
            Get("ro.product.cpu.abi"));
    }
}
