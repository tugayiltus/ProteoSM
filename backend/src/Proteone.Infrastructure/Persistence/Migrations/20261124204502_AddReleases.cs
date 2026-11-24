using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddReleases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events");

        migrationBuilder.AddColumn<Guid>(
            name: "release_id",
            table: "change_requests",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "releases",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                search_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                owner_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_releases", x => x.id);
                table.CheckConstraint("ck_releases_status", "status IN ('planned', 'in_progress', 'completed', 'cancelled')");
                table.ForeignKey(
                    name: "fk_releases_teams_owner_team_id",
                    column: x => x.owner_team_id,
                    principalTable: "teams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_releases_users_created_by_id",
                    column: x => x.created_by_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_releases_users_owner_id",
                    column: x => x.owner_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "release_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                release_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                data = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_release_events", x => x.id);
                table.CheckConstraint("ck_release_events_kind", "kind IN ('created', 'updated', 'started', 'completed', 'cancelled', 'change_added', 'change_removed')");
                table.ForeignKey(
                    name: "fk_release_events_releases_release_id",
                    column: x => x.release_id,
                    principalTable: "releases",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_release_events_users_actor_user_id",
                    column: x => x.actor_user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_change_requests_release_id",
            table: "change_requests",
            column: "release_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events",
            sql: "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed', 'ticket_linked', 'ticket_unlinked', 'problem_linked', 'problem_unlinked', 'service_set', 'service_cleared', 'release_set', 'release_cleared')");

        migrationBuilder.CreateIndex(
            name: "ix_release_events_actor_user_id",
            table: "release_events",
            column: "actor_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_release_events_release_id_occurred_at",
            table: "release_events",
            columns: new[] { "release_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "ix_releases_created_by_id",
            table: "releases",
            column: "created_by_id");

        migrationBuilder.CreateIndex(
            name: "ix_releases_number",
            table: "releases",
            column: "number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_releases_owner_id",
            table: "releases",
            column: "owner_id");

        migrationBuilder.CreateIndex(
            name: "ix_releases_owner_team_id_status",
            table: "releases",
            columns: new[] { "owner_team_id", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_releases_search_text",
            table: "releases",
            column: "search_text")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_releases_updated_at_id",
            table: "releases",
            columns: new[] { "updated_at", "id" },
            descending: new bool[0]);

        migrationBuilder.AddForeignKey(
            name: "fk_change_requests_releases_release_id",
            table: "change_requests",
            column: "release_id",
            principalTable: "releases",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_change_requests_releases_release_id",
            table: "change_requests");

        migrationBuilder.DropTable(
            name: "release_events");

        migrationBuilder.DropTable(
            name: "releases");

        migrationBuilder.DropIndex(
            name: "ix_change_requests_release_id",
            table: "change_requests");

        migrationBuilder.DropCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events");

        migrationBuilder.DropColumn(
            name: "release_id",
            table: "change_requests");

        migrationBuilder.AddCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events",
            sql: "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed', 'ticket_linked', 'ticket_unlinked', 'problem_linked', 'problem_unlinked', 'service_set', 'service_cleared')");
    }
}
