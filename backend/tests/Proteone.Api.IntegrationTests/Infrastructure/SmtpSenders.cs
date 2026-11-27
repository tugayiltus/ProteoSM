using Microsoft.Extensions.DependencyInjection;
using Proteone.Application.Abstractions;
using Proteone.Infrastructure.Email;

namespace Proteone.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The Worker's SMTP sender built by hand against a test host's database, with fixed environment settings. Each call
/// gets a new <see cref="ActiveSmtpEndpointProvider"/>, so no test sees another test's cache.
/// </summary>
internal static class SmtpSenders
{
    public static ActiveSmtpEndpointProvider Endpoints(ApiFactory factory, EmailOptions environment, TimeProvider? clock = null, IEmailSecretProtector? secrets = null) =>
        new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedOptionsMonitor<EmailOptions>(environment),
            secrets ?? factory.Services.GetRequiredService<IEmailSecretProtector>(),
            clock ?? TimeProvider.System);

    public static SmtpEmailSender Sender(ApiFactory factory, EmailOptions environment, TimeProvider? clock = null) =>
        new(new FixedOptionsMonitor<EmailOptions>(environment), Endpoints(factory, environment, clock));
}
