using Golem.Tablet.Core.At;
using Xunit;

namespace Golem.Tablet.Tests;

public class AtResponseParserTests
{
    [Fact]
    public void Ok_response_strips_echo_and_terminator()
    {
        AtResponse response = AtResponseParser.Parse(
            "AT+CGMM\r\r\nMediaTek MT8788\r\nOK\r\n", "AT+CGMM");

        Assert.True(response.Succeeded);
        Assert.Null(response.Error);
        Assert.Equal(new[] { "MediaTek MT8788" }, response.Lines);
        Assert.Equal("MediaTek MT8788", response.DisplayText);
    }

    [Fact]
    public void Error_result_code_is_reported()
    {
        AtResponse response = AtResponseParser.Parse("AT+CGMM\r\r\nERROR\r\n", "AT+CGMM");

        Assert.False(response.Succeeded);
        Assert.Equal("ERROR", response.Error);
        Assert.Empty(response.Lines);
    }

    [Fact]
    public void Extended_error_codes_are_reported()
    {
        AtResponse response = AtResponseParser.Parse("+CME ERROR: 10\r\n");

        Assert.False(response.Succeeded);
        Assert.StartsWith("+CME ERROR", response.Error);
    }

    [Fact]
    public void No_carrier_is_reported()
    {
        AtResponse response = AtResponseParser.Parse("NO CARRIER\r\n");

        Assert.False(response.Succeeded);
        Assert.Equal("NO CARRIER", response.Error);
    }

    [Fact]
    public void Empty_output_reports_no_response()
    {
        AtResponse response = AtResponseParser.Parse(string.Empty);

        Assert.False(response.Succeeded);
        Assert.Equal("No response from the modem.", response.Error);
    }

    [Fact]
    public void Body_without_result_code_is_a_failure()
    {
        AtResponse response = AtResponseParser.Parse("some noise\r\n");

        Assert.False(response.Succeeded);
        Assert.Equal("Response did not end with OK.", response.Error);
        Assert.Equal(new[] { "some noise" }, response.Lines);
    }

    [Fact]
    public void DisplayText_marks_missing_body()
    {
        AtResponse response = AtResponseParser.Parse("OK\r\n");

        Assert.True(response.Succeeded);
        Assert.Equal("<no data>", response.DisplayText);
    }
}
