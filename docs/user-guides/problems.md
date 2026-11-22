# Problemler

Bu sayfa IT personeli ve yöneticiler içindir. Çalışanlar problemleri görmez; arıza kayıtlarında da problem bilgisi çıkmaz.

Problem, birçok arızanın ortak nedenidir. Aynı hata tekrar tekrar geliyorsa (her pazartesi kopan VPN, hep aynı modelde sıkışan yazıcı) her arızayı ayrı ayrı çözmek yerine bir problem açın. Kök neden, geçici çözüm ve kalıcı çözüm problemde bir kez yazılır; tekrar eden arızalar probleme bağlanır. Problem numarası `PRB-` ile başlar (örneğin `PRB-2026-000004`). Problemlerin SLA süresi ve e-postası yoktur.

## Problemin durumları

| Durum | Anlamı |
|---|---|
| **Açık** | Problem açıldı, henüz incelenmiyor. |
| **İnceleniyor** | Ekip nedeni arıyor. |
| **Bilinen hata** | Kök neden ve geçici çözüm biliniyor; masa geçici çözümü uygulayabilir. |
| **Çözüldü** | Kalıcı çözüm uygulandı. Problem kapandı. |
| **İptal edildi** | Problem gereksiz çıktı (örneğin başka bir problemin tekrarı). |

Kapanan bir problem yeniden açılmaz. Aynı hata sonra tekrar ederse yeni bir problem açın.

## Problem açma

1. Menüde **Problemler** sayfasını açın ve **Yeni problem** düğmesine basın.
2. **Başlık** ve **Belirti** alanlarını doldurun. Belirti, kullanıcıların gördüğü hatadır.
3. **Etki** seçin: **Düşük**, **Orta** veya **Yüksek**.
4. **Sahip ekip** seçin. Yalnızca üyesi olduğunuz ekipler çıkar; yöneticiler her ekibi seçebilir. İsterseniz **Sahip** listesinden ekipten birini seçin.
5. **Oluştur** düğmesine basın. Problem **Açık** durumunda açılır.

Bir arıza üzerinde çalışırken de problem açabilirsiniz: arızanın **Problem** kartında **Problem oluştur** düğmesine basın. Form arızanın konusu ve açıklamasıyla dolu gelir; kaydedince arıza yeni probleme bağlanır.

## Problem listesi

**Kuyruk** filtresiyle listeyi seçin:

| Kuyruk | İçindekiler |
|---|---|
| **Problemlerim** | Sahibi siz olan problemler. |
| **Ekibimin problemleri** | Ekiplerinizin bütün problemleri, kapananlar da. |
| **Ekibimin açık problemleri** | Ekiplerinizin **Açık**, **İnceleniyor** ve **Bilinen hata** durumundaki problemleri. |
| **Açık problemler** | Bütün ekiplerin kapanmamış problemleri. |
| **Bilinen hatalar** | **Bilinen hata** durumundaki problemler. |

**Numara veya başlıkta ara**, **Durum** ve **Etki** ile listeyi daraltın. **Arıza** sütunu probleme bağlı arıza sayısını gösterir. **İş listem** sayfasındaki **Ekibimin problemleri** kartı, ekiplerinizin açık problem sayısını gösterir ve aynı listeyi açar.

## Problem üzerinde çalışma

Her IT personeli bütün problemleri okuyabilir. Problemi düzenlemek, durumunu değiştirmek, not eklemek ve değişiklik bağlamak için sahip ekibin üyesi veya yönetici olmalısınız.

1. Problemi açın. **İşlemler** kartında o anki durumda yapılabilecek işlemler çıkar.
2. İncelemeye başlayınca **İncelemeye al** düğmesine basın.
3. Nedeni bulunca **Düzenle** ile **Kök neden** ve **Geçici çözüm** alanlarını doldurup **Kaydet** düğmesine basın.
4. **Bilinen hata olarak işaretle** düğmesine basın. Bu düğme, kök neden ve geçici çözüm yazılmadan çalışmaz.
5. Kalıcı çözüm uygulanınca **Çözüldü olarak kapat** düğmesine basın ve **Kalıcı çözüm** alanını doldurun.

