using Microsoft.EntityFrameworkCore;
using Npgsql;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Assets;
using Proteone.Domain.Auth;
using Proteone.Domain.Catalog;
using Proteone.Domain.Knowledge;
using Proteone.Domain.Licences;
using Proteone.Domain.Changes;
using Proteone.Domain.Problems;
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

namespace Proteone.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMembership> TeamMemberships => Set<TeamMembership>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<AssetModel> AssetModels => Set<AssetModel>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<AssetMovement> AssetMovements => Set<AssetMovement>();
    public DbSet<AssetImportJob> AssetImportJobs => Set<AssetImportJob>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<TicketAssignment> TicketAssignments => Set<TicketAssignment>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<TaskEvent> TaskEvents => Set<TaskEvent>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<ChangeApproval> ChangeApprovals => Set<ChangeApproval>();
    public DbSet<ChangeEvent> ChangeEvents => Set<ChangeEvent>();
    public DbSet<ChangePlanEditor> ChangePlanEditors => Set<ChangePlanEditor>();
    public DbSet<ChangeAsset> ChangeAssets => Set<ChangeAsset>();
    public DbSet<ChangeTicketLink> ChangeTicketLinks => Set<ChangeTicketLink>();
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<ProblemEvent> ProblemEvents => Set<ProblemEvent>();
    public DbSet<ProblemTicketLink> ProblemTicketLinks => Set<ProblemTicketLink>();
    public DbSet<ProblemChangeLink> ProblemChangeLinks => Set<ProblemChangeLink>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceAsset> ServiceAssets => Set<ServiceAsset>();
    public DbSet<Software> Software => Set<Software>();
    public DbSet<Licence> Licences => Set<Licence>();
    public DbSet<LicenceAssignment> LicenceAssignments => Set<LicenceAssignment>();
    public DbSet<TicketRelation> TicketRelations => Set<TicketRelation>();
    public DbSet<TicketAsset> TicketAssets => Set<TicketAsset>();
    public DbSet<TicketArticle> TicketArticles => Set<TicketArticle>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<ArticleRevision> ArticleRevisions => Set<ArticleRevision>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RequestType> RequestTypes => Set<RequestType>();
    public DbSet<TicketCounter> TicketCounters => Set<TicketCounter>();
    public DbSet<AssetTagCounter> AssetTagCounters => Set<AssetTagCounter>();
    public DbSet<AuthSettings> AuthSettings => Set<AuthSettings>();
    public DbSet<LdapSignInFailure> LdapSignInFailures => Set<LdapSignInFailure>();
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();
    public DbSet<PasswordToken> PasswordTokens => Set<PasswordToken>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<BusinessHours> BusinessHours => Set<BusinessHours>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<SlaPolicyVersion> SlaPolicyVersions => Set<SlaPolicyVersion>();
    public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
    public DbSet<SlaPauseRule> SlaPauseRules => Set<SlaPauseRule>();
    public DbSet<SlaInstance> SlaInstances => Set<SlaInstance>();
    public DbSet<SlaPause> SlaPauses => Set<SlaPause>();
    public DbSet<SlaEvent> SlaEvents => Set<SlaEvent>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainException(ErrorCodes.Duplicate, "A unique constraint was violated.", exception);
        }
    }

    public async Task RunInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RunSerializedAsync(long lockKey, Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
        await work(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task LockAsync(long lockKey, CancellationToken cancellationToken)
    {
        if (Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("LockAsync must run inside RunSerializedAsync.");
        }

        await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
    }

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : Entity
    {
        var version = Entry(entity).Property(e => e.Version);
        // An update that changes nothing sends no statement, so the save alone would not notice a stale version.
        if (version.CurrentValue != expectedVersion)
        {
            throw new ConcurrencyException();
        }

        version.OriginalValue = expectedVersion;
    }

    public void SetExpectedVersion(AuthSettings settings, uint expectedVersion) =>
        Entry(settings).Property(s => s.Version).OriginalValue = expectedVersion;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension(Configurations.TrigramIndex.Extension);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
