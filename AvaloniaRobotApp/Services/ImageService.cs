using Desktop.Robot;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace AvaloniaRobotApp.Services;

public class ImageService
{
    private readonly IRobot _robot = new Robot();

    public Bitmap CreateScreenCapture(int width = 1920, int height = 1080)
    {
        return (Bitmap)_robot.CreateScreenCapture(new Rectangle(0, 0, width, height));
    }

    public byte[] BitmapToBytes(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
