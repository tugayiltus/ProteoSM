using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ProblemEmailTemplates : Migration
{
    private static readonly Guid CreatedId = new("c1e5a7b2-4d93-4e1f-8a60-7b2d9f3c5e11");
    private static readonly Guid AssignedId = new("d2f6b8c3-5ea4-4f20-9b71-8c3e0a4d6f22");
    private static readonly Guid KnownErrorId = new("e3a7c9d4-6fb5-4a31-8c82-9d4f1b5e7a33");
    private static readonly Guid ResolvedId = new("f4b8dae5-70c6-4b42-9d93-ae502c6f8b44");
    private static readonly Guid CauseFoundId = new("a5c9ebf6-81d7-4c53-8ea4-bf613d709c55");
    private static readonly Guid CauseResolvedId = new("b6dafc07-92e8-4d64-9fb5-c0724e81ad66");
    private static readonly Guid SystemSeedActor = Guid.Empty;
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 11, 27, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Columns = ["id", "event_type", "subject", "body", "updated_by", "updated_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Insert(migrationBuilder, CreatedId, "problem_created",
            "[{{problemNumber}}] Yeni problem açıldı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı yeni bir problem ekibinize açıldı.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}");
        Insert(migrationBuilder, AssignedId, "problem_assigned",
            "[{{problemNumber}}] Problem size atandı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem size veya ekibinize atandı.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}");
        Insert(migrationBuilder, KnownErrorId, "problem_known_error",
            "[{{problemNumber}}] Problem bilinen hata oldu",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem bilinen hata olarak işaretlendi. Sorumlu olduğunuz bağlı olaylarda geçici çözümü uygulayabilirsiniz.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}");
        Insert(migrationBuilder, ResolvedId, "problem_resolved",
            "[{{problemNumber}}] Problem çözüldü",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem çözüldü. Bağlı olaylarınızı gözden geçirip kapatabilirsiniz.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}");
        Insert(migrationBuilder, CauseFoundId, "ticket_cause_found",
            "[{{ticketNumber}}] Bildirdiğiniz sorunun nedeni bulundu",
            "Sayın {{recipientName}},\n\n{{ticketNumber}} numaralı {{subject}} kaydınız için bildirdiğiniz sorunun nedeni bulundu; IT ekibi çözüm üzerinde çalışıyor.\n\nKaydı görüntülemek için: {{ticketUrl}}");
        Insert(migrationBuilder, CauseResolvedId, "ticket_cause_resolved",
            "[{{ticketNumber}}] Bildirdiğiniz sorun giderildi",
            "Sayın {{recipientName}},\n\n{{ticketNumber}} numaralı {{subject}} kaydınız için bildirdiğiniz sorun kalıcı olarak giderildi.\n\nKaydı görüntülemek için: {{ticketUrl}}");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(table: "email_templates", keyColumn: "id", keyValues: new object[] { CreatedId, AssignedId, KnownErrorId, ResolvedId, CauseFoundId, CauseResolvedId });
    }

    private static void Insert(MigrationBuilder migrationBuilder, Guid id, string eventType, string subject, string body) =>
        migrationBuilder.InsertData(
            table: "email_templates",
            columns: Columns,
            values: new object[] { id, eventType, subject, body, SystemSeedActor, SeedTimestamp });
}
