using System.Diagnostics;
using System.Text;

namespace Golem.Tablet.Core.Adb;

/// <summary>
/// Runs an executable with an argument list (never a concatenated command string)
/// and enforces a timeout. Output is captured, not inherited.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return ProcessResult.NotStarted("The process did not start.");
            }
        }
        catch (Exception ex)
        {
            return ProcessResult.NotStarted(ex.Message);
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();

        bool timedOut = false;
        bool cancelled = false;

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = cancellationToken.IsCancellationRequested;
            timedOut = !cancelled;
            TryKill(process);
        }

        // Give the reader tasks a moment to drain after exit or kill.
        await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask), Task.Delay(2000)).ConfigureAwait(false);

        string stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : string.Empty;
        string stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : string.Empty;

        int? exitCode = null;
        try
        {
            if (process.HasExited)
            {
                exitCode = process.ExitCode;
            }
        }
        catch (InvalidOperationException)
        {
            exitCode = null;
        }

        return new ProcessResult(true, exitCode, stdout, stderr, timedOut, cancelled);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may already be gone; nothing useful to do.
        }
    }
}
