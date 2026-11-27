namespace ProteoSM.Domain.Releases;

/// <summary>Release states (ADR-0040): Planlandı, Sürüyor, Tamamlandı, İptal.</summary>
public enum ReleaseStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled,
}

public enum ReleaseEventKind
{
    Created,
    Updated,
    Started,
    Completed,
    Cancelled,
    ChangeAdded,
    ChangeRemoved,
}
