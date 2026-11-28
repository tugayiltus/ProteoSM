# ProteoSM — durum özeti

Güncel: 2026-11-28. Dal: `feature/proteosm` (henüz `master`'a alınmadı, sahip onayı bekliyor). Depo: `tugayiltus/ProteoSM` (eski adres yönlendirilir).

## Ürün

| Alan | Kapsam | ADR |
|---|---|---|
| Destek | Arıza, talep, onay, SLA, e-posta, ekler, bilgi bankası | 0009, 0011–0019 |
| Varlık | Envanter, zimmet, içe aktarma, cihaz kataloğu, QR etiket | 0010, 0023, 0028, 0029 |
| Operasyon | Görev, değişiklik, bakım takvimi, problem, sürüm | 0031–0034, 0040 |
| Hizmet ve lisans | Hizmet kataloğu, hafif CMDB, yazılım ve lisans | 0036, 0039 |
| İzleme | İmzalı webhook ile arıza açma | 0041 |
| Rapor | Raporlar, drill-down, Excel | 0020, 0022 |
| Yönetim | `/admin`, giriş yöntemi, denetim, genel ayarlar, duyurular | 0030, 0038, 0042 |
| Dağıtım | Docker Compose, yedek, geri yükleme | 0024, 0025 |
| İsim | ProteoSM adı, ProteONE çatı markası, eski adlı kurulumdan yedek ve geri yükleme ile taşıma | 0047 |

## ProteoSM çalışması (dal `feature/proteosm`)

- Ad ve taşıma: ProteoSM adı, eski ortam değişkeni adları yedek olarak okunur, dağıtım koruması, taşıma adımları `docs/operations.md`, tatbikat `tools/proteosm-move-drill.sh` (ADR-0047).
- Ekranlar: rota hata ekranı, İş listem görünümleri ve sayaçlar, arıza başlığı ve ana eylem ("Üzerime al" / "Yanıtla"), ana sayfada yanıt bekleyenler önde, form etiketleri ve tür değiştirme, liste durumları, admin etki sayımları, SLA özeti, önizlemeler, giriş yöntemi adayları, denetim süzgeçleri, e-posta teslimatında son deneme.
- Kanıt: `docs/release/proteosm-audit.md`, tıklama yolu E2E denetimi `frontend/e2e/click-path-audit.spec.ts`.

## Planlanan ve açık işler

Tek iş listesi: `docs/superpowers/plans/2026-11-25-proteone-next.md`. Gereksinimler: `proteosm.md`.
