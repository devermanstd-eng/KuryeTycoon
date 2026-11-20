# Kurye Tycoon: Claude için çalışma kuralları

Bu repo bir Unity projesinin `Assets/KuryeTycoon` klasörüdür (Unity 6000.6.9f1, URP). Sahne dosyası yok; oyun
`RuntimeInitializeOnLoadMethod` ile kendini kurar. İnsanlar için kurallar `KATKI.md`'de, buradakiler onlara ek.
Ekipte birden fazla kişi kendi Claude'uyla çalışıyor; bu kurallar kimsenin işi diğerininkini ezmesin diye var.

## Git
- **`main`'e asla doğrudan commit ya da push yapma.** Güncel `main`'den yeni dal aç (`<isim>/<konu>`, ör.
  `erman/sokak-donatilari`), commit'le, push'la, PR aç. Merge'ü bir insan yapar.
- Force push yok; başkasının dalını yeniden yazma.
- PR açmadan önce `main` ile çakışma var mı bak; varsa kendi dalında çöz.
- Sadece işle ilgili dosyaları commit'le. `Modeller/Malzemeler/*.mat`'ta yalnızca sayı yuvarlaması (`_Color`
  ondalıkları) değişmişse bu Unity'nin yeniden kaydetme gürültüsüdür, commit'leme.
- Commit mesajı ve PR açıklaması Türkçe: ne değişti, neden; denge değeri değiştiyse eski → yeni.

## Her değişiklikte
1. **`.meta`**: her yeni dosya ve klasörün `.meta`'sı commit'te olsun. Unity dışında oluşturduysan yeni GUID'li
   bir `.meta` yaz (`.cs` → `MonoImporter`, `.md` / `.txt` → `TextScriptImporter`, `.py` / `.sh` / klasör →
   `DefaultImporter`). Var olan GUID'leri asla değiştirme.
2. **`Scripts/Core/DebugReport.cs` → `CodeVersion`**: kod değiştiyse artır: `"YYYY-MM-DD uN (kısa açıklama)"`,
   N bir öncekinin bir fazlası. Hata raporları ve test özetleri hangi kodla oynandığını buradan bilir.
3. **`KURULUM.md`**: ilgili yeri güncelle (yeni sistem → "Oyunda neler var" tablosuna satır; yeni menü ya da
   komut → nasıl çalıştırılır).
4. **Unity'de derleyip denemediysen** PR açıklamasına açıkça **"Unity'de derlenmedi"** yaz ve kısa bir kontrol
   listesi ekle (Console temiz mi, değişen ekranda neye bakılacak).

## Kod
- Tanımlar, yorumlar ve oyun metinleri Türkçe (mevcut kod gibi).
- Arayüz **UI Toolkit** ile yazılır (`Scripts/UI/Arayuz`, görünüm `Resources/UI/Arayuz/Kurye.uss`).
  **OnGUI'ye yeni çizim ekleme.** Metin ağırlıklı pencereler `UiYazici` ile; renkler `U` paletinden.
  Satır içi stil USS'i ezer.
- Oyun durumunu tutan yeni alan kayda girmeli: `KATKI.md` > Kayıt sistemi.
- Sahneye bir şey yazma; ayarlar `Resources` altındaki dosyalarda (ör. `Resources/Gorsel/Atmosfer.asset`).

## Test ve Denge Doğrulama
- Denge bandı: rakiplerin normal olduğu oyunda pazar payı **%45–55**; kurye platformu paketinin dolu kaldığı
  süre **10–13 gün** (en fazla 13).
- Toplu simülasyon (Mac, Unity **kapalıyken**, repo kökünden): `Tools/toplu_test.sh` (5 şehir tohumu × 60 gün +
  senaryolar). Uzun vade: `--gun 120 --sec tohum`. Denge onayı için `--hiz 32`.
  Rapor: `<proje>/TopluTest/toplu_*/toplu_ozet.txt`.
- Denge değerini değiştiren PR, toplu testin önce/sonra sonucunu açıklamada verir.

## Blender modelleri
- Betikler `Tools/Blender/`; Unity'de **Kurye Tycoon > Blender** menüsü Blender'ı arka planda çalıştırır.
- Ölçü metre, ön −Y, zemin z = 0; oyuncunun şirket rengine boyanacak malzemeler `SirketRengi_*` / `SirketRengi2_*`.
- Üretilen FBX ve dokuları `.meta`'larıyla commit'le.
