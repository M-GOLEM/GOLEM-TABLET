namespace Golem.Tablet.Core.Devices;

public enum DeviceFamily
{
    Unknown,
    GenericAndroid,
    Samsung,
    Qualcomm,
    MediaTek,
    Spreadtrum
}

public enum ConnectionMode
{
    Unknown,
    Adb,
    Fastboot,
    OdinDownload,
    QualcommEdl,
    MediaTekPreloader,
    SerialModem,
    MassStorage
}
