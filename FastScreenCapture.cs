using System.Runtime.InteropServices;

namespace ColorBlockClicker;

internal readonly record struct Rgb(double R, double G, double B)
{
    public static double Distance(in Rgb a, in Rgb b)
    {
        double dr = a.R - b.R;
        double dg = a.G - b.G;
        double db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }
}

internal unsafe sealed class FastScreenCapture : IDisposable
{
    private readonly Rectangle _region;
    private readonly int _width;
    private readonly int _height;
    private readonly IntPtr _screenDc;
    private readonly IntPtr _memoryDc;
    private readonly IntPtr _bitmap;
    private readonly IntPtr _oldBitmap;
    private readonly byte* _bits;
    private bool _disposed;

    public int Width => _width;
    public int Height => _height;

    public FastScreenCapture(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }

        _region = region;
        _width = region.Width;
        _height = region.Height;

        _screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("GetDC failed.");
        }

        _memoryDc = NativeMethods.CreateCompatibleDC(_screenDc);
        if (_memoryDc == IntPtr.Zero)
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, _screenDc);
            throw new InvalidOperationException("CreateCompatibleDC failed.");
        }

        var bmi = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = _width,
                biHeight = -_height, // top-down DIB
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
                biSizeImage = (uint)(_width * _height * 4)
            }
        };

        _bitmap = NativeMethods.CreateDIBSection(
            _screenDc,
            ref bmi,
            NativeMethods.DIB_RGB_COLORS,
            out IntPtr bits,
            IntPtr.Zero,
            0);

        if (_bitmap == IntPtr.Zero || bits == IntPtr.Zero)
        {
            NativeMethods.DeleteDC(_memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, _screenDc);
            throw new InvalidOperationException("CreateDIBSection failed.");
        }

        _oldBitmap = NativeMethods.SelectObject(_memoryDc, _bitmap);
        _bits = (byte*)bits.ToPointer();
    }

    public bool Capture()
    {
        return NativeMethods.BitBlt(
            _memoryDc,
            0,
            0,
            _width,
            _height,
            _screenDc,
            _region.Left,
            _region.Top,
            NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
    }

    public Rgb SampleAverage(double x, double y, int radius)
    {
        int cx = Clamp((int)Math.Round(x), 0, _width - 1);
        int cy = Clamp((int)Math.Round(y), 0, _height - 1);
        int r = Math.Max(1, radius);

        long sumR = 0;
        long sumG = 0;
        long sumB = 0;
        int count = 0;

        for (int oy = -r; oy <= r; oy += r)
        {
            for (int ox = -r; ox <= r; ox += r)
            {
                int px = Clamp(cx + ox, 0, _width - 1);
                int py = Clamp(cy + oy, 0, _height - 1);
                GetPixel(px, py, out byte rr, out byte gg, out byte bb);
                sumR += rr;
                sumG += gg;
                sumB += bb;
                count++;
            }
        }

        return new Rgb(
            sumR / (double)count,
            sumG / (double)count,
            sumB / (double)count);
    }

    public double HorizontalEdge(int x, int y)
    {
        x = Clamp(x, 1, _width - 1);
        y = Clamp(y, 0, _height - 1);
        GetPixel(x, y, out byte r1, out byte g1, out byte b1);
        GetPixel(x - 1, y, out byte r0, out byte g0, out byte b0);
        return Math.Abs(r1 - r0) + Math.Abs(g1 - g0) + Math.Abs(b1 - b0);
    }

    public double VerticalEdge(int x, int y)
    {
        x = Clamp(x, 0, _width - 1);
        y = Clamp(y, 1, _height - 1);
        GetPixel(x, y, out byte r1, out byte g1, out byte b1);
        GetPixel(x, y - 1, out byte r0, out byte g0, out byte b0);
        return Math.Abs(r1 - r0) + Math.Abs(g1 - g0) + Math.Abs(b1 - b0);
    }

    private void GetPixel(int x, int y, out byte r, out byte g, out byte b)
    {
        byte* p = _bits + ((y * _width + x) * 4);
        b = p[0];
        g = p[1];
        r = p[2];
    }

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_memoryDc != IntPtr.Zero && _oldBitmap != IntPtr.Zero)
        {
            NativeMethods.SelectObject(_memoryDc, _oldBitmap);
        }

        if (_bitmap != IntPtr.Zero)
        {
            NativeMethods.DeleteObject(_bitmap);
        }

        if (_memoryDc != IntPtr.Zero)
        {
            NativeMethods.DeleteDC(_memoryDc);
        }

        if (_screenDc != IntPtr.Zero)
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, _screenDc);
        }
    }
}
