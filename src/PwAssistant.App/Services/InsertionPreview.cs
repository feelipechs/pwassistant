using System.Windows;
using System.Windows.Controls;

namespace PwAssistant.App.Services;

/// <summary>Shared insertion-index math for drag-and-drop reorder lists.</summary>
public static class InsertionPreview
{
    /// <summary>
    /// Returns the index where a drop at <paramref name="position"/> would
    /// land, plus the Y of the gold insertion line.
    /// </summary>
    public static int IndexAt(ItemsControl list, Point position, out double y)
    {
        int count = list.Items.Count;
        for (int i = 0; i < count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
                continue;
            Point top = container.TranslatePoint(new Point(0, 0), list);
            if (position.Y < top.Y + container.ActualHeight / 2)
            {
                y = top.Y;
                return i;
            }
        }
        y = count == 0 ? 0 : EndY(list, count - 1);
        return count;
    }

    private static double EndY(ItemsControl list, int last)
    {
        if (list.ItemContainerGenerator.ContainerFromIndex(last) is FrameworkElement container)
        {
            Point top = container.TranslatePoint(new Point(0, 0), list);
            return top.Y + container.ActualHeight;
        }
        return list.ActualHeight;
    }
}
