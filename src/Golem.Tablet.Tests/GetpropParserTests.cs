using Golem.Tablet.Core.Adb;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class GetpropParserTests
{
    private const string Sample =
        "[ro.product.model]: [SM-X216B]\r\n" +
        "[ro.product.manufacturer]: [samsung]\r\n" +
        "[ro.build.version.release]: [14]\r\n" +
        "[ro.build.version.sdk]: [34]\r\n" +
        "[ro.build.version.security_patch]: [2024-08-01]\r\n" +
        "[ro.build.fingerprint]: [samsung/dm3qxxx/dm3q:14/UP1A.231005.007/X216BXXU3AXH2:user/release-keys]\r\n" +
        "[ro.bootloader]: [X216BXXU3AXH2]\r\n" +
        "[ro.product.cpu.abi]: [arm64-v8a]\r\n" +
        "[persist.sys.timezone]: [Asia/Riyadh]\r\n";

    [Fact]
    public void PropertiesAreParsed()
    {
        IReadOnlyDictionary<string, string> properties = GetpropParser.Parse(Sample);

        Assert.Equal("SM-X216B", properties["ro.product.model"]);
        Assert.Equal("Asia/Riyadh", properties["persist.sys.timezone"]);
        Assert.Equal(9, properties.Count);
    }

    [Fact]
    public void ProfileMapsTheExpectedKeys()
    {
        DeviceProfile profile = GetpropParser.Describe(Sample);

        Assert.Equal("SM-X216B", profile.Model);
        Assert.Equal("samsung", profile.Manufacturer);
        Assert.Equal("14", profile.AndroidVersion);
        Assert.Equal("34", profile.Sdk);
        Assert.Equal("X216BXXU3AXH2", profile.Bootloader);
        Assert.Equal("arm64-v8a", profile.Abi);
        Assert.False(profile.IsEmpty);
        Assert.Contains("SM-X216B", profile.Summary);
    }

    [Fact]
    public void ProfileTextSkipsMissingFields()
    {
        DeviceProfile profile = GetpropParser.Describe("[ro.product.model]: [T505]\n");

        string text = profile.ToText();

        Assert.Contains("Model: T505", text);
        Assert.DoesNotContain("Bootloader:", text);
        Assert.DoesNotContain("Build:", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("[key]: []")]
    [InlineData("[broken")]
    public void EmptyOrMalformedInputYieldsNoProperties(string output)
    {
        DeviceProfile profile = GetpropParser.Describe(output);

        Assert.True(profile.IsEmpty);
        Assert.Equal("Unknown device", profile.Summary);
    }

    [Fact]
    public void VendorFallbackKeysAreUsed()
    {
        DeviceProfile profile = GetpropParser.Describe(
            "[ro.product.vendor.model]: [A9]\n[ro.product.vendor.manufacturer]: [Example]\n");

        Assert.Equal("A9", profile.Model);
        Assert.Equal("Example", profile.Manufacturer);
    }
}
