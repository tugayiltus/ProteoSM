using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Changes;
using ProteoSM.Application.Features.Tickets;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Problems;

/// <param name="TeamName">Null when the viewer cannot open the incident (ADR-0033 summary set).</param>
public sealed record ProblemTicketItem(string Number, string Title, TicketStatus Status, string? TeamName, DateTimeOffset LinkedAt, bool CanOpen);

public sealed record ProblemChangeItem(string Number, string Title, ChangeStatus Status, DateTimeOffset LinkedAt, bool CanOpen);

/// <summary>The incident's problem as the incident detail shows it to IT staff.</summary>
public sealed record TicketProblemItem(string Number, string Title, ProblemStatus Status, string? Workaround, string OwnerTeamName);

/// <summary>A problem on the change detail's "İlgili problemler" panel.</summary>
public sealed record ProblemChangeSideItem(string Number, string Title, ProblemStatus Status);

public sealed record ListProblemTicketsQuery(string Number);

public sealed record AddProblemTicketCommand(string Number, string TicketNumber);

public sealed record RemoveProblemTicketCommand(string Number, string TicketNumber);

public sealed record ListProblemChangesQuery(string Number);

public sealed record AddProblemChangeCommand(string Number, string ChangeNumber);

public sealed record RemoveProblemChangeCommand(string Number, string ChangeNumber);

public sealed record GetTicketProblemQuery(string Number);

public sealed record ListChangeProblemsQuery(string Number);

internal sealed class AddProblemTicketValidator : AbstractValidator<AddProblemTicketCommand>
{
    public AddProblemTicketValidator() => RuleFor(c => c.TicketNumber).NotEmpty().MaximumLength(TicketNumber.MaxLength);
}

internal sealed class AddProblemChangeValidator : AbstractValidator<AddProblemChangeCommand>
{
    public AddProblemChangeValidator() => RuleFor(c => c.ChangeNumber).NotEmpty().MaximumLength(TicketNumber.MaxLength);
}

internal static class ProblemLinkQueries
{
    /// <summary>Incidents of a problem, newest link first; the team shows only when the viewer can open the incident.</summary>
    public static async Task<IReadOnlyList<ProblemTicketItem>> TicketsOfProblemAsync(
        IAppDbContext db, TicketScope tickets, ProblemViewer viewer, Guid problemId, CancellationToken cancellationToken)
    {
        var openable = tickets.Visible(new TicketViewer(viewer.UserId, viewer.Role, viewer.TeamIds));
        return await (
            from l in db.ProblemTicketLinks.AsNoTracking()
            join t in db.Tickets on l.TicketId equals t.Id
            where l.ProblemId == problemId
            orderby l.CreatedAt descending, l.Id descending
            let canOpen = openable.Any(o => o.Id == t.Id)
            select new ProblemTicketItem(
                t.Number, t.Subject, t.Status,
                canOpen ? db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First() : null,
                l.CreatedAt, canOpen)).ToListAsync(cancellationToken);
    }

