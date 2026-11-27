using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMonitoring : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "monitoring_sources",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                team_id = table.Column<Guid>(type: "uuid", nullable: false),
                requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_id = table.Column<Guid>(type: "uuid", nullable: true),
                category_id = table.Column<Guid>(type: "uuid", nullable: true),
                impact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                urgency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                protected_secret = table.Column<string>(type: "text", nullable: false),
                secret_set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_monitoring_sources", x => x.id);
                table.CheckConstraint("ck_monitoring_sources_impact", "impact IN ('high', 'medium', 'low')");
                table.CheckConstraint("ck_monitoring_sources_urgency", "urgency IN ('high', 'medium', 'low')");
                table.ForeignKey(
                    name: "fk_monitoring_sources_categories_category_id",
                    column: x => x.category_id,
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_monitoring_sources_services_service_id",
                    column: x => x.service_id,
                    principalTable: "services",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_monitoring_sources_teams_team_id",
                    column: x => x.team_id,
                    principalTable: "teams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_monitoring_sources_users_requester_id",
                    column: x => x.requester_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "monitoring_alerts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: false),
                alert_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_firing_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                is_resolved = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_monitoring_alerts", x => x.id);
                table.ForeignKey(
                    name: "fk_monitoring_alerts_monitoring_sources_source_id",
                    column: x => x.source_id,
                    principalTable: "monitoring_sources",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_monitoring_alerts_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalTable: "tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "monitoring_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                source_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_monitoring_events", x => x.id);
                table.ForeignKey(
                    name: "fk_monitoring_events_monitoring_sources_source_id",
                    column: x => x.source_id,
                    principalTable: "monitoring_sources",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "monitoring_source_statuses",
            columns: table => new
            {
                source_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_event_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                failed_count = table.Column<int>(type: "integer", nullable: false),
                last_failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                limit_audited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_monitoring_source_statuses", x => x.source_id);
                table.ForeignKey(
                    name: "fk_monitoring_source_statuses_monitoring_sources_source_id",
                    column: x => x.source_id,
                    principalTable: "monitoring_sources",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_alerts_source_created",
            table: "monitoring_alerts",
            columns: new[] { "source_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_alerts_source_key",
            table: "monitoring_alerts",
            columns: new[] { "source_id", "alert_key" });

        migrationBuilder.CreateIndex(
            name: "ux_monitoring_alerts_ticket",
            table: "monitoring_alerts",
            column: "ticket_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_events_received_at",
            table: "monitoring_events",
            column: "received_at");

        migrationBuilder.CreateIndex(
            name: "ux_monitoring_events_source_event",
            table: "monitoring_events",
            columns: new[] { "source_id", "event_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_sources_category_id",
            table: "monitoring_sources",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_sources_requester_id",
            table: "monitoring_sources",
            column: "requester_id");

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_sources_service_id",
            table: "monitoring_sources",
            column: "service_id");

        migrationBuilder.CreateIndex(
            name: "ix_monitoring_sources_team_id",
            table: "monitoring_sources",
            column: "team_id");

        migrationBuilder.CreateIndex(
            name: "ux_monitoring_sources_normalized_name",
            table: "monitoring_sources",
            column: "normalized_name",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "monitoring_alerts");

        migrationBuilder.DropTable(
            name: "monitoring_events");

        migrationBuilder.DropTable(
            name: "monitoring_source_statuses");

        migrationBuilder.DropTable(
            name: "monitoring_sources");
    }
}
