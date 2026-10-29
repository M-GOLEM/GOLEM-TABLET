using System.Text.RegularExpressions;

namespace Golem.Tablet.Core.Devices;

/// <summary>One raw Windows PnP device record, before classification.</summary>
public sealed class UsbDeviceRecord
{
    private static readonly Regex VidPattern = new(@"VID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PidPattern = new(@"PID_([0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public UsbDeviceRecord(
        string name,
        string pnpDeviceId,
        string? service = null,
        string? status = null,
        uint? problemCode = null)
    {
        Name = name ?? string.Empty;
        PnpDeviceId = pnpDeviceId ?? string.Empty;
        Service = service;
        Status = status;
        ProblemCode = problemCode;
    }

    public string Name { get; }

    public string PnpDeviceId { get; }

    public string? Service { get; }

    public string? Status { get; }

    /// <summary>Win32 <c>ConfigManagerErrorCode</c>; 0 means "working".</summary>
    public uint? ProblemCode { get; }

    public ushort? VendorId => ParseHex(VidPattern);

    public ushort? ProductId => ParseHex(PidPattern);

    public string VidPidText
    {
        get
        {
            string vid = VendorId is { } v ? $"VID_{v:X4}" : "VID_----";
            string pid = ProductId is { } p ? $"PID_{p:X4}" : "PID_----";
            return $"{vid}&{pid}";
        }
    }

    public string? Serial
    {
        get
        {
            string[] parts = PnpDeviceId.Split('\\');
            return parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2] : null;
        }
    }

    private ushort? ParseHex(Regex pattern)
    {
        Match match = pattern.Match(PnpDeviceId);
        return match.Success && ushort.TryParse(match.Groups[1].Value,
            System.Globalization.NumberStyles.HexNumber, null, out ushort value)
            ? value
            : null;
    }
}
