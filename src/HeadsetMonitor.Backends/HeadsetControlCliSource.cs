using System.Diagnostics;
using HeadsetMonitor.Core;

namespace HeadsetMonitor.Backends;

public sealed class HeadsetControlCliSource(
    string executablePath,
    TimeSpan? timeout = null,
    HeadsetControlJsonParser? parser = null) : IHeadsetBatterySource
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(5);
    private readonly HeadsetControlJsonParser _parser = parser ?? new HeadsetControlJsonParser();

    public async Task<HeadsetReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("The HeadsetControl executable was not found.", executablePath);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add("json");

        if (!process.Start())
            throw new HeadsetControlProcessException("HeadsetControl could not be started.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new HeadsetControlProcessException($"HeadsetControl did not finish within {_timeout.TotalSeconds:g} seconds.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new HeadsetControlProcessException(
                $"HeadsetControl exited with code {process.ExitCode}: {Redact(stderr)}");

        return _parser.Parse(stdout, DateTimeOffset.UtcNow);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
    }

    private static string Redact(string value)
    {
        var singleLine = value.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= 300 ? singleLine : singleLine[..300] + "…";
    }
}

public sealed class HeadsetControlProcessException(string message) : Exception(message);
