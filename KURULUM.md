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

## 3. Sahne
Sahne kurmana gerek yok: Play'e basınca oyun kendiliğinden başlar (boş sahnede de).
Ayarları Inspector'dan değiştirmek istersen boş bir GameObject'e `GameBootstrap` ekleyebilirsin.

## 4. Oyna
1. **Play**'e bas.
2. Şirket adını yaz, ana ve ikincil rengini seç → **Şirketi Kur ve Başla**.
3. Kısayollar: **Boşluk** = duraklat, **1–8** = 1x / 2x / 4x / 8x / 16x / 32x / 64x / 128x hız. Takvim gerçek: şirket 1 Mart Pazartesi kurulur, vergi yılı 31 Aralık'ta biter.
4. Game penceresi küçükse üstteki **Free Aspect**'i **Full HD (1920x1080)** yap.

## 5. (İsteğe bağlı) 3D kurye karakterleri

Kuryeler varsayılan olarak daire çizilir. Asset Store'daki **City People FREE Samples** (ücretsiz) paketiyle 3D karakter olarak görünebilirler.
Modeller lisans gereği **repoya konmaz**; her bilgisayarda (Windows, Mac, arkadaşların) bir kez kurulur:

1. Tarayıcıda paket sayfasında **Add to My Assets**.
2. Unity'de **Window > Package Manager** → sol üstte **My Assets** → *City People FREE Samples* → **Download** → **Import** → açılan pencerede **Import**.
3. Unity üst menüsünden **Kurye Tycoon > Karakterleri Kur (City People)**. Prefablar `Assets/Resources/KuryeKarakterleri` klasörüne kopyalanır.
4. Play: çevrimiçi kuryeler 3D karakter olarak yürür, gittiği yöne döner; çevrimdışı olanlar soluk daire olarak kalır.
5. Yürüme animasyonu: **Kurye Tycoon > Karakter Animasyonlarını Kur (yürüme)** (3. adım bunu otomatik de yapar). Paketteki walk / idle kliplerini bulup döngüye alır; kuryeler yürürken walk, dururken idle oynatır.

Sorun olursa:
- **Pembe karakter:** Window > Rendering > Render Pipeline Converter → *Material Upgrade* işaretle → Initialize And Convert.
- **Siyah/karanlık karakter:** oyunda Ayarlar > Kurye karakterleri > **Düz renk**.
- **Menü "bulunamadı" diyorsa:** Project penceresinde paketin karakter prefablarının olduğu klasörü seç → **Kurye Tycoon > Seçili Klasördeki Karakterleri Kur**.
- Kapatmak için Ayarlar > **Kapalı (daire)**; tamamen kaldırmak için **Kurye Tycoon > Karakterleri Kaldır**.

## 6. (İsteğe bağlı) 3D şehir paketi

Yakınlaştırınca yükselen şehir varsayılan olarak basit kutu binalardır. Asset Store'daki ücretsiz **SimplePoly City - Low Poly Assets** paketiyle gerçek binalar ve ağaçlar görünür.
Bu modeller de lisans gereği repoya konmaz; her bilgisayarda bir kez:

1. Paket sayfasında **Add to My Assets**.
2. Unity'de **Window > Package Manager** → **My Assets** → *SimplePoly City* → **Download** → **Import** → **Import**.
3. Malzemeler kurulum menüsünde otomatik URP'ye çevrilir (paket Unity 5 için yapılmış). Pembe kalan olursa paketin klasörünü seç → **Kurye Tycoon > Seçili Klasördeki Malzemeleri URP'ye Çevir**.
4. **Kurye Tycoon > Şehir Paketini Kur (SimplePoly City)**. Prefablar isimlerine göre `Assets/Resources/SehirPaketi/Binalar`, `/Agaclar`, `/Yollar` klasörlerine ayrılır.
5. Yeni oyun başlat ve yakınlaş.

