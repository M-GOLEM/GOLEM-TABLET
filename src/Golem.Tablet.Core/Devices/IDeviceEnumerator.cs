namespace Golem.Tablet.Core.Devices;

public interface IDeviceEnumerator
{
    IReadOnlyList<UsbDeviceRecord> Enumerate();
}

public sealed class DeviceScanResult
{
    public DeviceScanResult(IReadOnlyList<DeviceInfo> devices, string? error)
    {
        Devices = devices;
        Error = error;
    }

    public IReadOnlyList<DeviceInfo> Devices { get; }

    public string? Error { get; }

    public bool Succeeded => Error is null;
}
