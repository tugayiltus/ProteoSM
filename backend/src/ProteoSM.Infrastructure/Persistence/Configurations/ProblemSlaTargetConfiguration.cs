using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Sla;
using ProteoSM.Domain.Tickets;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class ProblemSlaTargetConfiguration : IEntityTypeConfiguration<ProblemSlaTarget>
{
    public void Configure(EntityTypeBuilder<ProblemSlaTarget> builder)
    {
        builder.ToTable("problem_sla_targets", table =>
        {
            table.HasCheckConstraint("ck_problem_sla_targets_impact", SnakeCaseEnumConversion<Level>.CheckSql("impact"));
            table.HasCheckConstraint("ck_problem_sla_targets_target_minutes", $"target_minutes BETWEEN 1 AND {SlaTarget.MaxTargetMinutes}");
            table.HasCheckConstraint(
                "ck_problem_sla_targets_warning_percent",
                $"warning_percent BETWEEN {SlaTarget.MinWarningPercent} AND {SlaTarget.MaxWarningPercent}");
        });
        builder.ConfigureEntityBase();
        builder.Property(t => t.Impact).HasEnumName().IsRequired();
        builder.Property(t => t.TargetMinutes).IsRequired();
        builder.Property(t => t.WarningPercent).IsRequired();

        // One row per impact level; the rows are seeded by the migration and only edited afterwards.
        builder.HasIndex(t => t.Impact).IsUnique().HasDatabaseName("ux_problem_sla_targets_impact");
    }
}
