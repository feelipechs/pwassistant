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

    private void OnPoolPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragStartPoint = e.GetPosition(null);

    private void OnPoolPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not ListBox list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        ListBoxItem? item = FindRowContainer(list, e.GetPosition(list));
        if (item?.DataContext is not MemberOption option) return;

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
                Child = new TextBlock { Text = option.CharacterName }
            };
            _dragAdorner = new DragAdorner(this, ghost, Mouse.GetPosition(this));
            layer.Add(_dragAdorner);
        }
        list.GiveFeedback += OnDragFeedback;
        try
        {
            DragDrop.DoDragDrop(item, option.Account.Id, DragDropEffects.Move);
        }
        finally
        {
            list.GiveFeedback -= OnDragFeedback;
            if (layer is not null && _dragAdorner is not null)
                layer.Remove(_dragAdorner);
            _dragAdorner = null;
            foreach (GroupCard card in ViewModel.GroupCards)
                card.IsDragOver = false;
        }
    }

    private void OnDragFeedback(object? sender, GiveFeedbackEventArgs e)
    {
        _dragAdorner?.Move(Mouse.GetPosition(this));
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private static GroupCard? CardOf(object? sender) =>
        (sender as FrameworkElement)?.DataContext as GroupCard;

    private void OnCardDragOver(object sender, DragEventArgs e)
    {
        GroupCard? card = CardOf(sender);
        if (card is not null && e.Data.GetDataPresent(typeof(Guid)))
        {
            card.IsDragOver = true;
            foreach (GroupCard other in ViewModel.GroupCards)
                if (other != card)
                    other.IsDragOver = false;
        }
        e.Effects = card is not null && e.Data.GetDataPresent(typeof(Guid))
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnCardDragLeave(object sender, DragEventArgs e)
    {
        if (CardOf(sender) is GroupCard card)
            card.IsDragOver = false;
    }

    private async void OnCardDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(Guid))) return;
        if (CardOf(sender) is not GroupCard card) return;
        if (e.Data.GetData(typeof(Guid)) is not Guid accountId) return;
        card.IsDragOver = false;
        await ViewModel.DropAccountOntoGroupAsync(accountId, card.Group.Id);
    }

    private async void OnLoadFormationClick(object sender, RoutedEventArgs e)
    {
        FrameworkElement? element = sender as FrameworkElement;
        if (element?.DataContext is not Formation formation) return;
        DependencyObject? node = element;
        while (node is not null && (node as FrameworkElement)?.DataContext is not GroupCard)
            node = VisualTreeHelper.GetParent(node);
        if ((node as FrameworkElement)?.DataContext is not GroupCard card) return;
        card.IsMenuOpen = false;
        await ViewModel.LoadFormationForAsync(card, formation);
    }

    private static ListBoxItem? FindRowContainer(ListBox list, Point position)
    {
        if (list.InputHitTest(position) is not DependencyObject hit) return null;
        while (hit is not null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);
        return hit as ListBoxItem;
    }

    private static void RefreshMainHotkeys()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.RefreshPresetHotkeys();
    }
}
