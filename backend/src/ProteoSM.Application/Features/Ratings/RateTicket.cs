using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Tickets;
using ProteoSM.Application.Sla;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Ratings;

public sealed record RateTicketCommand(string Number, int Score, string? Comment);

internal sealed class RateTicketValidator : AbstractValidator<RateTicketCommand>
{
    public RateTicketValidator()
    {
        RuleFor(c => c.Score).InclusiveBetween(RatingLimits.MinScore, RatingLimits.MaxScore);
        // The domain stores the trimmed comment, so the limit applies to the trimmed text.
        RuleFor(c => c.Comment == null ? null : c.Comment.Trim())
            .MaximumLength(RatingLimits.CommentMaxLength)
            .OverridePropertyName(nameof(RateTicketCommand.Comment));
    }
}

/// <summary>
/// The requester rates a completed ticket, or changes the rating while the window is open. The write carries no
/// ticket version: it does not change the ticket itself, only marks activity like a comment does.
/// </summary>
public sealed class RateTicketHandler(
    IAppDbContext db, IValidator<RateTicketCommand> validator, TicketScope scope, TicketSlaReader sla, IAuditLog audit, TimeProvider clock)
{
    public async Task<TicketDetail> HandleAsync(RateTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var (ticket, viewer) = await scope.LoadAsync(command.Number, cancellationToken);
        if (ticket.RequesterId != viewer.UserId)
        {
            throw new DomainException(ErrorCodes.RatingNotRequester, "Only the requester rates the ticket.");
        }

        var now = clock.GetUtcNow();
        EnsureWindowOpen(ticket, now);

        var rating = await db.TicketRatings.SingleOrDefaultAsync(r => r.TicketId == ticket.Id, cancellationToken);
        string action;
        if (rating is null)
        {
            db.TicketRatings.Add(TicketRating.Create(ticket, viewer.UserId, command.Score, command.Comment, now));
            action = AuditActions.RatingSubmitted;
        }
        else
        {
            rating.Update(command.Score, command.Comment, now);
            action = AuditActions.RatingUpdated;
        }

        TicketTimeline.Record(db, ticket, TicketEventKind.RatingGiven, Visibility.Internal, viewer.UserId, now, new { score = command.Score });
        // The comment stays out of the audit log; it is free text of the requester.
        audit.Record(action, AuditEntityTypes.Ticket, ticket.Id, newValues: new { command.Score });

        try
        {
            await db.RunInTransactionAsync(async ct =>
            {
                await db.SaveChangesAsync(ct);
                await TicketActivity.TouchAsync(db, ticket.Id, now, ct);
            }, cancellationToken);
        }
        catch (DomainException exception) when (exception.Code == ErrorCodes.Duplicate)
        {
            // A parallel first rating won the unique index; the client reloads and sees that rating.
            throw new ConcurrencyException(exception);
        }

        return await TicketQueries.LoadDetailAsync(db, sla, clock, ticket.Id, viewer, cancellationToken);
    }

    /// <summary>The domain checks the window on create only, so an update checks it here too.</summary>
    private static void EnsureWindowOpen(Ticket ticket, DateTimeOffset now)
    {
        var until = TicketRating.RateUntil(ticket) ?? throw new DomainException(ErrorCodes.RatingNotRateable, "The ticket cannot be rated.");
        if (now >= until)
        {
            throw new DomainException(ErrorCodes.RatingWindowClosed, "The rating window is closed.");
        }
    }
}
