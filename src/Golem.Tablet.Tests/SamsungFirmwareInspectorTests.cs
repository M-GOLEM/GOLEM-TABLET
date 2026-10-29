using System.Security.Cryptography;
using System.Text;
using Golem.Tablet.Core.Firmware;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class SamsungFirmwareInspectorTests : IDisposable
{
    private readonly string _directory;

    public SamsungFirmwareInspectorTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "golem-tar-" + Guid.NewGuid().ToString("N"));
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
    public void Plain_tar_lists_entries()
    {
        string path = Write("AP_test.tar", BuildTar(("boot.img", Data(1024)), ("meta-data/fota.zip", Data(600))));

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: true);

        Assert.True(inspection.IsValid);
        Assert.Equal("Samsung TAR (.tar)", inspection.FormatLabel);
        Assert.Equal(new[] { "boot.img", "meta-data/fota.zip" }, inspection.Entries);
        Assert.Contains(inspection.Issues, i => i.Severity == FirmwareSeverity.Info && i.Message.Contains("2 file entries"));
    }

    [Fact]
    public void Valid_md5_trailer_passes_verification()
    {
        byte[] tar = BuildTar(("boot.img", Data(512)));
        string path = Path.Combine(_directory, "AP_test.tar.md5");
        File.WriteAllBytes(path, tar);
        File.AppendAllText(path, Convert.ToHexString(MD5.HashData(tar)).ToLowerInvariant());

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: true);

        Assert.Equal("Samsung TAR with MD5 (.tar.md5)", inspection.FormatLabel);
        Assert.True(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message == "MD5 check passed.");
    }

    [Fact]
    public void Corrupted_md5_trailer_fails_verification()
    {
        byte[] tar = BuildTar(("boot.img", Data(512)));
        string path = Path.Combine(_directory, "AP_test.tar.md5");
        File.WriteAllBytes(path, tar);
        File.AppendAllText(path, new string('0', 32));

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: true);

        Assert.False(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message.StartsWith("MD5 mismatch"));
    }

    [Fact]
    public void Md5_can_be_skipped()
    {
        byte[] tar = BuildTar(("boot.img", Data(512)));
        string path = Path.Combine(_directory, "AP_test.tar.md5");
        File.WriteAllBytes(path, tar);
        File.AppendAllText(path, Convert.ToHexString(MD5.HashData(tar)).ToLowerInvariant());

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: false);

        Assert.True(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message.Contains("verification was skipped"));
    }

    [Fact]
    public void Separator_newline_before_digest_is_accepted()
    {
        byte[] tar = BuildTar(("boot.img", Data(512)));
        string path = Path.Combine(_directory, "AP_test.tar.md5");
        File.WriteAllBytes(path, tar);
        File.AppendAllText(path, "\r\n" + Convert.ToHexString(MD5.HashData(tar)).ToLowerInvariant());

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: true);

        Assert.Equal("Samsung TAR with MD5 (.tar.md5)", inspection.FormatLabel);
        Assert.Contains(inspection.Issues, i => i.Message == "MD5 check passed.");
    }

    [Fact]
    public void Non_tar_file_is_rejected()
    {
        string path = Path.Combine(_directory, "AP_test.tar");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(new string('A', 2048)));

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: false);

        Assert.False(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message.Contains("TAR signature not found"));
    }

    [Fact]
    public void Missing_file_is_rejected()
    {
        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(
            Path.Combine(_directory, "missing.tar"), verifyChecksum: false);

        Assert.False(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Message == "File does not exist.");
    }

    [Fact]
    public void Archive_without_images_warns()
    {
        string path = Write("AP_test.tar", BuildTar(("readme.txt", Data(20))));

        FirmwareInspection inspection = SamsungFirmwareInspector.Inspect(path, verifyChecksum: false);

        Assert.True(inspection.IsValid);
        Assert.Contains(inspection.Issues, i => i.Severity == FirmwareSeverity.Warning
                                                 && i.Message.Contains("No image files"));
    }

    private string Write(string name, byte[] content)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] Data(int length)
    {
        var data = new byte[length];
        for (int i = 0; i < length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }

    private static byte[] BuildTar(params (string Name, byte[] Data)[] files)
    {
        using var stream = new MemoryStream();
        foreach ((string name, byte[] data) in files)
        {
            byte[] header = new byte[512];
            Put(header, 0, name, 100);
            Put(header, 100, "0000644", 8);
            Put(header, 108, "0000000", 8);
            Put(header, 116, "0000000", 8);
            Put(header, 124, Convert.ToString(data.Length, 8).PadLeft(11, '0'), 12);
            Put(header, 136, "00000000000", 12);
            for (int i = 148; i < 156; i++)
            {
                header[i] = (byte)' ';
            }

            header[156] = (byte)'0';
            Put(header, 257, "ustar\0", 6);
            Put(header, 263, "00", 2);

            int checksum = 0;
            foreach (byte b in header)
            {
                checksum += b;
            }

            Put(header, 148, Convert.ToString(checksum, 8).PadLeft(6, '0'), 6);
            header[154] = 0;
            header[155] = (byte)' ';

            stream.Write(header);
            stream.Write(data);
            stream.Write(new byte[(512 - (data.Length % 512)) % 512]);
        }

        stream.Write(new byte[1024]);
        return stream.ToArray();
    }

    private static void Put(byte[] target, int offset, string value, int maxLength)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, target, offset, Math.Min(bytes.Length, maxLength));
    }
}
