using DeltaruneMidiPlayer.UI;
using System.Diagnostics;

namespace DeltaruneMidiPlayer;

internal sealed class AboutForm : Form
{
    private const string YouTubeUrl = "https://www.youtube.com/@ultrakoishi";
    private const string GameBananaUrl = "https://gamebanana.com/mods/648107";

    private readonly ThemePalette _palette;

    public AboutForm(ThemePalette palette, bool english, Icon? applicationIcon, Image? brandImage = null)
    {
        _palette = palette;
        Text = english ? "About Deltarune MIDI Player" : "О программе Deltarune MIDI Player";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(580, 580);
        Font = new Font("Segoe UI", 10F);
        Icon = applicationIcon is null ? null : (Icon)applicationIcon.Clone();

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
            Padding = new Padding(26),
            CornerRadius = 14
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Margin = Padding.Empty
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var aboutImage = brandImage is not null
            ? new Bitmap(brandImage)
            : applicationIcon?.ToBitmap();
        if (aboutImage is not null)
        {
            content.Controls.Add(new PictureBox
            {
                Image = aboutImage,
                Size = new Size(72, 72),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 0, 0, 14),
                Anchor = AnchorStyles.Left
            }, 0, 0);
        }

        content.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Deltarune MIDI Player",
            Font = new Font("Segoe UI Semibold", 19F),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 1);
        content.Controls.Add(new Label
        {
            AutoSize = true,
            Text = $"v{Application.ProductVersion}",
            ForeColor = palette.Accent,
            Font = new Font("Segoe UI Semibold", 10F),
            Tag = ThemeRole.StatusAccent,
            Margin = new Padding(0, 0, 0, 14)
        }, 0, 2);
        content.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(476, 0),
            Text = english
                ? "Converts MIDI files into DELTARUNE key sequences and launches playback through AutoHotkey v2."
                : "Преобразует MIDI-файлы в последовательности клавиш для DELTARUNE и запускает воспроизведение через AutoHotkey v2.",
            Tag = ThemeRole.Muted,
            Margin = new Padding(0, 0, 0, 16)
        }, 0, 3);

        var authorRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 14)
        };
        authorRow.Controls.Add(new Label
        {
            AutoSize = true,
            Text = english ? "Created by:" : "Автор:",
            Margin = new Padding(0, 2, 5, 0)
        });
        authorRow.Controls.Add(CreateLink("UltraKoishi", YouTubeUrl, palette));
        content.Controls.Add(authorRow, 0, 4);

        var modPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 16)
        };
        modPanel.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(476, 0),
            Text = english
                ? "For full functionality in DELTARUNE, install the Organ Control Expansion mod:"
                : "Для полного функционала в DELTARUNE установите мод Organ Control Expansion:",
            Margin = new Padding(0, 0, 0, 5)
        });
        modPanel.Controls.Add(CreateLink(
            english ? "Organ Control Expansion on GameBanana" : "Organ Control Expansion на GameBanana",
            GameBananaUrl,
            palette));
        content.Controls.Add(modPanel, 0, 5);

        content.Controls.Add(new Label
        {
            AutoSize = true,
            MaximumSize = new Size(476, 0),
            Text = english
                ? "AutoHotkey v2 is required for playback. This utility is an independent fan-made tool and is not affiliated with the game creators."
                : "Для воспроизведения требуется AutoHotkey v2. Это независимый фанатский инструмент, не связанный с авторами игры.",
            Tag = ThemeRole.Muted
        }, 0, 6);
        card.Controls.Add(content);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 16, 0, 0)
        };
        var closeButton = new AppButton
        {
            Text = english ? "Close" : "Закрыть",
            ButtonStyle = AppButtonStyle.Primary,
            Width = 112,
            DialogResult = DialogResult.OK
        };
        var folderButton = new AppButton
        {
            Text = english ? "App folder" : "Папка приложения",
            ButtonStyle = AppButtonStyle.Secondary,
            Width = 150,
            Margin = new Padding(0, 0, 10, 0)
        };
        folderButton.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{AppContext.BaseDirectory}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                // The about window remains useful even if Explorer cannot be opened.
            }
        };
        actions.Controls.Add(closeButton);
        actions.Controls.Add(folderButton);

        root.Controls.Add(card, 0, 0);
        root.Controls.Add(actions, 0, 1);
        Controls.Add(root);
        AcceptButton = closeButton;
        CancelButton = closeButton;

        ThemeApplicator.Apply(this, palette);
        closeButton.ApplyTheme(palette);
        folderButton.ApplyTheme(palette);

        if (aboutImage is not null)
            FormClosed += (_, _) => aboutImage.Dispose();
    }

    private static LinkLabel CreateLink(string text, string url, ThemePalette palette)
    {
        var link = new LinkLabel
        {
            AutoSize = true,
            Text = text,
            LinkColor = palette.Accent,
            ActiveLinkColor = palette.AccentPressed,
            VisitedLinkColor = palette.Accent,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Margin = Padding.Empty
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // The About window remains useful even if the link cannot be opened.
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeTitleBar.Apply(this, _palette.IsDark);
    }
}
