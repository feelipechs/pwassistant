using System.Windows;
using System.Windows.Interop;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Models;
using PwAssistant.WinApi;
using AppStrings = PwAssistant.App.Resources.Strings;

namespace PwAssistant.App.Views;

/// <summary>
/// M6: global preset hotkeys (RegisterHotKey) so presets fire while the app
/// is unfocused. Format per preset: "CTRL+SHIFT+F9" (modifiers + key).
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly PresetDispatcher _dispatcher;
    private GlobalHotKeyManager? _hotkeys;
    private bool _hotkeysRegistered;
    private readonly TrayManager _tray = new();
    private bool _allowClose;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, AppState state, PresetDispatcher dispatcher)
    {
        _state = state;
        _dispatcher = dispatcher;
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        ServersLabel.Text = AppStrings.Servers;
        AddServerButton.ToolTip = AppStrings.AddServer;
        AddAccountButton.ToolTip = AppStrings.AddAccount;
        AddTabButton.ToolTip = AppStrings.NewTab;
        RenameTabButton.ToolTip = AppStrings.Rename;
        DeleteTabButton.ToolTip = AppStrings.Delete;
        GroupModeButton.Content = AppStrings.GroupMode;
        SettingsButton.ToolTip = AppStrings.Settings;
        RefreshButton.ToolTip = AppStrings.Refresh;
        SourceInitialized += OnSourceInitialized;
        _tray.OpenRequested += (_, _) => RestoreFromBackground();
        _tray.ExitRequested += (_, _) =>
        {
            _allowClose = true;
            Application.Current.Shutdown();
        };
    }

    /// <summary>Background: leave the taskbar, live in the tray instead.</summary>
    public void SendToBackground()
    {
        if (!IsVisible) return;
        Hide();
        _tray.Show();
    }

    /// <summary>Returns from the tray to a normal visible window.</summary>
    public void RestoreFromBackground()
    {
        _tray.Hide();
        if (!IsVisible)
            Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    /// <summary>In-window confirm sheet (no extra window).</summary>
    public Task<bool> AskConfirmAsync(string title, string message, string confirmLabel) =>
        ConfirmSheetHost.AskAsync(title, message, confirmLabel);

    /// <summary>In-window text prompt sheet (no extra window).</summary>
    public Task<(bool Ok, string Value)> AskPromptAsync(string labelKey, string initial) =>
        PromptSheetHost.AskAsync(labelKey, initial);

    /// <summary>In-window server editor sheet (no extra window).</summary>
    public Task<(bool Ok, string Name, string Path)> AskServerAsync(string? name, string? path) =>
        ServerSheetHost.AskAsync(name, path);

    /// <summary>In-window account editor sheet (no extra window).</summary>
    public Task<AccountDraft?> AskAccountAsync(
        AccountDraft initial, IEnumerable<string> knownTags, string? lockTag, bool requirePassword) =>
        AccountSheetHost.EditAsync(initial, knownTags, lockTag, requirePassword);

    /// <summary>In-window settings sheet (no extra window).</summary>
    public Task<bool> EditSettingsAsync() => SettingsSheetHost.EditAsync(_state);

    private async void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        await EditSettingsAsync();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    /// <summary>
    /// Re-registers from the current preset list (after B2 mutations).
    /// Disposes previous registrations first; safe to call repeatedly.
    /// </summary>
    public void RefreshPresetHotkeys()
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
        _hotkeysRegistered = false;
        RegisterPresetHotkeys();
    }

    /// <summary>
    /// (Re)registers global hotkeys for presets that declare one. Must run
    /// AFTER AppState.LoadAsync — at SourceInitialized time the preset list
    /// is still empty, so App calls this again once data is loaded.
    /// Idempotent: presets added later re-register via the B2 editor.
    /// </summary>
    public void RegisterPresetHotkeys()
    {
        if (_hotkeysRegistered) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        _hotkeys ??= new GlobalHotKeyManager(hwnd);

        foreach (Preset preset in _state.Data.Presets.Where(p => !string.IsNullOrWhiteSpace(p.Hotkey)))
        {
            try
            {
                (uint modifiers, uint vk) = ParseHotkey(preset.Hotkey!);
                Preset captured = preset;
                _hotkeys.Register(modifiers, vk, () => _ = _dispatcher.FireAsync(captured));
            }
            catch (Exception)
            {
                // One bad hotkey string never breaks the app startup.
            }
        }
        _hotkeysRegistered = true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotKeyManager.WM_HOTKEY)
        {
            _hotkeys?.Dispatch(wParam.ToInt32());
            handled = true;
        }
        return IntPtr.Zero;
    }

    internal static (uint Modifiers, uint Vk) ParseHotkey(string hotkey)
    {
        const uint MOD_ALT = 0x0001;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;
        const uint MOD_WIN = 0x0008;

        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            throw new ArgumentException($"Hotkey needs modifiers + key: {hotkey}");

        uint modifiers = 0;
        foreach (string part in parts[..^1])
        {
            modifiers |= part.ToUpperInvariant() switch
            {
                "ALT" => MOD_ALT,
                "CTRL" or "CONTROL" => MOD_CONTROL,
                "SHIFT" => MOD_SHIFT,
                "WIN" or "WINDOWS" => MOD_WIN,
                _ => throw new ArgumentException($"Unknown hotkey modifier: {part}")
            };
        }

        return (modifiers, (uint)KeyMapper.Resolve(parts[^1]));
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // The X button backgrounds to the tray; only the tray menu exits.
        if (!_allowClose)
        {
            e.Cancel = true;
            SendToBackground();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeys?.Dispose();
        _tray.Dispose();
        base.OnClosed(e);
    }
}
