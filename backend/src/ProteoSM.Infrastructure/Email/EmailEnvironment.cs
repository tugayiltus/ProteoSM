using Microsoft.Extensions.Options;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Infrastructure.Email;

/// <summary><see cref="IEmailEnvironment"/> over <c>Email:*</c>; read on each call, so a configuration reload shows at once.</summary>
internal sealed class EmailEnvironment(IOptionsMonitor<EmailOptions> options) : IEmailEnvironment
{
    public EmailEnvironmentInfo Current
    {
        get
        {
            var settings = options.CurrentValue;
            return new EmailEnvironmentInfo(
                string.Equals(settings.Mode, EmailModes.Pickup, StringComparison.Ordinal),
                settings.From,
                settings.Smtp.Host,
                settings.Smtp.Port);
        }
    }
}
