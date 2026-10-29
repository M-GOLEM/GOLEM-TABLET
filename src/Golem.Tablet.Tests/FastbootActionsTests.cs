using Golem.Tablet.Core.Fastboot;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class FastbootActionsTests
{
    [Fact]
    public void DeviceArgumentsHaveNoSerialWhenNoneIsSelected()
    {
        Assert.Equal(new[] { "devices" }, FastbootActions.DevicesArguments());
        Assert.Equal(new[] { "getvar", "all" }, FastbootActions.GetvarAllArguments(null));
        Assert.Equal(new[] { "continue" }, FastbootActions.ContinueArguments(" "));
    }

    [Fact]
    public void SerialIsPlacedBeforeTheCommand()
    {
        Assert.Equal(
            new[] { "-s", "R5CT30ABCDE", "getvar", "all" },
            FastbootActions.GetvarAllArguments("R5CT30ABCDE"));
        Assert.Equal(
            new[] { "-s", "R5CT30ABCDE", "continue" },
            FastbootActions.ContinueArguments("R5CT30ABCDE"));
    }

    [Fact]
    public void SystemRebootHasNoTargetArgument()
    {
        Assert.Equal(new[] { "reboot" }, FastbootActions.RebootArguments("system", null));
        Assert.Equal(
            new[] { "-s", "R5CT30ABCDE", "reboot", "bootloader" },
            FastbootActions.RebootArguments("bootloader", "R5CT30ABCDE"));
        Assert.Equal(
            new[] { "reboot", "fastbootd" },
            FastbootActions.RebootArguments("fastbootd", null));
    }

    [Theory]
    [InlineData("flash")]
    [InlineData("erase")]
    [InlineData("")]
    [InlineData("BOOTLOADER")]
    public void UnsupportedRebootTargetsAreRejected(string target) =>
        Assert.Throws<ArgumentException>(() => FastbootActions.RebootArguments(target, null));

    [Fact]
    public void DeviceListIsParsedFromFastbootOutput()
    {
        const string output =
            "List of devices attached\r\n" +
            "R5CT30ABCDE\tfastboot\r\n" +
            "emulator-5554\tfastbootd\r\n" +
            "\r\n";

        IReadOnlyList<FastbootDevice> devices = FastbootOutputParser.ParseDevices(output);

        Assert.Equal(2, devices.Count);
        Assert.Equal("R5CT30ABCDE", devices[0].Serial);
        Assert.Equal("fastboot", devices[0].State);
        Assert.True(devices[0].IsOnline);
        Assert.True(devices[1].IsOnline);
    }

    [Theory]
    [InlineData("")]
    [InlineData("< waiting for any device >")]
    [InlineData("fastboot: error: no devices found")]
    public void DeviceListIgnoresNoiseLines(string output) =>
        Assert.Empty(FastbootOutputParser.ParseDevices(output));

    [Fact]
    public void GetvarOutputIsParsedFromStdoutAndStderr()
    {
        const string output =
            "(bootloader) serialno: R5CT30ABCDE\r\n" +
            "(bootloader) product: dm3q\r\n" +
            "(bootloader) secure: yes\r\n" +
            "(bootloader) battery-soc-ok: yes\r\n" +
            "getvar:all FAILED (remote: unknown command)\r\n" +
            "finished. total time: 0.031s\r\n";

        IReadOnlyList<FastbootVariable> variables = FastbootOutputParser.ParseVariables(output);

        Assert.Equal(4, variables.Count);
        Assert.Contains(new FastbootVariable("serialno", "R5CT30ABCDE"), variables);
        Assert.Contains(new FastbootVariable("secure", "yes"), variables);
        Assert.DoesNotContain(variables, v => v.Name.StartsWith("getvar", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(variables, v => v.Name.StartsWith("finished", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetvarValuesWithColonsAreKept()
    {
        IReadOnlyList<FastbootVariable> variables =
            FastbootOutputParser.ParseVariables("(bootloader) version-baseband: X216BXXU3AXH2CP\r\n");

        Assert.Single(variables);
        Assert.Equal("X216BXXU3AXH2CP", variables[0].Value);
    }

    [Fact]
    public void GetvarOutputIsSortedByName()
    {
        IReadOnlyList<FastbootVariable> variables =
            FastbootOutputParser.ParseVariables("(bootloader) product: dm3q\n(bootloader) battery-soc-ok: yes\n");

        Assert.Equal(new[] { "battery-soc-ok", "product" }, variables.Select(v => v.Name));
    }
}
