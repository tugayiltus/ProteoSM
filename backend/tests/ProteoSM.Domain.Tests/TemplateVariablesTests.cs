using System.Reflection;
using ProteoSM.Domain.Notifications;

namespace ProteoSM.Domain.Tests;

public sealed class TemplateVariablesTests
{
    [Fact]
    public void Resolution_and_completed_at_are_allowed_only_for_ticket_completed()
    {
        var allowedForCompleted = TemplateVariables.AllowedFor(NotificationEventTypes.TicketCompleted);
        Assert.Contains(TemplateVariables.Resolution, allowedForCompleted);
        Assert.Contains(TemplateVariables.CompletedAt, allowedForCompleted);

        foreach (var eventType in AllEventTypes().Where(e => e != NotificationEventTypes.TicketCompleted))
        {
            var allowed = TemplateVariables.AllowedFor(eventType);
            Assert.DoesNotContain(TemplateVariables.Resolution, allowed);
            Assert.DoesNotContain(TemplateVariables.CompletedAt, allowed);
        }
    }

    [Fact]
    public void Action_url_and_expires_at_are_allowed_only_for_account_events()
    {
        Assert.Contains(TemplateVariables.ActionUrl, TemplateVariables.AllowedFor(NotificationEventTypes.AccountActivation));
        Assert.Contains(TemplateVariables.ActionUrl, TemplateVariables.AllowedFor(NotificationEventTypes.PasswordReset));

        var accountEvents = new HashSet<string>(StringComparer.Ordinal) { NotificationEventTypes.AccountActivation, NotificationEventTypes.PasswordReset };
        foreach (var eventType in AllEventTypes().Where(e => !accountEvents.Contains(e)))
        {
            var allowed = TemplateVariables.AllowedFor(eventType);
            Assert.DoesNotContain(TemplateVariables.ActionUrl, allowed);
            Assert.DoesNotContain(TemplateVariables.ExpiresAt, allowed);
        }
    }

    [Fact]
    public void Sla_metric_and_due_at_are_allowed_only_for_sla_events()
    {
        Assert.Contains(TemplateVariables.SlaMetric, TemplateVariables.AllowedFor(NotificationEventTypes.SlaWarning));
        Assert.Contains(TemplateVariables.DueAt, TemplateVariables.AllowedFor(NotificationEventTypes.SlaBreached));

        var slaEvents = new HashSet<string>(StringComparer.Ordinal) { NotificationEventTypes.SlaWarning, NotificationEventTypes.SlaBreached };
        foreach (var eventType in AllEventTypes().Where(e => !slaEvents.Contains(e)))
        {
            var allowed = TemplateVariables.AllowedFor(eventType);
            Assert.DoesNotContain(TemplateVariables.SlaMetric, allowed);
            // A problem timer has one target, so its events carry the due time but no metric.
            if (!ProblemSlaEvents.Contains(eventType))
            {
                Assert.DoesNotContain(TemplateVariables.DueAt, allowed);
            }
        }
    }

    private static readonly HashSet<string> ProblemSlaEvents = new(StringComparer.Ordinal)
    {
        NotificationEventTypes.ProblemSlaWarning, NotificationEventTypes.ProblemSlaBreached,
    };

    private static readonly string[] ProblemEvents =
    [
        NotificationEventTypes.ProblemCreated, NotificationEventTypes.ProblemAssigned,
        NotificationEventTypes.ProblemKnownError, NotificationEventTypes.ProblemResolved,
    ];

    private static readonly string[] TicketCauseEvents = [NotificationEventTypes.TicketCauseFound, NotificationEventTypes.TicketCauseResolved];

    [Fact]
    public void Problem_events_allow_the_problem_set_and_due_at_only_for_the_sla_events()
    {
        foreach (var eventType in ProblemEvents)
        {
            var allowed = TemplateVariables.AllowedFor(eventType);
            Assert.Contains(TemplateVariables.ProblemNumber, allowed);
            Assert.Contains(TemplateVariables.ProblemUrl, allowed);
            Assert.Contains(TemplateVariables.Impact, allowed);
            Assert.DoesNotContain(TemplateVariables.DueAt, allowed);
        }

        Assert.Contains(TemplateVariables.DueAt, TemplateVariables.AllowedFor(NotificationEventTypes.ProblemSlaWarning));
    }

    [Fact]
    public void Ticket_cause_events_allow_nothing_from_the_problem()
    {
        foreach (var eventType in TicketCauseEvents)
        {
            var allowed = TemplateVariables.AllowedFor(eventType);
            Assert.Contains(TemplateVariables.TicketNumber, allowed);
            Assert.DoesNotContain(TemplateVariables.ProblemNumber, allowed);
            Assert.DoesNotContain(TemplateVariables.ProblemUrl, allowed);
            Assert.DoesNotContain(TemplateVariables.Impact, allowed);
            Assert.DoesNotContain(TemplateVariables.TeamName, allowed);
        }
    }

    [Fact]
    public void Every_notification_event_type_has_a_non_empty_allowed_set()
    {
        foreach (var eventType in AllEventTypes())
        {
            Assert.NotEmpty(TemplateVariables.AllowedFor(eventType));
        }
    }

    [Fact]
    public void All_variables_is_the_union_of_every_event_s_allowed_set()
    {
        var union = AllEventTypes().SelectMany(e => TemplateVariables.AllowedFor(e)).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(TemplateVariables.AllVariables, union);
    }

    [Fact]
    public void Internal_event_types_are_not_emailable_events()
    {
        Assert.DoesNotContain(InternalEventTypes.PasswordResetRequested, AllEventTypes());
        Assert.Throws<ArgumentOutOfRangeException>(() => TemplateVariables.AllowedFor(InternalEventTypes.PasswordResetRequested));
    }

    private static IEnumerable<string> AllEventTypes() =>
        typeof(NotificationEventTypes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);
}
