using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EmailDeliveryLastAttemptAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_attempt_at",
            table: "email_deliveries",
            type: "timestamp with time zone",
            nullable: true);

        // A sent delivery had at least the attempt that sent it; failed rows keep null until their next attempt.
        migrationBuilder.Sql("UPDATE email_deliveries SET last_attempt_at = sent_at WHERE sent_at IS NOT NULL;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "last_attempt_at",
            table: "email_deliveries");
    }
}
