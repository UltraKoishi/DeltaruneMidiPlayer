using Microsoft.Win32;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DeltaruneMidiPlayer.UI;

internal enum ThemeMode
{
    System,
    Light,
    Dark
}

internal enum ThemeRole
{
    Default,
    Muted,
    Log,
    StatusAccent
}

internal enum AppButtonStyle
{
    Primary,
    Success,
    Secondary,
    Ghost
}

internal sealed record ThemePalette(
    bool IsDark,
    Color Window,
    Color Surface,
    Color SurfaceAlt,
    Color Input,
    Color Border,
    Color Text,
    Color Muted,
    Color Accent,
    Color AccentHover,
    Color AccentPressed,
    Color Success,
    Color Warning,
    Color Danger,
    Color LogBackground)
{
    public static ThemePalette Light { get; } = new(
        false,
        Color.FromArgb(245, 247, 252),
        Color.White,
        Color.FromArgb(239, 242, 249),
        Color.FromArgb(249, 250, 253),
        Color.FromArgb(214, 220, 233),
        Color.FromArgb(24, 29, 48),
        Color.FromArgb(101, 111, 134),
        Color.FromArgb(108, 92, 231),
        Color.FromArgb(92, 76, 216),
        Color.FromArgb(77, 63, 191),
        Color.FromArgb(25, 158, 113),
        Color.FromArgb(215, 137, 36),
        Color.FromArgb(207, 62, 88),
        Color.FromArgb(248, 249, 252));

    public static ThemePalette Dark { get; } = new(
        true,
        Color.FromArgb(10, 15, 29),
        Color.FromArgb(17, 25, 44),
        Color.FromArgb(24, 34, 56),
        Color.FromArgb(12, 20, 36),
        Color.FromArgb(42, 54, 80),
        Color.FromArgb(241, 244, 253),
        Color.FromArgb(165, 175, 198),
        Color.FromArgb(139, 124, 255),
        Color.FromArgb(157, 143, 255),
        Color.FromArgb(118, 102, 224),
        Color.FromArgb(55, 199, 146),
        Color.FromArgb(243, 184, 90),
        Color.FromArgb(255, 107, 131),
        Color.FromArgb(8, 15, 29));
}

internal static class ThemeCatalog
{
    public static ThemePalette Resolve(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => ThemePalette.Light,
        ThemeMode.Dark => ThemePalette.Dark,
        _ => IsSystemDarkMode() ? ThemePalette.Dark : ThemePalette.Light
    };

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class CardPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = ThemePalette.Light.Border;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 12;

    public CardPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        Margin = Padding.Empty;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Surface;
        ForeColor = palette.Text;
        BorderColor = palette.Border;
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRegion();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 2 || Height < 2)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = DrawingUtilities.CreateRoundedRectangle(
            new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F), CornerRadius);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawPath(pen, path);
    }

    private void UpdateRegion()
    {
        if (Width <= 0 || Height <= 0)
            return;

        using var path = DrawingUtilities.CreateRoundedRectangle(
            new RectangleF(0, 0, Width, Height), CornerRadius);
        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }
}

