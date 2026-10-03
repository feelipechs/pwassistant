using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PwAssistant.Avalonia.Resources;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.Avalonia.Views;

/// <summary>Per-group preset list with Trello-style live reorder (migrated
/// from GroupPresetsWindow into the Group tab). DataContext is the shared
/// GroupViewModel; row commands resolve through the parent window.</summary>
public partial class PresetListControl : UserControl
{
    private const string PresetFormat = "application/x-pwassistant-preset";
    private static readonly DataFormat<Preset> PresetDataFormat =
        DataFormat.CreateInProcessFormat<Preset>(PresetFormat);

    /// <summary>Press-to-move threshold (WPF used SystemParameters minimums).</summary>
    private const double DragThreshold = 4;

    /// <summary>LiveMove flips the slot ~10% into a row (on touch).</summary>
    private const double LiveSwapFraction = 0.1;

    private GroupViewModel? ViewModel => DataContext as GroupViewModel;

    private Point _pressPoint;
    private PointerPressedEventArgs? _pressTrigger;
    private bool _pressArmed;
    private bool _dragging;
    private Border? _ghost;
    private bool _dropHandled;
    private readonly DragDirectionTracker _direction = new();

    public PresetListControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        PresetsLabel.Text = Strings.Presets;
        ToolTip.SetTip(NewPresetButton, Strings.NewPreset);
    }

    private void OnPresetPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ItemsControl) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressPoint = e.GetPosition(this);
        _pressTrigger = e;
        _pressArmed = true;
    }

    private async void OnPresetPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressArmed || _dragging) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressArmed = false;
            return;
        }
        if (sender is not ItemsControl list) return;
        Point current = e.GetPosition(this);
        if (Math.Abs(current.X - _pressPoint.X) < DragThreshold &&
            Math.Abs(current.Y - _pressPoint.Y) < DragThreshold)
            return;
        _pressArmed = false;
        // Buttons keep working: drag starts only from passive surfaces.
        if (HasButtonAncestor(e.Source)) return;
        Preset? preset = FindDataContext<Preset>(e.Source);
        if (preset is null || _pressTrigger is null) return;
        await BeginPresetDrag(list, _pressTrigger, preset);
    }

    private void OnPresetPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        _pressArmed = false;

    private async Task BeginPresetDrag(Control source, PointerPressedEventArgs trigger, Preset preset)
    {
        _ghost = DragGhost.ForText(this, preset.Name);
        GhostLayer.Children.Add(_ghost);
        PositionGhost(trigger.GetPosition(GhostLayer));
        _dropHandled = false;
        _direction.Reset();
        _dragging = true;
        // Same-application object payload (never leaves the process).
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(PresetDataFormat, preset));
        try
        {
            // Async (no modal loop): DragOver keeps positioning the ghost live.
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move);
        }
        finally
        {
            _dragging = false;
            GhostLayer.Children.Remove(_ghost);
            _ghost = null;
            // Cancel/ESC/release outside: LiveMove only touched the UI —
            // restore the view order from the model.
            if (!_dropHandled)
                ViewModel?.RevertPresetOrder();
            _dropHandled = false;
            _pressTrigger = null;
        }
    }

    private void PositionGhost(Point anchor)
    {
        if (_ghost is null) return;
        Canvas.SetLeft(_ghost, anchor.X + 14);
        Canvas.SetTop(_ghost, anchor.Y + 14);
    }

    private void OnPresetDragOver(object? sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (sender is not ItemsControl list || e.DataTransfer.TryGetValue(PresetDataFormat) is not Preset preset)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        LiveMove(list, preset, e);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>Trello-style: the row moves in the UI while hovering.</summary>
    private void LiveMove(ItemsControl list, Preset preset, DragEventArgs e)
    {
        if (ViewModel is null) return;
        int from = ViewModel.Presets.IndexOf(preset);
        if (from < 0) return;
        int to = InsertionPreview.IndexAt(list, e.GetPosition(list), out _, LiveSwapFraction, _direction.Track(e, this));
        if (to > from) to--;
        to = Math.Clamp(to, 0, ViewModel.Presets.Count - 1);
        if (to != from)
            ViewModel.Presets.Move(from, to);
    }

    private async void OnPresetDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is null) return;
        if (sender is not ItemsControl list) return;
        if (e.DataTransfer.TryGetValue(PresetDataFormat) is not Preset preset) return;
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

    private static bool HasButtonAncestor(object? source)
    {
        Visual? node = source as Visual;
        while (node is not null)
        {
            if (node is Button)
                return true;
            node = node.GetVisualParent();
        }
        return false;
    }

    private static T? FindDataContext<T>(object? source) where T : class
    {
        Visual? node = source as Visual;
        while (node is not null)
        {
            if ((node as Control)?.DataContext is T match)
                return match;
            node = node.GetVisualParent();
        }
        return null;
    }
}
