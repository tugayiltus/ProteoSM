using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Sla.Admin;

/// <summary>The current policy version with its targets and pause rules.</summary>
public sealed class GetPolicyHandler(IAppDbContext db)
{
    public async Task<PolicyDetail> HandleAsync(CancellationToken cancellationToken)
    {
        var version = await db.SlaPolicyVersions.AsNoTracking()
            .Include(v => v.Targets)
            .Include(v => v.PauseRules)
            .SingleOrDefaultAsync(v => v.IsCurrent, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.SlaPolicy);
        return PolicyDetail.From(version);
    }
}
