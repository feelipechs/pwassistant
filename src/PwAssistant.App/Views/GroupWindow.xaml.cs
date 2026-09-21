using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private readonly System.Windows.Threading.DispatcherTimer _onlinePoller;
    private Point _dragStartPoint;

    public GroupWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        TitleLabel.Text = Strings.GroupMode;
        PoolLabel.Text = Strings.Ungrouped;
        MiniModeButton.Content = Strings.MiniMode;
        AddGroupButton.ToolTip = Strings.NewGroup;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
        // Event-oriented auto-refresh: picks up client deaths/starts without
        // requiring a window re-activation (cheap no-op when nothing changed).
        _onlinePoller = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromSeconds(2),
            System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => ViewModel.RefreshIfOnlineChanged(),
            Dispatcher);
        _onlinePoller.Start();
        Closed += (_, _) => _onlinePoller.Stop();
    }

    private void OnCardActivate(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GroupCard card)
            ViewModel.ActiveCard = card;
    }

    private void OnCardMenuToggle(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is GroupCard card)
            card.IsMenuOpen = !card.IsMenuOpen;
    }

    private void OnCardMenuClosed(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Popup popup
            && popup.DataContext is GroupCard card)
            card.IsMenuOpen = false;
    }

    private DragAdorner? _dragAdorner;
    private InsertionAdorner? _insertionAdorner;
    private UIElement? _insertionHost;
    private int _pendingIndex;

    private void OnPoolPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

    private void OnPoolPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ItemsControl list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        MemberOption? option = FindDataContext<MemberOption>(
            list.InputHitTest(e.GetPosition(list)) as DependencyObject);
        if (option is null) return;
        BeginMemberDrag(list, new MemberDrag(option.Account.Id, null), option.CharacterName);
    }

    private void OnMemberPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

    private void OnMemberPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ItemsControl list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        MemberOption? option = FindDataContext<MemberOption>(list.InputHitTest(e.GetPosition(list)) as DependencyObject);
        GroupCard? card = FindDataContext<GroupCard>(list);
        if (option is null || card is null) return;
        BeginMemberDrag(list, new MemberDrag(option.Account.Id, card.Group.Id), option.CharacterName);
    }

    private void BeginMemberDrag(FrameworkElement source, MemberDrag payload, string ghostText)
    {
        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(this);
        if (layer is not null)
        {
            var ghost = new Border
            {
                Background = (Brush)FindResource("CardBrush"),
                BorderBrush = (Brush)FindResource("AccentBrush"),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Child = new TextBlock { Text = ghostText }
            };
            _dragAdorner = new DragAdorner(this, ghost, Mouse.GetPosition(this));
            layer.Add(_dragAdorner);
        }
        source.GiveFeedback += OnDragFeedback;
        ViewModel.IsDragging = true;
        try
        {
            DragDrop.DoDragDrop(source, payload, DragDropEffects.Move);
        }
        finally
        {
            source.GiveFeedback -= OnDragFeedback;
            if (layer is not null && _dragAdorner is not null)
                layer.Remove(_dragAdorner);
            _dragAdorner = null;
            ClearInsertion();
            _dragDepths.Clear();
            ViewModel.IsDragging = false;
            foreach (GroupCard card in ViewModel.GroupCards)
                card.IsDragOver = false;
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

    private void OnDragFeedback(object? sender, GiveFeedbackEventArgs e)
    {
        _dragAdorner?.Move(Mouse.GetPosition(this));
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private static GroupCard? CardOf(object? sender) =>
        (sender as FrameworkElement)?.DataContext as GroupCard;

    private readonly Dictionary<GroupCard, int> _dragDepths = new();

    private static bool HasMemberDrag(DragEventArgs e) =>
        e.Data.GetDataPresent(typeof(MemberDrag));

    private static MemberDrag? DragOf(DragEventArgs e) =>
        e.Data.GetData(typeof(MemberDrag)) as MemberDrag;

    private void OnPoolDragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragOf(e)?.SourceGroupId is not null ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnPoolDrop(object sender, DragEventArgs e)
    {
        MemberDrag? drag = DragOf(e);
        if (drag is null) return;
        try
        {
            await ViewModel.UngroupMemberAsync(drag.AccountId, drag.SourceGroupId);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }

    private void OnCardDragEnter(object sender, DragEventArgs e)
    {
        if (CardOf(sender) is GroupCard card && HasMemberDrag(e))
        {
            _dragDepths[card] = _dragDepths.TryGetValue(card, out int depth) ? depth + 1 : 1;
            card.IsDragOver = true;
            foreach (GroupCard other in ViewModel.GroupCards)
                if (other != card)
                    other.IsDragOver = false;
        }
    }

    private void OnCardDragOver(object sender, DragEventArgs e)
    {
        e.Effects = CardOf(sender) is not null && HasMemberDrag(e)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCardDragLeave(object sender, DragEventArgs e)
    {
        if (CardOf(sender) is not GroupCard card) return;
        int depth = _dragDepths.TryGetValue(card, out int n) ? n - 1 : 0;
        if (depth <= 0)
        {
            _dragDepths.Remove(card);
            card.IsDragOver = false;
        }
        else
        {
            _dragDepths[card] = depth;
        }
    }

    private async void OnCardDrop(object sender, DragEventArgs e)
    {
        MemberDrag? drag = DragOf(e);
        if (drag is null) return;
        if (CardOf(sender) is not GroupCard card) return;
        card.IsDragOver = false;
        try
        {
            await ViewModel.MoveMemberAsync(drag.AccountId, drag.SourceGroupId, card.Group.Id, null);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }

    /// <summary>Live insertion preview inside a card member list.</summary>
    private void OnMembersDragOver(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl list || DragOf(e) is null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        _pendingIndex = InsertionPreview.IndexAt(list, e.GetPosition(list), out double y);
        ShowInsertion(list, y);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnMembersDragLeave(object sender, DragEventArgs e) => ClearInsertion();

    private async void OnMembersDrop(object sender, DragEventArgs e)
    {
        MemberDrag? drag = DragOf(e);
        GroupCard? card = CardOf(sender);
        int index = sender is ItemsControl list
            ? InsertionPreview.IndexAt(list, e.GetPosition(list), out _)
            : _pendingIndex;
        ClearInsertion();
        // Single drop: the card border below would run the same move again.
        e.Handled = true;
        if (drag is null || card is null) return;
        card.IsDragOver = false;
        try
        {
            await ViewModel.MoveMemberAsync(drag.AccountId, drag.SourceGroupId, card.Group.Id, index);
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
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

    private static void RefreshMainHotkeys()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.RefreshPresetHotkeys();
    }
}
