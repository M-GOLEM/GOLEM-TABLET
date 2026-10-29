using Golem.Tablet.Core.Configuration;

namespace Golem.Tablet.Core.Adb;

public sealed class AdbService
{
    private static readonly string[] VersionArguments = { "version" };
    private static readonly string[] DeviceListArguments = { "devices", "-l" };

    private readonly IProcessRunner _runner;

    public AdbService(IProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    /// <summary>Uses the configured path, otherwise searches PATH for adb.exe.</summary>
    public string? ResolveExecutable(AppSettings settings, string? pathEnvironment = null)
    {
        if (!string.IsNullOrWhiteSpace(settings.AdbPath) && File.Exists(settings.AdbPath))
        {
            return settings.AdbPath;
        }

        return FindOnPath(pathEnvironment);
    }

    public static string? FindOnPath(string? pathEnvironment = null)
    {
        string path = pathEnvironment ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(directory.Trim(), "adb.exe");
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var list = arguments.ToList();
        return _runner.RunAsync(executable, list, timeout, cancellationToken);
    }

    public Task<ProcessResult> RunForDeviceAsync(
        string executable,
        string serial,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var list = new List<string> { "-s", serial };
        list.AddRange(arguments);
        return _runner.RunAsync(executable, list, timeout, cancellationToken);
    }

    public Task<ProcessResult> GetVersionAsync(
        string executable,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _runner.RunAsync(executable, VersionArguments, timeout, cancellationToken);

    public Task<ProcessResult> ListDevicesAsync(
        string executable,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        _runner.RunAsync(executable, DeviceListArguments, timeout, cancellationToken);

    /// <summary>Argument preview for the log. Values are shown, never interpreted by a shell.</summary>
    public static string FormatCommand(string executable, IEnumerable<string> arguments) =>
        executable + " " + string.Join(' ', arguments.Select(QuoteIfNeeded));

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ') ? '"' + value + '"' : value;
}
