namespace ProteoSM.Application.Abstractions;

/// <summary>Protects licence keys at rest (ADR-0039); a purpose of its own, so no other stored secret opens with it.</summary>
public interface ILicenceKeyProtector
{
    string Protect(string key);

    /// <summary>Null when the stored value cannot be opened, e.g. after the key ring was lost.</summary>
    string? TryUnprotect(string protectedKey);
}
