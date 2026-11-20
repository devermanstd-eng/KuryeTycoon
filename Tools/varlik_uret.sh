#!/bin/bash
# Kurye Tycoon — stilize varlıkları Blender'da üret (Mac)
# Blender'ı kendisi bulur ve pencere açmadan (--background) Tools/Blender/varlik_uretici.py'yi çalıştırır.
#
# Kullanım (repo içinden ya da her yerden):
#   Tools/varlik_uret.sh                              # hepsi → Art/Stilize
#   Tools/varlik_uret.sh --modeller kurye,bina_kose   # sadece bazıları
#   Tools/varlik_uret.sh --cizgi 0                    # mürekkep dış çizgisiz
#   BLENDER_PATH=/yol/Blender Tools/varlik_uret.sh    # Blender başka yerdeyse
#
# Sonra Unity'de: Kurye Tycoon > Stilize Prefabları Kur (malzemeler + Prefabs/Stilize).
# Çıkış kodu: 0 = tamam · 1 = Blender ya da betik hata verdi · 2 = Blender bulunamadı

DIZIN="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
KOK="$(dirname "$DIZIN")"                       # Assets/KuryeTycoon
BETIK="$DIZIN/Blender/varlik_uretici.py"
CIKTI="$KOK/Art/Stilize"

say()  { printf "\033[1;36m==> %s\033[0m\n" "$1"; }
fail() { printf "\033[1;31mHATA: %s\033[0m\n" "$1"; exit "${2:-1}"; }

case "${1:-}" in -h|--help) sed -n '2,12p' "$0"; exit 0 ;; esac
[ -f "$BETIK" ] || fail "Betik yok: $BETIK" 2

# Blender: BLENDER_PATH > Uygulamalar > kullanıcı Uygulamalar > Steam > Spotlight (paket kimliği) > PATH
blender_bul() {
  if [ -n "${BLENDER_PATH:-}" ]; then echo "$BLENDER_PATH"; return; fi
  local app
  for app in "/Applications/Blender.app" "$HOME/Applications/Blender.app" \
             "$HOME/Library/Application Support/Steam/steamapps/common/Blender/Blender.app"; do
    if [ -x "$app/Contents/MacOS/Blender" ]; then echo "$app/Contents/MacOS/Blender"; return; fi
  done
  while IFS= read -r app; do
    if [ -x "$app/Contents/MacOS/Blender" ]; then echo "$app/Contents/MacOS/Blender"; return; fi
  done < <(mdfind "kMDItemCFBundleIdentifier == 'org.blenderfoundation.blender'" 2>/dev/null)
  command -v blender 2>/dev/null
}

BLENDER="$(blender_bul)"
[ -n "$BLENDER" ] && [ -x "$BLENDER" ] || fail "Blender bulunamadı. blender.org'dan kurup bir kez aç ya da BLENDER_PATH=/.../Blender ver." 2

say "Blender: $BLENDER"
say "Çıktı:   $CIKTI"
mkdir -p "$CIKTI"
LOG="${TMPDIR:-/tmp}/kurye_varlik_uret.log"

# --python-exit-code: betik hata verirse Blender 1 ile çıkar (yoksa hata olsa da 0 dönerdi)
"$BLENDER" --background --factory-startup --python-exit-code 1 --python "$BETIK" -- --cikti "$CIKTI" "$@" 2>&1 | tee "$LOG"
KOD=${PIPESTATUS[0]}

if [ "$KOD" -ne 0 ] || grep -q "Traceback (most recent call last)" "$LOG"; then
  fail "Blender hata verdi (çıkış kodu $KOD). Tam çıktı: $LOG" 1
fi
echo
printf "\033[1;32mTAMAM\033[0m · FBX'ler ve StilizePalet.png: %s\n" "$CIKTI"
echo "Unity'de: Kurye Tycoon > Stilize Prefabları Kur"
