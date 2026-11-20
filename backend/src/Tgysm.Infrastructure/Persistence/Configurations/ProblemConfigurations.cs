using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ProblemConfiguration : IEntityTypeConfiguration<Problem>
{
    public void Configure(EntityTypeBuilder<Problem> builder)
    {
        builder.ToTable("problems", table =>
        {
            table.HasCheckConstraint("ck_problems_status", SnakeCaseEnumConversion<ProblemStatus>.CheckSql("status"));
            table.HasCheckConstraint("ck_problems_impact", SnakeCaseEnumConversion<Level>.CheckSql("impact"));
        });
        builder.ConfigureEntityBase();
        builder.Property(p => p.Number).HasMaxLength(TicketNumber.MaxLength).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(Problem.TitleMaxLength).IsRequired();
        builder.Property(p => p.SearchText).HasMaxLength(Problem.TitleMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(Problem.TextMaxLength).IsRequired();
        builder.Property(p => p.RootCause).HasMaxLength(Problem.TextMaxLength);
        builder.Property(p => p.Workaround).HasMaxLength(Problem.TextMaxLength);
        builder.Property(p => p.Resolution).HasMaxLength(Problem.TextMaxLength);
        builder.Property(p => p.CancelReason).HasMaxLength(Problem.ReasonMaxLength);
        builder.Property(p => p.Status).HasEnumName();
        builder.Property(p => p.Impact).HasEnumName();
        builder.HasIndex(p => p.Number).IsUnique();
        builder.HasIndex(p => new { p.OwnerTeamId, p.Status });
        builder.HasIndex(p => new { p.OwnerId, p.Status });
        builder.HasIndex(p => p.ArticleId).IsUnique().HasFilter("article_id IS NOT NULL").HasDatabaseName("ux_problems_article_id");
        builder.HasIndex(p => new { p.UpdatedAt, p.Id }, "ix_problems_updated_at_id").IsDescending(true, true);
        // Substring search (LIKE '%x%') needs trigrams; a B-tree cannot serve it.
        builder.HasIndex(p => p.SearchText, "ix_problems_search_text_trgm").HasMethod(TrigramIndex.Method).HasOperators(TrigramIndex.Operators);
        builder.HasOne<Team>().WithMany().HasForeignKey(p => p.OwnerTeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(p => p.ArticleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProblemEventConfiguration : IEntityTypeConfiguration<ProblemEvent>
{
    public void Configure(EntityTypeBuilder<ProblemEvent> builder)
    {
        builder.ToTable("problem_events", table => table.HasCheckConstraint("ck_problem_events_kind", SnakeCaseEnumConversion<ProblemEventKind>.CheckSql("kind")));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Kind).HasEnumName();
        builder.Property(e => e.DataJson).HasColumnName("data").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(e => new { e.ProblemId, e.OccurredAt });
        builder.HasOne<Problem>().WithMany().HasForeignKey(e => e.ProblemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProblemTicketLinkConfiguration : IEntityTypeConfiguration<ProblemTicketLink>
{
    public void Configure(EntityTypeBuilder<ProblemTicketLink> builder)
    {
        builder.ToTable("problem_tickets");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        // An incident belongs to at most one problem.
        builder.HasIndex(l => l.TicketId).IsUnique().HasDatabaseName("ux_problem_tickets_ticket_id");
        builder.HasIndex(l => l.ProblemId).HasDatabaseName("ix_problem_tickets_problem_id");
        builder.HasOne<Problem>().WithMany().HasForeignKey(l => l.ProblemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProblemChangeLinkConfiguration : IEntityTypeConfiguration<ProblemChangeLink>
{
    public void Configure(EntityTypeBuilder<ProblemChangeLink> builder)
    {
        builder.ToTable("problem_changes");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.HasIndex(l => new { l.ProblemId, l.ChangeId }).IsUnique().HasDatabaseName("ux_problem_changes_problem_change");
        builder.HasIndex(l => l.ChangeId).HasDatabaseName("ix_problem_changes_change_id");
        builder.HasOne<Problem>().WithMany().HasForeignKey(l => l.ProblemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(l => l.ChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
