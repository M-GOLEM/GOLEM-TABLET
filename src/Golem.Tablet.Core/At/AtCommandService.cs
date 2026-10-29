using System.IO.Ports;
using System.Text;

namespace Golem.Tablet.Core.At;

/// <summary>Serial AT-command transport. One port is opened per command.</summary>
public sealed class AtCommandService
{
    public IReadOnlyList<string> GetPorts() =>
        SerialPort.GetPortNames()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public async Task<AtResponse> SendAsync(
        string portName,
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        int baudRate = 115200)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return AtResponse.Failure("No serial port selected.");
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return AtResponse.Failure("Command is empty.");
        }

        string trimmed = command.Trim();
        try
        {
            using var port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 250,
                WriteTimeout = 2000,
                NewLine = "\r",
                DtrEnable = true,
                RtsEnable = true,
                Handshake = Handshake.None
            };

            port.Open();
            port.DiscardInBuffer();
            port.DiscardOutBuffer();
            port.Write(trimmed + "\r");

            var text = new StringBuilder();
            byte[] buffer = new byte[512];
            DateTime deadline = DateTime.UtcNow + (timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(10) : timeout);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int available = port.BytesToRead;
                if (available > 0)
                {
                    int count = Math.Min(buffer.Length, available);
                    int read = await port.BaseStream
                        .ReadAsync(buffer.AsMemory(0, count), cancellationToken)
                        .ConfigureAwait(false);
                    if (read > 0)
                    {
                        text.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        if (TerminalReached(text.ToString()))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(40, cancellationToken).ConfigureAwait(false);
                }
            }

            return AtResponseParser.Parse(text.ToString(), trimmed);
        }
        catch (OperationCanceledException)
        {
            return AtResponse.Failure("Cancelled.");
        }
        catch (TimeoutException)
        {
            return AtResponse.Failure($"Timed out waiting for {portName}.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return AtResponse.Failure($"Cannot open {portName}: {ex.Message}");
        }
        catch (IOException ex)
        {
            return AtResponse.Failure($"Serial error on {portName}: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AtResponse.Failure($"Unexpected serial error: {ex.Message}");
        }
    }

    private static bool TerminalReached(string text)
    {
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return false;
        }

        string last = lines[^1].Trim();
        return last.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
               last.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) ||
               last.StartsWith("+CME ERROR", StringComparison.OrdinalIgnoreCase) ||
               last.StartsWith("+CMS ERROR", StringComparison.OrdinalIgnoreCase) ||
               last.Equals("NO CARRIER", StringComparison.OrdinalIgnoreCase);
    }
}
