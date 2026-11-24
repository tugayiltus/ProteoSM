using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddServices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events");

        migrationBuilder.DropCheckConstraint(
            name: "ck_problem_events_kind",
            table: "problem_events");

        migrationBuilder.DropCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events");

        migrationBuilder.AddColumn<Guid>(
            name: "service_id",
            table: "tickets",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "service_id",
            table: "problems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "service_id",
            table: "change_requests",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "services",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                owner_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                search_text = table.Column<string>(type: "character varying(1101)", maxLength: 1101, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_services", x => x.id);
                table.ForeignKey(
                    name: "fk_services_teams_owner_team_id",
                    column: x => x.owner_team_id,
                    principalTable: "teams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "service_assets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                service_id = table.Column<Guid>(type: "uuid", nullable: false),
                asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_service_assets", x => x.id);
                table.ForeignKey(
                    name: "fk_service_assets_assets_asset_id",
                    column: x => x.asset_id,
                    principalTable: "assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_service_assets_services_service_id",
                    column: x => x.service_id,
                    principalTable: "services",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_service_assets_users_created_by_id",
                    column: x => x.created_by_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_tickets_service_id",
            table: "tickets",
            column: "service_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events",
            sql: "kind IN ('created', 'assigned', 'transferred', 'status_changed', 'public_comment', 'internal_note', 'priority_changed', 'approval_requested', 'approval_decided', 'approval_voided', 'approval_reassigned', 'related_request_created', 'asset_linked', 'asset_unlinked', 'completed', 'cancelled', 'change_linked', 'change_unlinked', 'problem_linked', 'problem_unlinked', 'service_set', 'service_cleared')");

        migrationBuilder.CreateIndex(
            name: "ix_problems_service_id",
            table: "problems",
            column: "service_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_problem_events_kind",
            table: "problem_events",
            sql: "kind IN ('created', 'updated', 'started', 'known_error', 'resolved', 'cancelled', 'commented', 'transferred', 'owner_changed', 'ticket_linked', 'ticket_unlinked', 'change_linked', 'change_unlinked', 'article_created', 'service_set', 'service_cleared')");

        migrationBuilder.CreateIndex(
            name: "ix_change_requests_service_id",
            table: "change_requests",
            column: "service_id");

        migrationBuilder.AddCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events",
            sql: "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed', 'ticket_linked', 'ticket_unlinked', 'problem_linked', 'problem_unlinked', 'service_set', 'service_cleared')");

        migrationBuilder.CreateIndex(
            name: "ix_service_assets_asset_id",
            table: "service_assets",
            column: "asset_id");

        migrationBuilder.CreateIndex(
            name: "ix_service_assets_created_by_id",
            table: "service_assets",
            column: "created_by_id");

        migrationBuilder.CreateIndex(
            name: "ux_service_assets_service_asset",
            table: "service_assets",
            columns: new[] { "service_id", "asset_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_services_owner_team_id",
            table: "services",
            column: "owner_team_id");

        migrationBuilder.CreateIndex(
            name: "ix_services_search_text",
            table: "services",
            column: "search_text")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ux_services_normalized_name",
            table: "services",
            column: "normalized_name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "fk_change_requests_services_service_id",
            table: "change_requests",
            column: "service_id",
            principalTable: "services",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_problems_services_service_id",
            table: "problems",
            column: "service_id",
            principalTable: "services",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "fk_tickets_services_service_id",
            table: "tickets",
            column: "service_id",
            principalTable: "services",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_change_requests_services_service_id",
            table: "change_requests");

        migrationBuilder.DropForeignKey(
            name: "fk_problems_services_service_id",
            table: "problems");

        migrationBuilder.DropForeignKey(
            name: "fk_tickets_services_service_id",
            table: "tickets");

        migrationBuilder.DropTable(
            name: "service_assets");

        migrationBuilder.DropTable(
            name: "services");

        migrationBuilder.DropIndex(
            name: "ix_tickets_service_id",
            table: "tickets");

        migrationBuilder.DropCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events");

        migrationBuilder.DropIndex(
            name: "ix_problems_service_id",
            table: "problems");

        migrationBuilder.DropCheckConstraint(
            name: "ck_problem_events_kind",
            table: "problem_events");

        migrationBuilder.DropIndex(
            name: "ix_change_requests_service_id",
            table: "change_requests");

        migrationBuilder.DropCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events");

        migrationBuilder.DropColumn(
            name: "service_id",
            table: "tickets");

        migrationBuilder.DropColumn(
            name: "service_id",
            table: "problems");

        migrationBuilder.DropColumn(
            name: "service_id",
            table: "change_requests");

        migrationBuilder.AddCheckConstraint(
            name: "ck_ticket_events_kind",
            table: "ticket_events",
            sql: "kind IN ('created', 'assigned', 'transferred', 'status_changed', 'public_comment', 'internal_note', 'priority_changed', 'approval_requested', 'approval_decided', 'approval_voided', 'approval_reassigned', 'related_request_created', 'asset_linked', 'asset_unlinked', 'completed', 'cancelled', 'change_linked', 'change_unlinked', 'problem_linked', 'problem_unlinked')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_problem_events_kind",
            table: "problem_events",
            sql: "kind IN ('created', 'updated', 'started', 'known_error', 'resolved', 'cancelled', 'commented', 'transferred', 'owner_changed', 'ticket_linked', 'ticket_unlinked', 'change_linked', 'change_unlinked', 'article_created')");

        migrationBuilder.AddCheckConstraint(
            name: "ck_change_events_kind",
            table: "change_events",
            sql: "kind IN ('created', 'updated', 'submitted', 'withdrawn', 'approved', 'rejected', 'returned', 'approval_voided', 'implementation_started', 'implementation_finished', 'backout_started', 'closed', 'cancelled', 'assets_changed', 'ticket_linked', 'ticket_unlinked', 'problem_linked', 'problem_unlinked')");
    }
}
