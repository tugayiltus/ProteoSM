namespace Proteone.Api.Security;

/// <summary>
/// Marks the one endpoint without an antiforgery check: the monitoring webhook, which has no session and signs every
/// request (ADR-0041). Nothing else may carry it (checked by a test).
/// </summary>
internal sealed class SkipCsrfMetadata;
