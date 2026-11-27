using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProteoSM.Domain.Organization;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;

namespace ProteoSM.Infrastructure.Persistence.Configurations;

internal sealed class TicketRatingConfiguration : IEntityTypeConfiguration<TicketRating>
{
    private const string ScoreColumnType = "smallint";

    public void Configure(EntityTypeBuilder<TicketRating> builder)
    {
        builder.ToTable("ticket_ratings", table => table.HasCheckConstraint(
            "ck_ticket_ratings_score",
            $"score BETWEEN {RatingLimits.MinScore} AND {RatingLimits.MaxScore}"));
        builder.ConfigureEntityBase();
        builder.Property(r => r.Score).HasColumnType(ScoreColumnType);
        builder.Property(r => r.Comment).HasMaxLength(RatingLimits.CommentMaxLength);
        builder.HasIndex(r => r.TicketId).IsUnique().HasDatabaseName("ux_ticket_ratings_ticket_id");
        builder.HasIndex(r => new { r.TeamId, r.RatedAt }).HasDatabaseName("ix_ticket_ratings_team_id_rated_at");
        builder.HasOne<Ticket>().WithMany().HasForeignKey(r => r.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Team>().WithMany().HasForeignKey(r => r.TeamId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RatedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
