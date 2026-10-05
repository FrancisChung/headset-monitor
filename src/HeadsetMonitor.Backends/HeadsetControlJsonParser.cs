using System.Globalization;
using System.Text.Json;
using HeadsetMonitor.Core;

namespace HeadsetMonitor.Backends;

public sealed class HeadsetControlJsonParser
{
    public const int SupportedApiMajorVersion = 1;

    public HeadsetReadResult Parse(string json, DateTimeOffset observedAtUtc)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var apiVersion = GetString(root, "api_version");
        ValidateApiVersion(apiVersion);

        var snapshots = new List<BatterySnapshot>();
        if (!root.TryGetProperty("devices", out var devices) || devices.ValueKind != JsonValueKind.Array)
            throw new HeadsetControlProtocolException("HeadsetControl JSON does not contain a devices array.");

        foreach (var device in devices.EnumerateArray())
        {
            var name = GetString(device, "device") ?? "Unknown headset";
            if (!IsSupportedModel(name))
                continue;

            var vendorId = ParseUsbId(GetString(device, "id_vendor"));
            var productId = ParseUsbId(GetString(device, "id_product"));
            snapshots.Add(MapDevice(device, name, vendorId, productId, observedAtUtc));
        }

        return new HeadsetReadResult(snapshots, GetString(root, "version"), apiVersion);
    }

    private static BatterySnapshot MapDevice(
        JsonElement device,
        string name,
        ushort vendorId,
        ushort productId,
        DateTimeOffset observedAtUtc)
    {
        var deviceStatus = GetString(device, "status");
        if (string.Equals(deviceStatus, "failure", StringComparison.OrdinalIgnoreCase))
            return Snapshot(null, null, BatteryState.Error, "Device communication failed.");

        if (!device.TryGetProperty("battery", out var battery) || battery.ValueKind != JsonValueKind.Object)
            return Snapshot(null, null, BatteryState.Unavailable, "Battery data was not returned.");

        var status = GetString(battery, "status")?.ToUpperInvariant();
        var rawLevel = battery.TryGetProperty("level", out var levelElement) && levelElement.TryGetInt32(out var value)
            ? value
            : (int?)null;
        var validLevel = rawLevel is >= 0 and <= 100 ? rawLevel : null;

        return status switch
        {
            "BATTERY_AVAILABLE" => Snapshot(validLevel, false,
                validLevel.HasValue ? BatteryState.Available : BatteryState.Unavailable,
                validLevel.HasValue ? null : "Battery level was missing or outside 0-100."),
            "BATTERY_CHARGING" => Snapshot(validLevel, true, BatteryState.Charging),
            "BATTERY_UNAVAILABLE" => Snapshot(null, null, BatteryState.Unavailable),
            _ => Snapshot(null, null, BatteryState.Unavailable, "Unknown battery status was returned.")
        };

        BatterySnapshot Snapshot(int? level, bool? charging, BatteryState state, string? diagnostic = null) =>
            new(new DeviceKey(vendorId, productId, name), name, level, charging, state,
                BatteryPrecision.Unknown, observedAtUtc, diagnostic);
    }

    private static bool IsSupportedModel(string name) =>
        name.Contains("G933", StringComparison.OrdinalIgnoreCase);

    private static ushort ParseUsbId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        var value = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        return ushort.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) ? id : (ushort)0;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void ValidateApiVersion(string? apiVersion)
    {
        if (string.IsNullOrWhiteSpace(apiVersion))
            throw new HeadsetControlProtocolException("HeadsetControl JSON does not report api_version.");
        var majorText = apiVersion.Split('.', 2)[0];
        if (!int.TryParse(majorText, out var major) || major != SupportedApiMajorVersion)
            throw new HeadsetControlProtocolException($"Unsupported HeadsetControl API version '{apiVersion}'.");
    }
}

public sealed class HeadsetControlProtocolException(string message) : Exception(message);
