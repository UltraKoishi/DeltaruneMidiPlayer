using DeltaruneMidiPlayer.UI;

namespace DeltaruneMidiPlayer;

internal sealed record AdvancedSettingsValues(
    int NoteDelayMs,
    bool ChangeInstruments,
    bool UseEndPause,
    int EndPauseMs,
    bool UseCustomBpm,
    decimal CustomBpm,
    bool SuppressSimultaneousWarnings,
    bool ShowNoteSequence,
    bool EqualizeChannelDurations,
    bool ControlRecording,
    string RecordingHotkey);

internal sealed class AdvancedSettingsForm : Form
{
    private readonly ThemePalette _palette;
    private readonly NumericUpDown _delayInput = new()
    {
        Minimum = 0,
        Maximum = 5000,
        Width = 110
    };
    private readonly NumericUpDown _bpmInput = new()
    {
        Minimum = 20,
        Maximum = 500,
        DecimalPlaces = 2,
        Increment = 1,
        Width = 110
    };
    private readonly NumericUpDown _endPauseInput = new()
    {
        Minimum = 0,
        Maximum = 60000,
        Increment = 100,
        Width = 110
    };
    private readonly CheckBox _changeInstrumentsCheckBox = new() { AutoSize = true };
    private readonly CheckBox _endPauseCheckBox = new() { AutoSize = true };
    private readonly CheckBox _customBpmCheckBox = new() { AutoSize = true };
    private readonly CheckBox _showWarningsCheckBox = new() { AutoSize = true };
    private readonly CheckBox _showNoteSequenceCheckBox = new() { AutoSize = true };
    private readonly CheckBox _equalizeChannelDurationsCheckBox = new() { AutoSize = true };
    private readonly CheckBox _controlRecordingCheckBox = new() { AutoSize = true };
    private readonly AppComboBox _recordingHotkeyCombo = new() { Width = 170 };

    public AdvancedSettingsValues Values { get; private set; }

    public AdvancedSettingsForm(
        ThemePalette palette,
        bool english,
        AdvancedSettingsValues values,
        IReadOnlyList<string> recordingHotkeys,
        Icon? applicationIcon)
    {
        _palette = palette;
        Values = values;

        Text = english ? "Deltarune MIDI Player settings" : "Параметры Deltarune MIDI Player";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(610, 620);
        Font = new Font("Segoe UI", 10F);
        Icon = applicationIcon is null ? null : (Icon)applicationIcon.Clone();

        _delayInput.Value = Math.Clamp(values.NoteDelayMs, (int)_delayInput.Minimum, (int)_delayInput.Maximum);
        _endPauseInput.Value = Math.Clamp(values.EndPauseMs, (int)_endPauseInput.Minimum, (int)_endPauseInput.Maximum);
        _bpmInput.Value = Math.Clamp(values.CustomBpm, _bpmInput.Minimum, _bpmInput.Maximum);
        _changeInstrumentsCheckBox.Checked = values.ChangeInstruments;
        _endPauseCheckBox.Checked = values.UseEndPause;
        _customBpmCheckBox.Checked = values.UseCustomBpm;
        _showWarningsCheckBox.Checked = !values.SuppressSimultaneousWarnings;
        _showNoteSequenceCheckBox.Checked = values.ShowNoteSequence;
        _equalizeChannelDurationsCheckBox.Checked = values.EqualizeChannelDurations;
        _controlRecordingCheckBox.Checked = values.ControlRecording;
        _recordingHotkeyCombo.Items.AddRange(recordingHotkeys.Cast<object>().ToArray());
        _recordingHotkeyCombo.SelectedItem = recordingHotkeys.Contains(
            values.RecordingHotkey,
            StringComparer.OrdinalIgnoreCase)
            ? recordingHotkeys.First(key => key.Equals(values.RecordingHotkey, StringComparison.OrdinalIgnoreCase))
            : "Numpad7";

        _changeInstrumentsCheckBox.Text = english
            ? "Change instruments during playback"
            : "Изменять инструменты во время воспроизведения";
        _endPauseCheckBox.Text = english
            ? "Pause after playback"
            : "Пауза после проигрывания";
        _customBpmCheckBox.Text = english ? "Use custom BPM" : "Использовать свой BPM";
        _showWarningsCheckBox.Text = english
            ? "Show simultaneous-note warnings"
            : "Показывать предупреждения об одновременных нотах";
        _showNoteSequenceCheckBox.Text = english
            ? "Show MIDI note-number sequences in the log"
            : "Показывать последовательности MIDI-номеров в журнале";
        _equalizeChannelDurationsCheckBox.Text = english
            ? "Pad all MIDI channels to the same duration"
            : "Дополнять все MIDI-каналы до одинаковой длительности";
        _controlRecordingCheckBox.Text = english
            ? "Start and stop recording with playback"
            : "Запускать и останавливать запись вместе с воспроизведением";

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 2
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var card = new CardPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            CornerRadius = 14
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 12,
            Margin = Padding.Empty
        };
        for (var row = 0; row < content.RowCount; row++)
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(CreateSectionLabel(english ? "Playback" : "Воспроизведение"), 0, 0);
        _changeInstrumentsCheckBox.Margin = new Padding(0, 8, 0, 8);
        content.Controls.Add(_changeInstrumentsCheckBox, 0, 1);
        content.Controls.Add(CreateValueRow(
            english ? "Note delay, ms:" : "Задержка ноты, мс:",
            _delayInput), 0, 2);
        content.Controls.Add(CreateCheckBoxValueRow(
            _endPauseCheckBox,
            _endPauseInput,
            english ? "ms:" : "мс:"), 0, 3);

