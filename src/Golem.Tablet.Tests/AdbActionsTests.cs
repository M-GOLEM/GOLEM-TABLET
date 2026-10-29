using Golem.Tablet.Core.Adb;
using Xunit;

namespace Golem.Tablet.Tests;

public sealed class AdbActionsTests
{
    [Theory]
    [InlineData("com.example.app")]
    [InlineData("com.android.settings")]
    [InlineData("android")]
    [InlineData("com.example.my_app2")]
    [InlineData("com.example._internal")]
    public void ValidPackageNamesAreAccepted(string package) =>
        Assert.True(AdbActions.IsValidPackageName(package));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("com.example app")]
    [InlineData("com..example")]
    [InlineData(".com.example")]
    [InlineData("com.example.")]
    [InlineData("-k")]
    [InlineData("com.example;rm")]
    [InlineData("com.example/app")]
    public void InvalidPackageNamesAreRejected(string? package) =>
        Assert.False(AdbActions.IsValidPackageName(package));

    [Fact]
    public void UninstallTargetsTheCurrentUser()
    {
        IReadOnlyList<string> arguments = AdbActions.UninstallArguments("com.example.app");

        Assert.Equal(new[] { "shell", "pm", "uninstall", "--user", "0", "com.example.app" }, arguments);
    }

    [Fact]
    public void RebootArgumentsKeepModeAsASingleArgument()
    {
        Assert.Equal(new[] { "reboot", "edl" }, AdbActions.RebootArguments("edl"));
        Assert.Equal(new[] { "reboot", "download" }, AdbActions.RebootArguments("download"));
    }

    [Theory]
    [InlineData(400, "400")]
    [InlineData(0, "1")]
    [InlineData(9000, "5000")]
    public void LogcatLineCountIsClamped(int requested, string expected)
    {
        IReadOnlyList<string> arguments = AdbActions.LogcatArguments(requested);

        Assert.Equal(new[] { "shell", "logcat", "-d", "-t", expected }, arguments);
    }

    [Theory]
    [InlineData("/sdcard/", "file.zip", "/sdcard/file.zip")]
    [InlineData("/sdcard/Download", "file.zip", "/sdcard/Download/file.zip")]
    [InlineData("sdcard", "file.zip", "/sdcard/file.zip")]
    [InlineData("", "file.zip", "/sdcard/file.zip")]
    public void PushTargetUsesRemoteDirectory(string directory, string fileName, string expected) =>
        Assert.Equal(expected, AdbActions.RemoteTarget(directory, fileName));

    [Theory]
    [InlineData("/sdcard/Download/report.txt", "report.txt")]
    [InlineData("/sdcard/Download/", "Download")]
    [InlineData("report.txt", "report.txt")]
    [InlineData("/", "file")]
    public void RemoteFileNameIsExtracted(string remotePath, string expected) =>
        Assert.Equal(expected, AdbActions.RemoteFileName(remotePath));

    [Fact]
    public void ScreenshotUsesTheDeviceSidePath()
    {
        Assert.Equal(
            new[] { "shell", "screencap", "-p", "/sdcard/golem-screenshot.png" },
            AdbActions.ScreenshotArguments(AdbActions.DefaultScreenshotRemotePath));
    }

    [Fact]
    public void PullAndCleanupArgumentsKeepPathsIntact()
    {
        Assert.Equal(
            new[] { "pull", "/sdcard/golem-screenshot.png", "C:\\Temp\\shot.png" },
            AdbActions.PullArguments("/sdcard/golem-screenshot.png", "C:\\Temp\\shot.png"));
        Assert.Equal(
            new[] { "shell", "rm", "-f", "/sdcard/golem-screenshot.png" },
            AdbActions.DeleteRemoteFileArguments("/sdcard/golem-screenshot.png"));
    }

    [Fact]
    public void InstallReplacesTheExistingCopy()
    {
        Assert.Equal(
            new[] { "install", "-r", "C:\\Temp\\app.apk" },
            AdbActions.InstallArguments("C:\\Temp\\app.apk"));
    }

    [Fact]
    public void PackagePathQueryTargetsThePackageName()
    {
        Assert.Equal(
            new[] { "shell", "pm", "path", "com.example.app" },
            AdbActions.PackagePathArguments("com.example.app"));
    }

    [Theory]
    [InlineData(20, "20")]
    [InlineData(0, "1")]
    [InlineData(9999, "180")]
    public void ScreenRecordDurationIsClamped(int requested, string expected)
    {
        IReadOnlyList<string> arguments = AdbActions.ScreenRecordArguments(requested, "/sdcard/clip.mp4");

        Assert.Equal(new[] { "shell", "screenrecord", "--time-limit", expected, "/sdcard/clip.mp4" }, arguments);
    }

    [Fact]
    public void PackagePathIsReadFromPmOutput()
    {
        const string output = "package:/data/app/~~abc==/com.example.app-1==/base.apk\r\n";

        Assert.Equal("/data/app/~~abc==/com.example.app-1==/base.apk", AdbActions.ParsePackagePath(output));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Failure")]
    [InlineData("package:")]
    public void PackagePathIsNullWhenNoPathIsReturned(string? output) =>
        Assert.Null(AdbActions.ParsePackagePath(output));
}
