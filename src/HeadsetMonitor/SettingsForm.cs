using HeadsetMonitor.Core;

namespace HeadsetMonitor;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _polling = CreateNumber(1, 1_440);
    private readonly NumericUpDown _warning = CreateNumber(1, 100);
    private readonly NumericUpDown _critical = CreateNumber(0, 99);
    private readonly CheckBox _startAtSignIn = new()
    {
        Text = "Start automatically when I sign in",
        AutoSize = true
    };

    public AppSettings? UpdatedSettings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Text = "Headset Monitor Settings";
        Icon = ApplicationArtwork.LoadApplicationIcon();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _polling.Value = current.PollingIntervalMinutes;
        _warning.Value = current.WarningThreshold;
        _critical.Value = current.CriticalThreshold;
        _startAtSignIn.Checked = current.StartAtSignIn;

        var layout = new TableLayoutPanel { AutoSize = true, Padding = new Padding(12), ColumnCount = 2 };
        AddRow(layout, "Polling interval (minutes)", _polling);
        AddRow(layout, "Warning at or below (%)", _warning);
        AddRow(layout, "Critical at or below (%)", _critical);
        layout.Controls.Add(_startAtSignIn, 0, 3);
        layout.SetColumnSpan(_startAtSignIn, 2);

        var reset = new Button { Text = "Reset defaults", AutoSize = true };
        reset.Click += (_, _) =>
        {
            _polling.Value = AppSettings.DefaultPollingMinutes;
            _warning.Value = AppSettings.DefaultWarningThreshold;
            _critical.Value = AppSettings.DefaultCriticalThreshold;
            _startAtSignIn.Checked = false;
        };
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) => Save(current);
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([save, cancel, reset]);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Save(AppSettings current)
    {
        var candidate = current with
        {
            PollingIntervalMinutes = (int)_polling.Value,
            WarningThreshold = (int)_warning.Value,
            CriticalThreshold = (int)_critical.Value,
            StartAtSignIn = _startAtSignIn.Checked
        };
        var errors = candidate.Validate();
        if (errors.Count != 0)
        {
            MessageBox.Show(string.Join(Environment.NewLine, errors), "Invalid settings",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        UpdatedSettings = candidate;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static NumericUpDown CreateNumber(int minimum, int maximum) =>
        new() { Minimum = minimum, Maximum = maximum, Width = 90 };

    private static void AddRow(TableLayoutPanel layout, string text, Control control)
    {
        var row = layout.RowCount++;
        layout.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
