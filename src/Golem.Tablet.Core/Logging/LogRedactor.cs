using System.Text.RegularExpressions;

namespace Golem.Tablet.Core.Logging;

/// <summary>
/// Removes credential-like values from text before it is stored or exported.
/// Deliberately narrow: it targets labelled secrets and bearer tokens, not every
/// long token, so diagnostic values (paths, hashes) stay readable.
/// </summary>
public static class LogRedactor
{
    private static readonly Regex LabelledSecret = new(
        @"(?<key>\b(?:password|passwd|pwd|token|secret|api[_-]?key|authorization|auth)\b\s*[=:]\s*)(?<value>[^\s,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BearerToken = new(
        @"(?<key>\bBearer\s+)(?<value>[A-Za-z0-9._\-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SubscriberIdentity = new(
        @"(?<key>\b(?:imei|imsi|iccid)\b\s*[=:]\s*)(?<value>\d{5,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string result = LabelledSecret.Replace(text, "${key}<redacted>");
        result = BearerToken.Replace(result, "${key}<redacted>");
        result = SubscriberIdentity.Replace(result, "${key}<redacted>");
        return result;
    }
}
