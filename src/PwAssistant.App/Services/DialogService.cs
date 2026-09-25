using System.Windows;
using PwAssistant.App.ViewModels;
using PwAssistant.App.Views;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Services;

/// <summary>
/// Routes dialog requests to the live windows. All lookups assume the UI
/// thread, exactly like the direct window calls they replace.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;
    private readonly Func<MiniWindow> _miniWindowFactory;

    public DialogService(IServiceProvider services, Func<MiniWindow> miniWindowFactory)
    {
        _services = services;
        _miniWindowFactory = miniWindowFactory;
    }

    private static MainWindow? Main =>
        Application.Current.MainWindow as MainWindow;

    private static GroupWindow? OwnWindow(GroupViewModel owner) =>
        Application.Current.Windows
            .OfType<GroupWindow>()
            .FirstOrDefault(w => ReferenceEquals(w.ViewModel, owner));

    public Task<(bool Ok, string Name, string Path)> AskServerAsync(string? name, string? path) =>
        Main?.AskServerAsync(name, path) ?? Task.FromResult((false, string.Empty, string.Empty));

    public Task<AccountDraft?> AskAccountAsync(
        AccountDraft initial, IEnumerable<string> knownTags, string? lockTag, bool requirePassword) =>
        Main?.AskAccountAsync(initial, knownTags, lockTag, requirePassword)
            ?? Task.FromResult<AccountDraft?>(null);

    public Task<(bool Ok, string Value)> AskPromptAsync(string labelKey, string initial) =>
        Main?.AskPromptAsync(labelKey, initial) ?? Task.FromResult((false, initial));

    public Task<bool> AskConfirmAsync(string title, string message, string confirmLabel) =>
        Main?.AskConfirmAsync(title, message, confirmLabel) ?? Task.FromResult(false);

    public void ShowGroupWindow()
    {
        var window = (GroupWindow)_services.GetService(typeof(GroupWindow))!;
        window.Show();
    }

    public Task<(bool Ok, string Value)> AskGroupPromptAsync(GroupViewModel owner, string labelKey, string initial)
    {
        GroupWindow? window = OwnWindow(owner);
        if (window is null)
            return Task.FromResult((false, initial));
        return window.AskPromptAsync(labelKey, initial);
    }

    public Task<bool> AskGroupConfirmAsync(GroupViewModel owner, string title, string message, string confirmLabel)
    {
        GroupWindow? window = OwnWindow(owner);
        if (window is null)
            return Task.FromResult(false);
        return window.AskConfirmAsync(title, message, confirmLabel);
    }

    public void ShowPresetsTab(GroupViewModel owner) => OwnWindow(owner)?.ShowPresetsTab();

    public void ShowFormationsSheet(GroupViewModel owner) => OwnWindow(owner)?.ShowFormationsSheet();

    public void LoadPresetInTab(GroupViewModel owner, Preset preset, bool isNew) =>
        OwnWindow(owner)?.LoadPresetInTab(preset, isNew);

    /// <summary>
    /// Card minimize: the single Mini retargets to the group and the Group
    /// window hides (inaccessible until the Mini closes). Main stays put —
    /// background is Main's own close gesture, not the Mini flow.
    /// Closing the Mini shows the Group again.
    /// </summary>
    public void MinimizeGroupToMini(GroupViewModel owner, Group group)
    {
        GroupWindow? groupWindow = OwnWindow(owner);
        MiniWindow? existing = Application.Current.Windows.OfType<MiniWindow>().FirstOrDefault();
        if (existing is not null)
        {
            RetargetMini(existing.ViewModel, group);
            existing.Activate();
        }
        else
        {
            MiniWindow mini = _miniWindowFactory();
            mini.Loaded += (_, _) => RetargetMini(mini.ViewModel, group);
            mini.Closed += (_, _) =>
            {
                if (mini.Dispatcher.HasShutdownStarted) return;
                groupWindow?.Show();
            };
            mini.Show();
        }
        groupWindow?.Hide();
    }

    private static void RetargetMini(GroupViewModel vm, Group group) =>
        vm.ActiveCard = vm.GroupCards.FirstOrDefault(c => c.Group.Id == group.Id);
}
