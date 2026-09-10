using System.Drawing.Drawing2D;

namespace ColorBlockClicker;

internal sealed class MainForm : Form
{
    private const string AppVersion = "0.0.2";
    private const int HotkeyId = 0x4B21;

    private readonly AppSettings _settings;
    private readonly Label _regionLabel;
    private readonly Label _statusLabel;
    private readonly StatusDot _statusDot;
    private readonly ModernButton _selectButton;
    private readonly ModernButton _startButton;
    private readonly ModernButton _stopButton;
    private readonly CheckBox _ultraFastCheckBox;
    private readonly NumericUpDown _maxGridNumeric;
    private readonly MetricView _gridMetric;
    private readonly MetricView _clickMetric;
    private readonly MetricView _differentMetric;
    private readonly MetricView _gridConfidenceMetric;
    private readonly Panel _contentPanel;
    private Button _collapseButton = null!;
    private Panel _titleBar = null!;
    private readonly ToolTip _toolTip;

    private ClickEngine? _engine;
    private bool _collapsed;
    private int _expandedClientHeight;
    private Size _expandedMinimumSize;
    private Size _expandedMaximumSize;

    public MainForm()
    {
        _settings = AppSettings.Load();

        SuspendLayout();

        Text = $"异色块极速辅助 v{AppVersion}";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(472, 486);
        MinimumSize = new Size(472, 486);
        MaximumSize = new Size(720, 760);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        TopMost = true;
        ShowInTaskbar = true;
        BackColor = UiTheme.Window;
        ForeColor = UiTheme.Text;
        Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        DoubleBuffered = true;
        Padding = new Padding(1);
        KeyPreview = true;

        _toolTip = new ToolTip
        {
            InitialDelay = 350,
            ReshowDelay = 150,
            AutoPopDelay = 6000,
            ShowAlways = true
        };

        _titleBar = BuildTitleBar();
        Controls.Add(_titleBar);

        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Window,
            Padding = new Padding(16, 14, 16, 16),
            AutoScroll = true
        };
        Controls.Add(_contentPanel);
        _contentPanel.BringToFront();
        _titleBar.BringToFront();

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _contentPanel.Controls.Add(content);

