using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProblemSlaTimerNoConcurrency : Migration
{
    // xmin is a PostgreSQL system column: the model drops the concurrency token, the table needs no change.
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
