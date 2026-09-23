using System.Windows;
using System.Windows.Controls;

namespace PwAssistant.App.Services;

/// <summary>Shared insertion-index math for drag-and-drop reorder lists.</summary>
public static class InsertionPreview
{
    /// <summary>
    /// Returns the index where a drop at <paramref name="position"/> would
    /// land, plus the Y of the white insertion line.
    /// <paramref name="fraction"/> is how far into a row (0–1) the slot
    /// flips: 0.5 = midpoint (insertion lines), ~0.1 = on touch (live move).
    /// <paramref name="movingUp"/> mirrors the threshold to the entry edge
    /// (top + height·(1−fraction)) so upward drags swap on touch too —
    /// without it, upward swaps only trigger near the row end.
    /// </summary>
    public static int IndexAt(ItemsControl list, Point position, out double y, double fraction = 0.5, bool movingUp = false)
    {
        double f = Math.Clamp(movingUp ? 1 - fraction : fraction, 0, 1);
        int count = list.Items.Count;
        for (int i = 0; i < count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container)
                continue;
            Point top = container.TranslatePoint(new Point(0, 0), list);
            if (position.Y < top.Y + container.ActualHeight * f)
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
