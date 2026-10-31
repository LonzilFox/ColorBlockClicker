namespace ColorBlockClicker;

// Explicit coordinates and a manually controlled scroll bar avoid implicit
// WinForms AutoScroll focus-jumps, cropped card borders and excess bottom space.
internal sealed class MainForm : Form
{
    private const string AppVersion = "0.0.3";
    private const int HotkeyId = 0x4B21;

    private readonly AppSettings _settings;
    private readonly ToolTip _toolTip;
    private readonly SmoothPanel _contentPanel;
    private readonly SmoothPanel _surface;
    private readonly VScrollBar _verticalScroll;
    private int _contentHeight;
    private int _contentWidth;
    private readonly RoundedPanel _stateCard;
    private readonly RoundedPanel _settingsCard;
    private readonly RoundedPanel _statsCard;
    private readonly TableLayoutPanel _actions;
    private readonly Label _statusLabel;
    private readonly Label _regionLabel;
    private readonly StatusDot _statusDot;
    private readonly ModernButton _selectButton;
    private readonly ModernButton _startButton;
    private readonly ModernButton _stopButton;
    private readonly CheckBox _ultraFastCheckBox;
    private readonly Label _settingsTitle;
    private readonly Label _hotkeyLabel;
    private readonly Label _maxGridLabel;
    private readonly NumericUpDown _maxGridNumeric;
    private readonly MetricView _gridMetric;
    private readonly MetricView _clickMetric;
    private readonly MetricView _differentMetric;
    private readonly MetricView _gridConfidenceMetric;
    private readonly Label _hintLabel;
    private readonly Panel _titleBar;
    private readonly Panel _resizeStrip;
    private Button _collapseButton = null!;

    private ClickEngine? _engine;
    private bool _collapsed;
    private bool _layoutReady;
    private bool _layoutBusy;
    private bool _startupFitted;
    private int _expandedClientHeight;
    private Size _expandedMinimumSize;

    private int Px(int value) => Math.Max(1, (int)Math.Round(value * (DeviceDpi / 96.0)));

