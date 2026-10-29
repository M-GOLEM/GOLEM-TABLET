using System.Globalization;

namespace Golem.Tablet.Core.Adb;

/// <summary>Builds argument lists for supported adb operations. Arguments are never passed through a shell.</summary>
public static class AdbActions
{
    public const string DefaultRemoteDirectory = "/sdcard/";
    public const string DefaultScreenshotRemotePath = "/sdcard/golem-screenshot.png";
    public const string DefaultScreenRecordRemotePath = "/sdcard/golem-screenrecord.mp4";
    public const int ScreenRecordSeconds = 20;

    public static bool IsValidPackageName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string package = value.Trim();
        if (package.Length > 155)
        {
            return false;
        }

        foreach (string segment in package.Split('.'))
        {
            if (segment.Length == 0)
            {
                return false;
            }

            if (!char.IsAsciiLetter(segment[0]) && segment[0] != '_')
            {
                return false;
            }

            foreach (char c in segment)
            {
                if (!char.IsAsciiLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static IReadOnlyList<string> RebootArguments(string mode) =>
        new[] { "reboot", mode };

    public static IReadOnlyList<string> ListThirdPartyPackagesArguments() =>
        new[] { "shell", "pm", "list", "packages", "-3" };

    public static IReadOnlyList<string> UninstallArguments(string package) =>
        new[] { "shell", "pm", "uninstall", "--user", "0", package };

    public static IReadOnlyList<string> ScreenshotArguments(string remotePath) =>
        new[] { "shell", "screencap", "-p", remotePath };

    public static IReadOnlyList<string> DeleteRemoteFileArguments(string remotePath) =>
        new[] { "shell", "rm", "-f", remotePath };

    public static IReadOnlyList<string> LogcatArguments(int lines) =>
        new[] { "shell", "logcat", "-d", "-t", Math.Clamp(lines, 1, 5000).ToString(CultureInfo.InvariantCulture) };

    public static IReadOnlyList<string> PushArguments(string localPath, string remoteDirectory) =>
        new[] { "push", localPath, RemoteTarget(remoteDirectory, Path.GetFileName(localPath)) };

    public static IReadOnlyList<string> PullArguments(string remotePath, string localPath) =>
        new[] { "pull", remotePath, localPath };

    public static IReadOnlyList<string> InstallArguments(string localApk) =>
        new[] { "install", "-r", localApk };

    public static IReadOnlyList<string> PackagePathArguments(string package) =>
        new[] { "shell", "pm", "path", package };

    public static IReadOnlyList<string> ScreenRecordArguments(int seconds, string remotePath) =>
        new[]
        {
            "shell", "screenrecord", "--time-limit",
            Math.Clamp(seconds, 1, 180).ToString(CultureInfo.InvariantCulture), remotePath
        };

    /// <summary>Returns the first APK path from a <c>pm path</c> result, or null.</summary>
    public static string? ParsePackagePath(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        foreach (string line in output.Replace("\r\n", "\n").Split('\n'))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("package:", StringComparison.Ordinal))
            {
                continue;
            }

            string path = trimmed["package:".Length..].Trim();
            if (path.Length > 0)
            {
                return path;
            }
        }

        return null;
    }

    public static string RemoteTarget(string remoteDirectory, string fileName)
    {
        string directory = string.IsNullOrWhiteSpace(remoteDirectory)
            ? DefaultRemoteDirectory
            : remoteDirectory.Trim();

        if (!directory.StartsWith('/'))
        {
            directory = "/" + directory;
        }

        if (!directory.EndsWith('/'))
        {
            directory += "/";
        }

        return directory + fileName;
    }

    public static string RemoteFileName(string remotePath)
    {
        string trimmed = (remotePath ?? string.Empty).Trim().TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        string name = slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
        return name.Length == 0 ? "file" : name;
    }
}
