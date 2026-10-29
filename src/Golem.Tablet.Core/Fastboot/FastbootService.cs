using Golem.Tablet.Core.Adb;
using Golem.Tablet.Core.Configuration;

namespace Golem.Tablet.Core.Fastboot;

/// <summary>
/// Locates fastboot.exe beside the configured adb.exe, then on PATH, and runs it
/// with argument lists. Fastboot must come from the Android SDK platform-tools.
/// </summary>
public sealed class FastbootService
{
    private readonly IProcessRunner _runner;

    public FastbootService(IProcessRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public string? ResolveExecutable(AppSettings settings, string? pathEnvironment = null)
    {
        if (!string.IsNullOrWhiteSpace(settings.AdbPath))
        {
            try
            {
                string? directory = Path.GetDirectoryName(settings.AdbPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    string candidate = Path.Combine(directory, "fastboot.exe");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch (ArgumentException)
            {
                // Invalid configured path; fall through to PATH.
            }
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
                candidate = Path.Combine(directory.Trim(), "fastboot.exe");
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
}
