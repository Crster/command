using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CrsterCommand.Views;

public partial class AIReaderCropWindow : Window
{
    private Point? _startPoint;
    private Rectangle? _cropShape;
    private bool _isDragging;

    public AIReaderCropWindow() { InitializeComponent(); }

    public AIReaderCropWindow(Bitmap screenshot)
    {
        InitializeComponent();
        BackgroundImage.Source = screenshot;
        
        // Ensure window covers primary screen completely
        var screen = Screens.Primary;
        if (screen != null)
        {
            this.Width = screen.Bounds.Width;
            this.Height = screen.Bounds.Height;
        }

        DrawingCanvas.PointerPressed += (s, e) => {
            _startPoint = e.GetPosition(DrawingCanvas);
            _isDragging = true;
            if (_cropShape != null) DrawingCanvas.Children.Remove(_cropShape);
            
            _cropShape = new Rectangle
            {
                Stroke = Brushes.Cyan,
                StrokeThickness = 2,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double>(6, 4),
                Fill = new SolidColorBrush(Colors.Cyan, 0.1)
            };
            DrawingCanvas.Children.Add(_cropShape);
        };

        DrawingCanvas.PointerMoved += (s, e) => {
            if (!_isDragging || _startPoint == null || _cropShape == null) return;
            var currentPoint = e.GetPosition(DrawingCanvas);
            var minX = Math.Min(_startPoint.Value.X, currentPoint.X);
            var minY = Math.Min(_startPoint.Value.Y, currentPoint.Y);
            var width = Math.Abs(_startPoint.Value.X - currentPoint.X);
            var height = Math.Abs(_startPoint.Value.Y - currentPoint.Y);
            
            Canvas.SetLeft(_cropShape, minX);
            Canvas.SetTop(_cropShape, minY);
            _cropShape.Width = width;
            _cropShape.Height = height;
        };

        DrawingCanvas.PointerReleased += async (s, e) => {
            if (!_isDragging) return;
            _isDragging = false;
            
            if (_cropShape != null && _cropShape.Width > 5 && _cropShape.Height > 5)
            {
                var bitmap = await RenderToBitmap();
                Close(bitmap);
            }
            else
            {
                Close(null);
            }
        };

        this.KeyDown += (s, e) => {
            if (e.Key == Key.Escape) Close(null);
        };
    }

    private async Task<RenderTargetBitmap?> RenderToBitmap()
    {
        if (_cropShape == null) return null;
        
        // Hide overlay elements before rendering
        _cropShape.IsVisible = false;
        var instruction = MainPanel.Children.OfType<Border>().FirstOrDefault(b => b.Name == "InstructionBorder");
        if (instruction != null) instruction.IsVisible = false;
        
        // Let UI refresh
        await Task.Yield();
        
        try {
            var left = Canvas.GetLeft(_cropShape);
            var top = Canvas.GetTop(_cropShape);
            var width = _cropShape.Width;
            var height = _cropShape.Height;
            
            // Render the whole panel
            var pixelWidth = (int)MainPanel.Bounds.Width;
            var pixelHeight = (int)MainPanel.Bounds.Height;
            if (pixelWidth <= 0 || pixelHeight <= 0) return null;

            var fullBitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96, 96));
            fullBitmap.Render(MainPanel);
            
            // Create a cropped version
            var cropped = new RenderTargetBitmap(new PixelSize((int)width, (int)height), new Vector(96, 96));
            using (var ctx = cropped.CreateDrawingContext())
            {
                ctx.DrawImage(fullBitmap, 
                    new Rect(left, top, width, height), 
                    new Rect(0, 0, width, height));
            }
            return cropped;
        } finally {
            _cropShape.IsVisible = true;
            if (instruction != null) instruction.IsVisible = true;
        }
    }
}