    public MainForm()
    {
        _settings = AppSettings.Load();
        SuspendLayout();
        Text = $"异色块极速辅助 v{AppVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(530, 490);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = UiTheme.Window;
        ForeColor = UiTheme.Text;
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Padding = Padding.Empty;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        KeyPreview = true;
        MinimumSize = new Size(440, 220);

        _toolTip = new ToolTip
        {
            InitialDelay = 350,
            ReshowDelay = 150,
            AutoPopDelay = 6000,
            ShowAlways = true
        };

        _titleBar = BuildTitleBar();
        _titleBar.Dock = DockStyle.None;
        // A narrow resize strip keeps the bottom edge close to the last text line.
        _resizeStrip = new Panel { Dock = DockStyle.None, Height = 9, BackColor = UiTheme.Window };
        var grip = new Label
        {
            Dock = DockStyle.Right, Width = 24, Text = "⌟",
            Font = new Font("Segoe UI Symbol", 8F, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = UiTheme.MutedText, TextAlign = ContentAlignment.BottomRight,
            Cursor = Cursors.SizeNWSE, Margin = Padding.Empty
        };
        grip.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left || _collapsed) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN,
                (IntPtr)NativeMethods.HT_BOTTOMRIGHT, IntPtr.Zero);
        };
        _resizeStrip.Controls.Add(grip);

        _contentPanel = new SmoothPanel
        {
            Dock = DockStyle.None,
            AutoScroll = false,
            Padding = Padding.Empty,
            BackColor = UiTheme.Window
        };
        _surface = new SmoothPanel { Location = Point.Empty, BackColor = UiTheme.Window };
        _verticalScroll = new VScrollBar
        {
            Dock = DockStyle.Right, Visible = false,
            SmallChange = 24, TabStop = false
        };
        _verticalScroll.ValueChanged += (_, _) => PositionSurface();
        _contentPanel.Controls.Add(_surface);
        _contentPanel.Controls.Add(_verticalScroll);
        _verticalScroll.BringToFront();

        // Explicit host bounds are important here: WinForms reverse dock order
        // can otherwise place the Fill viewport underneath the title and footer,
        // cropping the first card and making the bottom look too tall.
        Controls.Add(_contentPanel);
        Controls.Add(_resizeStrip);
        Controls.Add(_titleBar);

        _stateCard = new RoundedPanel { Padding = Padding.Empty };
        _statusDot = new StatusDot { DotColor = UiTheme.Idle };
        _statusLabel = MakeLabel("等待开始", 10F, bold: true, color: UiTheme.Text);
        _regionLabel = MakeLabel("选区：未设置", 9.5F, bold: false, color: UiTheme.MutedText);
        _stateCard.Controls.AddRange(new Control[] { _statusDot, _statusLabel, _regionLabel });

        _actions = new TableLayoutPanel
        {
            ColumnCount = 3, RowCount = 1, Margin = Padding.Empty,
            Padding = Padding.Empty, BackColor = UiTheme.Window
        };
        _actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        _actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        _actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));
        _actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _selectButton = new ModernButton
        {
            Text = "框选区域", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 5, 0),
            NormalColor = UiTheme.NeutralButton, HoverColor = UiTheme.NeutralHover,
            PressedColor = Color.FromArgb(42, 49, 60),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
        _selectButton.Click += (_, _) => SelectRegion();
        _startButton = new ModernButton
        {
            Text = "开始  F8", Dock = DockStyle.Fill, Margin = new Padding(5, 0, 5, 0),
            NormalColor = UiTheme.Accent, HoverColor = UiTheme.AccentHover,
            PressedColor = UiTheme.AccentPressed, ForegroundColor = Color.FromArgb(10, 40, 31),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
        _startButton.Click += (_, _) => StartEngine();
        _stopButton = new ModernButton
        {
            Text = "停止", Dock = DockStyle.Fill, Margin = new Padding(5, 0, 0, 0),
            NormalColor = UiTheme.Stop, HoverColor = UiTheme.StopHover,
            PressedColor = Color.FromArgb(164, 61, 72),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            Enabled = false
        };
        _stopButton.Click += (_, _) => StopEngine();
        _actions.Controls.Add(_selectButton, 0, 0);
        _actions.Controls.Add(_startButton, 1, 0);
        _actions.Controls.Add(_stopButton, 2, 0);

        _settingsCard = new RoundedPanel { Padding = Padding.Empty };
        _settingsTitle = MakeLabel("运行设置", 9.5F, bold: true, color: UiTheme.Text);
        _hotkeyLabel = MakeLabel("F8  全局开始 / 停止", 9.5F, bold: false, color: UiTheme.MutedText);
        _ultraFastCheckBox = new CheckBox
        {
            Text = "极速扫描（最高响应速度）", AutoSize = false,
            ForeColor = UiTheme.Text, BackColor = UiTheme.Card,
            FlatStyle = FlatStyle.Flat, Checked = _settings.UltraFast,
            Margin = Padding.Empty, Cursor = Cursors.Hand
        };
        _maxGridLabel = MakeLabel("最大网格", 9.5F, bold: false, color: UiTheme.MutedText);
        _maxGridNumeric = new NumericUpDown
        {
            Minimum = 8, Maximum = 60, Value = Math.Clamp(_settings.MaxGrid, 8, 60),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(23, 28, 36), ForeColor = UiTheme.Text,
            TextAlign = HorizontalAlignment.Center, Margin = Padding.Empty
        };
        _settingsCard.Controls.AddRange(new Control[]
        {
            _settingsTitle, _hotkeyLabel, _ultraFastCheckBox, _maxGridLabel, _maxGridNumeric
        });

        _statsCard = new RoundedPanel { Padding = Padding.Empty };
        _gridMetric = new MetricView("网格");
        _clickMetric = new MetricView("点击");
        _differentMetric = new MetricView("异色置信");
        _gridConfidenceMetric = new MetricView("网格置信");
        _statsCard.Controls.AddRange(new Control[]
        {
            _gridMetric, _clickMetric, _differentMetric, _gridConfidenceMetric
        });

        _hintLabel = MakeLabel(
            "只框完整色块方阵，不要包含上方的得分 / 时间。运行时窗口保持置顶，但开始后会自动避开识别区域。",
            9.5F, bold: false, color: UiTheme.MutedText);

        _surface.Controls.AddRange(new Control[]
        {
            _stateCard, _actions, _settingsCard, _statsCard, _hintLabel
        });
        RegisterMouseWheel(_contentPanel);
        _toolTip.SetToolTip(_selectButton, "拖框选择整个色块网格区域");
        _toolTip.SetToolTip(_startButton, "开始高速识别与自动点击，也可以按 F8");
        _toolTip.SetToolTip(_stopButton, "停止自动识别与点击");
        _toolTip.SetToolTip(_collapseButton, "收起 / 展开悬浮窗");
        _toolTip.SetToolTip(_maxGridNumeric, "允许自动识别的最大 N×N 网格");
        _toolTip.SetToolTip(grip, "拖动以调整窗口大小");

        UpdateRegionLabel();
        SetRunState(false, "等待开始");

        _contentPanel.SizeChanged += (_, _) => LayoutWindow();
        Resize += (_, _) =>
        {
            LayoutHostBounds();
            if (_startupFitted && !_collapsed)
                _expandedClientHeight = ClientSize.Height;
        };
        Shown += (_, _) =>
        {
            FitInitialWindow();
            TryEnableSystemRoundedCorners();
        };
        FormClosing += (_, _) =>
        {
            StopEngine(updateUi: false);
            SaveUiSettings();
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _toolTip.Dispose();
        };

        ResumeLayout(true);
        _layoutReady = true;
        LayoutHostBounds();
    }

    private static Label MakeLabel(string text, float pointSize, bool bold, Color color) => new()
    {
        Text = text, AutoSize = false, ForeColor = color, BackColor = Color.Transparent,
        Font = new Font("Microsoft YaHei UI", pointSize,
            bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point),
        TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty,
        UseCompatibleTextRendering = false
    };

    private Panel BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Top, Height = 52, BackColor = UiTheme.TitleBar,
            Padding = new Padding(15, 0, 8, 0)
        };
        var titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty
        };
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var identity = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoSize = true, BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left, Margin = Padding.Empty
        };
        var title = new Label
        {
            Text = "异色块极速辅助", ForeColor = UiTheme.Text,
            Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true, Margin = new Padding(0, 4, 8, 0)
        };
        var version = new Label
        {
            Text = $"v{AppVersion}", ForeColor = UiTheme.Accent,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true, Padding = new Padding(7, 3, 7, 3),
            Margin = new Padding(0, 2, 0, 0), BackColor = Color.FromArgb(32, 64, 55)
        };
        identity.Controls.Add(title);
        identity.Controls.Add(version);
        _collapseButton = CreateTitleButton("▾");
        _collapseButton.Click += (_, _) => ToggleCollapsed();
        Button closeButton = CreateTitleButton("×");
        closeButton.Font = new Font("Segoe UI", 13F, FontStyle.Regular, GraphicsUnit.Point);
        closeButton.Click += (_, _) => Close();
        titleLayout.Controls.Add(identity, 0, 0);
        titleLayout.Controls.Add(_collapseButton, 1, 0);
        titleLayout.Controls.Add(closeButton, 2, 0);
        titleBar.Controls.Add(titleLayout);
        EnableWindowDrag(titleBar);
        EnableWindowDrag(titleLayout);
        EnableWindowDrag(identity);
        EnableWindowDrag(title);
        EnableWindowDrag(version);
        return titleBar;
    }

    private static Button CreateTitleButton(string text)
    {
        var button = new Button
        {
            Text = text, Width = 38, Height = 38,
            Margin = new Padding(2, 8, 0, 8), FlatStyle = FlatStyle.Flat,
            ForeColor = UiTheme.MutedText, BackColor = UiTheme.TitleBar,
            Cursor = Cursors.Hand, TabStop = false,
            Font = new Font("Segoe UI Symbol", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(41, 47, 58);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(51, 58, 70);
        return button;
    }

    private void EnableWindowDrag(Control control)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN,
                (IntPtr)NativeMethods.HT_CAPTION, IntPtr.Zero);
        };
    }

    // Text is measured at its real pixel width. No nested AutoSize tables,
    // MaximumSize constraints or resizing Region allocations are involved.
    private int TextHeight(Label label, int width)
    {
        Size measured = TextRenderer.MeasureText(label.Text, label.Font,
            new Size(Math.Max(1, width), 2000),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        return Math.Max(label.Font.Height + Px(4), measured.Height + Px(2));
    }

    private int TextWidth(Label label) => TextRenderer.MeasureText(
        label.Text, label.Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width + Px(3);

    private void LayoutHostBounds()
    {
        int width = Math.Max(0, ClientSize.Width);
        int height = Math.Max(0, ClientSize.Height);
        int headerHeight = _titleBar.Height;
        int footerHeight = _collapsed ? 0 : Px(9);
        _titleBar.SetBounds(0, 0, width, headerHeight);
        _resizeStrip.SetBounds(0, Math.Max(0, height - footerHeight), width, footerHeight);
        _contentPanel.SetBounds(0, headerHeight, width,
            Math.Max(0, height - headerHeight - footerHeight));
        LayoutWindow();
    }

    // The visible content area owns no implicit AutoScroll offsets. This avoids
    // Windows Forms moving the first card upward when an input gains focus.
    private void LayoutWindow()
    {
        if (!_layoutReady || _layoutBusy || _collapsed || _contentPanel.IsDisposed) return;
        if (_contentPanel.ClientSize.Width <= 0 || _contentPanel.ClientSize.Height <= 0) return;
        _layoutBusy = true;
        try
        {
            int edgeX = Px(12);
            int viewportWidth = _contentPanel.ClientSize.Width;
            int viewportHeight = _contentPanel.ClientSize.Height;
            int available = Math.Max(Px(240), viewportWidth - edgeX * 2);

            // First measure at full width, then re-measure with a scroll bar
            // only when the content physically exceeds the viewport height.
            int needed = LayoutSurface(available);
            int top = Px(8);
            bool needsScroll = top + needed > viewportHeight;
            if (needsScroll)
            {
                available = Math.Max(Px(240),
                    viewportWidth - _verticalScroll.Width - edgeX * 2);
                needed = LayoutSurface(available);
            }
            _contentHeight = needed;
            _contentWidth = available;
            _verticalScroll.Visible = needsScroll;
            _verticalScroll.LargeChange = Math.Max(1, viewportHeight);
            _verticalScroll.Maximum = Math.Max(0, top + needed - 1);
            int highest = Math.Max(0,
                _verticalScroll.Maximum - _verticalScroll.LargeChange + 1);
            if (_verticalScroll.Value > highest)
                _verticalScroll.Value = highest;
            if (!needsScroll && _verticalScroll.Value != 0)
                _verticalScroll.Value = 0;
            PositionSurface();
        }
        finally
        {
            _layoutBusy = false;
        }
    }

    private void PositionSurface()
    {
        if (_contentPanel.IsDisposed || _surface.IsDisposed) return;
        int top = Px(8) - (_verticalScroll.Visible ? _verticalScroll.Value : 0);
        _surface.SetBounds(Px(12), top, _contentWidth, _contentHeight);
    }

    private void RegisterMouseWheel(Control root)
    {
        // NumericUpDown retains the normal mouse-wheel interaction. Everywhere
        // else the wheel scrolls this explicit viewport when content overflows.
        if (root is not NumericUpDown)
            root.MouseWheel += OnContentMouseWheel;
        foreach (Control child in root.Controls)
            RegisterMouseWheel(child);
    }

    private void OnContentMouseWheel(object? sender, MouseEventArgs e)
    {
        if (!_verticalScroll.Visible || e.Delta == 0) return;
        int highest = Math.Max(0,
            _verticalScroll.Maximum - _verticalScroll.LargeChange + 1);
        int step = Math.Max(1, SystemInformation.MouseWheelScrollLines) * Px(14);
        int offset = e.Delta > 0 ? -step : step;
        int next = Math.Clamp(_verticalScroll.Value + offset, 0, highest);
        if (next == _verticalScroll.Value) return;
        _verticalScroll.Value = next;
    }

    // Returns exactly the occupied content height, including the final label.
    private int LayoutSurface(int width)
    {
        _surface.SuspendLayout();
        _stateCard.SuspendLayout();
        _settingsCard.SuspendLayout();
        _statsCard.SuspendLayout();
        try
        {
            int gap = Px(10);
            int xPad = Px(15);
            int cardContentWidth = width - 2 * xPad;
            // Keep the card border well inside the visible scroll viewport.
            int y = Px(3);

            // Status card: the region is a separate, fully measured second row.
            int statusWidth = cardContentWidth - Px(25);
            int statusHeight = TextHeight(_statusLabel, statusWidth);
            int regionHeight = TextHeight(_regionLabel, cardContentWidth);
            int statusTop = Px(15);
            int regionTop = statusTop + statusHeight + Px(8);
            int stateHeight = regionTop + regionHeight + Px(15);
            _stateCard.SetBounds(0, y, width, stateHeight);
            _statusDot.SetBounds(xPad, statusTop + (statusHeight - Px(10)) / 2, Px(10), Px(10));
            _statusLabel.SetBounds(xPad + Px(24), statusTop, statusWidth, statusHeight);
            _regionLabel.SetBounds(xPad, regionTop, cardContentWidth, regionHeight);
            y += stateHeight + gap;

            int buttonsHeight = Math.Max(Px(45), _startButton.Font.Height + Px(18));
            _actions.SetBounds(0, y, width, buttonsHeight);
            y += buttonsHeight + gap;

            int headerTop = Px(14);
            int titleHeight = TextHeight(_settingsTitle, cardContentWidth);
            int hotkeyW = TextWidth(_hotkeyLabel);
            int titleW = TextWidth(_settingsTitle);
            int headerHeight = Math.Max(titleHeight, TextHeight(_hotkeyLabel, cardContentWidth));
            _settingsTitle.SetBounds(xPad, headerTop, cardContentWidth, titleHeight);
            if (titleW + hotkeyW + Px(10) <= cardContentWidth)
            {
                _hotkeyLabel.SetBounds(xPad + cardContentWidth - hotkeyW, headerTop,
                    hotkeyW, headerHeight);
            }
            else
            {
                _hotkeyLabel.SetBounds(xPad, headerTop + titleHeight + Px(3),
                    cardContentWidth, TextHeight(_hotkeyLabel, cardContentWidth));
                headerHeight = titleHeight + Px(3) + _hotkeyLabel.Height;
            }

            int optsTop = headerTop + headerHeight + Px(9);
            int checkW = Math.Min(cardContentWidth, TextRenderer.MeasureText(
                _ultraFastCheckBox.Text, _ultraFastCheckBox.Font,
                Size.Empty, TextFormatFlags.SingleLine).Width + Px(32));
            int checkH = Math.Max(Px(29), _ultraFastCheckBox.Font.Height + Px(9));
            int gridLabelW = TextWidth(_maxGridLabel);
            int gridInputW = Px(69);
            int numericH = Math.Max(Px(27), _maxGridNumeric.PreferredHeight);
            int gridGroupW = gridLabelW + Px(8) + gridInputW;
            int optsHeight;
            _ultraFastCheckBox.SetBounds(xPad, optsTop, checkW, checkH);
            int gridY;
            int gridX;
            if (checkW + Px(12) + gridGroupW <= cardContentWidth)
            {
                gridX = xPad + cardContentWidth - gridGroupW;
                gridY = optsTop;
                optsHeight = Math.Max(checkH, numericH);
            }
            else
            {
                gridX = xPad;
                gridY = optsTop + checkH + Px(5);
                optsHeight = checkH + Px(5) + numericH;
            }
            _maxGridLabel.SetBounds(gridX, gridY, gridLabelW, numericH);
            _maxGridNumeric.SetBounds(gridX + gridLabelW + Px(8), gridY, gridInputW, numericH);
            int settingsHeight = optsTop + optsHeight + Px(14);
            _settingsCard.SetBounds(0, y, width, settingsHeight);
            y += settingsHeight + gap;

            int metricWidthMin = Px(92);
            int columns = (width - Px(16)) / 4 >= metricWidthMin ? 4 : 2;
            int rowHeight = Math.Max(Px(59), _gridMetric.PreferredMetricHeight);
            int statsHeight = Px(10) + rowHeight * (4 / columns) + Px(10);
            _statsCard.SetBounds(0, y, width, statsHeight);
            MetricView[] metrics = { _gridMetric, _clickMetric, _differentMetric, _gridConfidenceMetric };
            int statsInnerW = width - Px(16);
            for (int i = 0; i < metrics.Length; i++)
            {
                int col = i % columns;
                int row = i / columns;
                int left = Px(8) + col * statsInnerW / columns;
                int right = Px(8) + (col + 1) * statsInnerW / columns;
                metrics[i].SetBounds(left, Px(10) + row * rowHeight, right - left, rowHeight);
            }
            y += statsHeight + Px(8);

            int hintWidth = Math.Max(Px(100), width - Px(6));
            int hintHeight = TextHeight(_hintLabel, hintWidth);
            _hintLabel.SetBounds(Px(3), y, hintWidth, hintHeight);
            // No forced filler height: the surface ends just after the hint.
            y += hintHeight;
            return y;
        }
        finally
        {
            _statsCard.ResumeLayout(false);
            _settingsCard.ResumeLayout(false);
            _stateCard.ResumeLayout(false);
            _surface.ResumeLayout(false);
        }
    }

    private void FitInitialWindow()
    {
        if (_startupFitted) return;
        LayoutWindow();
        // Exact size: title + measured content (including the final label) +
        // viewport top inset + a 9px bottom resizing strip. No extra footer.
        int target = _titleBar.Height + Px(8) + _contentHeight + Px(9);
        int minHeight = Math.Max(MinimumSize.Height, Px(220));
        int maxHeight = Math.Max(minHeight,
            (int)(Screen.FromControl(this).WorkingArea.Height * 0.94));
        ClientSize = new Size(ClientSize.Width, Math.Clamp(target, minHeight, maxHeight));
        Rectangle work = Screen.FromControl(this).WorkingArea;
        Location = new Point(work.Left + (work.Width - Width) / 2,
            work.Top + (work.Height - Height) / 2);
        LayoutWindow();
        _expandedClientHeight = ClientSize.Height;
        _expandedMinimumSize = MinimumSize;
        _startupFitted = true;
    }

    private void TryEnableSystemRoundedCorners()
    {
        // A DWM corner preference is applied once, on supported Windows builds.
        // Unlike Form.Region it does not recreate a clipping mask every resize tick.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        try { NativeMethods.SetDwmRoundedCorners(Handle); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    private void ToggleCollapsed()
    {
        if (!_collapsed)
        {
            _expandedClientHeight = ClientSize.Height;
            _expandedMinimumSize = MinimumSize;
            _collapsed = true;
            _contentPanel.Visible = false;
            _resizeStrip.Visible = false;
            MinimumSize = new Size(_expandedMinimumSize.Width, 0);
            ClientSize = new Size(ClientSize.Width, _titleBar.Height + Padding.Vertical);
            _collapseButton.Text = "▴";
        }
        else
        {
            _collapsed = false;
            _contentPanel.Visible = true;
            _resizeStrip.Visible = true;
            MinimumSize = _expandedMinimumSize;
            ClientSize = new Size(ClientSize.Width,
                Math.Max(_expandedMinimumSize.Height, _expandedClientHeight));
            _collapseButton.Text = "▾";
            LayoutWindow();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.RegisterHotKey(Handle, HotkeyId,
            NativeMethods.MOD_NOREPEAT, NativeMethods.VK_F8);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_NCHITTEST && !_collapsed &&
            WindowState == FormWindowState.Normal)
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)NativeMethods.HT_CLIENT)
            {
                long xy = m.LParam.ToInt64();
                Point p = PointToClient(new Point(unchecked((short)(xy & 0xFFFF)),
                    unchecked((short)((xy >> 16) & 0xFFFF))));
                int edge = Px(7);
                bool left = p.X < edge;
                bool right = p.X >= ClientSize.Width - edge;
                bool top = p.Y < edge;
                bool bottom = p.Y >= ClientSize.Height - edge;
                if (left && top) m.Result = (IntPtr)NativeMethods.HT_TOPLEFT;
                else if (right && top) m.Result = (IntPtr)NativeMethods.HT_TOPRIGHT;
                else if (left && bottom) m.Result = (IntPtr)NativeMethods.HT_BOTTOMLEFT;
                else if (right && bottom) m.Result = (IntPtr)NativeMethods.HT_BOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)NativeMethods.HT_LEFT;
                else if (right) m.Result = (IntPtr)NativeMethods.HT_RIGHT;
                else if (top) m.Result = (IntPtr)NativeMethods.HT_TOP;
                else if (bottom) m.Result = (IntPtr)NativeMethods.HT_BOTTOM;
            }
            return;
        }
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            if (_engine?.IsRunning == true) StopEngine();
            else StartEngine();
        }
        base.WndProc(ref m);
    }

    private void SelectRegion()
    {
        StopEngine();
        Hide();
        try
        {
            using var selector = new RegionSelectorForm();
            if (selector.ShowDialog() == DialogResult.OK)
            {
                Rectangle region = selector.SelectedRegion;
                _settings.Left = region.Left;
                _settings.Top = region.Top;
                _settings.Width = region.Width;
                _settings.Height = region.Height;
                _settings.HasRegion = true;
                SaveUiSettings();
                UpdateRegionLabel();
                SetRunState(false, "选区已保存，可直接开始");
            }
        }
        finally
        {
            Show();
            Activate();
        }
    }

    private void StartEngine()
    {
        if (_engine?.IsRunning == true) return;
        if (!_settings.HasRegion || _settings.Width < 80 || _settings.Height < 80)
        {
            MessageBox.Show(this, "请先点“框选区域”，只框住完整的色块网格。",
                "未设置选区", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SaveUiSettings();
        MoveWindowAwayFromRegion();
        _engine?.Dispose();
        _engine = new ClickEngine(_settings.Region, _settings.MaxGrid,
            _settings.UltraFast, OnEngineStatus);
        _selectButton.Enabled = false;
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        _ultraFastCheckBox.Enabled = false;
        _maxGridNumeric.Enabled = false;
        SetRunState(true, "启动中…");
        _engine.Start();
    }

    private void StopEngine(bool updateUi = true)
    {
        if (_engine != null)
        {
            _engine.Stop();
            _engine.Dispose();
            _engine = null;
        }
        if (!updateUi || IsDisposed || Disposing) return;
        _selectButton.Enabled = true;
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _ultraFastCheckBox.Enabled = true;
        _maxGridNumeric.Enabled = true;
        SetRunState(false, "已停止");
    }

    private void OnEngineStatus(EngineStatus status)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing) return;
                SetRunState(_engine?.IsRunning == true, status.Message);
                _gridMetric.SetValue(status.GridSize > 0 ? $"{status.GridSize}×{status.GridSize}" : "-");
                _clickMetric.SetValue(status.ClickCount.ToString());
                _differentMetric.SetValue(status.LastConfidence > 0 ? status.LastConfidence.ToString("F2") : "-");
                _gridConfidenceMetric.SetValue(status.LastLineScore > 0 ? status.LastLineScore.ToString("F2") : "-");
            }));
        }
        catch { /* Form is closing. */ }
    }

    private void SetRunState(bool running, string message)
    {
        if (_statusLabel.Text != message)
        {
            int oldHeight = _statusLabel.Height;
            _statusLabel.Text = message;
            // Frequent engine status changes should repaint just the label,
            // not rearrange the entire window unless wrapping truly changes.
            if (_layoutReady && TextHeight(_statusLabel, Math.Max(1, _statusLabel.Width)) != oldHeight)
                LayoutWindow();
        }
        _statusDot.DotColor = running ? UiTheme.Running : UiTheme.Idle;
    }

    private void UpdateRegionLabel()
    {
        string text = !_settings.HasRegion ? "选区：未设置" :
            $"选区：X {_settings.Left} · Y {_settings.Top} · {_settings.Width} × {_settings.Height}";
        if (_regionLabel.Text != text)
        {
            _regionLabel.Text = text;
            LayoutWindow();
        }
    }

    private void SaveUiSettings()
    {
        _settings.MaxGrid = (int)_maxGridNumeric.Value;
        _settings.UltraFast = _ultraFastCheckBox.Checked;
        _settings.Save();
    }

    private void MoveWindowAwayFromRegion()
    {
        Rectangle region = _settings.Region;
        if (!Bounds.IntersectsWith(region)) return;
        Rectangle screen = Screen.FromRectangle(region).WorkingArea;
        Point[] positions =
        {
            new(screen.Left + 10, screen.Top + 10),
            new(screen.Right - Width - 10, screen.Top + 10),
            new(screen.Left + 10, screen.Bottom - Height - 10),
            new(screen.Right - Width - 10, screen.Bottom - Height - 10)
        };
        foreach (Point p in positions)
        {
            Rectangle candidate = new(p, Size);
            if (!candidate.IntersectsWith(region) && screen.Contains(candidate))
            {
                Location = p;
                return;
            }
        }
    }
}
