using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proteone.Domain.Organization;
using Proteone.Domain.Services;

namespace Proteone.Infrastructure.Persistence.Configurations;

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("services");
        builder.ConfigureEntityBase();

        builder.Property(s => s.Name).HasMaxLength(ServiceLimits.NameMaxLength).IsRequired();
        builder.Property(s => s.NormalizedName).HasMaxLength(ServiceLimits.NameMaxLength).IsRequired();
        builder.Property(s => s.Description).HasMaxLength(ServiceLimits.DescriptionMaxLength);
        builder.Property(s => s.SearchText).HasMaxLength(ServiceLimits.SearchTextMaxLength).IsRequired();
        builder.HasIndex(s => s.NormalizedName).IsUnique().HasDatabaseName("ux_services_normalized_name");
        builder.HasIndex(s => s.OwnerTeamId).HasDatabaseName("ix_services_owner_team_id");
        // Substring search (LIKE '%x%') needs trigrams; a B-tree cannot serve it.
        builder.HasIndex(s => s.SearchText, "ix_services_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.HasOne<Team>().WithMany().HasForeignKey(s => s.OwnerTeamId).OnDelete(DeleteBehavior.Restrict);
    }
}
