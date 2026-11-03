# Kurye Tycoon

Wolt benzeri bir kurye şirketini yönettiğin 2D tycoon oyunu (Unity prototip).
Annenle babanın birikimi olan 1 000 000 Ft ile kendi kurye şirketini kur ve borcu Temmuz sonuna kadar taksitlerle geri öde.
Kuryeleri işe al, siparişleri dağıt, kampanyalar düzenle, vergileri öde ve şirketini büyüt.

Bu repo, bir Unity projesinin `Assets/KuryeTycoon` klasörüdür. Görsel, prefab ya da sahne
dosyası gerekmez: tek bir script her şeyi kendisi kurar.

## Gerekenler
- **Unity 6** (6000.x) — Unity 2022.3 LTS de çalışır
- Unity Hub

## Mac: tek komutla kurulum
Terminal'i aç (Cmd + Boşluk → `Terminal`) ve yapıştır:
```bash
curl -fsSL https://raw.githubusercontent.com/devermanstd-eng/KuryeTycoon/main/Tools/mac-kurulum.sh | bash
```
Script; git'i, Unity Hub'ı ve Unity'yi kontrol eder/kurar, `~/UnityProjects/KuryeTycoon` projesini oluşturur,
kodu indirir ve Unity'yi açar. Bir adımda durursa ekrandaki talimatı yapıp komutu tekrar çalıştır.
Güncelleme almak için de aynı komutu çalıştırman yeterli.

## Kurulum (5 dakika)

### 1. Kodu indir
**Yol A — ZIP (git bilmeyenler için):**
1. Bu sayfada yeşil **Code** butonu → **Download ZIP**.
2. ZIP'i aç. İçinden çıkan klasörün adı `KuryeTycoon-main` olur; adını **`KuryeTycoon`** yap.

**Yol B — git:**
```bash
git clone https://github.com/devermanstd-eng/KuryeTycoon.git KuryeTycoon
```

### 2. Unity projesi oluştur
1. Unity Hub → **New project**.
2. Şablon: **Universal 2D**.
3. Proje adı istediğin gibi (ör. `KuryeTycoonOyun`) → **Create project**.

### 3. Klasörü projeye koy
1. Unity açılınca **Project** penceresinde **Assets**'e sağ tık → **Show in Explorer**.
2. Açılan **Assets** klasörünün içine 1. adımdaki **`KuryeTycoon`** klasörünü kopyala.
3. Unity'ye dön, derlemenin bitmesini bekle. **Console**'da kırmızı hata olmamalı.

### 4. Sahne
Sahne kurmana gerek yok: Play'e basınca oyun kendiliğinden başlar.
(İstersen başlangıç parası gibi ayarları değiştirmek için boş bir GameObject'e `GameBootstrap` ekleyebilirsin.)

### 5. Oyna
1. **Play** (▶) butonuna bas.
2. Şirket adını ve renklerini seç → **Şirketi Kur ve Başla**.
3. Kısayollar: **Boşluk** = duraklat, **1–8** = hız (1x, 2x, 4x, 8x, 16x, 32x, 64x, 128x).
4. Game penceresi küçükse üstteki **Free Aspect** yerine **Full HD (1920x1080)** seç.

## Güncelleme
- **ZIP ile kurduysan:** yeni ZIP'i indir, `Assets/KuryeTycoon` klasörünü sil ve yenisini koy.
- **git ile kurduysan:**
```bash
cd <UnityProjen>/Assets/KuryeTycoon
git pull
```

## Sorun çıkarsa
- **Console'da kırmızı hata:** Console'un sol üstündeki **Clear**'a bas; hâlâ varsa ilk satırı geliştiriciye gönder.
- **GameBootstrap listede çıkmıyor:** Klasör `Assets/KuryeTycoon/Scripts/...` şeklinde mi kontrol et (fazladan iç içe klasör olmamalı).

Oyunun tüm sistemleri ve ayarları için: [KURULUM.md](KURULUM.md)

Projeye kendi eklemelerini yapmak istiyorsan: [KATKI.md](KATKI.md)
