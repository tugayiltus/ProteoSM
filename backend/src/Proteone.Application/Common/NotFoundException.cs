using Proteone.Domain.Common;

namespace Proteone.Application.Common;

public sealed class NotFoundException(string entityType) : Exception($"{entityType} was not found.")
{
    public string Code { get; } = ErrorCodes.NotFound;

    public string EntityType { get; } = entityType;
}
