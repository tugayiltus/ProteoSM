namespace Tgysm.Domain.Problems;

/// <summary>Problem states (ADR-0034): open → investigating → known_error → resolved | cancelled; no reopen.</summary>
public enum ProblemStatus
{
    Open,
    Investigating,
    KnownError,
    Resolved,
    Cancelled,
}

public enum ProblemEventKind
{
    Created,
    Updated,
    Started,
    KnownError,
    Resolved,
    Cancelled,
    Commented,
    Transferred,
    OwnerChanged,
    TicketLinked,
    TicketUnlinked,
    ChangeLinked,
    ChangeUnlinked,
    ArticleCreated,
}
