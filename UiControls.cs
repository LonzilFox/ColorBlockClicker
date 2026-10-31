using System.Drawing.Drawing2D;

namespace ColorBlockClicker;

internal static class UiTheme
{
    internal static readonly Color Window = Color.FromArgb(18, 21, 27);
    internal static readonly Color TitleBar = Color.FromArgb(22, 26, 33);
    internal static readonly Color Card = Color.FromArgb(29, 34, 43);
    internal static readonly Color CardBorder = Color.FromArgb(67, 77, 92);
    internal static readonly Color Text = Color.FromArgb(236, 240, 246);
    internal static readonly Color MutedText = Color.FromArgb(151, 161, 177);
    internal static readonly Color Accent = Color.FromArgb(72, 215, 165);
    internal static readonly Color AccentHover = Color.FromArgb(86, 226, 177);
    internal static readonly Color AccentPressed = Color.FromArgb(57, 188, 143);
    internal static readonly Color NeutralButton = Color.FromArgb(49, 57, 70);
    internal static readonly Color NeutralHover = Color.FromArgb(60, 69, 84);
    internal static readonly Color Stop = Color.FromArgb(191, 74, 86);
    internal static readonly Color StopHover = Color.FromArgb(211, 86, 98);
    internal static readonly Color Disabled = Color.FromArgb(58, 63, 73);
    internal static readonly Color Running = Color.FromArgb(72, 215, 165);
    internal static readonly Color Idle = Color.FromArgb(126, 136, 151);
    internal static readonly Color Warning = Color.FromArgb(239, 193, 92);
}

// Provides flicker-free painting for the viewport and measured content host.
internal sealed class SmoothPanel : Panel
{
    public SmoothPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }
}

internal sealed class RoundedPanel : Panel
{
    public int CornerRadius { get; set; } = 12;
    public Color FillColor { get; set; } = UiTheme.Card;
    public Color BorderColor { get; set; } = UiTheme.CardBorder;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        Padding = new Padding(14);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        // Inset the *entire* stroke: drawing at y=0 clips the upper half of
        // the antialiased top border, which made the first card look covered.
        Rectangle rect = new(2, 2, Width - 4, Height - 4);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using GraphicsPath path = CreateRoundedPath(rect, CornerRadius);
        using var fill = new SolidBrush(FillColor);
        using var border = new Pen(BorderColor, 1.5F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        Rectangle arc = new(rect.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ModernButton : Button
{
    private bool _hovered;
    private bool _pressed;

    public Color NormalColor { get; set; } = UiTheme.NeutralButton;
    public Color HoverColor { get; set; } = UiTheme.NeutralHover;
    public Color PressedColor { get; set; } = UiTheme.NeutralButton;
    public Color ForegroundColor { get; set; } = UiTheme.Text;
    public int CornerRadius { get; set; } = 10;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Height = 42;
        TabStop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
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

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle rect = ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;

        Color fill = !Enabled
            ? UiTheme.Disabled
            : _pressed
                ? PressedColor
                : _hovered ? HoverColor : NormalColor;

        using GraphicsPath path = CreateRoundedPath(rect, CornerRadius);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);

        Color textColor = Enabled ? ForegroundColor : Color.FromArgb(125, 132, 143);
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            rect,
            textColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            Rectangle focus = Rectangle.Inflate(rect, -4, -4);
            ControlPaint.DrawFocusRectangle(pevent.Graphics, focus, textColor, fill);
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
        Rectangle arc = new(rect.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class MetricView : Panel
{
    private readonly Label _valueLabel;
    private readonly Label _captionLabel;

    public int PreferredMetricHeight => Math.Max(55,
        _captionLabel.Font.Height + _valueLabel.Font.Height + 12);

    public MetricView(string caption)
    {
        BackColor = Color.Transparent;
        Margin = new Padding(0);
        Padding = new Padding(2, 0, 2, 0);
        Height = 55;

        _captionLabel = new Label
        {
            Text = caption,
            ForeColor = UiTheme.MutedText,
            Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = Math.Max(22, Font.Height + 3),
            TextAlign = ContentAlignment.BottomCenter
        };

        _valueLabel = new Label
        {
            Text = "-",
            ForeColor = UiTheme.Text,
            Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter
        };

        Controls.Add(_valueLabel);
        Controls.Add(_captionLabel);
        _captionLabel.Height = Math.Max(22, _captionLabel.Font.Height + 4);
    }

    public void SetValue(string value)
    {
        if (_valueLabel.Text != value) _valueLabel.Text = value;
    }
}

internal sealed class StatusDot : Control
{
    private Color _dotColor = UiTheme.Idle;

    public Color DotColor
    {
        get => _dotColor;
        set
        {
            if (_dotColor == value) return;
            _dotColor = value;
            Invalidate();
        }
    }

    public StatusDot()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(10, 10);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(_dotColor);
        e.Graphics.FillEllipse(brush, 0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
    }
}
