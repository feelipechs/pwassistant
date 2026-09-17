using System.Text.Json.Serialization;

namespace PwHelper.Core.Models;

public sealed class Server
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Full path to this server's elementclient.exe.</summary>
    public string ElementClientPath { get; set; } = string.Empty;

    /// <summary>Optional server-select bypass passed to the client.</summary>
    public string? ForceServer { get; set; }

    public List<Account> Accounts { get; set; } = new();
}

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServerId { get; set; }
    public string Login { get; set; } = string.Empty;

    /// <summary>DPAPI-protected password, Base64 encoded. Never plaintext.</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>Character nickname used as the client role argument.</summary>
    public string Role { get; set; } = string.Empty;

    public string? Tag { get; set; }
    public string? Color { get; set; }
    public bool IsFavorite { get; set; }
    public long TotalPlayedTimeMs { get; set; }

    // ---- runtime fields: recalculated every launch, never persisted ----
    [JsonIgnore] public int? ProcessId { get; set; }
    [JsonIgnore] public IntPtr WindowHandle { get; set; } = IntPtr.Zero;
    [JsonIgnore] public AccountStatus Status => WindowHandle != IntPtr.Zero ? AccountStatus.Online : AccountStatus.Offline;
}

public sealed class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Possible members; offline members are admitted but skipped at dispatch.</summary>
    public List<Guid> AccountIds { get; set; } = new();
}

/// <summary>
/// Named, ordered snapshot of party members. Order is significant
/// (numpad selection in B4 follows it). No presets (they persist per group).
/// </summary>
public sealed class Formation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Ordered member account ids.</summary>
    public List<Guid> AccountIds { get; set; } = new();
}

public sealed class Preset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Hotkey { get; set; }
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Sequential;
    public List<AccountAction> Actions { get; set; } = new();
}

public sealed class AccountAction
{
    public Guid AccountId { get; set; }
    public GameAction Action { get; set; } = new();
}
