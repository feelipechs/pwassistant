using System.Resources;

namespace PwHelper.App.Resources;

/// <summary>Typed accessor over Strings.resx (neutral) / Strings.pt-BR.resx.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("PwHelper.App.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string key) => Manager.GetString(key) ?? key;

    public static string AppTitle => Get(nameof(AppTitle));
    public static string Servers => Get(nameof(Servers));
    public static string AddServer => Get(nameof(AddServer));
    public static string AddAccount => Get(nameof(AddAccount));
    public static string Play => Get(nameof(Play));
    public static string Online => Get(nameof(Online));
    public static string Offline => Get(nameof(Offline));
    public static string GroupMode => Get(nameof(GroupMode));
    public static string Presets => Get(nameof(Presets));
    public static string Fire => Get(nameof(Fire));
    public static string Cancel => Get(nameof(Cancel));
    public static string Save => Get(nameof(Save));
    public static string Login => Get(nameof(Login));
    public static string Password => Get(nameof(Password));
    public static string Role => Get(nameof(Role));
    public static string ServerName => Get(nameof(ServerName));
    public static string ClientPath => Get(nameof(ClientPath));
    public static string GroupName => Get(nameof(GroupName));
    public static string PresetName => Get(nameof(PresetName));
    public static string SyncEnabled => Get(nameof(SyncEnabled));
    public static string CaptureClick => Get(nameof(CaptureClick));
    public static string ClickOverlayHint => Get(nameof(ClickOverlayHint));
    public static string GroundClickLimitation => Get(nameof(GroundClickLimitation));
    public static string AccountsOfServer => Get(nameof(AccountsOfServer));
    public static string NoServerSelected => Get(nameof(NoServerSelected));
}
