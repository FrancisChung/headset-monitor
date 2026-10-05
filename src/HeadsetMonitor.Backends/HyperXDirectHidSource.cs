using HeadsetMonitor.Core;
using HidSharp;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace HeadsetMonitor.Backends;

// Protocol bytes and endpoint-selection behavior are derived from auto94's
// MIT-licensed HyperX-Cloud-2-Battery-Monitor v2.0 implementation.
public sealed class HyperXDirectHidSource : IHeadsetBatterySource
{
    private const int IoTimeoutMilliseconds = 1_000;

    public Task<HeadsetReadResult> ReadAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Read(cancellationToken), cancellationToken);

    private static HeadsetReadResult Read(CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var snapshots = new List<BatterySnapshot>();
        var diagnostics = new List<string>();

        foreach (var protocol in HyperXProtocol.SupportedDevices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var endpoints = DeviceList.Local.GetHidDevices(protocol.VendorId, protocol.ProductId).ToArray();
            if (endpoints.Length == 0)
                continue;

            var endpoint = SelectEndpoint(endpoints, protocol);
            if (endpoint is null)
            {
                diagnostics.Add($"{protocol.DisplayName}: compatible HID endpoint was not found.");
                continue;
            }

            snapshots.Add(ReadDevice(endpoint, protocol, observedAt, diagnostics));
        }

        return new HeadsetReadResult(snapshots, "HyperX direct HID (auto94 v2.0 protocol)", null, diagnostics);
    }

    private static HidDevice? SelectEndpoint(IEnumerable<HidDevice> endpoints, HyperXDeviceProtocol protocol)
    {
        if (protocol.UsagePage.HasValue && protocol.Usage.HasValue)
        {
            var expectedUsage = ((uint)protocol.UsagePage.Value << 16) | protocol.Usage.Value;
            return endpoints.FirstOrDefault(endpoint => GetUsages(endpoint).Contains(expectedUsage));
        }

        return endpoints
            .OrderByDescending(endpoint => GetUsages(endpoint).DefaultIfEmpty().Max())
            .FirstOrDefault();
    }

    private static IReadOnlyCollection<uint> GetUsages(HidDevice endpoint)
    {
        try
        {
            return endpoint.GetReportDescriptor().DeviceItems
                .SelectMany(item => item.Usages.GetAllValues())
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return [];
        }
    }

    private static BatterySnapshot ReadDevice(
        HidDevice endpoint,
        HyperXDeviceProtocol protocol,
        DateTimeOffset observedAt,
        ICollection<string> diagnostics)
    {
        try
        {
            if (protocol.ReadInputReportBeforeWrite)
            {
                var inputReport = new byte[Math.Max(160, endpoint.GetMaxInputReportLength())];
                inputReport[0] = 0x06;
                PrimeKingstonReceiver(endpoint.DevicePath, inputReport);
            }

            using var stream = endpoint.Open();
            stream.ReadTimeout = IoTimeoutMilliseconds;
            stream.WriteTimeout = IoTimeoutMilliseconds;
            stream.Write(protocol.Request);
            var response = new byte[Math.Max(20, endpoint.GetMaxInputReportLength())];
            var bytesRead = stream.Read(response, 0, response.Length);
            var level = HyperXProtocol.ParseBatteryLevel(protocol, response.AsSpan(0, bytesRead));

            return Snapshot(level, level.HasValue ? BatteryState.Available : BatteryState.Unavailable,
                level.HasValue ? null : "The headset is off or returned an invalid battery level.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException)
        {
            diagnostics.Add($"{protocol.DisplayName}: {exception.Message}");
            return Snapshot(null, BatteryState.Error, "Direct HID communication failed.");
        }

        BatterySnapshot Snapshot(int? level, BatteryState state, string? diagnostic) => new(
            new DeviceKey(protocol.VendorId, protocol.ProductId, protocol.DisplayName),
            protocol.DisplayName,
            level,
            null,
            state,
            BatteryPrecision.Exact,
            observedAt,
            diagnostic);
    }

    private static void PrimeKingstonReceiver(string devicePath, byte[] inputReport)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("The Kingston Cloud II HID handshake requires Windows.");

        using var handle = CreateFile(
            devicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException("Could not open the Kingston Cloud II HID endpoint.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        if (!HidD_GetInputReport(handle, inputReport, inputReport.Length))
            throw new IOException("Could not prime the Kingston Cloud II receiver.",
                new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool HidD_GetInputReport(
        SafeFileHandle hidDeviceObject,
        byte[] reportBuffer,
        int reportBufferLength);
}
