namespace HeadsetMonitor.Core;

public enum BatteryState
{
    Available,
    Charging,
    Unavailable,
    Disconnected,
    Error
}

public enum BatteryPrecision
{
    Exact,
    Coarse,
    Unknown
}

public sealed record DeviceKey(ushort VendorId, ushort ProductId, string ModelName);

public sealed record BatterySnapshot(
    DeviceKey DeviceKey,
    string DisplayName,
    int? LevelPercent,
    bool? Charging,
    BatteryState State,
    BatteryPrecision Precision,
    DateTimeOffset ObservedAtUtc,
    string? Diagnostic = null)
{
    public bool HasCurrentLevel =>
        LevelPercent is >= 0 and <= 100 && State is BatteryState.Available or BatteryState.Charging;
}
