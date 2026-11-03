#!/bin/bash
# Kurye Tycoon — Mac'te tek komutla kurulum
# Kullanım (Terminal):
#   curl -fsSL https://raw.githubusercontent.com/devermanstd-eng/KuryeTycoon/main/Tools/mac-kurulum.sh | bash
#
# Ne yapar: git'i kontrol eder, Unity Hub ve Unity Editor'ü kurar (yoksa),
# ~/UnityProjects/KuryeTycoon projesini oluşturur, kodu indirir (varsa günceller) ve Unity'yi açar.

VERSION="6000.6.7f1"
PROJECT="$HOME/UnityProjects/KuryeTycoon"
REPO="https://github.com/devermanstd-eng/KuryeTycoon.git"
HUB="/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"
export UNITY_HUB_CLI_NO_DEPRECATION_WARNING=1
EDITORS="/Applications/Unity/Hub/Editor"

say() { printf "\n\033[1;36m==> %s\033[0m\n" "$1"; }
fail() { printf "\n\033[1;31mHATA: %s\033[0m\n" "$1"; exit 1; }

say "Kurye Tycoon Mac kurulumu başlıyor"

# 1) Git (Xcode komut satırı araçları)
if ! xcode-select -p >/dev/null 2>&1; then
  say "Git için Apple'ın komut satırı araçları kuruluyor"
  xcode-select --install >/dev/null 2>&1
  echo "Açılan pencerede 'Yükle'ye bas. Kurulum bitince bu komutu TEKRAR çalıştır."
  exit 0
fi
echo "Git hazır: $(git --version)"

# 2) Unity Hub
if [ ! -x "$HUB" ]; then
  if command -v brew >/dev/null 2>&1; then
    say "Unity Hub Homebrew ile kuruluyor"
    brew install --cask unity-hub || fail "Unity Hub kurulamadı."
  else
    say "Unity Hub bulunamadı"
    echo "1. Açılan sayfadan 'Download for Mac' ile Unity Hub'ı indir ve kur."
    echo "2. Unity Hub'ı aç ve Unity hesabınla giriş yap."
    echo "3. Bu komutu TEKRAR çalıştır."
    open "https://unity.com/download"
    exit 0
  fi
  open -a "Unity Hub"
  echo "Unity Hub açıldı: Unity hesabınla giriş yap (ücretsiz Personal lisans), sonra bu komutu TEKRAR çalıştır."
  exit 0
fi
echo "Unity Hub hazır."

# 3) Unity Editor
APP="$EDITORS/$VERSION/Unity.app"
if [ ! -d "$APP" ]; then
  ARCH="$(uname -m)"; [ "$ARCH" = "arm64" ] || ARCH="x86_64"
  say "Unity $VERSION kuruluyor ($ARCH) — birkaç GB, internet hızına göre uzun sürebilir"
  "$HUB" -- --headless install --version "$VERSION" --architecture "$ARCH"
fi
if [ ! -d "$APP" ]; then
  # Aynı sürüm kurulamadıysa kurulu herhangi bir Unity 6 sürümünü kullan
  APP="$(ls -d "$EDITORS"/6000*/Unity.app 2>/dev/null | tail -1)"
fi
[ -n "$APP" ] && [ -d "$APP" ] || {
  open -a "Unity Hub"
  fail "Unity Editor kurulamadı. Unity Hub'da giriş yaptığından emin ol, Installs → Install Editor ile bir Unity 6 sürümü kur ve bu komutu tekrar çalıştır."
}
echo "Unity Editor: $APP"

# 4) Proje klasörü + kod
mkdir -p "$PROJECT/Assets"
if [ -d "$PROJECT/Assets/KuryeTycoon/.git" ]; then
  say "Kod zaten var, güncelleniyor"
  git -C "$PROJECT/Assets/KuryeTycoon" pull || fail "git pull başarısız."
else
  say "Kod GitHub'dan indiriliyor"
  git clone "$REPO" "$PROJECT/Assets/KuryeTycoon" || fail "git clone başarısız."
fi

# 5) Unity'yi aç
say "Unity açılıyor: $PROJECT"
open -a "$APP" --args -projectPath "$PROJECT"

cat <<MSG

Bitti!
- İlk açılış birkaç dakika sürebilir (Unity projeyi hazırlıyor).
- Açılınca üstteki Play (▶) butonuna bas; oyun kendiliğinden başlar, sahne kurmana gerek yok.
- Güncelleme almak için bu komutu tekrar çalıştırman yeterli.
- Proje klasörü: $PROJECT
MSG
