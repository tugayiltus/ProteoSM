namespace ProteoSM.Application.Abstractions;

/// <summary>Protects webhook secrets at rest (ADR-0041); a purpose of its own, so no other stored secret opens with it.</summary>
public interface IMonitoringSecretProtector
{
    string Protect(string secret);

    /// <summary>Null when the stored value cannot be opened, e.g. after the key ring was lost.</summary>
    string? TryUnprotect(string protectedSecret);
}
