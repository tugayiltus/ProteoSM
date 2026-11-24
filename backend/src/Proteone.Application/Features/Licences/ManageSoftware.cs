using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Common.Paging;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Licences;

namespace Proteone.Application.Features.Licences;

public sealed record ListSoftwareQuery(bool? Active, string? Search, int Page, int PageSize);

public sealed class ListSoftwareHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<PagedResult<SoftwareItem>> HandleAsync(ListSoftwareQuery query, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        var software = db.Software.AsNoTracking();
        if (query.Active is { } active)
        {
            software = software.Where(s => s.IsActive == active);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Containing(TurkishText.ToSearchKey(query.Search.Trim()));
            software = software.Where(s => EF.Functions.Like(s.SearchText, pattern, LikePattern.EscapeText));
        }

        return await software.OrderBy(s => s.SearchText).ThenBy(s => s.Id)
            .Select(s => new SoftwareItem(s.Id, s.Name, s.Vendor, s.IsActive, db.Licences.Count(l => l.SoftwareId == s.Id), s.Version))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

public sealed record CreateSoftwareCommand(string Name, string Vendor);

internal sealed class CreateSoftwareValidator : AbstractValidator<CreateSoftwareCommand>
{
    public CreateSoftwareValidator()
    {
        RuleFor(c => c.Name).ValidLicenceName();
        RuleFor(c => c.Vendor).ValidVendor();
    }
}

public sealed class CreateSoftwareHandler(
    IAppDbContext db, ICurrentActor actor, IValidator<CreateSoftwareCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<SoftwareItem> HandleAsync(CreateSoftwareCommand command, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var software = Software.Create(command.Name, command.Vendor, clock.GetUtcNow());
        db.Software.Add(software);
        audit.Record(AuditActions.SoftwareCreated, AuditEntityTypes.Software, software.Id, newValues: new { software.Name, software.Vendor });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.SoftwareNameTaken, LicenceQueries.SoftwareNameTakenMessage, cancellationToken);
        return await LicenceQueries.LoadSoftwareAsync(db, software.Id, cancellationToken);
    }
}

public sealed record UpdateSoftwareCommand(Guid Id, uint Version, string Name, string Vendor, bool IsActive);

internal sealed class UpdateSoftwareValidator : AbstractValidator<UpdateSoftwareCommand>
{
    public UpdateSoftwareValidator()
    {
        RuleFor(c => c.Name).ValidLicenceName();
        RuleFor(c => c.Vendor).ValidVendor();
    }
}

/// <summary>Under the seat lock, so a licence is never created for a product while it is being deactivated.</summary>
public sealed class UpdateSoftwareHandler(
    IAppDbContext db, ICurrentActor actor, IValidator<UpdateSoftwareCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<SoftwareItem> HandleAsync(UpdateSoftwareCommand command, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await db.RunSerializedAsync(SerializationLocks.LicenceSeats, async ct =>
        {
            var software = await db.Software.SingleOrDefaultAsync(s => s.Id == command.Id, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Software);
            db.SetExpectedVersion(software, command.Version);
            var before = new { software.Name, software.Vendor, software.IsActive };
            software.Update(command.Name, command.Vendor, command.IsActive, clock.GetUtcNow());
            audit.Record(AuditActions.SoftwareUpdated, AuditEntityTypes.Software, software.Id, before,
                new { software.Name, software.Vendor, software.IsActive });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.SoftwareNameTaken, LicenceQueries.SoftwareNameTakenMessage, ct);
        }, cancellationToken);
        return await LicenceQueries.LoadSoftwareAsync(db, command.Id, cancellationToken);
    }
}