    /// <summary>Changes of a problem, newest link first; <c>CanOpen</c> is full or summary access to the change.</summary>
    public static async Task<IReadOnlyList<ProblemChangeItem>> ChangesOfProblemAsync(
        IAppDbContext db, ChangeScope changes, ProblemViewer viewer, Guid problemId, CancellationToken cancellationToken)
    {
        var openable = changes.Visible(new ChangeViewer(viewer.UserId, viewer.Role, viewer.TeamIds));
        return await (
            from l in db.ProblemChangeLinks.AsNoTracking()
            join c in db.ChangeRequests on l.ChangeId equals c.Id
            where l.ProblemId == problemId
            orderby l.CreatedAt descending, l.Id descending
            select new ProblemChangeItem(c.Number, c.Title, c.Status, l.CreatedAt, openable.Any(o => o.Id == c.Id)))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Incidents linked to a problem; every staff member reads them, employees get not found.</summary>
public sealed class ListProblemTicketsHandler(IAppDbContext db, ProblemScope scope, TicketScope tickets)
{
    public async Task<IReadOnlyList<ProblemTicketItem>> HandleAsync(ListProblemTicketsQuery query, CancellationToken cancellationToken)
    {
        var (problem, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await ProblemLinkQueries.TicketsOfProblemAsync(db, tickets, viewer, problem.Id, cancellationToken);
    }
}

/// <summary>
/// Links a visible incident to a non-closed problem: for the incident's team (or admin) and for the problem's owner team.
/// The link moves neither record's version, status nor SLA; it touches the ticket's <c>updated_at</c> (ADR-0033) and
/// writes an event and audit row per side, the ticket's event internal-only.
/// </summary>
public sealed class AddProblemTicketHandler(
    IAppDbContext db, IValidator<AddProblemTicketCommand> validator, ProblemScope scope, TicketScope tickets, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProblemTicketItem>> HandleAsync(AddProblemTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        Guid problemId = Guid.Empty;
        ProblemViewer? viewer = null;
        await db.RunSerializedAsync(SerializationLocks.ProblemLink, async ct =>
        {
            var (problem, problemViewer) = await scope.LoadAsync(command.Number, ct);
            (problemId, viewer) = (problem.Id, problemViewer);
            var (ticket, ticketViewer) = await ProblemLinkRules.FindVisibleTicketAsync(tickets, problemViewer, command.TicketNumber, ct);
            ProblemLinkRules.EnsureMayLink(ticketViewer, ticket, problemViewer, problem);
            var now = clock.GetUtcNow();
            await ProblemLinkRules.AddIncidentAsync(db, audit, problem, ticket, problemViewer.UserId, now, ct);
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ProblemTicketAlreadyLinked, ProblemLinkRules.AlreadyLinkedMessage, ct);
            await TicketActivity.TouchAsync(db, ticket.Id, now, ct);
        }, cancellationToken);
        return await ProblemLinkQueries.TicketsOfProblemAsync(db, tickets, viewer!, problemId, cancellationToken);
    }
}

/// <summary>Removes an incident link, also on a closed problem; same people as linking.</summary>
public sealed class RemoveProblemTicketHandler(IAppDbContext db, ProblemScope scope, TicketScope tickets, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProblemTicketItem>> HandleAsync(RemoveProblemTicketCommand command, CancellationToken cancellationToken)
    {
        Guid problemId = Guid.Empty;
        ProblemViewer? viewer = null;
        await db.RunSerializedAsync(SerializationLocks.ProblemLink, async ct =>
        {
            var (problem, problemViewer) = await scope.LoadAsync(command.Number, ct);
            (problemId, viewer) = (problem.Id, problemViewer);
            var (ticket, ticketViewer) = await ProblemLinkRules.FindVisibleTicketAsync(tickets, problemViewer, command.TicketNumber, ct);
            ProblemLinkRules.EnsureMayLink(ticketViewer, ticket, problemViewer, problem);
            var link = await db.ProblemTicketLinks.SingleOrDefaultAsync(l => l.ProblemId == problem.Id && l.TicketId == ticket.Id, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Ticket);
            var now = clock.GetUtcNow();
            db.ProblemTicketLinks.Remove(link);
            var data = new { ticketNumber = ticket.Number, problemNumber = problem.Number };
            ProblemHistory.Record(db, problem, ProblemEventKind.TicketUnlinked, problemViewer.UserId, now, data);
            TicketTimeline.Record(db, ticket, TicketEventKind.ProblemUnlinked, Visibility.Internal, problemViewer.UserId, now, data);
            audit.Record(AuditActions.ProblemTicketUnlinked, AuditEntityTypes.Problem, problem.Id, oldValues: data);
            audit.Record(AuditActions.TicketProblemUnlinked, AuditEntityTypes.Ticket, ticket.Id, oldValues: data);
            await db.SaveChangesAsync(ct);
            await TicketActivity.TouchAsync(db, ticket.Id, now, ct);
        }, cancellationToken);
        return await ProblemLinkQueries.TicketsOfProblemAsync(db, tickets, viewer!, problemId, cancellationToken);
    }
}

/// <summary>Changes linked to a problem; every staff member reads them, employees get not found.</summary>
public sealed class ListProblemChangesHandler(IAppDbContext db, ProblemScope scope, ChangeScope changes)
{
    public async Task<IReadOnlyList<ProblemChangeItem>> HandleAsync(ListProblemChangesQuery query, CancellationToken cancellationToken)
    {
        var (problem, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await ProblemLinkQueries.ChangesOfProblemAsync(db, changes, viewer, problem.Id, cancellationToken);
    }
}

/// <summary>Links a change the actor can see to a non-closed problem; the problem's write rule applies.</summary>
public sealed class AddProblemChangeHandler(
    IAppDbContext db, IValidator<AddProblemChangeCommand> validator, ProblemScope scope, ChangeScope changes, IAuditLog audit, TimeProvider clock)
{
    private const string LinkExistsMessage = "The change is already linked to this problem.";

    public async Task<IReadOnlyList<ProblemChangeItem>> HandleAsync(AddProblemChangeCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        Guid problemId = Guid.Empty;
        ProblemViewer? viewer = null;
        await db.RunSerializedAsync(SerializationLocks.ProblemLink, async ct =>
        {
            var (problem, problemViewer) = await scope.LoadForWorkAsync(command.Number, ct);
            (problemId, viewer) = (problem.Id, problemViewer);
            ProblemLinkRules.EnsureOpen(problem);
            var normalized = ChangeNumber.Normalize(command.ChangeNumber);
            var change = await changes.Visible(new ChangeViewer(problemViewer.UserId, problemViewer.Role, problemViewer.TeamIds))
                .SingleOrDefaultAsync(c => c.Number == normalized, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Change);
            if (await db.ProblemChangeLinks.AnyAsync(l => l.ProblemId == problem.Id && l.ChangeId == change.Id, ct))
            {
                throw new DomainException(ErrorCodes.ProblemChangeLinkExists, LinkExistsMessage);
            }

            var now = clock.GetUtcNow();
            db.ProblemChangeLinks.Add(ProblemChangeLink.Create(problem.Id, change.Id, problemViewer.UserId, now));
            var data = new { changeNumber = change.Number, problemNumber = problem.Number };
            ProblemHistory.Record(db, problem, ProblemEventKind.ChangeLinked, problemViewer.UserId, now, data);
            ChangeHistory.Record(db, change, ChangeEventKind.ProblemLinked, problemViewer.UserId, now, data);
            audit.Record(AuditActions.ProblemChangeLinked, AuditEntityTypes.Problem, problem.Id, newValues: data);
            audit.Record(AuditActions.ChangeProblemLinked, AuditEntityTypes.Change, change.Id, newValues: data);
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ProblemChangeLinkExists, LinkExistsMessage, ct);
        }, cancellationToken);
        return await ProblemLinkQueries.ChangesOfProblemAsync(db, changes, viewer!, problemId, cancellationToken);
    }
}

/// <summary>
/// Removes a change link, also on a closed problem. Only the problem's write rule applies: the row is already on the
/// problem page, so a change the actor can no longer open can still be taken off.
/// </summary>
public sealed class RemoveProblemChangeHandler(IAppDbContext db, ProblemScope scope, ChangeScope changes, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProblemChangeItem>> HandleAsync(RemoveProblemChangeCommand command, CancellationToken cancellationToken)
    {
        Guid problemId = Guid.Empty;
        ProblemViewer? viewer = null;
        await db.RunSerializedAsync(SerializationLocks.ProblemLink, async ct =>
        {
            var (problem, problemViewer) = await scope.LoadForWorkAsync(command.Number, ct);
            (problemId, viewer) = (problem.Id, problemViewer);
            var normalized = ChangeNumber.Normalize(command.ChangeNumber);
            var linked = await (
                from l in db.ProblemChangeLinks
                join c in db.ChangeRequests on l.ChangeId equals c.Id
                where l.ProblemId == problem.Id && c.Number == normalized
                select new { Link = l, Change = c }).SingleOrDefaultAsync(ct)
                ?? throw new NotFoundException(AuditEntityTypes.Change);
            var now = clock.GetUtcNow();
            db.ProblemChangeLinks.Remove(linked.Link);
            var data = new { changeNumber = linked.Change.Number, problemNumber = problem.Number };
            ProblemHistory.Record(db, problem, ProblemEventKind.ChangeUnlinked, problemViewer.UserId, now, data);
            ChangeHistory.Record(db, linked.Change, ChangeEventKind.ProblemUnlinked, problemViewer.UserId, now, data);
            audit.Record(AuditActions.ProblemChangeUnlinked, AuditEntityTypes.Problem, problem.Id, oldValues: data);
            audit.Record(AuditActions.ChangeProblemUnlinked, AuditEntityTypes.Change, linked.Change.Id, oldValues: data);
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await ProblemLinkQueries.ChangesOfProblemAsync(db, changes, viewer!, problemId, cancellationToken);
    }
}

/// <summary>
/// The incident's problem for IT staff who can see the incident, or null when none is linked. Employees get not found,
/// so a requester never learns a problem exists (ADR-0034).
/// </summary>
public sealed class GetTicketProblemHandler(IAppDbContext db, TicketScope tickets)
{
    public async Task<TicketProblemItem?> HandleAsync(GetTicketProblemQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await tickets.LoadAsync(query.Number, cancellationToken);
        if (!viewer.SeesInternal)
        {
            throw new NotFoundException(AuditEntityTypes.Ticket);
        }

        return await (
            from l in db.ProblemTicketLinks.AsNoTracking()
            join p in db.Problems on l.ProblemId equals p.Id
            join team in db.Teams on p.OwnerTeamId equals team.Id
            where l.TicketId == ticket.Id
            select new TicketProblemItem(p.Number, p.Title, p.Status, p.Workaround, team.Name)).SingleOrDefaultAsync(cancellationToken);
    }
}

/// <summary>Problems a change is linked to, for anyone with full or summary access to the change; others get not found.</summary>
public sealed class ListChangeProblemsHandler(IAppDbContext db, ChangeScope changes)
{
    public async Task<IReadOnlyList<ProblemChangeSideItem>> HandleAsync(ListChangeProblemsQuery query, CancellationToken cancellationToken)
    {
        var (change, _, _) = await changes.LoadAsync(query.Number, cancellationToken);
        return await (
            from l in db.ProblemChangeLinks.AsNoTracking()
            join p in db.Problems on l.ProblemId equals p.Id
            where l.ChangeId == change.Id
            orderby l.CreatedAt descending, l.Id descending
            select new ProblemChangeSideItem(p.Number, p.Title, p.Status)).ToListAsync(cancellationToken);
    }
}
