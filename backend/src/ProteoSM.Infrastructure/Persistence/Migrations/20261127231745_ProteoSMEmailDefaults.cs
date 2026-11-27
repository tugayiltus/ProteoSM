using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProteoSM.Infrastructure.Persistence.Migrations;

/// <summary>
/// The product name in the default email texts becomes ProteoSM and every default gets the footer "ProteoSM by ProteONE".
/// A template changes only while its subject and body still equal the shipped default; a template an admin has edited,
/// sent email and audit history stay as they are.
/// </summary>
public partial class ProteoSMEmailDefaults : Migration
{
    private const string FormerName = "Proteone";
    private const string ProductName = "ProteoSM";
    private const string Footer = "\n\nProteoSM by ProteONE";

    /// <summary>The shipped defaults as they stand after the earlier migrations: event type, subject, body.</summary>
    public static readonly (string EventType, string Subject, string Body)[] Defaults =
    [
        ("account_activation",
            "Hesabınızı etkinleştirin",
            "Sayın {{recipientName}},\n\nProteone hesabınızı etkinleştirmek için aşağıdaki bağlantıyı kullanın.\nBağlantı: {{actionUrl}}\nBağlantı şu zamana kadar geçerlidir: {{expiresAt}}\n\nBu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz."),
        ("approval_decided",
            "[{{ticketNumber}}] Onay sonucu belli oldu",
            "Sayın {{recipientName}},\n\n{{subject}} konulu talebinizin onay süreci sonuçlandı.\nDurum: {{status}}\n\nAyrıntılar için: {{ticketUrl}}"),
        ("approval_requested",
            "[{{ticketNumber}}] Onayınız bekleniyor",
            "Sayın {{recipientName}},\n\n{{subject}} konulu talep onayınızı bekliyor.\nDurum: {{status}}\n\nOnaylamak için: {{ticketUrl}}"),
        ("change_approval_decided",
            "[{{changeNumber}}] Değişiklik onayı karara bağlandı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı değişikliğin onayı karara bağlandı.\nDurum: {{status}}\nGerekçe: {{reason}}\n\nDeğişikliği görüntülemek için: {{changeUrl}}"),
        ("change_approval_requested",
            "[{{changeNumber}}] Değişiklik onayınızı bekliyor",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı değişiklik onayınızı bekliyor.\nRisk: {{risk}}\nPencere: {{windowStart}} - {{windowEnd}}\n\nDeğişikliği görüntülemek için: {{changeUrl}}"),
        ("change_backout_started",
            "[{{changeNumber}}] Değişiklik için geri dönüş başladı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı değişiklik için geri dönüş başladı. Geri dönüş göreviniz var.\nDurum: {{status}}\n\nDeğişikliği görüntülemek için: {{changeUrl}}"),
        ("licence_expiring",
            "{{softwareName}} lisansının süresi bitiyor",
            "Sayın {{recipientName}},\n\n{{teamName}} ekibinin sahip olduğu {{licenceName}} lisansının süresi bitiyor.\nYazılım: {{softwareName}}\nBitiş tarihi: {{expiresOn}}\n\nLisansı görüntülemek için: {{licenceUrl}}"),
        ("password_reset",
            "Şifre sıfırlama isteği",
            "Sayın {{recipientName}},\n\nŞifrenizi sıfırlamak için aşağıdaki bağlantıyı kullanın.\nBağlantı: {{actionUrl}}\nBağlantı şu zamana kadar geçerlidir: {{expiresAt}}\n\nBu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz."),
        ("problem_assigned",
            "[{{problemNumber}}] Problem size atandı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem size veya ekibinize atandı.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}"),
        ("problem_created",
            "[{{problemNumber}}] Yeni problem açıldı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı yeni bir problem ekibinize açıldı.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}"),
        ("problem_known_error",
            "[{{problemNumber}}] Problem bilinen hata oldu",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem bilinen hata olarak işaretlendi. Sorumlu olduğunuz bağlı olaylarda geçici çözümü uygulayabilirsiniz.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}"),
        ("problem_resolved",
            "[{{problemNumber}}] Problem çözüldü",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem çözüldü. Bağlı olaylarınızı gözden geçirip kapatabilirsiniz.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\n\nProblemi görüntülemek için: {{problemUrl}}"),
        ("problem_sla_breached",
            "[{{problemNumber}}] Problem için bilinen hata hedefi aşıldı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem için bilinen hata hedefi aşıldı (SLA ihlali).\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\nSon zaman: {{dueAt}}\n\nProblemi görüntülemek için: {{problemUrl}}"),
        ("problem_sla_warning",
            "[{{problemNumber}}] Problem için bilinen hata süresi dolmak üzere",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı problem için bilinen hata hedefi dolmak üzere.\nEtki: {{impact}}\nDurum: {{status}}\nEkip: {{teamName}}\nSon zaman: {{dueAt}}\n\nProblemi görüntülemek için: {{problemUrl}}"),
        ("public_reply",
            "[{{ticketNumber}}] Kaydınıza yeni yanıt geldi",
            "Sayın {{recipientName}},\n\n{{subject}} konulu kaydınıza yeni bir yanıt eklendi.\nDurum: {{status}}\n\nYanıtı görüntülemek için: {{ticketUrl}}"),
        ("sla_breached",
            "[{{ticketNumber}}] SLA ihlali",
            "Sayın {{recipientName}},\n\n{{subject}} konulu kaydın {{slaMetric}} hedefi aşıldı.\nHedef zaman: {{dueAt}}\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}"),
        ("sla_warning",
            "[{{ticketNumber}}] SLA uyarısı",
            "Sayın {{recipientName}},\n\n{{subject}} konulu kaydın {{slaMetric}} süresi dolmak üzere.\nHedef zaman: {{dueAt}}\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}"),
        ("task_assigned",
            "[{{taskNumber}}] Görev size veya ekibinize atandı",
            "Sayın {{recipientName}},\n\n{{subject}} başlıklı görev size veya ekibinize atandı.\nEkip: {{teamName}}\nDurum: {{status}}\nBitiş tarihi: {{dueDate}}\n\nGörevi görüntülemek için: {{taskUrl}}"),
        ("ticket_assigned",
            "[{{ticketNumber}}] Kayıt size atandı",
            "Sayın {{recipientName}},\n\n{{subject}} konulu kayıt size atandı.\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}"),
        ("ticket_cause_found",
            "[{{ticketNumber}}] Bildirdiğiniz sorunun nedeni bulundu",
            "Sayın {{recipientName}},\n\n{{ticketNumber}} numaralı {{subject}} kaydınız için bildirdiğiniz sorunun nedeni bulundu; IT ekibi çözüm üzerinde çalışıyor.\n\nKaydı görüntülemek için: {{ticketUrl}}"),
        ("ticket_cause_resolved",
            "[{{ticketNumber}}] Bildirdiğiniz sorun giderildi",
            "Sayın {{recipientName}},\n\n{{ticketNumber}} numaralı {{subject}} kaydınız için bildirdiğiniz sorun kalıcı olarak giderildi.\n\nKaydı görüntülemek için: {{ticketUrl}}"),
        ("ticket_completed",
            "[{{ticketNumber}}] {{subject}} tamamlandı",
            "Sayın {{recipientName}},\n\nBildirim: {{subject}}\nÇözüm: {{resolution}}\nDurum: {{status}}\nTamamlanma zamanı: {{completedAt}}\n\nAyrıntıları kayıt ekranından görüntüleyebilirsiniz: {{ticketUrl}}\n\nKaydı puanlamak için: {{ratingUrl}}"),
        ("ticket_created",
            "[{{ticketNumber}}] Kaydınız oluşturuldu",
            "Sayın {{recipientName}},\n\n{{subject}} konulu kaydınız oluşturuldu.\nDurum: {{status}}\n\nKaydı görüntülemek için: {{ticketUrl}}"),
    ];

    /// <summary>The body of a default after this migration: the new product name, then the footer line.</summary>
    public static string NewBody(string defaultBody) => defaultBody.Replace(FormerName, ProductName, StringComparison.Ordinal) + Footer;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var (eventType, subject, body) in Defaults)
        {
            migrationBuilder.Sql(Update(eventType, subject, body, NewBody(body)));
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var (eventType, subject, body) in Defaults)
        {
            migrationBuilder.Sql(Update(eventType, subject, NewBody(body), body));
        }
    }

    /// <summary>Sets the body only where the subject and the body still equal the expected text.</summary>
    private static string Update(string eventType, string subject, string fromBody, string toBody) =>
        $"UPDATE email_templates SET body = {Quote(toBody)} WHERE event_type = {Quote(eventType)} AND subject = {Quote(subject)} AND body = {Quote(fromBody)};";

    private static string Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
}
