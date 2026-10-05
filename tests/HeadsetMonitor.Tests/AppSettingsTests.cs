using HeadsetMonitor.Core;

namespace HeadsetMonitor.Tests;

public sealed class AppSettingsTests
{
    [TestCase(0, BatteryBand.Critical)]
    [TestCase(20, BatteryBand.Critical)]
    [TestCase(21, BatteryBand.Warning)]
    [TestCase(50, BatteryBand.Warning)]
    [TestCase(51, BatteryBand.Good)]
    [TestCase(100, BatteryBand.Good)]
    public void Default_threshold_boundaries_are_unambiguous(int level, BatteryBand expected) =>
        Assert.That(new AppSettings().GetBand(level), Is.EqualTo(expected));

    [Test]
    public void Invalid_ordered_thresholds_are_rejected()
    {
        var settings = new AppSettings { CriticalThreshold = 50, WarningThreshold = 20 };
        Assert.That(settings.Validate(), Is.Not.Empty);
    }
}
