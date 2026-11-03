# Projeye katkı (arkadaşlar için)

Orijinal kodu (`main` dalı) bozmadan kendi eklemelerini yapmanın yolu:
**kendi dalında çalış → Pull Request aç → proje sahibi inceleyip birleştirsin.**

`main` dalına doğrudan push yapma. Her yeni özellik için ayrı bir dal aç.

---

## Yöntem A — GitHub Desktop (git bilmeyenler için, önerilen)

### İlk kurulum (bir kere)
1. [GitHub Desktop](https://desktop.github.com/)'u indir, GitHub hesabınla giriş yap.
2. **File → Clone repository** → `devermanstd-eng/KuryeTycoon` → konum olarak Unity projenin
   **`Assets`** klasörünü seç, klasör adı **`KuryeTycoon`** olsun → **Clone**.
3. Unity'de projeyi aç (kurulum için `README.md`).

### Her yeni özellik için
1. GitHub Desktop'ta üstte **Current branch → New branch**.
   Ad ver: `ozellik/ozel-kampanya`, `duzeltme/harita-renk` gibi → **Create branch**.
2. Unity'de / kod editöründe değişikliklerini yap, oyunu Play ile dene.
3. GitHub Desktop'ta sol altta kısa bir açıklama yaz (ör. "Gece boost kampanyası eklendi") → **Commit to ozellik/...**
4. Üstte **Publish branch** (ilk seferde) veya **Push origin**.
5. **Create Pull Request** butonuna bas → tarayıcıda ne yaptığını anlat → **Create pull request**.
6. Proje sahibi inceler; uygunsa **Merge** eder, değişiklik istersen aynı dalda düzeltip tekrar push et.

### Ana koddaki yenilikleri almak
1. **Current branch → main** → **Fetch origin** → **Pull origin**.
2. Kendi dalına geri dön → **Branch → Update from main**.

---

## Yöntem B — Komut satırı

```bash
# İlk kurulum (Unity projesinin Assets klasöründe)
git clone https://github.com/devermanstd-eng/KuryeTycoon.git KuryeTycoon
cd KuryeTycoon

# Her yeni özellik için
git checkout main
git pull
git checkout -b ozellik/ozel-kampanya
# ... değişiklikleri yap, Unity'de dene ...
git add .
git commit -m "Gece boost kampanyası eklendi"
git push -u origin ozellik/ozel-kampanya
# GitHub'da çıkan "Compare & pull request" butonuyla PR aç
```

---

## Kurallar
- **Her özellik ayrı dal, ayrı Pull Request.** Küçük PR'lar daha kolay incelenir.
- PR açmadan önce Unity'de **Console'da kırmızı hata olmadığından** emin ol.
- `.meta` dosyalarını da commit et (Unity'nin script bağlantıları bunlarla tutulur).
  Yeni bir `.cs` dosyası eklediysen Unity'nin oluşturduğu `.cs.meta` dosyası da commit'te olmalı.
- Denge değerlerini (fiyatlar, oranlar) değiştirdiysen PR açıklamasında belirt.
- Kod içi yorumlar ve oyun metinleri Türkçe.
- **Kayıt sistemi:** oyunun durumunu tutan yeni bir alan eklersen kayda girip girmediğine bak. `Courier` ve `CampaignManager`'ın public alanları otomatik kaydedilir (JsonUtility). Diğer sistemlerde (ekonomi, dükkanlar, rakip, aile borcu, destek, operasyon...) alanı `Scripts/Core/SaveSystem.cs`'teki ilgili Save sınıfına ve o sistemin `CaptureSave` / `RestoreSave` metotlarına ekle. Denge sabitlerini (kira, oranlar) kayda ekleme.

## Kodu nereden değiştirmeli?
| Ne değiştirmek istiyorsun | Dosya |
|---|---|
| Araç ücretleri, dükkanlar, ekipman fiyatları, isimler | `Scripts/Data/GameData.cs` |
| Sipariş üretimi, atama, puan | `Scripts/Orders/OrderManager.cs` |
| Kurye davranışı, meslekler, hijyen | `Scripts/Couriers/Courier.cs`, `CourierManager.cs` |
| Para, vergiler, fiyat formülü | `Scripts/Economy/EconomyManager.cs` |
| Boost ve hedef ödülleri | `Scripts/Economy/CampaignManager.cs` |
| Kalıcı geliştirmeler | `Scripts/Economy/UpgradeManager.cs` |
| Destek ekibi | `Scripts/Staff/SupportManager.cs` |
| Hikaye ve aile borcu | `Scripts/Story/FamilyLoan.cs` |
| Rastgele olaylar | `Scripts/Events/EventManager.cs` |
| Arayüz | `Scripts/UI/GameUI.cs` |
