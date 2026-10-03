using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;
using AppStrings = PwAssistant.Avalonia.Resources.Strings;

namespace PwAssistant.Avalonia.Views;

/// <summary>
/// Global preset hotkeys (RegisterHotKey) so presets fire while the app is
/// unfocused. Format per preset: "CTRL+SHIFT+F9" (modifiers + key).
/// The hotkey sink is a WinApi message-only window (no HwndSource in
/// Avalonia); the X button backgrounds to the tray, only the tray menu exits.
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly PresetDispatcher _dispatcher;
    private readonly FileLogger _log;
    private HotkeyMessageWindow? _sink;
    private GlobalHotKeyManager? _hotkeys;
    private bool _hotkeysRegistered;
    /// <summary>Hotkey auto-repeat guard: RegisterHotKey reports key-down,
    /// so a held hotkey must not fork one job per repeat.</summary>
    private const int HotkeyDebounceMs = 800;
    private readonly Dictionary<Guid, DateTimeOffset> _hotkeyLastFire = new();
    private readonly TrayManager _tray = new();
    private bool _allowClose;
    private readonly IWindowResolver _resolver;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel, AppState state, PresetDispatcher dispatcher, FileLogger log, IWindowResolver resolver)
    {
        _state = state;
        _dispatcher = dispatcher;
        _log = log;
        _resolver = resolver;
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        WindowChrome.ApplyNative(this, ThemeManager.IsDark);
        ServersLabel.Text = AppStrings.Servers;
        ToolTip.SetTip(AddServerButton, AppStrings.AddServer);
        ToolTip.SetTip(AddAccountButton, AppStrings.AddAccount);
        ToolTip.SetTip(AddTabButton, AppStrings.NewTab);
        ToolTip.SetTip(RenameTabButton, AppStrings.Rename);
        ToolTip.SetTip(DeleteTabButton, AppStrings.Delete);
        GroupModeButton.Content = AppStrings.GroupMode;
        ToolTip.SetTip(SettingsButton, AppStrings.Settings);
        ToolTip.SetTip(RefreshButton, AppStrings.Refresh);
        // Installed version in the corner (support/diagnostics); hidden on
        // dev builds where there is no installed version to show.
        if (AppUpdater.TryGetInstalledVersion() is string version)
        {
            VersionLabel.Text = "v" + version;
            VersionLabel.IsVisible = true;
        }
        _state.HotkeysChanged += RefreshPresetHotkeys;
        ToastService.BalloonSink = (title, message) => _tray.ShowBalloon(title, message);
        _tray.OpenRequested += (_, _) => RestoreFromBackground();
        _tray.ExitRequested += (_, _) =>
        {
            _allowClose = true;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
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

    private async void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        await EditSettingsAsync();
    }

    /// <summary>
    /// Re-registers from the current preset list (after B2 mutations).
    /// Disposes previous registrations first; safe to call repeatedly.
    /// </summary>
    public void RefreshPresetHotkeys()
    {
        _hotkeys?.Dispose();
        _hotkeys = null;
        _sink?.Dispose();
        _sink = null;
        _hotkeysRegistered = false;
        RegisterPresetHotkeys();
    }

    /// <summary>
    /// (Re)registers global hotkeys for presets that declare one. Must run
    /// AFTER AppState.LoadAsync — at window-open time the preset list is
    /// still empty, so App calls this again once data is loaded.
    /// Idempotent: presets added later re-register via the B2 editor.
    /// </summary>
    public void RegisterPresetHotkeys()
    {
        if (_hotkeysRegistered) return;

        _sink ??= new HotkeyMessageWindow();
        _sink.MessageReceived += OnSinkMessage;
        _hotkeys ??= new GlobalHotKeyManager(_sink.Handle);

        foreach (Preset preset in _state.Data.Presets.Where(p => !string.IsNullOrWhiteSpace(p.Hotkey)))
        {
            try
            {
                (uint modifiers, uint vk) = ParseHotkey(preset.Hotkey!);
                Preset captured = preset;
                _hotkeys.Register(modifiers, vk, () => FireFromHotkey(captured));
                _log.Info($"Hotkey registered '{preset.Hotkey}' for preset {preset.Name}.");
            }
            catch (Exception ex)
            {
                // One bad hotkey string never breaks the app startup.
                _log.Warn($"Skipping hotkey '{preset.Hotkey}' for preset {preset.Name}: {ex.Message}");
            }
        }
        _hotkeysRegistered = true;
    }

    private void OnSinkMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == GlobalHotKeyManager.WM_HOTKEY)
            _hotkeys?.Dispatch(wParam.ToInt32());
    }

    /// <summary>Observed fire-and-forget: hotkey failures are logged,
    /// never lost to an unobserved task.</summary>
    private async void FireFromHotkey(Preset preset)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        lock (_hotkeyLastFire)
        {
            if (_hotkeyLastFire.TryGetValue(preset.Id, out DateTimeOffset last)
                && (now - last).TotalMilliseconds < HotkeyDebounceMs)
            {
                _log.Info($"Hotkey fire {preset.Name} ignored (repeat debounced).");
                return;
            }
            _hotkeyLastFire[preset.Id] = now;
        }
        try
        {
            Core.Execution.PresetExecutionResult result = await _dispatcher.FireAsync(preset);
            var byId = _state.Data.Servers
                .SelectMany(s => s.Accounts)
                .ToDictionary(a => a.Id);
            PresetFireLog.Log(_log, _resolver, preset, result, "hotkey", id =>
                byId.TryGetValue(id, out Account? a)
                    ? ((string.IsNullOrWhiteSpace(a.Role) ? a.Login : a.Role), a.ProcessId)
                    : ("?", null), _state.VerboseFireLog);
        }
        catch (Exception ex)
        {
            _log.Error($"Hotkey fire {preset.Name} failed: {ex.Message}");
        }
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

    protected override void OnClosing(WindowClosingEventArgs e)
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
        _sink?.Dispose();
        _hotkeys?.Dispose();
        _tray.Dispose();
        base.OnClosed(e);
    }
}
