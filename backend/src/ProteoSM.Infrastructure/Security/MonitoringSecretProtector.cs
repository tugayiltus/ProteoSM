using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Infrastructure.Security;

internal sealed class MonitoringSecretProtector(IDataProtectionProvider provider) : IMonitoringSecretProtector
{
    public const string Purpose = "Proteone.MonitoringSecret";

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(string secret) => _protector.Protect(secret);

    public string? TryUnprotect(string protectedSecret)
    {
        try
        {
            return _protector.Unprotect(protectedSecret);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
