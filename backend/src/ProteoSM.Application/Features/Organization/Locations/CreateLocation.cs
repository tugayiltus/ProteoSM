using FluentValidation;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Organization;

namespace ProteoSM.Application.Features.Organization.Locations;

public sealed record CreateLocationCommand(string Name, Guid? ParentId);

internal sealed class CreateLocationValidator : AbstractValidator<CreateLocationCommand>
{
    public CreateLocationValidator() => RuleFor(c => c.Name).ValidLocationName();
}

public sealed class CreateLocationHandler(IAppDbContext db, IValidator<CreateLocationCommand> validator, IAuditLog audit)
{
    public async Task<LocationItem> HandleAsync(CreateLocationCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await LocationRules.EnsureParentActiveAsync(db, command.ParentId, cancellationToken);
        await LocationRules.EnsureNameFreeAsync(db, command.ParentId, command.Name, exceptId: null, cancellationToken);

        var location = Location.Create(command.Name, command.ParentId);
        db.Locations.Add(location);
        audit.Record(AuditActions.LocationCreated, AuditEntityTypes.Location, location.Id, newValues: new { location.Name, location.ParentId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.LocationNameTaken, LocationRules.NameTakenMessage, cancellationToken);
        return LocationItem.From(location);
    }
}
