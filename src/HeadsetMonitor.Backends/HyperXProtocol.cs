namespace HeadsetMonitor.Backends;

public sealed record HyperXDeviceProtocol(
    ushort VendorId,
    ushort ProductId,
    string DisplayName,
    ushort? UsagePage,
    ushort? Usage,
    byte[] Request,
    int BatteryByteIndex,
    bool ReadInputReportBeforeWrite = false);

public static class HyperXProtocol
{
    public const ushort HpVendorId = 0x03f0;
    public const ushort KingstonVendorId = 0x0951;

    private static readonly byte[] CloudIiHpRequest = CreateRequest(0x06, 0xff, 0xbb, 0x02);
    private static readonly byte[] CloudIiKingstonRequest = CreateKingstonCloudIiRequest();
    private static readonly byte[] CloudIiiSRequest = CreateRequest(0x0c, 0x02, 0x03, 0x01, 0x00, 0x06);

    public static IReadOnlyList<HyperXDeviceProtocol> SupportedDevices { get; } =
    [
        new(KingstonVendorId, 0x1718, "HyperX Cloud II Wireless", null, null,
            CloudIiKingstonRequest, 7, ReadInputReportBeforeWrite: true),
        new(HpVendorId, 0x018b, "HyperX Cloud II Wireless", null, null, CloudIiHpRequest, 7),
        new(HpVendorId, 0x0696, "HyperX Cloud II Wireless", null, null, CloudIiHpRequest, 7),
        new(HpVendorId, 0x06be, "HyperX Cloud III S Wireless", 0x01c0, 0x0001, CloudIiiSRequest, 6)
    ];

    public static HyperXDeviceProtocol? Find(ushort vendorId, ushort productId) =>
        SupportedDevices.FirstOrDefault(device => device.VendorId == vendorId && device.ProductId == productId);

    public static int? ParseBatteryLevel(HyperXDeviceProtocol protocol, ReadOnlySpan<byte> response)
    {
        if (response.Length <= protocol.BatteryByteIndex)
            return null;

        var level = response[protocol.BatteryByteIndex];
        return level <= 100 ? level : null;
    }

    private static byte[] CreateRequest(params byte[] prefix)
    {
        var request = new byte[52];
        prefix.CopyTo(request, 0);
        return request;
    }

    private static byte[] CreateKingstonCloudIiRequest()
    {
        var request = new byte[52];
        request[0] = 0x06;
        request[2] = 0x02;
        request[4] = 0x9a;
        request[7] = 0x68;
        request[8] = 0x4a;
        request[9] = 0x8e;
        request[10] = 0x0a;
        request[14] = 0xbb;
        request[15] = 0x02;
        return request;
    }
}
