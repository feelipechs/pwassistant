using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using PwAssistant.Avalonia.Services;

namespace PwAssistant.Avalonia.Views;

/// <summary>Maps a toast kind to its accent bar brush (theme-aware).</summary>
public sealed class KindToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            ToastKind.Warn => "Brush.Warning",
            ToastKind.Error => "Brush.Destructive",
            _ => "Brush.Success",
        };
        if (Application.Current?.TryFindResource(key, out object? found) == true && found is IBrush brush)
            return brush;
        return value switch
        {
            ToastKind.Warn => Brushes.Orange,
            ToastKind.Error => Brushes.Red,
            _ => Brushes.Green,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Overlay host for <see cref="ToastService"/> items:
/// bottom-right, click-through, no code-behind state beyond the source hookup.</summary>
public partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
        List.ItemsSource = ToastService.Items;
    }
}
