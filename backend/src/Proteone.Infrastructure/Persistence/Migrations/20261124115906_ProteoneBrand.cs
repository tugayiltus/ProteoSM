using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <summary>The product name in the email templates, also in templates an admin has edited.</summary>
public partial class ProteoneBrand : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE email_templates SET subject = replace(subject, 'TGYSM', 'Proteone'), body = replace(body, 'TGYSM', 'Proteone') "
            + "WHERE subject LIKE '%TGYSM%' OR body LIKE '%TGYSM%';");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Text only: which texts held the former name is not known afterwards.
    }
}
