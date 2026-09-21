using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace PwAssistant.App.Services;

/// <summary>Ghost following the cursor during pool-to-card drags.</summary>
public sealed class DragAdorner : Adorner
{
    private readonly ContentPresenter _presenter;
    private Point _offset;

    public DragAdorner(UIElement adorned, object content, Point offset)
        : base(adorned)
    {
        _offset = offset;
        _presenter = new ContentPresenter { Content = content, Opacity = 0.9 };
        IsHitTestVisible = false;
    }

    public void Move(Point position)
    {
        _offset = position;
        (Parent as AdornerLayer)?.Update(AdornedElement);
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _presenter;

    protected override Size MeasureOverride(Size constraint)
    {
        _presenter.Measure(constraint);
        return _presenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _presenter.Arrange(new Rect(finalSize));
        return finalSize;
    }

    public override GeneralTransform GetDesiredTransform(GeneralTransform transform)
    {
        var result = new GeneralTransformGroup();
        result.Children.Add(base.GetDesiredTransform(transform));
        result.Children.Add(new TranslateTransform(_offset.X + 12, _offset.Y + 12));
        return result;
    }
}
