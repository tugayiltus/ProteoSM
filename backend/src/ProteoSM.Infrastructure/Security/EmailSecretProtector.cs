using Microsoft.AspNetCore.DataProtection;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Infrastructure.Security;

/// <summary><see cref="IEmailSecretProtector"/> over ASP.NET Core Data Protection with its own purpose and the shared key ring (ADR-0024).</summary>
internal sealed class EmailSecretProtector(IDataProtectionProvider provider) : IEmailSecretProtector
{
    public const string Purpose = "Proteone.EmailSettings";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string secret) => _protector.Protect(secret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
