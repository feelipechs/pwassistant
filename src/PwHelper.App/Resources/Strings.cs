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
    public static string Members => Get(nameof(Members));
    public static string OnlineMembers => Get(nameof(OnlineMembers));
    public static string AddMember => Get(nameof(AddMember));
    public static string Remove => Get(nameof(Remove));
    public static string Formations => Get(nameof(Formations));
    public static string FormationName => Get(nameof(FormationName));
    public static string SaveFormation => Get(nameof(SaveFormation));
    public static string LoadFormation => Get(nameof(LoadFormation));
    public static string NewPreset => Get(nameof(NewPreset));
    public static string PresetNameNumber => Get(nameof(PresetNameNumber));
    public static string Add => Get(nameof(Add));
    public static string KeyRequired => Get(nameof(KeyRequired));
    public static string ClickPositionRequired => Get(nameof(ClickPositionRequired));
    public static string NoPositionCaptured => Get(nameof(NoPositionCaptured));
    public static string PositionCaptured => Get(nameof(PositionCaptured));
    public static string DelayMs => Get(nameof(DelayMs));
    public static string InvalidDelay => Get(nameof(InvalidDelay));
    public static string Edit => Get(nameof(Edit));
    public static string Delete => Get(nameof(Delete));
    public static string AddCommand => Get(nameof(AddCommand));
    public static string AddClick => Get(nameof(AddClick));
    public static string Duplicate => Get(nameof(Duplicate));
    public static string MoveUp => Get(nameof(MoveUp));
    public static string MoveDown => Get(nameof(MoveDown));
    public static string DuplicatePreset => Get(nameof(DuplicatePreset));
    public static string CopySuffix => Get(nameof(CopySuffix));
    public static string RowWithoutAccount(int line) =>
        string.Format(Get(nameof(RowWithoutAccount)), line);
}
