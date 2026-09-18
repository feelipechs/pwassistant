using System.Text.RegularExpressions;

namespace PwAssistant.Core.Ux;

/// <summary>Defense in depth: secrets never reach disk or log output.</summary>
public static partial class LogRedactor
{
    [GeneratedRegex(@"pwd:\S+")]
    private static partial Regex PasswordArgument();

    public static string Redact(string? message) =>
        message is null ? string.Empty : PasswordArgument().Replace(message, "pwd:***");
}
