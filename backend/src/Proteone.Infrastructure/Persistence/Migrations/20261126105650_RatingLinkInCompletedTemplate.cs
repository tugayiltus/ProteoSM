using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds the rating link to the default `ticket_completed` body. A template an admin has edited is left alone:
/// only a body still equal to the seeded default changes.
/// </summary>
public partial class RatingLinkInCompletedTemplate : Migration
{
    private const string EventType = "ticket_completed";
    private const string RatingLine = "\n\nKaydı puanlamak için: {{ratingUrl}}";

    /// <summary>The seeded body (migration EmailTemplates); no later migration changes it.</summary>
    public const string PreviousBody =
        "Sayın {{recipientName}},\n\nBildirim: {{subject}}\nÇözüm: {{resolution}}\nDurum: {{status}}\nTamamlanma zamanı: {{completedAt}}\n\nAyrıntıları kayıt ekranından görüntüleyebilirsiniz: {{ticketUrl}}";

    public const string NewBody = PreviousBody + RatingLine;

    public const string UpSql = $"UPDATE email_templates SET body = '{NewBody}' WHERE event_type = '{EventType}' AND body = '{PreviousBody}';";

    public const string DownSql = $"UPDATE email_templates SET body = '{PreviousBody}' WHERE event_type = '{EventType}' AND body = '{NewBody}';";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(DownSql);
}
