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
3. Kısayollar: **Boşluk** = duraklat, **1–5** = 1x / 2x / 4x / 8x / 16x hız. Takvim gerçek: şirket 1 Mart Pazartesi kurulur, vergi yılı 31 Aralık'ta biter.
4. Game penceresi küçükse üstteki **Free Aspect**'i **Full HD (1920x1080)** yap.

## Oyunda neler var
| Sistem | Ne yapar |
|---|---|
| Hikaye: aile borcu | Şirketi annenle babanın birikimi olan 1 000 000 Ft ile kurarsın (başlangıç kasası). Nisan–Temmuz her ay sonunda 250 000 Ft taksit; erken/kısmi ödenebilir. Taksit kaçırırsan aile güveni düşer ve borç sonrakine eklenir; 3 kez kaçırırsan şirket kapanır (kötü son). Borcu bitirirsen iyi son ve serbest mod. Aileden mesajlar ve hatırlatmalar gelir |
| Şirket kurulumu | İsim, ana/ikincil renk; kuryeler ve merkez ofis bu renklerde |
| Kuryeler | Bisiklet, e-scooter, motosiklet, araba; hız, kapasite ve ücret farklı |
| İşe alım | Başvurular zamanla gelir, kabul/red edersin; işe alım ve çıkarma ücretsiz |
| Serbest kuryeler | Her kuryenin asıl mesleği var: tam zamanlı (her gün çalışabilir, zorunlu izin yok), ofis çalışanı (17:00 sonrası), sadece hafta sonu, öğrenci (sınav haftalarında çalışmaz, tatilde çok çalışır), vardiyalı işçi (haftalık değişen vardiya), emekli (sabahları). Yorgunluğuna ve moraline göre kendisi çevrimiçi/çevrimdışı olur. Boostlar çevrimiçi olmayı teşvik eder |
| Kuryenin kendi parası | Kazancının %30'unu biriktirir. Araç yükseltmek zorunlu değil: aracından memnun olanlar hiç değiştirmez, istediği bir araç olan kurye parası yetince onu kendi cebinden alır. Araç arızalanırsa yedek aracıyla çalışır. İsterse platform üzerinden kendi parasıyla sigorta yaptırır; şirket kaza masrafı ödemez |
| Moral ve yorgunluk | Aynı anda kaç paket alacağını ve sipariş reddetme ihtimalini belirler |
| Ödeme | Paket başı sabit + km başı (bisiklet 175 → araba 286 Ft) + hava/gece/yoğun saat bonusu × bonus çarpanı |
| Kâr kuralı | Teslimat ücreti, en pahalı kuryeye bile verilse min. kâr kalacak şekilde hesaplanır; zararına atama engellenir |
| Siparişler | Sözleşmeli dükkanlardan gelir; pahalı ücrette müşteri vazgeçer, kurye bulunmazsa iptal olur |
| Dükkanlar | Burger Queen, Döner Kral, Starbocks, Szpar Express, Market 24, McDanold's, Pizza Hat, InterSzpar, KFG, Sushi Samurai |
| Sipariş ağırlığı | Bisiklet, scooter, motor ve araba 20 kg'a kadar; 20 kg ve üstü "ağır sipariş", sadece araba. Taşıyacak kurye yoksa sipariş alınamaz |
| Olaylar | Eksik ürün, hasarlı sipariş, kaza (sigortalıysa sigorta öder), araç arızası, kuryenin yeni araç alması, hastalık, kuryenin ayrılması, yavaş dükkan, viral yorum, komisyon teklifi |
| Ekipman | Kış montu, yağmurluk, yaz tişörtü, termal çanta, kask, powerbank; eksikse hız, hastalık, ıslak paket, kapanan telefon, kaza süresi olarak hissedilir |
| Hava ve mevsim | Mevsime göre hava; kötü havada talep artar, açık araçlar yavaşlar |
| Kampanyalar | Hafta sonu sabah/öğle boostu, hafta içi rastgele boostlar (%), haftalık ve aylık hedef ödülü (örn. ayda 450+ paket → 50 000 Ft); hepsi şirketin kârından |
| Başlangıç ekipmanı | Her yeni kuryeye termal çanta + şirket tişörtü verilir; şirkete maliyeti kit başı 5 000 Ft. Kurye 30 000 Ft kazanınca 20 000 Ft depozito kesilir (gelir değil, iade borcu). Ayrılırken ekipmanı iade ederse depozito geri ödenir; iade etmezse depozito şirkette kalır |
| Destek ekibi | Maaşlı destek personeli (aylık maaş, ay sonunda çalışılan gün kadar ödenir). Sabit vardiya: sabah 06–14, öğle 14–22, gece 22–06; Pzt–Cum veya Çar–Paz. Vardiyadayken eksik/hasarlı sipariş şikayetlerini pazarlıkla ucuza kendisi çözer, kurye bulunamayan siparişte müşteriyi arayıp ek süre alır, kurye moralini artırır |
| Hijyen | Her teslimatta çanta kirlenir (sıcak yemek, ağır paket, yağmur, dikkatsizlik hızlandırır). Titiz kuryeler kendisi temizler. %60 altında müşteri şikayeti, düşük puan ve itibar kaybı. Temizlik hatırlatması, şirkete temizletme (1 500 Ft) veya toplu temizlik; 400 teslimattan sonra çanta yıpranır, yenisi gerekir |
| Adres sorunları | Siparişlerin ~%4'ünde adres yanlış işaretlenmiş, ~%3'ünde müşteri evde yok. Kurye kapıda 10 dk uğraşır. Yanlış adreste vardiyadaki destek müşteriyi arayıp doğru adresi bulur (itibar artar), yoksa kurye kendisi dener; evde yoksa müşteri gelebilir ya da destek arar. Gecikme müşterinin hatası sayılır. Hepsi tutmazsa (çok nadir) sipariş kuryede kalır: müşteri öder, yemek kuryenin |
| Teklif ve iptal | Kurye teklifte tahmini kazancını ve süresini görür; seçiciliğine göre az kazandıran/uzak işleri reddeder, sipariş başka kuryeyi bekler (bonus/boost kabul ettirmeyi kolaylaştırır). Dükkanda bazen sıra olur; uzun bekleyen kurye teslim almayı iptal edebilir (kurye başı ayda en fazla 10), sipariş bekleyenlere döner |
| Geliştirmeler | Kalıcı şirket geliştirmeleri (Taksimetre'deki garaj geliştirmeleri gibi). Ofis büyütme kurye kapasitesini belirler (4 → 8 → 14 → 22 → 32) ama kirayı da artırır. Ayrıca: akıllı dispatcher, kurye uygulaması 2.0, dinlenme alanı, bakım/şarj istasyonu, hijyen istasyonu, marka bilinirliği |
| Bahşiş | Müşteriler bazen bahşiş verir (cömert müşteri, iyi puan, kötü hava, gece, ağır paket ihtimali artırır); bahşişin tamamı kuryenin, şirket kasasına girmez |
| Vergi | Yıl sonunda net kâr üzerinden %15 (öde ya da cezalı ertele). Yıl içinde habersiz vergi/harçlar: yerel işletme vergisi, KDV farkı (itiraz edilebilir), vergi denetimi (muhasebeciye danışılabilir), belediye harcı, iş güvenliği fonu; bazen vergi iadesi |
| Büyüme | 3 bölge, sözleşmeler, işe alım, seviye atlayan kuryeler |

## Dosya yapısı
```
KuryeTycoon/Scripts/
  Core/      GameBootstrap (sahneye eklenen tek script), GameClock, CompanyProfile, SpriteFactory
  Data/      GameData (araçlar, dükkanlar, ekipmanlar, isimler — dengeyi buradan ayarla)
  World/     CityMap (ızgara şehir, yol bulma, bölgeler), WeatherSystem
  Couriers/  Courier (hareket, yorgunluk, seviye), CourierManager (işe alım, moral, kaza, zam)
  Orders/    Order, OrderManager (sipariş üretme, atama, puan)
  Economy/   UpgradeManager (kalıcı geliştirmeler, ofis kapasitesi), CampaignManager (boost, hedefler)
             EconomyManager (fiyat formülü, kasa, sürpriz vergiler, yıl sonu vergi, iflas)
  Events/    EventManager (olay kartları, haber akışı)
  Staff/     SupportManager (maaşlı destek ekibi, vardiyalar)
  Story/     FamilyLoan (hikaye, aile borcu, taksitler, sonlar)
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
