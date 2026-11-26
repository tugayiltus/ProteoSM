namespace Proteone.Application.Features.Ratings;

/// <summary>The requester's rating on the ticket detail. <see cref="Comment"/> is null for viewers who may not read it.</summary>
public sealed record TicketRatingView(int Score, string? Comment, DateTimeOffset RatedAt);
