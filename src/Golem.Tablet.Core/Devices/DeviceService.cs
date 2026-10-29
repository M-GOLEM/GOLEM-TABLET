namespace Golem.Tablet.Core.Devices;

public sealed class DeviceService
{
    private readonly IDeviceEnumerator _enumerator;

    public DeviceService(IDeviceEnumerator enumerator)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    }

    public Task<DeviceScanResult> RefreshAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(), cancellationToken);

    private DeviceScanResult Scan()
    {
        try
        {
            IReadOnlyList<DeviceInfo> devices = _enumerator
                .Enumerate()
                .Select(DeviceClassifier.Classify)
                .OrderBy(d => d.FamilyText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new DeviceScanResult(devices, null);
        }
        catch (Exception ex)
        {
            return new DeviceScanResult(Array.Empty<DeviceInfo>(), ex.Message);
        }
    }
}
