using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;

namespace ProteoSM.Application.Features.Sla.Admin;

public sealed class GetCalendarHandler(IAppDbContext db)
{
    public async Task<CalendarDetail> HandleAsync(CancellationToken cancellationToken) =>
        await CalendarQueries.LoadAsync(db, cancellationToken);
}

internal static class CalendarQueries
{
    /// <summary>The single company calendar with its weekly hours, read without tracking so a later save starts from fresh state.</summary>
    public static async Task<CalendarDetail> LoadAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.SlaCalendar);
        var hours = await db.BusinessHours.AsNoTracking().ToListAsync(cancellationToken);
        return CalendarDetail.From(calendar, hours);
    }
}
