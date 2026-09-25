using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

/// <summary>Saved formations panel (replaces the GroupFormationsWindow):
/// clicking one applies it to the active card and collapses.</summary>
public partial class FormationsSheet : UserControl
{
    public FormationsSheet()
    {
        InitializeComponent();
        FormationsLabel.Text = Strings.Formations;
    }

    public void Show()
    {
        Visibility = Visibility.Visible;
        Dispatcher.InvokeAsync(Focus);
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    private async void OnLoadFormationClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Formation formation) return;
        if (DataContext is not GroupViewModel vm) return;
        if (vm.ActiveCard is not GroupCard card) return;
        await vm.LoadFormationForAsync(card, formation);
        Hide();
    }

    private async void OnRenameFormationClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Formation formation) return;
        if (DataContext is not GroupViewModel vm) return;
        await vm.RenameFormationAsync(formation);
    }

    private async void OnDeleteFormationClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Formation formation) return;
        if (DataContext is not GroupViewModel vm) return;
        await vm.DeleteFormationAsync(formation);
    }

    private void OnExpandFormationDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement header) return;
        // Up to the row first (a button has no children to search down).
        DependencyObject? row = header;
        while (row is not null && row is not Border)
            row = VisualTreeHelper.GetParent(row);
        if (row is null) return;
        if (FindDescendant<ItemsControl>(row, "MemberNames") is not ItemsControl list) return;
        if (FindDescendant<TextBlock>(row, "ExpandGlyph") is not TextBlock glyph) return;
        if (list.Visibility == Visibility.Visible)
        {
            list.Visibility = Visibility.Collapsed;
            glyph.Text = "\uE76C";
            return;
        }
        if (header.DataContext is not Formation formation) return;
        if (DataContext is not GroupViewModel vm) return;
        list.ItemsSource = vm.GetFormationMemberNames(formation);
        list.Visibility = Visibility.Visible;
        glyph.Text = "\uE70D";
    }

    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && match.Name == name)
                return match;
            if (FindDescendant<T>(child, name) is T found)
                return found;
        }
        return null;
    }

    private void OnDimmerDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Dimmer))
            Hide();
    }

    private void OnSheetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
        }
    }
}
