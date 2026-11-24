using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Common.Paging;
using Proteone.Domain.Common;
using Proteone.Domain.Problems;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Problems;

public enum ProblemQueue
{
    Mine,
    Team,
    /// <summary>Open problems of my teams (not resolved or cancelled): the İş listem card.</summary>
    TeamOpen,
    Open,
    KnownErrors,
}

public sealed record ListProblemsQuery(ProblemQueue? Queue, ProblemStatus? Status, Level? Impact, string? Search, int Page, int PageSize, Guid? ServiceId);

/// <summary>Lists problems for IT staff and admins; queues and filters only narrow the list. Employees get not found.</summary>
public sealed class ListProblemsHandler(IAppDbContext db, ProblemScope scope)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<ProblemListItem>> HandleAsync(ListProblemsQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        var problems = scope.Visible(viewer).AsNoTracking();
        var teamIds = viewer.TeamIds;
        problems = query.Queue switch
        {
            ProblemQueue.Mine => problems.Where(p => p.OwnerId == viewer.UserId),
            ProblemQueue.Team => problems.Where(p => teamIds.Contains(p.OwnerTeamId)),
            ProblemQueue.TeamOpen => problems.Where(p => teamIds.Contains(p.OwnerTeamId) && p.Status != ProblemStatus.Resolved && p.Status != ProblemStatus.Cancelled),
            ProblemQueue.Open => problems.Where(p => p.Status != ProblemStatus.Resolved && p.Status != ProblemStatus.Cancelled),
            ProblemQueue.KnownErrors => problems.Where(p => p.Status == ProblemStatus.KnownError),
            _ => problems,
        };
        if (query.Status is { } status)
        {
            problems = problems.Where(p => p.Status == status);
        }

        if (query.Impact is { } impact)
        {
            problems = problems.Where(p => p.Impact == impact);
        }

        if (query.ServiceId is { } serviceId)
        {
            problems = problems.Where(p => p.ServiceId == serviceId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var number = ProblemNumber.Normalize(query.Search);
            var key = TurkishText.ToSearchKey(query.Search);
            problems = ProblemNumber.IsValid(number)
                ? problems.Where(p => p.Number == number)
                : problems.Where(p => EF.Functions.Like(p.SearchText, LikePattern.Containing(key), LikePattern.EscapeText));
        }

        return await problems
            .OrderByDescending(p => p.UpdatedAt).ThenByDescending(p => p.Id)
            .Select(p => new ProblemListItem(
                p.Id, p.Number, p.Title, p.Status, p.Impact,
                db.Teams.Where(x => x.Id == p.OwnerTeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == p.OwnerId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                db.ProblemTicketLinks.Count(l => l.ProblemId == p.Id),
                p.UpdatedAt))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
