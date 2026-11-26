using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Proteone.Domain.Email;
using Proteone.Infrastructure.Persistence.Converters;

namespace Proteone.Infrastructure.Persistence.Configurations;

internal sealed class EmailSettingsConfiguration : IEntityTypeConfiguration<EmailSettings>
{
    public void Configure(EntityTypeBuilder<EmailSettings> builder)
    {
        builder.ToTable("email_settings", table =>
            table.HasCheckConstraint("ck_email_settings_port", $"port BETWEEN {EmailSettings.MinPort} AND {EmailSettings.MaxPort}"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Version).IsRowVersion();
        builder.Property(s => s.Provider).HasConversion(SnakeCaseEnumConversion<EmailProvider>.Converter).HasMaxLength(SnakeCaseEnumConversion<EmailProvider>.MaxLength);
        builder.Property(s => s.Security).HasConversion(SnakeCaseEnumConversion<EmailSecurity>.Converter).HasMaxLength(SnakeCaseEnumConversion<EmailSecurity>.MaxLength);
        builder.Property(s => s.SenderName).HasMaxLength(EmailSettings.SenderNameMaxLength);
        builder.Property(s => s.SenderAddress).HasMaxLength(EmailSettings.SenderAddressMaxLength);
        builder.Property(s => s.Host).HasMaxLength(EmailSettings.HostMaxLength);
        builder.Property(s => s.UserName).HasMaxLength(EmailSettings.UserNameMaxLength);
        builder.Property(s => s.LastTestErrorCode).HasMaxLength(EmailSettings.LastTestErrorCodeMaxLength);
        builder.Ignore(s => s.IsConfigured);
        builder.Ignore(s => s.IsTested);
    }
}
