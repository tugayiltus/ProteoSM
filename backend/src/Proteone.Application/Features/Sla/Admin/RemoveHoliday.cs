using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Sla;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Sla.Admin;

public sealed record RemoveHolidayCommand(Guid Id);

/// <summary>Reopens a holiday date (ADR-0018). Running SLA timers are re-planned in the same save.</summary>
public sealed class RemoveHolidayHandler(IAppDbContext db, IAuditLog audit, SlaCalendarReplanner replanner)
{
    public async Task HandleAsync(RemoveHolidayCommand command, CancellationToken cancellationToken)
    {
        var holiday = await db.Holidays.SingleOrDefaultAsync(h => h.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Holiday);
        await replanner.RunAsync(
            (rules, _) =>
            {
                db.Holidays.Remove(holiday);
                audit.Record(AuditActions.SlaHolidayRemoved, AuditEntityTypes.Holiday, holiday.Id, oldValues: new { holiday.Date, holiday.Name });
                var holidays = new HashSet<DateOnly>(rules.Holidays);
                holidays.Remove(holiday.Date);
                return Task.FromResult(rules with { Holidays = holidays });
            },
            db.SaveChangesAsync,
            cancellationToken);
    }
}
