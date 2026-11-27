using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Proteone.Application.Abstractions;
using Proteone.Infrastructure.Email;
using Proteone.Infrastructure.Files;
using Proteone.Infrastructure.Persistence;
using Proteone.Infrastructure.Reports;
using Proteone.Infrastructure.Security;

namespace Proteone.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // The connection string is read when the first DbContext is created, so hosts that never
        // touch the database (OpenAPI generation) start without one.
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured. Run tools/db-up.sh.");
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
        services.AddScoped<IAssetTagGenerator, AssetTagGenerator>();
        services.AddScoped<IOutboxClaimer, OutboxClaimer>();
        services.AddScoped<IEmailDeliveryClaimer, EmailDeliveryClaimer>();
        services.AddScoped<IExportJobClaimer, ExportJobClaimer>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);
        services.AddSingleton<LocalFileStore>();
        services.AddSingleton<IFileStore>(provider => provider.GetRequiredService<LocalFileStore>());
        services.AddSingleton<IFileScanner, DevelopmentFileScanner>();
        services.AddScoped<IReportFileWriter, ExcelReportWriter>();

        // Account-link tokens (ADR-0015, ADR-0017); the key ring is shared by the API and the Worker (ADR-0024).
        services.AddOptions<KeyStorageOptions>().BindConfiguration(KeyStorageOptions.SectionName);
        services.AddProteoneDataProtection();
        services.AddSingleton<IAccountLinkProtector, AccountLinkProtector>();
        services.AddSingleton<IAuthSecretProtector, AuthSecretProtector>();
        services.AddSingleton<IMonitoringSecretProtector, MonitoringSecretProtector>();
        services.AddSingleton<ILicenceKeyProtector, LicenceKeyProtector>();
        services.AddSingleton<IEmailSecretProtector, EmailSecretProtector>();
        services.AddSingleton<ILdapDirectory, Identity.NovellLdapDirectory>();
        // One client for the occasional connection test; the Worker composes this too, so it lives here, not in Proteone.Api.
        services.AddSingleton<IEntraConnectionTester>(_ => new Identity.EntraConnectionTester(new HttpClient()));
        services.AddSingleton<ILoginProviderCache, Identity.NoLoginProviderCache>();
        services.AddScoped<ILdapSignInThrottle, LdapSignInThrottle>();

        // Email:Mode picks the transport (spec §3.5); resolving IEmailSender with a missing/unrecognized Mode
        // throws immediately ("fail fast when missing"). Proteone.Worker additionally marks EmailOptions
        // ValidateOnStart so a bad Mode fails the host at startup, not at the first send.
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName);
        services.AddSingleton<IEmailEnvironment, EmailEnvironment>();
        services.AddScoped<IEmailTestSender, MailKitEmailTestSender>();
        // One cache of the active panel settings for the whole process; only Email:Mode=Smtp ever resolves it.
        services.AddSingleton<ActiveSmtpEndpointProvider>();
        services.AddScoped(CreateEmailSender);
        return services;
    }

    /// <summary>The <see cref="IEmailSender"/> for <c>Email:Mode</c>; Pickup ignores the panel settings and every SMTP value.</summary>
    internal static IEmailSender CreateEmailSender(IServiceProvider provider)
    {
        var mode = provider.GetRequiredService<IOptionsMonitor<EmailOptions>>().CurrentValue.Mode;
        return mode switch
        {
            EmailModes.Smtp => ActivatorUtilities.CreateInstance<SmtpEmailSender>(provider),
            EmailModes.Pickup => ActivatorUtilities.CreateInstance<PickupFolderEmailSender>(provider),
            _ => throw new InvalidOperationException(
                $"Email:Mode '{mode}' is not supported; expected '{EmailModes.Smtp}' or '{EmailModes.Pickup}'."),
        };
    }
}
