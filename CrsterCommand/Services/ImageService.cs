using System.ComponentModel;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CrsterCommand.Services;

public class ImageService
{
    public Image CreateScreenCapture()
    {
        var screenSize = GetFullscreenSize();

        if (TryCaptureNative(screenSize, out var nativeCapture))
        {
            return nativeCapture!;
        }

        if (TryCaptureWithFfmpeg(screenSize, out var ffmpegCapture))
        {
            return ffmpegCapture!;
        }

        throw new InvalidOperationException("Unable to capture the screen.");
    }

    public Rectangle GetFullscreenSize()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // SM_CXSCREEN = 0, SM_CYSCREEN = 1
                int width = GetSystemMetrics(0);
                int height = GetSystemMetrics(1);
                return new Rectangle(0, 0, width, height);
            }

            if (OperatingSystem.IsMacOS())
            {
                uint display = CGMainDisplayID();
                int width = CGDisplayPixelsWide(display);
                int height = CGDisplayPixelsHigh(display);
                return new Rectangle(0, 0, width, height);
            }

            if (OperatingSystem.IsLinux())
            {
                IntPtr display = XOpenDisplay(IntPtr.Zero);
                if (display != IntPtr.Zero)
                {
                    int screen = XDefaultScreen(display);
                    int width = XDisplayWidth(display, screen);
                    int height = XDisplayHeight(display, screen);
                    XCloseDisplay(display);
                    return new Rectangle(0, 0, width, height);
                }
            }
        }
        catch
        {
            // ignore and try fallbacks
        }

        // Final fallback
        return new Rectangle(0, 0, 800, 600);
    }

    public Point GetMousePosition()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (GetCursorPos(out var point))
                {
                    return new Point(point.X, point.Y);
                }
            }

            if (OperatingSystem.IsMacOS())
            {
                var eventRef = CGEventCreate(IntPtr.Zero);
                if (eventRef != IntPtr.Zero)
                {
                    var location = CGEventGetLocation(eventRef);
                    CFRelease(eventRef);
                    return new Point((int)Math.Round(location.X), (int)Math.Round(location.Y));
                }
            }

            if (OperatingSystem.IsLinux())
            {
                var display = XOpenDisplay(IntPtr.Zero);
                if (display != IntPtr.Zero)
                {
                    try
                    {
                        var screen = XDefaultScreen(display);
                        var root = XRootWindow(display, screen);
                        if (XQueryPointer(display, root, out var rootReturn, out var childReturn, out var rootX, out var rootY, out var winX, out var winY, out var maskReturn) != 0)
                        {
                            return new Point(rootX, rootY);
                        }
                    }
                    finally
                    {
                        XCloseDisplay(display);
                    }
                }
            }
        }
        catch
        {
            // ignore and fall back below
        }

        var fallback = GetFullscreenSize();
        return new Point(fallback.Width / 2, fallback.Height / 2);
    }

    public Avalonia.Media.Imaging.Bitmap ToAvaloniaBitmap(Image image)
    {
        using (var memory = new MemoryStream())
        {
            // Save to stream in a supported format like BMP
            image.Save(memory, ImageFormat.Bmp);
            memory.Position = 0;

            // Create Avalonia bitmap from the stream
            return new Avalonia.Media.Imaging.Bitmap(memory);
        }
    }

    private bool TryCaptureWithFfmpeg(Rectangle screenSize, out Image? capture)
    {
        capture = null;
        var ffmpegPath = ScreenRecorderService.ResolveFfmpegPath();
        if (ffmpegPath is null)
        {
            return false;
        }

        var arguments = GetFfmpegPipeArguments(screenSize);
        if (arguments is null)
        {
            return false;
        }

        return RunProcessToImage(ffmpegPath, arguments, out capture);
    }

    private bool TryCaptureNative(Rectangle screenSize, out Image? capture)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryCaptureWindows(screenSize, out capture);
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryCaptureMac(screenSize, out capture);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryCaptureLinux(screenSize, out capture);
        }

        capture = null;
        return false;
    }

    private string[]? GetFfmpegPipeArguments(Rectangle screenSize)
    {
        if (OperatingSystem.IsWindows())
        {
            return new[]
            {
                "-hide_banner",
                "-loglevel", "error",
                "-nostdin",
                "-f", "gdigrab",
                "-framerate", "1",
                "-video_size", $"{screenSize.Width}x{screenSize.Height}",
                "-i", "desktop",
                "-frames:v", "1",
                "-c:v", "png",
                "-f", "image2pipe",
                "pipe:1"
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new[]
            {
                "-hide_banner",
                "-loglevel", "error",
                "-nostdin",
                "-f", "avfoundation",
                "-framerate", "30",
                "-capture_cursor", "1",
                "-i", "Capture screen 0",
                "-frames:v", "1",
                "-c:v", "png",
                "-f", "image2pipe",
                "pipe:1"
            };
        }

        if (OperatingSystem.IsLinux())
        {
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (string.IsNullOrWhiteSpace(display))
            {
                display = ":0.0";
            }

            return new[]
            {
                "-hide_banner",
                "-loglevel", "error",
                "-nostdin",
                "-f", "x11grab",
                "-framerate", "1",
                "-video_size", $"{screenSize.Width}x{screenSize.Height}",
                "-i", display,
                "-frames:v", "1",
                "-c:v", "png",
                "-f", "image2pipe",
                "pipe:1"
            };
        }

        return null;
    }

    private bool RunProcessToImage(string fileName, string[] arguments, out Image? capture)
    {
        capture = null;

        try
        {
            var processWatch = Stopwatch.StartNew();
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Debug.WriteLine($"[ImageService] {fileName} failed to start after {processWatch.ElapsedMilliseconds} ms");
                return false;
            }

            Debug.WriteLine($"[ImageService] {fileName} pipe started in {processWatch.ElapsedMilliseconds} ms");

            using var output = new MemoryStream();
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(output);
            var stderrTask = process.StandardError.ReadToEndAsync();
            var waitWatch = Stopwatch.StartNew();

            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                Debug.WriteLine($"[ImageService] {fileName} pipe timed out after {waitWatch.ElapsedMilliseconds} ms");
                return false;
            }

            Task.WaitAll(stdoutTask, stderrTask);

            if (process.ExitCode != 0 || output.Length <= 0)
            {
                Debug.WriteLine($"[ImageService] {fileName} pipe exited with code {process.ExitCode} in {waitWatch.ElapsedMilliseconds} ms; outputLength={output.Length}; stderr={stderrTask.Result}");
                return false;
            }

            output.Position = 0;
            using var image = Image.FromStream(output);
            capture = new Bitmap(image);
            Debug.WriteLine($"[ImageService] {fileName} pipe exited with code {process.ExitCode} in {waitWatch.ElapsedMilliseconds} ms; outputLength={output.Length}");
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool TryCaptureWindows(Rectangle screenSize, out Image? capture)
    {
        capture = null;

        var screenDc = IntPtr.Zero;
        var memoryDc = IntPtr.Zero;
        var bitmapHandle = IntPtr.Zero;
        var selectedObject = IntPtr.Zero;

        try
        {
            screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                return false;
            }

            memoryDc = CreateCompatibleDC(screenDc);
            if (memoryDc == IntPtr.Zero)
            {
                return false;
            }

            bitmapHandle = CreateCompatibleBitmap(screenDc, screenSize.Width, screenSize.Height);
            if (bitmapHandle == IntPtr.Zero)
            {
                return false;
            }

            selectedObject = SelectObject(memoryDc, bitmapHandle);
            if (selectedObject == IntPtr.Zero)
            {
                return false;
            }

            if (!BitBlt(memoryDc, 0, 0, screenSize.Width, screenSize.Height, screenDc, 0, 0, unchecked((int)(Srccopy | (uint)CaptureBlt))))
            {
                return false;
            }

            var bitmap = new Bitmap(screenSize.Width, screenSize.Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, screenSize.Width, screenSize.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            try
            {
                var bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                        biWidth = screenSize.Width,
                        biHeight = -screenSize.Height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = BiRgb
                    }
                };

                if (GetDIBits(memoryDc, bitmapHandle, 0, (uint)screenSize.Height, data.Scan0, ref bmi, DibRgbColors) == 0)
                {
                    return false;
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            capture = bitmap;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (selectedObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
            {
                SelectObject(memoryDc, selectedObject);
            }

            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            if (screenDc != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }

    private bool TryCaptureMac(Rectangle screenSize, out Image? capture)
    {
        capture = null;

        var imageRef = CGDisplayCreateImage(CGMainDisplayID());
        if (imageRef == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var width = (int)CGImageGetWidth(imageRef);
            var height = (int)CGImageGetHeight(imageRef);
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var bytesPerRow = width * 4;
            var bufferSize = bytesPerRow * height;
            var buffer = Marshal.AllocHGlobal(bufferSize);
            var colorSpace = CGColorSpaceCreateDeviceRGB();

            try
            {
                var context = CGBitmapContextCreate(buffer, (nuint)width, (nuint)height, 8, (nuint)bytesPerRow, colorSpace, CGBitmapByteOrder32Little | CGImageAlphaPremultipliedFirst);
                if (context == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    var rect = new CGRect(0, 0, width, height);
                    CGContextDrawImage(context, rect, imageRef);

                    var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                    try
                    {
                        var managed = new byte[bufferSize];
                        Marshal.Copy(buffer, managed, 0, bufferSize);
                        Marshal.Copy(managed, 0, data.Scan0, bufferSize);
                    }
                    finally
                    {
                        bitmap.UnlockBits(data);
                    }

                    capture = bitmap;
                    return true;
                }
                finally
                {
                    CGContextRelease(context);
                }
            }
            finally
            {
                if (colorSpace != IntPtr.Zero)
                {
                    CGColorSpaceRelease(colorSpace);
                }

                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CFRelease(imageRef);
        }
    }

    private bool TryCaptureLinux(Rectangle screenSize, out Image? capture)
    {
        capture = null;

        var display = XOpenDisplay(IntPtr.Zero);
        if (display == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var root = XDefaultRootWindow(display);
            var image = XGetImage(display, root, 0, 0, (uint)screenSize.Width, (uint)screenSize.Height, ulong.MaxValue, ZPixmap);
            if (image == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var ximage = Marshal.PtrToStructure<XImage>(image);
                if (ximage.Data == IntPtr.Zero || ximage.Width <= 0 || ximage.Height <= 0)
                {
                    return false;
                }

                var bitmap = new Bitmap(ximage.Width, ximage.Height, PixelFormat.Format32bppArgb);
                var rect = new Rectangle(0, 0, ximage.Width, ximage.Height);
                var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                try
                {
                    CopyXImageToBitmap(image, ximage, data.Scan0);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                capture = bitmap;
                return true;
            }
            finally
            {
                XDestroyImage(image);
            }
        }
        finally
        {
            XCloseDisplay(display);
        }
    }

    private static void CopyXImageToBitmap(IntPtr image, XImage ximage, IntPtr destination)
    {
        if (ximage.BitsPerPixel == 32 && ximage.BytesPerLine > 0)
        {
            var rowBytes = ximage.Width * 4;
            var sourceBuffer = new byte[ximage.BytesPerLine * ximage.Height];
            Marshal.Copy(ximage.Data, sourceBuffer, 0, sourceBuffer.Length);

            var destinationBuffer = new byte[rowBytes * ximage.Height];
            for (var y = 0; y < ximage.Height; y++)
            {
                Buffer.BlockCopy(sourceBuffer, y * ximage.BytesPerLine, destinationBuffer, y * rowBytes, rowBytes);
            }

            Marshal.Copy(destinationBuffer, 0, destination, destinationBuffer.Length);
            return;
        }

        var fallback = new byte[ximage.Width * ximage.Height * 4];
        var index = 0;
        for (var y = 0; y < ximage.Height; y++)
        {
            for (var x = 0; x < ximage.Width; x++)
            {
                var pixel = XGetPixel(image, x, y);
                fallback[index++] = (byte)(pixel & 0xFF);
                fallback[index++] = (byte)((pixel >> 8) & 0xFF);
                fallback[index++] = (byte)((pixel >> 16) & 0xFF);
                fallback[index++] = 0xFF;
            }
        }

        Marshal.Copy(fallback, 0, destination, fallback.Length);
    }

    // Windows
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, IntPtr lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    // macOS / CoreGraphics
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGPoint CGEventGetLocation(IntPtr @event);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGDisplayCreateImage(uint displayID);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGImageGetWidth(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGImageGetHeight(IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGColorSpaceCreateDeviceRGB();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGColorSpaceRelease(IntPtr space);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGBitmapContextCreate(IntPtr data, nuint width, nuint height, nint bitsPerComponent, nuint bytesPerRow, IntPtr space, uint bitmapInfo);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern void CGContextRelease(IntPtr context);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public CGRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGDisplayPixelsWide(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGDisplayPixelsHigh(uint display);

    // Linux / X11
    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display_name);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XDefaultScreen(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XRootWindow(IntPtr display, int screen_number);

    [DllImport("libX11.so.6")]
    private static extern int XQueryPointer(IntPtr display, IntPtr window, out IntPtr root_return, out IntPtr child_return, out int root_x_return, out int root_y_return, out int win_x_return, out int win_y_return, out int mask_return);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayWidth(IntPtr display, int screen_number);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayHeight(IntPtr display, int screen_number);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, ulong plane_mask, int format);

    [DllImport("libX11.so.6")]
    private static extern int XDestroyImage(IntPtr image);

    [DllImport("libX11.so.6")]
    private static extern ulong XGetPixel(IntPtr ximage, int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    private struct XImage
    {
        public int Width;
        public int Height;
        public int XOffset;
        public int Format;
        public IntPtr Data;
        public int ByteOrder;
        public int BitmapUnit;
        public int BitmapBitOrder;
        public int BitmapPad;
        public int Depth;
        public int BytesPerLine;
        public int BitsPerPixel;
        public UIntPtr RedMask;
        public UIntPtr GreenMask;
        public UIntPtr BlueMask;
    }

    private const int ZPixmap = 2;
    private const uint Srccopy = 0x00CC0020;
    private const int CaptureBlt = unchecked((int)0x40000000);
    private const uint BiRgb = 0;
    private const uint DibRgbColors = 0;
    private const uint CGBitmapByteOrder32Little = 2u << 12;
    private const uint CGImageAlphaPremultipliedFirst = 1u << 0;
}