internal sealed class AppButton : Button
{
    private bool _hovered;
    private bool _pressed;
    private ThemePalette _palette = ThemePalette.Light;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public AppButtonStyle ButtonStyle { get; set; } = AppButtonStyle.Secondary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    public AppButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        UpdateStyles();
        AutoSize = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI Semibold", 9.5F);
        Height = 40;
        Padding = new Padding(12, 0, 12, 0);
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        ForeColor = ResolveForeground();
        BackColor = Color.Transparent;
        Invalidate();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Invalidate();
    }

    protected override void OnParentBackColorChanged(EventArgs e)
    {
        base.OnParentBackColorChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? _palette.Window);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        var bounds = new RectangleF(0.5F, 0.5F, Width - 1F, Height - 1F);
        using var path = DrawingUtilities.CreateRoundedRectangle(bounds, CornerRadius);
        using var brush = new SolidBrush(ResolveBackground());
        e.Graphics.FillPath(brush, path);

        if (ButtonStyle is AppButtonStyle.Secondary or AppButtonStyle.Ghost)
        {
            using var border = new Pen(_hovered ? _palette.Accent : _palette.Border);
            e.Graphics.DrawPath(border, path);
        }

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ResolveForeground(), flags);

        if (Focused && ShowFocusCues)
        {
            var focusRectangle = Rectangle.Inflate(ClientRectangle, -4, -4);
            ControlPaint.DrawFocusRectangle(e.Graphics, focusRectangle, ResolveForeground(), Color.Transparent);
        }
    }

    private Color ResolveBackground()
    {
        if (!Enabled)
            return _palette.SurfaceAlt;

        return ButtonStyle switch
        {
            AppButtonStyle.Primary => _pressed ? _palette.AccentPressed : _hovered ? _palette.AccentHover : _palette.Accent,
            AppButtonStyle.Success => _pressed
                ? ControlPaint.Dark(_palette.Success, 0.12F)
                : _hovered ? ControlPaint.Light(_palette.Success, 0.08F) : _palette.Success,
            AppButtonStyle.Ghost => _hovered ? _palette.SurfaceAlt : Parent?.BackColor ?? _palette.Surface,
            _ => _pressed ? _palette.Input : _hovered ? _palette.SurfaceAlt : _palette.Surface
        };
    }

    private Color ResolveForeground()
    {
        if (!Enabled)
            return _palette.Muted;
        return ButtonStyle is AppButtonStyle.Primary or AppButtonStyle.Success ? Color.White : _palette.Text;
    }
}

internal sealed class AppComboBox : ComboBox
{
    private ThemePalette _palette = ThemePalette.Light;

