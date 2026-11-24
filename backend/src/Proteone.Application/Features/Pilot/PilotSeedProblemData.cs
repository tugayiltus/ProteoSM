namespace Proteone.Application.Features.Pilot;

/// <summary>Text of the seeded problems (Turkish, as the pilot users write it); titles are words the central search finds.</summary>
internal static class PilotSeedProblemData
{
    public static readonly string[] ProblemTitles =
    [
        "VPN bağlantısı düzenli kopuyor",
        "Dosya paylaşımı yavaş açılıyor",
        "Yazıcı kuyruğu takılıyor",
        "E-posta gönderimi gecikiyor",
        "Wi-Fi bağlantısı toplantı odalarında kesiliyor",
        "ERP oturumu beklenmedik kapanıyor",
        "Bilgisayarlar sabah geç açılıyor",
        "Ortak diske erişim reddediliyor",
    ];

    public const string Description = "Birden fazla kullanıcı aynı belirtiyi bildirdi; ortak neden araştırılıyor.";
    public const string RootCause = "Sunucu tarafında zaman aşımı değeri çok düşük ayarlanmış.";
    public const string Workaround = "Oturumu kapatıp yeniden açın; sorun sürerse BT masasını arayın.";
    public const string Resolution = "Ayar düzeltildi ve izlemeye alındı.";
    public const string CancelReason = "Kayıt başka bir problemle birleştirildi.";
}
