using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLicences : Migration
{
    private static readonly Guid TemplateId = new("5b0f2a54-6c0e-4c1c-9a5d-3f1de8a0c739");
    private static readonly Guid SystemSeedActor = Guid.Empty;
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] TemplateColumns = ["id", "event_type", "subject", "body", "updated_by", "updated_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "software",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                vendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                search_text = table.Column<string>(type: "character varying(251)", maxLength: 251, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_software", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "licences",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                software_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                search_text = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                owner_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                protected_key = table.Column<string>(type: "text", nullable: true),
                expiry_notified_for = table.Column<DateOnly>(type: "date", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_licences", x => x.id);
                table.CheckConstraint("ck_licences_kind", "kind IN ('per_user', 'per_device')");
                table.CheckConstraint("ck_licences_quantity", "quantity BETWEEN 1 AND 100000");
                table.ForeignKey(
                    name: "fk_licences_software_software_id",
                    column: x => x.software_id,
                    principalTable: "software",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_licences_teams_owner_team_id",
                    column: x => x.owner_team_id,
                    principalTable: "teams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "licence_assignments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                licence_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                released_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_licence_assignments", x => x.id);
                table.CheckConstraint("ck_licence_assignments_target", "(user_id IS NULL) <> (asset_id IS NULL)");
                table.ForeignKey(
                    name: "fk_licence_assignments_assets_asset_id",
                    column: x => x.asset_id,
                    principalTable: "assets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_licence_assignments_licences_licence_id",
                    column: x => x.licence_id,
                    principalTable: "licences",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_licence_assignments_users_assigned_by",
                    column: x => x.assigned_by,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_licence_assignments_users_released_by",
                    column: x => x.released_by,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_licence_assignments_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_licence_assignments_asset_id",
            table: "licence_assignments",
            column: "asset_id");

        migrationBuilder.CreateIndex(
            name: "ix_licence_assignments_assigned_by",
            table: "licence_assignments",
            column: "assigned_by");

        migrationBuilder.CreateIndex(
            name: "ix_licence_assignments_released_by",
            table: "licence_assignments",
            column: "released_by");

        migrationBuilder.CreateIndex(
            name: "ix_licence_assignments_user_id",
            table: "licence_assignments",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ux_licence_assignments_asset",
            table: "licence_assignments",
            columns: new[] { "licence_id", "asset_id" },
            unique: true,
            filter: "released_at IS NULL AND asset_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_licence_assignments_user",
            table: "licence_assignments",
            columns: new[] { "licence_id", "user_id" },
            unique: true,
            filter: "released_at IS NULL AND user_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ix_licences_expires_on",
            table: "licences",
            column: "expires_on");

        migrationBuilder.CreateIndex(
            name: "ix_licences_owner_team_id",
            table: "licences",
            column: "owner_team_id");

        migrationBuilder.CreateIndex(
            name: "ix_licences_search_text_trgm",
            table: "licences",
            column: "search_text")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ix_licences_software_id",
            table: "licences",
            column: "software_id");

        migrationBuilder.CreateIndex(
            name: "ix_software_search_text",
            table: "software",
            column: "search_text")
            .Annotation("Npgsql:IndexMethod", "gin")
            .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

        migrationBuilder.CreateIndex(
            name: "ux_software_search_text",
            table: "software",
            column: "search_text",
            unique: true);

        migrationBuilder.InsertData(
            table: "email_templates",
            columns: TemplateColumns,
            values: new object[]
            {
                TemplateId,
                "licence_expiring",
                "{{softwareName}} lisansının süresi bitiyor",
                "Sayın {{recipientName}},\n\n{{teamName}} ekibinin sahip olduğu {{licenceName}} lisansının süresi bitiyor.\nYazılım: {{softwareName}}\nBitiş tarihi: {{expiresOn}}\n\nLisansı görüntülemek için: {{licenceUrl}}",
                SystemSeedActor,
                SeedTimestamp,
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM email_deliveries WHERE template_type = 'licence_expiring'; DELETE FROM notification_outbox WHERE event_type = 'licence_expiring';");
        migrationBuilder.DeleteData(table: "email_templates", keyColumn: "id", keyValue: TemplateId);

        migrationBuilder.DropTable(
            name: "licence_assignments");

        migrationBuilder.DropTable(
            name: "licences");

        migrationBuilder.DropTable(
            name: "software");
    }
}
