using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using CrsterCommand.Models;
using System.IO;

namespace CrsterCommand.Converters;

public class EqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

public class NullToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

public class CountToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return false;
        if (value is int i) return i > 0;
        if (int.TryParse(value.ToString(), out var parsed)) return parsed > 0;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

public class FileAttachmentToTooltipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileAttachment attachment && !string.IsNullOrEmpty(attachment.FileName))
        {
            var fileType = Path.GetExtension(attachment.FileName)?.TrimStart('.') ?? "file";
            return $"Click to remove {fileType} file";
        }
        return "Click to remove file";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}