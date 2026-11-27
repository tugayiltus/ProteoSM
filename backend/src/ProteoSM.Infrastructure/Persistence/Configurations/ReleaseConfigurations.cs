using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Organization;
using ProteoSM.Domain.Releases;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class ReleaseConfiguration : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.ToTable("releases", table => table.HasCheckConstraint("ck_releases_status", SnakeCaseEnumConversion<ReleaseStatus>.CheckSql("status")));
        builder.ConfigureEntityBase();
        builder.Property(r => r.Number).HasMaxLength(TicketNumber.MaxLength).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(Release.TitleMaxLength).IsRequired();
        builder.Property(r => r.SearchText).HasMaxLength(Release.TitleMaxLength).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(Release.TextMaxLength).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(Release.TextMaxLength);
        builder.Property(r => r.CancelReason).HasMaxLength(Release.ReasonMaxLength);
        builder.Property(r => r.Status).HasEnumName();
        builder.HasIndex(r => r.Number).IsUnique();
        builder.HasIndex(r => new { r.OwnerTeamId, r.Status });
        builder.HasIndex(r => new { r.UpdatedAt, r.Id }, "ix_releases_updated_at_id").IsDescending(true, true);
        builder.HasIndex(r => r.SearchText, "ix_releases_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.HasOne<Team>().WithMany().HasForeignKey(r => r.OwnerTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReleaseEventConfiguration : IEntityTypeConfiguration<ReleaseEvent>
{
    public void Configure(EntityTypeBuilder<ReleaseEvent> builder)
    {
        builder.ToTable("release_events", table => table.HasCheckConstraint("ck_release_events_kind", SnakeCaseEnumConversion<ReleaseEventKind>.CheckSql("kind")));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasEnumName();
        builder.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.ReleaseId, e.OccurredAt });
        builder.HasOne<Release>().WithMany().HasForeignKey(e => e.ReleaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
