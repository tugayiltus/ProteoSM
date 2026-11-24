using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Domain.Sla;

namespace Proteone.Application.Sla;

/// <summary>Builds the Domain <see cref="BusinessCalendarRules"/> from the company calendar (zone, weekly hours, holidays) for <see cref="BusinessTimeCalculator"/>.</summary>
public sealed class CalendarProvider(IAppDbContext db)
{
    public async Task<BusinessCalendarRules> GetRulesAsync(CancellationToken cancellationToken)
    {
        var calendar = await db.BusinessCalendars.AsNoTracking().SingleAsync(cancellationToken);
        var hours = await db.BusinessHours.AsNoTracking().ToListAsync(cancellationToken);
        var holidays = await db.Holidays.AsNoTracking().Select(h => h.Date).ToListAsync(cancellationToken);

        return new BusinessCalendarRules(
            TimeZoneInfo.FindSystemTimeZoneById(calendar.TimeZoneId),
            hours.ToDictionary(h => h.Weekday, h => new WorkingRange(h.StartsAt, h.EndsAt)),
            holidays.ToHashSet());
    }
}
