using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

/// <summary>Saved formations for a group card. Clicking one applies it
/// to the card and closes.</summary>
public partial class GroupFormationsWindow : Window
{
    private readonly GroupViewModel _viewModel;
    private readonly GroupCard _card;

    public GroupFormationsWindow(GroupViewModel viewModel, GroupCard card)
    {
        _viewModel = viewModel;
        _card = card;
        DataContext = viewModel;
        InitializeComponent();
        Title = $"{Strings.LoadFormation} — {card.Group.Name}";
        FormationsLabel.Text = Strings.Formations;
    }

    private async void OnLoadFormationClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Formation formation) return;
        await _viewModel.LoadFormationForAsync(_card, formation);
        Close();
    }
}
