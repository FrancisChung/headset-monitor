using HeadsetMonitor.Backends;

namespace HeadsetMonitor.Tests;

public sealed class HyperXProtocolTests
{
    [Test]
    public void Cloud_iii_s_uses_verified_identity_endpoint_and_request()
    {
        var protocol = HyperXProtocol.Find(0x03f0, 0x06be);

        Assert.That(protocol, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(protocol!.DisplayName, Is.EqualTo("HyperX Cloud III S Wireless"));
            Assert.That(protocol.UsagePage, Is.EqualTo(0x01c0));
            Assert.That(protocol.Usage, Is.EqualTo(1));
            Assert.That(protocol.Request.Take(6), Is.EqualTo(new byte[] { 0x0c, 0x02, 0x03, 0x01, 0x00, 0x06 }));
            Assert.That(protocol.BatteryByteIndex, Is.EqualTo(6));
        });
    }

    [TestCase(0x03f0, 0x018b)]
    [TestCase(0x03f0, 0x0696)]
    [TestCase(0x0951, 0x1718)]
    public void Includes_known_cloud_ii_receivers(int vendorId, int productId) =>
        Assert.That(HyperXProtocol.Find((ushort)vendorId, (ushort)productId), Is.Not.Null);

    [Test]
    public void Parses_valid_level_and_rejects_off_state()
    {
        var protocol = HyperXProtocol.Find(0x03f0, 0x06be)!;
        var available = new byte[20];
        available[6] = 64;
        var off = new byte[20];
        off[6] = 0xff;

        Assert.Multiple(() =>
        {
            Assert.That(HyperXProtocol.ParseBatteryLevel(protocol, available), Is.EqualTo(64));
            Assert.That(HyperXProtocol.ParseBatteryLevel(protocol, off), Is.Null);
            Assert.That(HyperXProtocol.ParseBatteryLevel(protocol, new byte[6]), Is.Null);
        });
    }
}
