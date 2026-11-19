#!/bin/bash
# Kurye Tycoon — Toplu simülasyon (Mac)
# Unity'yi arayüzsüz açar, akıllı test botunu 5 şehir tohumu + senaryolarla arka arkaya koşturur.
#
# Kullanım (Unity KAPALIYKEN, repo içinden ya da her yerden):
#   Tools/toplu_test.sh                       # hepsi, 64×
#   Tools/toplu_test.sh --hiz 32              # denge onayı için daha yavaş ve hassas
#   Tools/toplu_test.sh --sec tohum           # sadece 5 tohum (ya da: senaryo)
#   UNITY_PATH=/yol/Unity Tools/toplu_test.sh # Unity başka yerdeyse
#
# Çıktı: <Unity projesi>/TopluTest/toplu_TARİH/ (toplu_ozet.txt, gunluk_*.csv, ozet_*.txt, unity.log)
# Çıkış kodu: 0 = bütün koşular bitti ve hata yok · 1 = hata ya da yarım koşu · 2 = script/Unity bulunamadı

UNITY_SURUM="6000.6.9f1"
HIZ=64
SEC="hepsi"
ZAMAN_ASIMI=360   # dk

while [ $# -gt 0 ]; do
  case "$1" in
    --hiz) HIZ="$2"; shift 2 ;;
    --sec) SEC="$2"; shift 2 ;;
    --zaman-asimi) ZAMAN_ASIMI="$2"; shift 2 ;;
    -h|--help) sed -n '2,13p' "$0"; exit 0 ;;
    *) echo "Bilinmeyen seçenek: $1"; exit 2 ;;
  esac
done

say()  { printf "\033[1;36m==> %s\033[0m\n" "$1"; }
fail() { printf "\033[1;31mHATA: %s\033[0m\n" "$1"; exit 2; }

# 1) Unity projesinin kökü: bu repo <proje>/Assets/KuryeTycoon içinde durur. Yukarı çıkıp Assets + ProjectSettings olan klasörü bul.
DIZIN="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJE=""
ARA="$DIZIN"
while [ "$ARA" != "/" ]; do
  if [ -d "$ARA/Assets" ] && [ -d "$ARA/ProjectSettings" ]; then PROJE="$ARA"; break; fi
  ARA="$(dirname "$ARA")"
done
[ -n "$PROJE" ] || fail "Unity projesi bulunamadı (Assets ve ProjectSettings olan üst klasör yok). Repo <proje>/Assets/KuryeTycoon içinde olmalı."

# 2) Unity yolu: UNITY_PATH > Hub'daki 6000.6.9f1 > projenin sürümü > kurulu en yeni Unity 6 (uyarıyla)
EDITORLER="/Applications/Unity/Hub/Editor"
yol() { echo "$EDITORLER/$1/Unity.app/Contents/MacOS/Unity"; }
UNITY="${UNITY_PATH:-}"
if [ -z "$UNITY" ]; then
  if [ -x "$(yol "$UNITY_SURUM")" ]; then
    UNITY="$(yol "$UNITY_SURUM")"
  else
    PROJE_SURUM="$(sed -n 's/^m_EditorVersion: //p' "$PROJE/ProjectSettings/ProjectVersion.txt" 2>/dev/null | tr -d '\r')"
    if [ -n "$PROJE_SURUM" ] && [ -x "$(yol "$PROJE_SURUM")" ]; then
      UNITY="$(yol "$PROJE_SURUM")"
    else
      SON="$(ls -d "$EDITORLER"/6000* 2>/dev/null | tail -1)"
      [ -n "$SON" ] && UNITY="$SON/Unity.app/Contents/MacOS/Unity"
    fi
    [ -n "$UNITY" ] && printf "\033[1;33mUyarı: Unity %s bulunamadı, şu kullanılıyor: %s\033[0m\n" "$UNITY_SURUM" "$UNITY"
  fi
fi
[ -n "$UNITY" ] && [ -x "$UNITY" ] || fail "Unity bulunamadı. Unity Hub'dan $UNITY_SURUM kur ya da UNITY_PATH=/.../Unity ver."

# 3) Unity bu projeyle açıksa batch mod proje kilidine takılır
if [ -f "$PROJE/Temp/UnityLockfile" ] && lsof "$PROJE/Temp/UnityLockfile" >/dev/null 2>&1; then
  fail "Unity bu projeyle açık görünüyor. Önce Unity editörünü kapat (aynı proje iki kez açılamaz)."
fi

# 4) Çalıştır (rapor klasörü Assets dışında: Unity içeri aktarmaya çalışmasın)
CIKTI="$PROJE/TopluTest/toplu_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$CIKTI"
LOG="$CIKTI/unity.log"
: > "$LOG"

say "Proje: $PROJE"
say "Unity: $UNITY"
say "Koşular: $SEC · hız ${HIZ}× · rapor: $CIKTI"
echo "İlerleme aşağıda ([TopluTest] satırları). Tam log: $LOG"
BASLA=$(date +%s)

"$UNITY" -batchmode -nographics -projectPath "$PROJE" \
  -executeMethod KuryeTycoon.EditorTools.TopluTest.Calistir \
  -logFile "$LOG" \
  -topluCikti "$CIKTI" -topluHiz "$HIZ" -topluSec "$SEC" -topluZamanAsimi "$ZAMAN_ASIMI" &
UNITY_PID=$!

trap 'echo; echo "Durduruluyor..."; kill $UNITY_PID 2>/dev/null; kill $TAIL_PID 2>/dev/null; exit 130' INT TERM

tail -n +1 -F "$LOG" 2>/dev/null | grep --line-buffered "\[TopluTest\]" &
TAIL_PID=$!

wait $UNITY_PID
KOD=$?
sleep 1
kill $TAIL_PID 2>/dev/null
pkill -P $$ tail 2>/dev/null

SURE=$(( ($(date +%s) - BASLA) / 60 ))
echo
OZET="$CIKTI/toplu_ozet.txt"
if [ -f "$OZET" ]; then
  # Tablo ve denge/hata satırları (AYRINTI bölümüne kadar)
  awk '/^==================== AYRINTI/{exit} {print}' "$OZET"
else
  printf "\033[1;31mtoplu_ozet.txt oluşmadı.\033[0m Unity logunun sonu:\n"
  grep -E "error|Error|Exception|\[TopluTest\]" "$LOG" | tail -20
fi

echo
if [ "$KOD" -eq 0 ]; then
  printf "\033[1;32mBAŞARILI\033[0m (çıkış kodu 0) · %s dk\n" "$SURE"
else
  printf "\033[1;31mBAŞARISIZ\033[0m (çıkış kodu %s: hata ya da yarım koşu) · %s dk\n" "$KOD" "$SURE"
fi
echo "Rapor klasörü: $CIKTI"
exit "$KOD"
