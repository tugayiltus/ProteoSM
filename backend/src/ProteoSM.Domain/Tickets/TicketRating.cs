using ProteoSM.Domain.Common;

namespace ProteoSM.Domain.Tickets;

/// <summary>
/// The requester's satisfaction score for a completed ticket; one per ticket. Team and assignee are copied at
/// rating time so reports stay stable when the ticket moves later.
/// </summary>
public sealed class TicketRating : Entity
{
    /// <summary>Outcomes that can be rated; the single source for EF predicates too.</summary>
    public static readonly TicketOutcome[] RateableOutcomes = [TicketOutcome.Resolved, TicketOutcome.Fulfilled];

    private TicketRating()
    {
    }

    public Guid TicketId { get; private set; }
    public int Score { get; private set; }
    public string? Comment { get; private set; }
    public Guid TeamId { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid RatedBy { get; private set; }
    public DateTimeOffset RatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static bool IsRateable(Ticket ticket) =>
        ticket.Status == TicketStatus.Completed
        && ticket.CompletedAt is not null
        && ticket.Outcome is { } outcome
        && RateableOutcomes.Contains(outcome);

    /// <summary>End of the rating window (exclusive); null when the ticket cannot be rated.</summary>
    public static DateTimeOffset? RateUntil(Ticket ticket) =>
        IsRateable(ticket) ? ticket.CompletedAt + RatingLimits.Window : null;

    public static TicketRating Create(Ticket ticket, Guid raterId, int score, string? comment, DateTimeOffset now)
    {
        var until = RateUntil(ticket) ?? throw new DomainException(ErrorCodes.RatingNotRateable, "The ticket cannot be rated.");
        if (now >= until)
        {
            throw new DomainException(ErrorCodes.RatingWindowClosed, "The rating window is closed.");
        }

        ValidateScore(score);
        return new TicketRating
        {
            TicketId = ticket.Id,
            Score = score,
            Comment = NormalizeComment(comment),
            TeamId = ticket.TeamId,
            AssigneeId = ticket.AssigneeId,
            RatedBy = raterId,
            RatedAt = now,
            UpdatedAt = now,
        };
    }

    public void Update(int score, string? comment, DateTimeOffset now)
    {
        ValidateScore(score);
        Score = score;
        Comment = NormalizeComment(comment);
        UpdatedAt = now;
    }

    private static void ValidateScore(int score)
    {
        if (score is < RatingLimits.MinScore or > RatingLimits.MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "The score is outside the allowed range.");
        }
    }

    private static string? NormalizeComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
}
