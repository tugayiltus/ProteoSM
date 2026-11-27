namespace ProteoSM.Application.Common;

/// <summary>The row changed since the client read it (maps to 409 concurrency.conflict).</summary>
public sealed class ConcurrencyException(Exception? innerException = null)
    : Exception("The record was changed by another request.", innerException);
