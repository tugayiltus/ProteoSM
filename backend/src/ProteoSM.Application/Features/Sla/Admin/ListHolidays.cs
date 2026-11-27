using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Application.Features.Sla.Admin;

public sealed class ListHolidaysHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<HolidayItem>> HandleAsync(CancellationToken cancellationToken) =>
        (await db.Holidays.AsNoTracking().OrderBy(h => h.Date).ToListAsync(cancellationToken))
        .Select(HolidayItem.From)
        .ToList();
}
