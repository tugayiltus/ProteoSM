using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Email;

namespace ProteoSM.Application.Features.Email.Settings;

/// <summary>
/// Outgoing mail settings as the admin page shows them. The password is write-only, so only <see cref="HasPassword"/>
/// leaves the server. The environment values tell the admin what is in use while the panel settings are not active.
/// </summary>
public sealed record EmailSettingsDto(
    EmailProvider Provider,
    string? SenderName,
    string SenderAddress,
    string Host,
    int Port,
    EmailSecurity Security,
    string? UserName,
    bool HasPassword,
    bool IsTested,
    bool IsActive,
    DateTimeOffset? LastTestedAt,
    bool? LastTestSucceeded,
    string? LastTestErrorCode,
    bool IsPickup,
    string EnvironmentFrom,
    string EnvironmentHost,
    uint Version);

public static class EmailSettingsMapping
{
    public static EmailSettingsDto ToDto(EmailSettings settings, EmailEnvironmentInfo environment) => new(
        settings.Provider,
        settings.SenderName,
        settings.SenderAddress,
        settings.Host,
        settings.Port,
        settings.Security,
        settings.UserName,
        settings.PasswordProtected is not null,
        settings.IsTested,
        settings.IsActive,
        settings.LastTestedAt,
        settings.LastTestSucceeded,
        settings.LastTestErrorCode,
        environment.IsPickup,
        environment.From,
        environment.Host,
        settings.Version);
}
