using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PwAssistant.App.Resources;
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
        NewGroupHint.Text = Strings.NewGroup;
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

    private void OnCardMenuClosed(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.Popup popup
            && popup.DataContext is GroupCard card)
            card.IsMenuOpen = false;
    }

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
        DragDrop.DoDragDrop(item, option.Account.Id, DragDropEffects.Move);
    }

    private void OnCardDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(Guid)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnCardDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(Guid))) return;
        if ((sender as FrameworkElement)?.DataContext is not GroupCard card) return;
        if (e.Data.GetData(typeof(Guid)) is not Guid accountId) return;
        await ViewModel.DropAccountOntoGroupAsync(accountId, card.Group.Id);
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
