using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

/// <summary>Per-group preset manager. The group is activated on open, so the
/// shared preset commands (and Mini rows) operate on it. Rows reorder by
/// drag and drop with a live Trello-style preview (group parity).</summary>
public partial class GroupPresetsWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private Point _dragStartPoint;
    private Border? _ghost;
    private bool _dropHandled;
    private readonly DragDirectionTracker _direction = new();

    /// <summary>LiveMove flips the slot ~10% into a row (on touch).</summary>
    private const double LiveSwapFraction = 0.1;

    public GroupPresetsWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        DialogOwner.Own(this);
        Title = $"{Strings.Presets} — {viewModel.SelectedGroup?.Name}";
        PresetsLabel.Text = Strings.Presets;
        NewPresetButton.ToolTip = Strings.NewPreset;
    }

    private void OnPresetPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

    private void OnPresetPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ItemsControl list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        // Buttons keep working: drag starts only from passive surfaces.
        if (e.OriginalSource is ButtonBase) return;
        Preset? preset = FindDataContext<Preset>(list.InputHitTest(e.GetPosition(list)) as DependencyObject);
        if (preset is null) return;
        BeginPresetDrag(list, preset);
    }

    private void BeginPresetDrag(FrameworkElement source, Preset preset)
    {
        _ghost = DragGhost.ForText(this, preset.Name);
        GhostLayer.Children.Add(_ghost);
        PositionGhost();
        source.GiveFeedback += OnDragFeedback;
        _dropHandled = false;
        _direction.Reset();
        try
        {
            DragDrop.DoDragDrop(source, preset, DragDropEffects.Move);
        }
        finally
        {
            source.GiveFeedback -= OnDragFeedback;
            GhostLayer.Children.Remove(_ghost);
            _ghost = null;
            // Cancel/ESC/release outside: LiveMove only touched the UI —
            // restore the view order from the model.
            if (!_dropHandled)
                ViewModel.RevertPresetOrder();
            _dropHandled = false;
        }
    }

    private void OnDragFeedback(object? sender, GiveFeedbackEventArgs e)
    {
        PositionGhost();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void PositionGhost(Point? anchor = null)
    {
        if (_ghost is null) return;
        Point p = anchor ?? Mouse.GetPosition(GhostLayer);
        Canvas.SetLeft(_ghost, p.X + 14);
        Canvas.SetTop(_ghost, p.Y + 14);
    }

    private void OnPresetDragOver(object sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (sender is not ItemsControl list || e.Data.GetData(typeof(Preset)) is not Preset preset)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        LiveMove(list, preset, e);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>Trello-style: the row moves in the UI while hovering.</summary>
    private void LiveMove(ItemsControl list, Preset preset, DragEventArgs e)
    {
        int from = ViewModel.Presets.IndexOf(preset);
        if (from < 0) return;
        int to = InsertionPreview.IndexAt(list, e.GetPosition(list), out _, LiveSwapFraction, _direction.Track(e, this));
        if (to > from) to--;
        to = Math.Clamp(to, 0, ViewModel.Presets.Count - 1);
        if (to != from)
            ViewModel.Presets.Move(from, to);
    }

    private async void OnPresetDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list) return;
        if (e.Data.GetData(typeof(Preset)) is not Preset preset) return;
        _dropHandled = true;
        Guid? groupId = ViewModel.SelectedGroup?.Id;
        if (groupId is null) return;
        try
        {
            // Drop where released; the hover preview already placed it nearby.
            LiveMove(list, preset, e);
            int index = ViewModel.Presets.IndexOf(preset);
            await ViewModel.MovePresetAsync(groupId.Value, preset.Id, index);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }

    private static T? FindDataContext<T>(DependencyObject? node) where T : class
    {
        while (node is not null)
        {
            if ((node as FrameworkElement)?.DataContext is T match)
                return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
