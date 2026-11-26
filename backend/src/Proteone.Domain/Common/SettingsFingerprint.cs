using System.Security.Cryptography;
using System.Text;

namespace Proteone.Domain.Common;

/// <summary>
/// The fingerprint that a settings test stores. Any change to one of the parts gives a different value, so changed
/// settings need a new test before they can be used.
/// </summary>
public static class SettingsFingerprint
{
    private const char Separator = '\n';

    /// <summary>SHA-256 as upper-case hex over the parts joined by a new line.</summary>
    public static string Compute(params string[] parts) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(Separator, parts))));
}
