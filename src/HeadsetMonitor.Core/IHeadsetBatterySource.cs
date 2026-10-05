namespace HeadsetMonitor.Core;

public interface IHeadsetBatterySource
{
    Task<HeadsetReadResult> ReadAsync(CancellationToken cancellationToken);
}

public sealed record HeadsetReadResult(
    IReadOnlyList<BatterySnapshot> Devices,
    string? BackendVersion,
    string? ApiVersion,
    IReadOnlyList<string>? Diagnostics = null);
