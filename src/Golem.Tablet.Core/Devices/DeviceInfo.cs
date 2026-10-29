namespace Golem.Tablet.Core.Devices;

public sealed class DeviceInfo
{
    public DeviceInfo(UsbDeviceRecord record, DeviceFamily family, ConnectionMode mode)
    {
        Record = record;
        Family = family;
        Mode = mode;
    }

    public UsbDeviceRecord Record { get; }

    public DeviceFamily Family { get; }

    public ConnectionMode Mode { get; }

    public string Name => Record.Name;

    public string VidPid => Record.VidPidText;

    public string Serial => Record.Serial ?? "—";

    public string Service => string.IsNullOrWhiteSpace(Record.Service) ? "—" : Record.Service!;

    public string FamilyText => Family switch
    {
        DeviceFamily.GenericAndroid => "Android",
        DeviceFamily.Samsung => "Samsung",
        DeviceFamily.Qualcomm => "Qualcomm",
        DeviceFamily.MediaTek => "MediaTek",
        DeviceFamily.Spreadtrum => "Spreadtrum",
        _ => "Unknown"
    };

    public string ModeText => Mode switch
    {
        ConnectionMode.Adb => "ADB",
        ConnectionMode.Fastboot => "Fastboot",
        ConnectionMode.OdinDownload => "Odin download",
        ConnectionMode.QualcommEdl => "Qualcomm EDL",
        ConnectionMode.MediaTekPreloader => "MediaTek preloader",
        ConnectionMode.SerialModem => "Serial / AT",
        ConnectionMode.MassStorage => "Mass storage",
        _ => "Unknown"
    };

    public bool IsUsable => Mode != ConnectionMode.Unknown;
}
