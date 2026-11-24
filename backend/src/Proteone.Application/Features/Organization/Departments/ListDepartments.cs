using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common.Paging;
using Proteone.Domain.Common;

namespace Proteone.Application.Features.Organization.Departments;

public sealed record ListDepartmentsQuery(string? Search, bool? IsActive, int Page, int PageSize);

public sealed class ListDepartmentsHandler(IAppDbContext db)
{
    public Task<PagedResult<DepartmentItem>> HandleAsync(ListDepartmentsQuery query, CancellationToken cancellationToken)
    {
        var departments = db.Departments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var key = TurkishText.ToSearchKey(query.Search);
            departments = departments.Where(d => d.NormalizedName.Contains(key));
        }

        if (query.IsActive is { } isActive)
        {
            departments = departments.Where(d => d.IsActive == isActive);
        }

        return departments.ToItems(db).ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
