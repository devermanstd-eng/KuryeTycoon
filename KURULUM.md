# Kurye Tycoon — Unity Kurulum Rehberi

Oynanabilir 2D prototip. Hiç görsel, prefab veya Canvas gerekmez: tek bir script sahneyi kurar.

## 1. Proje oluştur
1. **Unity Hub**'ı aç → **New project**.
2. Şablon: **2D (Built-In Render Pipeline)** veya **Universal 2D**.
3. Sürüm: **Unity 2022.3 LTS** veya **Unity 6** (2021.3+ da çalışır).
4. Proje adı: `KuryeTycoon` → **Create project**.

## 2. Scriptleri ekle
1. İndirdiğin `KuryeTycoon.zip` dosyasını aç.
2. İçindeki **`KuryeTycoon`** klasörünü Unity'deki **Project** penceresinde **Assets** klasörünün içine sürükle.
3. Unity'nin derlemesini bekle (sağ altta dönen simge durunca biter). **Console**'da kırmızı hata olmamalı.

## 3. Sahneyi kur
1. **Hierarchy** → sağ tık → **Create Empty**.
2. Adını **`Oyun`** yap.
3. **Inspector** → **Add Component** → `GameBootstrap` yaz → seç.
4. Sahnede Main Camera zaten varsa dokunma; yoksa script kendisi oluşturur.

## 4. Sahneyi kaydet ve Build Settings'e ekle
1. **File → Save As…** → `Assets/Scenes/Ana.unity`.
2. **File → Build Settings** (Unity 6'da **Build Profiles**) → **Add Open Scenes**.
   (İflas sonrası "Yeniden başla" butonunun çalışması için gerekli.)

## 5. Oyna
1. **Play**'e bas.
2. Şirket adını yaz, ana ve ikincil rengini seç → **Şirketi Kur ve Başla**.
3. Kısayollar: **Boşluk** = duraklat, **1 / 2 / 3** = 1x / 2x / 4x hız.
4. Game penceresi küçükse üstteki **Free Aspect**'i **Full HD (1920x1080)** yap.

## Oyunda neler var
| Sistem | Ne yapar |
|---|---|
| Şirket kurulumu | İsim, ana/ikincil renk; kuryeler ve merkez ofis bu renklerde |
| Kuryeler | Yürüyerek, bisiklet, e-scooter, motosiklet, araba; hız, kapasite ve ücret farklı |
| İşe alım | Başvurular zamanla gelir, kabul/red edersin; işe alım ve çıkarma ücretsiz |
| Moral ve yorgunluk | Aynı anda kaç paket alacağını ve sipariş reddetme ihtimalini belirler; çok yorulan kurye mola verir |
| Ödeme | Paket başı sabit + km başı (175–286 Ft, araca göre) + hava/gece/yoğun saat bonusu × bonus çarpanı |
| Kâr kuralı | Teslimat ücreti, en pahalı kuryeye bile verilse min. kâr kalacak şekilde hesaplanır; zararına atama engellenir |
| Siparişler | Sözleşmeli dükkanlardan gelir; pahalı ücrette müşteri vazgeçer, kurye bulunmazsa iptal olur |
| Dükkanlar | Burger Queen, Döner Kral, Starbocks, Market 24, McDanold's, Pizza Hat, KFG, Sushi Samurai |
| Olaylar | Eksik ürün, hasarlı sipariş, kaza, zam talebi, istifa, yavaş dükkan, viral yorum, komisyon teklifi |
| Ekipman | Kış montu, yağmurluk, yaz tişörtü, termal çanta, kask, powerbank; eksikse hız, hastalık, ıslak paket, kapanan telefon, kaza süresi olarak hissedilir |
| Hava ve mevsim | Mevsime göre hava; kötü havada talep artar, açık araçlar yavaşlar |
| Kampanyalar | Hafta sonu sabah/öğle boostu, hafta içi rastgele boostlar (%), haftalık hedef ödülü; hepsi şirketin kârından |
| Vergi | Kârın bir kısmı vergi kasasına ayrılır; yıl sonunda öde ya da cezalı ertele |
| Büyüme | 3 bölge, sözleşmeler, işe alım, seviye atlayan kuryeler |

## Dosya yapısı
```
KuryeTycoon/Scripts/
  Core/      GameBootstrap (sahneye eklenen tek script), GameClock, CompanyProfile, SpriteFactory
  Data/      GameData (araçlar, dükkanlar, ekipmanlar, isimler — dengeyi buradan ayarla)
  World/     CityMap (ızgara şehir, yol bulma, bölgeler), WeatherSystem
  Couriers/  Courier (hareket, yorgunluk, seviye), CourierManager (işe alım, moral, kaza, zam)
  Orders/    Order, OrderManager (sipariş üretme, atama, puan)
  Economy/   EconomyManager (fiyat formülü, kasa, vergi kasası, yıl sonu vergi, iflas)
  Events/    EventManager (olay kartları, haber akışı)
  UI/        GameUI (kurulum ekranı, üst bar, sekmeler)
```

## Denge ayarları nerede
- **Başlangıç parası / kuryeleri**: `Oyun` objesindeki GameBootstrap → Inspector.
- **Gün uzunluğu, yıl kaç gün**: Play sırasında Hierarchy → Oyun → Saat.
- **Vergi oranı, kira, bonuslar**: Hierarchy → Oyun → Ekonomi (veya oyun içi Ayarlar sekmesi).
- **Araç ücretleri, dükkan komisyonları, ekipman fiyatları**: `Data/GameData.cs`.

## Sonraki adımlar (önerilen sıra)
1. Kayıt sistemi (JSON save/load).
2. Gerçek UI (Canvas + TextMeshPro) ile IMGUI'yi değiştirmek.
3. Kurye ve dükkan sprite'ları / 3D low-poly şehir.
4. Rakip kurye şirketi (AI) ve fiyat savaşı.
5. Abonelik sistemi ve reklam kampanyaları.
