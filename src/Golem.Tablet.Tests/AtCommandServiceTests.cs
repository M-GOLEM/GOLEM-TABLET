using Golem.Tablet.Core.At;
using Xunit;

namespace Golem.Tablet.Tests;

public class AtCommandServiceTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Missing_port_is_rejected_without_opening_anything()
    {
        var service = new AtCommandService();

        AtResponse response = await service.SendAsync(" ", "ATI", ShortTimeout, CancellationToken.None);

        Assert.False(response.Succeeded);
        Assert.Equal("No serial port selected.", response.Error);
    }

    [Fact]
    public async Task Empty_command_is_rejected()
    {
        var service = new AtCommandService();

        AtResponse response = await service.SendAsync("COM1", "  ", ShortTimeout, CancellationToken.None);

        Assert.False(response.Succeeded);
        Assert.Equal("Command is empty.", response.Error);
    }

    [Fact]
    public async Task Closed_port_fails_with_a_typed_error()
    {
        var service = new AtCommandService();

        AtResponse response = await service.SendAsync("COM255", "ATI", ShortTimeout, CancellationToken.None);

        Assert.False(response.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(response.Error));
        Assert.Contains("COM255", response.Error);
    }

    [Fact]
    public void Port_enumeration_returns_sorted_names()
    {
        var service = new AtCommandService();

        IReadOnlyList<string> ports = service.GetPorts();

        Assert.Equal(ports.OrderBy(p => p, StringComparer.OrdinalIgnoreCase), ports);
    }
}