Ayarlar:
- Yanlış klasöre düşen prefabı (ör. Binalar'da araba) Project penceresinde silebilir ya da taşıyabilirsin.
- Binaların arkası yola bakıyorsa oyunda **Ayarlar > 3D şehir > Bina cephesi düzeltme**.
- Yollar ve arabalar şimdilik paketten değil; yollar düz harita olarak kalır.

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
| Kâr kuralı | Teslimat ücreti = taban + km ücreti × km; siparişi büyük ihtimalle götürecek kuryenin (o ağırlığı taşıyabilen, müsait en ucuz kurye) ücretini + min. kârı karşılamıyorsa fark 'ek' olarak eklenir. Atamada sipariş başı en az 150 Ft kâr şartı (Ayarlar'dan değiştirilir); 15 dk'dan uzun bekleyen siparişte şart 0'a iner, zararına iş yine yapılmaz. Sipariş kartında ücret dökümü görünür |
| Siparişler | Sözleşmeli dükkanlardan gelir; pahalı ücrette müşteri vazgeçer, kurye bulunmazsa iptal olur |
| Dükkanlar | Burger Queen, Döner Kral, Starbocks, Szpar Express, Market 24, McDanold's, Pizza Hat, InterSzpar, KFG, Sushi Samurai |
| Talep saatleri | Hafta içi yemek: 10–13 ofis / home office öğle zirvesi, 16'dan sonra iş çıkışı, 18–21 akşam yemeği zirvesi. Hafta içi market: 16–20 iş çıkışı (evde yemek yapacaklar). Hafta sonu: sabahlar sakin; yemek öğle ve akşam, market öğleden önce/sonra. Gece yarısından sonra çok az sipariş |
| Sipariş ağırlığı | Bisiklet, scooter, motor ve araba 20 kg'a kadar; 20 kg ve üstü "ağır sipariş", sadece araba. Taşıyacak kurye yoksa sipariş alınamaz |
| Menüler ve ürünler | Her dükkanın gerçekçi, ayrıntılı menüsü (`Scripts/Data/Menus.cs`, forint, ~575 ürün): fast food'da menüler, tekli burgerler, yan ürünler, içecekler, tatlılar ve soslar; dönerci, pizzacı (12 çeşit × 3 boy), sushi, balık, büfe, gurme restoran; kahvecide boyuna göre kahveler, fırın ürünleri, sandviçler; marketlerde 80+ ürün (gece marketi biraz pahalı, hipermarket biraz ucuz ve toplu ürünlü, organik market). Sipariş gerçek sepet gibi oluşur: kişi başı menü ya da ana yemek + yan + içecek, ara sıra tatlı ve sos; kahvecide kahve + fırın; markette adetli çok ürün. Haritada dükkan kartında **Menüyü göster** ile bütün ürünler ve fiyatlar görünür |
| Teslimat sorunları | Nadir ve gerçekçi: **dükkanda eksik ürün** (market ~%1,5, restoran ~%0,7 teslimat): dükkan müşteriye bildirir ve ücretini kendisi iade eder, şirkete ve kuryeye maliyeti yok (sadece bildirim). **Kurye kaynaklı sorun** (döküldü, ezildi, ıslandı, poşet unutuldu; iade sadece etkilenen ürünün fiyatıdır, menüde ise menünün etkilenen kısmı; dikkatli kuryede ~%0,5, dikkatsizde ~%1,5; yağmurluksuz yağmurda ×1,6, termal çantayla ×0,6): müşterilerin ~%60'ı desteğe yazar, iade kuryenin hesabından düşülür (Wolt kuralı; destek ekibi varsa otomatik, yoksa karar kartı: kuryeden düş / şirket karşılasın / %50 kupon / reddet); yazmayan müşteri sadece düşük puan verir. Yanlış adres ~%2,5, müşteri evde yok ~%2 |
| Olaylar | Kaza (sigortalıysa sigorta öder), araç arızası, kuryenin yeni araç alması, hastalık, kuryenin ayrılması, yavaş dükkan, viral yorum, komisyon teklifi |
| Ekipman | Kış montu, yağmurluk, yaz tişörtü, termal çanta, kask, powerbank; eksikse hız, hastalık, ıslak paket, kapanan telefon, kaza süresi olarak hissedilir |
| Ekipman teklifi | Kuryeler serbest çalıştığı için ekipman zorla verilmez, teklif edilir; kurye kabul ya da ret eder. Ödeme şekli Ekipman sekmesinden: şirket öder / yarı yarıya / kurye öder (payı hesabından kesilir, kurye kesintisi olarak gelire yazılır). Kabul ihtimali ödeme şekline, mevsime, öğrenci / hafta sonu kuryesi olmasına, yeni olmasına, hesabındaki paraya, moraline ve (kask için) dikkatine bağlı; butonda yaklaşık yüzde görünür. Reddeden kuryeye aynı ekipman 1 gün sorulamaz. Arabalı kuryelere toplu teklifte giyecek ve kask sorulmaz |
| Operasyon sorumlusu | Destek sekmesinden işe alınan maaşlı tek kişi (yeteneğe göre 100–160 bin Ft/ay). Her sabah 09:00'da kurallara göre eksik ekipmanı teklif eder (giyecekleri sadece mevsiminde), yıpranan çantaları yeniler, gün içinde kirli çantası olanları uyarır; reddedene 3 gün sonra tekrar sorar. Kurallar: hangi ekipmanlar, yeni kuryeye ne zaman (hemen / 10 / 30 / 60 teslimat sonra), aylık ekipman bütçesi. Yetenek: ikna (+%0–15), işleri erteleme, toplu alım indirimi (%10). Ne yaptığı gün sonu raporunda yazar |
| Kayıt sistemi | Tek kayıt yuvası (`persistentDataPath/kayit.json`, bir önceki `kayit_onceki.json`). Her gece gün sonundan sonra, açık ya da sırada bekleyen olay kartı kalmayınca otomatik kaydeder (bekleyen bir karar kayıtla kaybolmasın diye). Esc menüsünden elle kaydet / ana menü / kaydet ve çık. Kurulum ekranında "Kayıtlı oyuna devam et". Harita tohumla üretilir, aynı harita ve 3D şehir geri gelir. Bilerek kaydedilmeyenler: yoldaki ve bekleyen siparişler (yüklemede iptal), kuryelerin yoldaki konumu (hepsi merkez ofiste başlar), iş başvuruları ve adaylar, hava (yeniden belirlenir). Denge sabitleri kayda yazılmaz, koddaki güncel değer kullanılır |
| İstatistik ve grafikler | Her gece günün özeti saklanır. Finans sekmesinde son 30 günün net kâr, teslimat ve itibar grafikleri |
| Denge kaydı (CSV) | Her gün sonunda bir satır: teslimat, iptal, kaçan, gelir, gider, kâr, kurye ödemesi, ekipman, maaş, kasa, serbest kasa, itibar, kurye, dükkan, bölge. `persistentDataPath/DengeKaydi`, ayraç `;`. Ayarlar'da aç/kapa ve "Klasörü aç" |
| Kilometre taşları | 15 hedef (1 / 100 / 1 000 / 10 000 teslimat, 5 / 10 / 20 kurye, bölgeler, itibar 4,0 ve 4,5, günde 100 000 Ft kâr, şehrin lideri, kurumsal paket, aile borcu). Ulaşınca kart; Finans'ta ilerlemeleriyle liste. Şimdilik oyuna etkisi yok |
| Onay ekranları | İşe alma / çıkarma (kurye, destek, operasyon), bölge açma, münhasırlık teklifi ve 50 000 Ft üstü harcamalar (geliştirme, sözleşme, toplu ekipman teklifi, aile borcuna erken ödeme) onay sorar. Açıkken oyun durur; Esc = Vazgeç, Enter = Evet. Eşik: `GameUI.ConfirmThreshold` |
| Araç filtresi | Kuryeler ve Gelen başvurular listesinde Hepsi / Bisiklet / E-scooter / Motosiklet / Araba (sayılarıyla) |
| Başlangıç rehberi | Yeni oyunda hoş geldin kartından sonra 8 adımlık kısa rehber: harita, Siparişler, Kuryeler, İşe Alım, Dükkanlar, Hedefler, hız, son notlar. Hedef simge sol çubukta yanıp söner, pencere açılınca sonraki adıma geçer. "Atla" / "Rehberi kapat"; Ayarlar > Arayüz'den yeniden başlatılır. Adım kayda yazılır (`Guide.cs`, `Tutorial.cs`) |
| Kademeli menüler | Gelişim 2. günde (ya da kurye hesapları dolunca), Kampanya 3. günde (ya da boost hedefi gelince), Destek 5. günde (ya da 6 kurye olunca) açılır; o zamana kadar simge soluk ve "Kilitli", üstüne gelince ne zaman açılacağı yazar. Açılınca bildirim gelir, kalıcıdır, kayda yazılır. Destek açılana kadar İşe Alım'da destek adayları görünmez. Arkadaki sistemler kilitliyken de çalışır. Ayarlar > Arayüz > "Bütün menüleri baştan aç" hepsini açar. Günler `Guide` sınıfının başında |
| Kısa açıklamalar | Pencerelerin başındaki uzun paragraflar tek cümleye indi; geri kalanı "Ayrıntı" ile açılır (Ayarlar > Arayüz'den hep açık yapılabilir). Hoş geldin kartı kısaldı |
| Pencere haritayı kapatmaz | Pencere açılınca harita pencerenin sağına kayar (yumuşak geçiş). Ayarlar > Arayüz'den kapatılırsa eskisi gibi haritanın üstünde yüzer |
| Hava ve mevsim | Mevsime göre hava. Kötü havada müşteriler dışarı çıkmaz, sipariş artar (soğuk +%20, yağmur +%35, fırtına +%45, kar +%50); müşteriye kötü hava ücreti eklenir (100–300 Ft). Açık araçlı kuryeler yavaşlar ve çevrimiçi olmaya daha az isteklidir (yağmurluk/kış montu azaltır). Kampanyalar'da otomatik kötü hava boostu (varsayılan soğuk %5, yağmur %10, kar %15, fırtına %20) |
| Meslekler | Tam zamanlı (10–15 ve 17–22, arada mola), ofis çalışanı (hafta içi 17–23), üniversite öğrencisi (her öğrencinin kendi ders programı, her dönem değişir; sınav ve tatil dönemleri), hafta sonu kuryesi, vardiyalı işçi (haftalık sabah/akşam), emekli (08–15), ebeveyn (çocuklar okuldayken 09–15), gece kuryesi (20–03) |
| Kampanyalar | Hafta sonu sabah/öğle boostu, hafta içi rastgele boostlar (%), haftalık (200 paket → 25 000 Ft) ve aylık (700 paket → 50 000 Ft) hedef ödülü; hepsi şirketin kârından |
| Başlangıç ekipmanı | Her yeni kuryeye termal çanta + şirket tişörtü verilir; şirkete maliyeti kit başı 5 000 Ft. Kurye 30 000 Ft kazanınca 20 000 Ft depozito kesilir (gelir değil, iade borcu). Ayrılırken ekipmanı iade ederse depozito geri ödenir; iade etmezse depozito şirkette kalır |
| Destek ekibi | Maaşlı destek personeli (aylık maaş, ay sonunda çalışılan gün kadar ödenir). Sabit vardiya: sabah 06–14, öğle 14–22, gece 22–06; Pzt–Cum veya Çar–Paz. Vardiyadayken eksik/hasarlı sipariş şikayetlerini pazarlıkla ucuza kendisi çözer, kurye bulunamayan siparişte müşteriyi arayıp ek süre alır, kurye moralini artırır |
| Hijyen | Her teslimatta çanta biraz kirlenir (sıcak yemek, ağır paket, yağmur, dikkatsizlik hızlandırır). Temizlik kuryenin kendi sorumluluğu: her gece temizler, gün içinde de kendi eşiğine göre temizler (titiz kurye çanta az kirlenince, dağınık kurye iyice kirlenince); şirkete maliyeti yok. %45 altında koku fark edilir: düşük puan, nadiren şikayet (çok kirli çantada teslimat başına en fazla ~%4, paketli ürünlerde yarısı). Şikayet kendiliğinden işlenir: kurye uyarılır, paketi bitince temizler, bildirim gelir (oyun durmaz). Aynı kurye haftada 3. şikayeti alırsa karar kartı: ciddi uyarı / yeni çanta (yıpranmışsa) / hesabı 1 gün dondur / görmezden gel. Gün içinde kirlenenleri ücretsiz uyarabilirsin; 400 teslimattan sonra çanta yıpranır, yenisi gerekir |
| Şehir düzeni | Sabit, elle tasarlanmış 49 × 33 karelik şehir, beş bölge: **Merkez Mahalle** (başlangıç, ortada), **Doğu İlçesi** (150 bin), **Sanayi & Kampüs** (400 bin), **Batı Liman** (750 bin: Tisza Limanı, iskeleler, Otogar), **Yeşil Tepe** (1,2 milyon: kuzeyde bütün şehir boyunca villalar ve korular). Ortadan Tisza nehri geçer, karşıya sadece 5 köprüden (x = 8, 16, 24, 32, 44) geçilir. Merkez ofis Kossuth Meydanı'nda (çeşmeli). Bloklar: Eski Şehir, Kule Bloklar, Bahçeli Evler, çarşı, Belediye Parkı (anıtlı), Nehir Parkı, Tepe AVM, Nyír Plaza AVM, Otogar (dükkanlar AVM/otogar içinde olabilir, müşteri gelmez), Kampüs, Sanayi & Lojistik (depolar ve tren yolu). Yeni bölgelerin dükkanları: Fish & Ships, Otogar Büfé (Batı Liman), Villa Gourmet, BioSzpar (Yeşil Tepe). Kilitli bölgeler gri-koyu görünür. Şehrin dışı kır: çayır, tarlalar, ormanlar, çiftlikler; nehir ve bazı yollar şehrin dışına devam eder (süs, rota yok). Nehirde tekneler, rayda yük treni dolaşır. Yer adları (Ayarlar > Harita'dan kapatılabilir) ve **Lejant** |
| Harita kamerası | Varsayılan **açılı görünüm**: harita her zaman yukarıdan yaklaşık 38° eğik görülür (kuzey yukarıda, sadece eğik; caddeler dikey, sokaklar yatay kalır), 3D binalar hep ayakta. Kamera perspektif (dar 30° görüş açısı: uzak taraf hafifçe küçülür, maket hissi; Ayarlar > Harita'dan ortografiğe çevrilebilir, düz harita modunda hep ortografik). Yakınlaştıkça eğim 50°'ye çıkar, kuryeler daireden 3D karaktere geçer. Q / E veya sağ tuşla sürükle = 45° döndür, N = kuzeye dön, uzaklaşınca dönüş kendiliğinden kuzeye döner. Sol sürükle / WASD / oklar = kaydır, tekerlek veya + / − = yakınlaş, 0 = tüm harita. **M** veya zoom düğmelerinin yanındaki **Düz harita** = eski düz kuş bakışı 2D harita (rota ve dükkan bakmak için; yakınlaşınca yine eğilir), tekrar basınca açılı görünüme yumuşakça döner; seçim hatırlanır |
| Sipariş detayı ve adres | Her siparişin menüden gerçek ürünleri var (sepet tutarı ürünlerin toplamı), müşteri adı ve bazen notu. Adresler tutarlı: dikey yollar Cadde, yatay yollar Sokak; bina numarası yol boyunca (bir taraf tek, karşı taraf çift); her binanın kalıcı kat sayısı blok türüne göre (kule bloklar 7–12, eski şehir 3–5, çarşı 3–6, kampüs 2–4, bahçeli ev ve sanayi 1–2), 5+ katta asansör, 7+ katta A/B blok, daire no. 3D binaların yüksekliği kat sayısıyla uyumlu. Kurye adrese varınca binaya girer: zemin katta hemen, asansörle 1–2 dk bekleme + kat başı ~0,25 dk, merdivenle kat başı ~0,9 dk (ağır pakette daha uzun, yorar); asansör bazen arızalı. Söz verilen süre ve kuryenin teklif tahmini katı hesaba katar |
| Dükkan sözleşmeleri | Her dükkanın memnuniyeti var (0–100): zamanında teslimat artırır (+0,6), 5 dakikaya kadar gecikme sorun sayılmaz; daha fazla gecikme (en fazla −2,5), soğuk yemek (−1), iptal (−1,5) ve müşterinin desteğe yazdığı kurye hatası (−1) düşürür. Bir günde teslimatlardan en fazla 12 puan kaybedilir; memnuniyet her gün 60'a doğru toparlanır (aradaki farkın %8'i). 35 altında bildirim kartıyla uyarır (oyunu durdurmaz), 15 altında günde %20 ihtimalle sözleşmeyi bozar. Dükkandaki eksik ürün memnuniyeti etkilemez (dükkanın kendi hatası). Sözleşmeler 6 ayda bir yenilenir; memnuniyet düşükse rakip teklif verir (eşitle / münhasırlık öner / ısrar et). Ayda bir yeniden pazarlık: komisyon +2 puan (şans memnuniyet, itibar ve hacme bağlı; başarısızsa dükkan küser), en fazla +6 puan. Münhasırlık: rakip çıkar, siparişler +%15, komisyon −3 puan. Ayda 400+ sipariş veren dükkan bazen indirim ister. Kaybedilen dükkana 30 gün sonra yeniden teklif verilebilir |
| Rakip şirket | Yapay zekâ kontrolündeki Turbo Paket: kendi itibarı, kurye sayısı, parası ve dükkanları var, her hafta büyümeye çalışır. Aynı dükkanla çalışıyorsanız siparişler itibar ve kurye sayısına göre paylaşılır. Senin dükkanlarınla da anlaşabilir, yenilemelerde teklif verir, ara sıra 5 günlük fiyat savaşı açar (müşteriler fiyata duyarlı), moralsiz kuryelerini ayartır. Dükkanlar sekmesinde ve haftalık raporda durumu ve pazar payın görünür. Kuryeleri haritada mor halkalı olarak dolaşır (rakibin dükkanlarından alıp binalara teslim eder, sayısı rakibin kurye sayısına ve saate göre); yakınlaşınca "Turbo" etiketli 3D karakter olur |
| Sigorta ortaklığı | Finans sekmesinden anlaşmalı sigortacı seçilir (Anlaşma yok / Ekonomik 1 800 Ft, %10 / Standart 2 500 Ft, %18 / Plus 3 400 Ft, %28 ve kazada 1 gün erken dönüş). Sigorta zorunlu değil; yaptırmak isteyen kurye anlaşma üzerinden yaptırır, haftalık primden şirket komisyon alır. Ucuz prim daha çok kurye çeker, pahalı prim daha az. Dışarıdan sigortalı kuryeler zamanla anlaşmaya geçebilir. Plan 30 günde bir değiştirilebilir; komisyon gün sonu raporunda ayrı yazar |
| Arayüz: üst bar | Koyu, modern üst bar; altında şirket renginde ince şerit. Solda şirket logosu ve adı; ortada simgeli bilgi kartları: Kasa (cüzdan), Serbest (para; eksiye düşerse kırmızı), İtibar (yıldız, x / 5), Çevrimiçi kurye (kişi), Saat (büyük) ve gün/tarih, Hava (hava durumuna göre güneş/bulut/yağmur/kar/fırtına simgesi, mevsim). Uyarılar renkli hap: Boost / Hava boostu (turuncu, şimşek), Vergi borcu (kırmızı, ünlem), Aile borcu (kalp). Sağda oynat/duraklat düğmesi (duraklatınca turuncu) ve 1×–128× hız segmentleri. Ekran darsa sığmayan kartlar gizlenir |
| Arayüz: simge çubuğu | Solda simge çubuğu: Siparişler, Kuryeler, İşe Alım, Dükkanlar, Ekipman, Kampanya, Destek, Gelişim, Finans ve en altta Ayarlar. Simgeye tıklayınca haritanın üstünde yüzen bir pencere açılır (başlık, simge, kapat düğmesi); aynı simgeye tekrar tıklamak, X ya da Esc kapatır (Esc pencere kapalıyken oyun menüsünü açar). Harita artık ekranın tamamını kullanır. Bekleyen sipariş ve iş başvurusu sayısı simgenin köşesinde kırmızı rozet olarak görünür. Simgeler dosya gerektirmez, çalışma anında çizilir (`Scripts/UI/Icons.cs`) |
| Haritada bina ve dükkan kartı | Haritada dükkana / markete ya da müşteri binasına tıklayınca sağda bilgi kartı açılır (3D'de çatıya tıklamak da olur; önde olan bina seçilir, seçili binanın üstünde işaret çıkar). Dükkan: tür (restoran/kafe/market), sözleşme durumu, adres ve bölge, bekleyen / yolda / bu ay / toplam sipariş, komisyon, hazırlık, sepet ve ağırlık aralığı, memnuniyet, sözleşme imzalama, pazarlık ve münhasırlık düğmeleri, şu anki siparişler (müşteri, adres, ürünler, durum, kalan süre). Müşteri binası: adres, bina türü ve bölge, kat sayısı, asansör, blok ve daire sayısı, aktif siparişler (dükkan, müşteri, kat, kurye durumu, kalan süre) ve son 6 sipariş (puan, bahşiş, soğuk geldi mi, iptal). Kart X, Esc ya da boş yere tıklayınca kapanır; kuryeye tıklamak kurye telefonunu açar |
| Oyunun başı | Yeni oyunda başlangıç kuryeleri ilk sabah işbaşında başlar. İptaller ilk günden itibarı düşürür (gerçekçi) |
| Müşteri mesafesi | Siparişlerin çoğu (~%80) dükkanın çevresindeki 16 karelik (≈3,2 km) bölgeden gelir, yakın binalar daha sık; ~%20'si şehrin açık bölgelerinin herhangi bir yerinden (uzak sipariş). Uzak siparişte teslimat ücreti, kurye kazancı (km başı) ve söz verilen süre mesafeyle artar (OrderManager > Delivery Radius, Far Order Chance) |
| Harita etiketleri | Etiketler çakışmaz: önem sırasına göre yerleşir (kuryeler, merkez ofis, dükkanlar, rakipler, yer adları), çakışan etiket biraz kaydırılır, çakışan yer adı gösterilmez. Uzaktayken sözleşmesiz dükkanların etiketi gizlenir |
| Trafik | Yollarda arabalar ve otobüsler sağdan gider, kavşaklarda döner; yayalar kaldırımlarda yürür (yakınlaşınca görünür). Şehir paketi kuruluysa paketin araç ve insan modelleri kullanılır (Kurye Tycoon > Şehir Paketini Kur artık araçları Resources/SehirPaketi/Araclar, insanları Insanlar klasörüne kopyalar; paket eskiden kurulduysa menüyü bir kez daha çalıştır), yoksa renkli kutular. Araçların burnu ters giderse Ayarlar > Harita > Araç yönü düzeltme. Süs, oyuna etkisi yok; oyun duraklayınca durur. Ayarlar > Harita > Trafik ile kapatılabilir |
| Bildirim kartları | Önemli olaylar (iptal, kaza, istifa, şikayet, boost, yeni bölge, sözleşme, yeni kurye, hedef tamamlandı) haritanın üst ortasında 5–6 saniye renkli kart olarak görünür (kırmızı: dikkat, turuncu: boost, yeşil: harika). Aynı türden art arda gelenler tek kartta toplanır (+3 gibi). Karta tıklayınca kapanır. Sıradan bilgiler alttaki akışta kalır |
| Haftalık hedefler | Her pazartesi şirket büyüklüğüne göre 3 hedef: her hafta bir teslimat hedefi + rastgele ikisi (4,5★ ve üstü puan, iptalsiz gün, soğumadan sıcak yemek, zamanında teslim, boost saatinde teslimat, yeni sözleşme, yeni kurye). Ödül hemen kasaya girer (bazılarında itibar da). Sol üstte takip kutusu, simge çubuğunda **Hedefler** penceresi. Kayda yazılır |
| Kurye telefonu | Haritada kuryeye tıklayınca sağdan kurye uygulaması açılır. Ana ekran: kuryenin çevresinin haritası, rota, dükkan/müşteri noktaları ve "Teslim alma X dk" balonu; alt panelde son değerlendirdiği teklif (kazanç, dükkan, mesafe, süre, nakit, ağır sipariş, kabul/ret ve nedeni), aktif görev ve sipariş kartları ya da sipariş yoğunluğu, boost kartı ve çevrimiçi durumu. Menü: Profil (kişisel bilgiler, araçlar, ekipman ve teslim tarihleri, depozito), Analizler (haftalık teslimat, kabul ve tamamlama oranı), Finans, İstatistikler (bugün/hafta/ay: teslimat, km, kazanç), Ekstra kazan (aktif ve sıradaki boostlar, hedefler), Sigorta, Gelen kutusu (ödeme, boost, ödül, uyarı mesajları), Destek (kuryeye özel "neden sipariş gelmiyor" ve kurallar) |
| Adres sorunları | Siparişlerin ~%4'ünde adres yanlış işaretlenmiş, ~%3'ünde müşteri evde yok. Kurye kapıda 10 dk uğraşır. Yanlış adreste vardiyadaki destek müşteriyi arayıp doğru adresi bulur (itibar artar), yoksa kurye kendisi dener; evde yoksa müşteri gelebilir ya da destek arar. Gecikme müşterinin hatası sayılır. Hepsi tutmazsa (çok nadir) sipariş kuryede kalır: müşteri öder, yemek kuryenin |
| Toplu sipariş | Kurye aynı ya da yakın (4 kare) dükkanlardaki siparişleri tek turda toplar, çantadakileri en kısa rotayla dağıtır. Otomatik atama yakın siparişleri aynı kuryeye verir. Toplu siparişte müşteriye +5 dk tahmini süre eklenir; sıcak yemek termal çantayla 30 dk, çantasız 15 dk sonra soğur (−0,8 yıldız) |
| Teklif ve iptal | Kurye teklifte tahmini kazancını ve süresini görür; seçiciliğine göre az kazandıran/uzak işleri reddeder, sipariş başka kuryeyi bekler (bonus/boost kabul ettirmeyi kolaylaştırır). Dükkanda bazen sıra olur; uzun bekleyen kurye teslim almayı iptal edebilir (kurye başı ayda en fazla 10), sipariş bekleyenlere döner |
| Geliştirmeler | Kalıcı şirket geliştirmeleri (Taksimetre'deki garaj geliştirmeleri gibi). Kurye platformu paketi aktif kurye hesabı sayısını belirler (4 → 10 → 20 → 34 → 55), büyük paketin günlük sunucu ücreti de yüksek (0 → 8 000 Ft). Ayrıca: akıllı dispatcher, kurye uygulaması 2.0, dinlenme alanı, bakım/şarj istasyonu, hijyen istasyonu, marka bilinirliği. Uygulama 2.0, dinlenme alanı ve marka bilinirliği Doğu İlçesi açılınca alınabilir |
| Kurye ödemeleri (Wolt tipi) | Kazanç (ücret, boost, ödül, uygulama bahşişi) kuryenin hesabında birikir. Ayın 15'i ve son günü gece hesap kesilir, 1–2 iş günü içinde (hafta sonu hariç) kasadan ödenir. Kurye istediği gün hesabının %80'ini erken çekebilir (2–3 iş günü). Ekipman depozitosu hesaptan kesilir |
| Nakit ödeme | Yemekte ~%12, markette ~%25 sipariş nakit: müşteri yuvarlak banknot verir, kurye para üstü verir ve tutarı şirkete borçlanır (şirket dükkana kendi cebinden öder). Kuryedeki nakit en fazla 100 000 Ft; ay sonuna kadar keep cash (hesabın en fazla %80'iyle mahsup) veya offload (kartla ödeme) ile kapatılır. Kapatmayanın hesabı kilitlenir. Nakit bahşiş doğrudan kuryenin |
| Bahşiş | Müşteriler bazen bahşiş verir (cömert müşteri, iyi puan, kötü hava, gece, ağır paket ihtimali artırır); bahşişin tamamı kuryenin, şirket kasasına girmez |
| Gün sonu raporu | Her gece oyun durur ve günün özeti gelir: teslimat/iptal sayıları, gelir kalemleri, neye ne kadar ödendiği (kurye, boost, ödül, kira, ekipman, vergi, maaş...), teslimat kârı, net kâr, borç ödemesi, kasadaki değişim, günün kuryesi ve sıradaki taksit. Finans'tan tekrar açılır, Ayarlar'dan kapatılabilir |
| Haftalık rapor | Her Pazar gecesi haftanın özeti: teslimat (günlük ortalama), gelir, gider kalemleri, teslimat ve net kâr (geçen haftayla karşılaştırma), en iyi/en kötü gün, borç ödemesi, kasadaki haftalık değişim, haftanın ilk 3 kuryesi ve hiç çalışmayanlar, aile borcu durumu. Finans'tan tekrar açılır, Ayarlar'dan kapatılabilir |
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
  Economy/   UpgradeManager (kalıcı geliştirmeler, platform paketi / kurye hesap sınırı), CampaignManager (boost, hedefler)
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
- **Para birimi (Ft / €)**: oyun içi Ayarlar sekmesi. Oyun içeride hep Forint ile hesaplar, € sadece gösterimdir
  (kur `GameData.FtPerEuro`, varsayılan 1 € = 400 Ft). Yeni bir metinde tutar yazarken elle "Ft" yazma, `Ft(tutar)` kullan.

## Sonraki adımlar (önerilen sıra)
1. ~~Kayıt sistemi (JSON save/load)~~ — yapıldı. Yeni bir sisteme kalıcı alan eklersen `SaveSystem.cs`'e de ekle.
2. Gerçek UI (Canvas + TextMeshPro) ile IMGUI'yi değiştirmek.
3. Kurye ve dükkan sprite'ları / 3D low-poly şehir.
4. Rakip kurye şirketi (AI) ve fiyat savaşı.
5. Abonelik sistemi ve reklam kampanyaları.
