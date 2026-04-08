using Desktop.Robot;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CrsterCommand.Services;

public class ImageService
{
    private readonly Robot _robot = new();

    public Image CreateScreenCapture()
    {
        var screenSize = this.GetFullscreenSize();
        return _robot.CreateScreenCapture(screenSize);
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

    // Windows
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // macOS / CoreGraphics
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGDisplayPixelsWide(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGDisplayPixelsHigh(uint display);

    // Linux / X11
    [DllImport("libX11.so.6")]
    private static extern IntPtr XOpenDisplay(IntPtr display_name);

    [DllImport("libX11.so.6")]
    private static extern int XDefaultScreen(IntPtr display);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayWidth(IntPtr display, int screen_number);

    [DllImport("libX11.so.6")]
    private static extern int XDisplayHeight(IntPtr display, int screen_number);

    [DllImport("libX11.so.6")]
    private static extern int XCloseDisplay(IntPtr display);
}
