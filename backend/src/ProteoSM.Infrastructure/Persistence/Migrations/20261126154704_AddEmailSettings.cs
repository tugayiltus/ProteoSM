using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddEmailSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "email_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                sender_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                sender_address = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                host = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: false),
                port = table.Column<int>(type: "integer", nullable: false),
                security = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                user_name = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                password_protected = table.Column<string>(type: "text", nullable: true),
                tested_fingerprint = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                last_tested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_test_succeeded = table.Column<bool>(type: "boolean", nullable: true),
                last_test_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_email_settings", x => x.id);
                table.CheckConstraint("ck_email_settings_port", "port BETWEEN 1 AND 65535");
            });

        // The single mail settings row: a Google preset, not active, so mail goes out with the environment settings.
        migrationBuilder.Sql(
            "INSERT INTO email_settings (id, provider, sender_address, host, port, security, is_active, updated_at) " +
            "VALUES ('00000000-0000-7000-8000-00000000e3a1', 'google', '', 'smtp.gmail.com', 587, 'start_tls', false, now())");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "email_settings");
    }
}
