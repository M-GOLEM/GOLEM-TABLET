namespace Golem.Tablet.Core.Devices;

/// <summary>
/// Pure, table-driven classification of USB devices. Kept free of I/O so it can
/// be unit-tested against synthetic PnP records.
/// </summary>
public static class DeviceClassifier
{
    private const ushort SamsungVid = 0x04E8;
    private const ushort QualcommVid = 0x05C6;
    private const ushort MediaTekVid = 0x0E8D;
    private const ushort SpreadtrumVid = 0x1782;
    private const ushort GoogleVid = 0x18D1;

    private const ushort QualcommEdlPid = 0x9008;
    private const ushort MediaTekPreloaderPid = 0x2000;

    public static DeviceInfo Classify(UsbDeviceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        DeviceFamily family = FamilyFromVid(record.VendorId);
        ConnectionMode mode = ModeFor(record, family);
        return new DeviceInfo(record, family, mode);
    }

    private static DeviceFamily FamilyFromVid(ushort? vid) => vid switch
    {
        SamsungVid => DeviceFamily.Samsung,
        QualcommVid => DeviceFamily.Qualcomm,
        MediaTekVid => DeviceFamily.MediaTek,
        SpreadtrumVid => DeviceFamily.Spreadtrum,
        GoogleVid => DeviceFamily.GenericAndroid,
        _ => DeviceFamily.Unknown
    };

    private static ConnectionMode ModeFor(UsbDeviceRecord record, DeviceFamily family)
    {
        string name = record.Name;
        string service = record.Service ?? string.Empty;

        if (Contains(name, "bootloader interface") || Contains(name, "fastboot"))
        {
            return ConnectionMode.Fastboot;
        }

        if (IsAdbInterface(name, service))
        {
            return ConnectionMode.Adb;
        }

        if (IsMassStorage(name, service))
        {
            return ConnectionMode.MassStorage;
        }

        return family switch
        {
            DeviceFamily.Samsung => SamsungMode(record, name, service),
            DeviceFamily.Qualcomm => QualcommMode(record, name, service),
            DeviceFamily.MediaTek => MediaTekMode(record, name, service),
            DeviceFamily.Spreadtrum => SpreadtrumMode(name, service),
            _ => ConnectionMode.Unknown
        };
    }

    private static ConnectionMode SamsungMode(UsbDeviceRecord record, string name, string service)
    {
        if (service.StartsWith("ssud", StringComparison.OrdinalIgnoreCase) ||
            Contains(name, "ssud") ||
            Contains(name, "download mode") ||
            record.ProductId == 0x685D)
        {
            return ConnectionMode.OdinDownload;
        }

        return Contains(name, "modem") || Contains(service, "modem")
            ? ConnectionMode.SerialModem
            : ConnectionMode.Unknown;
    }

    private static ConnectionMode QualcommMode(UsbDeviceRecord record, string name, string service)
    {
        if (record.ProductId == QualcommEdlPid)
        {
            return ConnectionMode.QualcommEdl;
        }

        return Contains(name, "diagnostic") || Contains(name, "modem") || Contains(service, "diag")
            ? ConnectionMode.SerialModem
            : ConnectionMode.Unknown;
    }

    private static ConnectionMode MediaTekMode(UsbDeviceRecord record, string name, string service)
    {
        if (Contains(name, "preloader") || record.ProductId == MediaTekPreloaderPid)
        {
            return ConnectionMode.MediaTekPreloader;
        }

        return Contains(name, "modem") || Contains(service, "modem") ||
               Contains(name, "at port") || Contains(service, "mdm")
            ? ConnectionMode.SerialModem
            : ConnectionMode.Unknown;
    }

    private static ConnectionMode SpreadtrumMode(string name, string service)
    {
        return Contains(name, "diagnostic") || Contains(name, "modem") ||
               Contains(service, "diag") || Contains(service, "modem")
            ? ConnectionMode.SerialModem
            : ConnectionMode.Unknown;
    }

    private static bool IsAdbInterface(string name, string service) =>
        Contains(name, "adb interface") ||
        Contains(name, "android composite") ||
        string.Equals(service, "adb", StringComparison.OrdinalIgnoreCase) ||
        (Contains(service, "winusb") && Contains(name, "adb"));

    private static bool IsMassStorage(string name, string service) =>
        Contains(service, "usbstor") ||
        Contains(name, "mass storage") ||
        Contains(name, "mtp");

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
