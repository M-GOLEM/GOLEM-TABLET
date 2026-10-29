namespace Golem.Tablet.Core.Adb;

public sealed class AdbDevice
{
    public AdbDevice(
        string serial,
        string state,
        string? product = null,
        string? model = null,
        string? device = null,
        string? transportId = null)
    {
        Serial = serial;
        State = state;
        Product = product;
        Model = model;
        Device = device;
        TransportId = transportId;
    }

    public string Serial { get; }

    public string State { get; }

    public string? Product { get; }

    public string? Model { get; }

    public string? Device { get; }

    public string? TransportId { get; }

    public bool IsOnline => string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);

    public string Summary
    {
        get
        {
            string model = Model is null ? string.Empty : $" ({Model})";
            return $"{Serial} — {State}{model}";
        }
    }
}
