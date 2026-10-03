using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PwAssistant.Avalonia.Services;

/// <summary>MiniCard-look lifted ghost shared by all drag sources.</summary>
public static class DragGhost
{
    public static Border ForText(Control scope, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        return new Border
        {
            Background = scope.FindResource("Brush.Raised") as IBrush ?? Brushes.Transparent,
            BorderBrush = scope.FindResource("Brush.Primary") as IBrush ?? Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Child = row,
            Opacity = 0.95,
            // TODO(Avalonia): WPF DropShadowEffect has no direct equivalent;
            // BoxShadow is the closest Avalonia visual (offset/blur kept).
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 4,
                Blur = 12,
                Color = Colors.Black
            })
        };
    }
}
