using Golem.Tablet.Core.Logging;
using Xunit;

namespace Golem.Tablet.Tests;

public class LogRedactorTests
{
    [Fact]
    public void Labelled_secrets_are_redacted()
    {
        string result = LogRedactor.Redact("password=hunter2 user=bob token: abc123");

        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("abc123", result);
        Assert.Contains("user=bob", result);
        Assert.Contains("<redacted>", result);
    }

    [Fact]
    public void Bearer_tokens_are_redacted()
    {
        string result = LogRedactor.Redact("Header Bearer eyJhbGciOi.payload.sig");

        Assert.DoesNotContain("eyJhbGciOi", result);
        Assert.Contains("<redacted>", result);
    }

    [Fact]
    public void Subscriber_identifiers_are_redacted()
    {
        string result = LogRedactor.Redact("imei: 356789012345678 iccid=89014103211118510720");

        Assert.DoesNotContain("356789012345678", result);
        Assert.DoesNotContain("89014103211118510720", result);
    }

    [Fact]
    public void Diagnostic_values_are_preserved()
    {
        const string text = @"scanning C:\Program Files\adb\adb.exe transport_id:7";
        Assert.Equal(text, LogRedactor.Redact(text));
    }

    [Fact]
    public void Null_and_empty_are_safe()
    {
        Assert.Equal(string.Empty, LogRedactor.Redact(null));
        Assert.Equal(string.Empty, LogRedactor.Redact(string.Empty));
    }
}
