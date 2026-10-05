using HeadsetMonitor.Backends;
using HeadsetMonitor.Core;

namespace HeadsetMonitor.Tests;

public sealed class HeadsetControlJsonParserTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void Parses_supported_devices_and_ignores_others()
    {
        const string json = """
            {
              "version": "3.0.0", "api_version": "1.5", "devices": [
                { "status": "success", "device": "Logitech G933", "id_vendor": "0x046d", "id_product": "0x0a5b",
                  "battery": { "status": "BATTERY_AVAILABLE", "level": 72 } },
                { "status": "success", "device": "Unrelated headset", "id_vendor": "0x0001", "id_product": "0x0002",
                  "battery": { "status": "BATTERY_AVAILABLE", "level": 99 } }
              ]
            }
            """;

        var result = new HeadsetControlJsonParser().Parse(json, ObservedAt);

        Assert.Multiple(() =>
        {
            Assert.That(result.Devices, Has.Count.EqualTo(1));
            Assert.That(result.Devices[0].DisplayName, Is.EqualTo("Logitech G933"));
            Assert.That(result.Devices[0].LevelPercent, Is.EqualTo(72));
            Assert.That(result.BackendVersion, Is.EqualTo("3.0.0"));
            Assert.That(result.ApiVersion, Is.EqualTo("1.5"));
        });
    }

    [TestCase("HyperX Cloud II Wireless")]
    [TestCase("HyperX Cloud III S Wireless")]
    public void Ignores_hyperx_devices_owned_by_direct_hid_source(string deviceName)
    {
        var json = $$"""
            { "api_version": "1.5", "devices": [
              { "status": "success", "device": "{{deviceName}}", "id_vendor": "0x03f0", "id_product": "0x06be",
                "battery": { "status": "BATTERY_AVAILABLE", "level": 64 } }
            ] }
            """;

        Assert.That(new HeadsetControlJsonParser().Parse(json, ObservedAt).Devices, Is.Empty);
    }

    [TestCase(-1)]
    [TestCase(101)]
    public void Invalid_available_level_becomes_unavailable(int level)
    {
        var json = $$"""
            { "api_version": "1.5", "devices": [
              { "status": "success", "device": "Logitech G933", "id_vendor": "0x046d", "id_product": "0x0a5b",
                "battery": { "status": "BATTERY_AVAILABLE", "level": {{level}} } }
            ] }
            """;

        var snapshot = new HeadsetControlJsonParser().Parse(json, ObservedAt).Devices.Single();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.LevelPercent, Is.Null);
            Assert.That(snapshot.State, Is.EqualTo(BatteryState.Unavailable));
        });
    }

    [Test]
    public void Charging_without_level_does_not_invent_one()
    {
        const string json = """
            { "api_version": "1.5", "devices": [
              { "status": "success", "device": "Logitech G933", "id_vendor": "0x046d", "id_product": "0x0a5b",
                "battery": { "status": "BATTERY_CHARGING", "level": -1 } }
            ] }
            """;

        var snapshot = new HeadsetControlJsonParser().Parse(json, ObservedAt).Devices.Single();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.LevelPercent, Is.Null);
            Assert.That(snapshot.Charging, Is.True);
            Assert.That(snapshot.State, Is.EqualTo(BatteryState.Charging));
        });
    }

    [Test]
    public void Rejects_breaking_api_major_version()
    {
        const string json = "{ \"api_version\": \"2.0\", \"devices\": [] }";
        Assert.Throws<HeadsetControlProtocolException>(() =>
            new HeadsetControlJsonParser().Parse(json, ObservedAt));
    }
}
