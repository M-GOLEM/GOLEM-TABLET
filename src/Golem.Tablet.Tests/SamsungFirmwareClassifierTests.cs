using Golem.Tablet.Core.Firmware;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class SamsungFirmwareClassifierTests
{
    [Theory]
    [InlineData("AP_X216BXXU3AXH2_CL26406874_QB84940144_REV00_user_low_ship_MULTI_CERT_meta_OS14.tar.md5", "AP")]
    [InlineData("BL_X216BXXU3AXH2_CL26406874_QB84940144_REV00_user_low_ship_MULTI_CERT.tar.md5", "BL")]
    [InlineData("CP_X216BXXU3AXH2_CP26169366_QB84933089_REV00_user_low_ship_MULTI_CERT.tar.md5", "CP")]
    [InlineData("CSC_OXM_X216BOXM3AXH2_CL26406874_QB84940144_REV00_user_low_ship_MULTI_CERT.tar.md5", "CSC")]
    [InlineData("HOME_CSC_OXM_X216BOXM3AXH2_CL26406874_QB84940144_REV00_user_low_ship_MULTI_CERT.tar.md5", "HOME CSC")]
    [InlineData("dm3q.pit", "PIT")]
    [InlineData("USERDATA_X216BXXU3AXH2_CL26406874_QB84940144_REV00_user_low_ship_MULTI_CERT.tar.md5", "USERDATA")]
    [InlineData("boot.img", "Other")]
    public void EntriesAreClassified(string entry, string expected) =>
        Assert.Equal(expected, SamsungFirmwareClassifier.Classify(entry));

    [Fact]
    public void ClassificationLooksAtTheFileNameOnly()
    {
        Assert.Equal("AP", SamsungFirmwareClassifier.Classify("folder/AP_X216.tar.md5"));
    }

    [Fact]
    public void SummaryCountsEachComponent()
    {
        string summary = SamsungFirmwareClassifier.Summarize(new[]
        {
            "AP_a.tar.md5", "BL_b.tar.md5", "CP_c.tar.md5", "HOME_CSC_d.tar.md5", "dm3q.pit", "boot.img"
        });

        Assert.Equal("AP 1 · BL 1 · CP 1 · HOME CSC 1 · PIT 1 · Other 1", summary);
    }

    [Fact]
    public void EmptySummaryIsExplicit() =>
        Assert.Equal("No entries", SamsungFirmwareClassifier.Summarize(Array.Empty<string>()));

    [Fact]
    public void TokenMustBeDelimited()
    {
        Assert.Equal("Other", SamsungFirmwareClassifier.Classify("MAP_data.bin"));
        Assert.Equal("Other", SamsungFirmwareClassifier.Classify("BOOTLOADER.bin"));
    }
}
