using ProteoSM.Application.Abstractions;

namespace ProteoSM.Worker;

/// <summary>
/// No signed-in user runs a job; each unit-of-work DI scope gets its own generated correlation id for the audit
/// trail, the same role <c>HttpCurrentActor</c> plays for a request in <c>ProteoSM.Api</c>.
/// </summary>
internal sealed class WorkerCurrentActor : ICurrentActor
{
    private const string CorrelationIdPrefix = "worker-";

    public Guid? UserId => null;

    public string CorrelationId { get; } = CorrelationIdPrefix + Guid.NewGuid().ToString("N");
}
