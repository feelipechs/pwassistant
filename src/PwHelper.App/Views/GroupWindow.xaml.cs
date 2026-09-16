using System.Windows;
using PwHelper.App.Resources;
using PwHelper.App.ViewModels;

namespace PwHelper.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    public GroupWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        SyncCheck.Content = Strings.SyncEnabled;
        LimitationLabel.Text = Strings.GroundClickLimitation;
        PresetsLabel.Text = Strings.Presets;
        CancelButton.Content = Strings.Cancel;
        Loaded += (_, _) => ViewModel.Initialize();
    }
}
