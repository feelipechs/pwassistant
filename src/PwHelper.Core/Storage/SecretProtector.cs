using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace PwHelper.Core.Storage;

/// <summary>
/// Protects account passwords at rest. Production implementation is DPAPI
/// (CurrentUser scope — Windows derives and guards the key; the AES Details
/// are owned by the OS). Never logs, throws with, or returns plaintext.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Reveal(string protectedPayload);
}

/// <summary>
/// DPAPI-backed protector. Windows-only at runtime; construction is cheap
/// and platform-agnostic so object graphs build anywhere.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Password must not be empty.", nameof(plaintext));

        byte[] cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    public string Reveal(string protectedPayload)
    {
        if (string.IsNullOrWhiteSpace(protectedPayload))
            throw new ArgumentException("Protected payload must not be empty.", nameof(protectedPayload));

        byte[] plain = ProtectedData.Unprotect(
            Convert.FromBase64String(protectedPayload), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plain);
    }
}
