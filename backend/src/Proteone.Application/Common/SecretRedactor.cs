using System.Text;

namespace Proteone.Application.Common;

/// <summary>
/// Replaces a known credential with <c>[redacted]</c> in text that leaves the process (test detail, audit, <c>last_error</c>).
/// Some SMTP servers echo the AUTH line, so the base64 forms of AUTH LOGIN (password alone) and AUTH PLAIN
/// (<c>authzid NUL user NUL password</c>, MailKit sends an empty authzid) are replaced too, not only the plain value.
/// Use it after <see cref="ErrorSanitizer"/> at full length and cut the text only after it, so no part of a secret stays at the cut.
/// </summary>
public static class SecretRedactor
{
    public const string RedactedValue = "[redacted]";

    private const char Separator = '\0';

    public static string Redact(string text, string? userName, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return text;
        }

        var user = userName ?? string.Empty;
        string[] secrets =
        [
            // Longest first, so a shorter form never cuts a longer one in two.
            Base64($"{user}{Separator}{user}{Separator}{password}"),
            Base64($"{Separator}{user}{Separator}{password}"),
            Base64(password),
            password,
        ];

        foreach (var secret in secrets)
        {
            text = text.Replace(secret, RedactedValue, StringComparison.Ordinal);
        }

        return text;
    }

    private static string Base64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
