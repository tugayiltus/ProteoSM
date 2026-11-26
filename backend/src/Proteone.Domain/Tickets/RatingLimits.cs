namespace Proteone.Domain.Tickets;

/// <summary>Bounds of the satisfaction rating on a completed ticket.</summary>
public static class RatingLimits
{
    public const int MinScore = 1;
    public const int MaxScore = 5;
    public const int CommentMaxLength = 500;
    public const int WindowDays = 14;

    /// <summary>How long after completion the requester may still rate or change the rating.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(WindowDays);
}
