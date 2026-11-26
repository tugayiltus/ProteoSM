using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTicketRatings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ticket_ratings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                score = table.Column<short>(type: "smallint", nullable: false),
                comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                rated_by = table.Column<Guid>(type: "uuid", nullable: false),
                rated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_ticket_ratings", x => x.id);
                table.CheckConstraint("ck_ticket_ratings_score", "score BETWEEN 1 AND 5");
                table.ForeignKey(
                    name: "fk_ticket_ratings_teams_team_id",
                    column: x => x.team_id,
                    principalTable: "teams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ticket_ratings_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ticket_ratings_users_assignee_id",
                    column: x => x.assignee_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_ticket_ratings_users_rated_by",
                    column: x => x.rated_by,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_ticket_ratings_assignee_id",
            table: "ticket_ratings",
            column: "assignee_id");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_ratings_rated_by",
            table: "ticket_ratings",
            column: "rated_by");

        migrationBuilder.CreateIndex(
            name: "ix_ticket_ratings_team_id_rated_at",
            table: "ticket_ratings",
            columns: new[] { "team_id", "rated_at" });

        migrationBuilder.CreateIndex(
            name: "ux_ticket_ratings_ticket_id",
            table: "ticket_ratings",
            column: "ticket_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ticket_ratings");
    }
}
