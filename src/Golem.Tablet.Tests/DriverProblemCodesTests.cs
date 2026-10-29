using Golem.Tablet.Core.Drivers;
using Xunit;

namespace Golem.Tablet.Tests;

public class DriverProblemCodesTests
{
    [Fact]
    public void Zero_means_working()
    {
        (string description, DriverSeverity severity) = DriverProblemCodes.Describe(0);

        Assert.Equal(DriverSeverity.Ok, severity);
        Assert.Contains("Working", description);
    }

    [Fact]
    public void Missing_driver_code_is_an_error()
    {
        (string description, DriverSeverity severity) = DriverProblemCodes.Describe(28);

        Assert.Equal(DriverSeverity.Error, severity);
        Assert.Contains("No driver", description);
    }

    [Fact]
    public void Unknown_code_is_a_warning()
    {
        (_, DriverSeverity severity) = DriverProblemCodes.Describe(999);

        Assert.Equal(DriverSeverity.Warning, severity);
    }

    [Fact]
    public void Guidance_forbids_silent_installation()
    {
        Assert.Contains("never performed automatically", DriverProblemCodes.Guidance);
    }
}
