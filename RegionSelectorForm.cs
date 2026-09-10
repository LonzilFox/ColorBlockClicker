using System.Drawing.Drawing2D;

namespace ColorBlockClicker;

internal sealed class RegionSelectorForm : Form
{
    private Point _start;
    private Point _current;
    private bool _dragging;

    public Rectangle SelectedRegion { get; private set; }

    public RegionSelectorForm()
    {
        Rectangle virtualScreen = SystemInformation.VirtualScreen;
        StartPosition = FormStartPosition.Manual;
        Bounds = virtualScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = 0.34;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        MouseDown += OnSelectorMouseDown;
        MouseMove += OnSelectorMouseMove;
        MouseUp += OnSelectorMouseUp;
        KeyDown += OnSelectorKeyDown;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        string tip = "拖动框选完整色块方阵    ·    Esc 取消";
        using var tipFont = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold, GraphicsUnit.Point);
        Size tipSize = TextRenderer.MeasureText(tip, tipFont, new Size(1200, 80), TextFormatFlags.NoPadding);
        Rectangle tipRect = new(24, 22, tipSize.Width + 32, Math.Max(46, tipSize.Height + 20));

        using (GraphicsPath path = RoundedRect(tipRect, 12))
        using (var fill = new SolidBrush(Color.FromArgb(220, 20, 24, 31)))
        using (var border = new Pen(Color.FromArgb(110, 72, 215, 165), 1F))
        {
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        }

        Rectangle tipTextRect = Rectangle.Inflate(tipRect, -16, -8);
        TextRenderer.DrawText(
            e.Graphics,
            tip,
            tipFont,
            tipTextRect,
            Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

        if (!_dragging)
        {
            return;
        }

        Rectangle rect = Normalize(_start, _current);
        using var fillSelection = new SolidBrush(Color.FromArgb(30, 72, 215, 165));
        using var pen = new Pen(Color.FromArgb(72, 215, 165), 3F);
        e.Graphics.FillRectangle(fillSelection, rect);
        e.Graphics.DrawRectangle(pen, rect);

        string sizeText = $"{rect.Width} × {rect.Height}";
        using var sizeFont = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        Size size = TextRenderer.MeasureText(sizeText, sizeFont, new Size(300, 50), TextFormatFlags.NoPadding);
        Rectangle badge = new(
            rect.Left + 8,
            Math.Max(82, rect.Top + 8),
            size.Width + 20,
            size.Height + 12);

        using (GraphicsPath badgePath = RoundedRect(badge, 8))
        using (var badgeFill = new SolidBrush(Color.FromArgb(225, 20, 24, 31)))
        {
            e.Graphics.FillPath(badgeFill, badgePath);
        }

        TextRenderer.DrawText(
            e.Graphics,
            sizeText,
            sizeFont,
            Rectangle.Inflate(badge, -10, -6),
            Color.White,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private void OnSelectorMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _start = e.Location;
        _current = e.Location;
        _dragging = true;
        Capture = true;
        Invalidate();
    }

    private void OnSelectorMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _current = e.Location;
        Invalidate();
    }

    private void OnSelectorMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging || e.Button != MouseButtons.Left)
        {
            return;
        }

        _current = e.Location;
        _dragging = false;
        Capture = false;

        Rectangle local = Normalize(_start, _current);
        if (local.Width < 80 || local.Height < 80)
        {
            Invalidate();
            return;
        }

        SelectedRegion = new Rectangle(
            Bounds.Left + local.Left,
            Bounds.Top + local.Top,
            local.Width,
            local.Height);

        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnSelectorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        DialogResult = DialogResult.Cancel;
        Close();
    }

    private static Rectangle Normalize(Point a, Point b)
    {
        int left = Math.Min(a.X, b.X);
        int top = Math.Min(a.Y, b.Y);
        int right = Math.Max(a.X, b.X);
        int bottom = Math.Max(a.Y, b.Y);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(2, radius * 2);
        Rectangle arc = new(rect.X, rect.Y, d, d);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - d;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - d;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
