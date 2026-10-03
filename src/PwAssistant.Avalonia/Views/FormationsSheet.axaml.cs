using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PwAssistant.Avalonia.Resources;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.Avalonia.Views;

/// <summary>Saved formations panel: clicking one applies it to the active
/// card and collapses.</summary>
public partial class FormationsSheet : UserControl
{
    public FormationsSheet()
    {
        InitializeComponent();
        FormationsLabel.Text = Strings.Formations;
    }

    public void Show()
    {
        IsVisible = true;
        Dispatcher.UIThread.Post(() => Dimmer.Focus());
    }

    public void Hide() => IsVisible = false;

    private async void OnLoadFormationClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not Formation formation)
            return;
        if (DataContext is not GroupViewModel vm)
            return;
        if (vm.ActiveCard is not GroupCard card)
            return;
        await vm.LoadFormationForAsync(card, formation);
        Hide();
    }

    private async void OnRenameFormationClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not Formation formation)
            return;
        if (DataContext is not GroupViewModel vm)
            return;
        await vm.RenameFormationAsync(formation);
    }

    private async void OnDeleteFormationClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not Formation formation)
            return;
        if (DataContext is not GroupViewModel vm)
            return;
        await vm.DeleteFormationAsync(formation);
    }

    private void OnExpandFormationDown(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control header)
            return;
        Border? row = header.FindAncestorOfType<Border>();
        if (row is null)
            return;
        ItemsControl? list = row.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(c => c.Name == "MemberNames");
        TextBlock? glyph = row.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(c => c.Name == "ExpandGlyph");
        if (list is null || glyph is null)
            return;
        if (list.IsVisible)
        {
            list.IsVisible = false;
            glyph.Text = "\uE76C";
            return;
        }
        if (header.DataContext is not Formation formation)
            return;
        if (DataContext is not GroupViewModel vm)
            return;
        list.ItemsSource = vm.GetFormationMemberNames(formation);
        list.IsVisible = true;
        glyph.Text = "\uE70D";
    }

    private void OnDimmerDown(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Dimmer))
            Hide();
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Hide();
        }
    }
}
