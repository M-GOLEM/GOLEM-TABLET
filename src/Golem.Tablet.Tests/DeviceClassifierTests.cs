using Golem.Tablet.Core.Devices;
using Xunit;

namespace Golem.Tablet.Tests;

public class DeviceClassifierTests
{
    private static DeviceInfo Classify(string name, string pnpDeviceId, string? service = null) =>
        DeviceClassifier.Classify(new UsbDeviceRecord(name, pnpDeviceId, service));

    [Fact]
    public void Samsung_download_mode_is_recognised()
    {
        DeviceInfo info = Classify("SAMSUNG Mobile USB CDC Composite Device",
            @"USB\VID_04E8&PID_685D\R58M1234ABC", "ssudbus");

        Assert.Equal(DeviceFamily.Samsung, info.Family);
        Assert.Equal(ConnectionMode.OdinDownload, info.Mode);
    }

    [Fact]
    public void Samsung_modem_is_recognised()
    {
        DeviceInfo info = Classify("SAMSUNG Mobile USB Modem #2",
            @"USB\VID_04E8&PID_6860\R58M1234ABC", "ss_conn_usb_driver");

        Assert.Equal(DeviceFamily.Samsung, info.Family);
        Assert.Equal(ConnectionMode.SerialModem, info.Mode);
    }

    [Fact]
    public void Qualcomm_edl_is_recognised()
    {
        DeviceInfo info = Classify("Qualcomm HS-USB QDLoader 9008",
            @"USB\VID_05C6&PID_9008\6&1A2B3C4D&0", "qcusbser");

        Assert.Equal(DeviceFamily.Qualcomm, info.Family);
        Assert.Equal(ConnectionMode.QualcommEdl, info.Mode);
    }

    [Fact]
    public void MediaTek_preloader_is_recognised()
    {
        DeviceInfo info = Classify("MediaTek PreLoader USB VCOM (Android)",
            @"USB\VID_0E8D&PID_2000\5&2B3C4D5E&0", "usbser");

        Assert.Equal(DeviceFamily.MediaTek, info.Family);
        Assert.Equal(ConnectionMode.MediaTekPreloader, info.Mode);
    }

    [Fact]
    public void Spreadtrum_diagnostic_port_is_recognised()
    {
        DeviceInfo info = Classify("SPRD U2S Diag",
            @"USB\VID_1782&PID_4D00\ABCDEF", "sprd_diag");

        Assert.Equal(DeviceFamily.Spreadtrum, info.Family);
        Assert.Equal(ConnectionMode.SerialModem, info.Mode);
    }

    [Fact]
    public void Adb_interface_wins_over_family()
    {
        DeviceInfo info = Classify("Android ADB Interface",
            @"USB\VID_18D1&PID_4EE7\SERIAL1", "winusb");

        Assert.Equal(DeviceFamily.GenericAndroid, info.Family);
        Assert.Equal(ConnectionMode.Adb, info.Mode);
    }

    [Fact]
    public void Fastboot_mode_is_recognised()
    {
        DeviceInfo info = Classify("Android Bootloader Interface",
            @"USB\VID_18D1&PID_4EE0\SERIAL1");

        Assert.Equal(ConnectionMode.Fastboot, info.Mode);
    }

    [Fact]
    public void Mass_storage_is_recognised()
    {
        DeviceInfo info = Classify("SAMSUNG MTP Device",
            @"USB\VID_04E8&PID_6860\R58M1234ABC");

        Assert.Equal(ConnectionMode.MassStorage, info.Mode);
    }

    [Fact]
    public void Unknown_vendor_stays_unknown()
    {
        DeviceInfo info = Classify("Generic USB Hub", @"USB\VID_1234&PID_0001\XYZ");

        Assert.Equal(DeviceFamily.Unknown, info.Family);
        Assert.Equal(ConnectionMode.Unknown, info.Mode);
    }

    [Fact]
    public void Record_parses_ids_and_serial()
    {
        var record = new UsbDeviceRecord("Device", @"USB\VID_04E8&PID_685D\R58M1234ABC");

        Assert.Equal((ushort?)0x04E8, record.VendorId);
        Assert.Equal((ushort?)0x685D, record.ProductId);
        Assert.Equal("VID_04E8&PID_685D", record.VidPidText);
        Assert.Equal("R58M1234ABC", record.Serial);
    }

    [Fact]
    public void Record_without_ids_reports_placeholders()
    {
        var record = new UsbDeviceRecord("Device", @"USB\ROOT_HUB30\4&2B3C");

        Assert.Null(record.VendorId);
        Assert.Equal("VID_----&PID_----", record.VidPidText);
    }
}