        var conversionLabel = CreateSectionLabel(english ? "MIDI conversion" : "Конвертация MIDI");
        conversionLabel.Margin = new Padding(0, 18, 0, 0);
        content.Controls.Add(conversionLabel, 0, 4);
        content.Controls.Add(CreateCheckBoxValueRow(_customBpmCheckBox, _bpmInput, "BPM:"), 0, 5);
        _showWarningsCheckBox.Margin = new Padding(0, 8, 0, 0);
        content.Controls.Add(_showWarningsCheckBox, 0, 6);
        _showNoteSequenceCheckBox.Margin = new Padding(0, 8, 0, 0);
        content.Controls.Add(_showNoteSequenceCheckBox, 0, 7);
        _equalizeChannelDurationsCheckBox.Margin = new Padding(0, 8, 0, 0);
        content.Controls.Add(_equalizeChannelDurationsCheckBox, 0, 8);

        var recordingLabel = CreateSectionLabel(english ? "Recording" : "Запись");
        recordingLabel.Margin = new Padding(0, 18, 0, 0);
        content.Controls.Add(recordingLabel, 0, 9);
        _controlRecordingCheckBox.Margin = new Padding(0, 8, 0, 8);
        content.Controls.Add(_controlRecordingCheckBox, 0, 10);
        content.Controls.Add(CreateValueRow(
            english ? "Recording key:" : "Клавиша записи:",
            _recordingHotkeyCombo), 0, 11);
        card.Controls.Add(content);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 16, 0, 0)
        };
        var saveButton = new AppButton
        {
            Text = english ? "Save" : "Сохранить",
            ButtonStyle = AppButtonStyle.Primary,
            Width = 120
        };
        var cancelButton = new AppButton
        {
            Text = english ? "Cancel" : "Отмена",
            ButtonStyle = AppButtonStyle.Secondary,
            Width = 120,
            Margin = new Padding(0, 0, 10, 0),
            DialogResult = DialogResult.Cancel
        };
        saveButton.Click += (_, _) =>
        {
            Values = new AdvancedSettingsValues(
                (int)_delayInput.Value,
                _changeInstrumentsCheckBox.Checked,
                _endPauseCheckBox.Checked,
                (int)_endPauseInput.Value,
                _customBpmCheckBox.Checked,
                _bpmInput.Value,
                !_showWarningsCheckBox.Checked,
                _showNoteSequenceCheckBox.Checked,
                _equalizeChannelDurationsCheckBox.Checked,
                _controlRecordingCheckBox.Checked,
                _recordingHotkeyCombo.SelectedItem as string ?? "Numpad7");
            DialogResult = DialogResult.OK;
            Close();
        };
        actions.Controls.Add(saveButton);
        actions.Controls.Add(cancelButton);

        root.Controls.Add(card, 0, 0);
        root.Controls.Add(actions, 0, 1);
        Controls.Add(root);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        void UpdateEnabledControls()
        {
            _bpmInput.Enabled = _customBpmCheckBox.Checked;
            _endPauseInput.Enabled = _endPauseCheckBox.Checked;
            _recordingHotkeyCombo.Enabled = _controlRecordingCheckBox.Checked;
            ThemeApplicator.Apply(this, palette);
        }

        _customBpmCheckBox.CheckedChanged += (_, _) => UpdateEnabledControls();
        _endPauseCheckBox.CheckedChanged += (_, _) => UpdateEnabledControls();
        _controlRecordingCheckBox.CheckedChanged += (_, _) => UpdateEnabledControls();
        UpdateEnabledControls();
    }

    private static Label CreateSectionLabel(string text) => new()
    {
        AutoSize = true,
        Text = text,
        Font = new Font("Segoe UI Semibold", 12F),
        Margin = Padding.Empty
    };

    private static Control CreateValueRow(string labelText, Control valueControl)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        var label = new Label
        {
            AutoSize = true,
            Text = labelText,
            Width = 190,
            Margin = new Padding(0, 7, 12, 0)
        };
        valueControl.Margin = Padding.Empty;
        row.Controls.Add(label);
        row.Controls.Add(valueControl);
        return row;
    }

    private static Control CreateCheckBoxValueRow(CheckBox checkBox, Control valueControl, string valueLabel)
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 8, 0, 0)
        };
        checkBox.Width = 250;
        checkBox.Margin = new Padding(0, 6, 12, 0);
        var label = new Label
        {
            AutoSize = true,
            Text = valueLabel,
            Margin = new Padding(0, 7, 8, 0)
        };
        valueControl.Margin = Padding.Empty;
        row.Controls.Add(checkBox);
        row.Controls.Add(label);
        row.Controls.Add(valueControl);
        return row;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTitleBar.Apply(this, _palette.IsDark);
    }
}
