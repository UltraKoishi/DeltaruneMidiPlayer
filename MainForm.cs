using System.Diagnostics;
using DeltaruneMidiPlayer.Models;
using DeltaruneMidiPlayer.Services;
using DeltaruneMidiPlayer.UI;

namespace DeltaruneMidiPlayer;

public sealed class MainForm : Form
{
    private enum UiLanguage { Russian, English }

    private static readonly string[] RecordingHotkeys =
    [
        "Numpad0", "Numpad1", "Numpad2", "Numpad3", "Numpad4",
        "Numpad5", "Numpad6", "Numpad7", "Numpad8", "Numpad9",
        "F1", "F2", "F3", "F4", "F5", "F6",
        "F7", "F8", "F11", "F12"
    ];

    private readonly string _midiDirectory = Path.Combine(AppContext.BaseDirectory, "MIDI");
    private readonly string _noteSequencesDirectory = Path.Combine(AppContext.BaseDirectory, "NoteSequences");
    private readonly string _ahkTemplatePath = Path.Combine(AppContext.BaseDirectory, "Ahk", "PlayerTemplate.ahk");

    private readonly AppComboBox _languageCombo = new() { Width = 122 };
    private readonly AppComboBox _themeCombo = new() { Width = 132 };
    private readonly AppComboBox _midiCombo = new() { Dock = DockStyle.Fill };
    private readonly AppComboBox _sequenceCombo = new() { Dock = DockStyle.Fill };
    private readonly AppComboBox _channelCombo = new() { Dock = DockStyle.Fill };
    private readonly TextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        MaxLength = 0,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        Font = new Font("Consolas", 9.5F),
        Tag = ThemeRole.Log
    };

    private readonly Label _titleLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 21F) };
    private readonly Label _subtitleLabel = new() { AutoSize = true, Tag = ThemeRole.Muted };
    private readonly Label _languageLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), Tag = ThemeRole.Muted };
    private readonly Label _themeLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5F), Tag = ThemeRole.Muted };
    private readonly Label _midiLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10F) };
    private readonly Label _sequenceLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10F) };
    private readonly Label _channelLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10F) };
    private readonly Label _logLabel = new() { AutoSize = true, Font = new Font("Segoe UI Semibold", 10F) };
    private readonly Label _dropHintLabel = new() { AutoSize = true, Tag = ThemeRole.Muted };
    private readonly Label _statusDot = new() { AutoSize = true, Font = new Font("Segoe UI", 12F), Tag = ThemeRole.StatusAccent };
    private readonly Label _statusLabel = new() { AutoSize = true };
    private readonly Label _playerHint = new() { AutoSize = true, Tag = ThemeRole.Muted, MaximumSize = new Size(760, 0) };
    private readonly Label _versionLabel = new() { AutoSize = true, Tag = ThemeRole.Muted, Anchor = AnchorStyles.Right };

    private readonly AppButton _refreshMidiButton = CreateSecondaryButton();
    private readonly AppButton _refreshSequencesButton = CreateSecondaryButton();
    private readonly AppButton _convertButton = CreatePrimaryButton(AppButtonStyle.Primary);
    private readonly AppButton _playButton = CreatePrimaryButton(AppButtonStyle.Success);
    private readonly AppButton _advancedButton = CreateSecondaryButton();
    private readonly AppButton _copyLogButton = CreateCompactButton();
    private readonly AppButton _clearLogButton = CreateCompactButton();
    private readonly AppButton _aboutButton = CreateCompactButton();
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 8000, InitialDelay = 450, ReshowDelay = 100 };
    private readonly AppSettings _settings;

    private UiLanguage _language = UiLanguage.Russian;
    private ThemeMode _themeMode = ThemeMode.System;
    private ThemePalette _palette = ThemePalette.Light;
    private bool _updatingSelectors;
    private Color _statusSignal = Color.SeaGreen;
    private Image? _brandImage;
    private Process? _ahkProcess;
    private string? _ahkStopRequestPath;
    private string? _draggedMidiPath;
    private IReadOnlyList<SequenceChannelInfo> _sequenceChannels = [];
    private int _noteDelayMs = 50;
    private bool _useCustomBpm;
    private decimal _customBpm = 120;
    private bool _suppressSimultaneousWarnings;
    private bool _changeInstruments;
    private bool _controlRecording;
    private string _recordingHotkey = "Numpad7";

    public MainForm()
    {
        _settings = AppSettingsStore.Load();
        _language = _settings.Language == "en" ? UiLanguage.English : UiLanguage.Russian;
        _themeMode = _settings.Theme;
        _controlRecording = _settings.ControlRecording;
        _recordingHotkey = RecordingHotkeys.Contains(
            _settings.RecordingHotkey,
            StringComparer.OrdinalIgnoreCase)
            ? RecordingHotkeys.First(key => key.Equals(_settings.RecordingHotkey, StringComparison.OrdinalIgnoreCase))
            : "Numpad7";

        Text = "Deltarune MIDI Player";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 680);
        ClientSize = new Size(_settings.WindowWidth, _settings.WindowHeight);
        Font = new Font("Segoe UI", 10F);
        AllowDrop = true;
        DoubleBuffered = true;
        KeyPreview = true;

        try
        {
            var executablePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(executablePath))
                Icon = Icon.ExtractAssociatedIcon(executablePath);
        }
        catch
        {
            // The default icon is acceptable while running under a development host.
        }
        _brandImage = LoadBrandImage();

        Directory.CreateDirectory(_midiDirectory);
        Directory.CreateDirectory(_noteSequencesDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(_ahkTemplatePath)!);

        _languageCombo.Items.AddRange(["Русский", "English"]);
        _themeCombo.Items.AddRange(["System", "Light", "Dark"]);
        _languageCombo.SelectedIndex = _language == UiLanguage.English ? 1 : 0;
        _themeCombo.SelectedIndex = (int)_themeMode;

        Controls.Add(BuildLayout());

        _languageCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingSelectors)
                return;
            _language = _languageCombo.SelectedIndex == 1 ? UiLanguage.English : UiLanguage.Russian;
            ApplyLocalization();
            SaveSettings();
        };
        _themeCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_updatingSelectors || _themeCombo.SelectedIndex < 0)
                return;
            _themeMode = (ThemeMode)_themeCombo.SelectedIndex;
            ApplyTheme();
            SaveSettings();
        };

        _refreshMidiButton.Click += (_, _) => RefreshMidiList();
        _refreshSequencesButton.Click += (_, _) => RefreshSequenceList();
        _sequenceCombo.SelectedIndexChanged += (_, _) => RefreshChannelList();
        _convertButton.Click += GenerateButton_Click;
        _playButton.Click += PlayButton_Click;
        _advancedButton.Click += (_, _) => OpenAdvancedSettings();
        _copyLogButton.Click += (_, _) => CopyLog();
        _clearLogButton.Click += (_, _) =>
        {
            _logBox.Clear();
            SetStatus(_language == UiLanguage.English ? "Activity log cleared." : "Журнал действий очищен.", "●", Color.SeaGreen);
        };
        _aboutButton.Click += (_, _) =>
        {
            using var about = new AboutForm(_palette, _language == UiLanguage.English, Icon, _brandImage);
            about.ShowDialog(this);
        };

        DragEnter += MainForm_DragEnter;
        DragDrop += MainForm_DragDrop;

        ApplyLocalization();
        ApplyTheme();

        Shown += (_, _) =>
        {
            RefreshAllLists();
            if (_settings.Maximized)
                BeginInvoke(new Action(() => WindowState = FormWindowState.Maximized));
        };
        FormClosing += (_, _) =>
        {
            SaveSettings();
            StopAhkProcess();
        };
        FormClosed += (_, _) => _brandImage?.Dispose();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26, 22, 26, 18),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildSourceCard(), 0, 1);
        root.Controls.Add(BuildActionArea(), 0, 2);
        root.Controls.Add(BuildLogArea(), 0, 3);
        root.Controls.Add(BuildStatusBar(), 0, 4);
        return root;
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 20),
            Margin = Padding.Empty
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var brand = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var iconBox = new PictureBox
        {
            Image = _brandImage ?? (Icon ?? SystemIcons.Application).ToBitmap(),
            Size = new Size(56, 56),
            SizeMode = PictureBoxSizeMode.Zoom,
            Margin = new Padding(0, 0, 14, 0),
            Anchor = AnchorStyles.Top
        };
        var textPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left
        };
        textPanel.Controls.Add(_titleLabel);
        textPanel.Controls.Add(_subtitleLabel);
        brand.Controls.Add(iconBox, 0, 0);
        brand.Controls.Add(textPanel, 1, 0);

        var selectors = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        selectors.Controls.Add(CreateSelectorGroup(_themeLabel, _themeCombo));
        selectors.Controls.Add(CreateSelectorGroup(_languageLabel, _languageCombo));
        _aboutButton.Width = 38;
        _aboutButton.Height = 34;
        _aboutButton.Margin = new Padding(10, 18, 0, 0);
        selectors.Controls.Add(_aboutButton);

        panel.Controls.Add(brand, 0, 0);
        panel.Controls.Add(selectors, 1, 0);
        return panel;
    }

    private static Control CreateSelectorGroup(Label label, Control selector)
    {
        var group = new TableLayoutPanel
        {
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(10, 0, 0, 0)
        };
        group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        group.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        label.Margin = new Padding(2, 0, 0, 4);
        selector.Margin = Padding.Empty;
        group.Controls.Add(label, 0, 0);
        group.Controls.Add(selector, 0, 1);
        return group;
    }

    private Control BuildSourceCard()
    {
        var card = CreateCard();
        card.Padding = new Padding(22, 18, 22, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 9,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        layout.Controls.Add(_midiLabel, 0, 0);
        layout.SetColumnSpan(_midiLabel, 3);
        _midiLabel.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(_midiCombo, 0, 1);
        layout.SetColumnSpan(_midiCombo, 2);
        _midiCombo.Margin = Padding.Empty;
        _refreshMidiButton.Margin = new Padding(12, 0, 0, 0);
        layout.Controls.Add(_refreshMidiButton, 2, 1);
        _dropHintLabel.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_dropHintLabel, 0, 2);
        layout.SetColumnSpan(_dropHintLabel, 3);

        layout.Controls.Add(new Panel { Height = 16, AutoSize = false }, 0, 3);

        layout.Controls.Add(_sequenceLabel, 0, 4);
        layout.SetColumnSpan(_sequenceLabel, 3);
        _sequenceLabel.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(_sequenceCombo, 0, 5);
        layout.SetColumnSpan(_sequenceCombo, 2);
        _sequenceCombo.Margin = Padding.Empty;
        _refreshSequencesButton.Margin = new Padding(12, 0, 0, 0);
        layout.Controls.Add(_refreshSequencesButton, 2, 5);

        _channelLabel.Margin = new Padding(0, 14, 0, 6);
        layout.Controls.Add(_channelLabel, 0, 6);
        layout.SetColumnSpan(_channelLabel, 3);
        _channelCombo.Margin = Padding.Empty;
        layout.Controls.Add(_channelCombo, 0, 7);
        layout.SetColumnSpan(_channelCombo, 3);

        _playerHint.Margin = new Padding(0, 8, 0, 0);
        layout.Controls.Add(_playerHint, 0, 8);
        layout.SetColumnSpan(_playerHint, 3);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildActionArea()
    {
        var container = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 16, 0, 10)
        };

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _convertButton.Dock = DockStyle.Fill;
        _playButton.Dock = DockStyle.Fill;
        _convertButton.Height = 48;
        _playButton.Height = 48;
        _advancedButton.Height = 48;
        _advancedButton.Width = 170;
        _convertButton.Margin = new Padding(0, 0, 6, 0);
        _playButton.Margin = new Padding(6, 0, 6, 0);
        _advancedButton.Margin = new Padding(6, 0, 0, 0);

        actions.Controls.Add(_convertButton, 0, 0);
        actions.Controls.Add(_playButton, 1, 0);
        actions.Controls.Add(_advancedButton, 2, 0);
        container.Controls.Add(actions);
        return container;
    }

    private Control BuildLogArea()
    {
        var card = CreateCard();
        card.Dock = DockStyle.Fill;
        card.AutoSize = false;
        card.Padding = new Padding(16);
        card.Margin = new Padding(0, 4, 0, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _logLabel.Anchor = AnchorStyles.Left;

        var tools = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right
        };
        _copyLogButton.Width = 112;
        _clearLogButton.Width = 96;
        _copyLogButton.Margin = Padding.Empty;
        _clearLogButton.Margin = new Padding(8, 0, 0, 0);
        tools.Controls.Add(_copyLogButton);
        tools.Controls.Add(_clearLogButton);
        header.Controls.Add(_logLabel, 0, 0);
        header.Controls.Add(tools, 1, 0);

        _logBox.Margin = new Padding(0, 12, 0, 0);
        _logBox.MinimumSize = new Size(0, 92);
        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_logBox, 0, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildStatusBar()
    {
        var bar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(4, 4, 4, 0),
            Margin = Padding.Empty
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var status = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Left
        };
        _statusDot.Margin = new Padding(0, 0, 8, 0);
        _statusLabel.Margin = new Padding(0, 3, 0, 0);
        status.Controls.Add(_statusDot);
        status.Controls.Add(_statusLabel);
        _versionLabel.Margin = new Padding(16, 4, 0, 0);
        bar.Controls.Add(status, 0, 0);
        bar.Controls.Add(_versionLabel, 1, 0);
        return bar;
    }

    private static CardPanel CreateCard() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        CornerRadius = 12,
        Margin = Padding.Empty
    };

    private static AppButton CreatePrimaryButton(AppButtonStyle style) => new()
    {
        Height = 48,
        ButtonStyle = style,
        Font = new Font("Segoe UI Semibold", 10F),
        Margin = Padding.Empty
    };

    private static AppButton CreateSecondaryButton() => new()
    {
        Height = 36,
        Width = 112,
        ButtonStyle = AppButtonStyle.Secondary,
        Margin = Padding.Empty
    };

    private static AppButton CreateCompactButton() => new()
    {
        Height = 32,
        Width = 92,
        ButtonStyle = AppButtonStyle.Ghost,
        Font = new Font("Segoe UI Semibold", 9F),
        Margin = Padding.Empty
    };

    private void ApplyLocalization()
    {
        var en = _language == UiLanguage.English;
        _updatingSelectors = true;
        try
        {
            _themeCombo.BeginUpdate();
            _themeCombo.Items.Clear();
            _themeCombo.Items.AddRange(en
                ? ["System", "Light", "Dark"]
                : ["Системная", "Светлая", "Тёмная"]);
            _themeCombo.SelectedIndex = (int)_themeMode;
            _themeCombo.EndUpdate();
        }
        finally
        {
            _updatingSelectors = false;
        }

        Text = "Deltarune MIDI Player";
        _titleLabel.Text = "Deltarune MIDI Player";
        _subtitleLabel.Text = en ? "MIDI → playable DELTARUNE key sequences" : "MIDI → последовательности клавиш для DELTARUNE";
        _themeLabel.Text = en ? "THEME" : "ТЕМА";
        _languageLabel.Text = en ? "LANGUAGE" : "ЯЗЫК";
        _midiLabel.Text = en ? "MIDI file" : "MIDI-файл";
        _sequenceLabel.Text = en ? "Generated composition" : "Сгенерированная композиция";
        _channelLabel.Text = en ? "MIDI channel" : "MIDI-канал";
        _dropHintLabel.Text = en ? "You can also drag a .mid or .midi file onto this window." : "Также можно перетащить файл .mid или .midi прямо в это окно.";
        _refreshMidiButton.Text = _refreshSequencesButton.Text = en ? "↻  Refresh" : "↻  Обновить";
        _convertButton.Text = en ? "Convert MIDI" : "Конвертировать MIDI";
        _playButton.Text = _ahkProcess is { HasExited: false }
            ? (en ? "Stop playback" : "Остановить")
            : (en ? "Start playback" : "Запустить воспроизведение");
        _advancedButton.Text = en ? "Settings" : "Параметры";
        _logLabel.Text = en ? "Activity log" : "Журнал действий";
        _copyLogButton.Text = en ? "Copy log" : "Копировать";
        _clearLogButton.Text = en ? "Clear" : "Очистить";
        _aboutButton.Text = "i";
        _versionLabel.Text = $"v{Application.ProductVersion}";
        _playerHint.Text = en
            ? "F9 starts the selected channel in the game. F10 closes the AHK script."
            : "F9 запускает выбранный канал в игре. F10 закрывает AHK-скрипт.";
        UpdateChannelComboItems();

        _toolTip.SetToolTip(_themeCombo, en ? "Choose the application color theme" : "Выбрать цветовую тему приложения");
        _toolTip.SetToolTip(_languageCombo, en ? "Change interface language" : "Изменить язык интерфейса");
        _toolTip.SetToolTip(_channelCombo, en ? "Choose the MIDI channel to play" : "Выбрать MIDI-канал для воспроизведения");
        _toolTip.SetToolTip(_aboutButton, en ? "About the application" : "О программе");
        _toolTip.SetToolTip(_copyLogButton, en ? "Copy the full activity log" : "Скопировать весь журнал действий");
        _toolTip.SetToolTip(_clearLogButton, en ? "Clear the activity log" : "Очистить журнал действий");
        _toolTip.SetToolTip(_advancedButton, en ? "Open playback and conversion settings" : "Открыть параметры воспроизведения и конвертации");

        if (_ahkProcess is { HasExited: false })
            SetStatus(en ? "AHK script is running. Press F10 or Stop playback." : "AHK-скрипт запущен. Нажмите F10 или «Остановить».", "●", Color.SeaGreen);
        else if (string.IsNullOrWhiteSpace(_statusLabel.Text))
            SetStatus(en ? "Ready" : "Готово", "●", Color.SeaGreen);

        Invalidate(true);
    }

    private void ApplyTheme()
    {
        _palette = ThemeCatalog.Resolve(_themeMode);
        try
        {
            Application.SetColorMode(_themeMode == ThemeMode.System
                ? SystemColorMode.System
                : _palette.IsDark ? SystemColorMode.Dark : SystemColorMode.Classic);
        }
        catch
        {
            // Custom colors still provide a complete fallback on unsupported Windows builds.
        }
        ThemeApplicator.Apply(this, _palette);
        UpdateStatusColor();
        if (IsHandleCreated)
            NativeTitleBar.Apply(this, _palette.IsDark);
        Invalidate(true);
    }

    private void UpdateStatusColor()
    {
        _statusDot.ForeColor = _statusSignal == Color.Firebrick
            ? _palette.Danger
            : _statusSignal == Color.DarkOrange
                ? _palette.Warning
                : _palette.Success;
    }

    private void CopyLog()
    {
        var en = _language == UiLanguage.English;
        if (string.IsNullOrWhiteSpace(_logBox.Text))
        {
            SetStatus(en ? "The activity log is empty." : "Журнал действий пуст.", "●", Color.DarkOrange);
            return;
        }

        try
        {
            Clipboard.SetText(_logBox.Text);
            SetStatus(en ? "Activity log copied." : "Журнал действий скопирован.", "●", Color.SeaGreen);
        }
        catch (Exception ex)
        {
            SetStatus(en ? "Could not copy the log: " + ex.Message : "Не удалось скопировать журнал: " + ex.Message, "●", Color.Firebrick);
        }
    }

    private void OpenAdvancedSettings()
    {
        var en = _language == UiLanguage.English;
        var values = new AdvancedSettingsValues(
            _noteDelayMs,
            _changeInstruments,
            _useCustomBpm,
            _customBpm,
            _suppressSimultaneousWarnings,
            _controlRecording,
            _recordingHotkey);

        using var dialog = new AdvancedSettingsForm(
            _palette,
            en,
            values,
            RecordingHotkeys,
            Icon);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _noteDelayMs = dialog.Values.NoteDelayMs;
        _changeInstruments = dialog.Values.ChangeInstruments;
        _useCustomBpm = dialog.Values.UseCustomBpm;
        _customBpm = dialog.Values.CustomBpm;
        _suppressSimultaneousWarnings = dialog.Values.SuppressSimultaneousWarnings;
        _controlRecording = dialog.Values.ControlRecording;
        _recordingHotkey = dialog.Values.RecordingHotkey;
        SaveSettings();
        SetStatus(en ? "Settings saved." : "Параметры сохранены.", "●", Color.SeaGreen);
    }

    private void SaveSettings()
    {
        _settings.Language = _language == UiLanguage.English ? "en" : "ru";
        _settings.Theme = _themeMode;
        _settings.ControlRecording = _controlRecording;
        _settings.RecordingHotkey = _recordingHotkey;
        _settings.Maximized = WindowState == FormWindowState.Maximized;
        if (WindowState == FormWindowState.Normal)
        {
            _settings.WindowWidth = ClientSize.Width;
            _settings.WindowHeight = ClientSize.Height;
        }
        AppSettingsStore.Save(_settings);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTitleBar.Apply(this, _palette.IsDark);
    }

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        const int WmSettingChange = 0x001A;
        const int WmThemeChanged = 0x031A;
        if (_themeMode == ThemeMode.System && message.Msg is WmSettingChange or WmThemeChanged && IsHandleCreated)
            BeginInvoke(new Action(ApplyTheme));
    }

    private void GenerateButton_Click(object? sender, EventArgs e)
    {
        ConvertSelectedMidi(true);
    }

    private bool ConvertSelectedMidi(bool showStatus)
    {
        var en = _language == UiLanguage.English;
        string? sourcePath = null;
        string? fileName = null;

        if (!string.IsNullOrWhiteSpace(_draggedMidiPath) && File.Exists(_draggedMidiPath))
        {
            sourcePath = _draggedMidiPath;
            fileName = Path.GetFileName(_draggedMidiPath);
        }
        else if (_midiCombo.SelectedItem is string selected)
        {
            fileName = selected;
            sourcePath = Path.Combine(_midiDirectory, selected);
        }

        if (sourcePath is null || fileName is null)
        {
            SetStatus(en ? "No MIDI file selected." : "MIDI-файл не выбран.", "●", Color.Firebrick);
            return false;
        }

        _logBox.Text = (en ? "Reading: " : "Чтение: ") + sourcePath + Environment.NewLine;

        try
        {
            UseWaitCursor = true;
            _convertButton.Enabled = false;
            SetStatus(en ? "Converting MIDI..." : "Конвертация MIDI...", "●", Color.DarkOrange);

            double? bpmOverride = _useCustomBpm
                ? decimal.ToDouble(_customBpm)
                : null;
            var channelConversions = MidiConverter.ConvertChannels(
                sourcePath,
                _noteDelayMs,
                en,
                bpmOverride,
                _suppressSimultaneousWarnings);

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var outputFileName = $"{baseName}.txt";
            var outputPath = Path.Combine(_noteSequencesDirectory, outputFileName);
            SequenceFile.Save(outputPath, channelConversions);

            var warnings = channelConversions
                .SelectMany(channel => channel.Result.Warnings.Select(warning =>
                    (en ? $"MIDI channel {channel.MidiChannel}: " : $"MIDI-канал {channel.MidiChannel}: ") + warning))
                .ToArray();
            if (warnings.Length == 0)
            {
                _logBox.AppendText(en
                    ? $"No errors or warnings.{Environment.NewLine}"
                    : $"Ошибок и предупреждений нет.{Environment.NewLine}");
            }
            else
            {
                var numberedWarnings = string.Join(
                    Environment.NewLine + Environment.NewLine,
                    warnings.Select((warning, index) => $"{index + 1}. {warning}"));
                _logBox.AppendText(
                    (en ? "Warnings" : "Предупреждения") + $": {warnings.Length}{Environment.NewLine}" +
                    numberedWarnings + Environment.NewLine + Environment.NewLine);
            }

            var summary = channelConversions[0].Result;
            var tempoSummary = summary.TempoChangeCount == 0
                ? $"BPM: {summary.InitialBpm:F2}{Environment.NewLine}"
                : (en
                    ? $"Initial BPM: {summary.InitialBpm:F2}{Environment.NewLine}" +
                      $"BPM range: {summary.MinimumBpm:F2}–{summary.MaximumBpm:F2}{Environment.NewLine}" +
                      $"Tempo changes: {summary.TempoChangeCount}{Environment.NewLine}"
                    : $"Начальный BPM: {summary.InitialBpm:F2}{Environment.NewLine}" +
                      $"Диапазон BPM: {summary.MinimumBpm:F2}–{summary.MaximumBpm:F2}{Environment.NewLine}" +
                      $"Изменений темпа: {summary.TempoChangeCount}{Environment.NewLine}");

            _logBox.AppendText(
                tempoSummary +
                (en
                    ? $"MIDI channels: {channelConversions.Count}{Environment.NewLine}" +
                      $"MIDI events: {channelConversions.Sum(channel => channel.Result.MidiEventCount)}{Environment.NewLine}" +
                      $"Commands: {channelConversions.Sum(channel => channel.Result.Commands.Count)}{Environment.NewLine}" +
                      $"Saved composition: {outputFileName}"
                    : $"MIDI-каналов: {channelConversions.Count}{Environment.NewLine}" +
                      $"MIDI-событий: {channelConversions.Sum(channel => channel.Result.MidiEventCount)}{Environment.NewLine}" +
                      $"Команд: {channelConversions.Sum(channel => channel.Result.Commands.Count)}{Environment.NewLine}" +
                      $"Сохранена композиция: {outputFileName}"));
            _logBox.SelectionStart = 0;
            _logBox.SelectionLength = 0;
            _logBox.ScrollToCaret();

            RefreshSequenceList(outputFileName);
            if (showStatus)
                SetStatus(en
                    ? $"Created one composition with {channelConversions.Count} MIDI channel(s)."
                    : $"Создана одна композиция. MIDI-каналов: {channelConversions.Count}.", "●", Color.SeaGreen);
            return true;
        }
        catch (MidiValidationException ex)
        {
            var heading = en
                ? $"Failed to process MIDI.{Environment.NewLine}Validation errors: {ex.Errors.Count}{Environment.NewLine}{Environment.NewLine}"
                : $"Не удалось обработать MIDI.{Environment.NewLine}Ошибок проверки: {ex.Errors.Count}{Environment.NewLine}{Environment.NewLine}";
            var numberedErrors = string.Join(
                Environment.NewLine + Environment.NewLine,
                ex.Errors.Select((error, index) => $"{index + 1}. {error}"));
            _logBox.Text = heading + numberedErrors;
            if (ex.Warnings.Count > 0)
            {
                var warningHeading = en ? "Warnings" : "Предупреждения";
                var numberedWarnings = string.Join(
                    Environment.NewLine,
                    ex.Warnings.Select((warning, index) => $"{index + 1}. {warning}"));
                _logBox.AppendText(
                    Environment.NewLine + Environment.NewLine +
                    $"{warningHeading}: {ex.Warnings.Count}" + Environment.NewLine +
                    numberedWarnings);
            }
            _logBox.SelectionStart = 0;
            _logBox.ScrollToCaret();
            SetStatus(en
                ? $"MIDI conversion failed: {ex.Errors.Count} error(s) found."
                : $"Ошибка конвертации MIDI: найдено ошибок — {ex.Errors.Count}.",
                "●", Color.Firebrick);
            return false;
        }
        catch (Exception ex)
        {
            _logBox.Text = en
                ? $"Failed to process MIDI.{Environment.NewLine}{ex.GetType().Name}: {ex.Message}"
                : $"Не удалось обработать MIDI.{Environment.NewLine}{ex.GetType().Name}: {ex.Message}";
            SetStatus(en ? "MIDI conversion failed." : "Ошибка конвертации MIDI.", "●", Color.Firebrick);
            return false;
        }
        finally
        {
            _convertButton.Enabled = true;
            UseWaitCursor = false;
        }
    }

    private void PlayButton_Click(object? sender, EventArgs e)
    {
        if (_ahkProcess is { HasExited: false })
        {
            try
            {
                StopAhkProcess();
                SetStatus(_language == UiLanguage.English ? "Stopping AHK script..." : "Остановка AHK-скрипта...", "●", Color.DarkOrange);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, "●", Color.Firebrick);
            }
            return;
        }

        LaunchSelectedSequence();
    }

    private void LaunchSelectedSequence()
    {
        var en = _language == UiLanguage.English;
        if (_sequenceCombo.SelectedItem is not string fileName)
        {
            if (!ConvertSelectedMidi(false) || _sequenceCombo.SelectedItem is not string generated)
            {
                SetStatus(en ? "Select or convert a MIDI file first." : "Сначала выберите или конвертируйте MIDI-файл.", "●", Color.Firebrick);
                return;
            }
            fileName = generated;
        }

        var sequencePath = Path.Combine(_noteSequencesDirectory, fileName);
        var selectedChannel = SelectedSequenceChannel;
        if (selectedChannel is null)
        {
            RefreshChannelList();
            selectedChannel = SelectedSequenceChannel;
        }
        if (selectedChannel is null)
        {
            SetStatus(en ? "The selected composition contains no playable channels." : "В выбранной композиции нет доступных каналов.", "●", Color.Firebrick);
            return;
        }

        if (!File.Exists(_ahkTemplatePath))
        {
            SetStatus((en ? "AHK template not found: " : "Не найден AHK-шаблон: ") + _ahkTemplatePath, "●", Color.Firebrick);
            return;
        }

        try
        {
            var changeInstruments = _changeInstruments ? "1" : "0";
            var language = en ? "en" : "ru";
            var controlRecording = _controlRecording ? "1" : "0";
            var recordingHotkey = _recordingHotkey;
            _ahkStopRequestPath = Path.Combine(
                Path.GetTempPath(),
                $"DeltaruneMidiPlayer-{Guid.NewGuid():N}.stop");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = _ahkTemplatePath,
                Arguments = $"\"{sequencePath}\" {changeInstruments} {language} {_noteDelayMs} " +
                            $"{controlRecording} {recordingHotkey} \"{_ahkStopRequestPath}\" {selectedChannel.MidiChannel}",
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true
            });

            if (process is null)
                throw new InvalidOperationException(en ? "The process was not created." : "Процесс не был создан.");

            _ahkProcess = process;
            _ahkProcess.EnableRaisingEvents = true;
            _ahkProcess.Exited += AhkProcess_Exited;
            _sequenceCombo.Enabled = false;
            _channelCombo.Enabled = false;
            _refreshSequencesButton.Enabled = false;
            ApplyLocalization();
            SetStatus(en
                ? $"Ready to play {fileName}, channel {selectedChannel.MidiChannel}. Press F9 in the game."
                : $"Готов к воспроизведению: {fileName}, канал {selectedChannel.MidiChannel}. Нажмите F9 в игре.", "●", Color.SeaGreen);
        }
        catch (Exception ex)
        {
            _ahkProcess?.Dispose();
            _ahkProcess = null;
            CleanupAhkStopRequest();
            _sequenceCombo.Enabled = true;
            _channelCombo.Enabled = _sequenceChannels.Count > 0;
            _refreshSequencesButton.Enabled = true;
            ApplyLocalization();
            SetStatus(en
                ? "AHK failed to start. Install AutoHotkey v2 and check file associations. " + ex.Message
                : "AHK не запустился. Установите AutoHotkey v2 и проверьте ассоциацию файлов. " + ex.Message,
                "●", Color.Firebrick);
        }
    }

    private void AhkProcess_Exited(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke(new Action(() =>
        {
            _ahkProcess?.Dispose();
            _ahkProcess = null;
            CleanupAhkStopRequest();
            _sequenceCombo.Enabled = true;
            _channelCombo.Enabled = _sequenceChannels.Count > 0;
            _refreshSequencesButton.Enabled = true;
            ApplyLocalization();
            SetStatus(_language == UiLanguage.English
                ? "AHK script closed. Ready for another sequence."
                : "AHK-скрипт закрыт. Можно запускать следующую последовательность.", "●", Color.SeaGreen);
        }));
    }

    private void StopAhkProcess()
    {
        var process = _ahkProcess;
        if (process is null || process.HasExited)
            return;

        var gracefulStopRequested = false;
        if (!string.IsNullOrWhiteSpace(_ahkStopRequestPath))
        {
            try
            {
                File.WriteAllText(_ahkStopRequestPath, "stop");
                gracefulStopRequested = true;
            }
            catch
            {
                // Fall back to terminating the process below.
            }
        }

        if (gracefulStopRequested && process.WaitForExit(1000))
            return;

        process.Kill(true);
    }

    private void CleanupAhkStopRequest()
    {
        if (string.IsNullOrWhiteSpace(_ahkStopRequestPath))
            return;

        try
        {
            if (File.Exists(_ahkStopRequestPath))
                File.Delete(_ahkStopRequestPath);
        }
        catch
        {
            // A stale stop request is harmless because every launch uses a unique path.
        }
        finally
        {
            _ahkStopRequestPath = null;
        }
    }

    private void MainForm_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (files?.Any(IsMidiFile) == true)
                e.Effect = DragDropEffects.Copy;
        }
    }

    private void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
        var source = files?.FirstOrDefault(IsMidiFile);
        if (source is null)
            return;

        try
        {
            var destination = Path.Combine(_midiDirectory, Path.GetFileName(source));
            if (!Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                File.Copy(source, destination, true);

            _draggedMidiPath = null;
            RefreshMidiList(Path.GetFileName(destination));
            SetStatus(_language == UiLanguage.English
                ? $"MIDI file added: {Path.GetFileName(destination)}"
                : $"MIDI-файл добавлен: {Path.GetFileName(destination)}", "●", Color.SeaGreen);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, "●", Color.Firebrick);
        }
    }

    private static bool IsMidiFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mid", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".midi", StringComparison.OrdinalIgnoreCase);
    }

    private void SetStatus(string text, string dot, Color color)
    {
        _statusSignal = color;
        _statusDot.Text = dot;
        _statusLabel.Text = text;
        UpdateStatusColor();
    }

    private void RefreshAllLists()
    {
        RefreshMidiList();
        RefreshSequenceList();
    }

    private void RefreshMidiList(string? select = null)
    {
        var files = Directory.EnumerateFiles(_midiDirectory)
            .Where(IsMidiFile)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        ReplaceComboItems(_midiCombo, files!, select);
    }

    private void RefreshSequenceList(string? select = null)
    {
        var files = Directory.EnumerateFiles(_noteSequencesDirectory, "*.txt")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        ReplaceComboItems(_sequenceCombo, files!, select);
        RefreshChannelList();
    }

    private SequenceChannelInfo? SelectedSequenceChannel =>
        _channelCombo.SelectedIndex >= 0 && _channelCombo.SelectedIndex < _sequenceChannels.Count
            ? _sequenceChannels[_channelCombo.SelectedIndex]
            : null;

    private void RefreshChannelList(int? selectChannel = null)
    {
        if (_sequenceCombo.SelectedItem is not string fileName)
        {
            _sequenceChannels = [];
            UpdateChannelComboItems();
            return;
        }

        try
        {
            var sequencePath = Path.Combine(_noteSequencesDirectory, fileName);
            var previousChannel = selectChannel ?? SelectedSequenceChannel?.MidiChannel;
            _sequenceChannels = SequenceFile.ReadChannels(sequencePath);
            UpdateChannelComboItems(previousChannel);
        }
        catch (Exception ex)
        {
            _sequenceChannels = [];
            UpdateChannelComboItems();
            SetStatus(
                _language == UiLanguage.English
                    ? "Could not read the composition channels: " + ex.Message
                    : "Не удалось прочитать каналы композиции: " + ex.Message,
                "●",
                Color.Firebrick);
        }
    }

    private void UpdateChannelComboItems(int? selectChannel = null)
    {
        var previousChannel = selectChannel ?? SelectedSequenceChannel?.MidiChannel;
        _channelCombo.BeginUpdate();
        _channelCombo.Items.Clear();
        _channelCombo.Items.AddRange(_sequenceChannels.Select(FormatChannelItem).Cast<object>().ToArray());
        _channelCombo.EndUpdate();

        if (previousChannel.HasValue)
        {
            var index = _sequenceChannels
                .Select((channel, index) => (channel, index))
                .FirstOrDefault(item => item.channel.MidiChannel == previousChannel.Value)
                .index;
            if (index >= 0 && index < _sequenceChannels.Count &&
                _sequenceChannels[index].MidiChannel == previousChannel.Value)
            {
                _channelCombo.SelectedIndex = index;
            }
        }

        if (_channelCombo.SelectedIndex < 0 && _channelCombo.Items.Count > 0)
            _channelCombo.SelectedIndex = 0;

        _channelCombo.Enabled = _sequenceChannels.Count > 0 && _ahkProcess is not { HasExited: false };
    }

    private string FormatChannelItem(SequenceChannelInfo channel)
    {
        var en = _language == UiLanguage.English;
        var instrument = channel.MidiChannel == 10
            ? (en ? "Drums" : "Ударные")
            : channel.InstrumentNumber is int instrumentNumber
                ? $"{GeneralMidiInstruments.GetName(instrumentNumber)} (#{instrumentNumber})"
                : (en ? "Instrument not specified" : "Инструмент не указан");
        var noteText = en
            ? $"{channel.NoteCount} {(channel.NoteCount == 1 ? "note" : "notes")}"
            : $"{channel.NoteCount} {RussianNoteWord(channel.NoteCount)}";
        return en
            ? $"Channel {channel.MidiChannel} — {instrument} — {noteText}"
            : $"Канал {channel.MidiChannel} — {instrument} — {noteText}";
    }

    private static string RussianNoteWord(int count)
    {
        var lastTwoDigits = Math.Abs(count) % 100;
        if (lastTwoDigits is >= 11 and <= 14)
            return "нот";

        return (Math.Abs(count) % 10) switch
        {
            1 => "нота",
            2 or 3 or 4 => "ноты",
            _ => "нот"
        };
    }

    private static void ReplaceComboItems(ComboBox comboBox, IEnumerable<string> items, string? select)
    {
        var previous = select ?? comboBox.SelectedItem as string;
        comboBox.BeginUpdate();
        comboBox.Items.Clear();
        comboBox.Items.AddRange(items.Cast<object>().ToArray());
        comboBox.EndUpdate();

        if (previous is not null)
        {
            var index = comboBox.FindStringExact(previous);
            if (index >= 0)
                comboBox.SelectedIndex = index;
        }
        if (comboBox.SelectedIndex < 0 && comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    private static Image? LoadBrandImage()
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("DeltaruneMidiPlayer.AppIcon.png");
            if (stream is null)
                return null;
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }
}
