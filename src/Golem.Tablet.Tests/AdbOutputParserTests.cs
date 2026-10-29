using Golem.Tablet.Core.Adb;
using Xunit;

namespace Golem.Tablet.Tests;

public class AdbOutputParserTests
{
    private const string Sample = "List of devices attached\r\n" +
                                  "R58M1234ABC            device product:beyond1 model:SM_G973F device:beyond1 transport_id:1\r\n" +
                                  "emulator-5554          offline\r\n" +
                                  "0123456789ABCDEF       no permissions (user in plugdev group)\r\n" +
                                  "\r\n";

    [Fact]
    public void Parses_devices_with_properties()
    {
        IReadOnlyList<AdbDevice> devices = AdbOutputParser.ParseDevices(Sample);

        Assert.Equal(3, devices.Count);
        Assert.Equal("R58M1234ABC", devices[0].Serial);
        Assert.Equal("device", devices[0].State);
        Assert.Equal("beyond1", devices[0].Product);
        Assert.Equal("SM G973F", devices[0].Model);
        Assert.Equal("1", devices[0].TransportId);
    }

    [Fact]
    public void Parses_offline_state()
    {
        IReadOnlyList<AdbDevice> devices = AdbOutputParser.ParseDevices(Sample);

        Assert.Equal("offline", devices[1].State);
    }

    [Fact]
    public void Reports_devices_without_permissions()
    {
        IReadOnlyList<AdbDevice> devices = AdbOutputParser.ParseDevices(Sample);

        Assert.Equal("0123456789ABCDEF", devices[2].Serial);
        Assert.Equal("no permissions", devices[2].State);
    }

    [Fact]
    public void Ignores_daemon_startup_lines()
    {
        const string output = "* daemon not running; starting now at tcp:5037\n" +
                              "* daemon started successfully\n" +
                              "List of devices attached\n" +
                              "ABC123\tdevice\n";

        IReadOnlyList<AdbDevice> devices = AdbOutputParser.ParseDevices(output);

        AdbDevice device = Assert.Single(devices);
        Assert.Equal("ABC123", device.Serial);
    }

    [Fact]
    public void Empty_or_null_output_yields_no_devices()
    {
        Assert.Empty(AdbOutputParser.ParseDevices(null));
        Assert.Empty(AdbOutputParser.ParseDevices(""));
        Assert.Empty(AdbOutputParser.ParseDevices("List of devices attached"));
    }

    [Fact]
    public void NormalizeShellOutput_trims_blank_lines_and_crlf()
    {
        Assert.Equal("hello\nworld", AdbOutputParser.NormalizeShellOutput("hello\r\nworld\r\n\r\n"));
        Assert.Equal(string.Empty, AdbOutputParser.NormalizeShellOutput(null));
    }
}
