using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddSettingsAndAnnouncements : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "announcements",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                body = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                level = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_announcements", x => x.id);
                table.CheckConstraint("ck_announcements_level", "level IN ('info', 'warning')");
                table.CheckConstraint("ck_announcements_period", "ends_at IS NULL OR ends_at > starts_at");
                table.ForeignKey(
                    name: "fk_announcements_users_created_by_id",
                    column: x => x.created_by_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "system_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                licence_notice_days = table.Column<int>(type: "integer", nullable: false),
                warranty_window_days = table.Column<int>(type: "integer", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_system_settings", x => x.id);
                table.CheckConstraint("ck_system_settings_days", "licence_notice_days BETWEEN 1 AND 365 AND warranty_window_days BETWEEN 1 AND 365");
            });

        // The single settings row (ADR-0042); the values were fixed in code before.
        migrationBuilder.Sql(
            "INSERT INTO system_settings (id, licence_notice_days, warranty_window_days, updated_at) " +
            "VALUES ('00000000-0000-7000-8000-0000000005e7', 30, 90, now())");

        migrationBuilder.CreateIndex(
            name: "ix_announcements_created_by_id",
            table: "announcements",
            column: "created_by_id");

        migrationBuilder.CreateIndex(
            name: "ix_announcements_starts_at",
            table: "announcements",
            column: "starts_at");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "announcements");

        migrationBuilder.DropTable(
            name: "system_settings");
    }
}