Diğer işlemler:

| İşlem | Ne yapar |
|---|---|
| **Düzenle** | Başlığı, belirtiyi, etkiyi, kök nedeni ve geçici çözümü değiştirir. Problem kapanana kadar açıktır. |
| **Aktar** | Problemi bir nedenle başka bir ekibe verir (**Yeni ekip**, **Neden**). Sahip boşalır. |
| **Sahip ata** | Sahip ekipten birini problemin sahibi yapar. |
| **İptal et** | Problemi bir **Neden** ile kapatır. |

**Geçmiş ve notlar** bölümü problemde olan her şeyi sırayla gösterir. **Not ekle** ile iç not yazın.

Siz problemi açtıktan sonra başka biri onu değiştirdiyse işleminiz reddedilir ve sayfa yenilenir. Güncel bilgiye bakıp işlemi tekrar yapın.

## Arıza ve değişiklik bağlama

**İlgili arızalar** bölümü probleme bağlı arızaları gösterir.

1. **Arıza bağla** düğmesine basın.
2. **Arıza numarası** alanına arızanın numarasını yazın (örneğin `INC-2026-000123`) ve **Bağla** düğmesine basın.

Kurallar:

- Yalnızca arızalar bağlanır; talepler bağlanmaz.
- Bir arıza yalnızca bir probleme bağlanır. Başka bir probleme bağlıysa önce oradan **Kaldır** ile ayırın.
- **Çözüldü** veya **İptal edildi** durumundaki probleme yeni arıza bağlanmaz.
- Bağlantı arızanın durumunu ve SLA süresini değiştirmez. Arızanın geçmişinde bir iç kayıt olarak görünür.
- Arızanın ekibindeyseniz, problemin sahip ekibinde olmasanız bile o arızayı probleme bağlayabilir ve ayırabilirsiniz. Arızanın **Problem** kartındaki **Probleme bağla** da bu işi yapar.

**Düzelten değişiklikler** bölümü problemi gideren değişiklikleri gösterir. **Değişiklik bağla** ile **Değişiklik numarası** yazıp **Bağla** düğmesine basın; **Kaldır** bağlantıyı siler. Değişiklik sayfasındaki **İlgili problemler** bölümü bu bağlantıları yalnızca gösterir; bağlantı problem sayfasından kurulur.

Açma yetkiniz olmayan bir arıza veya değişiklik listede numarası, başlığı ve durumuyla görünür, ama bağlantı olarak açılmaz (**Açma yetkiniz yok**).

## Bilinen hata makalesi

Problem **Bilinen hata** veya **Çözüldü** durumundayken **Bilinen hata makalesi** kutusunda **Makale oluştur** düğmesine basın. Bilgi bankasında "Bilinen hata: <problem başlığı>" adlı bir taslak açılır. Makalede belirti, kök neden, geçici çözüm (problem çözüldüyse kalıcı çözüm de) ve problem numarası yazar. Makale yalnızca IT personeline görünür ve taslaktır.

- Her problem için bir makale oluşturulur. Sonra kutuda makalenin bağlantısı çıkar.
- Problemi sonradan değiştirmek makaleyi değiştirmez; makaleyi bilgi bankasında düzenleyin.
- Makaleyi çalışanlara açmak için bilgi bankasında görünürlüğünü değiştirip yayımlayın (bkz. [Bilgi bankası](knowledge-base.md)).

## İpuçları

- Aynı hata üçüncü kez geldiyse problem açın. Masa, arızanın **Problem** kartında geçici çözümü hemen görür.
- Geçici çözümü kısa ve adım adım yazın; masa onu doğrudan kullanıcıya anlatır.
- Kalıcı çözüm bir değişiklik gerektiriyorsa değişikliği açın ve **Düzelten değişiklikler** bölümünden bağlayın.
- Problemleri ana menüdeki **Ara** ile de bulabilirsiniz; tam `PRB-` numarası problemi doğrudan açar.
- Problem sayıları ve çözüm süreleri **Raporlar** sayfasındaki **Problemler** sekmesindedir (bkz. [Raporlar](reports.md)).
