using System.Windows;
using PwHelper.App.Resources;
using PwHelper.App.Services;
using PwHelper.App.ViewModels;
using PwHelper.Core.Input;
using PwHelper.Core.Models;
using PwHelper.WinApi;

namespace PwHelper.App.Views;

public partial class GroupWindow : Window
{
    private readonly AppState _state;
    private readonly IWindowResolver _resolver;

    public GroupViewModel ViewModel { get; }

    public GroupWindow(GroupViewModel viewModel, AppState state, IWindowResolver resolver)
    {
        ViewModel = viewModel;
        _state = state;
        _resolver = resolver;
        DataContext = viewModel;
        InitializeComponent();
        SyncCheck.Content = Strings.SyncEnabled;
        LimitationLabel.Text = Strings.GroundClickLimitation;
        PresetsLabel.Text = Strings.Presets;
        CancelButton.Content = Strings.Cancel;
        CaptureTestButton.Content = Strings.CaptureTest;
        Loaded += (_, _) => ViewModel.Initialize();
    }

    // TEMP diagnostic for the M5 overlay validation (remove when B2 lands):
    // opens the capture overlay and reports the fraction for the first
    // online member, proving capture-over-game works.
    private void OnCaptureTest(object sender, RoutedEventArgs e)
    {
        _state.RefreshOnlineStatus();
        Account? account = ViewModel.OnlineMembers.FirstOrDefault();
        if (account is null)
        {
            ViewModel.StatusMessage = Strings.Offline;
            return;
        }

        var overlay = new ClickCaptureOverlay();
        overlay.ShowDialog();
        if (overlay.CapturedScreenPoint is null)
            return;

        try
        {
            (int x, int y) = _resolver.ScreenToClientPoint(
                account.WindowHandle,
                (int)overlay.CapturedScreenPoint.Value.X,
                (int)overlay.CapturedScreenPoint.Value.Y);
            (int w, int h) = _resolver.GetClientSize(account.WindowHandle);
            var fraction = RelativePosition.FromAbsolute(x, y, w, h);
            ViewModel.StatusMessage = $"captured=({fraction.X:F2},{fraction.Y:F2}) [{account.Role}]";
        }
        catch (WinApiException ex)
        {
            ViewModel.StatusMessage = ex.Message;
        }
    }
}
