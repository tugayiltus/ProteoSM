namespace Proteone.Application.Abstractions;

/// <summary>Protects licence keys at rest (ADR-0039); a purpose of its own, so no other stored secret opens with it.</summary>
public interface ILicenceKeyProtector
{
    string Protect(string key);

    string Unprotect(string protectedKey);
}
