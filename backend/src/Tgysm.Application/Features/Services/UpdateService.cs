using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Services;

public sealed record UpdateServiceCommand(Guid Id, uint Version, string Name, string? Description, Guid OwnerTeamId);

internal sealed class UpdateServiceValidator : AbstractValidator<UpdateServiceCommand>
{
    public UpdateServiceValidator()
    {
        RuleFor(c => c.Name).ValidServiceName();
        RuleFor(c => c.Description).ValidServiceDescription();
        RuleFor(c => c.OwnerTeamId).NotEmpty();
    }
}

public sealed class UpdateServiceHandler(IAppDbContext db, IValidator<UpdateServiceCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<ServiceDetail> HandleAsync(UpdateServiceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await db.RunSerializedAsync(SerializationLocks.ServiceCatalog, async ct =>
        {
            var service = await db.Services.SingleOrDefaultAsync(s => s.Id == command.Id, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Service);
            db.SetExpectedVersion(service, command.Version);

            // Only a new owner is checked: a service whose team was deactivated later can still be edited.
            if (command.OwnerTeamId != service.OwnerTeamId)
            {
                await ServiceQueries.EnsureOwnerTeamActiveAsync(db, command.OwnerTeamId, ct);
            }

            if (TurkishText.ToSearchKey(command.Name) != service.NormalizedName)
            {
                await ServiceQueries.EnsureNameFreeAsync(db, command.Name, service.Id, ct);
            }

            var before = new { service.Name, service.Description, service.OwnerTeamId };
            service.Update(command.Name, command.Description, command.OwnerTeamId, clock.GetUtcNow());
            audit.Record(AuditActions.ServiceUpdated, AuditEntityTypes.Service, service.Id, before,
                new { service.Name, service.Description, service.OwnerTeamId });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ServiceNameTaken, ServiceQueries.NameTakenMessage, ct);
        }, cancellationToken);
        return await ServiceQueries.LoadDetailAsync(db, command.Id, cancellationToken);
    }
}
