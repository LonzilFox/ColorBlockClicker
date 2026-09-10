using System.Diagnostics;

namespace ColorBlockClicker;

internal sealed record EngineStatus(
    bool Running,
    int GridSize,
    long ClickCount,
    double LastConfidence,
    double LastLineScore,
    string Message);

internal sealed class ClickEngine : IDisposable
{
    private const int MinimumSameBoardRetryMs = 24;
    private const double BoardChangeThreshold = 0.70;
    private const double MinimumOutlierDistance = 0.55;
    private const double MinimumOutlierRatio = 1.08;
    private const double MinimumOutlierGap = 0.20;

    private readonly Rectangle _region;
    private readonly int _maxGrid;
    private readonly bool _ultraFast;
    private readonly Action<EngineStatus> _statusCallback;
    private readonly CancellationTokenSource _cts = new();
    private Task? _worker;
    private bool _disposed;

    public bool IsRunning => _worker is { IsCompleted: false };

    public ClickEngine(
        Rectangle region,
        int maxGrid,
        bool ultraFast,
        Action<EngineStatus> statusCallback)
    {
        _region = region;
        _maxGrid = Math.Clamp(maxGrid, 2, 60);
        _ultraFast = ultraFast;
        _statusCallback = statusCallback;
    }

    public void Start()
    {
        if (_worker != null)
        {
            return;
        }

        _worker = Task.Factory.StartNew(
            RunLoop,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void Stop()
    {
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        _cts.Cancel();
    }

    private void RunLoop()
    {
        long clickCount = 0;
        int currentN = 0;
        int weakFrames = 0;
        Rgb[]? lastClickedSamples = null;
        int lastClickedN = 0;
        bool waitingForChange = false;
        long lastClickTicks = 0;
        var statusClock = Stopwatch.StartNew();

        try
        {
            using var capture = new FastScreenCapture(_region);
            Point park = FindParkingPoint(_region);
            NativeMethods.SetCursorPos(park.X, park.Y);

            Report(true, 0, 0, 0, 0, "正在识别网格...");

            while (!_cts.IsCancellationRequested)
            {
                if (!capture.Capture())
                {
                    Report(true, currentN, clickCount, 0, 0, "屏幕捕获失败，正在重试...");
                    Thread.Sleep(10);
                    continue;
                }

                if (currentN == 0)
                {
                    GridDetection grid = DetectBestGrid(capture, _maxGrid);
                    if (!grid.Valid)
                    {
                        if (statusClock.ElapsedMilliseconds >= 200)
                        {
                            Report(true, 0, clickCount, 0, grid.LineScore,
                                "未锁定网格：请把选区尽量贴紧最外侧色块边缘");
                            statusClock.Restart();
                        }

                        RelaxCpu();
                        continue;
                    }

                    currentN = grid.N;
                    weakFrames = 0;
                }

                Rgb[] samples = SampleCells(capture, currentN);

                if (waitingForChange && lastClickedSamples != null)
                {
                    double boardDelta = MaxBoardDelta(samples, lastClickedSamples);
                    double elapsedMs = TicksToMilliseconds(Stopwatch.GetTimestamp() - lastClickTicks);

                    if (currentN != lastClickedN || boardDelta >= BoardChangeThreshold)
                    {
                        waitingForChange = false;
                    }
                    else if (elapsedMs < MinimumSameBoardRetryMs)
                    {
                        RelaxCpu();
                        continue;
                    }
                    else
                    {
                        // A new round can occasionally repeat the exact same target position/color.
                        // After one display-frame-sized guard interval, allow another click.
                        waitingForChange = false;
                    }
                }

                double currentLine = GridLineScore(capture, currentN);

                // The game grows from small grids to larger ones. Check the next size cheaply.
                if (currentN < Math.Min(_maxGrid, MaxGridByPixels(capture)))
                {
                    double nextLine = GridLineScore(capture, currentN + 1);
                    if (nextLine > Math.Max(2.0, currentLine * 1.35))
                    {
                        currentN++;
                        samples = SampleCells(capture, currentN);
                        currentLine = nextLine;
                    }
                }

                TargetDetection target = FindOutlier(samples, currentN);

                bool targetValid =
                    target.MaxDistance >= MinimumOutlierDistance &&
                    target.Ratio >= MinimumOutlierRatio &&
                    target.Gap >= MinimumOutlierGap;

                if (!targetValid)
                {
                    weakFrames++;
                    if (weakFrames >= 3)
                    {
                        GridDetection reacquired = DetectBestGrid(capture, _maxGrid);
                        if (reacquired.Valid && reacquired.N != currentN)
                        {
                            currentN = reacquired.N;
                            weakFrames = 0;
                        }
                    }

                    if (statusClock.ElapsedMilliseconds >= 150)
                    {
                        Report(true, currentN, clickCount, target.Ratio, currentLine,
                            "等待稳定的异色块...");
                        statusClock.Restart();
                    }

                    RelaxCpu();
                    continue;
                }

                weakFrames = 0;

                int row = target.Index / currentN;
                int col = target.Index % currentN;
                int clickX = _region.Left + (int)Math.Round((col + 0.5) * _region.Width / currentN);
                int clickY = _region.Top + (int)Math.Round((row + 0.5) * _region.Height / currentN);

                lastClickedSamples = samples;
                lastClickedN = currentN;
                waitingForChange = true;
                lastClickTicks = Stopwatch.GetTimestamp();

                NativeMethods.LeftClickAt(clickX, clickY);
                NativeMethods.SetCursorPos(park.X, park.Y);
                clickCount++;

                Report(true, currentN, clickCount, target.Ratio, currentLine,
                    $"已点击：{currentN}×{currentN}  第 {row + 1} 行 / 第 {col + 1} 列");

                RelaxCpu();
            }
        }
        catch (Exception ex)
        {
            Report(false, currentN, clickCount, 0, 0, "运行异常：" + ex.Message);
            return;
        }

        Report(false, currentN, clickCount, 0, 0, "已停止");
    }

    private void RelaxCpu()
    {
        if (_ultraFast)
        {
            Thread.Yield();
        }
        else
        {
            Thread.Sleep(1);
        }
    }

    private static Point FindParkingPoint(Rectangle region)
    {
        Rectangle screen = SystemInformation.VirtualScreen;

        Point[] candidates =
        {
            new(region.Left - 24, region.Top - 24),
            new(region.Right + 24, region.Top - 24),
            new(region.Left - 24, region.Bottom + 24),
            new(region.Right + 24, region.Bottom + 24)
        };

        foreach (Point p in candidates)
        {
            if (screen.Contains(p) && !region.Contains(p))
            {
                return p;
            }
        }

        return new Point(screen.Left + 8, screen.Top + 8);
    }

    private static Rgb[] SampleCells(FastScreenCapture capture, int n)
    {
        var result = new Rgb[n * n];
        double pitchX = capture.Width / (double)n;
        double pitchY = capture.Height / (double)n;
        int radius = Math.Max(1, (int)Math.Round(Math.Min(pitchX, pitchY) * 0.08));
        radius = Math.Min(radius, 10);

        int index = 0;
        for (int row = 0; row < n; row++)
        {
            double y = (row + 0.5) * pitchY;
            for (int col = 0; col < n; col++)
            {
                double x = (col + 0.5) * pitchX;
                result[index++] = capture.SampleAverage(x, y, radius);
            }
        }

        return result;
    }

    private static TargetDetection FindOutlier(Rgb[] colors, int n)
    {
        double[] rs = new double[colors.Length];
        double[] gs = new double[colors.Length];
        double[] bs = new double[colors.Length];

        for (int i = 0; i < colors.Length; i++)
        {
            rs[i] = colors[i].R;
            gs[i] = colors[i].G;
            bs[i] = colors[i].B;
        }

        Array.Sort(rs);
        Array.Sort(gs);
        Array.Sort(bs);

        var median = new Rgb(MedianSorted(rs), MedianSorted(gs), MedianSorted(bs));
        var distances = new double[colors.Length];

        int maxIndex = 0;
        double max = double.MinValue;
        double second = double.MinValue;

        for (int i = 0; i < colors.Length; i++)
        {
            double d = Rgb.Distance(colors[i], median);
            distances[i] = d;

            if (d > max)
            {
                second = max;
                max = d;
                maxIndex = i;
            }
            else if (d > second)
            {
                second = d;
            }
        }

        if (second < 0)
        {
            second = 0;
        }

        double ratio = max / (second + 0.25);
        double gap = max - second;
        return new TargetDetection(n, maxIndex, max, second, ratio, gap);
    }

    private static double MaxBoardDelta(Rgb[] current, Rgb[] previous)
    {
        if (current.Length != previous.Length)
        {
            return double.PositiveInfinity;
        }

        double max = 0;
        for (int i = 0; i < current.Length; i++)
        {
            double d = Rgb.Distance(current[i], previous[i]);
            if (d > max)
            {
                max = d;
            }
        }

        return max;
    }

    private static GridDetection DetectBestGrid(FastScreenCapture capture, int requestedMax)
    {
        int max = Math.Min(requestedMax, MaxGridByPixels(capture));
        int bestN = 0;
        double bestWeighted = 0;
        double bestLine = 0;

        for (int n = 2; n <= max; n++)
        {
            double line = GridLineScore(capture, n);
            double weighted = line * Math.Sqrt(n);

            if (weighted > bestWeighted)
            {
                bestWeighted = weighted;
                bestLine = line;
                bestN = n;
            }
        }

        bool valid = bestN >= 2 && bestLine >= 1.70;
        return new GridDetection(bestN, bestLine, bestWeighted, valid);
    }

    private static int MaxGridByPixels(FastScreenCapture capture)
    {
        // Keep at least ~12 physical pixels per cell so center sampling remains reliable.
        return Math.Max(2, Math.Min(60, Math.Min(capture.Width, capture.Height) / 12));
    }

    private static double GridLineScore(FastScreenCapture capture, int n)
    {
        if (n < 2)
        {
            return 0;
        }

        double x = AxisLineScore(capture, n, verticalBoundaries: true);
        double y = AxisLineScore(capture, n, verticalBoundaries: false);

        x = Math.Max(x, 0.01);
        y = Math.Max(y, 0.01);
        return Math.Sqrt(x * y);
    }

    private static double AxisLineScore(
        FastScreenCapture capture,
        int n,
        bool verticalBoundaries)
    {
        int length = verticalBoundaries ? capture.Width : capture.Height;
        int other = verticalBoundaries ? capture.Height : capture.Width;
        double pitch = length / (double)n;
        int radius = Math.Clamp((int)Math.Round(pitch * 0.06), 1, 8);

        var boundaryStrengths = new double[n - 1];
        for (int i = 1; i < n; i++)
        {
            boundaryStrengths[i - 1] = LocalBoundaryStrength(
                capture,
                i * pitch,
                radius,
                other,
                verticalBoundaries);
        }

        var interiorStrengths = new double[n];
        for (int i = 0; i < n; i++)
        {
            interiorStrengths[i] = LocalBoundaryStrength(
                capture,
                (i + 0.5) * pitch,
                1,
                other,
                verticalBoundaries);
        }

        Array.Sort(boundaryStrengths);
        Array.Sort(interiorStrengths);

        double boundaryMedian = MedianSorted(boundaryStrengths);
        double interiorMedian = MedianSorted(interiorStrengths);
        return boundaryMedian / (interiorMedian + 1.0);
    }

    private static double LocalBoundaryStrength(
        FastScreenCapture capture,
        double position,
        int radius,
        int otherLength,
        bool verticalBoundary)
    {
        int length = verticalBoundary ? capture.Width : capture.Height;
        int center = (int)Math.Round(position);
        int from = Math.Max(1, center - radius);
        int to = Math.Min(length - 1, center + radius);
        double best = 0;

        const int samples = 12;
        for (int u = from; u <= to; u++)
        {
            double sum = 0;
            for (int k = 0; k < samples; k++)
            {
                int v = (int)Math.Round((k + 0.5) * otherLength / samples);
                v = Math.Clamp(v, 1, otherLength - 1);

                sum += verticalBoundary
                    ? capture.HorizontalEdge(u, v)
                    : capture.VerticalEdge(v, u);
            }

            double average = sum / samples;
            if (average > best)
            {
                best = average;
            }
        }

        return best;
    }

    private static double MedianSorted(double[] sorted)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        int mid = sorted.Length / 2;
        return (sorted.Length & 1) == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) * 0.5;
    }

    private static double TicksToMilliseconds(long ticks)
        => ticks * 1000.0 / Stopwatch.Frequency;

    private void Report(
        bool running,
        int grid,
        long clicks,
        double confidence,
        double lineScore,
        string message)
    {
        try
        {
            _statusCallback(new EngineStatus(
                running,
                grid,
                clicks,
                confidence,
                lineScore,
                message));
        }
        catch
        {
            // UI may already be closing.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();

        try
        {
            _worker?.Wait(500);
        }
        catch
        {
            // Ignore cancellation/close races.
        }

        _cts.Dispose();
    }

    private readonly record struct TargetDetection(
        int N,
        int Index,
        double MaxDistance,
        double SecondDistance,
        double Ratio,
        double Gap);

    private readonly record struct GridDetection(
        int N,
        double LineScore,
        double WeightedScore,
        bool Valid);
}
