using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Catalog;
using ProteoSM.Domain.Monitoring;
using ProteoSM.Domain.Organization;
using ProteoSM.Domain.Services;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class MonitoringSourceConfiguration : IEntityTypeConfiguration<MonitoringSource>
{
    public void Configure(EntityTypeBuilder<MonitoringSource> builder)
    {
        builder.ToTable("monitoring_sources", table =>
        {
            table.HasCheckConstraint("ck_monitoring_sources_impact", SnakeCaseEnumConversion<Level>.CheckSql("impact"));
            table.HasCheckConstraint("ck_monitoring_sources_urgency", SnakeCaseEnumConversion<Level>.CheckSql("urgency"));
        });
        builder.ConfigureEntityBase();
        builder.Property(s => s.Name).HasMaxLength(MonitoringLimits.NameMaxLength).IsRequired();
        builder.Property(s => s.NormalizedName).HasMaxLength(MonitoringLimits.NameMaxLength).IsRequired();
        builder.Property(s => s.ProtectedSecret).IsRequired();
        builder.Property(s => s.Impact).HasEnumName();
        builder.Property(s => s.Urgency).HasEnumName();
        builder.HasIndex(s => s.NormalizedName).IsUnique().HasDatabaseName("ux_monitoring_sources_normalized_name");
        builder.HasOne<Team>().WithMany().HasForeignKey(s => s.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Service>().WithMany().HasForeignKey(s => s.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(s => s.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MonitoringSourceStatusConfiguration : IEntityTypeConfiguration<MonitoringSourceStatus>
{
    public void Configure(EntityTypeBuilder<MonitoringSourceStatus> builder)
    {
        builder.ToTable("monitoring_source_statuses");
        builder.HasKey(s => s.SourceId);
        builder.Property(s => s.SourceId).ValueGeneratedNever();
        builder.HasOne<MonitoringSource>().WithMany().HasForeignKey(s => s.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MonitoringEventConfiguration : IEntityTypeConfiguration<MonitoringEvent>
{
    public void Configure(EntityTypeBuilder<MonitoringEvent> builder)
    {
        builder.ToTable("monitoring_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.EventId).HasMaxLength(MonitoringLimits.EventIdMaxLength).IsRequired();
        builder.HasIndex(e => new { e.SourceId, e.EventId }).IsUnique().HasDatabaseName("ux_monitoring_events_source_event");
        builder.HasIndex(e => e.ReceivedAt).HasDatabaseName("ix_monitoring_events_received_at");
        builder.HasOne<MonitoringSource>().WithMany().HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MonitoringAlertConfiguration : IEntityTypeConfiguration<MonitoringAlert>
{
    public void Configure(EntityTypeBuilder<MonitoringAlert> builder)
    {
        builder.ToTable("monitoring_alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.AlertKey).HasMaxLength(MonitoringLimits.AlertKeyMaxLength).IsRequired();
        builder.HasIndex(a => a.TicketId).IsUnique().HasDatabaseName("ux_monitoring_alerts_ticket");
        builder.HasIndex(a => new { a.SourceId, a.AlertKey }).HasDatabaseName("ix_monitoring_alerts_source_key");
        builder.HasIndex(a => new { a.SourceId, a.CreatedAt }).HasDatabaseName("ix_monitoring_alerts_source_created");
        builder.HasOne<MonitoringSource>().WithMany().HasForeignKey(a => a.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
