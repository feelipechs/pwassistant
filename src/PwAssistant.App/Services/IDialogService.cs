using PwAssistant.App.ViewModels;
using PwAssistant.App.Views;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Services;

/// <summary>
/// Window-facing UI behind an interface so ViewModels never reference
/// concrete Window types (MVVM seam, unit-testable). Implementations route
/// to the live Main/Group windows; every method is a safe no-op (or
/// negative answer) when its window is absent — preserving the old
/// "if window is null return" semantics.
/// </summary>
public interface IDialogService
{
    // Main window sheets.
    Task<(bool Ok, string Name, string Path)> AskServerAsync(string? name, string? path);
    Task<AccountDraft?> AskAccountAsync(
        AccountDraft initial, IEnumerable<string> knownTags, string? lockTag, bool requirePassword);
    Task<(bool Ok, string Value)> AskPromptAsync(string labelKey, string initial);
    Task<bool> AskConfirmAsync(string title, string message, string confirmLabel);
    void ShowGroupWindow();

    // Group window sheets, routed to the window owning `owner`.
    Task<(bool Ok, string Value)> AskGroupPromptAsync(GroupViewModel owner, string labelKey, string initial);
    Task<bool> AskGroupConfirmAsync(GroupViewModel owner, string title, string message, string confirmLabel);
    void ShowPresetsTab(GroupViewModel owner);
    void ShowFormationsSheet(GroupViewModel owner);
    void LoadPresetInTab(GroupViewModel owner, Preset preset, bool isNew);
    void MinimizeGroupToMini(GroupViewModel owner, Group group);
}
