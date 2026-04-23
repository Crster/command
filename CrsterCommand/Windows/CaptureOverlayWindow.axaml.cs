using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using Avalonia.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace CrsterCommand.Windows;

public partial class CaptureOverlayWindow : Window
{
    public Bitmap? ResultBitmap { get; private set; }

    private List<Control> _undoStack = new();
    private TextBox? _currentTextBox;
    private Point? _startPoint;
    private Shape? _currentShape;
    private Shape? _cropShape;
    private bool _isDragging;
    // Removed marching ants timer and offset
    // Default tool changed to Crop so the dashed crop frame is selected when overlay opens
    private string _currentTool = "Crop";
    private DateTime _lastEscapeTime = DateTime.MinValue;
    private const int DoubleTapMs = 500;
    private bool _isDraggingToolbar;
    private Point _toolbarDragStart;

    public CaptureOverlayWindow() { InitializeComponent(); }

    public CaptureOverlayWindow(Bitmap screenshot)
    {
        InitializeComponent();
        BackgroundImage.Source = screenshot;

        // Force full screen dimensions to cover taskbar
        var screen = Screens.Primary;
        if (screen != null)
        {
            this.Width = screen.Bounds.Width;
            this.Height = screen.Bounds.Height;
        }
        this.WindowState = WindowState.FullScreen;
        this.Topmost = true;

        RectButton.Click += (s, e) => { _currentTool = "Box"; UpdateToolSelection(); };
        ArrowButton.Click += (s, e) => { _currentTool = "Arrow"; UpdateToolSelection(); };
        TextButton.Click += (s, e) => { _currentTool = "Text"; UpdateToolSelection(); };
        CropButton.Click += (s, e) => { _currentTool = "Crop"; UpdateToolSelection(); };
        UndoButton.Click += (s, e) => Undo();
        UpdateToolSelection(); 
        // Initialize haze visibility and sizing for crop default
        if (_currentTool == "Crop")
        {
            EnsureCropHaze();
        }
        
        CloseButton.Click += (s, e) =>
        {
            ResultBitmap = null;
            Close(null);
        };

        CopyButton.Click += async (s, e) =>
        {
            CommitText();
            var bitmap = await RenderToBitmap();
            ResultBitmap = bitmap;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                // For best clipboard compatibility, ensure we use a static Bitmap
                using var ms = new MemoryStream();
                bitmap.Save(ms);
                ms.Position = 0;
                var staticBitmap = new Avalonia.Media.Imaging.Bitmap(ms);
                await clipboard.SetBitmapAsync(staticBitmap);
            }
            Close(bitmap);
        };

