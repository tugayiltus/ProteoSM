using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class WidenExportJobsReportConstraintForLicences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_export_jobs_report",
            table: "export_jobs");

        migrationBuilder.AddCheckConstraint(
            name: "ck_export_jobs_report",
            table: "export_jobs",
            sql: "report IN ('incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty', 'changes', 'tasks', 'problems', 'licences')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_export_jobs_report",
            table: "export_jobs");

        migrationBuilder.AddCheckConstraint(
            name: "ck_export_jobs_report",
            table: "export_jobs",
            sql: "report IN ('incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty', 'changes', 'tasks', 'problems')");
    }
}
