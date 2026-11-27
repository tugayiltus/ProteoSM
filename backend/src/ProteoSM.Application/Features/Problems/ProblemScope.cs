using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Users;

namespace ProteoSM.Application.Features.Problems;

/// <summary>The signed-in user as problem access sees them (ADR-0034).</summary>
public sealed record ProblemViewer(Guid UserId, UserRole Role, IReadOnlyList<Guid> TeamIds)
{
    public bool IsStaff => Role != UserRole.Employee;

    /// <summary>May work on the problem: admin always, IT staff who are members of the owner team.</summary>
    public bool CanWork(Problem problem) => Role == UserRole.Admin || (Role == UserRole.ItStaff && TeamIds.Contains(problem.OwnerTeamId));
}

/// <summary>
/// Problem access: every active IT staff member and admin reads every problem; employees get "not found" on every
/// problem route. Working on a problem needs the owner team or admin, others get "forbidden" (ADR-0034).
/// </summary>
public sealed class ProblemScope(IAppDbContext db, ICurrentActor actor)
{
    public async Task<ProblemViewer> ViewerAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
        var teams = await db.TeamMemberships.Where(m => m.UserId == userId).Select(m => m.TeamId).ToListAsync(cancellationToken);
        return new ProblemViewer(userId, role, teams);
    }

    /// <summary>Viewer for creating or listing problems; employees get not found.</summary>
    public async Task<ProblemViewer> StaffViewerAsync(CancellationToken cancellationToken)
    {
        var viewer = await ViewerAsync(cancellationToken);
        return viewer.IsStaff ? viewer : throw new NotFoundException(AuditEntityTypes.Problem);
    }

    /// <summary>Every problem for staff and admins; nothing for employees.</summary>
    public IQueryable<Problem> Visible(ProblemViewer viewer) => viewer.IsStaff ? db.Problems : db.Problems.Where(_ => false);

    /// <summary>Loads a tracked problem by its number, or throws not found (also for employees).</summary>
    public async Task<(Problem Problem, ProblemViewer Viewer)> LoadAsync(string number, CancellationToken cancellationToken)
    {
        var viewer = await StaffViewerAsync(cancellationToken);
        var normalized = ProblemNumber.Normalize(number);
        var problem = await db.Problems.SingleOrDefaultAsync(p => p.Number == normalized, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Problem);
        return (problem, viewer);
    }

    /// <summary>Like <see cref="LoadAsync"/>, but viewers outside the owner team (and non-admins) get forbidden.</summary>
    public async Task<(Problem Problem, ProblemViewer Viewer)> LoadForWorkAsync(string number, CancellationToken cancellationToken)
    {
        var (problem, viewer) = await LoadAsync(number, cancellationToken);
        return viewer.CanWork(problem) ? (problem, viewer) : throw new ForbiddenException();
    }
}
