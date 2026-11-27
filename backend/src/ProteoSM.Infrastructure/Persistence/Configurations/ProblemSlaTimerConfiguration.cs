using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Sla;
using ProteoSM.Infrastructure.Persistence.Converters;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class ProblemSlaTimerConfiguration : IEntityTypeConfiguration<ProblemSlaTimer>
{
    public void Configure(EntityTypeBuilder<ProblemSlaTimer> builder)
    {
        builder.ToTable("problem_sla_timers", table =>
            table.HasCheckConstraint("ck_problem_sla_timers_result", SnakeCaseEnumConversion<SlaResult>.CheckSql("result")));
        builder.ConfigureEntityBase();

        // No concurrency token, like ticket timers: the monitor writes only the warning/breach columns through a
        // conditional update, so it and a user's stop or re-target must not fail each other with a 409.
        builder.Ignore(t => t.Version);
        builder.Ignore(t => t.IsStopped);
        builder.Property(t => t.Result).HasEnumName();
        builder.Property(t => t.TargetMinutes).IsRequired();
        builder.Property(t => t.WarningPercent).IsRequired();
        builder.Property(t => t.ElapsedSeconds).IsRequired();
        builder.Property(t => t.StartedAt).IsRequired();
        builder.Property(t => t.RunningSince).IsRequired();
        builder.Property(t => t.DueAt).IsRequired();
        builder.Property(t => t.WarnAt).IsRequired();

        // One timer per problem; it goes away with the problem.
        builder.HasIndex(t => t.ProblemId).IsUnique().HasDatabaseName("ux_problem_sla_timers_problem_id");

        // The monitor's sweeps look at running timers only.
        builder.HasIndex(t => t.DueAt).HasFilter("stopped_at IS NULL").HasDatabaseName("ix_problem_sla_timers_due_open");
        builder.HasIndex(t => t.WarnAt).HasFilter("stopped_at IS NULL").HasDatabaseName("ix_problem_sla_timers_warn_open");
        builder.HasOne<Problem>().WithMany().HasForeignKey(t => t.ProblemId).OnDelete(DeleteBehavior.Cascade);
    }
}
