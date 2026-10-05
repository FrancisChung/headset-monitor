using HeadsetMonitor.Backends;
using HeadsetMonitor.Core;

namespace HeadsetMonitor;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly WindowsAutoStartManager _autoStartManager = new();
    private readonly NotifyIcon _notifyIcon = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private AppSettings _settings;
    private IReadOnlyList<BatterySnapshot> _snapshots = [];
    private Icon? _currentIcon;
    private string? _backendVersion;
    private string? _apiVersion;
    private string? _lastError;

    public TrayApplicationContext()
    {
        _settings = _settingsStore.Load() with { StartAtSignIn = _autoStartManager.IsEnabled() };
        _timer.Tick += async (_, _) => await RefreshAsync();
        ApplyPollingInterval();
        _notifyIcon.Visible = true;
        UpdatePresentation();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (!await _pollGate.WaitAsync(0))
            return;
        try
        {
            var executable = string.IsNullOrWhiteSpace(_settings.HeadsetControlPath)
                ? Path.Combine(AppContext.BaseDirectory, "headsetcontrol.exe")
                : _settings.HeadsetControlPath;
            var source = new CompositeHeadsetBatterySource(
                new HyperXDirectHidSource(),
                new HeadsetControlCliSource(executable));
            var result = await source.ReadAsync(_shutdown.Token);
            _snapshots = result.Devices;
            _backendVersion = result.BackendVersion;
            _apiVersion = result.ApiVersion;
            _lastError = result.Diagnostics is { Count: > 0 }
                ? string.Join(Environment.NewLine, result.Diagnostics)
                : null;
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException
            or System.ComponentModel.Win32Exception
            or System.Text.Json.JsonException
            or HeadsetControlProcessException
            or HeadsetControlProtocolException)
        {
            _snapshots = [];
            _lastError = exception.Message;
        }
        finally
        {
            _pollGate.Release();
            if (!_shutdown.IsCancellationRequested)
                UpdatePresentation();
        }
    }

    private void UpdatePresentation()
    {
        var selected = SelectSnapshot();
        var nextIcon = TrayIconRenderer.Render(selected, _settings);
        _notifyIcon.Icon = nextIcon;
        _currentIcon?.Dispose();
        _currentIcon = nextIcon;
        _notifyIcon.Text = BuildTooltip();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.ContextMenuStrip = BuildMenu();
    }

    private BatterySnapshot? SelectSnapshot() =>
        _snapshots.FirstOrDefault(x => SerializeKey(x.DeviceKey) == _settings.SelectedDeviceKey) ??
        _snapshots.FirstOrDefault();

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        if (_snapshots.Count == 0)
            menu.Items.Add(new ToolStripMenuItem(_lastError is null ? "No supported headset found" : "Reading unavailable") { Enabled = false });
        foreach (var snapshot in _snapshots)
        {
            var item = new ToolStripMenuItem(FormatSnapshot(snapshot))
            {
                Checked = SerializeKey(snapshot.DeviceKey) == _settings.SelectedDeviceKey
            };
            item.Click += (_, _) => Select(snapshot);
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Refresh", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add("About / diagnostics…", null, (_, _) => ShowDiagnostics());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void Select(BatterySnapshot snapshot)
    {
        _settings = _settings with { SelectedDeviceKey = SerializeKey(snapshot.DeviceKey) };
        _settingsStore.Save(_settings);
        UpdatePresentation();
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settings);
        if (form.ShowDialog() != DialogResult.OK || form.UpdatedSettings is null)
            return;

        try
        {
            _autoStartManager.SetEnabled(form.UpdatedSettings.StartAtSignIn);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException
            or InvalidOperationException)
        {
            MessageBox.Show($"The startup setting could not be changed.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "Headset Monitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _settings = form.UpdatedSettings with { StartAtSignIn = _autoStartManager.IsEnabled() };
        _settingsStore.Save(_settings);
        ApplyPollingInterval();
        UpdatePresentation();
    }

    private void ShowDiagnostics() => MessageBox.Show(
        $"HeadsetControl version: {_backendVersion ?? "unknown"}{Environment.NewLine}" +
        $"JSON API version: {_apiVersion ?? "unknown"}{Environment.NewLine}" +
        $"Last error: {_lastError ?? "none"}",
        "Headset Monitor diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void ApplyPollingInterval()
    {
        _timer.Interval = checked(_settings.PollingIntervalMinutes * 60 * 1_000);
        _timer.Start();
    }

    private string BuildTooltip()
    {
        var text = _snapshots.Count == 0
            ? (_lastError is null ? "No supported headset found" : "Reading unavailable")
            : string.Join(Environment.NewLine, _snapshots.Select(FormatSnapshot));
        return text.Length <= 63 ? text : text[..62] + "…";
    }

    private static string FormatSnapshot(BatterySnapshot snapshot) => snapshot.HasCurrentLevel
        ? $"{snapshot.DisplayName} — {snapshot.LevelPercent}%{(snapshot.Charging == true ? " (charging)" : string.Empty)}"
        : $"{snapshot.DisplayName} — {snapshot.State}";

    private static string SerializeKey(DeviceKey key) => $"{key.VendorId:x4}:{key.ProductId:x4}:{key.ModelName}";

    protected override void ExitThreadCore()
    {
        _shutdown.Cancel();
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentIcon?.Dispose();
        _timer.Dispose();
        // A cancelled refresh may still be unwinding and will release the gate.
        // Let these process-lifetime synchronization objects be reclaimed on exit.
        base.ExitThreadCore();
    }
}
