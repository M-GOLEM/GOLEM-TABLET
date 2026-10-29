using System.Security.Principal;
using Golem.Tablet.Core.Devices;

namespace Golem.Tablet.Core.Drivers;

public sealed class DriverInspector
{
    private readonly IDeviceEnumerator _enumerator;

    public DriverInspector(IDeviceEnumerator enumerator)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    }

    public static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public Task<DriverReport> InspectAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Inspect(), cancellationToken);

    private DriverReport Inspect()
    {
        try
        {
            IReadOnlyList<UsbDeviceRecord> records = _enumerator.Enumerate();
            var issues = new List<DriverIssue>();

            foreach (UsbDeviceRecord record in records)
            {
                if (record.ProblemCode is not { } code || code == 0)
                {
                    continue;
                }

                (string description, DriverSeverity severity) = DriverProblemCodes.Describe(code);
                issues.Add(new DriverIssue(record.Name, record.PnpDeviceId, code, description, severity));
            }

            bool elevated = IsElevated();
            return new DriverReport(
                issues.OrderByDescending(i => i.Severity).ThenBy(i => i.DeviceName).ToList(),
                records.Count,
                elevated,
                null);
        }
        catch (Exception ex)
        {
            return new DriverReport(Array.Empty<DriverIssue>(), 0, IsElevated(), ex.Message);
        }
    }
}