    public AppComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 26;
        IntegralHeight = false;
        DropDownHeight = 240;
    }

    public void ApplyTheme(ThemePalette palette)
    {
        _palette = palette;
        BackColor = palette.Input;
        ForeColor = palette.Text;
        Invalidate();
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0)
            return;

        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? _palette.Accent : _palette.Input);
        e.Graphics.FillRectangle(background, e.Bounds);
        var textColor = selected ? Color.White : _palette.Text;
        var textBounds = Rectangle.Inflate(e.Bounds, -8, 0);
        TextRenderer.DrawText(
            e.Graphics,
            GetItemText(Items[e.Index]),
            Font,
            textBounds,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
    }

    protected override void WndProc(ref Message message)
    {
        const int WmPaint = 0x000F;
        const int WmPrint = 0x0317;
        const int WmPrintClient = 0x0318;
        base.WndProc(ref message);

        if (message.Msg == WmPaint)
        {
            using var graphics = Graphics.FromHwnd(Handle);
            DrawChrome(graphics);
        }
        else if ((message.Msg == WmPrint || message.Msg == WmPrintClient) && message.WParam != IntPtr.Zero)
        {
            using var graphics = Graphics.FromHdc(message.WParam);
            DrawChrome(graphics);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    private void DrawChrome(Graphics graphics)
    {
        var scale = DeviceDpi / 96F;
        var arrowWidth = Math.Max(18, (int)Math.Round(24 * scale));
        var arrowBounds = new Rectangle(Math.Max(0, Width - arrowWidth - 1), 1, arrowWidth, Math.Max(0, Height - 2));
        using var arrowBackground = new SolidBrush(_palette.Input);
        graphics.FillRectangle(arrowBackground, arrowBounds);

        var centerX = arrowBounds.Left + arrowBounds.Width / 2F;
        var centerY = arrowBounds.Top + arrowBounds.Height / 2F;
        var halfWidth = Math.Max(3F, 3.5F * scale);
        var halfHeight = Math.Max(2F, 2.5F * scale);
        using var arrowPen = new Pen(_palette.Muted, Math.Max(1F, scale))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawLines(arrowPen,
        [
            new PointF(centerX - halfWidth, centerY - halfHeight),
            new PointF(centerX, centerY + halfHeight),
            new PointF(centerX + halfWidth, centerY - halfHeight)
        ]);

        using var border = new Pen(Focused ? _palette.Accent : _palette.Border);
        graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }
}

internal static class ThemeApplicator
{
    public static void Apply(Control root, ThemePalette palette) =>
        ApplyRecursive(root, palette, palette.Window);

    private static void ApplyRecursive(Control control, ThemePalette palette, Color inheritedBackground)
    {
        var childBackground = inheritedBackground;

        switch (control)
        {
            case Form:
                control.BackColor = palette.Window;
                control.ForeColor = palette.Text;
                childBackground = palette.Window;
                break;
            case CardPanel card:
                card.ApplyTheme(palette);
                childBackground = palette.Surface;
                break;
            case AppButton button:
                button.ApplyTheme(palette);
                break;
            case AppComboBox comboBox:
                comboBox.ApplyTheme(palette);
                break;
            case TextBox textBox:
                textBox.BackColor = control.Tag is ThemeRole.Log ? palette.LogBackground : palette.Input;
                textBox.ForeColor = palette.Text;
                NativeControlTheme.ApplyScrollBarTheme(textBox, palette.IsDark);
                break;
            case NumericUpDown numericUpDown:
                numericUpDown.BackColor = palette.Input;
                numericUpDown.ForeColor = numericUpDown.Enabled ? palette.Text : palette.Muted;
                break;
            case Label label:
                label.BackColor = Color.Transparent;
                if (control.Tag is not ThemeRole.StatusAccent)
                    label.ForeColor = control.Tag is ThemeRole.Muted ? palette.Muted : palette.Text;
                break;
            case CheckBox checkBox:
                checkBox.BackColor = inheritedBackground;
                checkBox.ForeColor = checkBox.Enabled ? palette.Text : palette.Muted;
                checkBox.UseVisualStyleBackColor = false;
                break;
            case PictureBox pictureBox:
                pictureBox.BackColor = Color.Transparent;
                break;
            case Panel or TableLayoutPanel or FlowLayoutPanel:
                control.BackColor = inheritedBackground;
                control.ForeColor = palette.Text;
                break;
            default:
                control.ForeColor = palette.Text;
                break;
        }

        foreach (Control child in control.Controls)
            ApplyRecursive(child, palette, childBackground);
    }
}

internal static class NativeControlTheme
{
    private const int WmThemeChanged = 0x031A;
    private static readonly ConditionalWeakTable<Control, ThemeState> States = new();

    public static void ApplyScrollBarTheme(Control control, bool dark)
    {
        var state = States.GetOrCreateValue(control);
        state.Dark = dark;
        if (!state.HandleCreatedHooked)
        {
            control.HandleCreated += Control_HandleCreated;
            state.HandleCreatedHooked = true;
        }

        ApplyToHandle(control, dark);
    }

    private static void Control_HandleCreated(object? sender, EventArgs e)
    {
        if (sender is Control control && States.TryGetValue(control, out var state))
            ApplyToHandle(control, state.Dark);
    }

    private static void ApplyToHandle(Control control, bool dark)
    {
        if (!control.IsHandleCreated || !OperatingSystem.IsWindows())
            return;

        try
        {
            SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
            SendMessage(control.Handle, WmThemeChanged, IntPtr.Zero, IntPtr.Zero);
            control.Invalidate(true);
        }
        catch
        {
            // Custom control colors still work if native theme APIs are unavailable.
        }
    }

    private sealed class ThemeState
    {
        public bool Dark { get; set; }
        public bool HandleCreatedHooked { get; set; }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr handle, string? subAppName, string? subIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
}

internal static class NativeTitleBar
{
    private const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmUseImmersiveDarkMode = 20;

    public static void Apply(Form form, bool dark)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) || !form.IsHandleCreated)
            return;

        var enabled = dark ? 1 : 0;
        try
        {
            if (DwmSetWindowAttribute(
                    form.Handle,
                    DwmUseImmersiveDarkMode,
                    ref enabled,
                    sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(
                    form.Handle,
                    DwmUseImmersiveDarkModeBefore20H1,
                    ref enabled,
                    sizeof(int));
            }
        }
        catch
        {
            // Older Windows builds may not expose this DWM attribute.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}

internal static class DrawingUtilities
{
    public static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = Math.Max(1F, Math.Min(radius * 2F, Math.Min(bounds.Width, bounds.Height)));
        var arc = new RectangleF(bounds.X, bounds.Y, diameter, diameter);
        var path = new GraphicsPath();
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
