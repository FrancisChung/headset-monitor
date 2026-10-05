namespace HeadsetMonitor.Core;

public sealed record AppSettings
{
    public const int DefaultPollingMinutes = 30;
    public const int DefaultWarningThreshold = 50;
    public const int DefaultCriticalThreshold = 20;

    public int PollingIntervalMinutes { get; init; } = DefaultPollingMinutes;
    public int WarningThreshold { get; init; } = DefaultWarningThreshold;
    public int CriticalThreshold { get; init; } = DefaultCriticalThreshold;
    public bool StartAtSignIn { get; init; }
    public string? SelectedDeviceKey { get; init; }
    public string? HeadsetControlPath { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (PollingIntervalMinutes is < 1 or > 1_440)
            errors.Add("Polling interval must be between 1 and 1440 minutes.");
        if (CriticalThreshold is < 0 or > 100)
            errors.Add("Critical threshold must be between 0 and 100.");
        if (WarningThreshold is < 0 or > 100)
            errors.Add("Warning threshold must be between 0 and 100.");
        if (CriticalThreshold >= WarningThreshold)
            errors.Add("Critical threshold must be lower than warning threshold.");
        return errors;
    }

    public BatteryBand GetBand(int levelPercent)
    {
        if (levelPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(levelPercent));

        return levelPercent <= CriticalThreshold
            ? BatteryBand.Critical
            : levelPercent <= WarningThreshold
                ? BatteryBand.Warning
                : BatteryBand.Good;
    }
}

public enum BatteryBand
{
    Critical,
    Warning,
    Good
}
