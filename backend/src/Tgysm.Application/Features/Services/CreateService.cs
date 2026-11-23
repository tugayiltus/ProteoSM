using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Services;

namespace Tgysm.Application.Features.Services;

public sealed record CreateServiceCommand(string Name, string? Description, Guid OwnerTeamId);

internal sealed class CreateServiceValidator : AbstractValidator<CreateServiceCommand>
{
    public CreateServiceValidator()
    {
        RuleFor(c => c.Name).ValidServiceName();
        RuleFor(c => c.Description).ValidServiceDescription();
        RuleFor(c => c.OwnerTeamId).NotEmpty();
    }
}

public sealed class CreateServiceHandler(IAppDbContext db, IValidator<CreateServiceCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<ServiceDetail> HandleAsync(CreateServiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        Guid serviceId = default;
        await db.RunSerializedAsync(SerializationLocks.ServiceCatalog, async ct =>
        {
            await ServiceQueries.EnsureOwnerTeamActiveAsync(db, command.OwnerTeamId, ct);
            await ServiceQueries.EnsureNameFreeAsync(db, command.Name, exceptId: null, ct);

            var service = Service.Create(command.Name, command.Description, command.OwnerTeamId, clock.GetUtcNow());
            db.Services.Add(service);
            audit.Record(AuditActions.ServiceCreated, AuditEntityTypes.Service, service.Id,
                newValues: new { service.Name, service.Description, service.OwnerTeamId });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ServiceNameTaken, ServiceQueries.NameTakenMessage, ct);
            serviceId = service.Id;
        }, cancellationToken);
        return await ServiceQueries.LoadDetailAsync(db, serviceId, cancellationToken);
    }
}
