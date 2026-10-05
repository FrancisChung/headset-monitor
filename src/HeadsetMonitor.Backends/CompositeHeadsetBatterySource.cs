using HeadsetMonitor.Core;

namespace HeadsetMonitor.Backends;

public sealed class CompositeHeadsetBatterySource(params IHeadsetBatterySource[] sources) : IHeadsetBatterySource
{
    public async Task<HeadsetReadResult> ReadAsync(CancellationToken cancellationToken)
    {
        var reads = sources.Select(source => ReadSourceAsync(source, cancellationToken)).ToArray();
        var results = await Task.WhenAll(reads).ConfigureAwait(false);
        var successful = results.Where(result => result.Result is not null).Select(result => result.Result!).ToArray();
        var devices = successful.SelectMany(result => result.Devices)
            .DistinctBy(snapshot => snapshot.DeviceKey)
            .ToArray();
        var diagnostics = successful.SelectMany(result => result.Diagnostics ?? [])
            .Concat(results.Where(result => result.Error is not null).Select(result => result.Error!))
            .ToArray();
        var headsetControl = successful.FirstOrDefault(result => result.ApiVersion is not null);

        return new HeadsetReadResult(
            devices,
            headsetControl?.BackendVersion ?? successful.FirstOrDefault()?.BackendVersion,
            headsetControl?.ApiVersion,
            diagnostics);
    }

    private static async Task<SourceRead> ReadSourceAsync(
        IHeadsetBatterySource source,
        CancellationToken cancellationToken)
    {
        try
        {
            return new(await source.ReadAsync(cancellationToken).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or System.ComponentModel.Win32Exception
            or System.Text.Json.JsonException
            or HeadsetControlProcessException
            or HeadsetControlProtocolException)
        {
            return new(null, $"{source.GetType().Name}: {exception.Message}");
        }
    }

    private sealed record SourceRead(HeadsetReadResult? Result, string? Error);
}
