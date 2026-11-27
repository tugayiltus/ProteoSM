using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProblemSla : Migration
{
    // Fixed v7 ids so the seed rows are identical on every database and Down can remove them by key.
    private static readonly Guid HighTargetId = new("019ac000-0000-7000-8000-000000000001");
    private static readonly Guid MediumTargetId = new("019ac000-0000-7000-8000-000000000002");
    private static readonly Guid LowTargetId = new("019ac000-0000-7000-8000-000000000003");
    private static readonly string[] TargetColumns = ["id", "impact", "target_minutes", "warning_percent"];

    // Two, five and ten business days of eight hours, warning at 80 percent.
    private const int HighTargetMinutes = 960;
    private const int MediumTargetMinutes = 2400;
    private const int LowTargetMinutes = 4800;
    private const int WarningPercent = 80;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "problem_sla_targets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                impact = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                target_minutes = table.Column<int>(type: "integer", nullable: false),
                warning_percent = table.Column<int>(type: "integer", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_problem_sla_targets", x => x.id);
                table.CheckConstraint("ck_problem_sla_targets_impact", "impact IN ('high', 'medium', 'low')");
                table.CheckConstraint("ck_problem_sla_targets_target_minutes", "target_minutes BETWEEN 1 AND 527040");
                table.CheckConstraint("ck_problem_sla_targets_warning_percent", "warning_percent BETWEEN 1 AND 99");
            });

        migrationBuilder.CreateTable(
            name: "problem_sla_timers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                target_minutes = table.Column<int>(type: "integer", nullable: false),
                warning_percent = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                elapsed_seconds = table.Column<long>(type: "bigint", nullable: false),
                running_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                warn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                warned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                breached_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                stopped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                result = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_problem_sla_timers", x => x.id);
                table.CheckConstraint("ck_problem_sla_timers_result", "result IN ('met', 'breached', 'cancelled')");
                table.ForeignKey(
                    name: "fk_problem_sla_timers_problems_problem_id",
                    column: x => x.problem_id,
                    principalTable: "problems",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_problem_sla_targets_impact",
            table: "problem_sla_targets",
            column: "impact",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_problem_sla_timers_due_open",
            table: "problem_sla_timers",
            column: "due_at",
            filter: "stopped_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ix_problem_sla_timers_warn_open",
            table: "problem_sla_timers",
            column: "warn_at",
            filter: "stopped_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_problem_sla_timers_problem_id",
            table: "problem_sla_timers",
            column: "problem_id",
            unique: true);

        migrationBuilder.InsertData(table: "problem_sla_targets", columns: TargetColumns, values: new object[] { HighTargetId, "high", HighTargetMinutes, WarningPercent });
        migrationBuilder.InsertData(table: "problem_sla_targets", columns: TargetColumns, values: new object[] { MediumTargetId, "medium", MediumTargetMinutes, WarningPercent });
        migrationBuilder.InsertData(table: "problem_sla_targets", columns: TargetColumns, values: new object[] { LowTargetId, "low", LowTargetMinutes, WarningPercent });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "problem_sla_targets");

        migrationBuilder.DropTable(
            name: "problem_sla_timers");
    }
}
