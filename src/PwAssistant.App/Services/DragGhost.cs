using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PwAssistant.App.Services;

/// <summary>MiniCard-look lifted ghost shared by all drag sources.</summary>
public static class DragGhost
{
    public static Border ForText(FrameworkElement scope, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        });
        return new Border
        {
            Background = (Brush)scope.FindResource("Brush.Raised"),
            BorderBrush = (Brush)scope.FindResource("Brush.Primary"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 6, 10, 6),
            Child = row,
            Opacity = 0.95,
            Effect = new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 4,
                Opacity = 0.5,
                Color = Colors.Black
            }
        };
    }
}
