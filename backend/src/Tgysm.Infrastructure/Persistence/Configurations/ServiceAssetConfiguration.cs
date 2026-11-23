using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Services;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ServiceAssetConfiguration : IEntityTypeConfiguration<ServiceAsset>
{
    public void Configure(EntityTypeBuilder<ServiceAsset> builder)
    {
        builder.ToTable("service_assets");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasIndex(l => new { l.ServiceId, l.AssetId }).IsUnique().HasDatabaseName("ux_service_assets_service_asset");
        builder.HasIndex(l => l.AssetId).HasDatabaseName("ix_service_assets_asset_id");
        builder.HasOne<Service>().WithMany().HasForeignKey(l => l.ServiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(l => l.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
