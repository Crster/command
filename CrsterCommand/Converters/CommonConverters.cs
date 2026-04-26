using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
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

public class BudgetWeekRowBackgroundConverter : IValueConverter
{
    public static BudgetWeekRowBackgroundConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dueDate)
            return Brushes.Transparent;

        var today = DateTime.Now.Date;
        var weekEnd = today.AddDays(7);
        var isThisWeek = dueDate.Date >= today && dueDate.Date <= weekEnd;

        if (!isThisWeek)
            return Brushes.Transparent;

        if (Application.Current?.Resources is not null &&
            Application.Current.Resources.TryGetResource("AppSidebarActiveItemBrush", Application.Current.ActualThemeVariant, out var resource) &&
            resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Color.Parse("#1A223D"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}

public class BudgetTransactionTypeToBrushConverter : IValueConverter
{
    public static BudgetTransactionTypeToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value switch
        {
            BudgetTransactionType.Income => "AppSuccessBrush",
            BudgetTransactionType.Expense => "AppDangerBrush",
            BudgetTransactionType.Reserve => "AppSecondaryAccentBrush",
            _ => "AppPrimaryTextBrush"
        };

        if (Application.Current?.Resources is not null &&
            Application.Current.Resources.TryGetResource(resourceKey, Application.Current.ActualThemeVariant, out var resource) &&
            resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.White;
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

public class BoolToChevronConverter : IValueConverter
{
    public static BoolToChevronConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? FluentIcons.Common.Symbol.ChevronUp : FluentIcons.Common.Symbol.ChevronDown;
        return FluentIcons.Common.Symbol.ChevronDown;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return BindingOperations.DoNothing;
    }
}