using HeadsetMonitor.Backends;
using HeadsetMonitor.Core;

namespace HeadsetMonitor.Tests;

public sealed class CompositeHeadsetBatterySourceTests
{
    [Test]
    public async Task Keeps_successful_devices_when_another_source_fails()
    {
        var snapshot = new BatterySnapshot(
            new DeviceKey(0x03f0, 0x06be, "HyperX Cloud III S Wireless"),
            "HyperX Cloud III S Wireless", 64, null, BatteryState.Available,
            BatteryPrecision.Exact, DateTimeOffset.UtcNow);
        var source = new CompositeHeadsetBatterySource(
            new StubSource(new HeadsetReadResult([snapshot], "HyperX direct HID", null)),
            new StubSource(new IOException("HeadsetControl is missing")));

        var result = await source.ReadAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Devices, Is.EqualTo(new[] { snapshot }));
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
            Assert.That(result.Diagnostics![0], Does.Contain("HeadsetControl is missing"));
        });
    }

    private sealed class StubSource : IHeadsetBatterySource
    {
        private readonly HeadsetReadResult? _result;
        private readonly Exception? _exception;

        public StubSource(HeadsetReadResult result) => _result = result;

        public StubSource(Exception exception) => _exception = exception;

        public Task<HeadsetReadResult> ReadAsync(CancellationToken cancellationToken) =>
            _exception is null
                ? Task.FromResult(_result!)
                : Task.FromException<HeadsetReadResult>(_exception);
    }
}
