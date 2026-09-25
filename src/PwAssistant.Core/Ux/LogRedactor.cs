using System.Text.RegularExpressions;

namespace PwAssistant.Core.Ux;

/// <summary>
/// Defense in depth: secrets never reach disk or log output.
/// Masks the startbypatcher credential tokens (pwd:/user:/role:) —
/// bare logins elsewhere stay readable for diagnostics by design.
/// </summary>
public static partial class LogRedactor
{
    [GeneratedRegex(@"pwd:\S+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordArgument();

    [GeneratedRegex(@"user:\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UserArgument();

    [GeneratedRegex(@"\brole:\S+", RegexOptions.IgnoreCase)]
    private static partial Regex RoleArgument();

    public static string Redact(string? message)
    {
        if (message is null)
            return string.Empty;
        string redacted = PasswordArgument().Replace(message, "pwd:***");
        redacted = UserArgument().Replace(redacted, "user:***");
        return RoleArgument().Replace(redacted, "role:***");
    }
}
