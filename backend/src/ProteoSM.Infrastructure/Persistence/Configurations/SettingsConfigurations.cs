using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Announcements;
using ProteoSM.Domain.Settings;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.ToTable("system_settings", table => table.HasCheckConstraint(
            "ck_system_settings_days",
            $"licence_notice_days BETWEEN {SystemSettings.MinDays} AND {SystemSettings.MaxDays} AND warranty_window_days BETWEEN {SystemSettings.MinDays} AND {SystemSettings.MaxDays}"));
        builder.ConfigureEntityBase();
    }
}

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements", table =>
        {
            table.HasCheckConstraint("ck_announcements_level", SnakeCaseEnumConversion<AnnouncementLevel>.CheckSql("level"));
            table.HasCheckConstraint("ck_announcements_period", "ends_at IS NULL OR ends_at > starts_at");
        });
        builder.ConfigureEntityBase();
        builder.Property(a => a.Title).HasMaxLength(Announcement.TitleMaxLength).IsRequired();
        builder.Property(a => a.Body).HasMaxLength(Announcement.BodyMaxLength).IsRequired();
        builder.Property(a => a.Level).HasEnumName();
        builder.HasIndex(a => a.StartsAt);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
