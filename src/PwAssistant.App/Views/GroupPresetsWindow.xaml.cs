using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

/// <summary>Per-group preset manager. The group is activated on open, so the
/// shared preset commands (and Mini rows) operate on it. Rows reorder by
/// drag and drop with a live insertion preview.</summary>
public partial class GroupPresetsWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private Point _dragStartPoint;
    private InsertionAdorner? _insertionAdorner;
    private UIElement? _insertionHost;

    public GroupPresetsWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
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
        DragDrop.DoDragDrop(list, preset, DragDropEffects.Move);
    }

    private void OnPresetDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list || e.Data.GetData(typeof(Preset)) is not Preset)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        int index = InsertionPreview.IndexAt(list, e.GetPosition(list), out double y);
        ShowInsertion(list, y);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnPresetDragLeave(object sender, DragEventArgs e) => ClearInsertion();

    private async void OnPresetDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list) return;
        int index = InsertionPreview.IndexAt(list, e.GetPosition(list), out _);
        ClearInsertion();
        e.Handled = true;
        if (e.Data.GetData(typeof(Preset)) is not Preset preset) return;
        Guid? groupId = ViewModel.SelectedGroup?.Id;
        if (groupId is null) return;
        try
        {
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

    private void ShowInsertion(UIElement host, double y)
    {
        if (_insertionHost != host || _insertionAdorner is null)
        {
            ClearInsertion();
            AdornerLayer? layer = AdornerLayer.GetAdornerLayer(host);
            if (layer is null) return;
            _insertionAdorner = new InsertionAdorner(host);
            layer.Add(_insertionAdorner);
            _insertionHost = host;
        }
        _insertionAdorner.SetY(y);
    }

    private void ClearInsertion()
    {
        if (_insertionHost is not null && _insertionAdorner is not null)
            AdornerLayer.GetAdornerLayer(_insertionHost)?.Remove(_insertionAdorner);
        _insertionAdorner = null;
        _insertionHost = null;
    }
}
