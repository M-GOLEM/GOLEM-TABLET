using Golem.Tablet.Core.Firmware;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class QualcommFirmwareInspectorTests : IDisposable
{
    private readonly string _directory;

    public QualcommFirmwareInspectorTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "golem-edl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Complete_folder_is_accepted()
    {
        Write("rawprogram0.xml",
            "<data><program label=\"boot\" filename=\"boot.img\" /><program label=\"system\" filename=\"system.img\" /></data>");
        Write("patch0.xml", "<patches />");
        Write("prog_firehose_ddr.elf", "binary");

        FirmwareInspection inspection = QualcommFirmwareInspector.Inspect(_directory);

        Assert.True(inspection.IsValid);
        Assert.Equal("Qualcomm EDL firmware folder", inspection.FormatLabel);
        Assert.Equal(new[] { "boot", "system" }, inspection.Entries);
        Assert.Contains(inspection.Issues, i => i.Message.Contains("Programmer: prog_firehose_ddr.elf"));
    }

    [Fact]
    public void Missing_rawprogram_is_an_error()
    {
        Write("patch0.xml", "<patches />");

        FirmwareInspection inspection = QualcommFirmwareInspector.Inspect(_directory);

        Assert.False(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message.Contains("No rawprogram"));
    }

    [Fact]
    public void Missing_programmer_warns()
    {
        Write("rawprogram0.xml", "<data><program label=\"boot\" filename=\"boot.img\" /></data>");

        FirmwareInspection inspection = QualcommFirmwareInspector.Inspect(_directory);

        Assert.True(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Severity == FirmwareSeverity.Warning
                                                 && i.Message.Contains("firehose programmer"));
    }

    [Fact]
    public void Invalid_xml_is_reported()
    {
        Write("rawprogram0.xml", "<data><program label=\"boot\"></data>");

        FirmwareInspection inspection = QualcommFirmwareInspector.Inspect(_directory);

        Assert.False(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message.Contains("not valid XML"));
    }

    [Fact]
    public void Missing_folder_is_rejected()
    {
        FirmwareInspection inspection = QualcommFirmwareInspector.Inspect(
            Path.Combine(_directory, "does-not-exist"));

        Assert.False(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message == "Folder does not exist.");
    }

    private void Write(string name, string content) =>
        File.WriteAllText(Path.Combine(_directory, name), content);
}
