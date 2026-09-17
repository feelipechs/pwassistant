using System.Text.Json;
using System.Text.Json.Serialization;
using PwHelper.Core.Models;

namespace PwHelper.Core.Storage;

/// <summary>Single-file persistence root. Versioned for future migrations.</summary>
public sealed class AppData
{
    public int Version { get; set; } = 1;
    public List<Server> Servers { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public List<Preset> Presets { get; set; } = new();
    public List<Formation> Formations { get; set; } = new();
}

public interface IAccountStore
{
    Task<AppData> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task SaveAsync(string path, AppData data, CancellationToken cancellationToken = default);
    void SetPassword(Account account, string plaintextPassword);
    string RevealPassword(Account account);
}

/// <summary>
/// JSON file store. Secrets stay DPAPI-protected; runtime fields
/// (ProcessId/WindowHandle) are [JsonIgnore] and never hit disk.
/// </summary>
public sealed class JsonFileAccountStore : IAccountStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ISecretProtector _protector;

    public JsonFileAccountStore(ISecretProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public void SetPassword(Account account, string plaintextPassword)
    {
        ArgumentNullException.ThrowIfNull(account);
        account.EncryptedPassword = _protector.Protect(plaintextPassword);
    }

    public string RevealPassword(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return _protector.Reveal(account.EncryptedPassword);
    }

    public async Task<AppData> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(path);
        AppData? data = await JsonSerializer.DeserializeAsync<AppData>(
            stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return data ?? new AppData();
    }

    public async Task SaveAsync(string path, AppData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string tempPath = path + ".tmp";
        await using (FileStream stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream, data, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        File.Move(tempPath, path, overwrite: true);
    }
}
