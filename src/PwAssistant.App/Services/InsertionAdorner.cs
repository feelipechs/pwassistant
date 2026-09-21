using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PwAssistant.App.Services;

/// <summary>Gold insertion line showing where a dragged member will land.</summary>
public sealed class InsertionAdorner : Adorner
{
    private static readonly Brush LineBrush =
        new SolidColorBrush(Color.FromRgb(0xE8, 0xB9, 0x5C));

    private double _y;

    public InsertionAdorner(UIElement adorned)
        : base(adorned)
    {
        IsHitTestVisible = false;
    }

    public void SetY(double y)
    {
        _y = y;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var pen = new Pen(LineBrush, 2);
        double width = AdornedElement.RenderSize.Width;
        drawingContext.DrawLine(pen, new Point(4, _y), new Point(width - 4, _y));
        drawingContext.DrawEllipse(LineBrush, null, new Point(4, _y), 3, 3);
    }
}
