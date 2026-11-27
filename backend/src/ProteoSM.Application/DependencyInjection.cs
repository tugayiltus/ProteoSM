using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Audit;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Organization;
using ProteoSM.Application.Features.Users;
using ProteoSM.Application.Notifications;
using ProteoSM.Application.Security;
using ProteoSM.Application.Sla;

namespace ProteoSM.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        var handlerTypes = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true }
                && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal));
        foreach (var handlerType in handlerTypes)
        {
            services.AddScoped(handlerType);
        }

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<INotificationOutbox, NotificationOutbox>();
        services.AddScoped<RecipientResolver>();
        services.AddScoped<NotificationContextLoader>();
        services.AddScoped<ICompanyClock, CompanyClock>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddScoped<PasswordTokenIssuer>();
        services.AddScoped<Features.Auth.Settings.LoginProviderTester>();
        services.AddScoped<Features.Email.Settings.EmailSettingsTester>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<OrganizationReferences>();
        services.AddScoped<LastAdminGuard>();
        services.AddScoped<CalendarProvider>();
        services.AddScoped<PolicyProvider>();
        services.AddScoped<SlaTracker>();
        services.AddScoped<SlaCalendarReplanner>();
        services.AddScoped<TicketSlaReader>();
        services.AddScoped<Features.Tickets.TicketScope>();
        services.AddScoped<Reports.ReportScope>();
        services.AddScoped<Reports.TicketReportSource>();
        services.AddScoped<Reports.ReportRowSource>();
        services.AddScoped<Features.Tickets.TicketFactory>();
        services.AddScoped<Features.Tickets.TicketWork>();
        services.AddScoped<Features.Tasks.TaskScope>();
        services.AddScoped<Features.Tasks.TaskWork>();
        services.AddScoped<Features.Problems.ProblemScope>();
        services.AddScoped<Features.Problems.ProblemWork>();
        services.AddScoped<Features.Problems.ProblemSlaTracker>();
        services.AddScoped<Features.Problems.ProblemSlaReader>();
        services.AddScoped<Features.Changes.ChangeScope>();
        services.AddScoped<Features.Changes.ChangeConflicts>();
        services.AddScoped<Features.Changes.ChangeWork>();
        services.AddScoped<Features.Releases.ReleaseWork>();
        services.AddScoped<Features.Changes.ChangeApprovalFlow>();
        services.AddScoped<Features.Tasks.ChangeTaskRules>();
        return services;
    }
}
