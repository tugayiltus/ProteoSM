using FluentValidation;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Organization;

namespace ProteoSM.Application.Features.Organization.Departments;

public sealed record CreateDepartmentCommand(string Name, Guid? ApproverUserId);

internal sealed class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator() => RuleFor(c => c.Name).ValidDepartmentName();
}

public sealed class CreateDepartmentHandler(IAppDbContext db, IValidator<CreateDepartmentCommand> validator, IAuditLog audit)
{
    public async Task<DepartmentItem> HandleAsync(CreateDepartmentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await DepartmentQueries.EnsureNameFreeAsync(db, command.Name, exceptId: null, cancellationToken);
        await DepartmentQueries.EnsureApproverAsync(db, command.ApproverUserId, cancellationToken);

        var department = Department.Create(command.Name, command.ApproverUserId);
        db.Departments.Add(department);
        audit.Record(AuditActions.DepartmentCreated, AuditEntityTypes.Department, department.Id,
            newValues: new { department.Name, department.ApproverUserId });
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.DepartmentNameTaken, DepartmentQueries.NameTakenMessage, cancellationToken);
        return await DepartmentQueries.LoadItemAsync(db, department.Id, cancellationToken);
    }
}
