namespace ProteoSM.Application.Features.Pilot;

/// <summary>One seeded service: its name, a short description and whether the field team owns it (else the intake team).</summary>
internal sealed record PilotService(string Name, string Description, bool FieldTeamOwns);

/// <summary>The pilot service catalogue (Turkish, as the IT team names its services); the same eight at any scale.</summary>
internal static class PilotSeedServiceData
{
    public static readonly PilotService[] Services =
    [
        new("SAP", "Muhasebe, satın alma ve stok işlemleri için kurumsal kaynak planlama sistemi.", FieldTeamOwns: false),
        new("E-posta", "Kurumsal e-posta, takvim ve dağıtım listeleri.", FieldTeamOwns: false),
        new("VPN", "Ofis dışından şirket ağına güvenli bağlantı.", FieldTeamOwns: false),
        new("Dosya sunucusu", "Departman klasörleri ve ortak paylaşım alanları.", FieldTeamOwns: false),
        new("Yazıcılar", "Kat yazıcıları, tarayıcılar ve yazdırma kuyrukları.", FieldTeamOwns: true),
        new("İnternet", "Ofis internet çıkışı ve kablosuz ağ.", FieldTeamOwns: true),
        new("ERP mobil", "Saha personelinin sipariş ve stok için kullandığı mobil uygulama.", FieldTeamOwns: false),
        new("Telefon", "Masa telefonları ve santral.", FieldTeamOwns: true),
    ];
}
