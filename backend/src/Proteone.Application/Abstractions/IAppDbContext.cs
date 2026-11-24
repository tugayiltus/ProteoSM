using Microsoft.EntityFrameworkCore;
using Proteone.Domain.Assets;
using Proteone.Domain.Auth;
using Proteone.Domain.Catalog;
using Proteone.Domain.Knowledge;
using Proteone.Domain.Licences;
using Proteone.Domain.Changes;
using Proteone.Domain.Problems;
using Proteone.Domain.Releases;
using Proteone.Domain.Services;
using Proteone.Domain.Tasks;
using Proteone.Domain.Tickets;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Notifications;
using Proteone.Domain.Operations;
using Proteone.Domain.Organization;
using Proteone.Domain.Reports;
using Proteone.Domain.Sla;
using Proteone.Domain.Users;

namespace Proteone.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<AuthSettings> AuthSettings { get; }
    DbSet<Department> Departments { get; }
    DbSet<Location> Locations { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMembership> TeamMemberships { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<Asset> Assets { get; }
    DbSet<Brand> Brands { get; }
    DbSet<AssetModel> AssetModels { get; }
    DbSet<AssetAssignment> AssetAssignments { get; }
    DbSet<AssetMovement> AssetMovements { get; }
    DbSet<AssetImportJob> AssetImportJobs { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<Approval> Approvals { get; }
    DbSet<TicketAssignment> TicketAssignments { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketEvent> TicketEvents { get; }
    DbSet<WorkTask> WorkTasks { get; }
    DbSet<TaskEvent> TaskEvents { get; }
    DbSet<TaskAssignment> TaskAssignments { get; }
    DbSet<ChangeRequest> ChangeRequests { get; }
    DbSet<ChangeApproval> ChangeApprovals { get; }
    DbSet<ChangeEvent> ChangeEvents { get; }
    DbSet<ChangePlanEditor> ChangePlanEditors { get; }
    DbSet<ChangeAsset> ChangeAssets { get; }
    DbSet<ChangeTicketLink> ChangeTicketLinks { get; }
    DbSet<Problem> Problems { get; }
    DbSet<ProblemEvent> ProblemEvents { get; }
    DbSet<ProblemTicketLink> ProblemTicketLinks { get; }
    DbSet<ProblemChangeLink> ProblemChangeLinks { get; }
    DbSet<Service> Services { get; }
    DbSet<ServiceAsset> ServiceAssets { get; }
    DbSet<Release> Releases { get; }
    DbSet<ReleaseEvent> ReleaseEvents { get; }
    DbSet<Software> Software { get; }
    DbSet<Licence> Licences { get; }
    DbSet<LicenceAssignment> LicenceAssignments { get; }
    DbSet<TicketRelation> TicketRelations { get; }
    DbSet<TicketAsset> TicketAssets { get; }
    DbSet<TicketArticle> TicketArticles { get; }
    DbSet<KnowledgeArticle> KnowledgeArticles { get; }
    DbSet<ArticleRevision> ArticleRevisions { get; }
    DbSet<Category> Categories { get; }
    DbSet<RequestType> RequestTypes { get; }
    DbSet<WorkerHeartbeat> WorkerHeartbeats { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailDelivery> EmailDeliveries { get; }
    DbSet<PasswordToken> PasswordTokens { get; }
    DbSet<BusinessCalendar> BusinessCalendars { get; }
    DbSet<BusinessHours> BusinessHours { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<SlaPolicyVersion> SlaPolicyVersions { get; }
    DbSet<SlaTarget> SlaTargets { get; }
    DbSet<SlaPauseRule> SlaPauseRules { get; }
    DbSet<SlaInstance> SlaInstances { get; }
    DbSet<SlaPause> SlaPauses { get; }
    DbSet<SlaEvent> SlaEvents { get; }
    DbSet<ExportJob> ExportJobs { get; }

    /// <summary>
    /// Saves all tracked changes in one transaction. Throws <see cref="Common.ConcurrencyException"/> on a stale
    /// <see cref="Entity.Version"/> and <see cref="DomainException"/> with <see cref="ErrorCodes.Duplicate"/> on a unique violation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Makes the next save fail with a concurrency error unless the row still has <paramref name="expectedVersion"/>.</summary>
    void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : Entity;

    /// <summary>The same check for the login settings row, which is not an <see cref="Entity"/> (fixed singleton id).</summary>
    void SetExpectedVersion(AuthSettings settings, uint expectedVersion);

    /// <summary>Runs <paramref name="work"/> in one transaction (several saves commit together).</summary>
    Task RunInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction that first takes the advisory lock <paramref name="lockKey"/>,
    /// so check-then-write rules that span rows (last admin, location cycles) cannot race.
    /// </summary>
    Task RunSerializedAsync(long lockKey, Func<CancellationToken, Task> work, CancellationToken cancellationToken);

    /// <summary>
    /// Takes a further advisory lock inside the transaction of <see cref="RunSerializedAsync"/>; it is held until that
    /// transaction ends. Callers that need two locks take them in one fixed order.
    /// </summary>
    Task LockAsync(long lockKey, CancellationToken cancellationToken);
}
