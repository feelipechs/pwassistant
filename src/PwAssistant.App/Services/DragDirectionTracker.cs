using System.Windows;

namespace PwAssistant.App.Services;

/// <summary>Tracks pointer direction across DragOver events (window Y).</summary>
public sealed class DragDirectionTracker
{
    private double _lastY;
    private bool _hasY;

    /// <summary>True once the pointer moved upward during the drag.</summary>
    public bool MovingUp { get; private set; }

    public void Reset()
    {
        _hasY = false;
        MovingUp = false;
    }

    /// <summary>Feeds one DragOver position; returns the current direction.</summary>
    public bool Track(DragEventArgs e, IInputElement relativeTo)
    {
        double y = e.GetPosition(relativeTo).Y;
        if (_hasY)
        {
            if (y < _lastY) MovingUp = true;
            else if (y > _lastY) MovingUp = false;
        }
        _lastY = y;
        _hasY = true;
        return MovingUp;
    }
}
