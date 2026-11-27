using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Proteone.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProblemSlaEmailTemplates : Migration
{
    private static readonly Guid WarningId = new("5f0d6c3e-8a1b-4c7e-9d52-3b1f7a64e0a1");
    private static readonly Guid BreachedId = new("a7c24e90-1d3b-4f58-8e6a-92b5d0c3f7b2");
    private static readonly Guid SystemSeedActor = Guid.Empty;
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 11, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_by", "updated_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Insert(migrationBuilder, WarningId, "problem_sla_warning",
            "[{{problemNumber}}] Problem için bilinen hata süresi dolmak üzere",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem için bilinen hata hedefi dolmak üzere.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\nSon zaman: {{dueAt}}\n\nProblemi görüntülemek için: {{problemUrl}}");
        Insert(migrationBuilder, BreachedId, "problem_sla_breached",
            "[{{problemNumber}}] Problem için bilinen hata hedefi aşıldı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem için bilinen hata hedefi aşıldı (SLA ihlali).\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\nSon zaman: {{dueAt}}\n\nProblemi görüntülemek için: {{problemUrl}}");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(table: "email_templates", keyColumn: "id", keyValues: new object[] { WarningId, BreachedId });
    }

    private static void Insert(MigrationBuilder migrationBuilder, Guid id, string eventType, string subject, string body) =>
        migrationBuilder.InsertData(
            table: "email_templates",
            columns: Columns,
            values: new object[] { id, eventType, subject, body, SystemSeedActor, SeedTimestamp });
}