        RoundedPanel stateCard = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12)
        };

        var stateLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        stateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        stateLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        _statusDot = new StatusDot
        {
            Size = new Size(10, 10),
            Margin = new Padding(0, 6, 9, 0),
            DotColor = UiTheme.Idle
        };

        _statusLabel = new Label
        {
            Text = "等待开始",
            ForeColor = UiTheme.Text,
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 0)
        };

        _regionLabel = new Label
        {
            Text = "选区：未设置",
            ForeColor = UiTheme.MutedText,
            AutoSize = true,
            MaximumSize = new Size(390, 0),
            Margin = new Padding(19, 8, 0, 0)
        };

        var hotkeyLabel = new Label
        {
            Text = "F8  全局开始 / 停止",
            ForeColor = UiTheme.MutedText,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(10, 2, 0, 0)
        };

        stateLayout.Controls.Add(_statusDot, 0, 0);
        stateLayout.Controls.Add(_statusLabel, 1, 0);
        stateLayout.Controls.Add(_regionLabel, 0, 1);
        stateLayout.SetColumnSpan(_regionLabel, 2);
        stateCard.Controls.Add(stateLayout);

        content.Controls.Add(stateCard, 0, 0);

        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0),
            BackColor = Color.Transparent
        };
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334F));

        _selectButton = new ModernButton
        {
            Text = "框选区域",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 5, 0),
            NormalColor = UiTheme.NeutralButton,
            HoverColor = UiTheme.NeutralHover,
            PressedColor = Color.FromArgb(42, 49, 60),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
        _selectButton.Click += (_, _) => SelectRegion();

        _startButton = new ModernButton
        {
            Text = "开始  F8",
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 0, 5, 0),
            NormalColor = UiTheme.Accent,
            HoverColor = UiTheme.AccentHover,
            PressedColor = UiTheme.AccentPressed,
            ForegroundColor = Color.FromArgb(10, 40, 31),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point)
        };
        _startButton.Click += (_, _) => StartEngine();

        _stopButton = new ModernButton
        {
            Text = "停止",
            Dock = DockStyle.Fill,
            Margin = new Padding(5, 0, 0, 0),
            NormalColor = UiTheme.Stop,
            HoverColor = UiTheme.StopHover,
            PressedColor = Color.FromArgb(164, 61, 72),
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            Enabled = false
        };
        _stopButton.Click += (_, _) => StopEngine();

        actionLayout.Controls.Add(_selectButton, 0, 0);
        actionLayout.Controls.Add(_startButton, 1, 0);
        actionLayout.Controls.Add(_stopButton, 2, 0);
        content.Controls.Add(actionLayout, 0, 1);

        RoundedPanel settingsCard = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12)
        };

        var settingsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));

        var settingsTitle = new Label
        {
            Text = "运行设置",
            ForeColor = UiTheme.Text,
            Font = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };

        hotkeyLabel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        hotkeyLabel.Margin = new Padding(0, 1, 0, 10);

        _ultraFastCheckBox = new CheckBox
        {
            Text = "极速扫描（最高响应速度）",
            AutoSize = true,
            ForeColor = UiTheme.Text,
            FlatStyle = FlatStyle.Flat,
            Checked = _settings.UltraFast,
            Margin = new Padding(0, 4, 0, 0),
            Cursor = Cursors.Hand
        };

        var gridRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 0),
            Anchor = AnchorStyles.Right
        };

        var maxGridLabel = new Label
        {
            Text = "最大网格",
            AutoSize = true,
            ForeColor = UiTheme.MutedText,
            Margin = new Padding(0, 7, 7, 0)
        };

        _maxGridNumeric = new NumericUpDown
        {
            Minimum = 8,
            Maximum = 60,
            Value = Math.Clamp(_settings.MaxGrid, 8, 60),
            Width = 62,
            Height = 30,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(23, 28, 36),
            ForeColor = UiTheme.Text,
            TextAlign = HorizontalAlignment.Center,
            Margin = new Padding(0, 2, 0, 0)
        };

        gridRow.Controls.Add(maxGridLabel);
        gridRow.Controls.Add(_maxGridNumeric);

        settingsLayout.Controls.Add(settingsTitle, 0, 0);
        settingsLayout.Controls.Add(hotkeyLabel, 1, 0);
        settingsLayout.Controls.Add(_ultraFastCheckBox, 0, 1);
        settingsLayout.Controls.Add(gridRow, 1, 1);
        settingsCard.Controls.Add(settingsLayout);
        content.Controls.Add(settingsCard, 0, 2);

        RoundedPanel statsCard = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(8, 9, 8, 9)
        };

        var statsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 58,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        for (int i = 0; i < 4; i++)
        {
            statsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        }

        _gridMetric = new MetricView("网格") { Dock = DockStyle.Fill };
        _clickMetric = new MetricView("点击") { Dock = DockStyle.Fill };
        _differentMetric = new MetricView("异色置信") { Dock = DockStyle.Fill };
        _gridConfidenceMetric = new MetricView("网格置信") { Dock = DockStyle.Fill };

        statsLayout.Controls.Add(_gridMetric, 0, 0);
        statsLayout.Controls.Add(_clickMetric, 1, 0);
        statsLayout.Controls.Add(_differentMetric, 2, 0);
        statsLayout.Controls.Add(_gridConfidenceMetric, 3, 0);
        statsCard.Controls.Add(statsLayout);
        content.Controls.Add(statsCard, 0, 3);

        var hint = new Label
        {
            Text = "只框完整色块方阵，不要包含上方的得分 / 时间。运行时窗口保持置顶，但开始后会自动避开识别区域。",
            ForeColor = UiTheme.MutedText,
            AutoSize = true,
            MaximumSize = new Size(410, 0),
            Margin = new Padding(2, 0, 2, 0),
            Padding = new Padding(0, 0, 0, 2)
        };
        content.Controls.Add(hint, 0, 4);

        _toolTip.SetToolTip(_selectButton, "拖框选择整个色块网格区域");
        _toolTip.SetToolTip(_startButton, "开始高速识别与自动点击，也可以按 F8");
        _toolTip.SetToolTip(_stopButton, "停止自动识别与点击");
        _toolTip.SetToolTip(_collapseButton, "收起 / 展开悬浮窗");
        _toolTip.SetToolTip(_maxGridNumeric, "允许自动识别的最大 N×N 网格");

        UpdateRegionLabel();
        SetRunState(false, "等待开始");

        Shown += (_, _) =>
        {
            _expandedClientHeight = ClientSize.Height;
            _expandedMinimumSize = MinimumSize;
            _expandedMaximumSize = MaximumSize;
            ApplyRoundedRegion();
        };
        Resize += (_, _) => ApplyRoundedRegion();

        FormClosing += (_, _) =>
        {
            StopEngine(updateUi: false);
            SaveUiSettings();
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _toolTip.Dispose();
        };

        ResumeLayout(true);
    }

    private Panel BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = UiTheme.TitleBar,
            Padding = new Padding(15, 0, 8, 0)
        };

        var titleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        titleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var identity = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0)
        };

        var title = new Label
        {
            Text = "异色块极速辅助",
            ForeColor = UiTheme.Text,
            Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Margin = new Padding(0, 4, 8, 0)
        };

        var version = new Label
        {
            Text = $"v{AppVersion}",
            ForeColor = UiTheme.Accent,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point),
            AutoSize = true,
            Padding = new Padding(7, 3, 7, 3),
            Margin = new Padding(0, 2, 0, 0),
            BackColor = Color.FromArgb(32, 64, 55)
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
            Text = text,
            Width = 38,
            Height = 38,
            Margin = new Padding(2, 8, 0, 8),
            FlatStyle = FlatStyle.Flat,
            ForeColor = UiTheme.MutedText,
            BackColor = UiTheme.TitleBar,
            Cursor = Cursors.Hand,
            TabStop = false,
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
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, (IntPtr)NativeMethods.HT_CAPTION, IntPtr.Zero);
        };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.RegisterHotKey(
            Handle,
            HotkeyId,
            NativeMethods.MOD_NOREPEAT,
            NativeMethods.VK_F8);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
        {
            if (_engine?.IsRunning == true)
            {
                StopEngine();
            }
            else
            {
                StartEngine();
            }
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
            DialogResult result = selector.ShowDialog();
            if (result == DialogResult.OK)
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
        if (_engine?.IsRunning == true)
        {
            return;
        }

        if (!_settings.HasRegion || _settings.Width < 80 || _settings.Height < 80)
        {
            MessageBox.Show(
                this,
                "请先点“框选区域”，只框住完整的色块网格。",
                "未设置选区",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        SaveUiSettings();
        MoveWindowAwayFromRegion();

        _engine?.Dispose();
        _engine = new ClickEngine(
            _settings.Region,
            _settings.MaxGrid,
            _settings.UltraFast,
            OnEngineStatus);

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

        if (!updateUi || IsDisposed || Disposing)
        {
            return;
        }

        _selectButton.Enabled = true;
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _ultraFastCheckBox.Enabled = true;
        _maxGridNumeric.Enabled = true;
        SetRunState(false, "已停止");
    }

    private void OnEngineStatus(EngineStatus status)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                SetRunState(_engine?.IsRunning == true, status.Message);
                _gridMetric.SetValue(status.GridSize > 0 ? $"{status.GridSize}×{status.GridSize}" : "-");
                _clickMetric.SetValue(status.ClickCount.ToString());
                _differentMetric.SetValue(status.LastConfidence > 0 ? status.LastConfidence.ToString("F2") : "-");
                _gridConfidenceMetric.SetValue(status.LastLineScore > 0 ? status.LastLineScore.ToString("F2") : "-");
            }));
        }
        catch
        {
            // Form is closing.
        }
    }

    private void SetRunState(bool running, string message)
    {
        _statusLabel.Text = message;
        _statusDot.DotColor = running ? UiTheme.Running : UiTheme.Idle;
    }

    private void SaveUiSettings()
    {
        _settings.MaxGrid = (int)_maxGridNumeric.Value;
        _settings.UltraFast = _ultraFastCheckBox.Checked;
        _settings.Save();
    }

    private void UpdateRegionLabel()
    {
        if (!_settings.HasRegion)
        {
            _regionLabel.Text = "选区：未设置";
            return;
        }

        Rectangle r = _settings.Region;
        _regionLabel.Text = $"选区：X {r.Left} · Y {r.Top} · {r.Width} × {r.Height}";
    }

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;

        if (_collapsed)
        {
            _expandedClientHeight = Math.Max(_expandedClientHeight, ClientSize.Height);
            _contentPanel.Visible = false;
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            int collapsedHeight = _titleBar.Height + Padding.Vertical;
            ClientSize = new Size(ClientSize.Width, collapsedHeight);
            _collapseButton.Text = "▴";
        }
        else
        {
            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            ClientSize = new Size(ClientSize.Width, _expandedClientHeight);
            MinimumSize = _expandedMinimumSize;
            MaximumSize = _expandedMaximumSize;
            _contentPanel.Visible = true;
            _collapseButton.Text = "▾";
        }

        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (Width < 2 || Height < 2)
        {
            return;
        }

        using var path = new GraphicsPath();
        const int radius = 14;
        int diameter = radius * 2;
        Rectangle rect = new(0, 0, Width, Height);
        Rectangle arc = new(rect.Left, rect.Top, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        Region? oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }

    private void MoveWindowAwayFromRegion()
    {
        Rectangle region = _settings.Region;
        Rectangle current = Bounds;
        if (!current.IntersectsWith(region))
        {
            return;
        }

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
