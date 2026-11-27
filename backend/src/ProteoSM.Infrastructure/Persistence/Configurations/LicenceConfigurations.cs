using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Assets;
using ProteoSM.Domain.Licences;
using ProteoSM.Domain.Organization;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class SoftwareConfiguration : IEntityTypeConfiguration<Software>
{
    public void Configure(EntityTypeBuilder<Software> builder)
    {
        builder.ToTable("software");
        builder.ConfigureEntityBase();
        builder.Property(s => s.Name).HasMaxLength(LicenceLimits.NameMaxLength).IsRequired();
        builder.Property(s => s.Vendor).HasMaxLength(LicenceLimits.VendorMaxLength).IsRequired();
        builder.Property(s => s.SearchText).HasMaxLength(LicenceLimits.SearchTextMaxLength).IsRequired();
        builder.Property(s => s.NormalizedKey).HasMaxLength(LicenceLimits.SearchTextMaxLength).IsRequired();
        builder.HasIndex(s => s.NormalizedKey).IsUnique().HasDatabaseName("ux_software_normalized_key");
        builder.HasIndex(s => s.SearchText, "ix_software_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
    }
}

internal sealed class LicenceConfiguration : IEntityTypeConfiguration<Licence>
{
    public void Configure(EntityTypeBuilder<Licence> builder)
    {
        builder.ToTable("licences", table =>
        {
            table.HasCheckConstraint("ck_licences_kind", SnakeCaseEnumConversion<LicenceKind>.CheckSql("kind"));
            table.HasCheckConstraint("ck_licences_quantity", $"quantity BETWEEN {LicenceLimits.MinQuantity} AND {LicenceLimits.MaxQuantity}");
        });
        builder.ConfigureEntityBase();
        builder.Property(l => l.Name).HasMaxLength(LicenceLimits.NameMaxLength).IsRequired();
        builder.Property(l => l.SearchText).HasMaxLength(LicenceLimits.NameMaxLength).IsRequired();
        builder.HasIndex(l => l.SearchText).HasDatabaseName("ix_licences_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.Property(l => l.Kind).HasConversion(SnakeCaseEnumConversion<LicenceKind>.Converter).HasMaxLength(SnakeCaseEnumConversion<LicenceKind>.MaxLength);
        builder.Property(l => l.Notes).HasMaxLength(LicenceLimits.NotesMaxLength);
        builder.HasIndex(l => l.ExpiresOn).HasDatabaseName("ix_licences_expires_on");
        builder.HasOne<Software>().WithMany().HasForeignKey(l => l.SoftwareId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(l => l.OwnerTeamId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LicenceAssignmentConfiguration : IEntityTypeConfiguration<LicenceAssignment>
{
    private const string ActiveUser = "released_at IS NULL AND user_id IS NOT NULL";
    private const string ActiveAsset = "released_at IS NULL AND asset_id IS NOT NULL";

    public void Configure(EntityTypeBuilder<LicenceAssignment> builder)
    {
        builder.ToTable("licence_assignments", table =>
            table.HasCheckConstraint("ck_licence_assignments_target", "(user_id IS NULL) <> (asset_id IS NULL)"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        // One active seat per licence and target, enforced by the database even under races.
        builder.HasIndex(a => new { a.LicenceId, a.UserId }).IsUnique().HasFilter(ActiveUser).HasDatabaseName("ux_licence_assignments_user");
        builder.HasIndex(a => new { a.LicenceId, a.AssetId }).IsUnique().HasFilter(ActiveAsset).HasDatabaseName("ux_licence_assignments_asset");
        builder.HasOne<Licence>().WithMany().HasForeignKey(a => a.LicenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.AssignedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.ReleasedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
