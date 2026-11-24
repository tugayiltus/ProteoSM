using Microsoft.AspNetCore.DataProtection;
using Proteone.Application.Abstractions;

namespace Proteone.Infrastructure.Security;

internal sealed class LicenceKeyProtector(IDataProtectionProvider provider) : ILicenceKeyProtector
{
    public const string Purpose = "Proteone.LicenceKey";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string key) => _protector.Protect(key);

    public string Unprotect(string protectedKey) => _protector.Unprotect(protectedKey);
}