        SaveButton.Click += async (s, e) =>
        {
            CommitText();
            var bitmap = await RenderToBitmap();
            ResultBitmap = bitmap;
            if (bitmap != null)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                    {
                        Title = "Save Screenshot",
                        SuggestedFileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}",
                        DefaultExtension = "png",
                        FileTypeChoices = new[]
                        {
                            new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                            new FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                            new FilePickerFileType("BMP Image") { Patterns = new[] { "*.bmp" } }
                        }
                    });

                    if (file != null)
                    {
                        var ext = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
                        var format = ext switch
                        {
                            ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                            ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                            _ => System.Drawing.Imaging.ImageFormat.Png
                        };
                        
                        await using var stream = await file.OpenWriteAsync();
                        if (format == ImageFormat.Png)
                        {
                            bitmap.Save(stream);
                        }
                        else
                        {
                            // Bridge to System.Drawing for other formats
                            using var ms = new MemoryStream();
                            bitmap.Save(ms);
                            ms.Position = 0;
                            using var sysImage = System.Drawing.Image.FromStream(ms);
                            sysImage.Save(stream, format);
                        }
                    }
                }
            }
            Close(bitmap);
        };
        
        DrawingCanvas.PointerPressed += OnPointerPressed;
        DrawingCanvas.PointerMoved += OnPointerMoved;
        DrawingCanvas.PointerReleased += OnPointerReleased;
        this.KeyDown += OnKeyDown;

        // Add toolbar drag functionality
        var toolbar = this.FindControl<Border>("ToolbarContainer");
        if (toolbar != null)
        {
            toolbar.PointerPressed += OnToolbarPointerPressed;
            toolbar.PointerMoved += OnToolbarPointerMoved;
            toolbar.PointerReleased += OnToolbarPointerReleased;
        }
    }

    private void UpdateToolSelection()
    {
        RectButton.Classes.Remove("Active");
        ArrowButton.Classes.Remove("Active");
        TextButton.Classes.Remove("Active");
        CropButton.Classes.Remove("Active");
        
        if (_currentTool == "Box") RectButton.Classes.Add("Active");
        else if (_currentTool == "Arrow") ArrowButton.Classes.Add("Active");
        else if (_currentTool == "Text") TextButton.Classes.Add("Active");
        else if (_currentTool == "Crop") CropButton.Classes.Add("Active");

        // Toggle haze overlay when crop is active
        if (_currentTool == "Crop")
        {
            EnsureCropHaze();
        }
        else
        {
            HideCropHaze();
        }
    }

    private void EnsureCropHaze()
    {
        // Make sure haze elements exist and are visible
        var top = this.FindControl<Rectangle>("HazeTop");
        var left = this.FindControl<Rectangle>("HazeLeft");
        var right = this.FindControl<Rectangle>("HazeRight");
        var bottom = this.FindControl<Rectangle>("HazeBottom");

        if (top == null || left == null || right == null || bottom == null)
            return;
        // Position haze depending on whether a crop rect exists.
        double canvasW = DrawingCanvas.Bounds.Width;
        double canvasH = DrawingCanvas.Bounds.Height;

        // If layout hasn't happened yet, defer until we have sizes
        if (canvasW <= 0 || canvasH <= 0)
        {
            DrawingCanvas.LayoutUpdated += OnDrawingCanvasLayoutUpdated;
            return;
        }

        // If there's no crop rectangle yet, show a full-screen haze to indicate crop mode is active
        if (_cropShape == null || _cropShape.Width <= 0 || _cropShape.Height <= 0)
        {
            top.IsVisible = true;
            left.IsVisible = false;
            right.IsVisible = false;
            bottom.IsVisible = false;

            Canvas.SetLeft(top, 0);
            Canvas.SetTop(top, 0);
            top.Width = canvasW;
            top.Height = canvasH;
            return;
        }

        top.IsVisible = true; left.IsVisible = true; right.IsVisible = true; bottom.IsVisible = true;

        var cropX = Canvas.GetLeft(_cropShape);
        var cropY = Canvas.GetTop(_cropShape);
        var cropW = _cropShape.Width;
        var cropH = _cropShape.Height;

        // Position crop shape (ensure values are sane)
        Canvas.SetLeft(_cropShape, cropX);
        Canvas.SetTop(_cropShape, cropY);

        // Position haze rectangles around crop
        Canvas.SetLeft(top, 0);
        Canvas.SetTop(top, 0);
        top.Width = canvasW;
        top.Height = Math.Max(0, cropY);

        Canvas.SetLeft(left, 0);
        Canvas.SetTop(left, cropY);
        left.Width = Math.Max(0, cropX);
        left.Height = Math.Max(0, cropH);

        Canvas.SetLeft(right, cropX + cropW);
        Canvas.SetTop(right, cropY);
        right.Width = Math.Max(0, canvasW - (cropX + cropW));
        right.Height = Math.Max(0, cropH);

        Canvas.SetLeft(bottom, 0);
        Canvas.SetTop(bottom, cropY + cropH);
        bottom.Width = canvasW;
        bottom.Height = Math.Max(0, canvasH - (cropY + cropH));
    }

    private void HideCropHaze()
    {
        var top = this.FindControl<Rectangle>("HazeTop");
        var left = this.FindControl<Rectangle>("HazeLeft");
        var right = this.FindControl<Rectangle>("HazeRight");
        var bottom = this.FindControl<Rectangle>("HazeBottom");
        if (top != null) top.IsVisible = false;
        if (left != null) left.IsVisible = false;
        if (right != null) right.IsVisible = false;
        if (bottom != null) bottom.IsVisible = false;
    }

    private void OnDrawingCanvasLayoutUpdated(object? sender, EventArgs e)
    {
        DrawingCanvas.LayoutUpdated -= OnDrawingCanvasLayoutUpdated;
        EnsureCropHaze();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;

            // Cancel active text entry first (doesn't count as an Undo step)
            if (_currentTextBox != null)
            {
                CancelText();
                return;
            }

            var now = DateTime.Now;
            var elapsed = (now - _lastEscapeTime).TotalMilliseconds;

            if (elapsed <= DoubleTapMs)
            {
                // Double-tap: cancel the screen capture entirely
                _lastEscapeTime = DateTime.MinValue;
                Close(null);
            }
            else
            {
                // Single tap: undo last move
                _lastEscapeTime = now;
                Undo();
            }
        }
    }

    private void OnToolbarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var toolbar = sender as Border;
        if (toolbar != null)
        {
            _isDraggingToolbar = true;
            _toolbarDragStart = e.GetPosition(this);
            e.Handled = true;
        }
    }

    private void OnToolbarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingToolbar && sender is Border toolbar)
        {
            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _toolbarDragStart;

            var currentMargin = toolbar.Margin;
            toolbar.Margin = new Thickness(
                currentMargin.Left + offset.X,
                currentMargin.Top + offset.Y,
                currentMargin.Right,
                currentMargin.Bottom
            );

            _toolbarDragStart = currentPosition;
            e.Handled = true;
        }
    }

    private void OnToolbarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDraggingToolbar = false;
        e.Handled = true;
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            var last = _undoStack[^1];
            DrawingCanvas.Children.Remove(last);
            _undoStack.RemoveAt(_undoStack.Count - 1);
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        CommitText();
        _startPoint = e.GetPosition(DrawingCanvas);
        
        if (_currentTool == "Box")
        {
            _currentShape = new Rectangle
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1.5
            };
            DrawingCanvas.Children.Add(_currentShape);
            _undoStack.Add(_currentShape);
        }
        else if (_currentTool == "Arrow")
        {
            _currentShape = new ShapePath
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1.5,
                Fill = Brushes.Red,
                Data = new PathGeometry()
            };
            DrawingCanvas.Children.Add(_currentShape);
            _undoStack.Add(_currentShape);
        }
        else if (_currentTool == "Text")
        {
            CreateTextBox(_startPoint.Value);
        }
        else if (_currentTool == "Crop")
        {
            _isDragging = true;
            if (_cropShape != null) DrawingCanvas.Children.Remove(_cropShape);
            // Just a static dashed frame
            _cropShape = new Rectangle
            {
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                StrokeDashArray = new AvaloniaList<double>(6, 4),
                // No StrokeDashOffset
                Fill = new SolidColorBrush(Colors.White, 0.06)
            };
            DrawingCanvas.Children.Add(_cropShape);
            // Ensure haze updated to reflect new crop area
            EnsureCropHaze();
        }
    }

    private void CreateTextBox(Point position)
    {
        // Use a Border as the atomic undo container. Bottom-only red dotted underline.
        var underline = new Line
        {
            Stroke = Brushes.Red,
            StrokeThickness = 1.5,
            StrokeDashArray = new AvaloniaList<double>(4, 2),
            StartPoint = new Point(0, 0),
            EndPoint = new Point(120, 0), // default width, grows with text
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
        };

        _currentTextBox = new TextBox
        {
            Classes = { "annotation" },
            Background = Brushes.Transparent,
            Foreground = Brushes.Red,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            CaretBrush = Brushes.White,
            AcceptsReturn = true,
            MinWidth = 80,
            Padding = new Thickness(2, 2, 2, 4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };

        // Sync underline width to textbox width
        _currentTextBox.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Bounds")
                underline.EndPoint = new Point(_currentTextBox.Bounds.Width, 0);
        };

        var container = new Panel();
        container.Children.Add(_currentTextBox);
        container.Children.Add(underline);

        Canvas.SetLeft(container, position.X);
        Canvas.SetTop(container, position.Y);
        
        DrawingCanvas.Children.Add(container);
        _undoStack.Add(container);
        
        _currentTextBox.Focus();
        _currentTextBox.Tag = container; // Tag to container for CommitText
    }

    private void CommitText()
    {
        if (_currentTextBox != null)
        {
            if (string.IsNullOrWhiteSpace(_currentTextBox.Text))
            {
                CancelText();
                return;
            }

            // Remove the underline indicator, keep just the text
            if (_currentTextBox.Tag is Panel container && container.Children.Count > 1)
            {
                var underline = container.Children.OfType<Line>().FirstOrDefault();
                if (underline != null) container.Children.Remove(underline);
            }
            _currentTextBox = null;
        }
    }

    private void CancelText()
    {
        if (_currentTextBox != null)
        {
            var container = (Panel)_currentTextBox.Parent!;
            DrawingCanvas.Children.Remove(container);
            _undoStack.Remove(container);
            _currentTextBox = null;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var currentPoint = e.GetPosition(DrawingCanvas);

        // Handle crop drag separately since _cropShape isn't _currentShape
        if (_currentTool == "Crop" && _isDragging && _cropShape is Rectangle cropRect && _startPoint != null)
        {
            var minX = Math.Min(_startPoint.Value.X, currentPoint.X);
            var minY = Math.Min(_startPoint.Value.Y, currentPoint.Y);
            var width = Math.Abs(_startPoint.Value.X - currentPoint.X);
            var height = Math.Abs(_startPoint.Value.Y - currentPoint.Y);
            
            Canvas.SetLeft(cropRect, minX);
            Canvas.SetTop(cropRect, minY);
            cropRect.Width = width;
            cropRect.Height = height;
            // Update haze to follow the dynamic crop area
            EnsureCropHaze();
            return;
        }

        if (_startPoint == null || _currentShape == null) return;
        
        if (_currentShape is Rectangle rect)
        {
            var minX = Math.Min(_startPoint.Value.X, currentPoint.X);
            var minY = Math.Min(_startPoint.Value.Y, currentPoint.Y);
            var width = Math.Abs(_startPoint.Value.X - currentPoint.X);
            var height = Math.Abs(_startPoint.Value.Y - currentPoint.Y);
            
            Canvas.SetLeft(rect, minX);
            Canvas.SetTop(rect, minY);
            rect.Width = width;
            rect.Height = height;
        }
        else if (_currentShape is ShapePath path)
        {
            path.Data = CreateArrowGeometry(_startPoint.Value, currentPoint);
        }
    }

    private Geometry CreateArrowGeometry(Point start, Point end)
    {
        var geometry = new PathGeometry();
        
        // 1. Draw the line
        var lineFigure = new PathFigure { StartPoint = start, IsClosed = false };
        lineFigure.Segments!.Add(new LineSegment { Point = end });
        geometry.Figures!.Add(lineFigure);
        
        // 2. Draw the head (Pointy triangle)
        double headLength = 10;
        double headAngle = Math.PI / 8; // Sharp angle
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        
        Point p1 = new Point(
            end.X - headLength * Math.Cos(angle - headAngle),
            end.Y - headLength * Math.Sin(angle - headAngle));
        Point p2 = new Point(
            end.X - headLength * Math.Cos(angle + headAngle),
            end.Y - headLength * Math.Sin(angle + headAngle));

        var headFigure = new PathFigure { StartPoint = end, IsClosed = true, IsFilled = true };
        headFigure.Segments!.Add(new LineSegment { Point = p1 });
        headFigure.Segments.Add(new LineSegment { Point = p2 });
        geometry.Figures.Add(headFigure);

        return geometry;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _startPoint = null;
        _currentShape = null;
        
        if (_currentTool == "Crop" && _isDragging)
        {
            _isDragging = false;
            // No marching ants, just static dashed frame
        }
    }


    private async Task<RenderTargetBitmap> RenderToBitmap()
    {
        // Give the UI a moment to be sure everything is rendered
        await Task.Yield();
        
        // Temporarily hide Crop and Toolbar for rendering
        var toolbar = this.MainPanel.Children.OfType<Border>().FirstOrDefault();
        if (toolbar != null) toolbar.IsVisible = false;
        if (_cropShape != null) _cropShape.IsVisible = false;
        
        try
        {
            var totalBounds = MainPanel.Bounds;
            PixelRect pixelCrop;

            if (_cropShape != null && _cropShape.Width > 0 && _cropShape.Height > 0)
            {
                var left = Canvas.GetLeft(_cropShape);
                var top = Canvas.GetTop(_cropShape);
                pixelCrop = new PixelRect((int)left, (int)top, (int)_cropShape.Width, (int)_cropShape.Height);
            }
            else
            {
                pixelCrop = new PixelRect(0, 0, (int)totalBounds.Width, (int)totalBounds.Height);
            }

            var fullBitmap = new RenderTargetBitmap(new PixelSize((int)totalBounds.Width, (int)totalBounds.Height), new Vector(96, 96));
            fullBitmap.Render(MainPanel);

            if (pixelCrop.Width == (int)totalBounds.Width && pixelCrop.Height == (int)totalBounds.Height)
                return fullBitmap;

            // Crop the bitmap
            var cropped = new RenderTargetBitmap(new PixelSize(pixelCrop.Width, pixelCrop.Height), new Vector(96, 96));
            using (var ctx = cropped.CreateDrawingContext())
            {
                ctx.DrawImage(fullBitmap, 
                    new Rect(pixelCrop.X, pixelCrop.Y, pixelCrop.Width, pixelCrop.Height), 
                    new Rect(0, 0, pixelCrop.Width, pixelCrop.Height));
            }
            return cropped;
        }
        finally
        {
            if (toolbar != null) toolbar.IsVisible = true;
            if (_cropShape != null) _cropShape.IsVisible = true;
        }
    }
}
