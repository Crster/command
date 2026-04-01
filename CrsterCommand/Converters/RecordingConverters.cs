using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CrsterCommand.Converters;

public class RecordingBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording && isRecording)
            return Brushes.Red;
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class FfmpegStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool available && !available)
            return new SolidColorBrush(Color.Parse("#fab387")); // warning orange
        return new SolidColorBrush(Color.Parse("#bac2de")); // normal
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class RecordingTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRecording && isRecording)
            return "Stop Recording";
        return "Start Recording";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
