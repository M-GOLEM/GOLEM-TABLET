using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Golem.Tablet.Core.Firmware;

/// <summary>
/// Inspects Samsung-style firmware packages (.tar / .tar.md5) by walking TAR
/// headers and optionally verifying an appended MD5 digest. The file is streamed;
/// only headers and the digest are read.
/// </summary>
public static class SamsungFirmwareInspector
{
    private const int BlockSize = 512;
    private const int MaxEntries = 5000;

    public static FirmwareInspection Inspect(
        string path,
        bool verifyChecksum,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<FirmwareIssue>();
        var entries = new List<string>();

        if (!File.Exists(path))
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Error, "File does not exist."));
            return new FirmwareInspection(path, "Unknown", entries, issues);
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024);
        long fileLength = stream.Length;
        if (fileLength < BlockSize)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Error, "File is too small to be a TAR archive."));
            return new FirmwareInspection(path, "Unknown", entries, issues);
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".tar" or ".md5"))
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Warning,
                "Unexpected file extension; expected .tar or .tar.md5."));
        }

        byte[] firstBlock = ReadBlock(stream, 0);
        bool isTar = Matches(firstBlock, 257, "ustar");
        if (!isTar)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Error,
                "TAR signature not found at offset 257; this is not a plain TAR archive."));
            return new FirmwareInspection(path, "Unknown", entries, issues);
        }

        (long tarLength, string? trailerHash) = AnalyzeTrailer(stream, fileLength);
        string format = trailerHash is null ? "Samsung TAR (.tar)" : "Samsung TAR with MD5 (.tar.md5)";

        if (trailerHash is null && extension == ".md5")
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Warning,
                "File name suggests tar.md5 but no 32-character digest trailer was found."));
        }

        WalkEntries(stream, tarLength, entries, issues, cancellationToken);

        if (entries.Count == 0)
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Warning, "No file entries were found in the archive."));
        }
        else if (!entries.Any(IsFlashableImage))
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Warning,
                "No image files (.img, .bin, .mbn, .pit) were found in the archive."));
        }
        else
        {
            issues.Add(new FirmwareIssue(FirmwareSeverity.Info,
                $"{entries.Count} file entries found."));
        }

        if (trailerHash is not null)
        {
            if (!verifyChecksum)
            {
                issues.Add(new FirmwareIssue(FirmwareSeverity.Info,
                    "MD5 trailer present; verification was skipped."));
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                string actual = ComputeMd5(stream, tarLength);
                if (string.Equals(actual, trailerHash, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new FirmwareIssue(FirmwareSeverity.Info, "MD5 check passed."));
                }
                else
                {
                    issues.Add(new FirmwareIssue(FirmwareSeverity.Error,
                        $"MD5 mismatch: file says {trailerHash}, computed {actual}."));
                }
            }
        }

        return new FirmwareInspection(path, format, entries, issues);
    }

    private static (long TarLength, string? TrailerHash) AnalyzeTrailer(FileStream stream, long fileLength)
    {
        int tailLength = (int)Math.Min(80, fileLength);
        byte[] tail = new byte[tailLength];
        stream.Position = fileLength - tailLength;
        int read = stream.Read(tail, 0, tailLength);
        string text = Encoding.ASCII.GetString(tail, 0, read);

        int end = text.Length;
        while (end > 0 && (text[end - 1] == '\r' || text[end - 1] == '\n' || text[end - 1] == ' '))
        {
            end--;
        }

        if (end < 32)
        {
            return (fileLength, null);
        }

        string candidate = text.Substring(end - 32, 32);
        if (!candidate.All(Uri.IsHexDigit))
        {
            return (fileLength, null);
        }

        if (end - 32 > 0 && Uri.IsHexDigit(text[end - 33]))
        {
            return (fileLength, null);
        }

        long tarLength = fileLength - (tailLength - end) - 32;
        if (tarLength > 1 && tarLength % BlockSize != 0)
        {
            // Some writers put a CRLF (or LF) between the archive and the digest.
            // Only strip trailing newline bytes when that yields a block-aligned
            // archive; otherwise they belong to the archive itself.
            long aligned = tarLength;
            int stripped = 0;
            while (stripped < 2 && aligned > 0 && aligned % BlockSize != 0)
            {
                stream.Position = aligned - 1;
                int previous = stream.ReadByte();
                if (previous is not ('\n' or '\r'))
                {
                    break;
                }

                aligned--;
                stripped++;
            }

            if (aligned % BlockSize == 0)
            {
                tarLength = aligned;
            }
        }

        return tarLength > 0 ? (tarLength, candidate) : (fileLength, null);
    }

    private static void WalkEntries(
        FileStream stream,
        long tarLength,
        List<string> entries,
        List<FirmwareIssue> issues,
        CancellationToken cancellationToken)
    {
        long offset = 0;
        int guard = 0;
        string? pendingLongName = null;

        while (offset + BlockSize <= tarLength && guard++ < MaxEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] header = ReadBlock(stream, offset);
            if (IsZeroBlock(header))
            {
                break;
            }

            string name = ReadString(header, 0, 100);
            string prefix = ReadString(header, 345, 155);
            if (prefix.Length > 0)
            {
                name = prefix + "/" + name;
            }

            char typeFlag = (char)header[156];
            long size = ReadSize(header);

            if (typeFlag == 'L')
            {
                byte[] longNameBlock = ReadBlock(stream, offset + BlockSize);
                pendingLongName = ReadString(longNameBlock, 0, (int)Math.Min(size, 512)).TrimEnd('\0');
                offset += BlockSize + RoundUp(size);
                continue;
            }

            string effectiveName = pendingLongName ?? name;
            pendingLongName = null;

            if (!string.IsNullOrWhiteSpace(effectiveName))
            {
                entries.Add(effectiveName);
            }

            long dataBlocks = RoundUp(size);
            if (offset + BlockSize + dataBlocks > tarLength)
            {
                issues.Add(new FirmwareIssue(FirmwareSeverity.Error,
                    $"Entry '{effectiveName}' extends past the end of the archive."));
                break;
            }

            offset += BlockSize + dataBlocks;
        }
    }

    private static bool IsFlashableImage(string name)
    {
        string extension = Path.GetExtension(name).ToLowerInvariant();
        return extension is ".img" or ".bin" or ".mbn" or ".pit" or ".tar" or ".lz4";
    }

    private static string ComputeMd5(FileStream stream, long length)
    {
        stream.Position = 0;
        using var md5 = MD5.Create();
        byte[] buffer = new byte[64 * 1024];
        long remaining = length;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buffer.Length, remaining);
            int read = stream.Read(buffer, 0, toRead);
            if (read <= 0)
            {
                break;
            }

            md5.TransformBlock(buffer, 0, read, null, 0);
            remaining -= read;
        }

        md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(md5.Hash!).ToLowerInvariant();
    }

    private static byte[] ReadBlock(FileStream stream, long offset)
    {
        var block = new byte[BlockSize];
        stream.Position = offset;
        int total = 0;
        while (total < BlockSize)
        {
            int read = stream.Read(block, total, BlockSize - total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        return block;
    }

    private static bool IsZeroBlock(byte[] block)
    {
        foreach (byte value in block)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string ReadString(byte[] block, int offset, int length)
    {
        int end = offset;
        int limit = Math.Min(offset + length, block.Length);
        while (end < limit && block[end] != 0)
        {
            end++;
        }

        return Encoding.ASCII.GetString(block, offset, end - offset).Trim();
    }

    private static long ReadSize(byte[] header)
    {
        // GNU base-256 encoding sets the high bit of the first size byte.
        if ((header[124] & 0x80) != 0)
        {
            long value = header[124] & 0x7F;
            for (int i = 125; i < 136; i++)
            {
                value = (value << 8) | header[i];
            }

            return Math.Max(0, value);
        }

        string text = Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
        return FromOctal(text);
    }

    private static long FromOctal(string text)
    {
        long value = 0;
        foreach (char c in text)
        {
            if (c is < '0' or > '7')
            {
                break;
            }

            value = (value * 8) + (c - '0');
        }

        return value;
    }

    private static long RoundUp(long size) => (size + BlockSize - 1) / BlockSize * BlockSize;

    private static bool Matches(byte[] block, int offset, string text) =>
        offset + text.Length <= block.Length &&
        Encoding.ASCII.GetString(block, offset, text.Length) == text;
}
