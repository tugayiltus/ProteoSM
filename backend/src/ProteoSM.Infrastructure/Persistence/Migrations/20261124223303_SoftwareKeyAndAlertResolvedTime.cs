using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class SoftwareKeyAndAlertResolvedTime : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_software_search_text",
            table: "software");

        migrationBuilder.AddColumn<string>(
            name: "normalized_key",
            table: "software",
            type: "character varying(251)",
            maxLength: 251,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_resolved_at",
            table: "monitoring_alerts",
            type: "timestamp with time zone",
            nullable: true);

        // Unique so far by the search text; an edit of a row writes its own key.
        migrationBuilder.Sql("UPDATE software SET normalized_key = search_text;");

        migrationBuilder.CreateIndex(
            name: "ux_software_normalized_key",
            table: "software",
            column: "normalized_key",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_software_normalized_key",
            table: "software");

        migrationBuilder.DropColumn(
            name: "normalized_key",
            table: "software");

        migrationBuilder.DropColumn(
            name: "last_resolved_at",
            table: "monitoring_alerts");

        migrationBuilder.CreateIndex(
            name: "ux_software_search_text",
            table: "software",
            column: "search_text",
            unique: true);
    }
}
