using System.Management;

namespace Golem.Tablet.Core.Devices;

/// <summary>Enumerates USB PnP entities through WMI (Win32_PnPEntity).</summary>
public sealed class WindowsDeviceEnumerator : IDeviceEnumerator
{
    private const string Query =
        "SELECT Name, PNPDeviceID, Service, Status, ConfigManagerErrorCode FROM Win32_PnPEntity";

    public IReadOnlyList<UsbDeviceRecord> Enumerate()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<UsbDeviceRecord>();
        }

        var records = new List<UsbDeviceRecord>();
        using var searcher = new ManagementObjectSearcher(Query);
        using ManagementObjectCollection results = searcher.Get();

        foreach (ManagementBaseObject item in results)
        {
            using (item)
            {
                string? pnpDeviceId = item["PNPDeviceID"] as string;
                if (string.IsNullOrEmpty(pnpDeviceId) || !IsUsbPnpId(pnpDeviceId))
                {
                    continue;
                }

                string name = item["Name"] as string ?? "(unnamed device)";
                string? service = item["Service"] as string;
                string? status = item["Status"] as string;
                uint? problemCode = ToUInt32(item["ConfigManagerErrorCode"]);

                records.Add(new UsbDeviceRecord(name, pnpDeviceId, service, status, problemCode));
            }
        }

        return records;
    }

    private static bool IsUsbPnpId(string pnpDeviceId) =>
        pnpDeviceId.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase) ||
        pnpDeviceId.StartsWith("USBSTOR\\", StringComparison.OrdinalIgnoreCase);

    private static uint? ToUInt32(object? value) => value switch
    {
        uint u => u,
        int i => (uint)i,
        ushort us => us,
        _ => null
    };
}
