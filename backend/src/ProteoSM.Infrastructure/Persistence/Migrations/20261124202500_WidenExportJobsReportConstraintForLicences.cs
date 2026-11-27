using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

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
        // The narrower check below refuses rows of this feature.
        migrationBuilder.Sql("DELETE FROM export_jobs WHERE report = 'licences';");

        migrationBuilder.DropCheckConstraint(
            name: "ck_export_jobs_report",
            table: "export_jobs");

        migrationBuilder.AddCheckConstraint(
            name: "ck_export_jobs_report",
            table: "export_jobs",
            sql: "report IN ('incidents', 'requests', 'operations', 'transfers', 'assets', 'warranty', 'changes', 'tasks', 'problems')");
    }
}
