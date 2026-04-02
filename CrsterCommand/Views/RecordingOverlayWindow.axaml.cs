using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace CrsterCommand.Views;

public partial class RecordingOverlayWindow : Window
{
    private Point _dragOffset;
    private bool _isDragging;
    private DispatcherTimer? _timer;
    private TimeSpan _elapsed;
    private Action? _stopCallback;

    public RecordingOverlayWindow()
    {
        InitializeComponent();
    }

    public RecordingOverlayWindow(Action stopCallback)
    {
        InitializeComponent();
        _stopCallback = stopCallback;

        // Position top-center of primary screen with some margin
        var screen = Screens.Primary;
        if (screen != null)
        {
            var bounds = screen.WorkingArea;
            Position = new PixelPoint(
                bounds.X + (bounds.Width - (int)Width) / 2,
                bounds.Y + 20
            );
        }
    }

    public void StartTimer()
    {
        _elapsed = TimeSpan.Zero;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
            TimerText.Text = _elapsed.ToString(@"mm\:ss");
        };
        _timer.Start();
    }

    public void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private void OnStopClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _stopCallback?.Invoke();
    }

    private void OnDragStart(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _dragOffset = e.GetPosition(this);
    }

    private void OnDragMove(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;
        var screenPos = e.GetPosition(null);
        var newPos = new PixelPoint(
            (int)(Position.X + screenPos.X - _dragOffset.X),
            (int)(Position.Y + screenPos.Y - _dragOffset.Y)
        );
        Position = newPos;
    }

    private void OnDragEnd(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }
}
