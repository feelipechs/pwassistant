using System.Resources;

namespace PwAssistant.App.Resources;

/// <summary>Typed accessor over Strings.resx (neutral) / Strings.pt-BR.resx.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("PwAssistant.App.Resources.Strings", typeof(Strings).Assembly);

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
    public static string Stop => Get(nameof(Stop));
    public static string Save => Get(nameof(Save));
    public static string Login => Get(nameof(Login));
    public static string Password => Get(nameof(Password));
    public static string Role => Get(nameof(Role));
    public static string ServerName => Get(nameof(ServerName));
    public static string ClientPath => Get(nameof(ClientPath));
    public static string GroupName => Get(nameof(GroupName));
    public static string PresetName => Get(nameof(PresetName));
    public static string SyncEnabled => Get(nameof(SyncEnabled));
    public static string FocusSwitch => Get(nameof(FocusSwitch));
    public static string MiniMode => Get(nameof(MiniMode));
    public static string FocusKeysHint => Get(nameof(FocusKeysHint));
    public static string FocusConfig => Get(nameof(FocusConfig));
    public static string CycleKey => Get(nameof(CycleKey));
    public static string NumpadSelect => Get(nameof(NumpadSelect));
    public static string ShiftTapToggle => Get(nameof(ShiftTapToggle));
    public static string Settings => Get(nameof(Settings));
    public static string Close => Get(nameof(Close));
    public static string ArmingHint => Get(nameof(ArmingHint));
    public static string GameSkillWarning => Get(nameof(GameSkillWarning));
    public static string Loop => Get(nameof(Loop));
    public static string CaptureClick => Get(nameof(CaptureClick));
    public static string ClickOverlayHint => Get(nameof(ClickOverlayHint));
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
    public static string Copy => Get(nameof(Copy));
    public static string Nickname => Get(nameof(Nickname));
    public static string SameAsRole => Get(nameof(SameAsRole));
    public static string Class => Get(nameof(Class));
    public static string AddCommand => Get(nameof(AddCommand));
    public static string AddClick => Get(nameof(AddClick));
    public static string Duplicate => Get(nameof(Duplicate));
    public static string MoveUp => Get(nameof(MoveUp));
    public static string MoveDown => Get(nameof(MoveDown));
    public static string DuplicatePreset => Get(nameof(DuplicatePreset));
    public static string CopySuffix => Get(nameof(CopySuffix));
    public static string Hotkey => Get(nameof(Hotkey));
    public static string HotkeyHint => Get(nameof(HotkeyHint));
    public static string RecordHotkey => Get(nameof(RecordHotkey));
    public static string PressKeys => Get(nameof(PressKeys));
    public static string NoHotkey => Get(nameof(NoHotkey));
    public static string PresetNameRequired => Get(nameof(PresetNameRequired));
    public static string InvalidHotkey => Get(nameof(InvalidHotkey));
    public static string ExecutionMode => Get(nameof(ExecutionMode));
    public static string ShowPassword => Get(nameof(ShowPassword));
    public static string HidePassword => Get(nameof(HidePassword));
    public static string All => Get(nameof(All));
    public static string NewTab => Get(nameof(NewTab));
    public static string Rename => Get(nameof(Rename));
    public static string RemoveFromTab => Get(nameof(RemoveFromTab));
    public static string TabName => Get(nameof(TabName));
    public static string NewGroup => Get(nameof(NewGroup));
    public static string DeleteGroupTitle => Get(nameof(DeleteGroupTitle));
    public static string ServerStats(int total, int online) =>
        string.Format(Get(nameof(ServerStats)), total, online);
    public static string DeleteServerTitle => Get(nameof(DeleteServerTitle));
    public static string DeleteServerConfirm(string server, int accounts) =>
        string.Format(Get(nameof(DeleteServerConfirm)), server, accounts);
    public static string DeleteGroupConfirm(string group) =>
        string.Format(Get(nameof(DeleteGroupConfirm)), group);
    public static string CopyLogin => Get(nameof(CopyLogin));
    public static string CopyPassword => Get(nameof(CopyPassword));
    public static string Refresh => Get(nameof(Refresh));
    public static string Tag => Get(nameof(Tag));
    public static string CancelDialog => Get(nameof(CancelDialog));
    public static string FormationApplied(string name, int members) =>
        string.Format(Get(nameof(FormationApplied)), name, members);
    public static string FormationAppliedSkipped(string name, int members, int skipped) =>
        string.Format(Get(nameof(FormationAppliedSkipped)), name, members, skipped);
    public static string PresetFired(int fired, int skipped) =>
        string.Format(Get(nameof(PresetFired)), fired, skipped);
    public static string PresetCanceled => Get(nameof(PresetCanceled));
    public static string SkippedMark => Get(nameof(SkippedMark));
    public static string RowWithoutAccount(int line) =>
        string.Format(Get(nameof(RowWithoutAccount)), line);
}
