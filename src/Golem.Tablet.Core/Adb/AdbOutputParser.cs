namespace Golem.Tablet.Core.Adb;

/// <summary>Parses the textual output of the adb client.</summary>
public static class AdbOutputParser
{
    private static readonly string[] HeaderMarkers =
    {
        "List of devices attached",
        "* daemon",
        "* server"
    };

    /// <summary>Parses <c>adb devices -l</c> output.</summary>
    public static IReadOnlyList<AdbDevice> ParseDevices(string? output)
    {
        var devices = new List<AdbDevice>();
        if (string.IsNullOrWhiteSpace(output))
        {
            return devices;
        }

        foreach (string rawLine in output.Split('\n'))
        {
            string line = rawLine.Trim('\r', ' ', '\t');
            if (line.Length == 0)
            {
                continue;
            }

            if (HeaderMarkers.Any(marker => line.StartsWith(marker, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            int permissionIndex = line.IndexOf("no permissions", StringComparison.OrdinalIgnoreCase);
            if (permissionIndex >= 0)
            {
                string serial = line[..permissionIndex].Trim();
                if (serial.Length > 0 && !serial.Contains(':'))
                {
                    devices.Add(new AdbDevice(serial, "no permissions"));
                }

                continue;
            }

            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            string deviceSerial = parts[0];
            string state = parts[1];
            string? product = null;
            string? model = null;
            string? device = null;
            string? transportId = null;

            for (int i = 2; i < parts.Length; i++)
            {
                int separator = parts[i].IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string key = parts[i][..separator];
                string value = parts[i][(separator + 1)..];
                switch (key)
                {
                    case "product":
                        product = value;
                        break;
                    case "model":
                        model = value.Replace('_', ' ');
                        break;
                    case "device":
                        device = value;
                        break;
                    case "transport_id":
                        transportId = value;
                        break;
                }
            }

            devices.Add(new AdbDevice(deviceSerial, state, product, model, device, transportId));
        }

        return devices;
    }

    /// <summary>Trims trailing blank lines from a shell response.</summary>
    public static string NormalizeShellOutput(string? output) =>
        (output ?? string.Empty).Replace("\r\n", "\n").Trim('\n', '\r', ' ');
}
