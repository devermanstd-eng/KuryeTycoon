# -*- coding: utf-8 -*-
"""
KURYE TYCOON · BİNA ATÖLYESİ: PASTEL MODÜLER PARÇALAR VE BİRLEŞTİRİCİ

"Birkaç blok koy, kasabaya dönüşsün" (Little City / Townscaper tarzı) bina kurma sisteminin parça kütüphanesi ve
birleştirme kurallarının Python hâli. Geometri çekirdeği bolge_modelleri.py'den gelir (saf Python).

  • Parçalar: hücreye göre normalize (hücre 2,8 × 2,8 m taban, 3 m kat; kök hücrenin alt ortası, parçanın "önü" −Y).
    Duvar panelleri hücrenin ön yüzüne göre çizilir; birleştirici 90°'lik dönüşlerle diğer yüzlere koyar.
  • Renkler rol olarak yazılır: malzeme adı "Pastel_<etiket>_@D" (duvar), "@d" (duvarın koyusu), "@C" (çatı),
    "@c" (çatının koyusu), "@K" (kapı / panjur vurgusu). Birleştirici bunları bloğun seçili rengiyle değiştirir:
    "Pastel_Duvar_bcd9ea" gibi. Sabit renkler doğrudan "Pastel_<etiket>_<rrggbb>".
  • Unity tarafı parçaları FBX'ten değil, bu betiğin yazdığı JSON'dan okur (Resources/Atolye/pastel_parcalar.json):
    Blender gerekmez, araç parçaları anında kurar. Köşeler yüz başına ayrıdır (düz gölgeleme); normalleri Unity hesaplar. Parça değişince:  python3 Tools/Blender/pastel_parcalar.py --json
  • Birleştirme kuralları (bina()) Scripts/World/Atolye/AtolyeKurucu.cs'teki C# birleştiricisiyle AYNI olmalı: birini değiştirince öbürünü de.
  • v6 (Londra mimarisi): sanat stili aynı, mimari Londra. Eski parçalar aynı adla Londra hâlini aldı (giyotin pencere,
    Victoria tarzı dükkân cephesi, sade arduvaz çatı); bloğun tarz alanı 5 ve üstü Londra üsluplarıdır (USLUPLAR tablosu:
    Georgian, Victorian, beyaz sıva, mews, Portland taşı, gotik, tuğla depo); çatı tarzı 7 parapet arkasında mansart.
    Kalem çizgisi inceldi; "Pastel_Iz_*" malzemeli yüzeyler (tuğla / arduvaz sırası izi, cam kayıtları) kontur almaz.
    Ayrıntı: dosyada "LONDRA MİMARİSİ (v6)" bölümü.
  • v7 (gerçekçi ağaçlar): gövde + dal iskeleti + yaprak kümeleri, dört yaprak tonu (roller Y, y, a, m), kaynaklı köşeler
    (g.vn: yumuşak gölgeleme), üç ayrıntı düzeyi (JSON: parcalar / hafifler / uzaklar), kışın çıplak dal (oyunda
    gölgelendirici kırpar; önizlemede yaprak rengi KIS_RENGI). Ayrıntı: "GERÇEKÇİ AĞAÇLAR (v7)" bölümü.

Çalıştırma (Blender gerekmez):
  python3 Tools/Blender/pastel_parcalar.py --json                 parça kütüphanesini Unity için yazar
  python3 Tools/Blender/pastel_parcalar.py --onizleme klasor      parça kataloğu + örnek binalar (PNG, kolaj)
  python3 Tools/Blender/pastel_parcalar.py --ornekler klasor      örnek tasarımları JSON olarak yazar (araçta "İçe aktar")
  python3 Tools/Blender/pastel_parcalar.py --sahneler             örnek sahneleri Resources/Atolye/Sahneler altına yazar

Ölçek: oyunda bölge binaları 14 m arsaya göre sabit ölçekle yerleşir (City3D.ArsaOlculu). 5 × 5 hücre = 14 m: bir hücre bir
pencere aksı kadar (2,8 m), kat 3 m; Bolge_* binalarıyla (kat ~3,05–3,3 m) yan yana aynı ölçüde durur.
"""

import json
import math
import os
import random
import sys


def _cekirdek():
    if "bolge_modelleri" in sys.modules:
        return sys.modules["bolge_modelleri"]
    adaylar = []
    kok = os.environ.get("KURYE_TYCOON_KOK")
    if kok:
        adaylar.append(os.path.join(kok, "Tools", "Blender"))
    try:
        adaylar.append(os.path.dirname(os.path.abspath(__file__)))
    except NameError:
        pass
    adaylar.append(os.path.join(os.getcwd(), "Tools", "Blender"))
    import importlib.util
    for klasor in adaylar:
        yol = os.path.join(klasor, "bolge_modelleri.py")
        if os.path.isfile(yol):
            spec = importlib.util.spec_from_file_location("bolge_modelleri", yol)
            mod = importlib.util.module_from_spec(spec)
            sys.modules["bolge_modelleri"] = mod
            spec.loader.exec_module(mod)
            return mod
    raise RuntimeError("bolge_modelleri.py bulunamadi")


B = _cekirdek()
G, C = B.G, B.C
kutu, obox, cubuk, silindir, dsil, top, prizma, kati = B.kutu, B.obox, B.cubuk, B.silindir, B.dsil, B.top, B.prizma, B.kati

# ================================== ÖLÇÜLER VE RENKLER ==================================
HUCRE = 2.8                 # hücre tabanı (m)
KAT = 3.0                   # kat yüksekliği (m)
A = HUCRE / 2
IZGARA = 5                  # 5 × 5 hücre = 14 m arsa
KAT_SAYISI = 8
DK = 0.2                    # duvar panelinin kalınlığı (hücrenin içine doğru)

# Bloğun tarzı
APARTMAN, FACHWERK, TAS, KAIDE, MERDIVEN = 0, 1, 2, 3, 4
# 5 ve üstü Londra üslupları (USLUPLAR tablosu; tarz − USLUP_TABAN = tablo sırası)
USLUP_TABAN = 5
GEORGIAN, VICTORIAN, SIVA_TERAS, MEWS, PORTLAND_T, GOTIK, DEPO = 5, 6, 7, 8, 9, 10, 11
TARZ_AD = ["Boyalı sıva teras (pastel)", "Yarı ahşap (Tudor) ev", "Taş kule", "Taş kaide / teras", "Taş merdiven"]

# 0–9 pastel boyalı sıva (Notting Hill); 10–16 Londra (v6): 10 sarı "London stock" tuğla · 11 koyu stock · 12 kırmızı tuğla ·
# 13 koyu kırmızı tuğla · 14 krem-beyaz sıva (stucco) · 15 Portland taşı · 16 kahve tuğla (depo, mews)
DUVAR_RENK = ["#bcd9ea", "#f3c6cf", "#d9c8ea", "#f6ecc9", "#c5e6d0", "#f7d9b5", "#f4f1e8", "#e9b9a6", "#cfd6e6", "#f2e3a1",
              "#dcc594", "#cfb384", "#d3927a", "#c58069", "#f4efe2", "#e6e2d8", "#bfa98f"]
# 0–5 eski; 6 gri-mavi arduvaz · 7 açık arduvaz · 8 kırmızımsı kiremit (bazı Victorian evler) · 9 kurşun grisi
CATI_RENK = ["#c8553f", "#e08a45", "#5f8fb8", "#6fa37a", "#8a5a78", "#7d7f8c", "#8795a8", "#a3adba", "#b9705a", "#7f8c90"]
# kapı renkleri (Londra): kırmızı, lacivert, siyah, yeşil, sarı, petrol, mürdüm, gök mavisi
VURGU_RENK = ["#b8382f", "#2c3d70", "#34333a", "#2f6048", "#e2b33c", "#3f7d74", "#6b4a7a", "#5f8fb8"]
# 0–5 eski; 6 koyu yeşil · 7 bordo · 8 lacivert · 9 krem (düz renk tenteler)
TENTE_RENK = ["#c8553f", "#3f7d74", "#3f5f8a", "#d9a441", "#8a5a78", "#5a8f4a", "#2f6048", "#7a2f38", "#2c3d70", "#efe6cf"]
# yaz açık / yaz koyu / ilkbahar / sonbahar turuncu / kızıl / sarı / çiçekli pembe / kış (karlı)
YAPRAK_RENK = ["#8fbf7a", "#6fa968", "#a9c96a", "#e0a04a", "#c9603f", "#e8c95a", "#f2b6c8", "#dfe3e6"]
KIS_RENGI = 7               # bu yaprak renginde önizleme kışı gösterir: yaprak döken ağaçlar çıplak, dallarda kar
TAS_RENK = "#cfc8ba"

HEX = B.HEX


def PM(etiket, hexv):
    """Sabit renkli pastel malzeme: "Pastel_<etiket>_<rrggbb>"."""
    hexv = hexv.lower().lstrip("#")
    ad = "Pastel_%s_%s" % (etiket, hexv)
    HEX[ad] = "#" + hexv
    return ad


def ROL(etiket, rol, ornek):
    """Rol renkli malzeme ("Pastel_Duvar_@D"): birleştirici bloğun rengini koyar. ornek: katalog resmindeki renk."""
    ad = "Pastel_%s_@%s" % (etiket, rol)
    HEX[ad] = ornek
    return ad


D = ROL("Duvar", "D", "#bcd9ea")
D2 = ROL("Duvar", "d", "#a9c6d8")
CT = ROL("Cati", "C", "#c8553f")
CT2 = ROL("Cati", "c", "#ad4835")
VR = ROL("Vurgu", "K", "#3f7d74")
TN = ROL("Tente", "T", "#c8553f")
YP = ROL("Yaprak", "Y", "#8fbf7a")
YP2 = ROL("Yaprak", "y", "#7aa868")
KABUK = ROL("Kabuk", "k", "#3e4a3a")        # taç öbeklerinin ters kabuğu (dış hat çizgisi)
YAZI = PM("Yazi", "#fff6dc")
SILME = PM("Silme", "#fbf7ee")
SILME2 = PM("Silme", "#e9e2d2")
CAM = PM("Cam", "#bcd6f0")
CAM2 = PM("Cam", "#a8c8e8")
VIT = PM("Vitrin", "#bcd6f0")
AHSAP = PM("Ahsap", "#8a6247")
AHSAP2 = PM("Ahsap", "#5d4336")
SIVA = PM("Siva", "#f6f0e2")
TAS = PM("Tas", TAS_RENK)
TAS2 = PM("Tas", "#b5ad9d")
TAS3 = PM("Tas", "#dcd6ca")
DEMIR = PM("Demir", "#4a4550")
SAKSI = PM("Saksi", "#c9785a")
YAPRAK = PM("Yaprak", "#8fbf7a")
ISIK = PM("Isik", "#ffe9b0")
PIRINC = PM("Pirinc", "#d9b45a")
CICEK = [PM("Cicek", h) for h in ("#e8607a", "#f5c84a", "#ffffff", "#b58ad9")]


def koyu(hexv, f=0.86):
    return B.koyu(hexv, f)


def karis(h1, h2, t):
    """İki rengin karışımı (t = 0 → h1, 1 → h2). C#: AtolyeKurucu.Karis (aynı yuvarlama: aşağı)."""
    a, b = h1.lstrip("#"), h2.lstrip("#")
    return "#%02x%02x%02x" % tuple(int(int(a[i:i + 2], 16) * (1 - t) + int(b[i:i + 2], 16) * t) for i in (0, 2, 4))


def yaprak_tonlari(yp):
    """Yaprak renginden dört ton: Y orta, y koyu (küme altı), a açık sarımsı (ışık), m gölge yanı (yeşilde mavimsi;
    sonbahar gibi sıcak renkte kızıl: aynı ağaçta sarı-turuncu-kızıl karışır). C#: AtolyeKurucu.YaprakTonlari."""
    h = yp.lstrip("#")
    sicak = int(h[0:2], 16) > int(h[2:4], 16)
    return dict(Y=yp, y=koyu(yp, 0.88), a=karis(yp, "#f6ecaa", 0.32),
                m=karis(koyu(yp, 0.92), "#c0503a", 0.42) if sicak else karis(koyu(yp, 0.9), "#5f8fa8", 0.24))


def cer(o, n, L):
    return C(o, (-n[1], n[0]), n, L)


def on():
    """Hücrenin ön (−Y) yüzünün cephesi: u soldan sağa 0…HUCRE, o dışarı."""
    return cer((-A, -A), (0, -1), HUCRE)


def meyve(g, x, y, z, r, mat, n=6, rz=None):
    rz = rz or r
    alt, ust = [], []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        alt.append((x + math.cos(a) * r, y + math.sin(a) * r, z))
        ust.append((x + math.cos(a) * r * 0.58, y + math.sin(a) * r * 0.58, z + rz * 0.78))
    kati(g, alt + ust, [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)] + [list(range(n, 2 * n))], mat, ic=(x, y, z + rz * 0.3))


# ================================== PARÇALAR ==================================
PARCA = {}          # ad -> üretici
PARCA_BILGI = {}    # ad -> (grup, açıklama, hedef)
PARCA_HAFIF = {}    # ad -> uzak görünüm üreticisi (yoksa küçük katılar atılarak türetilir)


def parca(grup, aciklama, hedef=""):
    """hedef: çevre modeli tek başına prefab kaydedilirken gideceği havuz ("agac", "cali", "donati:<tür>")."""
    def sar(fn):
        PARCA[fn.__name__] = fn
        PARCA_BILGI[fn.__name__] = (grup, aciklama, hedef)
        return fn
    return sar


def ekle(ad, grup, aciklama, fn, hedef="", hafif=None):
    PARCA[ad] = fn
    PARCA_BILGI[ad] = (grup, aciklama, hedef)
    if hafif:
        PARCA_HAFIF[ad] = hafif


def panel(g, c, mat=None, z0=0.0, z1=KAT):
    """Duvar panelinin gövdesi (hücrenin içine DK kadar)."""
    c.kutu(g, 0, HUCRE, -DK, 0, z0, z1, mat or D, arka=True)


def pencere(g, c, u0, u1, z0, z1, kemer=False, bolme=1, yatay=True, cam=CAM, t=0.12):
    """Pencere: gök mavisi cam (çerçevenin 8 cm gerisinde: pervaz derinliği), kalın beyaz çerçeve, artı biçimli kayıt, denizlik."""
    dc = 0.1                                       # çerçevenin duvardan çıkması
    c.kutu(g, u0, u1, 0, 0.015, z0, z1, cam)
    c.kutu(g, u0 - t, u0, 0, dc, z0 - t, z1 + (0 if kemer else t), SILME)
    c.kutu(g, u1, u1 + t, 0, dc, z0 - t, z1 + (0 if kemer else t), SILME)
    c.kutu(g, u0, u1, 0, dc, z0 - t, z0, SILME)
    if kemer:
        r = (u1 - u0) / 2
        um = (u0 + u1) / 2
        c.yarim_disk(g, um, z1, r, 0, 0.015, cam, n=8)
        n = 6
        R = r + t / 2
        for i in range(n):
            a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
            cubuk(g, c.p(um + R * math.cos(a0), dc / 2, z1 + R * math.sin(a0)), c.p(um + R * math.cos(a1), dc / 2, z1 + R * math.sin(a1)), t, SILME, dc)
    else:
        c.kutu(g, u0, u1, 0, dc, z1, z1 + t, SILME)
    for i in range(1, bolme + 1):
        uu = u0 + (u1 - u0) * i / (bolme + 1)
        c.kutu(g, uu - 0.03, uu + 0.03, 0.015, 0.06, z0, z1, SILME)
    if yatay:
        zz = (z0 + z1) / 2 if bolme == 1 else z0 + (z1 - z0) * 0.62
        c.kutu(g, u0, u1, 0.015, 0.06, zz - 0.03, zz + 0.03, SILME)
    c.kutu(g, u0 - t - 0.07, u1 + t + 0.07, 0, 0.17, z0 - t - 0.07, z0 - t, SILME2)       # denizlik


def cicek_kutusu(g, c, u0, u1, z):
    c.kutu(g, u0, u1, 0.05, 0.3, z - 0.26, z - 0.04, AHSAP)
    n = max(3, int((u1 - u0) / 0.2))
    for i in range(n):
        p = c.p(u0 + 0.09 + (u1 - u0 - 0.18) * i / (n - 1), 0.175, z - 0.04)
        meyve(g, p[0], p[1], p[2], 0.12, YAPRAK)
        meyve(g, p[0], p[1], p[2] + 0.08, 0.065, CICEK[i % len(CICEK)], n=5)


# ---------------------------------------------------------------- duvarlar: pastel apartman

@parca("duvar", "Düz duvar")
def duvar_duz(g):
    panel(g, on())


@parca("duvar", "Tek pencere")
def duvar_pencere(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.85, 1.95, 0.95, 2.45)


@parca("duvar", "Çift pencere")
def duvar_cift(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.4, 1.2, 0.95, 2.45, bolme=0)
    pencere(g, c, 1.6, 2.4, 0.95, 2.45, bolme=0)


@parca("duvar", "Kemerli pencere")
def duvar_kemerli(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.9, 1.9, 0.95, 2.0, kemer=True)


@parca("duvar", "Fransız balkonlu kapı-pencere")
def duvar_balkon(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.8, 2.0, 0.25, 2.5, bolme=1)
    c.kutu(g, 0.5, 2.3, 0, 0.55, 0.08, 0.18, SILME2)                     # balkon tablası
    for uu in (0.52, 2.28):
        c.kutu(g, uu - 0.02, uu + 0.02, 0.5, 0.54, 0.18, 1.1, DEMIR)
    c.kutu(g, 0.5, 2.3, 0.5, 0.54, 1.06, 1.1, DEMIR)
    n = 9
    for i in range(1, n):
        uu = 0.5 + 1.8 * i / n
        c.kutu(g, uu - 0.012, uu + 0.012, 0.51, 0.53, 0.18, 1.06, DEMIR)
    for uu in (0.52, 2.28):
        c.kutu(g, uu - 0.02, uu + 0.02, 0.02, 0.5, 1.06, 1.1, DEMIR)


@parca("duvar", "Saksılı pencere")
def duvar_saksi(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.85, 1.95, 1.0, 2.45)
    cicek_kutusu(g, c, 0.75, 2.05, 0.9)


@parca("duvar", "Panjurlu pencere")
def duvar_panjur(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.95, 1.85, 0.95, 2.45)
    for a, b in ((0.42, 0.84), (1.96, 2.38)):
        c.kutu(g, a, b, 0, 0.05, 0.9, 2.5, VR)
        for i in range(1, 7):
            zz = 0.9 + 1.6 * i / 7
            c.kutu(g, a + 0.04, b - 0.04, 0.05, 0.065, zz - 0.02, zz + 0.02, SILME2)


@parca("duvar", "Yuvarlak pencere")
def duvar_yuvarlak(g):
    c = on()
    panel(g, c)
    p = c.p(1.4, 0.0, 1.75)
    silindir(g, (p[0], p[1], p[2]), (p[0], p[1] - 0.07, p[2]), 0.56, 0.56, 12, SILME)
    silindir(g, (p[0], p[1] - 0.07, p[2]), (p[0], p[1] - 0.085, p[2]), 0.45, 0.45, 12, CAM)
    c.kutu(g, 1.375, 1.425, 0.085, 0.1, 1.3, 2.2, SILME)
    c.kutu(g, 0.95, 1.85, 0.085, 0.1, 1.725, 1.775, SILME)


@parca("duvar", "İki dar pencere")
def duvar_dar(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.55, 1.05, 0.8, 2.5, bolme=0)
    pencere(g, c, 1.75, 2.25, 0.8, 2.5, bolme=0)


@parca("duvar", "Geniş atölye penceresi")
def duvar_genis(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.45, 2.35, 0.9, 2.5, bolme=2)


# ---------------------------------------------------------------- duvarlar: yarı ahşap (fachwerk)

def _kiris(g, c, noktalar, t=0.13):
    """Ahşap kirişler: (u0, z0, u1, z1) çizgileri, duvarın önünde."""
    for u0, z0, u1, z1 in noktalar:
        cubuk(g, c.p(u0, 0.03, z0), c.p(u1, 0.03, z1), t, AHSAP, 0.06)


def _fachwerk_cerceve(g, c):
    panel(g, c, SIVA)
    _kiris(g, c, [(0.065, 0, 0.065, KAT), (HUCRE - 0.065, 0, HUCRE - 0.065, KAT), (0, 0.065, HUCRE, 0.065), (0, KAT - 0.065, HUCRE, KAT - 0.065)])


@parca("fachwerk", "Yarı ahşap: çapraz kirişli")
def fachwerk_capraz(g):
    c = on()
    _fachwerk_cerceve(g, c)
    _kiris(g, c, [(1.4, 0.13, 1.4, KAT - 0.13), (0.13, 0.13, 1.33, KAT - 0.13), (HUCRE - 0.13, 0.13, 1.47, KAT - 0.13)], 0.11)


@parca("fachwerk", "Yarı ahşap: pencereli")
def fachwerk_pencere(g):
    c = on()
    _fachwerk_cerceve(g, c)
    _kiris(g, c, [(0.75, 0.13, 0.75, KAT - 0.13), (2.05, 0.13, 2.05, KAT - 0.13), (0.75, 0.82, 2.05, 0.82), (0.75, 2.5, 2.05, 2.5),
                  (0.13, 0.13, 0.69, 0.76), (HUCRE - 0.13, 0.13, 2.11, 0.76)], 0.11)
    c.kutu(g, 0.82, 1.98, 0, 0.03, 0.89, 2.43, CAM)
    c.kutu(g, 1.375, 1.425, 0.03, 0.05, 0.89, 2.43, SILME)
    c.kutu(g, 0.82, 1.98, 0.03, 0.05, 1.63, 1.68, SILME)


@parca("fachwerk", "Yarı ahşap: X kirişli")
def fachwerk_x(g):
    c = on()
    _fachwerk_cerceve(g, c)
    _kiris(g, c, [(0.13, 0.13, HUCRE - 0.13, KAT - 0.13), (0.13, KAT - 0.13, HUCRE - 0.13, 0.13), (0, 1.5, HUCRE, 1.5)], 0.11)


@parca("fachwerk", "Yarı ahşap: saksılı pencere")
def fachwerk_saksi(g):
    c = on()
    _fachwerk_cerceve(g, c)
    _kiris(g, c, [(0.75, 0.13, 0.75, KAT - 0.13), (2.05, 0.13, 2.05, KAT - 0.13), (0.75, 0.95, 2.05, 0.95), (0.75, 2.5, 2.05, 2.5)], 0.11)
    c.kutu(g, 0.82, 1.98, 0, 0.03, 1.02, 2.43, CAM)
    c.kutu(g, 1.375, 1.425, 0.03, 0.05, 1.02, 2.43, SILME)
    cicek_kutusu(g, c, 0.8, 2.0, 1.0)


# ---------------------------------------------------------------- duvarlar: taş

def _tas_orgu(g, c, rnd, z0=0.0, z1=KAT):
    panel(g, c, TAS, z0, z1)
    for _ in range(9):
        u = rnd.uniform(0.15, HUCRE - 0.75)
        z = z0 + rnd.uniform(0.15, z1 - z0 - 0.5)
        c.kutu(g, u, u + rnd.uniform(0.4, 0.62), 0, 0.03, z, z + rnd.uniform(0.24, 0.34), rnd.choice((TAS2, TAS3)))


@parca("tas", "Taş duvar")
def tas_duvar(g):
    _tas_orgu(g, on(), random.Random(3))


@parca("tas", "Taş duvar: mazgal pencere")
def tas_mazgal(g):
    c = on()
    _tas_orgu(g, c, random.Random(5))
    c.kutu(g, 1.22, 1.58, 0, 0.05, 0.95, 2.35, TAS3)
    c.kutu(g, 1.31, 1.49, 0.05, 0.06, 1.05, 2.25, DEMIR)


@parca("tas", "Taş duvar: kemerli pencere")
def tas_kemerli(g):
    c = on()
    _tas_orgu(g, c, random.Random(7))
    pencere(g, c, 0.95, 1.85, 1.0, 1.95, kemer=True, bolme=1, yatay=False, cam=CAM2)


# ---------------------------------------------------------------- zemin kat

def _kapi_kanadi(g, c, u0, u1, z1, mat):
    c.kutu(g, u0, u1, 0, 0.04, 0.12, z1, mat)
    w = u1 - u0
    c.kutu(g, u0 + 0.1, u1 - 0.1, 0.04, 0.06, 0.25, 0.25 + (z1 - 0.4) * 0.42, D2 if mat == SILME else AHSAP2 if mat == AHSAP else SILME2)
    c.kutu(g, u0 + 0.1, u1 - 0.1, 0.04, 0.06, 0.25 + (z1 - 0.4) * 0.5, z1 - 0.15, AHSAP2 if mat == AHSAP else SILME2)
    c.kutu(g, u1 - 0.2, u1 - 0.12, 0.04, 0.1, 0.12 + z1 * 0.42, 0.12 + z1 * 0.48, PIRINC)
    return w


@parca("zemin", "Ahşap kapı (sundurmalı)")
def kapi_ahsap(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.72, 2.08, 0, 0.09, 0.0, 2.42, SILME)
    _kapi_kanadi(g, c, 0.86, 1.94, 2.28, VR)
    c.kutu(g, 0.6, 2.2, 0, 0.5, 0.0, 0.12, TAS)                               # eşik basamağı
    c.kutu(g, 0.55, 2.25, 0, 0.6, 2.5, 2.58, VR)                               # sundurma
    for uu in (0.62, 2.18):
        cubuk(g, c.p(uu, 0.02, 2.2), c.p(uu, 0.55, 2.5), 0.05, AHSAP2)
    p = c.p(2.38, 0.16, 1.9)                                                    # fener
    kutu(g, p[0] - 0.07, p[0] + 0.07, p[1] - 0.07, p[1] + 0.07, 1.9, 2.1, ISIK, atla=())
    kutu(g, p[0] - 0.09, p[0] + 0.09, p[1] - 0.09, p[1] + 0.09, 2.1, 2.14, DEMIR, atla=())
    c.kutu(g, 2.36, 2.4, 0, 0.16, 2.14, 2.18, DEMIR)


@parca("zemin", "Kemerli kapı")
def kapi_kemerli(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.78, 0.9, 0, 0.1, 0.0, 1.75, TAS3)
    c.kutu(g, 1.9, 2.02, 0, 0.1, 0.0, 1.75, TAS3)
    n = 7
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        cubuk(g, c.p(1.4 + 0.56 * math.cos(a0), 0.05, 1.75 + 0.56 * math.sin(a0)), c.p(1.4 + 0.56 * math.cos(a1), 0.05, 1.75 + 0.56 * math.sin(a1)), 0.13, TAS3, 0.1)
    c.kutu(g, 0.9, 1.9, 0, 0.04, 0.1, 1.75, AHSAP)
    c.yarim_disk(g, 1.4, 1.75, 0.5, 0, 0.04, AHSAP, n=8)
    c.kutu(g, 1.385, 1.415, 0.04, 0.055, 0.1, 2.2, AHSAP2)
    for zz in (0.5, 1.5):
        c.kutu(g, 0.9, 1.9, 0.04, 0.06, zz, zz + 0.07, DEMIR)
    c.kutu(g, 0.7, 2.1, 0, 0.45, 0.0, 0.1, TAS)


@parca("zemin", "Dükkân vitrini")
def vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.09, 0.0, 0.5, VR)                       # kaide panosu
    c.kutu(g, 0.27, HUCRE - 0.27, 0.09, 0.11, 0.1, 0.4, SILME2)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.02, 0.5, 2.15, VIT)
    for uu in (0.15, 1.34, HUCRE - 0.27):
        c.kutu(g, uu, uu + 0.12, 0, 0.1, 0.5, 2.15, SILME)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.1, 2.15, 2.28, SILME)
    c.kutu(g, 0.27, HUCRE - 0.27, 0.02, 0.06, 1.62, 1.68, SILME)


@parca("zemin", "Geniş vitrin (tek cam)")
def vitrin_genis(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.09, 0.0, 0.3, VR)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.02, 0.3, 2.15, VIT)
    for uu in (0.1, HUCRE - 0.2):
        c.kutu(g, uu, uu + 0.1, 0, 0.1, 0.3, 2.15, SILME)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.1, 2.15, 2.28, SILME)
    c.kutu(g, 0.5, 1.1, 0.02, 0.03, 0.9, 1.5, PM("Afis", "#f5c84a"))             # camda kampanya kâğıdı
    c.kutu(g, 0.56, 1.04, 0.03, 0.04, 1.25, 1.38, VR)


@parca("zemin", "Vitrinli cam kapı")
def kapi_vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.15, 1.2, 0, 0.09, 0.0, 0.5, VR)
    c.kutu(g, 0.15, 1.2, 0, 0.02, 0.5, 2.15, VIT)
    c.kutu(g, 0.15, 0.27, 0, 0.1, 0.5, 2.15, SILME)
    c.kutu(g, 1.2, 1.32, 0, 0.11, 0.0, 2.28, SILME)
    c.kutu(g, 2.5, 2.62, 0, 0.11, 0.0, 2.28, SILME)
    c.kutu(g, 0.15, 2.62, 0, 0.11, 2.15, 2.28, SILME)
    c.kutu(g, 1.32, 2.5, 0, 0.02, 0.12, 2.15, VIT)
    c.kutu(g, 1.32, 2.5, 0.02, 0.06, 0.12, 0.45, VR)
    c.kutu(g, 1.32, 2.5, 0.02, 0.06, 1.1, 1.17, VR)
    c.kutu(g, 2.3, 2.38, 0.03, 0.1, 1.0, 1.3, PIRINC)
    c.kutu(g, 1.2, 2.62, 0, 0.4, 0.0, 0.12, TAS)


@parca("zemin", "Otomatik cam kapı (market)")
def kapi_otomatik(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.25, 0.37, 0, 0.11, 0.0, 2.28, SILME)
    c.kutu(g, HUCRE - 0.37, HUCRE - 0.25, 0, 0.11, 0.0, 2.28, SILME)
    c.kutu(g, 0.25, HUCRE - 0.25, 0, 0.11, 2.1, 2.28, PM("Metal", "#9aa3ab"))
    c.kutu(g, 0.37, HUCRE - 0.37, 0, 0.02, 0.05, 2.1, VIT)
    c.kutu(g, 1.37, 1.43, 0.02, 0.05, 0.05, 2.1, PM("Metal", "#9aa3ab"))
    for uu in (0.8, 2.0):
        c.kutu(g, uu - 0.25, uu + 0.25, 0.02, 0.03, 1.15, 1.25, VR)             # camdaki uyarı şeridi
    c.kutu(g, 0.2, HUCRE - 0.2, 0, 0.5, 0.0, 0.05, PM("Paspas", "#7d7f8c"))


@parca("zemin", "Tente (çizgili)")
def tente(g):
    c = on()
    n = 7
    for i in range(n):
        a, b = 0.06 + (HUCRE - 0.12) * i / n, 0.06 + (HUCRE - 0.12) * (i + 1) / n
        m = TN if i % 2 == 0 else SILME
        pts = [c.p(a, 0.1, 2.3), c.p(b, 0.1, 2.3), c.p(b, 1.0, 1.98), c.p(a, 1.0, 1.98)]
        q = [(p[0], p[1], p[2] - 0.035) for p in pts]
        kati(g, pts + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], m)
        c.kutu(g, a, b, 0.98, 1.0, 1.8, 1.98, m)
    for uu in (0.08, HUCRE - 0.08):
        cubuk(g, c.p(uu, 0.05, 1.9), c.p(uu, 0.95, 1.99), 0.03, DEMIR)


@parca("zemin", "Tabela bandı (cephe)")
def tabela_bant(g):
    c = on()
    c.kutu(g, 0.0, HUCRE, 0, 0.13, 2.3, 2.78, VR)
    c.kutu(g, 0.0, HUCRE, 0, 0.16, 2.78, 2.84, SILME)
    c.kutu(g, 0.0, HUCRE, 0, 0.16, 2.26, 2.3, SILME)


@parca("zemin", "Zemin kat penceresi (parmaklıklı)")
def zemin_pencere(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.8, 2.0, 0.95, 2.05, bolme=1, yatay=False)
    for i in range(6):
        uu = 0.86 + 1.08 * i / 5
        c.kutu(g, uu - 0.012, uu + 0.012, 0.1, 0.12, 0.95, 2.05, DEMIR)
    c.kutu(g, 0.8, 2.0, 0.1, 0.12, 1.48, 1.51, DEMIR)


@parca("zemin", "Garaj / depo kapısı")
def kapi_garaj(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.3, 2.5, 0, 0.08, 0.0, 2.4, SILME)
    c.kutu(g, 0.42, 2.38, 0.02, 0.06, 0.05, 2.28, VR)
    for i in range(1, 6):
        zz = 0.05 + 2.23 * i / 6
        c.kutu(g, 0.42, 2.38, 0.06, 0.075, zz - 0.02, zz + 0.02, SILME2)


# ---------------------------------------------------------------- köşeler, bantlar, kaide

@parca("kose", "Dış köşe: beyaz pilastr")
def kose_dis(g):
    kutu(g, -A - 0.05, -A + 0.2, -A - 0.05, -A + 0.2, 0, KAT, SILME)


@parca("kose", "Dış köşe: dişli köşe taşları")
def kose_tas(g):
    z, i = 0.0, 0
    while z < KAT - 0.01:
        w = 0.42 if i % 2 == 0 else 0.28
        kutu(g, -A - 0.04, -A + w, -A - 0.04, -A + (0.7 - w), z + 0.02, z + 0.36, TAS3, atla=())
        z += 0.375
        i += 1


@parca("kose", "Dış köşe: ahşap dikme")
def kose_ahsap(g):
    kutu(g, -A - 0.04, -A + 0.16, -A - 0.04, -A + 0.16, 0, KAT, AHSAP)


@parca("kose", "İç köşe dolgusu")
def kose_ic(g):
    kutu(g, -A, -A + DK, -A, -A + DK, 0, KAT, D)


@parca("bant", "Kat silmesi (beyaz)")
def silme(g):
    c = on()
    c.kutu(g, -0.06, HUCRE + 0.06, 0, 0.09, -0.12, 0.0, SILME)
    c.kutu(g, -0.1, HUCRE + 0.1, 0, 0.14, -0.045, 0.0, SILME2)


@parca("bant", "Kat kirişi (ahşap)")
def silme_ahsap(g):
    c = on()
    c.kutu(g, -0.06, HUCRE + 0.06, 0, 0.1, -0.16, 0.02, AHSAP2)
    for i in range(6):
        uu = 0.2 + i * 0.48
        c.kutu(g, uu, uu + 0.12, 0.1, 0.2, -0.16, -0.02, AHSAP)                 # kiriş başları


@parca("bant", "Taş kaide (zemin)")
def kaide(g):
    c = on()
    c.kutu(g, -0.07, HUCRE + 0.07, 0, 0.08, 0.0, 0.42, TAS2)
    c.kutu(g, -0.09, HUCRE + 0.09, 0, 0.1, 0.42, 0.5, TAS3)


@parca("bant", "Alt döşeme (boşluk üstü)")
def taban(g):
    kutu(g, -A, A, -A, A, -0.18, 0.0, SILME2, atla=())


@parca("kemer", "Kemerli geçit yüzü (altı boş)")
def kemer(g):
    c = on()
    r = 1.05
    zc = KAT - 0.18 - r - 0.25
    # kemerin üstü: ortada yarım daire boşluk bırakan dilimler
    n = 8
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        u0, u1 = 1.4 + r * math.cos(a1), 1.4 + r * math.cos(a0)
        zalt = zc + r * min(math.sin(a0), math.sin(a1))
        c.kutu(g, u0, u1, -0.3, 0, zalt, KAT - 0.18, TAS, arka=True)
        cubuk(g, c.p(1.4 + r * math.cos(a0), 0.02, zc + r * math.sin(a0)), c.p(1.4 + r * math.cos(a1), 0.02, zc + r * math.sin(a1)), 0.16, TAS3, 0.36)
    for a, b in ((0.0, 1.4 - r), (1.4 + r, HUCRE)):
        c.kutu(g, a, b, -0.3, 0, 0.0, KAT - 0.18, TAS, arka=True)
    c.kutu(g, 1.28, 1.52, 0, 0.07, zc + r - 0.02, zc + r + 0.3, TAS2)             # kilit taşı


@parca("kemer", "Geçit ayağı (köşe)")
def kemer_ayak(g):
    kutu(g, -A - 0.04, -A + 0.42, -A - 0.04, -A + 0.42, 0, KAT - 0.18, TAS)
    kutu(g, -A - 0.08, -A + 0.46, -A - 0.08, -A + 0.46, 0, 0.3, TAS2)
    kutu(g, -A - 0.08, -A + 0.46, -A - 0.08, -A + 0.46, KAT - 0.42, KAT - 0.3, TAS3, atla=())


# ---------------------------------------------------------------- çatı karoları (yarım hücre, 1,4 × 1,4; köşe yükseklikleri 0 / 1 m)
# Kök: karonun (0, 0) köşesi, x ve y 0…1,4. Birleştirici döndürür, z'yi çatı eğimine göre ölçekler ve katına kaldırır.

def _cati_yuzu(g, h00, h10, h11, h01, capraz, sira=None):
    a, b, c_, d = (0, 0, h00), (A, 0, h10), (A, A, h11), (0, A, h01)
    ucgenler = [(a, b, c_), (a, c_, d)] if capraz == 0 else [(a, b, d), (b, c_, d)]
    for t in ucgenler:
        alt = [(p[0], p[1], p[2] - 0.14) for p in t]
        kati(g, list(t) + alt, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], [CT, None, CT2, CT2, CT2])


@parca("cati", "Çatı karosu: eğim (saçaktan yukarı)")
def cati_egim(g):
    _cati_yuzu(g, 0, 0, 1, 1, 0)
    for i in range(1, 4):                        # kiremit sıraları
        y = A * i / 4
        z = i / 4.0
        obox(g, (A / 2, y, z + 0.03), (A / 2, 0, 0), (0, 0.05, 0.036), (0, -0.012, 0.017), CT2)


@parca("cati", "Çatı karosu: dış köşe (kırma)")
def cati_kose(g):
    _cati_yuzu(g, 0, 0, 1, 0, 0)
    cubuk(g, (0, 0, 0.04), (A, A, 1.04), 0.12, CT2, 0.09)


@parca("cati", "Çatı karosu: iç köşe (vadi)")
def cati_vadi(g):
    _cati_yuzu(g, 0, 1, 1, 1, 0)


@parca("cati", "Çatı karosu: semer")
def cati_semer(g):
    _cati_yuzu(g, 1, 0, 1, 0, 1)


@parca("cati", "Çatı karosu: düz tepe")
def cati_duz(g):
    kutu(g, 0, A, 0, A, -0.14, 0.0, CT, atla=("zn",))


@parca("cati", "Saçak (çatı eteği)")
def sacak(g):
    c = on()
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.26, -0.1, 0.02, SILME)
    c.kutu(g, -0.02, HUCRE + 0.02, 0.2, 0.3, -0.03, 0.07, CT2)
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.12, -0.24, -0.1, SILME2)


@parca("cati", "Saçak köşesi")
def sacak_kose(g):
    kutu(g, -A - 0.3, -A, -A - 0.3, -A, -0.1, 0.07, CT2, atla=())
    kutu(g, -A - 0.26, -A, -A - 0.26, -A, -0.24, -0.1, SILME2, atla=())


@parca("cati", "Düz çatı (teras)")
def duz_cati(g):
    kutu(g, -A, A, -A, A, 0.0, 0.08, PM("Teras", "#d9d2c4"))
    for i in range(2):
        for j in range(2):
            if (i + j) % 2 == 0:
                kutu(g, -A + i * A + 0.04, -A + (i + 1) * A - 0.04, -A + j * A + 0.04, -A + (j + 1) * A - 0.04, 0.08, 0.1, PM("Teras", "#cbc3b3"))


@parca("cati", "Korkuluk duvarı (parapet)")
def korkuluk(g):
    c = on()
    c.kutu(g, 0, HUCRE, -DK, 0, 0.0, 0.75, D, arka=True)
    c.kutu(g, -0.05, HUCRE + 0.05, -DK - 0.04, 0.06, 0.75, 0.85, SILME, arka=True)


@parca("cati", "Demir korkuluk")
def korkuluk_demir(g):
    c = on()
    c.kutu(g, 0, HUCRE, -0.1, -0.05, 0.95, 1.0, DEMIR, arka=True)
    c.kutu(g, 0, HUCRE, -0.1, -0.05, 0.1, 0.14, DEMIR, arka=True)
    for i in range(13):
        uu = 0.03 + (HUCRE - 0.06) * i / 12
        c.kutu(g, uu - 0.014, uu + 0.014, -0.09, -0.06, 0.0, 0.95, DEMIR, arka=True)


@parca("cati", "Kale daması (mazgallı korkuluk)")
def dama(g):
    c = on()
    c.kutu(g, 0, HUCRE, -0.3, 0.06, 0.0, 0.5, TAS, arka=True)
    c.kutu(g, -0.04, HUCRE + 0.04, -0.3, 0.12, -0.14, 0.0, TAS2, arka=True)
    for i in range(3):
        uu = 0.14 + i * 0.94
        c.kutu(g, uu, uu + 0.64, -0.3, 0.06, 0.5, 1.0, TAS, arka=True)
        c.kutu(g, uu - 0.03, uu + 0.67, -0.33, 0.09, 1.0, 1.08, TAS3, arka=True)


@parca("cati", "Kule tepesi: sivri külah")
def kule_sivri(g):
    e = A + 0.28
    kutu(g, -e, e, -e, e, -0.12, 0.06, SILME, atla=())
    kati(g, [(-e, -e, 0.06), (e, -e, 0.06), (e, e, 0.06), (-e, e, 0.06), (0, 0, 5.2)], [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], CT)
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        cubuk(g, (sx * e, sy * e, 0.1), (0, 0, 5.24), 0.1, CT2, 0.08)
    for i in range(1, 5):
        t = i / 5.0
        ee = e * (1 - t) + 0.02
        kutu(g, -ee, ee, -ee, ee, 0.06 + 5.14 * t - 0.02, 0.06 + 5.14 * t + 0.03, CT2, atla=())
    cubuk(g, (0, 0, 5.1), (0, 0, 5.9), 0.05, DEMIR)
    meyve(g, 0, 0, 5.55, 0.11, PIRINC)
    kati(g, [(0, 0, 5.72), (0, 0, 5.92), (0.42, 0, 5.82)], [[0, 1, 2], [2, 1, 0]], VR)


@parca("cati", "Kule tepesi: soğan kubbe")
def kule_kubbe(g):
    e = A + 0.2
    kutu(g, -e, e, -e, e, -0.12, 0.06, SILME, atla=())
    profil = [(1.3, 0.06), (1.5, 0.7), (1.35, 1.5), (0.8, 2.3), (0.28, 2.9), (0.1, 3.6)]
    for (r0, z0), (r1, z1) in zip(profil, profil[1:]):
        dsil(g, 0, 0, z0, z1, r0, 8, CT, r1=r1, kapak=(False, False))
    dsil(g, 0, 0, 3.6, 3.62, 0.1, 8, CT)
    cubuk(g, (0, 0, 3.6), (0, 0, 4.2), 0.05, DEMIR)
    meyve(g, 0, 0, 4.1, 0.12, PIRINC)


# ---------------------------------------------------------------- çatı ayrıntıları ve aksesuarlar

@parca("ayrinti", "Baca (tuğla)")
def baca(g):
    tg = PM("Tugla", "#c98a72")
    kutu(g, -0.3, 0.3, -0.25, 0.25, -1.2, 1.5, tg)
    kutu(g, -0.36, 0.36, -0.31, 0.31, 1.5, 1.62, SILME, atla=())
    kutu(g, -0.32, 0.32, -0.27, 0.27, 1.25, 1.36, SILME, atla=())
    for sx in (-0.14, 0.14):
        dsil(g, sx, 0, 1.62, 1.95, 0.1, 6, PM("Tugla", "#b3745e"), kapak=(False, True), kapak_mat=DEMIR)


@parca("ayrinti", "Baca (ince, şapkalı)")
def baca_ince(g):
    kutu(g, -0.18, 0.18, -0.18, 0.18, -1.2, 1.8, SIVA)
    kutu(g, -0.24, 0.24, -0.24, 0.24, 1.8, 1.88, SILME2, atla=())
    kati(g, [(-0.3, -0.3, 2.05), (0.3, -0.3, 2.05), (0.3, 0.3, 2.05), (-0.3, 0.3, 2.05), (0, 0, 2.35)], [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], CT2)
    for sx, sy in ((-0.2, -0.2), (0.2, -0.2), (0.2, 0.2), (-0.2, 0.2)):
        cubuk(g, (sx, sy, 1.88), (sx, sy, 2.05), 0.04, DEMIR)


@parca("ayrinti", "Çatı penceresi")
def cati_penceresi(g):
    # kök: hücrenin ön kenarının ortası, saçak hizası; çatı eğimine oturur (öne bakar)
    kutu(g, -0.55, 0.55, 0.25, 1.6, 0.0, 1.25, D, atla=("zn", "yp"))
    prizma(g, [(-0.7, 0.12, 1.25), (0.7, 0.12, 1.25), (0, 0.12, 1.85)], (0, 1.6, 0), CT)
    kutu(g, -0.36, 0.36, 0.22, 0.25, 0.3, 1.05, CAM)
    for a, b, p, q in ((-0.44, -0.36, 0.22, 1.13), (0.36, 0.44, 0.22, 1.13), (-0.44, 0.44, 0.22, 0.3), (-0.44, 0.44, 1.05, 1.13), (-0.025, 0.025, 0.3, 1.05)):
        kutu(g, a, b, 0.19, 0.25, p, q, SILME, atla=())


@parca("ayrinti", "Dış merdiven (üst kata)")
def dis_merdiven(g):
    # hücrenin ön yüzü boyunca soldan sağa bir kat yükselir
    c = on()
    n = 12
    for i in range(n):
        u0 = 0.1 + (HUCRE - 0.9) * i / n
        c.kutu(g, u0, u0 + (HUCRE - 0.9) / n + 0.02, 0, 0.85, KAT * i / n, KAT * (i + 1) / n, TAS3 if i % 2 else TAS)
    c.kutu(g, HUCRE - 0.8, HUCRE, 0, 0.85, KAT - 0.2, KAT, TAS)                    # sahanlık
    c.kutu(g, HUCRE - 0.8, HUCRE - 0.6, 0.6, 0.85, 0.0, KAT - 0.2, TAS2)
    cubuk(g, c.p(0.1, 0.8, 0.95), c.p(HUCRE - 0.8, 0.8, KAT + 0.95), 0.05, DEMIR)
    cubuk(g, c.p(HUCRE - 0.8, 0.8, KAT + 0.95), c.p(HUCRE, 0.8, KAT + 0.95), 0.05, DEMIR)
    for i in range(0, n + 1, 3):
        uu = 0.1 + (HUCRE - 0.9) * i / n
        cubuk(g, c.p(uu, 0.8, KAT * i / n), c.p(uu, 0.8, KAT * i / n + 0.95), 0.04, DEMIR)
    cubuk(g, c.p(HUCRE - 0.02, 0.8, KAT), c.p(HUCRE - 0.02, 0.8, KAT + 0.95), 0.04, DEMIR)


@parca("ayrinti", "Geniş taş merdiven (bir hücre)")
def tas_merdiven(g):
    # hücreyi doldurur; önden (−Y) arkaya doğru bir kat (3 m) yükselir: taş kaidenin üstüne çıkar
    n = 10
    for i in range(n):
        y0 = -A + HUCRE * i / n
        kutu(g, -A + 0.18, A - 0.18, y0, A, KAT * i / n, KAT * (i + 1) / n, TAS3 if i % 2 else TAS)
    for sx in (-1, 1):
        x0, x1 = (A - 0.18, A) if sx > 0 else (-A, -A + 0.18)
        kutu(g, x0, x1, -A, A, 0.0, 0.55, TAS2)
        prizma(g, [(x0, -A, 0.55), (x0, A, 0.55), (x0, A, KAT + 0.55)], (x1 - x0, 0, 0), TAS2)
        kutu(g, x0 - 0.03, x1 + 0.03, -A - 0.03, -A + 0.3, 0.55, 0.8, TAS3, atla=())


@parca("ayrinti", "Fıçı")
def fici(g):
    for (r0, z0), (r1, z1) in (((0.3, 0.0), (0.36, 0.42)), ((0.36, 0.42), (0.3, 0.84))):
        dsil(g, 0, 0, z0, z1, r0, 10, AHSAP, r1=r1, kapak=(False, z1 > 0.5), kapak_mat=AHSAP2)
    for z in (0.12, 0.66):
        dsil(g, 0, 0, z, z + 0.05, 0.345, 10, DEMIR, kapak=(False, False))


@parca("ayrinti", "Paspas")
def paspas(g):
    kutu(g, -0.5, 0.5, -0.3, 0.3, 0.0, 0.03, PM("Paspas", "#b5754f"))
    kutu(g, -0.4, 0.4, -0.2, 0.2, 0.03, 0.036, PM("Paspas", "#d19a6c"))


@parca("ayrinti", "Saksı (çiçekli)")
def saksi(g):
    dsil(g, 0, 0, 0, 0.36, 0.2, 8, SAKSI, r1=0.27, kapak=(False, True), kapak_mat=AHSAP2)
    top(g, (0, 0, 0.62), 0.32, 0.32, 0.3, YAPRAK)
    for i in range(5):
        a = i * 1.26
        meyve(g, math.cos(a) * 0.2, math.sin(a) * 0.2, 0.74, 0.08, CICEK[i % len(CICEK)], n=5)


@parca("ayrinti", "Asma tabela")
def tabela(g):
    c = on()
    c.kutu(g, 1.36, 1.44, 0, 0.9, 2.6, 2.66, DEMIR)
    cubuk(g, c.p(1.4, 0.02, 2.25), c.p(1.4, 0.5, 2.62), 0.035, DEMIR)
    c.kutu(g, 1.37, 1.43, 0.25, 0.85, 1.95, 2.5, VR, arka=True)
    for s in (-1, 1):
        p = c.p(1.4 + s * 0.035, 0.55, 2.22)
        silindir(g, p, c.p(1.4 + s * 0.045, 0.55, 2.22), 0.16, 0.16, 8, SILME)


@parca("ayrinti", "Duvar feneri")
def fener(g):
    c = on()
    cubuk(g, c.p(1.4, 0.0, 2.25), c.p(1.4, 0.28, 2.25), 0.035, DEMIR)
    p = c.p(1.4, 0.28, 1.95)
    kutu(g, p[0] - 0.08, p[0] + 0.08, p[1] - 0.08, p[1] + 0.08, 1.95, 2.18, ISIK, atla=())
    kutu(g, p[0] - 0.11, p[0] + 0.11, p[1] - 0.11, p[1] + 0.11, 2.18, 2.22, DEMIR, atla=())


@parca("ayrinti", "Bank")
def bank(g):
    for sx in (-0.6, 0.6):
        kutu(g, sx - 0.04, sx + 0.04, -0.2, 0.2, 0, 0.42, DEMIR)
    kutu(g, -0.75, 0.75, -0.22, 0.2, 0.42, 0.48, AHSAP, atla=())
    kutu(g, -0.75, 0.75, 0.16, 0.22, 0.6, 0.9, AHSAP, atla=())


@parca("ayrinti", "Sarmaşık (duvarda)")
def sarmasik(g):
    c = on()
    rnd = random.Random(11)
    cubuk(g, c.p(0.3, 0.03, 0.0), c.p(0.42, 0.03, 1.6), 0.06, AHSAP2)
    for i in range(9):
        p = c.p(0.25 + rnd.uniform(0, 0.9), 0.06, 0.9 + i * 0.22 + rnd.uniform(-0.1, 0.1))
        top(g, p, 0.3, 0.12, 0.24, YAPRAK if i % 3 else PM("Yaprak", "#7aae6a"), rnd, 0.12)


# ---------------------------------------------------------------- kullanım türlerine özel cephe parçaları

@parca("zemin", "Çiçek kutusu (pencere altı)")
def cicek_kutusu_p(g):
    cicek_kutusu(g, on(), 0.75, 2.05, 0.82)


@parca("kamu", "Sütunlu cephe")
def sutunlu(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.95, 1.85, 0.9, 2.1)
    for uu in (0.25, HUCRE - 0.25):
        p = c.p(uu, 0.55, 0)
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 0.0, 0.2, TAS3)
        dsil(g, p[0], p[1], 0.2, 2.45, 0.15, 8, SILME, r1=0.13, kapak=(False, False))
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 2.45, 2.6, SILME, atla=())
    c.kutu(g, 0.0, HUCRE, 0, 0.8, 2.6, 2.95, SILME)
    c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.86, 2.95, 3.03, SILME2)
    c.kutu(g, 0.0, HUCRE, 0, 0.8, 0.0, 0.08, TAS)


@parca("kamu", "Sütunlu giriş (kapı + basamak)")
def kapi_sutunlu(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.8, 2.0, 0, 0.1, 0.0, 2.4, SILME)
    c.kutu(g, 0.92, 1.88, 0.02, 0.06, 0.25, 2.28, VR)
    c.kutu(g, 1.385, 1.415, 0.06, 0.075, 0.25, 2.28, SILME2)
    for uu in (1.1, 1.66):
        c.kutu(g, uu - 0.12, uu + 0.12, 0.06, 0.075, 1.3, 2.05, CAM)
    for uu in (0.25, HUCRE - 0.25):
        p = c.p(uu, 0.55, 0)
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 0.25, 0.42, TAS3)
        dsil(g, p[0], p[1], 0.42, 2.45, 0.15, 8, SILME, r1=0.13, kapak=(False, False))
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 2.45, 2.6, SILME, atla=())
    c.kutu(g, 0.0, HUCRE, 0, 0.8, 2.6, 2.95, SILME)
    c.prizma_uc(g, -0.05, HUCRE + 0.05, 0.1, 0.86, 2.95, 0.7, SILME2)
    for i in range(3):
        c.kutu(g, 0.0, HUCRE, 0, 0.8 + 0.3 * (2 - i), 0.085 * i, 0.085 * (i + 1), TAS3 if i % 2 else TAS)


@parca("kamu", "Cephe saati")
def saat_cephe(g):
    c = on()
    p = c.p(1.4, 0.0, 1.7)
    silindir(g, p, (p[0], p[1] - 0.1, p[2]), 0.8, 0.8, 12, VR)
    silindir(g, (p[0], p[1] - 0.1, p[2]), (p[0], p[1] - 0.13, p[2]), 0.66, 0.66, 12, SILME)
    c.kutu(g, 1.375, 1.425, 0.13, 0.15, 1.7, 2.2, DEMIR)
    c.kutu(g, 1.4, 1.75, 0.13, 0.15, 1.675, 1.725, DEMIR)
    for i in range(4):
        a = i * math.pi / 2
        q = c.p(1.4 + 0.55 * math.cos(a), 0.13, 1.7 + 0.55 * math.sin(a))
        kutu(g, q[0] - 0.04, q[0] + 0.04, q[1] - 0.015, q[1], q[2] - 0.04, q[2] + 0.04, DEMIR, atla=())


@parca("kamu", "Cephe artısı (hastane / eczane)")
def hac_cephe(g):
    c = on()
    c.kutu(g, 0.75, 2.05, 0, 0.08, 1.05, 2.35, SILME)
    c.kutu(g, 1.2, 1.6, 0.08, 0.13, 1.2, 2.2, VR)
    c.kutu(g, 0.9, 1.9, 0.08, 0.13, 1.5, 1.9, VR)


@parca("kamu", "Bayrak direği (çatıda)")
def bayrak(g):
    cubuk(g, (0, 0, -1.0), (0, 0, 3.6), 0.07, PM("Metal", "#c9ccd0"))
    meyve(g, 0, 0, 3.6, 0.09, PIRINC)
    kutu(g, 0.04, 1.25, -0.02, 0.02, 2.75, 3.5, VR, atla=())
    kutu(g, 0.04, 1.25, -0.025, 0.025, 3.02, 3.23, SILME, atla=())


@parca("sanayi", "Sac duvar (oluklu)")
def sac_duvar(g):
    c = on()
    panel(g, c, PM("Sac", "#b9c2c8"))
    for i in range(9):
        uu = 0.16 + i * 0.31
        c.kutu(g, uu - 0.05, uu + 0.05, 0, 0.035, 0.05, KAT - 0.05, PM("Sac", "#a3adb4"))


@parca("sanayi", "Açık kepenkli tamirhane ağzı")
def garaj_acik(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.25, HUCRE - 0.25, 0, 0.02, 0.0, 2.45, PM("Ic", "#5a5560"))
    c.kutu(g, 0.15, 0.27, 0, 0.1, 0.0, 2.55, SILME)
    c.kutu(g, HUCRE - 0.27, HUCRE - 0.15, 0, 0.1, 0.0, 2.55, SILME)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.14, 2.45, 2.62, PM("Metal", "#9aa3ab"))
    for i in range(4):
        c.kutu(g, 0.27, HUCRE - 0.27, 0.02, 0.07, 1.85 + i * 0.15, 1.97 + i * 0.15, VR if i % 2 else SILME2)
    c.kutu(g, 0.5, 1.3, 0.02, 0.5, 0.0, 0.5, PM("Metal", "#7d8590"))               # içeride tezgâh / kasa
    p = c.p(2.0, 0.3, 0)
    for k in range(3):
        silindir(g, (p[0], p[1], 0.12 + k * 0.22), (p[0], p[1], 0.3 + k * 0.22), 0.3, 0.3, 8, PM("Lastik", "#3d3b42"))


@parca("sanayi", "Sera camı")
def sera_cam(g):
    c = on()
    c.kutu(g, 0, HUCRE, -0.06, 0, 0.0, 0.45, TAS, arka=True)
    c.kutu(g, 0, HUCRE, -0.04, -0.02, 0.45, KAT, CAM, arka=True)
    for uu in (0.0, 0.9, 1.84, HUCRE - 0.06):
        c.kutu(g, uu, uu + 0.06, -0.06, 0.0, 0.45, KAT, SILME, arka=True)
    for zz in (1.7, KAT - 0.07):
        c.kutu(g, 0, HUCRE, -0.06, 0.0, zz, zz + 0.07, SILME, arka=True)


@parca("sanayi", "Fabrika bacası (çatıda)")
def baca_fabrika(g):
    tg = PM("Tugla", "#c98a72")
    dsil(g, 0, 0, -1.0, 6.0, 0.45, 8, tg, r1=0.32, kapak=(False, True), kapak_mat=DEMIR)
    for z, m in ((5.2, SILME), (5.55, PM("Tugla", "#c8553f")), (4.85, PM("Tugla", "#c8553f"))):
        dsil(g, 0, 0, z, z + 0.3, 0.38, 8, m, kapak=(True, True))


@parca("simge", "Büyük kemerli pencere")
def kemer_cam(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.4, 2.4, 0, 0.015, 0.35, 1.7, CAM)
    c.yarim_disk(g, 1.4, 1.7, 1.0, 0, 0.015, CAM, n=10)
    for uu in (0.28, 2.4):
        c.kutu(g, uu, uu + 0.12, 0, 0.1, 0.25, 1.7, SILME)
    c.kutu(g, 0.28, 2.52, 0, 0.1, 0.23, 0.35, SILME)
    n = 8
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        cubuk(g, c.p(1.4 + 1.06 * math.cos(a0), 0.05, 1.7 + 1.06 * math.sin(a0)), c.p(1.4 + 1.06 * math.cos(a1), 0.05, 1.7 + 1.06 * math.sin(a1)), 0.12, SILME, 0.1)
    c.kutu(g, 1.37, 1.43, 0.015, 0.06, 0.35, 2.7, SILME)
    c.kutu(g, 0.4, 2.4, 0.015, 0.06, 1.67, 1.73, SILME)
    for uu in (0.9, 1.9):
        c.kutu(g, uu - 0.025, uu + 0.025, 0.015, 0.05, 0.35, 1.7, SILME)


@parca("konut", "Cumba (bay window)")
def bay_window(g):
    c = on()
    panel(g, c)
    # üç yüzlü çıkma: ortada geniş, yanlarda eğik camlar
    pts = [c.p(0.45, 0, 0.5), c.p(0.9, 0.55, 0.5), c.p(1.9, 0.55, 0.5), c.p(2.35, 0, 0.5)]
    prizma(g, [(p[0], p[1], 0.0) for p in pts], (0, 0, 0.75), D)
    prizma(g, [(p[0], p[1], 0.75) for p in pts], (0, 0, 1.6), CAM)
    prizma(g, [(p[0], p[1], 2.35) for p in [c.p(0.38, 0, 0), c.p(0.86, 0.62, 0), c.p(1.94, 0.62, 0), c.p(2.42, 0, 0)]], (0, 0, 0.18), SILME)
    prizma(g, [(p[0], p[1], 0.68) for p in [c.p(0.38, 0, 0), c.p(0.86, 0.62, 0), c.p(1.94, 0.62, 0), c.p(2.42, 0, 0)]], (0, 0, 0.09), SILME)
    for a, b in ((pts[0], pts[1]), (pts[1], pts[2]), (pts[2], pts[3])):
        for q in (a, b):
            cubuk(g, (q[0], q[1], 0.77), (q[0], q[1], 2.35), 0.1, SILME)
    m = c.p(1.4, 0.56, 0)
    cubuk(g, (m[0], m[1], 0.77), (m[0], m[1], 2.35), 0.06, SILME)
    cubuk(g, c.p(0.92, 0.56, 1.6), c.p(1.88, 0.56, 1.6), 0.06, SILME)


@parca("konut", "Merdivenli kapı (korkuluklu)")
def kapi_merdivenli(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.82, 1.98, 0, 0.1, 0.5, 2.75, SILME)
    c.kutu(g, 0.95, 1.85, 0.02, 0.06, 0.5, 2.3, VR)
    c.kutu(g, 0.95, 1.85, 0.02, 0.04, 2.36, 2.65, CAM)
    c.kutu(g, 1.68, 1.75, 0.06, 0.11, 1.35, 1.45, PIRINC)
    c.kutu(g, 0.6, 2.2, 0, 0.8, 0.0, 0.5, TAS)
    for i in range(3):
        c.kutu(g, 0.7, 2.1, 0.8 + i * 0.28, 1.08 + i * 0.28, 0.0, 0.5 - (i + 1) * 0.125, TAS3 if i % 2 else TAS2)
    for uu in (0.63, 2.17):
        cubuk(g, c.p(uu, 0.05, 1.4), c.p(uu, 0.78, 1.4), 0.04, DEMIR)
        cubuk(g, c.p(uu, 0.78, 1.4), c.p(uu, 1.6, 0.9), 0.04, DEMIR)
        for oo, zz in ((0.78, 0.5), (1.6, 0.0)):
            cubuk(g, c.p(uu, oo, zz), c.p(uu, oo, zz + 0.9), 0.035, DEMIR)


@parca("cati", "Testere çatı dişi (sanayi)")
def cati_testere(g):
    cam = [(-A, A, 0.0), (A, A, 0.0), (A, A, 1.5), (-A, A, 1.5)]
    kati(g, [(-A, -A, 0.0), (A, -A, 0.0), (A, A, 0.0), (-A, A, 0.0), (-A, A, 1.5), (A, A, 1.5)],
         [[0, 1, 5, 4], [2, 3, 4, 5], [0, 3, 4], [1, 2, 5]], [CT, CAM, D, D])
    for i in range(1, 4):
        x = -A + HUCRE * i / 4
        cubuk(g, (x, A + 0.01, 0.05), (x, A + 0.01, 1.45), 0.05, SILME)


# ---------------------------------------------------------------- tabela simgeleri (bandın sol ucunda yuvarlak madalyon)

def _madalyon(g):
    c = on()
    p = c.p(1.4, 0.13, 2.54)
    silindir(g, p, (p[0], p[1] - 0.04, p[2]), 0.3, 0.3, 10, SILME)
    return c


def _simge(ad, aciklama):
    def sar(fn):
        def uret(g):
            fn(g, _madalyon(g))
        ekle("simge_" + ad, "simge", aciklama, uret)
        return fn
    return sar


@_simge("burger", "Simge: burger")
def _s_burger(g, c):
    c.kutu(g, 1.22, 1.58, 0.17, 0.2, 2.6, 2.7, PM("Simge", "#e0a04a"))
    c.kutu(g, 1.2, 1.6, 0.17, 0.21, 2.52, 2.59, PM("Simge", "#7a4a35"))
    c.kutu(g, 1.2, 1.6, 0.17, 0.2, 2.47, 2.515, PM("Simge", "#7fb86a"))
    c.kutu(g, 1.22, 1.58, 0.17, 0.2, 2.38, 2.46, PM("Simge", "#e0a04a"))


@_simge("pizza", "Simge: pizza dilimi")
def _s_pizza(g, c):
    c.prizma_uc(g, 1.2, 1.6, 0.17, 0.2, 2.72, -0.36, PM("Simge", "#f0c060"))
    c.kutu(g, 1.2, 1.6, 0.17, 0.21, 2.7, 2.75, PM("Simge", "#c98a4a"))
    for uu, zz in ((1.34, 2.62), (1.46, 2.56), (1.4, 2.48)):
        c.kutu(g, uu - 0.03, uu + 0.03, 0.2, 0.21, zz - 0.03, zz + 0.03, PM("Simge", "#c8453f"))


@_simge("fincan", "Simge: fincan")
def _s_fincan(g, c):
    c.kutu(g, 1.25, 1.52, 0.17, 0.2, 2.42, 2.62, VR)
    c.kutu(g, 1.52, 1.6, 0.17, 0.2, 2.5, 2.58, VR)
    c.kutu(g, 1.2, 1.57, 0.17, 0.2, 2.37, 2.4, VR)
    for uu in (1.32, 1.44):
        c.kutu(g, uu, uu + 0.03, 0.17, 0.2, 2.65, 2.73, PM("Simge", "#b5ada0"))


@_simge("balik", "Simge: balık")
def _s_balik(g, c):
    c.kutu(g, 1.25, 1.52, 0.17, 0.2, 2.48, 2.6, PM("Simge", "#5f8fb8"))
    c.prizma_uc(g, 1.5, 1.66, 0.17, 0.2, 2.54, 0.0001, PM("Simge", "#5f8fb8"))
    c.kutu(g, 1.52, 1.62, 0.17, 0.2, 2.45, 2.63, PM("Simge", "#5f8fb8"))
    c.kutu(g, 1.29, 1.32, 0.2, 0.21, 2.55, 2.58, DEMIR)


@_simge("hac", "Simge: artı (eczane / sağlık)")
def _s_hac(g, c):
    c.kutu(g, 1.34, 1.46, 0.17, 0.2, 2.36, 2.72, VR)
    c.kutu(g, 1.22, 1.58, 0.17, 0.2, 2.48, 2.6, VR)


@_simge("kitap", "Simge: kitap")
def _s_kitap(g, c):
    c.kutu(g, 1.2, 1.39, 0.17, 0.2, 2.42, 2.66, VR)
    c.kutu(g, 1.41, 1.6, 0.17, 0.2, 2.42, 2.66, VR)
    for zz in (2.5, 2.57):
        c.kutu(g, 1.23, 1.36, 0.2, 0.21, zz, zz + 0.02, SILME)
        c.kutu(g, 1.44, 1.57, 0.2, 0.21, zz, zz + 0.02, SILME)


@_simge("cicek", "Simge: çiçek")
def _s_cicek(g, c):
    c.kutu(g, 1.385, 1.415, 0.17, 0.2, 2.34, 2.56, PM("Simge", "#6fa968"))
    for du, dz in ((0, 0.09), (0.09, 0), (-0.09, 0), (0, -0.09)):
        c.kutu(g, 1.36 + du, 1.44 + du, 0.17, 0.2, 2.58 + dz, 2.66 + dz, PM("Simge", "#e8607a"))
    c.kutu(g, 1.37, 1.43, 0.2, 0.21, 2.59, 2.65, PM("Simge", "#f5c84a"))


@_simge("ekmek", "Simge: ekmek")
def _s_ekmek(g, c):
    c.kutu(g, 1.2, 1.6, 0.17, 0.2, 2.44, 2.62, PM("Simge", "#d9a05a"))
    c.kutu(g, 1.24, 1.56, 0.17, 0.2, 2.62, 2.67, PM("Simge", "#d9a05a"))
    for uu in (1.28, 1.39, 1.5):
        c.kutu(g, uu, uu + 0.03, 0.2, 0.21, 2.5, 2.62, PM("Simge", "#a8703a"))


@_simge("sepet", "Simge: alışveriş sepeti")
def _s_sepet(g, c):
    c.kutu(g, 1.22, 1.58, 0.17, 0.2, 2.42, 2.58, VR)
    c.kutu(g, 1.27, 1.3, 0.17, 0.2, 2.58, 2.7, VR)
    c.kutu(g, 1.5, 1.53, 0.17, 0.2, 2.58, 2.7, VR)
    c.kutu(g, 1.27, 1.53, 0.17, 0.2, 2.68, 2.71, VR)


@_simge("bardak", "Simge: bardak (pub)")
def _s_bardak(g, c):
    c.kutu(g, 1.27, 1.5, 0.17, 0.2, 2.38, 2.64, PM("Simge", "#e0a04a"))
    c.kutu(g, 1.25, 1.52, 0.17, 0.21, 2.64, 2.72, SILME2)
    c.kutu(g, 1.5, 1.58, 0.17, 0.2, 2.46, 2.6, PM("Simge", "#e0a04a"))


@_simge("et", "Simge: et (kasap)")
def _s_et(g, c):
    c.kutu(g, 1.22, 1.5, 0.17, 0.2, 2.44, 2.66, PM("Simge", "#c8453f"))
    c.kutu(g, 1.28, 1.44, 0.2, 0.21, 2.5, 2.6, PM("Simge", "#f0b0a8"))
    c.kutu(g, 1.5, 1.62, 0.17, 0.2, 2.52, 2.58, SILME2)


@_simge("yildiz", "Simge: yıldız (genel)")
def _s_yildiz(g, c):
    c.kutu(g, 1.3, 1.5, 0.17, 0.2, 2.44, 2.64, VR)
    p = c.p(1.4, 0.2, 2.54)
    dkare = 0.14
    obox(g, (p[0], p[1] - 0.005, p[2]), (dkare * 0.707, 0, dkare * 0.707), (0, 0.006, 0), (-dkare * 0.707, 0, dkare * 0.707), PM("Simge", "#f5c84a"))


# ---------------------------------------------------------------- piksel harfler (tabela yazısı; her harf bir parça)

HARF = {
    "A": ".###. #...# #...# ##### #...# #...# #...#", "B": "####. #...# #...# ####. #...# #...# ####.",
    "C": ".###. #...# #.... #.... #.... #...# .###.", "D": "####. #...# #...# #...# #...# #...# ####.",
    "E": "##### #.... #.... ####. #.... #.... #####", "F": "##### #.... #.... ####. #.... #.... #....",
    "G": ".###. #...# #.... #.### #...# #...# .###.", "H": "#...# #...# #...# ##### #...# #...# #...#",
    "I": "##### ..#.. ..#.. ..#.. ..#.. ..#.. #####", "J": "..### ...#. ...#. ...#. ...#. #..#. .##..",
    "K": "#...# #..#. #.#.. ##... #.#.. #..#. #...#", "L": "#.... #.... #.... #.... #.... #.... #####",
    "M": "#...# ##.## #.#.# #.#.# #...# #...# #...#", "N": "#...# ##..# #.#.# #..## #...# #...# #...#",
    "O": ".###. #...# #...# #...# #...# #...# .###.", "P": "####. #...# #...# ####. #.... #.... #....",
    "Q": ".###. #...# #...# #...# #.#.# #..#. .##.#", "R": "####. #...# #...# ####. #.#.. #..#. #...#",
    "S": ".#### #.... #.... .###. ....# ....# ####.", "T": "##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..",
    "U": "#...# #...# #...# #...# #...# #...# .###.", "V": "#...# #...# #...# #...# #...# .#.#. ..#..",
    "W": "#...# #...# #...# #.#.# #.#.# ##.## #...#", "X": "#...# #...# .#.#. ..#.. .#.#. #...# #...#",
    "Y": "#...# #...# .#.#. ..#.. ..#.. ..#.. ..#..", "Z": "##### ....# ...#. ..#.. .#... #.... #####",
    "0": ".###. #...# #..## #.#.# ##..# #...# .###.", "1": "..#.. .##.. ..#.. ..#.. ..#.. ..#.. .###.",
    "2": ".###. #...# ....# ...#. ..#.. .#... #####", "3": "####. ....# ....# .###. ....# ....# ####.",
    "4": "...#. ..##. .#.#. #..#. ##### ...#. ...#.", "5": "##### #.... ####. ....# ....# #...# .###.",
    "6": ".###. #.... #.... ####. #...# #...# .###.", "7": "##### ....# ...#. ..#.. ..#.. ..#.. ..#..",
    "8": ".###. #...# #...# .###. #...# #...# .###.", "9": ".###. #...# #...# .#### ....# ....# .###.",
    "&": ".##.. #..#. #.#.. .#... #.#.# #..#. .##.#", "'": "..#.. ..#.. ..... ..... ..... ..... .....",
    "-": "..... ..... ..... .###. ..... ..... .....", "/": "....# ....# ...#. ..#.. .#... #.... #....",
    ".": "..... ..... ..... ..... ..... ..... ..#..",
}
HARF_OZEL = {"&": "ve", "'": "kesme", "-": "tire", "/": "bolu", ".": "nokta"}


def harf_adi(ch):
    """Karakterin parça adı (boşluk ve bilinmeyenler için None). Scripts/World/Atolye/AtolyeKurucu.cs'teki HarfAdi ile aynı."""
    ch = ch.upper()
    if ch in HARF_OZEL:
        return "harf_" + HARF_OZEL[ch]
    if ch in HARF:
        return "harf_" + ch.lower()
    return None


def _harf_uret(sekil):
    def uret(g):
        # yerel: x 0…5, z 0…7 (piksel = 1 birim), y 0 (duvar) … −1 (dışarı); birleştirici piksel boyuna ölçekler
        for r, satir in enumerate(sekil.split(" ")):
            i = 0
            while i < 5:
                if satir[i] != "#":
                    i += 1
                    continue
                j = i
                while j < 5 and satir[j] == "#":
                    j += 1
                kutu(g, i, j, -1.0, 0.0, 6 - r, 7 - r, YAZI, atla=("yp",))
                i = j
    return uret


for _ch, _sekil in HARF.items():
    ekle(harf_adi(_ch), "harf", "Harf: " + _ch, _harf_uret(_sekil))


# ---------------------------------------------------------------- çevre: dükkân önü ve sokak dekorları (kök: tabanın ortası)

def _cevre(aciklama, hedef=""):
    return parca("cevre", aciklama, hedef)


for _ad in ("fici", "saksi", "bank", "paspas"):
    PARCA_BILGI[_ad] = ("cevre", PARCA_BILGI[_ad][1], {"bank": "donati:bank", "saksi": "donati:saksi"}.get(_ad, ""))


@_cevre("Kafe masası ve iki sandalye")
def masa(g):
    dsil(g, 0, 0, 0, 0.72, 0.05, 6, DEMIR, kapak=(False, False))
    dsil(g, 0, 0, 0.72, 0.77, 0.45, 10, SILME, kapak=(True, True))
    for s in (-1, 1):
        kutu(g, s * 0.75 - 0.2, s * 0.75 + 0.2, -0.2, 0.2, 0.4, 0.46, AHSAP, atla=())
        kutu(g, s * 0.93 - 0.03, s * 0.93 + 0.03, -0.2, 0.2, 0.46, 0.92, AHSAP, atla=())
        for dx in (-0.16, 0.16):
            for dy in (-0.16, 0.16):
                cubuk(g, (s * 0.75 + dx, dy, 0), (s * 0.75 + dx, dy, 0.4), 0.04, DEMIR)
    dsil(g, 0.1, 0.05, 0.77, 0.9, 0.05, 6, PM("Cicek", "#f5c84a"), kapak=(False, True))


@_cevre("Menü panosu (A tabela)")
def menu_panosu(g):
    for s in (-1, 1):
        obox(g, (0, s * 0.14, 0.5), (0.3, 0, 0), (0, s * 0.012, 0.004), (0, -s * 0.14, 0.5), AHSAP2)
        obox(g, (0, s * 0.165, 0.52), (0.24, 0, 0), (0, s * 0.004, 0.001), (0, -s * 0.1, 0.38), PM("Tahta", "#4a5a52"))
        for i in range(3):
            obox(g, (0, s * (0.2 - i * 0.035), 0.72 - i * 0.14), (0.16 - 0.03 * (i % 2), 0, 0), (0, s * 0.004, 0), (0, 0, 0.025), YAZI)


@_cevre("Alışveriş arabası yeri")
def araba_yeri(g):
    mt = PM("Metal", "#c9ccd0")
    for s in (-0.55, 0.55):
        cubuk(g, (s, -0.5, 0), (s, -0.5, 1.0), 0.05, mt)
        cubuk(g, (s, 0.5, 0), (s, 0.5, 1.0), 0.05, mt)
        cubuk(g, (s, -0.5, 1.0), (s, 0.5, 1.0), 0.05, mt)
    for i in range(3):
        y = -0.3 + i * 0.3
        kutu(g, -0.35, 0.35, y - 0.2, y + 0.2, 0.35, 0.8, mt, atla=("zp",))
        kutu(g, -0.33, 0.33, y - 0.18, y + 0.18, 0.5, 0.52, VR, atla=())
        for sx in (-0.28, 0.28):
            dsil(g, sx, y, 0.0, 0.1, 0.06, 5, DEMIR)
            cubuk(g, (sx, y, 0.1), (sx, y, 0.35), 0.03, mt)


@_cevre("Kasalar (meyve / sebze)")
def kasalar(g):
    renk = ("#d8402f", "#f08a2a", "#8ab84a", "#f2d04a")
    for i, (x, y, z) in enumerate(((-0.45, 0, 0), (0.45, 0, 0), (0, 0.02, 0.3), (-0.5, -0.62, 0))):
        kutu(g, x - 0.4, x + 0.4, y - 0.28, y + 0.28, z, z + 0.28, AHSAP if i % 2 else PM("Ahsap", "#b08a62"))
        kutu(g, x - 0.34, x + 0.34, y - 0.22, y + 0.22, z + 0.28, z + 0.3, PM("Dolgu", "#5d4336"), atla=())
        for a in range(3):
            for b in range(2):
                meyve(g, x - 0.22 + a * 0.22, y - 0.1 + b * 0.2, z + 0.29, 0.1, PM("Meyve", renk[i]))


@_cevre("Çiçek kovaları (çiçekçi)")
def cicek_kovalari(g):
    for i, (x, y) in enumerate(((-0.6, 0), (0, 0.1), (0.6, 0), (-0.3, -0.5), (0.35, -0.5))):
        dsil(g, x, y, 0, 0.38, 0.16, 7, PM("Metal", "#9aa3ab"), r1=0.2, kapak=(False, True), kapak_mat=YAPRAK)
        for k in range(4):
            a = k * 1.6 + i
            meyve(g, x + math.cos(a) * 0.1, y + math.sin(a) * 0.1, 0.42 + (k % 2) * 0.08, 0.09, CICEK[(i + k) % len(CICEK)], n=5)


@_cevre("Ahşap çit (bir hücre)", "donati:cit_ahsap")
def cit_ahsap(g):
    for i in range(8):
        x = -A + 0.18 + i * 0.35
        kutu(g, x - 0.06, x + 0.06, -0.03, 0.03, 0.0, 0.95, SILME)
        prizma(g, [(x - 0.06, -0.03, 0.95), (x + 0.06, -0.03, 0.95), (x, -0.03, 1.07)], (0, 0.06, 0), SILME)
    for z in (0.3, 0.7):
        kutu(g, -A, A, 0.03, 0.07, z, z + 0.09, SILME2, atla=())


@_cevre("Alçak taş duvar (bir hücre)")
def tas_duvar_alcak(g):
    kutu(g, -A, A, -0.2, 0.2, 0.0, 0.7, TAS)
    kutu(g, -A - 0.02, A + 0.02, -0.24, 0.24, 0.7, 0.8, TAS3, atla=())
    rnd = random.Random(4)
    for _ in range(5):
        x = rnd.uniform(-A + 0.1, A - 0.6)
        z = rnd.uniform(0.08, 0.4)
        kutu(g, x, x + rnd.uniform(0.35, 0.5), -0.215, 0.215, z, z + 0.22, TAS2, atla=())


@_cevre("Çit bitkisi (budanmış, bir hücre)")
def cit_bitki(g):
    kutu(g, -A, A, -0.3, 0.3, 0.0, 1.0, YP)
    kutu(g, -A + 0.05, A - 0.05, -0.25, 0.25, 1.0, 1.08, YP2, atla=())


@_cevre("Çiçek tarhı", "donati:cicek_havuzu")
def cicek_tarhi(g):
    kutu(g, -1.0, 1.0, -0.5, 0.5, 0.0, 0.16, TAS3)
    kutu(g, -0.9, 0.9, -0.4, 0.4, 0.16, 0.2, PM("Toprak", "#8a6a55"), atla=())
    rnd = random.Random(8)
    for i in range(14):
        x, y = rnd.uniform(-0.8, 0.8), rnd.uniform(-0.32, 0.32)
        meyve(g, x, y, 0.2, 0.11, YAPRAK)
        meyve(g, x, y, 0.28, 0.07, CICEK[i % len(CICEK)], n=5)


@_cevre("Sebze bahçesi")
def sebze_bahcesi(g):
    kutu(g, -1.3, 1.3, -0.9, 0.9, 0.0, 0.1, PM("Toprak", "#8a6a55"))
    for i in range(4):
        y = -0.66 + i * 0.44
        kutu(g, -1.2, 1.2, y - 0.13, y + 0.13, 0.1, 0.17, PM("Toprak", "#7a5a48"), atla=())
        for k in range(7):
            x = -1.05 + k * 0.35
            if i % 2:
                meyve(g, x, y, 0.17, 0.13, PM("Yaprak", "#7fb86a"))
            else:
                meyve(g, x, y, 0.17, 0.09, PM("Yaprak", "#6fa968"), n=5, rz=0.3)
                if k % 2:
                    meyve(g, x + 0.05, y, 0.3, 0.05, PM("Meyve", "#d8402f"), n=5)
    for sx in (-1.3, 1.3):
        for sy in (-0.9, 0.9):
            cubuk(g, (sx, sy, 0), (sx, sy, 0.5), 0.07, AHSAP)


@_cevre("Sokak lambası", "donati:lamba_klasik")
def sokak_lambasi(g):
    dsil(g, 0, 0, 0, 0.3, 0.14, 6, DEMIR, r1=0.08)
    dsil(g, 0, 0, 0.3, 3.3, 0.05, 6, DEMIR, kapak=(False, False))
    cubuk(g, (0, 0, 3.25), (0.45, 0, 3.25), 0.04, DEMIR)
    kutu(g, 0.33, 0.57, -0.12, 0.12, 2.85, 3.15, ISIK, atla=())
    kati(g, [(0.28, -0.17, 3.15), (0.62, -0.17, 3.15), (0.62, 0.17, 3.15), (0.28, 0.17, 3.15), (0.45, 0, 3.4)],
         [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], DEMIR)


@_cevre("Çöp kutusu", "donati:cop_kutusu")
def cop_kutusu(g):
    dsil(g, 0, 0, 0, 0.8, 0.24, 8, PM("Metal", "#5f8f6a"), r1=0.28, kapak=(False, True), kapak_mat=DEMIR)
    dsil(g, 0, 0, 0.8, 0.86, 0.3, 8, DEMIR, kapak=(True, True))


@_cevre("Posta kutusu", "donati:posta_kutusu")
def posta_kutusu(g):
    kr = PM("Posta", "#c8453f")
    kutu(g, -0.22, 0.22, -0.18, 0.18, 0.0, 0.12, DEMIR)
    kutu(g, -0.2, 0.2, -0.16, 0.16, 0.12, 1.05, kr)
    silindir(g, (-0.2, 0, 1.05), (0.2, 0, 1.05), 0.16, 0.16, 8, kr)
    kutu(g, -0.14, 0.14, -0.17, -0.16, 0.8, 0.86, DEMIR, atla=())
    kutu(g, -0.1, 0.1, -0.17, -0.16, 0.45, 0.65, SILME, atla=())


@_cevre("Otobüs durağı", "donati:durak")
def durak(g):
    mt = PM("Metal", "#7d8590")
    for sx in (-1.3, 1.3):
        cubuk(g, (sx, 0.5, 0), (sx, 0.5, 2.4), 0.08, mt)
        cubuk(g, (sx, -0.4, 2.3), (sx, 0.5, 2.4), 0.06, mt)
    kutu(g, -1.45, 1.45, -0.55, 0.6, 2.38, 2.46, VR, atla=())
    kutu(g, -1.3, 1.3, 0.5, 0.53, 0.5, 2.3, CAM, atla=())
    kutu(g, -1.0, 1.0, 0.1, 0.45, 0.42, 0.48, AHSAP, atla=())
    for sx in (-0.8, 0.8):
        kutu(g, sx - 0.04, sx + 0.04, 0.12, 0.43, 0, 0.42, DEMIR)
    cubuk(g, (1.7, 0, 0), (1.7, 0, 2.6), 0.06, mt)
    kutu(g, 1.5, 1.9, -0.03, 0.03, 2.2, 2.6, PM("Levha", "#3f5f8a"), atla=())


@_cevre("Kuyu")
def kuyu(g):
    dsil(g, 0, 0, 0, 0.8, 0.7, 10, TAS, kapak=(False, False))
    dsil(g, 0, 0, 0.8, 0.9, 0.78, 10, TAS3, kapak=(True, True))
    dsil(g, 0, 0, 0.55, 0.56, 0.6, 10, PM("Su", "#7ab8d6"))
    for sx in (-0.62, 0.62):
        cubuk(g, (sx, 0, 0.9), (sx, 0, 2.1), 0.1, AHSAP)
    prizma(g, [(-0.95, -0.6, 2.05), (-0.95, 0.6, 2.05), (-0.95, 0, 2.6)], (1.9, 0, 0), CT)
    silindir(g, (-0.6, 0, 1.75), (0.6, 0, 1.75), 0.07, 0.07, 6, AHSAP2)
    cubuk(g, (0, 0, 1.75), (0, 0, 1.15), 0.025, DEMIR)
    dsil(g, 0, 0, 0.95, 1.2, 0.13, 6, AHSAP, r1=0.16)


@_cevre("Odun yığını")
def odun_yigini(g):
    for sx in (-0.9, 0.9):
        cubuk(g, (sx, 0, 0), (sx, 0, 1.0), 0.08, AHSAP2)
    for k in range(4):
        for i in range(6 - (k % 2)):
            x = -0.72 + i * 0.29 + (k % 2) * 0.145
            silindir(g, (x, -0.3, 0.14 + k * 0.24), (x, 0.3, 0.14 + k * 0.24), 0.13, 0.13, 6, AHSAP, kapak_mat=PM("Ahsap", "#d9b58a"))


@_cevre("Bisiklet park yeri", "donati:bisiklet_park")
def bisiklet_park(g):
    mt = PM("Metal", "#c9ccd0")
    for i in range(3):
        x = -0.7 + i * 0.7
        cubuk(g, (x, -0.35, 0), (x, -0.35, 0.75), 0.05, mt)
        cubuk(g, (x, 0.35, 0), (x, 0.35, 0.75), 0.05, mt)
        cubuk(g, (x, -0.35, 0.75), (x, 0.35, 0.75), 0.05, mt)
    for d in (-0.45, 0.45):
        silindir(g, (-0.38, d, 0.33), (-0.32, d, 0.33), 0.33, 0.33, 10, PM("Lastik", "#3d3b42"))
    cubuk(g, (-0.35, -0.45, 0.33), (-0.35, 0, 0.62), 0.04, VR)
    cubuk(g, (-0.35, 0.45, 0.33), (-0.35, 0.28, 0.85), 0.04, VR)
    cubuk(g, (-0.35, -0.1, 0.8), (-0.35, 0.28, 0.82), 0.04, VR)
    cubuk(g, (-0.35, -0.05, 0.33), (-0.35, -0.12, 0.84), 0.04, VR)


@_cevre("Çeşme", "donati:cesme")
def cesme(g):
    dsil(g, 0, 0, 0, 0.45, 1.3, 10, TAS, kapak=(False, False))
    dsil(g, 0, 0, 0.45, 0.55, 1.38, 10, TAS3, kapak=(True, True))
    dsil(g, 0, 0, 0.38, 0.4, 1.2, 10, PM("Su", "#7ab8d6"))
    dsil(g, 0, 0, 0.4, 1.3, 0.2, 8, TAS3, r1=0.14, kapak=(False, False))
    dsil(g, 0, 0, 1.3, 1.4, 0.55, 8, TAS, r1=0.62, kapak=(True, True), kapak_mat=PM("Su", "#7ab8d6"))
    dsil(g, 0, 0, 1.4, 1.8, 0.08, 6, TAS3)


@_cevre("Heykel (kaideli)", "donati:heykel")
def heykel(g):
    kutu(g, -0.5, 0.5, -0.5, 0.5, 0.0, 0.25, TAS2)
    kutu(g, -0.38, 0.38, -0.38, 0.38, 0.25, 1.2, TAS)
    kutu(g, -0.44, 0.44, -0.44, 0.44, 1.2, 1.3, TAS3, atla=())
    br = PM("Bronz", "#7fa89a")
    kutu(g, -0.16, 0.16, -0.1, 0.1, 1.3, 2.2, br)
    top(g, (0, 0, 2.38), 0.15, 0.15, 0.17, br)
    cubuk(g, (0.16, 0, 2.1), (0.5, -0.1, 2.5), 0.08, br)
    cubuk(g, (-0.16, 0, 2.1), (-0.28, 0, 1.6), 0.08, br)


@_cevre("Kayık")
def kayik(g):
    kati(g, [(-1.3, 0, 0.1), (-0.7, -0.5, 0.05), (0.9, -0.5, 0.05), (1.2, 0, 0.1), (0.9, 0.5, 0.05), (-0.7, 0.5, 0.05),
             (-1.6, 0, 0.55), (-0.8, -0.65, 0.5), (1.0, -0.65, 0.5), (1.35, 0, 0.55), (1.0, 0.65, 0.5), (-0.8, 0.65, 0.5)],
         [[0, 1, 2, 3, 4, 5], [0, 1, 7, 6], [1, 2, 8, 7], [2, 3, 9, 8], [3, 4, 10, 9], [4, 5, 11, 10], [5, 0, 6, 11]], VR)
    kutu(g, -0.75, 0.95, -0.55, 0.55, 0.3, 0.34, AHSAP, atla=())
    for x in (-0.3, 0.5):
        kutu(g, x - 0.12, x + 0.12, -0.6, 0.6, 0.42, 0.47, PM("Ahsap", "#b08a62"), atla=())
    cubuk(g, (0.1, -0.75, 0.55), (0.9, -1.3, 0.3), 0.05, AHSAP2)


@_cevre("İskele (ahşap)")
def iskele(g):
    for i in range(7):
        y = -1.3 + i * 0.44
        kutu(g, -0.7, 0.7, y - 0.2, y + 0.2, 0.5, 0.58, AHSAP if i % 2 else PM("Ahsap", "#b08a62"), atla=())
    for sx in (-0.65, 0.65):
        for y in (-1.3, 0.0, 1.3):
            dsil(g, sx, y, -0.3, 0.9, 0.09, 6, AHSAP2, kapak=(False, True))


@_cevre("Kaya")
def kaya(g):
    top(g, (0, 0, 0.3), 0.7, 0.55, 0.45, TAS2, random.Random(3), 0.22)
    top(g, (0.55, 0.2, 0.18), 0.35, 0.3, 0.26, TAS, random.Random(5), 0.2)


@_cevre("Kütük")
def kutuk(g):
    dsil(g, 0, 0, 0, 0.45, 0.34, 8, AHSAP2, r1=0.3, kapak=(False, True), kapak_mat=PM("Ahsap", "#d9b58a"))
    dsil(g, 0, 0, 0.45, 0.46, 0.16, 8, PM("Ahsap", "#c49a6c"))
    cubuk(g, (0.3, 0, 0.05), (0.55, 0.1, 0.0), 0.12, AHSAP2)


@_cevre("Koyun")
def koyun(g):
    yun = PM("Yun", "#f4efe4")
    top(g, (0, 0, 0.55), 0.5, 0.32, 0.3, yun, random.Random(2), 0.1)
    top(g, (0.52, 0, 0.72), 0.16, 0.13, 0.15, PM("Yun", "#4a4550"))
    for sx in (-0.28, 0.28):
        for sy in (-0.15, 0.15):
            cubuk(g, (sx, sy, 0), (sx, sy, 0.35), 0.07, PM("Yun", "#4a4550"))


@_cevre("Ot öbeği")
def ot_obegi(g):
    for i in range(5):
        a = i * 1.26
        meyve(g, math.cos(a) * 0.12, math.sin(a) * 0.12, 0, 0.09, YP if i % 2 else YP2, n=4, rz=0.42 + 0.1 * (i % 3))


@_cevre("Çiçek öbeği")
def cicek_obegi(g):
    for i in range(4):
        a = i * 1.57 + 0.4
        x, y = math.cos(a) * 0.14, math.sin(a) * 0.14
        meyve(g, x, y, 0, 0.07, YAPRAK, n=4, rz=0.3)
        meyve(g, x, y, 0.2, 0.07, CICEK[i % len(CICEK)], n=5)


# ================================== ZEMİN PARÇALARI (sahne: çim, yol, kaldırım, meydan, patika, su, tarh, çit, yükselti) ==================================
# Hücre karoları yalnız üst yüzdür (yanları yok: yan yana karoların eki çizilmez); kenar parçaları on() cephe çerçevesindedir
# (u: kenar boyunca 0…HUCRE, o: dışa) ve dönüşle dört kenara / köşeye konur. Kurallar: zemin_yerlesim() = AtolyeKurucu.ZeminYerlesim.

KADEME = 1.2                 # bir yükselti kademesi (m)
ETEK = 0.6                   # sahnenin dış kenarındaki toprak eteğin boyu (m)
YOL_Z, KALDIRIM_Z, CIM_Z, SU_Z = 0.0, 0.12, 0.05, -0.38
ZEMIN_TUR = [("cim", "Çim"), ("yol", "Yol (asfalt; kaldırımı kendiliğinden)"), ("kaldirim", "Kaldırım"), ("meydan", "Arnavut kaldırımı meydan"),
             ("patika", "Toprak patika"), ("su", "Su / kanal (taş kenarlı)"), ("tarh", "Çiçek tarhı"), ("cit", "Çit bitkisi")]
SERT_ZEMIN = ("yol", "kaldirim", "meydan", "su")
CIM = [PM("Cim", "#bfe0a8"), PM("Cim", "#aed396"), PM("Cim", "#cfe8b8"), PM("Cim", "#9fc888")]
ASFALT = PM("Asfalt", "#958f9c")
ASFALT2 = PM("Asfalt", "#878190")
KALDIRIM = PM("Kaldirim", "#e6dfd0")
KALDIRIM2 = PM("Kaldirim", "#d8d0bf")
TOPRAK = PM("Toprak", "#d9c09a")
TOPRAK2 = PM("Toprak", "#c4a67c")
SU = PM("Su", "#9fd0e0")
SU2 = PM("Su", "#c4e6ee")


def _z(aciklama):
    return parca("zemin_karo", aciklama)


def _ust(g, x0, x1, y0, y1, z, mat):
    """Yalnız üst yüz (yukarı bakar)."""
    g.yuz([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], mat)
    g.sid += 1


def _tutam(g, x, y, z, rnd, mat):
    """Çim tutamı: üç ince üçgen yaprak."""
    for k in range(3):
        a = rnd.uniform(0, math.pi)
        dx, dy = math.cos(a) * 0.07, math.sin(a) * 0.07
        h = rnd.uniform(0.16, 0.3)
        tx, ty = rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06)
        g.yuz([(x - dx, y - dy, z), (x + dx, y + dy, z), (x + tx, y + ty, z + h)], mat)
        g.yuz([(x + dx, y + dy, z), (x - dx, y - dy, z), (x + tx, y + ty, z + h)], mat)
    g.sid += 1


def _cim(tohum, cicek=False, toprak=False):
    def fn(g):
        rnd = random.Random(tohum)
        _ust(g, -A, A, -A, A, CIM_Z, CIM[0])
        for i in range(3):                                   # renk lekeleri (düzgün olmayan dörtgenler)
            cx, cy = rnd.uniform(-0.8, 0.8), rnd.uniform(-0.8, 0.8)
            rx, ry = rnd.uniform(0.3, 0.6), rnd.uniform(0.22, 0.45)
            pts = [(cx + rx * math.cos(a + 0.4) * rnd.uniform(0.75, 1.1), cy + ry * math.sin(a + 0.4) * rnd.uniform(0.75, 1.1), CIM_Z + 0.004 + i * 0.001)
                   for a in (0, math.pi / 3, 2 * math.pi / 3, math.pi, 4 * math.pi / 3, 5 * math.pi / 3)]
            g.yuz(pts, CIM[1 + i % 2])
            g.sid += 1
        for i in range(5):
            _tutam(g, rnd.uniform(-1.2, 1.2), rnd.uniform(-1.2, 1.2), CIM_Z, rnd, CIM[3] if i % 2 else CIM[1])
        if cicek:
            for i in range(5):
                meyve(g, rnd.uniform(-1.1, 1.1), rnd.uniform(-1.1, 1.1), CIM_Z, 0.06, CICEK[i % len(CICEK)], n=5, rz=0.1)
        if toprak:
            cx, cy = rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5)
            pts = [(cx + 0.42 * math.cos(a) * rnd.uniform(0.7, 1.1), cy + 0.3 * math.sin(a) * rnd.uniform(0.7, 1.1), CIM_Z + 0.008) for a in [k * math.pi / 3 for k in range(6)]]
            g.yuz(pts, TOPRAK)
            g.sid += 1
    return fn


ekle("z_cim_0", "zemin_karo", "Çim (tutamlı)", _cim(1))
ekle("z_cim_1", "zemin_karo", "Çim (çiçekli)", _cim(2, cicek=True))
ekle("z_cim_2", "zemin_karo", "Çim (toprak lekeli)", _cim(3, toprak=True))


@_z("Yol: asfalt")
def z_yol(g):
    _ust(g, -A, A, -A, A, YOL_Z, ASFALT)
    rnd = random.Random(5)
    for i in range(2):                                       # yama / leke
        cx, cy = rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6)
        g.yuz([(cx - 0.35, cy - 0.2, YOL_Z + 0.012), (cx + 0.3, cy - 0.26, YOL_Z + 0.012), (cx + 0.38, cy + 0.18, YOL_Z + 0.012), (cx - 0.28, cy + 0.24, YOL_Z + 0.012)], ASFALT2)
        g.sid += 1


@_z("Yol: şerit çizgisi (kesikli; y ekseni boyunca)")
def z_serit(g):
    for y0 in (-1.25, -0.25, 0.75):
        _ust(g, -0.07, 0.07, y0, y0 + 0.6, YOL_Z + 0.025, SILME)


@_z("Yol: iki şerit arası çizgi (x ekseni boyunca, hücrenin arka kenarında)")
def z_serit_kenar(g):
    for x0 in (-1.25, -0.25, 0.75):
        _ust(g, x0, x0 + 0.6, A - 0.07, A + 0.07, YOL_Z + 0.025, SILME)


@_z("Yol: yaya geçidi (y ekseni boyunca giden yolda)")
def z_gecit(g):
    for i in range(5):
        x = -0.82 + i * 0.41
        _ust(g, x - 0.13, x + 0.13, -0.8, 0.8, YOL_Z + 0.025, SILME)


@_z("Yol: rögar kapağı")
def z_rogar(g):
    dsil(g, 0.45, 0.3, YOL_Z, YOL_Z + 0.012, 0.3, 10, DEMIR, kapak=(False, True))
    for i in range(3):
        _ust(g, 0.25, 0.65, 0.2 + i * 0.09, 0.24 + i * 0.09, YOL_Z + 0.016, ASFALT2)


def _kaldirim_seridi(g, c, u0, u1):
    """Yol kenarı: 60 cm kaldırım (iki tonlu karolar) + yola bakan bordür taşı."""
    n = max(1, int(round((u1 - u0) / 0.8)))
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        c.kutu(g, a, b, -0.46, 0.0, YOL_Z, KALDIRIM_Z, KALDIRIM if i % 2 == 0 else KALDIRIM2, arka=True)
    c.kutu(g, u0, u1, -0.6, -0.46, YOL_Z, KALDIRIM_Z + 0.02, TAS3, arka=True)


@parca("zemin_kenar", "Yol kenarı: kaldırım şeridi + bordür")
def z_bordur(g):
    _kaldirim_seridi(g, on(), 0.6, HUCRE - 0.6)


@parca("zemin_kenar", "Yol kenarı: şeridin köşeye uzayan başı")
def z_bordur_uc(g):
    _kaldirim_seridi(g, on(), 0.0, 0.6)


@parca("zemin_kenar", "Yol kenarı: şeridin köşeye uzayan sonu")
def z_bordur_son(g):
    _kaldirim_seridi(g, on(), HUCRE - 0.6, HUCRE)


@parca("zemin_kenar", "Yol köşesi: kaldırım karesi + L bordür")
def z_bordur_kose(g):
    kutu(g, -A, -A + 0.46, -A, -A + 0.46, YOL_Z, KALDIRIM_Z, KALDIRIM, atla=("zn",))
    kutu(g, -A + 0.46, -A + 0.6, -A, -A + 0.6, YOL_Z, KALDIRIM_Z + 0.02, TAS3, atla=("zn",))
    kutu(g, -A, -A + 0.46, -A + 0.46, -A + 0.6, YOL_Z, KALDIRIM_Z + 0.02, TAS3, atla=("zn",))


@_z("Kaldırım (karolu)")
def z_kaldirim(g):
    _ust(g, -A, A, -A, A, KALDIRIM_Z, KALDIRIM)
    for i in range(4):
        for j in range(4):
            if (i + j) % 2 == 0:
                _ust(g, -A + i * 0.7 + 0.03, -A + (i + 1) * 0.7 - 0.03, -A + j * 0.7 + 0.03, -A + (j + 1) * 0.7 - 0.03, KALDIRIM_Z + 0.004, KALDIRIM2)


def _meydan(tohum):
    def fn(g):
        rnd = random.Random(tohum)
        m = [PM("Meydan", "#d9cbb6"), PM("Meydan", "#cbbba3"), PM("Meydan", "#e3d7c4"), PM("Meydan", "#bfae96")]
        _ust(g, -A, A, -A, A, KALDIRIM_Z, m[0])
        for j in range(7):                                   # şaşırtmalı taş sıraları
            y0 = -A + j * 0.4
            x = -A + (0.0 if j % 2 == 0 else -0.2)
            while x < A - 0.05:
                w = rnd.uniform(0.3, 0.5)
                a, b = max(-A, x + 0.03), min(A, x + w - 0.03)
                if b - a > 0.12 and rnd.random() < 0.62:
                    _ust(g, a, b, y0 + 0.03, y0 + 0.37, KALDIRIM_Z + 0.006, m[1 + rnd.randrange(3)])
                x += w
    return fn


ekle("z_meydan_0", "zemin_karo", "Arnavut kaldırımı", _meydan(4))
ekle("z_meydan_1", "zemin_karo", "Arnavut kaldırımı (varyant)", _meydan(9))


@_z("Toprak patika (çim üstünde)")
def z_patika(g):
    _ust(g, -A, A, -A, A, CIM_Z, TOPRAK)
    rnd = random.Random(6)
    for i in range(4):
        cx, cy = rnd.uniform(-0.9, 0.9), rnd.uniform(-0.9, 0.9)
        g.yuz([(cx - 0.3, cy - 0.16, CIM_Z + 0.004), (cx + 0.26, cy - 0.2, CIM_Z + 0.004), (cx + 0.32, cy + 0.15, CIM_Z + 0.004), (cx - 0.22, cy + 0.2, CIM_Z + 0.004)], TOPRAK2)
        g.sid += 1
    for i in range(6):                                       # çakıl
        meyve(g, rnd.uniform(-1.2, 1.2), rnd.uniform(-1.2, 1.2), CIM_Z, 0.06, TAS3, n=5, rz=0.05)
    for i in range(3):
        _tutam(g, rnd.choice((-1.25, 1.25)) + rnd.uniform(-0.08, 0.08), rnd.uniform(-1.2, 1.2), CIM_Z, rnd, CIM[3])


@_z("Su (ince dalga çizgili)")
def z_su(g):
    _ust(g, -A, A, -A, A, SU_Z, SU)
    rnd = random.Random(8)
    for i in range(5):
        cx, cy = rnd.uniform(-0.9, 0.7), -1.1 + i * 0.52 + rnd.uniform(-0.08, 0.08)
        w = rnd.uniform(0.35, 0.7)
        _ust(g, cx, cx + w, cy, cy + 0.035, SU_Z + 0.004, SU2)
        _ust(g, cx + w * 0.5, cx + w * 1.3, cy + 0.1, cy + 0.13, SU_Z + 0.004, SU2)


@parca("zemin_kenar", "Su kenarı: taş rıhtım duvarı")
def z_su_kenar(g):
    c = on()
    c.kutu(g, 0.26, HUCRE - 0.26, -0.26, 0.0, SU_Z - 0.3, 0.06, TAS, arka=True)
    for i in range(4):
        a, b = 0.26 + (HUCRE - 0.52) * i / 4, 0.26 + (HUCRE - 0.52) * (i + 1) / 4
        c.kutu(g, a + 0.015, b - 0.015, -0.3, 0.0, 0.06, 0.13, TAS3 if i % 2 else TAS2, arka=True)


@parca("zemin_kenar", "Su kenarı: köşe babası")
def z_su_kose(g):
    kutu(g, -A, -A + 0.3, -A, -A + 0.3, SU_Z - 0.3, 0.2, TAS2, atla=("zn",))
    kutu(g, -A - 0.0, -A + 0.34, -A - 0.0, -A + 0.34, 0.2, 0.27, TAS3, atla=())


def _tarh(tohum):
    def fn(g):
        rnd = random.Random(tohum)
        _ust(g, -A, A, -A, A, CIM_Z, CIM[0])
        kutu(g, -1.15, 1.15, -1.15, 1.15, CIM_Z, CIM_Z + 0.14, TAS3, atla=("zn",))
        kutu(g, -1.05, 1.05, -1.05, 1.05, CIM_Z + 0.14, CIM_Z + 0.17, PM("Toprak", "#8a6a52"), atla=("zn",))
        for i in range(4):
            for j in range(4):
                x, y = -0.78 + i * 0.52 + rnd.uniform(-0.08, 0.08), -0.78 + j * 0.52 + rnd.uniform(-0.08, 0.08)
                meyve(g, x, y, CIM_Z + 0.17, 0.16, YAPRAK, n=5, rz=0.2)
                meyve(g, x, y, CIM_Z + 0.32, 0.09, CICEK[(i * 3 + j + tohum) % len(CICEK)], n=5)
    return fn


ekle("z_tarh_0", "zemin_karo", "Çiçek tarhı", _tarh(1))
ekle("z_tarh_1", "zemin_karo", "Çiçek tarhı (varyant)", _tarh(2))


@_z("Çit bitkisi: orta")
def z_cit_orta(g):
    kutu(g, -0.4, 0.4, -0.4, 0.4, CIM_Z, 1.05, YP, atla=("zn",))
    kutu(g, -0.34, 0.34, -0.34, 0.34, 1.05, 1.13, YP2, atla=("zn",))


@parca("zemin_kenar", "Çit bitkisi: komşu çite uzanan kol")
def z_cit_kol(g):
    c = on()
    c.kutu(g, 1.0, 1.8, -1.0, 0.0, CIM_Z, 1.02, YP, arka=True)
    c.kutu(g, 1.06, 1.74, -1.0, 0.0, 1.02, 1.1, YP2, arka=True)


@parca("zemin_kenar", "Yükselti kenarı: toprak yamaç (boyu z ölçeğiyle)")
def z_yamac(g):
    c = on()
    prizma(g, [c.p(0, 0, 0.0), c.p(0, 0.75, -1.0), c.p(0, -0.02, -1.0)], (c.u[0] * HUCRE, c.u[1] * HUCRE, 0), TOPRAK)
    c.kutu(g, 0.0, HUCRE, -0.02, 0.1, -0.06, 0.012, CIM[1], arka=True)                       # üst kenarda sarkan çim
    for i, (u, o, zz) in enumerate(((0.5, 0.25, -0.33), (1.5, 0.45, -0.6), (2.2, 0.2, -0.27), (1.0, 0.6, -0.8))):
        p = c.p(u, o + 0.02, zz)
        meyve(g, p[0], p[1], p[2], 0.1, TAS3 if i % 2 else CIM[3], n=5, rz=0.08)


@parca("zemin_kenar", "Yükselti kenarı: taş istinat duvarı (boyu z ölçeğiyle)")
def z_istinat(g):
    c = on()
    c.kutu(g, 0.0, HUCRE, -0.02, 0.16, -1.0, 0.0, TAS, arka=True)
    rnd = random.Random(14)
    for j in range(3):
        zz = -0.95 + j * 0.32
        u = 0.0 if j % 2 == 0 else -0.35
        while u < HUCRE - 0.1:
            w = rnd.uniform(0.5, 0.8)
            a, b = max(0.0, u + 0.02), min(HUCRE, u + w - 0.02)
            if b - a > 0.2:
                c.kutu(g, a, b, 0.16, 0.19, zz, zz + 0.27, TAS2 if rnd.random() < 0.5 else TAS3)
            u += w
    c.kutu(g, -0.02, HUCRE + 0.02, -0.06, 0.22, 0.0, 0.09, TAS3, arka=True)                  # harpuşta


@parca("zemin_kenar", "Sahne kenarı: toprak etek (boyu z ölçeğiyle)")
def z_etek(g):
    c = on()
    c.kutu(g, 0.0, HUCRE, -0.06, 0.0, -1.0, 0.0, TOPRAK2, arka=True)
    c.kutu(g, 0.0, HUCRE, -0.06, 0.012, -0.2, 0.0, TOPRAK, arka=True)


# ================================== AYRINTI KATMANI (v4) ==================================
# Aynı adlı parçalar burada yeniden tanımlanır (sonraki tanım geçerlidir): pencereler derin pervazlı, söveli, perdeli;
# köşeler dişli taşlı; silmeler profilli; saçaklar konsollu ve oluklu; kiremitler kademeli ve oluklu; dükkân cepheleri
# dilimli tenteli, çerçeveli tabelalı. Kurallar (yerlesim) yalnız üç yerde değişti: APT_UST'a yeni pencereler, düz duvar
# varyantları (duz_varyant) ve iniş borulu köşe (kose_boru). Uzak görünüm (LOD1) eski sade parçalarla kalır.

PERDE = [PM("Perde", "#f6ead2"), PM("Perde", "#e9c9c0"), PM("Perde", "#c9dcc4"), PM("Perde", "#d9d0e6")]
TUGLA = PM("Tugla", "#c98a72")
TUGLA2 = PM("Tugla", "#b3745e")
METAL = PM("Metal", "#9aa3ab")
OLUK = PM("Metal", "#7d8790")


def _sade(ad):
    """Parçanın şimdiki (sade) üreticisini uzak görünüm için saklar."""
    if ad in PARCA and ad not in PARCA_HAFIF:
        PARCA_HAFIF[ad] = PARCA[ad]


for _ad in ("kose_dis", "silme", "kaide", "sacak", "cati_egim", "cati_kose", "cati_semer", "tabela_bant", "duvar_duz", "baca", "cati_penceresi"):
    _sade(_ad)


def pencere(g, c, u0, u1, z0, z1, kemer=False, bolme=1, yatay=True, cam=CAM, t=0.12, perde=1, sove=True):
    """Pencere: cam duvar yüzünde, kalın çerçeve 10 cm önde (derin pervaz gölgesi), kayıtlar, denizlik + konsollar,
    alın taşı ve kilit taşı, cam arkasında perde / stor. perde: 0 yok · 1 iki yana toplanmış · 2 stor · 3 tek yanda + lamba."""
    dc = 0.1
    w = u1 - u0
    c.kutu(g, u0, u1, 0, 0.015, z0, z1, cam)
    # pervazın iç gölgesi (üstte ve solda koyu bant: çerçeve derin görünür)
    c.kutu(g, u0, u1, 0.015, 0.028, z1 - 0.07, z1, D2)
    c.kutu(g, u0, u0 + 0.05, 0.015, 0.028, z0, z1 - 0.07, D2)
    # perde / stor (camın hemen önünde, kayıtların arkasında)
    pm = PERDE[int(round(u0 * 10 + z1 * 7 + w * 3)) % len(PERDE)]
    if perde == 1 and w >= 0.7:
        pw = w * 0.2
        for a, b in ((u0 + 0.05, u0 + 0.05 + pw), (u1 - pw, u1)):
            c.kutu(g, a, b, 0.028, 0.036, z0 + 0.02, z1 - 0.07, pm)
        c.kutu(g, u0 + 0.05, u1, 0.028, 0.04, z1 - 0.17, z1 - 0.07, pm)                    # farbala
    elif perde == 2:
        c.kutu(g, u0 + 0.05, u1, 0.028, 0.036, z0 + (z1 - z0) * 0.55, z1 - 0.07, pm)
        c.kutu(g, u0 + 0.05, u1, 0.028, 0.045, z0 + (z1 - z0) * 0.55 - 0.03, z0 + (z1 - z0) * 0.55, SILME2)
    elif perde == 3:
        c.kutu(g, u0 + 0.05, u0 + 0.05 + w * 0.3, 0.028, 0.036, z0 + 0.02, z1 - 0.07, pm)
        p = c.p(u1 - w * 0.25, 0.03, z0 + 0.02)
        c.kutu(g, u1 - w * 0.25 - 0.012, u1 - w * 0.25 + 0.012, 0.028, 0.04, z0, z0 + 0.3, DEMIR)
        c.kutu(g, u1 - w * 0.25 - 0.1, u1 - w * 0.25 + 0.1, 0.028, 0.045, z0 + 0.3, z0 + 0.46, ISIK)   # içeride abajur
    # çerçeve
    c.kutu(g, u0 - t, u0, 0, dc, z0 - t, z1 + (0 if kemer else t), SILME)
    c.kutu(g, u1, u1 + t, 0, dc, z0 - t, z1 + (0 if kemer else t), SILME)
    c.kutu(g, u0, u1, 0, dc, z0 - t, z0, SILME)
    if kemer:
        r = w / 2
        um = (u0 + u1) / 2
        c.yarim_disk(g, um, z1, r, 0, 0.015, cam, n=8)
        n = 6
        R = r + t / 2
        for i in range(n):
            a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
            cubuk(g, c.p(um + R * math.cos(a0), dc / 2, z1 + R * math.sin(a0)), c.p(um + R * math.cos(a1), dc / 2, z1 + R * math.sin(a1)), t, SILME, dc)
        if sove:                                                                              # kemerin kilit taşı
            c.kutu(g, um - 0.09, um + 0.09, 0, dc + 0.05, z1 + r - 0.02, z1 + r + t + 0.1, TAS3)
    else:
        c.kutu(g, u0, u1, 0, dc, z1, z1 + t, SILME)
        if sove:                                                                              # alın taşı + kilit taşı
            c.kutu(g, u0 - t - 0.08, u1 + t + 0.08, 0, dc + 0.04, z1 + t, z1 + t + 0.09, SILME2)
            um = (u0 + u1) / 2
            c.kutu(g, um - 0.08, um + 0.08, 0, dc + 0.07, z1 + 0.02, z1 + t + 0.14, TAS3)
    for i in range(1, bolme + 1):
        uu = u0 + w * i / (bolme + 1)
        c.kutu(g, uu - 0.03, uu + 0.03, 0.036, 0.07, z0, z1, SILME)
    if yatay:
        zz = (z0 + z1) / 2 if bolme == 1 else z0 + (z1 - z0) * 0.62
        c.kutu(g, u0, u1, 0.036, 0.07, zz - 0.03, zz + 0.03, SILME)
    c.kutu(g, u0 - t - 0.07, u1 + t + 0.07, 0, 0.18, z0 - t - 0.07, z0 - t, SILME2)          # denizlik
    if sove:
        for uu in (u0 - t + 0.02, u1 + t - 0.12):                                             # denizlik konsolları
            c.kutu(g, uu, uu + 0.1, 0, 0.12, z0 - t - 0.2, z0 - t - 0.07, SILME2)


def _panjur(g, c, a, b, z0, z1, acik=True):
    """Lamelli panjur kanadı (açıkken duvara yaslı, kapalıyken pencerenin önünde)."""
    o0 = 0.0 if acik else 0.1
    c.kutu(g, a, b, o0, o0 + 0.05, z0, z1, VR)
    n = 9
    for i in range(1, n):
        zz = z0 + (z1 - z0) * i / n
        c.kutu(g, a + 0.04, b - 0.04, o0 + 0.05, o0 + 0.068, zz - 0.022, zz + 0.022, SILME2)
    for zz in (z0 + 0.18, z1 - 0.18):                                                         # menteşeler
        c.kutu(g, (a if acik and a < 1.4 else b - 0.05) if acik else a, ((a + 0.05) if acik and a < 1.4 else b) if acik else a + 0.05,
               o0 + 0.05, o0 + 0.075, zz - 0.03, zz + 0.03, DEMIR)


@parca("duvar", "Panjurlu pencere (açık panjur)")
def duvar_panjur(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.95, 1.85, 0.95, 2.45, perde=1)
    _panjur(g, c, 0.38, 0.8, 0.86, 2.56)
    _panjur(g, c, 2.0, 2.42, 0.86, 2.56)


@parca("duvar", "Panjurlu pencere (bir kanadı kapalı)")
def duvar_panjur_kapali(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.95, 1.85, 0.95, 2.45, perde=0)
    _panjur(g, c, 0.38, 0.8, 0.86, 2.56)
    _panjur(g, c, 1.4, 1.87, 0.9, 2.5, acik=False)


@parca("duvar", "Storlu pencere")
def duvar_stor(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.8, 2.0, 0.95, 2.45, bolme=1, perde=2)
    c.kutu(g, 0.55, 2.25, 0.18, 0.21, 0.5, 0.72, DEMIR)                                      # pencere önü demir saksılık
    for uu in (0.57, 2.2):
        c.kutu(g, uu, uu + 0.03, 0, 0.2, 0.5, 0.72, DEMIR)
    for i in range(3):
        p = c.p(0.85 + i * 0.55, 0.1, 0.62)
        dsil(g, p[0], p[1], 0.58, 0.74, 0.08, 5, SAKSI, r1=0.1, kapak=(False, True), kapak_mat=AHSAP2)
        meyve(g, p[0], p[1], 0.74, 0.11, CICEK[i % len(CICEK)], n=5)


@parca("duvar", "Alınlıklı pencere (üçgen alınlık, lambalı)")
def duvar_alinlik(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.9, 1.9, 0.9, 2.3, bolme=1, perde=3, sove=False)
    c.kutu(g, 0.68, 2.12, 0, 0.15, 2.42, 2.5, SILME2)
    c.prizma_uc(g, 0.7, 2.1, 0, 0.13, 2.5, 0.32, SILME)
    for uu in (0.72, 1.96):
        c.kutu(g, uu, uu + 0.12, 0, 0.13, 0.62, 0.72, SILME2)


@parca("duvar", "Fransız balkonlu kapı-pencere (dökme demir)")
def duvar_balkon(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.8, 2.0, 0.25, 2.5, bolme=1, perde=1, sove=False)
    c.kutu(g, 0.42, 2.38, 0, 0.62, 0.06, 0.18, SILME2)                                       # balkon tablası
    c.kutu(g, 0.38, 2.42, 0, 0.66, 0.18, 0.22, SILME)
    for uu in (0.55, 2.13):                                                                   # konsollar
        c.prizma_uc(g, uu, uu + 0.12, 0, 0.0, 0, 0, SILME2) if False else prizma(
            g, [c.p(uu, 0, 0.06), c.p(uu, 0.5, 0.06), c.p(uu, 0, -0.3)], tuple(x * 0.12 for x in (c.u[0], c.u[1], 0)), SILME2)
    z0, z1 = 0.22, 1.12
    for uu in (0.44, 2.36):
        c.kutu(g, uu - 0.025, uu + 0.025, 0.56, 0.61, z0, z1 + 0.06, DEMIR)
        c.kutu(g, uu - 0.02, uu + 0.02, 0.02, 0.58, z1 - 0.04, z1, DEMIR)
        c.kutu(g, uu - 0.012, uu + 0.012, 0.02, 0.58, z0 + 0.1, z0 + 0.13, DEMIR)
    c.kutu(g, 0.44, 2.36, 0.56, 0.61, z1 - 0.04, z1, DEMIR)
    c.kutu(g, 0.44, 2.36, 0.565, 0.605, z0 + 0.1, z0 + 0.13, DEMIR)
    n = 8
    for i in range(n):                                                                         # desen: çubuk + çapraz
        ua, ub = 0.44 + 1.92 * i / n, 0.44 + 1.92 * (i + 1) / n
        c.kutu(g, ua - 0.011, ua + 0.011, 0.575, 0.595, z0, z1 - 0.04, DEMIR)
        if i % 2 == 0:
            cubuk(g, c.p(ua, 0.585, z0 + 0.13), c.p(ub, 0.585, z1 - 0.04), 0.02, DEMIR)
        else:
            cubuk(g, c.p(ua, 0.585, z1 - 0.04), c.p(ub, 0.585, z0 + 0.13), 0.02, DEMIR)
    for uu, ci in ((0.62, 0), (2.18, 1)):                                                     # saksılar
        p = c.p(uu, 0.36, 0.22)
        dsil(g, p[0], p[1], 0.22, 0.42, 0.09, 6, SAKSI, r1=0.12, kapak=(False, True), kapak_mat=AHSAP2)
        meyve(g, p[0], p[1], 0.42, 0.14, YAPRAK, n=5)
        meyve(g, p[0], p[1], 0.52, 0.07, CICEK[ci], n=5)


def _siva_lekesi(g, c, u, z, w, h):
    c.kutu(g, u, u + w, 0, 0.006, z, z + h, D2)


@parca("duvar", "Düz duvar (sıva lekeli, süs bantlı)")
def duvar_duz(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.0, HUCRE, 0, 0.03, 2.06, 2.14, D2)                                            # süs bandı
    _siva_lekesi(g, c, 0.5, 0.75, 0.55, 0.35)
    _siva_lekesi(g, c, 1.7, 1.3, 0.7, 0.28)
    cubuk(g, c.p(2.2, 0.004, 2.0), c.p(2.32, 0.004, 1.6), 0.012, D2, 0.008)                   # ince çatlak
    cubuk(g, c.p(2.32, 0.004, 1.6), c.p(2.26, 0.004, 1.32), 0.012, D2, 0.008)


@parca("duvar", "Düz duvar: sıvası dökülmüş, tuğlası görünen")
def duvar_duz_tugla(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.0, HUCRE, 0, 0.03, 2.06, 2.14, D2)
    c.kutu(g, 0.55, 1.75, 0, 0.012, 0.7, 1.55, TUGLA2)
    for j in range(5):
        zz = 0.72 + j * 0.165
        for i in range(4):
            uu = 0.57 + i * 0.3 + (0.15 if j % 2 else 0.0)
            if uu + 0.26 <= 1.76:
                c.kutu(g, uu, uu + 0.26, 0.012, 0.03, zz, zz + 0.13, TUGLA)
    _siva_lekesi(g, c, 1.85, 1.9, 0.5, 0.3)
    p = c.p(2.3, 0.06, 0.0)                                                                    # ev numarası
    c.kutu(g, 2.12, 2.44, 0, 0.03, 1.5, 1.74, PM("Emaye", "#3d5a8a"))
    c.kutu(g, 2.2, 2.36, 0.03, 0.04, 1.56, 1.68, SILME)


@parca("duvar", "Düz duvar: klima, elektrik kutusu, kablo")
def duvar_duz_kutu(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.0, HUCRE, 0, 0.03, 2.06, 2.14, D2)
    c.kutu(g, 0.5, 1.35, 0.02, 0.36, 1.5, 2.0, SILME2)                                        # klima dış ünitesi
    c.kutu(g, 0.56, 1.0, 0.36, 0.38, 1.56, 1.94, METAL)
    for i in range(4):
        c.kutu(g, 0.58, 0.98, 0.38, 0.39, 1.6 + i * 0.09, 1.63 + i * 0.09, DEMIR)
    for uu in (0.56, 1.25):
        cubuk(g, c.p(uu, 0.0, 1.32), c.p(uu, 0.34, 1.5), 0.035, DEMIR)
    c.kutu(g, 1.9, 2.3, 0, 0.12, 0.9, 1.5, METAL)                                              # elektrik kutusu
    c.kutu(g, 1.94, 2.26, 0.12, 0.13, 0.94, 1.46, OLUK)
    c.kutu(g, 2.2, 2.24, 0.13, 0.15, 1.15, 1.25, PM("Emaye", "#f2c35a"))
    c.kutu(g, 2.08, 2.12, 0, 0.04, 1.5, KAT, DEMIR)                                            # kablo borusu
    c.kutu(g, 1.3, 2.1, 0, 0.03, 1.78, 1.81, DEMIR)


@parca("kose", "Dış köşe: pilastr + dişli köşe taşları")
def kose_dis(g):
    kutu(g, -A - 0.05, -A + 0.2, -A - 0.05, -A + 0.2, 0.0, KAT, SILME)
    kutu(g, -A - 0.09, -A + 0.24, -A - 0.09, -A + 0.24, 0.0, 0.16, SILME2, atla=())            # taban
    kutu(g, -A - 0.09, -A + 0.24, -A - 0.09, -A + 0.24, KAT - 0.13, KAT - 0.03, SILME2, atla=())   # başlık
    z, i = 0.34, 0
    while z < KAT - 0.5:
        w = 0.46 if i % 2 == 0 else 0.32
        kutu(g, -A - 0.075, -A + w, -A - 0.075, -A + (0.78 - w), z, z + 0.34, SILME2, atla=("zn",))
        z += 0.58
        i += 1


@parca("kose", "Dış köşe: pilastr + yağmur iniş borusu")
def kose_boru(g):
    PARCA["kose_dis"](g)
    x, y = -A + 0.42, -A - 0.11
    dsil(g, x, y, 0.0, KAT, 0.055, 6, OLUK, kapak=(False, False))
    for zz in (0.5, 1.9):                                                                      # kelepçeler
        kutu(g, x - 0.075, x + 0.075, y - 0.075, -A, zz, zz + 0.05, DEMIR, atla=())


PARCA_HAFIF["kose_boru"] = PARCA_HAFIF["kose_dis"]


@parca("bant", "Kat silmesi (profilli, dişli)")
def silme(g):
    c = on()
    c.kutu(g, -0.06, HUCRE + 0.06, 0, 0.07, -0.2, -0.12, SILME2)
    c.kutu(g, -0.08, HUCRE + 0.08, 0, 0.11, -0.12, -0.05, SILME)
    c.kutu(g, -0.11, HUCRE + 0.11, 0, 0.16, -0.05, 0.0, SILME2)
    for i in range(3):                                                                         # diş sırası
        uu = 0.3 + i * 0.95
        c.kutu(g, uu, uu + 0.3, 0.07, 0.105, -0.2, -0.125, SILME)


@parca("bant", "Taş kaide (derzli, bodrum menfezli)")
def kaide(g):
    c = on()
    c.kutu(g, -0.07, HUCRE + 0.07, 0, 0.08, 0.0, 0.42, TAS2)
    c.kutu(g, -0.09, HUCRE + 0.09, 0, 0.11, 0.42, 0.5, TAS3)
    c.kutu(g, 1.4 - 0.012, 1.4 + 0.012, 0.08, 0.086, 0.02, 0.42, TAS)                          # düşey derz
    c.kutu(g, -0.07, HUCRE + 0.07, 0.08, 0.086, 0.2, 0.222, TAS)                               # yatay derz
    c.kutu(g, 0.2, 0.56, 0.08, 0.1, 0.1, 0.3, DEMIR)                                           # bodrum menfezi


@parca("cati", "Saçak (konsollu korniş, saçak tahtası, yağmur oluğu)")
def sacak(g):
    c = on()
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.26, -0.1, 0.02, SILME)
    c.kutu(g, -0.02, HUCRE + 0.02, 0.2, 0.3, -0.03, 0.07, CT2)
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.12, -0.24, -0.1, SILME2)
    c.kutu(g, -0.02, HUCRE + 0.02, 0.26, 0.3, -0.14, -0.03, AHSAP2)                            # saçak tahtası
    c.kutu(g, -0.02, HUCRE + 0.02, 0.3, 0.42, -0.1, -0.03, OLUK)                               # yağmur oluğu
    c.kutu(g, -0.02, HUCRE + 0.02, 0.33, 0.39, -0.03, -0.025, DEMIR)
    for i in range(3):                                                                         # konsollar
        uu = 0.3 + i * 1.05
        prizma(g, [c.p(uu, 0.02, -0.1), c.p(uu, 0.24, -0.1), c.p(uu, 0.02, -0.36)], (c.u[0] * 0.1, c.u[1] * 0.1, 0), SILME2)


def _kiremit(g, x0=0.0, x1=None, n=4):
    """Eğimli karonun kiremit sıraları: her sıra aşağı ucunda kalın (kademeli), üstünde üç oluk sırtı."""
    x1 = A if x1 is None else x1
    for i in range(n):
        y0, y1 = A * i / n, A * (i + 1) / n
        z0, z1 = i / float(n), (i + 1) / float(n)
        v = [(x0, y0, z0), (x1, y0, z0), (x1, y0, z0 + 0.075), (x0, y0, z0 + 0.075), (x0, y1, z1 + 0.012), (x1, y1, z1 + 0.012)]
        kati(g, v, [[0, 1, 2, 3], [3, 2, 5, 4], [0, 3, 4], [1, 5, 2]], [CT2, CT, CT2, CT2])
    m = 3
    for j in range(m):
        x = x0 + (x1 - x0) * (j + 0.5) / m
        obox(g, (x, A / 2 + 0.01, 0.5 + 0.05), (0.045, 0, 0), (0, A / 2 - 0.02, 0.5 - 0.014), (0, -0.012, 0.017), CT2, ("1-", "1+"))


@parca("cati", "Çatı karosu: eğim (kademeli, oluklu kiremit)")
def cati_egim(g):
    _cati_yuzu(g, 0, 0, 1, 1, 0)
    _kiremit(g)


def _mahya(g, p0, p1, n=4):
    """Mahya kiremitleri: sırt boyunca bindirmeli yarım boru parçaları."""
    for i in range(n):
        a = tuple(p0[k] + (p1[k] - p0[k]) * i / n for k in range(3))
        b = tuple(p0[k] + (p1[k] - p0[k]) * (i + 0.86) / n for k in range(3))
        cubuk(g, (a[0], a[1], a[2] + 0.02), (b[0], b[1], b[2] + 0.05), 0.17, CT2, 0.11)


@parca("cati", "Çatı karosu: dış köşe (kırma, mahya kiremitli)")
def cati_kose(g):
    _cati_yuzu(g, 0, 0, 1, 0, 0)
    _mahya(g, (0, 0, 0.03), (A, A, 1.03))


@parca("cati", "Çatı karosu: semer (mahya kiremitli)")
def cati_semer(g):
    _cati_yuzu(g, 1, 0, 1, 0, 1)
    _mahya(g, (0, 0, 1.03), (A, A, 1.03), 3)


@parca("ayrinti", "Baca (tuğla, başlıklı, çift künk)")
def baca(g):
    kutu(g, -0.3, 0.3, -0.25, 0.25, -1.2, 1.5, TUGLA)
    for j in range(6):                                                                         # tuğla sıraları (derz)
        zz = 0.1 + j * 0.22
        kutu(g, -0.305, 0.305, -0.255, 0.255, zz, zz + 0.022, TUGLA2, atla=("zn", "zp"))
    kutu(g, -0.34, 0.34, -0.29, 0.29, 1.2, 1.3, TUGLA2, atla=())
    kutu(g, -0.38, 0.38, -0.33, 0.33, 1.5, 1.62, SILME, atla=())
    kutu(g, -0.33, 0.33, -0.28, 0.28, 1.62, 1.68, SILME2, atla=())
    for sx in (-0.14, 0.14):
        dsil(g, sx, 0, 1.68, 2.02, 0.1, 6, TUGLA2, r1=0.085, kapak=(False, True), kapak_mat=DEMIR)
        dsil(g, sx, 0, 2.02, 2.07, 0.12, 6, TUGLA, kapak=(True, True))


@parca("ayrinti", "Çatı penceresi (çıkıntılı, alınlıklı)")
def cati_penceresi(g):
    kutu(g, -0.58, 0.58, 0.18, 1.6, 0.0, 1.2, D, atla=("zn", "yp"))
    kutu(g, -0.64, 0.64, 0.12, 0.2, 0.0, 0.1, SILME2, atla=())                                 # denizlik
    prizma(g, [(-0.78, 0.04, 1.2), (0.78, 0.04, 1.2), (0, 0.04, 1.86)], (0, 1.6, 0), CT)
    prizma(g, [(-0.6, 0.02, 1.26), (0.6, 0.02, 1.26), (0, 0.02, 1.74)], (0, 0.05, 0), SILME)   # alınlık yüzü
    kutu(g, -0.82, 0.82, 0.0, 0.1, 1.14, 1.22, SILME2, atla=())                                # saçak tahtası
    kutu(g, -0.36, 0.36, 0.15, 0.18, 0.28, 1.02, CAM)
    kutu(g, -0.36, 0.36, 0.13, 0.15, 0.8, 1.02, PERDE[0], atla=())
    for a, b, p, q in ((-0.46, -0.36, 0.2, 1.12), (0.36, 0.46, 0.2, 1.12), (-0.46, 0.46, 0.2, 0.28), (-0.46, 0.46, 1.02, 1.12), (-0.025, 0.025, 0.28, 1.02), (-0.36, 0.36, 0.62, 0.67)):
        kutu(g, a, b, 0.1, 0.18, p, q, SILME, atla=())
    meyve(g, 0.0, 0.3, 1.86, 0.07, PIRINC, n=5, rz=0.22)                                       # tepe süsü


@parca("zemin", "Tabela bandı (çerçeveli, kaz boynu lambalı)")
def tabela_bant(g):
    c = on()
    c.kutu(g, 0.0, HUCRE, 0, 0.13, 2.3, 2.78, VR)
    c.kutu(g, 0.0, HUCRE, 0, 0.17, 2.78, 2.85, SILME)
    c.kutu(g, 0.0, HUCRE, 0, 0.17, 2.25, 2.3, SILME)
    cubuk(g, c.p(1.4, 0.15, 2.9), c.p(1.4, 0.42, 3.0), 0.03, DEMIR)                            # kaz boynu lamba
    cubuk(g, c.p(1.4, 0.42, 3.0), c.p(1.4, 0.5, 2.88), 0.03, DEMIR)
    p = c.p(1.4, 0.5, 2.8)
    dsil(g, p[0], p[1], 2.78, 2.9, 0.11, 6, DEMIR, r1=0.04, kapak=(False, True))
    meyve(g, p[0], p[1], 2.74, 0.05, ISIK, n=5, rz=0.05)


@parca("zemin", "Tente (çizgili, dilimli etekli)")
def tente(g):
    c = on()
    n = 7
    for i in range(n):
        a, b = 0.06 + (HUCRE - 0.12) * i / n, 0.06 + (HUCRE - 0.12) * (i + 1) / n
        m = TN if i % 2 == 0 else SILME
        pts = [c.p(a, 0.1, 2.3), c.p(b, 0.1, 2.3), c.p(b, 1.0, 1.98), c.p(a, 1.0, 1.98)]
        q = [(p[0], p[1], p[2] - 0.035) for p in pts]
        kati(g, pts + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], m)
        # dilimli etek: dikdörtgen + altında yarım yuvarlak (üçgen dilim)
        c.kutu(g, a, b, 0.98, 1.0, 1.86, 1.98, m)
        um = (a + b) / 2
        prizma(g, [c.p(a, 0.98, 1.86), c.p(b, 0.98, 1.86), c.p(um + (b - a) * 0.28, 0.98, 1.76), c.p(um - (b - a) * 0.28, 0.98, 1.76)],
               (c.n[0] * 0.02, c.n[1] * 0.02, 0), m)
    for uu in (0.08, HUCRE - 0.08):
        cubuk(g, c.p(uu, 0.05, 1.9), c.p(uu, 0.95, 1.99), 0.03, DEMIR)
        cubuk(g, c.p(uu, 0.05, 1.55), c.p(uu, 0.95, 1.97), 0.025, DEMIR)                       # payanda
    c.kutu(g, 0.04, HUCRE - 0.04, 0.02, 0.12, 2.17, 2.26, METAL)                               # tente / kepenk kutusu (tabelanın altında)


def _vitrin_urun(g, c, u0, u1, z, rnd):
    """Vitrin içi: alçak raf + ürün dizisi (camın hemen önünde)."""
    c.kutu(g, u0, u1, 0.02, 0.07, z, z + 0.04, SILME2)
    uu = u0 + 0.06
    renk = ("#d94f3d", "#e8a23a", "#f2d04a", "#5a9a4a", "#2f7d8c", "#8a4a6b", "#f4f0e6", "#7ab8d6")
    while uu < u1 - 0.14:
        w = rnd.uniform(0.11, 0.2)
        c.kutu(g, uu, uu + w, 0.022, 0.06, z + 0.04, z + 0.04 + rnd.uniform(0.14, 0.34), PM("Urun", rnd.choice(renk)))
        uu += w + 0.05


@parca("zemin", "Dükkân vitrini (ürün dizili)")
def vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.09, 0.0, 0.5, VR)                                       # kaide panosu
    c.kutu(g, 0.27, HUCRE - 0.27, 0.09, 0.11, 0.1, 0.4, SILME2)
    c.kutu(g, 0.33, HUCRE - 0.33, 0.11, 0.12, 0.16, 0.34, VR)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.02, 0.5, 2.15, VIT)
    for uu in (0.15, 1.34, HUCRE - 0.27):
        c.kutu(g, uu, uu + 0.12, 0, 0.1, 0.5, 2.15, SILME)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.1, 2.15, 2.28, SILME)
    c.kutu(g, 0.27, HUCRE - 0.27, 0.02, 0.06, 1.62, 1.68, SILME)
    rnd = random.Random(21)
    _vitrin_urun(g, c, 0.28, 1.33, 0.5, rnd)
    _vitrin_urun(g, c, 1.47, HUCRE - 0.28, 0.5, rnd)
    c.kutu(g, 0.4, 1.2, 0.02, 0.03, 1.72, 2.1, PERDE[0])                                       # üst camlarda yarım perde
    c.kutu(g, 1.6, 2.4, 0.02, 0.03, 1.72, 2.1, PERDE[0])
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.16, 0.5, 0.55, SILME2)                                    # vitrin eşiği


@parca("zemin", "Geniş vitrin (tek cam, ürün dizili, kampanya kâğıtlı)")
def vitrin_genis(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.09, 0.0, 0.3, VR)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.02, 0.3, 2.15, VIT)
    for uu in (0.1, HUCRE - 0.2):
        c.kutu(g, uu, uu + 0.1, 0, 0.1, 0.3, 2.15, SILME)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.1, 2.15, 2.28, SILME)
    c.kutu(g, 0.06, HUCRE - 0.06, 0, 0.15, 0.3, 0.35, SILME2)
    c.kutu(g, 0.5, 1.1, 0.02, 0.03, 1.1, 1.7, PM("Afis", "#f5c84a"))                           # kampanya kâğıdı
    c.kutu(g, 0.56, 1.04, 0.03, 0.04, 1.45, 1.58, VR)
    c.kutu(g, 0.56, 1.04, 0.03, 0.04, 1.2, 1.26, DEMIR)
    _vitrin_urun(g, c, 0.24, HUCRE - 0.24, 0.35, random.Random(22))
    for uu in (0.7, 1.4, 2.1):                                                                 # vitrin spotları
        c.kutu(g, uu - 0.05, uu + 0.05, 0.02, 0.07, 2.06, 2.13, ISIK)


@parca("zemin", "Vitrinli cam kapı (üst ışıklıklı, zilli)")
def kapi_vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.15, 1.2, 0, 0.09, 0.0, 0.5, VR)
    c.kutu(g, 0.15, 1.2, 0, 0.02, 0.5, 2.15, VIT)
    c.kutu(g, 0.15, 0.27, 0, 0.1, 0.5, 2.15, SILME)
    c.kutu(g, 1.2, 1.32, 0, 0.11, 0.0, 2.28, SILME)
    c.kutu(g, 2.5, 2.62, 0, 0.11, 0.0, 2.28, SILME)
    c.kutu(g, 0.15, 2.62, 0, 0.11, 2.15, 2.28, SILME)
    _vitrin_urun(g, c, 0.28, 1.19, 0.5, random.Random(23))
    c.kutu(g, 1.32, 2.5, 0, 0.02, 0.12, 1.86, VIT)                                             # kapı camı
    c.kutu(g, 1.32, 2.5, 0, 0.02, 1.92, 2.15, CAM)                                             # üst ışıklık
    c.kutu(g, 1.32, 2.5, 0.02, 0.07, 1.86, 1.92, SILME)
    c.kutu(g, 1.32, 2.5, 0.02, 0.06, 0.12, 0.45, VR)
    c.kutu(g, 1.38, 2.44, 0.06, 0.07, 0.18, 0.39, SILME2)
    c.kutu(g, 1.32, 2.5, 0.02, 0.06, 1.1, 1.17, VR)
    c.kutu(g, 2.3, 2.38, 0.03, 0.1, 1.0, 1.3, PIRINC)
    c.kutu(g, 1.6, 2.2, 0.02, 0.03, 1.3, 1.5, PM("Afis", "#f4f0e6"))                           # "AÇIK" kartı
    c.kutu(g, 1.2, 2.62, 0, 0.42, 0.0, 0.12, TAS)
    c.kutu(g, 1.26, 2.56, 0.42, 0.66, 0.0, 0.06, TAS2)
    cubuk(g, c.p(2.7, 0.0, 2.1), c.p(2.7, 0.2, 2.1), 0.025, DEMIR)                             # kapı zili
    p = c.p(2.7, 0.2, 2.0)
    dsil(g, p[0], p[1], 1.96, 2.08, 0.07, 6, PIRINC, r1=0.02, kapak=(False, True))


def _basamak_korkuluk(g, c, u, o0, o1, z0, z1):
    cubuk(g, c.p(u, o0, z0 + 0.85), c.p(u, o1, z1 + 0.85), 0.04, DEMIR)
    for o, z in ((o0, z0), (o1, z1)):
        cubuk(g, c.p(u, o, z), c.p(u, o, z + 0.85), 0.035, DEMIR)
    p = c.p(u, o1, z1 + 0.85)
    meyve(g, p[0], p[1], p[2], 0.045, PIRINC, n=5)


def _saksi_cifti(g, c, ua, ub, o):
    for uu, ci in ((ua, 0), (ub, 3)):
        p = c.p(uu, o, 0)
        dsil(g, p[0], p[1], 0.0, 0.34, 0.13, 6, SAKSI, r1=0.17, kapak=(False, True), kapak_mat=AHSAP2)
        top(g, (p[0], p[1], 0.62), 0.2, 0.2, 0.3, YAPRAK)
        meyve(g, p[0], p[1], 0.86, 0.07, CICEK[ci], n=5)


@parca("zemin", "Ahşap kapı (panelli, üst ışıklıklı, kanopili, basamaklı)")
def kapi_ahsap(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.72, 2.08, 0, 0.1, 0.0, 2.62, SILME)                                            # söve
    c.kutu(g, 0.86, 1.94, 0, 0.04, 0.24, 2.1, VR)                                              # kanat
    for a, b in ((0.94, 1.36), (1.44, 1.86)):                                                  # paneller
        c.kutu(g, a, b, 0.04, 0.06, 0.34, 1.0, D2)
        c.kutu(g, a + 0.05, b - 0.05, 0.06, 0.07, 0.4, 0.94, VR)
        c.kutu(g, a, b, 0.04, 0.06, 1.1, 1.98, D2)
        c.kutu(g, a + 0.05, b - 0.05, 0.06, 0.07, 1.16, 1.92, VR)
    c.kutu(g, 1.74, 1.82, 0.06, 0.12, 1.02, 1.1, PIRINC)                                       # tokmak
    c.kutu(g, 1.25, 1.55, 0.06, 0.075, 1.3, 1.38, PIRINC)                                      # mektup ağzı
    c.kutu(g, 0.86, 1.94, 0, 0.02, 2.16, 2.5, CAM)                                             # üst ışıklık
    c.kutu(g, 0.86, 1.94, 0.02, 0.06, 2.1, 2.16, SILME)
    for uu in (1.22, 1.58):
        c.kutu(g, uu - 0.02, uu + 0.02, 0.02, 0.05, 2.16, 2.5, SILME)
    c.kutu(g, 0.55, 2.25, 0, 0.62, 0.0, 0.12, TAS)                                             # iki basamak
    c.kutu(g, 0.62, 2.18, 0, 0.36, 0.12, 0.24, TAS3)
    c.kutu(g, 0.5, 2.3, 0, 0.7, 2.68, 2.76, VR)                                                # kanopi
    c.kutu(g, 0.46, 2.34, 0, 0.74, 2.76, 2.8, SILME)
    for uu in (0.6, 2.1):
        prizma(g, [c.p(uu, 0.0, 2.68), c.p(uu, 0.58, 2.68), c.p(uu, 0.0, 2.2)], (c.u[0] * 0.1, c.u[1] * 0.1, 0), SILME2)
    p = c.p(2.38, 0.18, 1.9)                                                                   # duvar feneri
    kutu(g, p[0] - 0.07, p[0] + 0.07, p[1] - 0.07, p[1] + 0.07, 1.9, 2.12, ISIK, atla=())
    kutu(g, p[0] - 0.1, p[0] + 0.1, p[1] - 0.1, p[1] + 0.1, 2.12, 2.16, DEMIR, atla=())
    kutu(g, p[0] - 0.05, p[0] + 0.05, p[1] - 0.05, p[1] + 0.05, 1.86, 1.9, DEMIR, atla=())
    c.kutu(g, 2.36, 2.4, 0, 0.18, 2.16, 2.2, DEMIR)
    c.kutu(g, 0.32, 0.6, 0, 0.03, 1.55, 1.75, PM("Emaye", "#3d5a8a"))                          # ev numarası
    c.kutu(g, 0.39, 0.53, 0.03, 0.04, 1.6, 1.7, SILME)
    c.kutu(g, 0.3, 0.62, 0, 0.14, 1.0, 1.32, VR)                                               # posta kutusu
    c.kutu(g, 0.34, 0.58, 0.14, 0.15, 1.22, 1.26, DEMIR)
    _saksi_cifti(g, c, 0.36, 2.44, 0.42)


@parca("konut", "Merdivenli kapı (sıra ev: korkuluklu basamak, üst ışıklık)")
def kapi_merdivenli(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.82, 1.98, 0, 0.1, 0.5, 2.82, SILME)
    c.kutu(g, 0.95, 1.85, 0.02, 0.06, 0.5, 2.3, VR)
    for a, b in ((1.02, 1.36), (1.44, 1.78)):
        c.kutu(g, a, b, 0.06, 0.075, 0.62, 1.3, D2)
        c.kutu(g, a, b, 0.06, 0.075, 1.42, 2.2, D2)
    c.kutu(g, 0.95, 1.85, 0.02, 0.04, 2.36, 2.72, CAM)
    c.kutu(g, 1.38, 1.42, 0.04, 0.06, 2.36, 2.72, SILME)
    c.kutu(g, 1.68, 1.75, 0.06, 0.12, 1.35, 1.45, PIRINC)
    c.kutu(g, 0.6, 2.2, 0, 0.8, 0.0, 0.5, TAS)
    for i in range(3):
        c.kutu(g, 0.7, 2.1, 0.8 + i * 0.28, 1.08 + i * 0.28, 0.0, 0.5 - (i + 1) * 0.125, TAS3 if i % 2 else TAS2)
    for uu in (0.66, 2.14):
        _basamak_korkuluk(g, c, uu, 0.72, 1.6, 0.5, 0.06)
    c.kutu(g, 0.7, 2.1, 0, 0.5, 2.88, 2.94, SILME2)                                            # küçük saçak
    c.kutu(g, 2.2, 2.46, 0, 0.03, 1.6, 1.8, PM("Emaye", "#3d5a8a"))
    p = c.p(0.5, 0.16, 2.0)
    kutu(g, p[0] - 0.07, p[0] + 0.07, p[1] - 0.07, p[1] + 0.07, 2.0, 2.2, ISIK, atla=())
    kutu(g, p[0] - 0.1, p[0] + 0.1, p[1] - 0.1, p[1] + 0.1, 2.2, 2.24, DEMIR, atla=())


DUZ_VARYANT = ["duvar_duz_tugla", "duvar_duz_kutu"]


def duz_varyant(x, y, i, k, tohum):
    """Düz apartman duvarının varyantı (altıda bir tuğla yamalı, altıda bir klimalı / kutulu). C#: AtolyeKurucu.DuzVaryant."""
    v = karma(x * 4 + i, y, k, tohum + 61) % 6
    return DUZ_VARYANT[v] if v < len(DUZ_VARYANT) else "duvar_duz"


# ---------------------------------------------------------------- iç mekân (zemin kat; çatı kalkınca görünür)
# Mobilya parçaları bir hücreye sığar; ön yüzü (−Y) müşterinin durduğu taraf (kapı koridoruna bakar), arkası hücrenin dibinde.

def _ic(aciklama):
    return parca("ic", aciklama)


def _urunler(g, x0, x1, y, z, rnd, h=0.24, d=0.16, w0=0.2, w1=0.42):
    renk = ("#d94f3d", "#e8a23a", "#f2d04a", "#5a9a4a", "#2f7d8c", "#3d5a8a", "#8a4a6b", "#f4f0e6", "#7ab8d6", "#e07a9a")
    x = x0
    while x < x1 - 0.12:
        w = rnd.uniform(w0, w1)
        kutu(g, x, min(x1, x + w) - 0.03, y - d, y, z, z + rnd.uniform(h * 0.6, h), PM("Urun", rnd.choice(renk)), atla=("zn",))
        x += w


@_ic("Zemin: karo")
def ic_zemin_karo(g):
    kutu(g, -A, A, -A, A, 0.0, 0.03, PM("Karo", "#efe6d2"), atla=("zn",))
    for i in range(4):
        for j in range(4):
            if (i + j) % 2 == 0:
                g.yuz([(-A + i * 0.7, -A + j * 0.7, 0.034), (-A + (i + 1) * 0.7, -A + j * 0.7, 0.034), (-A + (i + 1) * 0.7, -A + (j + 1) * 0.7, 0.034), (-A + i * 0.7, -A + (j + 1) * 0.7, 0.034)], PM("Karo", "#a9cfc6"))
    g.sid += 1


@_ic("Zemin: ahşap")
def ic_zemin_ahsap(g):
    for i in range(5):
        kutu(g, -A + i * 0.56, -A + (i + 1) * 0.56, -A, A, 0.0, 0.03 + 0.002 * (i % 2), PM("Parke", "#d9b58a" if i % 2 else "#cfa878"), atla=("zn",))


@_ic("Zemin: beton")
def ic_zemin_beton(g):
    kutu(g, -A, A, -A, A, 0.0, 0.03, PM("Beton", "#cfcac0"), atla=("zn",))
    kutu(g, -0.06, 0.06, -A, A, 0.03, 0.034, PM("Beton", "#e8d27a"), atla=("zn",))


@_ic("Tavan lambası (sarkıt)")
def ic_lamba(g):
    cubuk(g, (0, 0, KAT - 0.05), (0, 0, KAT - 0.6), 0.025, DEMIR)
    dsil(g, 0, 0, KAT - 0.82, KAT - 0.6, 0.26, 8, VR, r1=0.07, kapak=(False, True))
    meyve(g, 0, 0, KAT - 0.84, 0.09, ISIK, n=6, rz=-0.1)


@_ic("Kasa bankosu")
def ic_kasa(g):
    kutu(g, -1.1, 1.1, 0.0, 0.6, 0.0, 1.0, VR)
    kutu(g, -1.15, 1.15, -0.05, 0.65, 1.0, 1.05, AHSAP, atla=())
    kutu(g, 0.45, 0.9, 0.12, 0.5, 1.05, 1.18, PM("Kasa", "#4a4550"))
    kutu(g, 0.49, 0.85, 0.4, 0.48, 1.18, 1.46, PM("Kasa", "#4a4550"))
    kutu(g, 0.53, 0.81, 0.39, 0.4, 1.24, 1.41, ISIK, atla=())
    kutu(g, -0.9, -0.5, 0.15, 0.45, 1.05, 1.1, PM("Metal", "#c9ccd0"))
    _urunler(g, -1.0, 1.0, -0.05, 0.25, random.Random(2), 0.14, 0.1)


@_ic("Market rafı (çift yüzlü)")
def ic_raf(g):
    kutu(g, -1.15, 1.15, -0.05, 0.75, 0.0, 0.1, SILME2)
    kutu(g, -1.15, 1.15, 0.32, 0.38, 0.1, 1.5, SILME2)
    rnd = random.Random(7)
    for i, z in enumerate((0.12, 0.55, 0.98)):
        dd = 0.36 - i * 0.07
        kutu(g, -1.15, 1.15, 0.35 - dd, 0.35 + dd, z - 0.03, z, SILME, atla=())
        _urunler(g, -1.1, 1.1, 0.35 - dd + 0.18, z, rnd, w0=0.4, w1=0.7)
        _urunler(g, -1.1, 1.1, 0.35 + dd, z, rnd, w0=0.4, w1=0.7)


@_ic("Soğutucu dolap")
def ic_sogutucu(g):
    kutu(g, -1.2, 1.2, 0.5, 1.2, 0.0, 2.0, SILME)
    kutu(g, -1.1, 1.1, 0.48, 0.5, 0.3, 1.7, ISIK, atla=())
    kutu(g, -1.2, 1.2, 0.44, 0.5, 1.7, 2.0, PM("Sogutucu", "#5f8fb8"))
    rnd = random.Random(4)
    for z in (0.32, 0.78, 1.24):
        kutu(g, -1.1, 1.1, 0.3, 0.5, z - 0.03, z, PM("Metal", "#c9ccd0"), atla=())
        for i in range(6):
            kutu(g, -1.02 + i * 0.36, -0.76 + i * 0.36, 0.34, 0.46, z, z + 0.3, PM("Sise", rnd.choice(("#c8453f", "#7fb86a", "#e0a04a", "#bcd6f0", "#6b4a35"))), atla=("zn", "yp"))
    for x in (-1.2, -0.02, 1.16):
        kutu(g, x, x + 0.06, 0.28, 0.5, 0.25, 1.75, PM("Metal", "#9aa3ab"))


@_ic("Meyve sebze standı")
def ic_manav(g):
    for i, (z, y) in enumerate(((0.35, 0.0), (0.6, 0.45), (0.85, 0.9))):
        kutu(g, -1.1, 1.1, y, y + 0.45, 0.0, z, AHSAP)
        for k in range(4):
            x = -0.82 + k * 0.55
            kutu(g, x - 0.24, x + 0.24, y + 0.04, y + 0.41, z, z + 0.1, PM("Ahsap", "#b08a62"))
            for a in range(2):
                meyve(g, x - 0.11 + a * 0.22, y + 0.22, z + 0.1, 0.11, PM("Meyve", ("#d8402f", "#f08a2a", "#8ab84a", "#f2d04a", "#7a3a6a")[(i * 2 + k) % 5]), n=5)


@_ic("Sipariş tezgâhı (menü panolu)")
def ic_tezgah(g):
    kutu(g, -1.2, 1.2, 0.0, 0.55, 0.0, 1.0, VR)
    kutu(g, -1.25, 1.25, -0.05, 0.6, 1.0, 1.05, SILME, atla=())
    for x in (-0.7, 0.5):
        kutu(g, x - 0.18, x + 0.18, 0.15, 0.4, 1.05, 1.3, PM("Kasa", "#4a4550"))
    kutu(g, -1.2, 1.2, 1.25, 1.32, 1.9, 2.6, PM("Tahta", "#4a5a52"))
    for i in range(3):
        kutu(g, -1.05 + i * 0.75, -0.45 + i * 0.75, 1.24, 1.25, 2.0, 2.5, (ISIK, SILME, PM("Afis", "#f5c84a"))[i], atla=())
    kutu(g, -0.45, -0.1, 0.1, 0.4, 1.05, 1.12, PM("Tepsi", "#c8553f"))


@_ic("Mutfak (ızgara, fritöz, davlumbaz)")
def ic_mutfak(g):
    mt = PM("Metal", "#c9ccd0")
    kutu(g, -1.2, 1.2, 0.6, 1.3, 0.0, 0.9, mt)
    kutu(g, -1.1, -0.2, 0.7, 1.2, 0.9, 0.94, PM("Izgara", "#4a4550"), atla=())
    for i in range(3):
        meyve(g, -0.9 + i * 0.3, 0.95, 0.94, 0.1, PM("Simge", "#7a4a35"))
    for x in (0.2, 0.75):
        kutu(g, x, x + 0.4, 0.75, 1.15, 0.9, 1.0, PM("Yag", "#e0b45a"), atla=("zn",))
    kutu(g, -1.2, 1.2, 0.5, 1.35, 2.0, 2.4, mt)
    kutu(g, -0.3, 0.3, 0.8, 1.2, 2.4, KAT, mt)


@_ic("Masa ve dört sandalye")
def ic_masa(g):
    kutu(g, -0.5, 0.5, 0.2, 1.0, 0.7, 0.76, SILME, atla=())
    cubuk(g, (0, 0.6, 0), (0, 0.6, 0.7), 0.1, DEMIR)
    for sx, sy in ((-0.8, 0.6), (0.8, 0.6)):
        kutu(g, sx - 0.2, sx + 0.2, sy - 0.2, sy + 0.2, 0.0, 0.45, VR)
        kutu(g, sx + (0.14 if sx > 0 else -0.2), sx + (0.2 if sx > 0 else -0.14), sy - 0.2, sy + 0.2, 0.45, 0.95, VR)
    dsil(g, 0.15, 0.6, 0.76, 0.9, 0.05, 6, PM("Cicek", "#f5c84a"), kapak=(False, True))


@_ic("Taş fırın (pizza)")
def ic_tas_firin(g):
    tg = PM("Tugla", "#c98a72")
    kutu(g, -0.9, 0.9, 0.4, 1.3, 0.0, 0.9, TAS)
    dsil(g, 0, 0.85, 0.9, 1.5, 0.85, 8, tg, r1=0.5, kapak=(False, True))
    kutu(g, -0.3, 0.3, 0.02, 0.1, 0.95, 1.3, PM("Ates", "#f08a2a"))
    kutu(g, -0.25, 0.25, 0.7, 1.0, 1.5, KAT, tg)
    cubuk(g, (0.5, -0.2, 0.9), (1.1, 0.3, 0.2), 0.05, AHSAP)
    meyve(g, -1.05, 0.5, 0.0, 0.3, PM("Simge", "#f0c060"), n=8, rz=0.08)


@_ic("Döner ocağı ve tezgâh")
def ic_doner(g):
    mt = PM("Metal", "#c9ccd0")
    kutu(g, -1.2, 1.2, 0.2, 0.8, 0.0, 1.05, mt)
    dsil(g, -0.7, 0.5, 1.05, 1.1, 0.25, 8, mt, kapak=(True, True))
    dsil(g, -0.7, 0.5, 1.1, 1.9, 0.2, 8, PM("Simge", "#a8603a"), r1=0.12, kapak=(False, True))
    cubuk(g, (-0.7, 0.5, 1.05), (-0.7, 0.5, 2.1), 0.03, DEMIR)
    kutu(g, -1.05, -0.35, 0.8, 0.86, 1.05, 2.0, PM("Ates", "#f08a2a"))
    for i in range(3):
        kutu(g, 0.35 + i * 0.28, 0.58 + i * 0.28, 0.3, 0.7, 1.05, 1.12, PM("Meyve", ("#d8402f", "#8ab84a", "#f4f0e6")[i]), atla=("zn",))


@_ic("Sushi barı ve tabureler")
def ic_sushi(g):
    kutu(g, -1.2, 1.2, 0.5, 1.0, 0.0, 1.0, AHSAP2)
    kutu(g, -1.25, 1.25, 0.4, 1.05, 1.0, 1.05, PM("Ahsap", "#d9b58a"), atla=())
    for i in range(6):
        kutu(g, -1.0 + i * 0.36, -0.78 + i * 0.36, 0.55, 0.7, 1.05, 1.1, PM("Simge", ("#f0a090", "#f4f0e6", "#7fb86a")[i % 3]), atla=("zn",))
    for i in range(3):
        dsil(g, -0.8 + i * 0.8, 0.0, 0.0, 0.62, 0.05, 6, DEMIR, kapak=(False, False))
        dsil(g, -0.8 + i * 0.8, 0.0, 0.62, 0.7, 0.2, 8, VR, kapak=(True, True))


@_ic("Bar tezgâhı, musluklar, şişe rafı")
def ic_bar(g):
    kutu(g, -1.25, 1.25, 0.3, 0.8, 0.0, 1.05, AHSAP2)
    kutu(g, -1.3, 1.3, 0.22, 0.86, 1.05, 1.1, AHSAP, atla=())
    for x in (-0.75, -0.45, 0.65):
        cubuk(g, (x, 0.6, 1.1), (x, 0.6, 1.4), 0.05, PIRINC)
        cubuk(g, (x, 0.6, 1.38), (x, 0.45, 1.34), 0.04, PIRINC)
    rnd = random.Random(9)
    for z in (1.3, 1.75):
        kutu(g, -1.2, 1.2, 1.15, 1.38, z - 0.04, z, AHSAP, atla=())
        for i in range(10):
            dsil(g, -1.05 + i * 0.23, 1.26, z, z + 0.32, 0.06, 5, PM("Sise", rnd.choice(("#6b4a35", "#7fb86a", "#e0a04a", "#c8453f"))), r1=0.03, kapak=(False, True))
    for i in range(3):
        dsil(g, -0.8 + i * 0.8, -0.15, 0.0, 0.7, 0.05, 6, DEMIR, kapak=(False, False))
        dsil(g, -0.8 + i * 0.8, -0.15, 0.7, 0.78, 0.2, 8, AHSAP, kapak=(True, True))


@_ic("Kahve tezgâhı (makine, pasta dolabı)")
def ic_kahve(g):
    kutu(g, -1.2, 1.2, 0.3, 0.85, 0.0, 0.95, VR)
    kutu(g, -1.25, 1.25, 0.24, 0.9, 0.95, 1.0, AHSAP, atla=())
    kutu(g, 0.35, 1.05, 0.4, 0.8, 1.0, 1.5, PM("Metal", "#c9ccd0"))
    kutu(g, 0.45, 0.95, 0.36, 0.4, 1.1, 1.25, DEMIR)
    kutu(g, -1.1, 0.1, 0.32, 0.8, 1.0, 1.45, VIT)
    for i in range(4):
        meyve(g, -0.95 + i * 0.3, 0.3, 1.0, 0.1, PM("Simge", ("#f0b0c0", "#7a4a35", "#f5e6c0", "#e0a04a")[i]), n=8, rz=0.14)
    kutu(g, -1.2, 1.2, 1.25, 1.32, 1.9, 2.5, PM("Tahta", "#4a5a52"))


@_ic("Koltuk ve sehpa")
def ic_koltuk(g):
    kutu(g, -0.9, 0.9, 0.7, 1.25, 0.0, 0.45, VR)
    kutu(g, -0.9, 0.9, 1.1, 1.3, 0.45, 0.95, VR)
    for sx in (-1.0, 0.86):
        kutu(g, sx, sx + 0.14, 0.7, 1.3, 0.0, 0.62, VR)
    kutu(g, -0.4, 0.4, 0.0, 0.4, 0.0, 0.4, AHSAP)
    kutu(g, -0.15, 0.1, 0.1, 0.3, 0.4, 0.43, SILME, atla=())


@_ic("Ekmek rafı")
def ic_ekmek(g):
    kutu(g, -1.2, 1.2, 1.0, 1.3, 0.0, 2.0, AHSAP)
    for z in (0.5, 1.0, 1.5):
        kutu(g, -1.15, 1.15, 0.6, 1.0, z - 0.04, z, PM("Ahsap", "#d9b58a"), atla=())
        for i in range(6):
            meyve(g, -0.95 + i * 0.38, 0.8, z, 0.15, PM("Simge", ("#d9a05a", "#c98a4a", "#e0b070")[i % 3]), n=6, rz=0.16)
    kutu(g, -1.2, 1.2, 0.0, 0.5, 0.0, 0.9, VIT)
    kutu(g, -1.2, 1.2, 0.0, 0.5, 0.9, 0.95, SILME, atla=())


@_ic("Et tezgâhı (soğutmalı, asılı etler)")
def ic_et(g):
    kutu(g, -1.2, 1.2, 0.2, 0.8, 0.0, 0.8, SILME)
    kutu(g, -1.2, 1.2, 0.2, 0.8, 0.8, 1.05, VIT)
    for i in range(5):
        kutu(g, -1.0 + i * 0.43, -0.7 + i * 0.43, 0.18, 0.2, 0.84, 1.0, PM("Simge", ("#c8453f", "#e08a80", "#a8342f")[i % 3]), atla=())
    cubuk(g, (-1.2, 1.2, 2.2), (1.2, 1.2, 2.2), 0.04, DEMIR)
    for i in range(5):
        dsil(g, -0.9 + i * 0.45, 1.2, 1.5, 2.15, 0.1, 5, PM("Simge", "#b84a3a"), r1=0.05, kapak=(True, False))


@_ic("Çiçek standı")
def ic_cicek(g):
    for i, (z, y) in enumerate(((0.3, 0.2), (0.6, 0.65), (0.9, 1.05))):
        kutu(g, -1.1, 1.1, y, y + 0.4, 0.0, z, SILME2)
        for k in range(4):
            x = -0.8 + k * 0.53
            dsil(g, x, y + 0.2, z, z + 0.3, 0.13, 6, PM("Metal", "#9aa3ab"), r1=0.17, kapak=(False, True), kapak_mat=YAPRAK)
            for a in range(2):
                meyve(g, x - 0.07 + a * 0.14, y + 0.2, z + 0.34, 0.09, CICEK[(i + k + a) % len(CICEK)], n=5)


@_ic("Kitaplık")
def ic_kitaplik(g):
    kutu(g, -1.2, 1.2, 0.9, 1.3, 0.0, 2.1, AHSAP)
    rnd = random.Random(12)
    for z in (0.1, 0.6, 1.1, 1.6):
        kutu(g, -1.15, 1.15, 0.85, 0.9, z, z + 0.42, AHSAP2, atla=())
        x = -1.1
        while x < 1.05:
            w = rnd.uniform(0.3, 0.55)
            kutu(g, x, x + w - 0.01, 0.72, 0.9, z, z + rnd.uniform(0.28, 0.4), PM("Urun", rnd.choice(("#c8453f", "#3f5f8a", "#6fa968", "#d9a441", "#8a5a78", "#f4f0e6"))), atla=("zn",))
            x += w
    kutu(g, -0.6, 0.6, -0.1, 0.4, 0.0, 0.7, AHSAP2)
    _urunler(g, -0.55, 0.55, 0.3, 0.7, rnd, 0.1, 0.3)


@_ic("Eczane rafı ve banko")
def ic_eczane(g):
    kutu(g, -1.2, 1.2, 1.0, 1.3, 0.0, 2.2, SILME)
    rnd = random.Random(5)
    for z in (0.4, 0.85, 1.3, 1.75):
        kutu(g, -1.15, 1.15, 0.8, 1.0, z - 0.03, z, SILME2, atla=())
        x = -1.1
        while x < 1.0:
            kutu(g, x, x + 0.14, 0.82, 0.98, z, z + rnd.uniform(0.15, 0.3), PM("Urun", rnd.choice(("#f4f0e6", "#7ab8d6", "#8ab84a", "#f0b0c0"))), atla=("zn",))
            x += 0.18
    kutu(g, -1.2, 1.2, 0.0, 0.5, 0.0, 1.0, SILME)
    kutu(g, -1.2, 1.2, -0.02, 0.0, 0.3, 0.7, VR, atla=())
    kutu(g, -0.12, 0.12, -0.04, -0.02, 0.35, 0.65, SILME, atla=())
    kutu(g, -0.25, 0.25, -0.04, -0.02, 0.44, 0.56, SILME, atla=())


@_ic("Kamu bankosu (camlı gişe)")
def ic_gise(g):
    kutu(g, -1.25, 1.25, 0.3, 0.8, 0.0, 1.05, AHSAP)
    kutu(g, -1.25, 1.25, 0.5, 0.54, 1.05, 1.9, VIT)
    for x in (-1.25, -0.02, 1.21):
        kutu(g, x, x + 0.06, 0.46, 0.58, 1.05, 1.95, AHSAP2)
    kutu(g, -1.25, 1.25, 0.44, 0.6, 1.9, 2.0, AHSAP2, atla=())
    kutu(g, -0.3, 0.3, 0.4, 0.46, 2.05, 2.35, PM("Kasa", "#4a4550"))
    kutu(g, -0.2, 0.2, 0.39, 0.4, 2.12, 2.28, ISIK, atla=())


@_ic("Bekleme sandalyeleri")
def ic_bekleme(g):
    for i in range(4):
        x = -0.9 + i * 0.6
        kutu(g, x - 0.24, x + 0.24, 0.8, 1.25, 0.38, 0.45, VR, atla=())
        kutu(g, x - 0.24, x + 0.24, 1.2, 1.27, 0.45, 0.95, VR, atla=())
    for x in (-1.1, 1.1):
        kutu(g, x - 0.04, x + 0.04, 0.85, 1.2, 0.0, 0.38, DEMIR)
    cubuk(g, (-1.1, 1.0, 0.36), (1.1, 1.0, 0.36), 0.05, DEMIR)
    saksi_x = -1.0
    dsil(g, saksi_x, 0.2, 0, 0.35, 0.16, 7, SAKSI, r1=0.2, kapak=(False, True), kapak_mat=AHSAP2)
    top(g, (saksi_x, 0.2, 0.7), 0.28, 0.28, 0.4, YAPRAK)


@_ic("Sedye ve serum")
def ic_sedye(g):
    kutu(g, -0.95, 0.95, 0.5, 1.2, 0.55, 0.7, SILME, atla=())
    kutu(g, 0.55, 0.9, 0.6, 1.1, 0.7, 0.8, PM("Yastik", "#bcd6f0"))
    kutu(g, -0.9, 0.4, 0.52, 1.18, 0.7, 0.74, PM("Yastik", "#a9cfc6"), atla=("zn",))
    for sx in (-0.85, 0.85):
        for sy in (0.6, 1.1):
            cubuk(g, (sx, sy, 0.08), (sx, sy, 0.55), 0.05, PM("Metal", "#c9ccd0"))
            dsil(g, sx, sy, 0.0, 0.1, 0.06, 5, DEMIR)
    cubuk(g, (1.15, 0.4, 0), (1.15, 0.4, 1.8), 0.035, PM("Metal", "#c9ccd0"))
    kutu(g, 1.08, 1.22, 0.36, 0.44, 1.45, 1.75, VIT)


@_ic("Ofis masası")
def ic_ofis(g):
    kutu(g, -0.9, 0.9, 0.5, 1.2, 0.72, 0.78, AHSAP, atla=())
    for sx in (-0.85, 0.75):
        kutu(g, sx, sx + 0.1, 0.55, 1.15, 0.0, 0.72, AHSAP2)
    kutu(g, -0.3, 0.3, 0.9, 0.96, 0.78, 1.2, PM("Kasa", "#4a4550"))
    kutu(g, -0.26, 0.26, 0.89, 0.9, 0.84, 1.16, ISIK, atla=())
    kutu(g, 0.45, 0.8, 0.6, 0.85, 0.78, 0.86, SILME)
    kutu(g, -0.25, 0.25, 1.35, 1.4, 0.0, 0.95, VR)
    kutu(g, -0.25, 0.25, 1.0, 1.4, 0.4, 0.47, VR, atla=())


@_ic("İtfaiye aracı")
def ic_itfaiye(g):
    kr = PM("Arac", "#c8342f")
    kutu(g, -1.2, 1.2, 0.2, 1.2, 0.35, 1.3, kr)
    kutu(g, -1.2, -0.4, 0.2, 1.2, 1.3, 1.9, kr)
    kutu(g, -1.22, -1.2, 0.3, 1.1, 1.35, 1.8, VIT, atla=())
    kutu(g, -1.15, -0.5, 0.18, 0.2, 1.4, 1.8, VIT, atla=())
    for x in (-0.75, 0.7):
        for y in (0.2, 1.2):
            silindir(g, (x, y - 0.08, 0.35), (x, y + 0.08, 0.35), 0.35, 0.35, 8, PM("Lastik", "#3d3b42"))
    kutu(g, -0.3, 1.15, 0.45, 0.95, 1.3, 1.42, PM("Metal", "#c9ccd0"))
    for i in range(6):
        kutu(g, -0.2 + i * 0.25, -0.14 + i * 0.25, 0.45, 0.95, 1.42, 1.46, PM("Metal", "#9aa3ab"), atla=())
    kutu(g, -0.95, -0.65, 0.55, 0.85, 1.9, 2.0, PM("Isik", "#7ab8ff"))
    kutu(g, 0.0, 1.0, 0.18, 0.2, 0.75, 0.9, SILME, atla=())


@_ic("Okul sıraları")
def ic_sira(g):
    for j in range(2):
        for i in range(2):
            x, y = -0.6 + i * 1.2, 0.2 + j * 0.9
            kutu(g, x - 0.45, x + 0.45, y, y + 0.35, 0.62, 0.67, PM("Ahsap", "#d9b58a"), atla=())
            kutu(g, x - 0.4, x + 0.4, y - 0.4, y - 0.15, 0.38, 0.43, AHSAP, atla=())
            for sx in (x - 0.42, x + 0.36):
                kutu(g, sx, sx + 0.06, y - 0.4, y + 0.35, 0.0, 0.62, DEMIR)


@_ic("Kara tahta ve öğretmen masası")
def ic_tahta(g):
    kutu(g, -1.2, 1.2, 1.3, 1.36, 0.9, 2.1, PM("Tahta", "#4a5a52"))
    kutu(g, -1.25, 1.25, 1.28, 1.38, 0.84, 0.9, AHSAP, atla=())
    for i in range(3):
        kutu(g, -0.9 + i * 0.3, 0.0 + i * 0.3, 1.29, 1.3, 1.75 - i * 0.25, 1.8 - i * 0.25, SILME, atla=())
    kutu(g, -0.6, 0.6, 0.5, 1.0, 0.0, 0.75, AHSAP)
    top(g, (0.3, 0.75, 0.93), 0.16, 0.16, 0.16, PM("Su", "#7ab8d6"))


@_ic("Okuma masası")
def ic_okuma(g):
    kutu(g, -1.0, 1.0, 0.3, 1.1, 0.7, 0.76, AHSAP, atla=())
    for sx in (-0.9, 0.8):
        kutu(g, sx, sx + 0.1, 0.35, 1.05, 0.0, 0.7, AHSAP2)
    for x in (-0.5, 0.5):
        kutu(g, x - 0.18, x + 0.18, 0.5, 0.75, 0.76, 0.79, SILME)
        cubuk(g, (x, 0.95, 0.76), (x, 0.95, 1.1), 0.03, PIRINC)
        meyve(g, x, 0.95, 1.1, 0.12, PM("Isik", "#8fe0a0"), n=6, rz=0.1)
        kutu(g, x - 0.2, x + 0.2, -0.15, 0.2, 0.0, 0.45, VR)


@_ic("Palet rafı (depo)")
def ic_palet(g):
    mt = PM("Metal", "#3f6fb0")
    for x in (-1.2, 1.14):
        for y in (0.5, 1.24):
            kutu(g, x, x + 0.06, y, y + 0.06, 0.0, 2.6, mt)
    rnd = random.Random(3)
    for z in (0.12, 1.2, 2.2):
        kutu(g, -1.2, 1.2, 0.5, 1.3, z - 0.08, z, PM("Metal", "#e08a45"), atla=())
        for i in range(2):
            x = -0.6 + i * 1.2
            kutu(g, x - 0.5, x + 0.5, 0.55, 1.25, z, z + 0.1, AHSAP)
            kutu(g, x - 0.45, x + 0.45, 0.6, 1.2, z + 0.1, z + rnd.uniform(0.5, 0.85), PM("Karton", "#c49a6c"))


@_ic("Forklift")
def ic_forklift(g):
    sr = PM("Arac", "#e8b83a")
    kutu(g, -0.5, 0.5, 0.3, 1.3, 0.2, 0.9, sr)
    kutu(g, -0.5, 0.5, 0.9, 1.3, 0.9, 1.1, PM("Kasa", "#4a4550"))
    for x in (-0.45, 0.45):
        cubuk(g, (x, 0.35, 0.9), (x, 0.35, 1.9), 0.06, DEMIR)
        cubuk(g, (x, 1.1, 0.9), (x, 1.1, 1.9), 0.06, DEMIR)
        cubuk(g, (x * 0.6, 0.25, 0.1), (x * 0.6, 0.25, 2.1), 0.07, DEMIR)
        kutu(g, x * 0.6 - 0.05, x * 0.6 + 0.05, -0.35, 0.25, 0.1, 0.16, DEMIR)
        for y in (0.45, 1.15):
            silindir(g, (x - 0.06, y, 0.2), (x + 0.06, y, 0.2), 0.2, 0.2, 8, PM("Lastik", "#3d3b42"))
    kutu(g, -0.5, 0.5, 0.3, 1.15, 1.9, 1.95, sr, atla=())
    kutu(g, -0.4, 0.4, -0.3, 0.2, 0.16, 0.7, PM("Karton", "#c49a6c"))


@_ic("Kaldırıcıda araba (tamirhane)")
def ic_lift(g):
    for x in (-1.15, 1.05):
        kutu(g, x, x + 0.1, 0.6, 0.75, 0.0, 2.0, PM("Metal", "#3f6fb0"))
    kutu(g, -1.1, 1.1, 0.35, 1.0, 1.0, 1.08, PM("Metal", "#3f6fb0"), atla=())
    ar = PM("Arac", "#7ab8d6")
    kutu(g, -0.95, 0.95, 0.25, 1.1, 1.2, 1.6, ar)
    kutu(g, -0.5, 0.5, 0.3, 1.05, 1.6, 1.95, VIT)
    for x in (-0.6, 0.6):
        for y in (0.25, 1.1):
            silindir(g, (x, y - 0.06, 1.2), (x, y + 0.06, 1.2), 0.22, 0.22, 8, PM("Lastik", "#3d3b42"))
    kutu(g, 1.25, 1.38, 0.9, 1.3, 0.0, 0.9, PM("Arac", "#c8342f"))


@_ic("Makine bandı (fabrika)")
def ic_makine(g):
    mt = PM("Metal", "#9aa3ab")
    kutu(g, -1.3, 1.3, 0.5, 1.0, 0.5, 0.62, PM("Lastik", "#3d3b42"))
    for x in (-1.2, 0.0, 1.1):
        kutu(g, x, x + 0.1, 0.55, 0.95, 0.0, 0.5, mt)
    kutu(g, -0.5, 0.4, 0.3, 1.2, 0.62, 1.6, PM("Makine", "#5f8f6a"))
    kutu(g, -0.3, 0.2, 0.28, 0.3, 1.0, 1.4, ISIK, atla=())
    dsil(g, -0.05, 0.75, 1.6, 2.3, 0.15, 6, mt)
    for i in range(3):
        kutu(g, 0.55 + i * 0.25, 0.72 + i * 0.25, 0.65, 0.85, 0.62, 0.8, PM("Karton", "#c49a6c"))


@_ic("Resepsiyon (otel / hastane)")
def ic_resepsiyon(g):
    kutu(g, -1.2, 1.2, 0.4, 0.9, 0.0, 1.1, VR)
    kutu(g, -1.25, 1.25, 0.34, 0.96, 1.1, 1.16, PM("Mermer", "#f4f0e6"), atla=())
    kutu(g, -1.2, 1.2, 0.38, 0.4, 0.2, 0.9, PIRINC, atla=())
    dsil(g, 0.8, 0.65, 1.16, 1.28, 0.09, 6, PIRINC, r1=0.02)
    kutu(g, -0.9, -0.5, 0.5, 0.8, 1.16, 1.2, SILME)
    kutu(g, -1.1, 1.1, 1.3, 1.36, 1.3, 2.2, AHSAP)
    for i in range(8):
        kutu(g, -0.95 + i * 0.26, -0.8 + i * 0.26, 1.28, 1.3, 1.5 + (i % 2) * 0.3, 1.62 + (i % 2) * 0.3, PIRINC, atla=())


@_ic("Koltuk sıraları (opera)")
def ic_koltuklar(g):
    kr = PM("Kadife", "#a8342f")
    for j in range(3):
        for i in range(4):
            x, y = -0.9 + i * 0.6, 0.0 + j * 0.5
            kutu(g, x - 0.24, x + 0.24, y, y + 0.3, 0.1 * j, 0.42 + 0.1 * j, kr)
            kutu(g, x - 0.24, x + 0.24, y + 0.3, y + 0.38, 0.1 * j, 0.9 + 0.1 * j, kr)


@_ic("Sahne ve perde")
def ic_sahne(g):
    kutu(g, -A, A, 0.3, A, 0.0, 0.8, AHSAP)
    kr = PM("Kadife", "#a8342f")
    for i in range(7):
        x = -A + i * 0.4
        if 2 <= i <= 4:
            kutu(g, x, x + 0.4, A - 0.15, A - 0.05, 2.3, KAT, kr)
        else:
            kutu(g, x, x + 0.4, A - 0.15 - 0.03 * (i % 2), A - 0.05, 0.8, KAT, kr)
    kutu(g, -0.3, 0.3, 0.7, 1.0, 0.8, 1.4, PM("Kasa", "#4a4550"))


@_ic("Pazar tezgâhı")
def ic_pazar(g):
    kutu(g, -1.1, 1.1, 0.3, 1.1, 0.0, 0.85, AHSAP)
    for x in (-1.05, 1.05):
        cubuk(g, (x, 0.35, 0.85), (x, 0.35, 2.1), 0.06, AHSAP2)
        cubuk(g, (x, 1.05, 0.85), (x, 1.05, 2.1), 0.06, AHSAP2)
    for i in range(6):
        kutu(g, -1.2 + i * 0.4, -0.8 + i * 0.4, 0.2, 1.2, 2.1, 2.16, TN if i % 2 == 0 else SILME, atla=())
    for k in range(4):
        x = -0.8 + k * 0.53
        for b in range(3):
            meyve(g, x, 0.45 + b * 0.22, 0.85, 0.12, PM("Meyve", ("#d8402f", "#f08a2a", "#8ab84a", "#f2d04a")[k]), n=5)


@_ic("Yürüyen merdiven")
def ic_yuruyen(g):
    mt = PM("Metal", "#9aa3ab")
    prizma(g, [(-0.5, -0.2, 0.0), (-0.5, 1.3, 0.0), (-0.5, 1.3, 2.6)], (1.0, 0, 0), mt)
    for i in range(9):
        t = i / 9.0
        kutu(g, -0.4, 0.4, -0.2 + 1.5 * t, -0.2 + 1.5 * (t + 0.11), 2.6 * t, 2.6 * t + 0.3, PM("Lastik", "#3d3b42"))
    for x in (-0.5, 0.44):
        cubuk(g, (x + 0.03, -0.2, 0.9), (x + 0.03, 1.3, 3.5), 0.06, PM("Lastik", "#3d3b42"))


@_ic("Mağaza askılığı ve raf")
def ic_askilik(g):
    cubuk(g, (-1.0, 0.7, 1.5), (1.0, 0.7, 1.5), 0.04, PM("Metal", "#c9ccd0"))
    for x in (-1.0, 1.0):
        cubuk(g, (x, 0.7, 0), (x, 0.7, 1.5), 0.04, PM("Metal", "#c9ccd0"))
    for i in range(9):
        kutu(g, -0.85 + i * 0.2, -0.72 + i * 0.2, 0.5, 0.9, 0.7, 1.45, PM("Urun", ("#d94f3d", "#3d5a8a", "#f2d04a", "#5a9a4a", "#e07a9a", "#f4f0e6")[i % 6]))
    kutu(g, -0.5, 0.5, -0.2, 0.2, 0.0, 0.5, SILME)
    _urunler(g, -0.45, 0.45, 0.15, 0.5, random.Random(6), 0.15, 0.3)


@_ic("Oturma odası (koltuk, halı, lamba)")
def ic_oturma(g):
    kutu(g, -1.1, 1.1, -0.9, 0.7, 0.03, 0.045, PM("Hali", "#c98a8a"), atla=("zn",))
    kutu(g, -0.9, 0.9, 0.75, 1.25, 0.0, 0.45, PM("Kumas", "#7a9ab8"))
    kutu(g, -0.9, 0.9, 1.1, 1.3, 0.45, 0.95, PM("Kumas", "#7a9ab8"))
    kutu(g, -0.4, 0.4, -0.1, 0.3, 0.0, 0.4, AHSAP)
    cubuk(g, (1.15, 1.1, 0), (1.15, 1.1, 1.5), 0.04, DEMIR)
    dsil(g, 1.15, 1.1, 1.5, 1.8, 0.22, 7, ISIK, r1=0.12, kapak=(False, True))
    kutu(g, -1.3, -1.0, 0.4, 1.2, 0.0, 1.6, AHSAP2)


@_ic("Hortum makaraları ve hortum rafı (itfaiye)")
def ic_hortum(g):
    kr = PM("Arac", "#c8342f")
    for x in (-0.7, 0.5):
        silindir(g, (x - 0.14, 1.05, 1.1), (x + 0.14, 1.05, 1.1), 0.42, 0.42, 10, kr)
        silindir(g, (x - 0.1, 1.05, 1.1), (x + 0.1, 1.05, 1.1), 0.34, 0.34, 10, PM("Hortum", "#e8d9a8"))
        kutu(g, x - 0.2, x + 0.2, 1.0, 1.35, 0.0, 0.7, PM("Metal", "#9aa3ab"))
    kutu(g, -1.25, 1.25, 1.1, 1.38, 1.9, 1.96, PM("Metal", "#9aa3ab"), atla=())
    for i in range(5):
        kutu(g, -1.1 + i * 0.46, -0.75 + i * 0.46, 1.12, 1.36, 1.96, 2.12, PM("Hortum", "#e8d9a8" if i % 2 else "#d9c48a"))
    dsil(g, 1.15, 0.9, 0.0, 0.62, 0.11, 6, kr, kapak=(False, True))
    dsil(g, 1.15, 0.9, 0.62, 0.74, 0.05, 5, DEMIR)


@_ic("Soyunma dolapları ve bank")
def ic_dolap(g):
    for i in range(4):
        x = -1.2 + i * 0.6
        kutu(g, x, x + 0.56, 0.9, 1.35, 0.0, 1.9, PM("Metal", "#7a9ab8" if i % 2 else "#6b8aa8"))
        kutu(g, x + 0.1, x + 0.46, 0.88, 0.9, 1.5, 1.7, DEMIR, atla=())
        kutu(g, x + 0.44, x + 0.5, 0.86, 0.9, 0.9, 1.0, PM("Metal", "#c9ccd0"))
    kutu(g, -1.0, 1.0, 0.2, 0.55, 0.42, 0.48, AHSAP, atla=())
    for x in (-0.9, 0.82):
        kutu(g, x, x + 0.08, 0.25, 0.5, 0.0, 0.42, DEMIR)


@_ic("Ekipman askısı (ceket, kask, çizme)")
def ic_ekipman(g):
    cubuk(g, (-1.2, 1.25, 1.7), (1.2, 1.25, 1.7), 0.05, DEMIR)
    kutu(g, -1.25, 1.25, 1.1, 1.38, 1.95, 2.0, AHSAP, atla=())
    for i in range(5):
        x = -1.0 + i * 0.5
        kutu(g, x - 0.19, x + 0.19, 1.12, 1.3, 0.85, 1.68, PM("Ceket", "#e8b83a"))
        kutu(g, x - 0.19, x + 0.19, 1.1, 1.12, 1.1, 1.18, SILME, atla=())
        meyve(g, x, 1.24, 2.0, 0.17, PM("Arac", "#c8342f"), n=6, rz=0.2)
        kutu(g, x - 0.12, x + 0.12, 1.0, 1.3, 0.0, 0.35, PM("Lastik", "#3d3b42"))


@_ic("Muayene yatağı (perde bölmeli)")
def ic_muayene(g):
    mt = PM("Metal", "#c9ccd0")
    kutu(g, -0.5, 0.45, 0.3, 1.3, 0.5, 0.62, SILME, atla=())
    kutu(g, -0.45, 0.4, 1.0, 1.28, 0.62, 0.72, PM("Yastik", "#bcd6f0"))
    kutu(g, -0.5, 0.45, 0.32, 0.95, 0.62, 0.66, PM("Yastik", "#a9cfc6"), atla=("zn",))
    for x in (-0.45, 0.4):
        for y in (0.38, 1.22):
            cubuk(g, (x, y, 0.0), (x, y, 0.5), 0.05, mt)
    cubuk(g, (-1.2, 0.1, 2.1), (-1.2, 1.38, 2.1), 0.04, mt)
    cubuk(g, (-1.2, 0.1, 2.1), (0.2, 0.1, 2.1), 0.04, mt)
    for i in range(4):
        kutu(g, -1.22 - 0.02 * (i % 2), -1.18, 0.14 + i * 0.31, 0.45 + i * 0.31, 0.35, 2.08, PM("Perde", "#a9cfc6" if i % 2 else "#bfe0d8"))
    for i in range(3):
        kutu(g, -1.15 + i * 0.3, -0.85 + i * 0.3, 0.08, 0.12 + 0.02 * (i % 2), 0.35, 2.08, PM("Perde", "#bfe0d8" if i % 2 else "#a9cfc6"))
    dsil(g, 0.95, 0.9, 0.0, 0.45, 0.04, 5, mt, kapak=(False, False))
    dsil(g, 0.95, 0.9, 0.45, 0.52, 0.17, 7, VR, kapak=(True, True))
    kutu(g, 0.75, 1.2, 1.1, 1.38, 0.0, 0.8, SILME)


@_ic("Tekerlekli sandalye ve ecza dolabı")
def ic_tekerlekli(g):
    mt = PM("Metal", "#9aa3ab")
    kutu(g, -0.85, -0.35, 0.55, 1.0, 0.45, 0.5, VR, atla=())
    kutu(g, -0.85, -0.35, 0.98, 1.04, 0.5, 1.0, VR, atla=())
    for x in (-0.9, -0.3):
        silindir(g, (x - 0.03, 0.85, 0.32), (x + 0.03, 0.85, 0.32), 0.32, 0.32, 10, mt)
        silindir(g, (x - 0.02, 0.45, 0.1), (x + 0.02, 0.45, 0.1), 0.1, 0.1, 6, DEMIR)
        cubuk(g, (x, 1.04, 0.5), (x, 1.14, 1.05), 0.04, DEMIR)
    kutu(g, 0.3, 1.2, 1.0, 1.38, 0.0, 1.8, SILME)
    kutu(g, 0.36, 0.73, 0.98, 1.0, 0.95, 1.7, VIT, atla=())
    kutu(g, 0.77, 1.14, 0.98, 1.0, 0.95, 1.7, VIT, atla=())
    kutu(g, 0.62, 0.88, 0.96, 0.98, 0.35, 0.61, PM("Arac", "#c8342f"), atla=())
    kutu(g, 0.71, 0.79, 0.94, 0.96, 0.38, 0.58, SILME, atla=())
    kutu(g, 0.65, 0.85, 0.94, 0.96, 0.44, 0.52, SILME, atla=())


@_ic("Bilgi panosu, su sebili, saksı")
def ic_pano(g):
    kutu(g, -1.0, 0.3, 1.3, 1.36, 0.9, 2.0, PM("Tahta", "#d9c9a8"))
    kutu(g, -1.05, 0.35, 1.28, 1.38, 0.84, 0.9, AHSAP, atla=())
    rnd = random.Random(8)
    for i in range(3):
        for j in range(2):
            kutu(g, -0.9 + i * 0.4, -0.6 + i * 0.4, 1.28, 1.3, 1.05 + j * 0.45, 1.4 + j * 0.45, PM("Afis", rnd.choice(("#f4f0e6", "#f5c84a", "#a9cfc6", "#f0b0c0"))), atla=())
    kutu(g, 0.6, 1.0, 0.95, 1.35, 0.0, 1.0, SILME)
    dsil(g, 0.8, 1.15, 1.0, 1.45, 0.15, 7, PM("Su", "#7ab8d6"), kapak=(False, True))
    dsil(g, -1.05, 0.9, 0.0, 0.4, 0.18, 7, SAKSI, r1=0.23, kapak=(False, True), kapak_mat=AHSAP2)
    top(g, (-1.05, 0.9, 0.85), 0.3, 0.3, 0.45, YAPRAK)


@_ic("Saksılı bitkiler ve bank (dolgu)")
def ic_bitki(g):
    for x, h in ((-0.95, 1.0), (0.95, 0.8)):
        dsil(g, x, 1.0, 0.0, 0.45, 0.2, 7, SAKSI, r1=0.26, kapak=(False, True), kapak_mat=AHSAP2)
        top(g, (x, 1.0, 0.45 + h * 0.55), 0.34, 0.34, h * 0.5, YAPRAK)
    kutu(g, -0.55, 0.55, 0.85, 1.2, 0.4, 0.46, AHSAP, atla=())
    kutu(g, -0.55, 0.55, 1.2, 1.26, 0.46, 0.85, AHSAP, atla=())
    for x in (-0.5, 0.42):
        kutu(g, x, x + 0.08, 0.9, 1.2, 0.0, 0.4, DEMIR)


@_ic("Sipariş poşeti (tezgâhta)")
def ic_poset(g):
    kg = PM("Karton", "#c49a6c")
    kati(g, [(-0.12, -0.08, 0), (0.12, -0.08, 0), (0.12, 0.08, 0), (-0.12, 0.08, 0), (-0.14, -0.09, 0.34), (0.14, -0.09, 0.34), (0.14, 0.09, 0.34), (-0.14, 0.09, 0.34)],
         [[4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], kg)
    kutu(g, -0.15, 0.15, -0.035, 0.035, 0.29, 0.35, PM("Karton", "#a8824f"), atla=())
    kutu(g, -0.07, 0.07, -0.092, -0.088, 0.1, 0.22, VR, atla=())


@parca("zemin", "Kepenk (kapalı dükkânda iner)")
def kepenk(g):
    c = on()
    n = 18
    for i in range(n):
        z0, z1 = 2.28 * i / n, 2.28 * (i + 1) / n
        c.kutu(g, 0.12, HUCRE - 0.12, 0.11, 0.15 if i % 2 else 0.135, z0, z1, PM("Kepenk", "#aab1b5") if i % 2 else PM("Kepenk", "#8f979c"))


# ---------------------------------------------------------------- ağaçlar ve çalılar (tohumlu varyantlar)
GOVDE = PM("Govde", "#8a6a55")


def bulut(g, c, rx, ry, rz, tohum, mat=None, kabuk=True, t=0.07):
    """Taç öbeği: pürüzlü küre + (kabuk) biraz büyük, yüzleri ters koyu kopyası: öbeğin dış hattında kalem çizgisi gibi durur."""
    top(g, c, rx, ry, rz, mat or YP, random.Random(tohum), 0.1)
    if kabuk:
        tmp = G("k")
        top(tmp, c, rx + t, ry + t, rz + t, KABUK, random.Random(tohum), 0.1)
        for f in tmp.f:
            g.yuz([tmp.v[i] for i in reversed(f)], KABUK)
        g.sid += 1


def _govde(g, h, rnd, hafif, kal=0.3):
    if hafif:
        cubuk(g, (0, 0, 0), (0, 0, h), kal * 0.85, GOVDE)
        return (0.0, 0.0, h)
    ex, ey = rnd.uniform(-0.3, 0.3), rnd.uniform(-0.2, 0.2)
    orta = (ex, ey, h * 0.55)
    ust = (ex * 0.3, ey * 0.3, h)
    cubuk(g, (0, 0, 0), orta, kal, GOVDE)
    cubuk(g, orta, ust, kal * 0.75, GOVDE)
    return ust


def _agac(g, tip, tohum, hafif=False):
    rnd = random.Random(tohum * 31 + len(tip))
    kb = not hafif
    if tip == "yuvarlak":
        h = rnd.uniform(2.0, 2.8)
        u = _govde(g, h, rnd, hafif)
        bulut(g, (u[0], u[1], h + 1.0), 1.45, 1.4, 1.2, tohum, kabuk=kb)
        if not hafif:
            n = 1 + tohum % 2
            for i in range(n):
                a = rnd.uniform(0, 6.28)
                bulut(g, (u[0] + math.cos(a) * 1.0, u[1] + math.sin(a) * 1.0, h + 0.5 + rnd.uniform(0, 0.9)), 0.95, 0.95, 0.8, tohum + i + 3, mat=YP2 if i else YP)
            if tohum % 2 == 0:       # çatallı gövde: yan dal
                cubuk(g, (u[0] * 0.5, u[1] * 0.5, h * 0.5), (u[0] + 0.7, u[1] - 0.3, h + 0.2), 0.16, GOVDE)
    elif tip == "cam":
        h = rnd.uniform(0.8, 1.3)
        cubuk(g, (0, 0, 0), (0, 0, h + 0.3), 0.26, GOVDE)
        kat = 2 if hafif else 3 + tohum % 2
        r, z = rnd.uniform(1.25, 1.5), h
        for i in range(kat):
            dz = 1.7 - i * 0.15
            dsil(g, 0, 0, z, z + dz, r, 7, YP2 if i % 2 else YP, r1=0.0, kapak=(True, False))
            z += dz * 0.55
            r *= 0.74
    elif tip == "sogut":
        h = rnd.uniform(2.2, 2.7)
        u = _govde(g, h, rnd, hafif, 0.34)
        bulut(g, (u[0], u[1], h + 0.7), 1.7, 1.6, 1.0, tohum, kabuk=kb)
        if not hafif:
            for i in range(9):
                a = i * 0.7 + rnd.uniform(-0.1, 0.1)
                x, y = u[0] + math.cos(a) * 1.45, u[1] + math.sin(a) * 1.35
                L = rnd.uniform(1.0, 1.7)
                obox(g, (x, y, h + 0.5 - L / 2), (0.17 * math.sin(a), -0.17 * math.cos(a), 0), (0.05 * math.cos(a), 0.05 * math.sin(a), 0), (0, 0, L / 2), YP2)
    elif tip == "kavak":
        h = rnd.uniform(1.0, 1.5)
        cubuk(g, (0, 0, 0), (0, 0, h + 0.5), 0.24, GOVDE)
        bulut(g, (0, 0, h + 2.4), 0.75, 0.75, 2.5, tohum, kabuk=kb)
        if not hafif:
            bulut(g, (0.25, 0.1, h + 1.2), 0.6, 0.6, 1.2, tohum + 5, mat=YP2, kabuk=False)
    elif tip == "meyve":
        h = rnd.uniform(1.4, 1.8)
        u = _govde(g, h, rnd, hafif, 0.24)
        bulut(g, (u[0], u[1], h + 0.7), 1.15, 1.1, 0.9, tohum, kabuk=kb)
        if not hafif:
            bulut(g, (u[0] + 0.6, u[1] + 0.3, h + 0.5), 0.75, 0.7, 0.6, tohum + 2, mat=YP2)
            mm = PM("Meyve", ("#d8402f", "#f08a2a", "#f2b6c8")[tohum % 3])
            for i in range(8):
                a, b = rnd.uniform(0, 6.28), rnd.uniform(-0.3, 0.9)
                rr = 1.12 * math.cos(b * 0.9)
                meyve(g, u[0] + math.cos(a) * rr, u[1] + math.sin(a) * rr * 0.95, h + 0.65 + math.sin(b) * 0.8, 0.1, mm, n=5)
    elif tip == "ciplak":
        h = rnd.uniform(2.0, 2.6)
        u = _govde(g, h, rnd, hafif)
        for i in range(2 if hafif else 5):
            a = i * 1.3 + rnd.uniform(-0.2, 0.2)
            L = rnd.uniform(0.9, 1.5)
            bas = (u[0] * 0.6, u[1] * 0.6, h * rnd.uniform(0.6, 1.0))
            uc = (bas[0] + math.cos(a) * L, bas[1] + math.sin(a) * L, bas[2] + L * 0.9)
            cubuk(g, bas, uc, 0.12, GOVDE)
            if not hafif:
                cubuk(g, uc, (uc[0] + math.cos(a + 0.8) * 0.5, uc[1] + math.sin(a + 0.8) * 0.5, uc[2] + 0.5), 0.07, GOVDE)
    else:   # çalı
        bulut(g, (0, 0, 0.45), 0.7, 0.65, 0.5, tohum, kabuk=kb)
        if not hafif and tohum % 3:
            bulut(g, (0.55, 0.15, 0.35), 0.5, 0.45, 0.38, tohum + 4, mat=YP2)
        if tohum % 3 == 2 and not hafif:
            for i in range(6):
                a = i * 1.05
                meyve(g, math.cos(a) * 0.45, math.sin(a) * 0.42, 0.7 + 0.1 * (i % 2), 0.08, CICEK[i % len(CICEK)], n=5)


AGAC_TIP = [("yuvarlak", 4, "Ağaç: yuvarlak taçlı", "agac"), ("cam", 3, "Ağaç: çam", "agac"), ("sogut", 2, "Ağaç: söğüt", "agac"),
            ("kavak", 2, "Ağaç: kavak", "agac"), ("meyve", 3, "Ağaç: meyve ağacı", "agac"), ("ciplak", 1, "Ağaç: çıplak (kış)", "agac"),
            ("cali", 3, "Çalı", "cali")]
for _tip, _n, _acik, _hedef in AGAC_TIP:
    for _i in range(1, _n + 1):
        _ad = ("cali_%d" % _i) if _tip == "cali" else "agac_%s_%d" % (_tip, _i)
        ekle(_ad, "agac", "%s %d" % (_acik, _i), (lambda g, t=_tip, s=_i: _agac(g, t, s)), _hedef, hafif=(lambda g, t=_tip, s=_i: _agac(g, t, s, True)))


# ================================== LONDRA MİMARİSİ (v6) ==================================
# Sanat stili aynı (pastel renk, kalem kontur, kâğıt); binaların MİMARİSİ Londra. Bu bölüm:
#   • ortak yardımcılar: konturu çizilmeyen ince "iz" yüzeyleri (Pastel_Iz_*: tuğla / arduvaz sırası, cam kayıtları, demir
#     çubuklar), tek yüzlü levhalar, giyotin (sash) pencere, kemerler, mızrak başlı dökme demir korkuluk, bodrum aydınlığı;
#   • eski parçaların Londra hâli (aynı adla: pencereler, dükkân cephesi, tabela panosu, tente, çatı karoları, saçak, baca…);
#   • yeni duvar üslupları (bloğun tarz alanı 5 ve üstü, USLUP tablosu): Georgian sıra ev, Victorian tuğla, beyaz sıva
#     (Belgravia), mews, Portland taşı (neoklasik), Victorian gotik, tuğla depo;
#   • parapet arkasında arduvaz mansart çatı (CT_PARAPET) ve sıra bacalar; Londra sokak donatıları.
# Kural değişiklikleri (yerlesim / _cati / kenarlar) AtolyeKurucu.cs ile aynı tutulur; USLUP tablosu JSON'a yazılır.

DEMIR_S = PM("Demir", "#3b3a42")            # siyah dökme demir (saf siyah değil)
SIVA_L = PM("Siva", "#f4efe2")              # krem-beyaz sıva (stucco)
SIVA_L2 = PM("Siva", "#e4dccb")
PORTLAND = PM("Tas", "#e6e2d8")             # Portland taşı
PORTLAND2 = PM("Tas", "#d3cec2")
KURSUN = PM("Kursun", "#aab2ba")            # kurşun kaplama (çatı penceresi, cumba üstü)
KULAH = PM("Kulah", "#d39472")              # seramik baca külahı
KULAH2 = PM("Kulah", "#e6caa0")
BOYA_SARI = PM("Boya", "#f0c94a")
KIRMIZI = PM("Kirmizi", "#c8413a")          # telefon kulübesi, posta kutusu
IZ_D = ROL("Iz", "d", "#a9c6d8")            # duvarın koyusu (tuğla sırası izi)
IZ_C = ROL("Iz", "c", "#ad4835")            # çatının koyusu (arduvaz sırası izi)
IZ_BEYAZ = PM("Iz", "#fbf7ee")              # cam kayıtları
IZ_SIVA = PM("Iz", "#dcd4c2")               # sıva derzi (rustika)
IZ_TAS = PM("Iz", "#c9c3b6")
IZ_DEMIR = PM("Iz", "#3b3a42")              # korkuluk çubukları
IZ_GOLGE = PM("Iz", "#77727e")              # bodrum aydınlığının tabanı
IZ_PERDE = [PM("Iz", "#f3e7cf"), PM("Iz", "#e6c6bd"), PM("Iz", "#c8dac3"), PM("Iz", "#d6cde3")]
IZ_KARO = [PM("Iz", "#3b3a42"), PM("Iz", "#f4efe2"), PM("Iz", "#c98a72")]
IZ_PIRINC = PM("Iz", "#d9b45a")
IZ_KABARTMA = PM("Iz", "#d8d2dc")           # kapı panellerinin ışık alan kenarı
CAM_BUZLU = PM("Cam", "#dbe7e6")            # pub'ın buzlu camı


def levha(g, pts, mat, cift=False):
    """Tek yüzlü ince levha (kendi katısı). cift: arka yüzü de çizilir."""
    g.sid += 1
    g.yuz(list(pts), mat)
    if cift:
        g.yuz(list(pts)[::-1], mat)
    g.sid += 1


def cokgen(g, c, uz, mat, o=0.03, cift=False):
    """Cephe düzleminde levha: (u, z) köşeleri dışarıdan bakınca saat yönünün tersine."""
    levha(g, [c.p(u, o, z) for u, z in uz], mat, cift)


def iz(g, c, u0, u1, z0, z1, mat, o=0.012):
    """Cephe yüzünde dikdörtgen iz (malzemesi Pastel_Iz_* ise konturu çizilmez)."""
    cokgen(g, c, [(u0, z0), (u1, z0), (u1, z1), (u0, z1)], mat, o)


def yer_levha(g, c, u0, u1, o0, o1, z, mat):
    """Yukarı bakan yatay levha (cephe ekseninde: u boyunca, o dışarı)."""
    levha(g, [c.p(u0, o1, z), c.p(u1, o1, z), c.p(u1, o0, z), c.p(u0, o0, z)], mat)


def tugla_izi(g, c, tohum, n=4, u0=0.12, u1=HUCRE - 0.12, z0=0.15, z1=KAT - 0.2, yasak=None, mat=None):
    """Çok hafif tuğla dokusu: birkaç kısa sıra izi (iki sıra, yarım tuğla kaydırmalı). yasak: (u0, u1, z0, z1) pencere boşluğu."""
    rnd = random.Random(tohum)
    for i in range(n):
        w = rnd.uniform(0.3, 0.55)
        u = rnd.uniform(u0, u1 - w - 0.15)
        z = z0 + (z1 - z0) * (i + rnd.uniform(0.15, 0.85)) / n
        if yasak and u + w + 0.15 > yasak[0] - 0.12 and u < yasak[1] + 0.12 and z + 0.12 > yasak[2] - 0.2 and z < yasak[3] + 0.35:
            continue
        iz(g, c, u, u + w, z, z + 0.018, mat or IZ_D)
        iz(g, c, u + 0.13, u + w * 0.6 + 0.13, z + 0.085, z + 0.103, mat or IZ_D)


def rustika(g, c, u0=0.0, u1=HUCRE, z0=0.0, z1=KAT - 0.12, adim=0.44, yasak=None, mat=None):
    """Rustika bantları: sıvada yatay derz izleri. yasak: (u0, u1, z0, z1) boşluk (derz iki parçaya bölünür)."""
    z = z0 + adim
    while z < z1 - 0.1:
        if yasak and yasak[2] - 0.05 < z < yasak[3] + 0.05:
            if yasak[0] - u0 > 0.1:
                iz(g, c, u0, yasak[0] - 0.06, z, z + 0.022, mat or IZ_SIVA)
            if u1 - yasak[1] > 0.1:
                iz(g, c, yasak[1] + 0.06, u1, z, z + 0.022, mat or IZ_SIVA)
        else:
            iz(g, c, u0, u1, z, z + 0.022, mat or IZ_SIVA)
        z += adim


def _yay(um, zs, r, tip="yarim", n=6):
    """Kemer eğrisinin (u, z) noktaları: sağ üzengiden tepeye, oradan sol üzengiye.
    tip: yarim (yarım daire) · basik (basık yay) · sivri (gotik sivri kemer)."""
    pts = []
    if tip == "sivri":
        m = max(2, n // 2)
        for i in range(m + 1):
            a = (math.pi / 3) * i / m
            pts.append((um - r + 2 * r * math.cos(a), zs + 2 * r * math.sin(a)))
        for i in range(1, m + 1):
            a = 2 * math.pi / 3 + (math.pi / 3) * i / m
            pts.append((um + r + 2 * r * math.cos(a), zs + 2 * r * math.sin(a)))
        return pts
    k = 0.32 if tip == "basik" else 1.0
    for i in range(n + 1):
        a = math.pi * i / n
        pts.append((um + r * math.cos(a), zs + r * k * math.sin(a)))
    return pts


def kemer_halka(g, c, um, zs, r, t, mat, tip="yarim", n=6, o=0.05):
    """Kemer sövesi: iç yarıçap r, kalınlık t; dilim dilim levha (konturu çizilir)."""
    ic = _yay(um, zs, r, tip, n)
    f = (r + t) / r
    dis = [(um + (u - um) * f, zs + (z - zs) * f) for u, z in ic]
    g.sid += 1
    for i in range(len(ic) - 1):
        g.yuz([c.p(ic[i][0], o, ic[i][1]), c.p(dis[i][0], o, dis[i][1]), c.p(dis[i + 1][0], o, dis[i + 1][1]), c.p(ic[i + 1][0], o, ic[i + 1][1])], mat)
    g.sid += 1


def sash(g, c, u0, u1, z0, z1, bolme=(3, 2), bas="duz", perde=1, cam=CAM, t=0.06, d=0.05, denizlik=True, cerceve=SILME):
    """Giyotin (sash) pencere: ince beyaz kasa, ortada buluşma kaydı, cam kayıtları iz olarak (6/6 → bolme=(3, 2),
    2/2 → (2, 1), 1/1 → (1, 1)), taş denizlik. bas: duz · yarim · basik · sivri (kemerli baş; z1 üzengi kotu).
    perde: 0 yok · 1 stor (üstte) · 2 iki yanda perde."""
    w = u1 - u0
    um = (u0 + u1) / 2
    govde = [(u0 - t / 2, z0 - t / 2), (u1 + t / 2, z0 - t / 2)]
    if bas == "duz":
        cokgen(g, c, govde + [(u1 + t / 2, z1 + t / 2), (u0 - t / 2, z1 + t / 2)], cam, 0.012)
    else:
        cokgen(g, c, govde + _yay(um, z1, w / 2 + t / 2, bas, 6), cam, 0.012)
    pm = IZ_PERDE[int(round(u0 * 10 + z1 * 7 + w * 3)) % len(IZ_PERDE)]
    if perde == 1:
        iz(g, c, u0, u1, z1 - (z1 - z0) * 0.3, z1, pm, 0.016)
    elif perde == 2:
        iz(g, c, u0, u0 + w * 0.2, z0, z1, pm, 0.016)
        iz(g, c, u1 - w * 0.2, u1, z0, z1, pm, 0.016)
    zm = (z0 + z1) / 2 if bas == "duz" else z0 + (z1 - z0) * 0.56
    nx, ny = bolme
    for i in range(1, nx):                                    # düşey cam kayıtları
        uu = u0 + w * i / nx
        iz(g, c, uu - 0.014, uu + 0.014, z0, z1, IZ_BEYAZ, 0.02)
    for a, b in ((z0, zm), (zm, z1)):                         # her kanatta yatay kayıtlar
        for j in range(1, ny):
            zz = a + (b - a) * j / ny
            iz(g, c, u0, u1, zz - 0.014, zz + 0.014, IZ_BEYAZ, 0.02)
    c.kutu(g, u0 - t, u0, 0, d, z0 - t, z1, cerceve)
    c.kutu(g, u1, u1 + t, 0, d, z0 - t, z1, cerceve)
    c.kutu(g, u0, u1, 0, d, z0 - t, z0, cerceve)
    c.kutu(g, u0, u1, 0.012, d + 0.012, zm - 0.03, zm + 0.03, cerceve)      # buluşma kaydı
    if bas == "duz":
        c.kutu(g, u0 - t, u1 + t, 0, d, z1, z1 + t, cerceve)
    else:
        kemer_halka(g, c, um, z1, w / 2, t, cerceve, bas, 6, d)
    if denizlik:
        c.kutu(g, u0 - t - 0.06, u1 + t + 0.06, 0, 0.14, z0 - t - 0.07, z0 - t, PORTLAND)


def duz_kemer(g, c, u0, u1, z, h=0.24, mat=None):
    """Düz tuğla kemer (gauged arch): pencerenin üstünde yelpaze kesimli lento (levha; konturu çizilir)."""
    cokgen(g, c, [(u0 - 0.06, z), (u1 + 0.06, z), (u1 + 0.17, z + h), (u0 - 0.17, z + h)], mat or D2, 0.022)


def tas_lento(g, c, u0, u1, z, h=0.2, mat=None):
    c.kutu(g, u0 - 0.14, u1 + 0.14, 0, 0.07, z, z + h, mat or PORTLAND)


def sacak_lento(g, c, u0, u1, z, alinlik=False, mat=None):
    """Sıva pencere başlığı: konsollu düz korniş (alinlik: üstünde üçgen alınlık)."""
    m = mat or SIVA_L
    c.kutu(g, u0 - 0.12, u1 + 0.12, 0, 0.07, z, z + 0.12, m)
    c.kutu(g, u0 - 0.2, u1 + 0.2, 0, 0.16, z + 0.12, z + 0.19, m)
    if alinlik:
        c.prizma_uc(g, u0 - 0.2, u1 + 0.2, 0, 0.13, z + 0.19, 0.26, m)


def parmaklik(g, c, u0, u1, o, z0=0.0, h=1.05, aralik=0.2, kaide=True):
    """Siyah dökme demir korkuluk (cepheye paralel): alçak taş kaide, alt ve üst ray, mızrak başlı çubuklar.
    Çubuklar iki yüzlü ince iz levhalarıdır (konturu çizilmez; uzak görünümde atılır)."""
    zb = z0
    if kaide:
        c.kutu(g, u0, u1, o - 0.08, o + 0.08, z0, z0 + 0.14, PORTLAND2, arka=True)
        zb = z0 + 0.14
    zt = z0 + h - 0.14
    c.kutu(g, u0, u1, o - 0.02, o + 0.02, zt, zt + 0.04, DEMIR_S, arka=True)
    c.kutu(g, u0, u1, o - 0.02, o + 0.02, zb + 0.07, zb + 0.11, DEMIR_S, arka=True)
    n = max(2, int(round((u1 - u0) / aralik)))
    g.sid += 1
    for i in range(n):
        uu = u0 + (u1 - u0) * (i + 0.5) / n
        govde = [c.p(uu - 0.014, o, zb), c.p(uu + 0.014, o, zb), c.p(uu + 0.014, o, zt + 0.1), c.p(uu - 0.014, o, zt + 0.1)]
        uc = [c.p(uu - 0.04, o, zt + 0.1), c.p(uu + 0.04, o, zt + 0.1), c.p(uu, o, zt + 0.24)]
        for pts in (govde, uc):
            g.yuz(pts, IZ_DEMIR)
            g.yuz(pts[::-1], IZ_DEMIR)
    g.sid += 1


def parmaklik_yan(g, c, u, o0, o1, z0=0.0, z1=None, h=1.05, n=4):
    """Cepheye dik korkuluk (basamak yanı): eğimli üst ray + dikmeler. z0: o0'daki taban kotu, z1: o1'deki."""
    z1 = z0 if z1 is None else z1
    cubuk(g, c.p(u, o0, z0 + h - 0.12), c.p(u, o1, z1 + h - 0.12), 0.04, DEMIR_S)
    for i in range(n):
        f = i / float(n - 1)
        oo, zz = o0 + (o1 - o0) * f, z0 + (z1 - z0) * f
        cubuk(g, c.p(u, oo, zz), c.p(u, oo, zz + h - 0.02), 0.03 if 0 < i < n - 1 else 0.045, DEMIR_S)


AVLU = 1.15          # bodrum aydınlığının derinliği: cepheden kaldırım korkuluğuna


def avlu(g, c, u0, u1, pencere_u=None, korkuluk=True):
    """Bodrum aydınlığı (light well): koyu çukur tabanı, bodrum penceresinin görünen üstü, kaldırım kenarında korkuluk."""
    yer_levha(g, c, u0, u1, 0.0, AVLU - 0.08, 0.03, IZ_GOLGE)
    if pencere_u:
        a, b = pencere_u
        cokgen(g, c, [(a, 0.04), (b, 0.04), (b, 0.5), (a, 0.5)], CAM2, 0.012)
        c.kutu(g, a - 0.06, b + 0.06, 0, 0.06, 0.5, 0.57, SILME)
        iz(g, c, (a + b) / 2 - 0.014, (a + b) / 2 + 0.014, 0.04, 0.5, IZ_BEYAZ, 0.02)
    if korkuluk:
        parmaklik(g, c, u0, u1, AVLU)


def panelli_kapi(g, c, u0, u1, z0, z1, mat=None, cift=False, cam_ust=False):
    """Panelli ahşap kapı kanadı (parlak renkli): kabartma paneller, pirinç tokmak ve mektup ağzı."""
    m = mat or VR
    c.kutu(g, u0, u1, 0, 0.05, z0, z1, m)
    w, h = u1 - u0, z1 - z0
    sutun = ((0.1, 0.46), (0.54, 0.9))
    for a, b in sutun:
        for p, q in ((0.07, 0.36), (0.42, 0.7), (0.76, 0.93)):
            if cam_ust and p > 0.7:
                cokgen(g, c, [(u0 + w * a, z0 + h * p), (u0 + w * b, z0 + h * p), (u0 + w * b, z0 + h * q), (u0 + w * a, z0 + h * q)], CAM2, 0.056)
            else:
                c.kutu(g, u0 + w * a, u0 + w * b, 0.05, 0.068, z0 + h * p, z0 + h * q, m)
                # kabartmanın ışık alan kenarı (koyu kapıda kontur görünmez: açık renkli ince iz)
                iz(g, c, u0 + w * a, u0 + w * b, z0 + h * q - 0.016, z0 + h * q, IZ_KABARTMA, 0.07)
                iz(g, c, u0 + w * a, u0 + w * a + 0.016, z0 + h * p, z0 + h * q, IZ_KABARTMA, 0.07)
    if cift:
        iz(g, c, (u0 + u1) / 2 - 0.012, (u0 + u1) / 2 + 0.012, z0, z1, IZ_DEMIR, 0.056)
    c.kutu(g, u0 + w * 0.47, u0 + w * 0.53, 0.05, 0.11, z0 + h * 0.5, z0 + h * 0.56, PIRINC)      # tokmak
    iz(g, c, u0 + w * 0.36, u0 + w * 0.64, z0 + h * 0.385, z0 + h * 0.41, IZ_PIRINC, 0.07)         # mektup ağzı


def yelpaze(g, c, um, z, r, mat=CAM):
    """Yarım daire yelpaze ışıklık (fanlight): cam + ışınsal kayıtlar + kemer sövesi."""
    cokgen(g, c, _yay(um, z, r, "yarim", 6), mat, 0.012)
    for a in (math.pi / 4, math.pi / 2, 3 * math.pi / 4):
        p0, p1 = (um + 0.12 * math.cos(a), z + 0.12 * math.sin(a)), (um + r * math.cos(a), z + r * math.sin(a))
        nx, nz = -math.sin(a) * 0.013, math.cos(a) * 0.013
        cokgen(g, c, [(p0[0] + nx, p0[1] + nz), (p0[0] - nx, p0[1] - nz), (p1[0] - nx, p1[1] - nz), (p1[0] + nx, p1[1] + nz)], IZ_BEYAZ, 0.02)
    cokgen(g, c, _yay(um, z, 0.13, "yarim", 4), IZ_BEYAZ, 0.021)
    kemer_halka(g, c, um, z, r, 0.09, SILME, "yarim", 6, 0.06)


def basamaklar(g, c, u0, u1, o1, z, n=3, mat=None):
    """Kapıya çıkan basamaklar: sahanlık (cephede, z kotunda) + n basamak, o1'de kaldırıma iner."""
    do = (o1 - 0.42) / n
    c.kutu(g, u0, u1, 0, 0.42, 0.0, z, mat or PORTLAND)
    for i in range(n):
        c.kutu(g, u0, u1, 0.42 + i * do, 0.42 + (i + 1) * do, 0.0, z * (n - i) / (n + 1.0), PORTLAND2 if i % 2 == 0 else (mat or PORTLAND))


def saksi_k(g, x, y, z=0.0, r=0.16, cicek=0):
    dsil(g, x, y, z, z + 0.3, r * 0.8, 6, SAKSI, r1=r, kapak=(False, True), kapak_mat=AHSAP2)
    meyve(g, x, y, z + 0.3, r * 1.25, YAPRAK, n=6, rz=r * 1.6)
    meyve(g, x, y, z + 0.3 + r * 1.1, r * 0.5, CICEK[cicek % len(CICEK)], n=5)


def asili_sepet(g, x, y, z, cicek=0):
    """Asılı çiçek sepeti (pub): askı + yarım küre sepet + çiçekler."""
    cubuk(g, (x, y, z), (x, y, z + 0.4), 0.02, DEMIR_S)
    dsil(g, x, y, z - 0.22, z, 0.1, 6, AHSAP2, r1=0.24, kapak=(False, False))
    meyve(g, x, y, z - 0.02, 0.27, YAPRAK, n=6, rz=0.26)
    for i in range(3):
        a = i * 2.1 + 0.4
        meyve(g, x + math.cos(a) * 0.17, y + math.sin(a) * 0.17, z + 0.06, 0.09, CICEK[(cicek + i) % len(CICEK)], n=5)


# ---------------------------------------------------------------- eski parçaların Londra hâli (aynı adla)

for _ad in ("kose_dis", "kose_boru", "silme", "kaide", "sacak", "cati_egim", "cati_kose", "cati_semer", "tabela_bant", "duvar_duz", "baca", "cati_penceresi"):
    PARCA_HAFIF.pop(_ad, None)          # uzak görünüm artık yeni (sade) Londra parçalarından türetilir


def pencere(g, c, u0, u1, z0, z1, kemer=False, bolme=1, yatay=True, cam=CAM, t=0.12, perde=1, sove=True):
    """Pastel apartman (boyalı sıva teras) pencereleri de giyotin: eski çağrılar aynı imzayla Londra penceresi çizer."""
    w = u1 - u0
    nx = 3 if w >= 1.0 else 2 if w >= 0.7 else 1
    if kemer:
        sash(g, c, u0, u1, z0, z1, (nx, 2), "yarim", 1 if perde else 0, cam)
    else:
        sash(g, c, u0, u1, z0, z1, (nx, 2 if yatay else 1), "duz", 2 if perde == 1 and w >= 1.0 else 1 if perde else 0, cam)
        if sove:
            c.kutu(g, u0 - 0.13, u1 + 0.13, 0, 0.08, z1 + 0.06, z1 + 0.17, SILME)       # sıva başlık


@parca("duvar", "Tek giyotin pencere")
def duvar_pencere(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.88, 1.92, 0.85, 2.45)


@parca("duvar", "Çift giyotin pencere")
def duvar_cift(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.42, 1.2, 0.85, 2.45, perde=2)
    pencere(g, c, 1.6, 2.38, 0.85, 2.45, perde=2)


@parca("duvar", "Yarım daire başlı pencere")
def duvar_kemerli(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.9, 1.9, 0.85, 2.0, kemer=True)


@parca("duvar", "Saksılı pencere")
def duvar_saksi(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.88, 1.92, 0.95, 2.45)
    cicek_kutusu(g, c, 0.8, 2.0, 0.86)


@parca("duvar", "Giyotin pencere (2/2)")
def duvar_panjur(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.9, 1.9, 0.85, 2.45, (2, 1), perde=1)
    c.kutu(g, 0.77, 2.03, 0, 0.08, 2.51, 2.62, SILME)


@parca("duvar", "Giyotin pencere (storu inik)")
def duvar_panjur_kapali(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.9, 1.9, 0.85, 2.45, (2, 1), perde=0)
    iz(g, c, 0.9, 1.9, 1.5, 2.45, IZ_PERDE[0], 0.016)
    c.kutu(g, 0.77, 2.03, 0, 0.08, 2.51, 2.62, SILME)


@parca("duvar", "Pencere + demir saksılık")
def duvar_stor(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.85, 1.95, 0.95, 2.45)
    c.kutu(g, 0.7, 2.1, 0.2, 0.23, 0.5, 0.8, DEMIR_S)
    for uu in (0.7, 2.07):
        c.kutu(g, uu, uu + 0.03, 0, 0.22, 0.5, 0.8, DEMIR_S)
    for i in range(3):
        p = c.p(0.95 + i * 0.45, 0.11, 0.7)
        meyve(g, p[0], p[1], 0.68, 0.13, YAPRAK, n=5)
        meyve(g, p[0], p[1], 0.8, 0.07, CICEK[i % len(CICEK)], n=5)


@parca("duvar", "Alınlıklı pencere (sıva başlık + üçgen alınlık)")
def duvar_alinlik(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.88, 1.92, 0.8, 2.3, (3, 2), perde=2)
    sacak_lento(g, c, 0.88, 1.92, 2.36, alinlik=True, mat=SILME)


@parca("duvar", "Balkonlu uzun pencere (dökme demir korkuluk)")
def duvar_balkon(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.85, 1.95, 0.3, 2.5, (2, 2), perde=2, denizlik=False)
    c.kutu(g, 0.45, 2.35, 0, 0.5, 0.1, 0.2, SILME2)
    parmaklik(g, c, 0.48, 2.32, 0.46, 0.2, 0.95, 0.17, kaide=False)
    for uu in (0.48, 2.32):
        cubuk(g, c.p(uu, 0.02, 1.03), c.p(uu, 0.46, 1.03), 0.035, DEMIR_S)


@parca("duvar", "İki dar pencere")
def duvar_dar(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.52, 1.08, 0.8, 2.5, (1, 2), perde=1)
    sash(g, c, 1.72, 2.28, 0.8, 2.5, (1, 2), perde=1)


@parca("duvar", "Geniş atölye penceresi (küçük camlı)")
def duvar_genis(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.4, 2.4, 0.9, 2.5, (6, 2), perde=0)


@parca("duvar", "Yuvarlak pencere (öküz gözü)")
def duvar_yuvarlak(g):
    c = on()
    panel(g, c)
    pts = [(1.4 + 0.45 * math.cos(2 * math.pi * i / 10), 1.75 + 0.45 * math.sin(2 * math.pi * i / 10)) for i in range(10)]
    cokgen(g, c, pts, CAM, 0.012)
    for i in range(10):
        a0, a1 = 2 * math.pi * i / 10, 2 * math.pi * (i + 1) / 10
        cokgen(g, c, [(1.4 + 0.45 * math.cos(a0), 1.75 + 0.45 * math.sin(a0)), (1.4 + 0.56 * math.cos(a0), 1.75 + 0.56 * math.sin(a0)),
                      (1.4 + 0.56 * math.cos(a1), 1.75 + 0.56 * math.sin(a1)), (1.4 + 0.45 * math.cos(a1), 1.75 + 0.45 * math.sin(a1))], SILME, 0.05)
    iz(g, c, 1.386, 1.414, 1.3, 2.2, IZ_BEYAZ, 0.02)
    iz(g, c, 0.95, 1.85, 1.736, 1.764, IZ_BEYAZ, 0.02)


@parca("duvar", "Düz duvar (kör pencere nişi)")
def duvar_duz(g):
    c = on()
    panel(g, c)
    cokgen(g, c, [(0.9, 0.85), (1.9, 0.85), (1.9, 2.45), (0.9, 2.45)], D2, 0.012)
    c.kutu(g, 0.82, 1.98, 0, 0.1, 0.72, 0.79, SILME2)


@parca("duvar", "Düz duvar (tuğla izi, sokak adı levhası)")
def duvar_duz_tugla(g):
    c = on()
    panel(g, c)
    tugla_izi(g, c, 31, 5)
    c.kutu(g, 0.5, 1.5, 0, 0.03, 2.2, 2.52, SILME)
    iz(g, c, 0.58, 1.42, 2.3, 2.42, IZ_DEMIR, 0.034)


@parca("duvar", "Düz duvar (iniş borusu, havalandırma)")
def duvar_duz_kutu(g):
    c = on()
    panel(g, c)
    c.kutu(g, 2.2, 2.3, 0, 0.1, 0.0, KAT, DEMIR_S)
    c.kutu(g, 2.16, 2.34, 0, 0.13, 2.3, 2.5, DEMIR_S)
    c.kutu(g, 0.6, 1.0, 0, 0.04, 0.4, 0.7, PORTLAND2)
    tugla_izi(g, c, 37, 3, u1=2.0)


@parca("zemin", "Zemin kat penceresi (giyotin, pencere korkuluklu)")
def zemin_pencere(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.85, 1.95, 0.95, 2.35, (3, 2), perde=1)
    c.kutu(g, 0.72, 2.08, 0, 0.08, 2.41, 2.52, SILME)
    parmaklik(g, c, 0.78, 2.02, 0.16, 0.88, 0.55, 0.16, kaide=False)


@parca("kose", "Dış köşe: sıva köşe taşları (quoin)")
def kose_dis(g):
    z, i = 0.02, 0
    while z < KAT - 0.2:
        w = 0.44 if i % 2 == 0 else 0.3
        kutu(g, -A - 0.035, -A + w, -A - 0.035, -A + (0.74 - w), z, z + 0.4, SILME, atla=())
        z += 0.5
        i += 1


@parca("kose", "Dış köşe: köşe taşları + dökme demir iniş borusu")
def kose_boru(g):
    PARCA["kose_dis"](g)
    x, y = -A + 0.6, -A - 0.07
    kutu(g, x - 0.05, x + 0.05, y - 0.05, y + 0.05, 0.0, KAT, DEMIR_S)
    kutu(g, x - 0.09, x + 0.09, y - 0.07, -A, 2.1, 2.16, DEMIR_S, atla=())


@parca("bant", "Kat silmesi (sıva)")
def silme(g):
    c = on()
    c.kutu(g, -0.04, HUCRE + 0.04, 0, 0.07, -0.16, -0.05, SILME)
    c.kutu(g, -0.07, HUCRE + 0.07, 0, 0.13, -0.05, 0.0, SILME2)


@parca("bant", "Taş kaide (sade)")
def kaide(g):
    c = on()
    c.kutu(g, -0.05, HUCRE + 0.05, 0, 0.06, 0.0, 0.4, PORTLAND2)
    c.kutu(g, -0.07, HUCRE + 0.07, 0, 0.09, 0.4, 0.46, PORTLAND)


@parca("cati", "Saçak (korniş, saçak tahtası, dökme demir oluk)")
def sacak(g):
    c = on()
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.12, -0.22, -0.08, SILME2)
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.26, -0.08, 0.02, SILME)
    c.kutu(g, -0.02, HUCRE + 0.02, 0.2, 0.34, -0.02, 0.09, CT2)                # kalın saçak ucu (arduvazın bittiği yer)
    c.kutu(g, -0.02, HUCRE + 0.02, 0.26, 0.4, -0.1, -0.02, DEMIR_S)            # oluk


@parca("cati", "Saçak köşesi")
def sacak_kose(g):
    kutu(g, -A - 0.34, -A, -A - 0.34, -A, -0.02, 0.09, CT2, atla=())
    kutu(g, -A - 0.26, -A, -A - 0.26, -A, -0.22, -0.02, SILME, atla=())


def _sira_izi(g, yuzler, n=2):
    """Çatı yüzünde birkaç çok ince yatay sıra izi (kontursuz). yuzler: (eşleyici, satırın x başlangıcı, ters mi)."""
    for es, x0, ters in yuzler:
        for i in range(n):
            v = A * (i + 0.8) / (n + 0.6)
            e = 0.02
            pts = [es(x0(v), v, v / A + e), es(A, v, v / A + e), es(A, v + 0.026, (v + 0.026) / A + e), es(x0(v + 0.026), v + 0.026, (v + 0.026) / A + e)]
            levha(g, pts[::-1] if ters else pts, IZ_C)


@parca("cati", "Çatı karosu: eğim (düz renk arduvaz, ince sıra izleri)")
def cati_egim(g):
    _cati_yuzu(g, 0, 0, 1, 1, 0)
    _sira_izi(g, [(lambda u, v, z: (u, v, z), lambda v: 0.0, False)])


@parca("cati", "Çatı karosu: dış köşe (kırma, mahya şeritli)")
def cati_kose(g):
    _cati_yuzu(g, 0, 0, 1, 0, 0)
    cubuk(g, (0, 0, 0.03), (A, A, 1.03), 0.16, CT2, 0.1)
    _sira_izi(g, [(lambda u, v, z: (u, v, z), lambda v: v + 0.1, False), (lambda u, v, z: (v, u, z), lambda v: v + 0.1, True)], 1)


@parca("cati", "Çatı karosu: semer (mahya şeritli)")
def cati_semer(g):
    _cati_yuzu(g, 1, 0, 1, 0, 1)
    cubuk(g, (0, 0, 1.03), (A, A, 1.03), 0.16, CT2, 0.1)


@parca("ayrinti", "Baca (tuğla gövde, taş başlık, seramik külahlar)")
def baca(g):
    kutu(g, -0.36, 0.36, -0.26, 0.26, -1.2, 1.5, D)
    kutu(g, -0.4, 0.4, -0.3, 0.3, 1.26, 1.36, D2, atla=())
    kutu(g, -0.42, 0.42, -0.32, 0.32, 1.5, 1.6, PORTLAND, atla=())
    for i, sx in enumerate((-0.2, 0.2)):
        dsil(g, sx, 0, 1.6, 1.98, 0.11, 6, KULAH if i == 0 else KULAH2, r1=0.085, kapak=(False, True), kapak_mat=DEMIR_S)


@parca("ayrinti", "Sıra baca (ortak duvarda: uzun tuğla kütle, sıra sıra külahlar)")
def baca_sira(g):
    kutu(g, -0.27, 0.27, -0.95, 0.95, -0.4, 2.55, D)
    kutu(g, -0.31, 0.31, -0.99, 0.99, 2.25, 2.35, D2, atla=())
    kutu(g, -0.33, 0.33, -1.01, 1.01, 2.55, 2.66, PORTLAND, atla=())
    for i in range(5):
        y = -0.72 + i * 0.36
        dsil(g, 0, y, 2.66, 3.02 + (0.08 if i % 2 else 0.0), 0.12, 6, KULAH if i % 3 else KULAH2, r1=0.09, kapak=(False, True), kapak_mat=DEMIR_S)


def _baca_sira_hafif(g):
    kutu(g, -0.27, 0.27, -0.95, 0.95, -0.4, 2.6, D)
    kutu(g, -0.12, 0.12, -0.8, 0.8, 2.6, 2.98, KULAH, atla=())


PARCA_HAFIF["baca_sira"] = _baca_sira_hafif


@parca("ayrinti", "Çatı penceresi (kurşun kaplı, giyotin)")
def cati_penceresi(g):
    kutu(g, -0.55, 0.55, 0.2, 1.6, 0.0, 1.22, KURSUN, atla=("zn", "yp"))
    kutu(g, -0.63, 0.63, 0.12, 1.6, 1.22, 1.3, KURSUN, atla=())
    c = C((-0.55, 0.2), (1, 0), (0, -1), 1.1)
    sash(g, c, 0.2, 0.9, 0.28, 1.08, (2, 1), perde=1, t=0.05, d=0.04, denizlik=False)
    kutu(g, -0.6, 0.6, 0.1, 0.2, 0.12, 0.2, SILME, atla=())


@parca("ayrinti", "Mansart penceresi (parapetin arkasında, kurşun kaplı)")
def mansart_penceresi(g):
    # kök: hücrenin ön kenarının ortası, çatı tabanı; parapetin arkasında mansart eğimine oturur
    kutu(g, -0.5, 0.5, 0.42, 1.7, 0.3, 1.5, KURSUN, atla=("zn", "yp"))
    kutu(g, -0.57, 0.57, 0.35, 1.7, 1.5, 1.57, KURSUN, atla=())
    c = C((-0.5, 0.42), (1, 0), (0, -1), 1.0)
    sash(g, c, 0.18, 0.82, 0.62, 1.36, (2, 1), perde=1, t=0.05, d=0.04, denizlik=False)


@parca("cati", "Parapet (korniş + alçak duvar + taş harpuşta)")
def parapet(g):
    c = on()
    c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.1, -0.26, -0.12, PORTLAND2)
    c.kutu(g, -0.05, HUCRE + 0.05, 0, 0.22, -0.12, -0.02, PORTLAND)             # korniş
    c.kutu(g, 0, HUCRE, -DK, 0, -0.02, 0.78, D, arka=True)
    c.kutu(g, -0.03, HUCRE + 0.03, -DK - 0.04, 0.05, 0.78, 0.87, PORTLAND, arka=True)   # harpuşta


@parca("cati", "Parapet köşesi")
def parapet_kose(g):
    kutu(g, -A - 0.22, -A, -A - 0.22, -A, -0.12, -0.02, PORTLAND, atla=())
    kutu(g, -A - 0.1, -A, -A - 0.1, -A, -0.26, -0.12, PORTLAND2, atla=())
    kutu(g, -A - 0.05, -A + DK + 0.04, -A - 0.05, -A + DK + 0.04, 0.78, 0.87, PORTLAND, atla=())


@parca("cati", "Korkuluk duvarı (düz çatı parapeti, taş harpuştalı)")
def korkuluk(g):
    c = on()
    c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.16, -0.12, -0.02, PORTLAND)
    c.kutu(g, 0, HUCRE, -DK, 0, -0.02, 0.7, D, arka=True)
    c.kutu(g, -0.03, HUCRE + 0.03, -DK - 0.04, 0.05, 0.7, 0.79, PORTLAND, arka=True)


@parca("ayrinti", "Baca (düz çatıda: kısa tuğla baca, tek külah)")
def baca_ince(g):
    # kök çatıdan 0,9 m yukarıda (kural öyle koyar): gövde çatıya iner
    kutu(g, -0.24, 0.24, -0.24, 0.24, -1.0, 0.1, D)
    kutu(g, -0.29, 0.29, -0.29, 0.29, 0.1, 0.2, PORTLAND, atla=())
    dsil(g, 0, 0, 0.2, 0.56, 0.11, 6, KULAH, r1=0.085, kapak=(False, True), kapak_mat=DEMIR_S)


@parca("cati", "Düz çatı (kurşun / asfalt kaplama)")
def duz_cati(g):
    kutu(g, -A, A, -A, A, 0.0, 0.08, PM("Teras", "#b9bfc4"))


@parca("cati", "Demir korkuluk (mızrak başlı)")
def korkuluk_demir(g):
    parmaklik(g, on(), 0, HUCRE, -0.08, 0.0, 1.05, 0.2, kaide=False)


@parca("cati", "Kule tepesi: arduvaz külah")
def kule_sivri(g):
    e = A + 0.2
    kutu(g, -e, e, -e, e, -0.14, 0.04, PORTLAND, atla=())
    kati(g, [(-e, -e, 0.04), (e, -e, 0.04), (e, e, 0.04), (-e, e, 0.04), (0, 0, 4.6)], [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], CT)
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        cubuk(g, (sx * e, sy * e, 0.08), (0, 0, 4.64), 0.1, CT2, 0.08)
    cubuk(g, (0, 0, 4.5), (0, 0, 5.3), 0.05, DEMIR_S)
    meyve(g, 0, 0, 4.95, 0.11, PIRINC)


@parca("cati", "Kule tepesi: kurşun kubbe (fenerli)")
def kule_kubbe(g):
    e = A + 0.15
    kutu(g, -e, e, -e, e, -0.14, 0.04, PORTLAND, atla=())
    profil = [(1.3, 0.04), (1.25, 0.7), (1.0, 1.4), (0.6, 1.9), (0.3, 2.1)]
    for (r0, z0), (r1, z1) in zip(profil, profil[1:]):
        dsil(g, 0, 0, z0, z1, r0, 8, CT, r1=r1, kapak=(False, False))
    dsil(g, 0, 0, 2.1, 2.7, 0.26, 8, PORTLAND, kapak=(False, True))
    dsil(g, 0, 0, 2.7, 3.1, 0.34, 8, CT, r1=0.03, kapak=(True, False))
    cubuk(g, (0, 0, 3.05), (0, 0, 3.6), 0.04, DEMIR_S)
    meyve(g, 0, 0, 3.45, 0.1, PIRINC)


# ---------------------------------------------------------------- dükkân cephesi (Victoria tarzı shopfront)

def _dukkan_pilastr(g, c, z1=2.3):
    """Hücrenin iki ucunda yarım pilastr (komşu hücreninkiyle birleşir): kaide + gövde."""
    for a, b in ((0.0, 0.12), (HUCRE - 0.12, HUCRE)):
        c.kutu(g, a, b, 0, 0.13, 0.0, z1, VR)
        c.kutu(g, a, b, 0, 0.16, 0.0, 0.5, VR)


def _kaide_panel(g, c, u0, u1, z1, n=2):
    """Vitrin altındaki alçak kaide (stall riser): boyalı ahşap, kabartma panolu."""
    c.kutu(g, u0, u1, 0, 0.1, 0.0, z1, VR)
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n + 0.08, u0 + (u1 - u0) * (i + 1) / n - 0.08
        cokgen(g, c, [(a, 0.1), (b, 0.1), (b, z1 - 0.1), (a, z1 - 0.1)], VR, 0.115)
    c.kutu(g, u0 - 0.02, u1 + 0.02, 0, 0.16, z1, z1 + 0.05, SILME2)             # vitrin eşiği


def _vitrin_ici(g, c, u0, u1, z, tohum, n=5):
    """Vitrindeki ürün dizisi: birkaç renkli iz (kontursuz)."""
    rnd = random.Random(tohum)
    renk = ("#d94f3d", "#e8a23a", "#f2d04a", "#5a9a4a", "#2f7d8c", "#8a4a6b", "#f4f0e6", "#7ab8d6")
    for i in range(n):
        a = u0 + (u1 - u0) * (i + 0.15) / n
        b = u0 + (u1 - u0) * (i + 0.8) / n
        iz(g, c, a, b, z, z + rnd.uniform(0.16, 0.36), PM("Iz", rnd.choice(renk)), 0.02)


@parca("zemin", "Dükkân vitrini (ahşap çerçeve, bölmeli cam, kaide panel)")
def vitrin(g):
    c = on()
    panel(g, c)
    _dukkan_pilastr(g, c)
    _kaide_panel(g, c, 0.12, HUCRE - 0.12, 0.55)
    cokgen(g, c, [(0.12, 0.6), (HUCRE - 0.12, 0.6), (HUCRE - 0.12, 2.2), (0.12, 2.2)], VIT, 0.02)
    c.kutu(g, 0.12, HUCRE - 0.12, 0, 0.1, 2.2, 2.3, VR)
    c.kutu(g, 0.12, HUCRE - 0.12, 0.02, 0.07, 1.76, 1.81, VR)                     # üst ışıklık kaydı (transom)
    for uu in (0.97, 1.83):
        c.kutu(g, uu - 0.025, uu + 0.025, 0.02, 0.07, 0.6, 2.2, VR)
    for uu in (0.4, 0.69, 1.26, 1.54, 2.11, 2.4):                                  # üst ışıklıkların küçük camları
        iz(g, c, uu - 0.012, uu + 0.012, 1.81, 2.2, IZ_BEYAZ, 0.03)
    _vitrin_ici(g, c, 0.2, HUCRE - 0.2, 0.62, 21, 6)


@parca("zemin", "Geniş vitrin (tek cam, alçak kaide)")
def vitrin_genis(g):
    c = on()
    panel(g, c)
    _dukkan_pilastr(g, c)
    _kaide_panel(g, c, 0.12, HUCRE - 0.12, 0.38, 3)
    cokgen(g, c, [(0.12, 0.43), (HUCRE - 0.12, 0.43), (HUCRE - 0.12, 2.2), (0.12, 2.2)], VIT, 0.02)
    c.kutu(g, 0.12, HUCRE - 0.12, 0, 0.1, 2.2, 2.3, VR)
    c.kutu(g, 0.12, HUCRE - 0.12, 0.02, 0.07, 1.86, 1.91, VR)
    c.kutu(g, 1.375, 1.425, 0.02, 0.07, 0.43, 2.2, VR)
    iz(g, c, 0.45, 1.05, 1.1, 1.62, PM("Iz", "#f5c84a"), 0.024)                    # kampanya kâğıdı
    _vitrin_ici(g, c, 0.2, HUCRE - 0.2, 0.45, 22, 6)


@parca("zemin", "Dükkân kapısı (girintili, camlı, üst ışıklıklı, karo eşikli)")
def kapi_vitrin(g):
    c = on()
    panel(g, c)
    _dukkan_pilastr(g, c)
    _kaide_panel(g, c, 0.12, 0.88, 0.55, 1)
    _kaide_panel(g, c, 1.92, HUCRE - 0.12, 0.55, 1)
    cokgen(g, c, [(0.12, 0.6), (0.88, 0.6), (0.88, 2.2), (0.12, 2.2)], VIT, 0.02)
    cokgen(g, c, [(1.92, 0.6), (HUCRE - 0.12, 0.6), (HUCRE - 0.12, 2.2), (1.92, 2.2)], VIT, 0.02)
    c.kutu(g, 0.12, HUCRE - 0.12, 0, 0.1, 2.2, 2.3, VR)
    for uu in (0.88, 1.86):
        c.kutu(g, uu, uu + 0.06, 0, 0.1, 0.0, 2.2, VR)
    # kapı kanadı: alt panel + büyük cam + üst ışıklık
    cokgen(g, c, [(0.94, 0.08), (1.86, 0.08), (1.86, 2.2), (0.94, 2.2)], VIT, 0.014)
    c.kutu(g, 0.94, 1.86, 0.014, 0.05, 0.08, 0.62, VR)
    c.kutu(g, 0.94, 1.86, 0.014, 0.05, 1.84, 1.9, VR)
    for uu in (0.94, 1.78):
        c.kutu(g, uu, uu + 0.08, 0.014, 0.05, 0.62, 1.84, VR)
    cokgen(g, c, [(1.04, 0.16), (1.76, 0.16), (1.76, 0.54), (1.04, 0.54)], VR, 0.062)
    c.kutu(g, 1.68, 1.74, 0.05, 0.11, 1.05, 1.3, PIRINC)
    iz(g, c, 1.2, 1.6, 1.36, 1.52, PM("Iz", "#f4f0e6"), 0.03)                      # "OPEN" kartı
    c.kutu(g, 0.9, 1.9, 0, 0.3, 0.0, 0.08, PORTLAND)                               # eşik
    for i in range(4):                                                             # karo eşik (dama)
        for j in range(2):
            a, b = 0.94 + i * 0.23, 0.94 + (i + 1) * 0.23
            yer_levha(g, c, a, b, 0.3 + j * 0.22, 0.52 + j * 0.22, 0.02, IZ_KARO[(i + j) % 2])
    _vitrin_ici(g, c, 0.18, 0.84, 0.62, 23, 2)
    _vitrin_ici(g, c, 1.96, HUCRE - 0.18, 0.62, 24, 2)


@parca("zemin", "Tabela panosu (fascia: korniş, iki uçta konsol)")
def tabela_bant(g):
    c = on()
    c.kutu(g, 0.0, HUCRE, 0, 0.13, 2.3, 2.78, VR)
    c.kutu(g, 0.0, HUCRE, 0, 0.2, 2.78, 2.86, SILME)                              # korniş
    c.kutu(g, 0.0, HUCRE, 0, 0.16, 2.26, 2.3, SILME2)
    for a, b in ((0.0, 0.11), (HUCRE - 0.11, HUCRE)):                              # konsollar
        c.kutu(g, a, b, 0, 0.19, 2.3, 2.9, VR)
        c.kutu(g, a, b, 0, 0.24, 2.62, 2.92, SILME)


def _tabela_bant_hafif(g):
    c = on()
    c.kutu(g, 0.0, HUCRE, 0, 0.14, 2.28, 2.86, VR)


PARCA_HAFIF["tabela_bant"] = _tabela_bant_hafif


@parca("zemin", "Tente (düz renk, katlanır kollu)")
def tente(g):
    c = on()
    pts = [c.p(0.06, 0.12, 2.26), c.p(HUCRE - 0.06, 0.12, 2.26), c.p(HUCRE - 0.06, 1.05, 1.92), c.p(0.06, 1.05, 1.92)]
    q = [(p[0], p[1], p[2] - 0.035) for p in pts]
    kati(g, pts + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], TN)
    c.kutu(g, 0.06, HUCRE - 0.06, 1.03, 1.05, 1.74, 1.92, TN)                      # düz etek
    for uu in (0.1, HUCRE - 0.1):                                                  # katlanır kollar
        cubuk(g, c.p(uu, 0.1, 1.62), c.p(uu, 1.0, 1.9), 0.028, DEMIR_S)
    c.kutu(g, 0.04, HUCRE - 0.04, 0.02, 0.13, 2.17, 2.27, AHSAP2)                  # tente kutusu (tabelanın altında)


@parca("zemin", "Ahşap kapı (panelli, dikdörtgen üst ışıklık, basamak)")
def kapi_ahsap(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.78, 0.9, 0, 0.09, 0.0, 2.62, SILME)
    c.kutu(g, 1.9, 2.02, 0, 0.09, 0.0, 2.62, SILME)
    c.kutu(g, 0.72, 2.08, 0, 0.14, 2.62, 2.74, SILME)
    panelli_kapi(g, c, 0.9, 1.9, 0.12, 2.12)
    cokgen(g, c, [(0.9, 2.18), (1.9, 2.18), (1.9, 2.62), (0.9, 2.62)], CAM, 0.012)
    c.kutu(g, 0.9, 1.9, 0, 0.06, 2.12, 2.18, SILME)
    for uu in (1.23, 1.57):
        iz(g, c, uu - 0.013, uu + 0.013, 2.18, 2.62, IZ_BEYAZ, 0.02)
    c.kutu(g, 0.6, 2.2, 0, 0.5, 0.0, 0.12, PORTLAND)
    p = c.p(2.36, 0.16, 1.9)                                                       # duvar feneri
    kutu(g, p[0] - 0.07, p[0] + 0.07, p[1] - 0.07, p[1] + 0.07, 1.9, 2.12, ISIK, atla=())
    kutu(g, p[0] - 0.1, p[0] + 0.1, p[1] - 0.1, p[1] + 0.1, 2.12, 2.16, DEMIR_S, atla=())
    c.kutu(g, 2.34, 2.38, 0, 0.16, 2.16, 2.2, DEMIR_S)


@parca("zemin", "Garaj / depo kapısı (çift kanat ahşap, demir menteşeli)")
def kapi_garaj(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.28, 0.4, 0, 0.08, 0.0, 2.5, PORTLAND)
    c.kutu(g, 2.4, 2.52, 0, 0.08, 0.0, 2.5, PORTLAND)
    c.kutu(g, 0.22, 2.58, 0, 0.1, 2.5, 2.66, PORTLAND)
    c.kutu(g, 0.4, 2.4, 0, 0.05, 0.03, 2.5, VR)
    iz(g, c, 1.388, 1.412, 0.03, 2.5, IZ_DEMIR, 0.056)
    for a, b in ((0.5, 1.3), (1.5, 2.3)):
        cokgen(g, c, [(a, 0.15), (b, 0.15), (b, 1.5), (a, 1.5)], VR, 0.066)
        cokgen(g, c, [(a, 1.65), (b, 1.65), (b, 2.35), (a, 2.35)], CAM2, 0.056)
        iz(g, c, (a + b) / 2 - 0.012, (a + b) / 2 + 0.012, 1.65, 2.35, IZ_BEYAZ, 0.06)
        iz(g, c, a, b, 1.99, 2.012, IZ_BEYAZ, 0.06)


@parca("konut", "Cumba (çokgen bay window, giyotin camlı)")
def bay_window(g):
    _cumba(g, on(), alt=True, ust_kapak=True)


@parca("konut", "Merdivenli kapı (yelpaze ışıklıklı, korkuluklu)")
def kapi_merdivenli(g):
    PARCA["g_kapi"](g)


# ---------------------------------------------------------------- pub

@parca("zemin", "Pub cephesi (cilalı koyu ahşap, buzlu cam, asılı çiçek sepeti)")
def pub_cephe(g):
    c = on()
    panel(g, c)
    _dukkan_pilastr(g, c)
    for a, b in ((0.0, 0.12), (HUCRE - 0.12, HUCRE)):                              # altın başlıklar
        c.kutu(g, a, b, 0, 0.17, 2.12, 2.24, PIRINC)
    c.kutu(g, 0.12, HUCRE - 0.12, 0, 0.1, 0.0, 1.0, VR)                            # yüksek ahşap kaide
    for i in range(3):
        a, b = 0.22 + i * 0.82, 0.22 + i * 0.82 + 0.7
        cokgen(g, c, [(a, 0.14), (b, 0.14), (b, 0.86), (a, 0.86)], VR, 0.115)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.15, 1.0, 1.05, VR)
    cokgen(g, c, [(0.12, 1.05), (HUCRE - 0.12, 1.05), (HUCRE - 0.12, 2.2), (0.12, 2.2)], CAM_BUZLU, 0.02)
    iz(g, c, 0.3, HUCRE - 0.3, 1.2, 1.7, PM("Iz", "#c5d6d4"), 0.024)               # buzlu camın oymalı bandı
    c.kutu(g, 0.12, HUCRE - 0.12, 0, 0.1, 2.2, 2.3, VR)
    c.kutu(g, 0.12, HUCRE - 0.12, 0.02, 0.07, 1.82, 1.87, VR)
    for uu in (0.97, 1.83):
        c.kutu(g, uu - 0.03, uu + 0.03, 0.02, 0.08, 1.05, 2.2, VR)
    p = c.p(1.4, 0.55, 0)
    cubuk(g, c.p(1.4, 0.13, 2.25), c.p(1.4, 0.55, 2.25), 0.03, DEMIR_S)
    asili_sepet(g, p[0], p[1], 1.85, 0)


@parca("zemin", "Pub kapısı (çift kanat, pirinç, fener, asılı resimli tabela)")
def pub_kapi(g):
    c = on()
    panel(g, c)
    _dukkan_pilastr(g, c)
    for a, b in ((0.0, 0.12), (HUCRE - 0.12, HUCRE)):
        c.kutu(g, a, b, 0, 0.17, 2.12, 2.24, PIRINC)
    c.kutu(g, 0.12, 0.8, 0, 0.1, 0.0, 2.3, VR)
    c.kutu(g, 2.0, HUCRE - 0.12, 0, 0.1, 0.0, 2.3, VR)
    cokgen(g, c, [(0.22, 0.14), (0.7, 0.14), (0.7, 0.86), (0.22, 0.86)], VR, 0.115)
    cokgen(g, c, [(2.1, 0.14), (2.58, 0.14), (2.58, 0.86), (2.1, 0.86)], VR, 0.115)
    cokgen(g, c, [(0.24, 1.1), (0.68, 1.1), (0.68, 2.1), (0.24, 2.1)], CAM_BUZLU, 0.105)
    cokgen(g, c, [(2.12, 1.1), (2.56, 1.1), (2.56, 2.1), (2.12, 2.1)], CAM_BUZLU, 0.105)
    c.kutu(g, 0.8, 2.0, 0, 0.1, 2.08, 2.3, VR)
    panelli_kapi(g, c, 0.86, 1.94, 0.1, 2.08, cift=True, cam_ust=True)
    c.kutu(g, 0.7, 2.1, 0, 0.42, 0.0, 0.1, PORTLAND)
    # asılı resimli tabela: hücrenin sol ucunda, cepheye dik
    cubuk(g, c.p(0.14, 0.14, 2.74), c.p(0.14, 1.12, 2.74), 0.045, DEMIR_S)
    cubuk(g, c.p(0.14, 0.14, 2.42), c.p(0.14, 0.62, 2.72), 0.03, DEMIR_S)
    c.kutu(g, 0.11, 0.17, 0.4, 1.06, 1.92, 2.66, VR, arka=True)
    for s, uu in ((-1, 0.105), (1, 0.175)):
        cy = C(c.p(uu, 0.4, 0)[:2], (0, -1), (s, 0), 0.66)
        pts = [(0.07, 1.99), (0.59, 1.99), (0.59, 2.59), (0.07, 2.59)]
        levha(g, [cy.p(u, 0, z) for u, z in (pts if s < 0 else pts[::-1])], PM("Iz", "#efe2c2"))
        daire = [(0.33 + 0.17 * math.cos(2 * math.pi * i / 8), 2.33 + 0.17 * math.sin(2 * math.pi * i / 8)) for i in range(8)]
        levha(g, [cy.p(u, 0.004, z) for u, z in (daire if s < 0 else daire[::-1])], IZ_PIRINC)
    for zz in (2.66, 2.7):
        cubuk(g, c.p(0.14, 0.5, zz), c.p(0.14, 0.5, zz + 0.04), 0.02, DEMIR_S)
    p = c.p(HUCRE - 0.2, 0.3, 0)                                                    # fener
    cubuk(g, c.p(HUCRE - 0.2, 0.13, 2.2), c.p(HUCRE - 0.2, 0.3, 2.2), 0.03, DEMIR_S)
    kutu(g, p[0] - 0.08, p[0] + 0.08, p[1] - 0.08, p[1] + 0.08, 1.9, 2.14, ISIK, atla=())
    kutu(g, p[0] - 0.11, p[0] + 0.11, p[1] - 0.11, p[1] + 0.11, 2.14, 2.19, DEMIR_S, atla=())


# ---------------------------------------------------------------- Georgian / Regency sıra ev (g_)
# Zemin kat beyaz sıva + rustika; üst katlar tuğla (bloğun duvar rengi), üst katlara doğru küçülen 6/6 giyotin pencereler;
# önde bodrum aydınlığı ve mızrak başlı demir korkuluk; kapı parlak renkli, yelpaze ışıklıklı, basamaklı.

@parca("konut", "Georgian zemin (ön): sıva + rustika, 6/6 pencere, bodrum aydınlığı, demir korkuluk")
def g_zemin(g):
    c = on()
    panel(g, c, SIVA_L)
    rustika(g, c, yasak=(0.82, 1.98, 1.05, 2.7))
    sash(g, c, 0.88, 1.92, 1.15, 2.55, (3, 2), perde=1)
    cokgen(g, c, [(0.8, 2.61), (2.0, 2.61), (2.12, 2.84), (0.68, 2.84)], SIVA_L2, 0.02)      # sıva kemer taşları
    avlu(g, c, 0.0, HUCRE, (1.0, 1.8))


@parca("konut", "Georgian zemin (yan / arka): sıva + rustika, 6/6 pencere")
def g_zemin_yan(g):
    c = on()
    panel(g, c, SIVA_L)
    rustika(g, c, yasak=(0.82, 1.98, 0.9, 2.6))
    sash(g, c, 0.88, 1.92, 1.0, 2.45, (3, 2), perde=1)
    c.kutu(g, -0.04, HUCRE + 0.04, 0, 0.07, 0.0, 0.36, SIVA_L2)


@parca("konut", "Georgian kapı: parlak renkli panelli kapı, yelpaze ışıklık, pilastrlar, basamak, korkuluk, bodrum aydınlığı")
def g_kapi(g):
    c = on()
    panel(g, c, SIVA_L)
    rustika(g, c, yasak=(0.66, 2.14, 0.0, 3.0))
    zk = 0.45
    panelli_kapi(g, c, 0.9, 1.9, zk, 2.4)
    yelpaze(g, c, 1.4, 2.4, 0.5)
    for a, b in ((0.76, 0.9), (1.9, 2.04)):                                          # pilastrlar
        c.kutu(g, a, b, 0, 0.09, zk, 2.4, SILME)
        c.kutu(g, a - 0.03, b + 0.03, 0, 0.12, 2.32, 2.42, SILME2)
    basamaklar(g, c, 0.78, 2.02, AVLU, zk)
    for uu in (0.74, 2.06):
        parmaklik_yan(g, c, uu, 0.06, AVLU, zk, 0.0)
    avlu(g, c, 0.0, 0.7)
    avlu(g, c, 2.1, HUCRE)
    p = c.p(2.36, 0.12, 0)                                                            # fener
    kutu(g, p[0] - 0.06, p[0] + 0.06, p[1] - 0.06, p[1] + 0.06, 2.0, 2.2, ISIK, atla=())
    kutu(g, p[0] - 0.085, p[0] + 0.085, p[1] - 0.085, p[1] + 0.085, 2.2, 2.24, DEMIR_S, atla=())


def _g_ust(g, z0, z1, bolme, tohum, korkuluk=False):
    c = on()
    panel(g, c)
    sash(g, c, 0.88, 1.92, z0, z1, bolme, perde=1)
    duz_kemer(g, c, 0.88, 1.92, z1 + 0.06)
    tugla_izi(g, c, tohum, 4, yasak=(0.88, 1.92, z0, z1))
    if korkuluk:
        parmaklik(g, c, 0.8, 2.0, 0.17, z0 - 0.13, 0.62, 0.15, kaide=False)


ekle("g_kat1", "konut", "Georgian birinci kat: uzun 6/6 pencere, düz tuğla kemer", lambda g: _g_ust(g, 0.55, 2.5, (3, 2), 11))
ekle("g_kat1_korkuluk", "konut", "Georgian birinci kat: uzun pencere + dökme demir pencere korkuluğu", lambda g: _g_ust(g, 0.55, 2.5, (3, 2), 12, True))
ekle("g_ust", "konut", "Georgian üst kat: 6/6 pencere", lambda g: _g_ust(g, 0.8, 2.35, (3, 2), 13))
ekle("g_tavan", "konut", "Georgian en üst kat: küçük 3/3 pencere", lambda g: _g_ust(g, 1.0, 2.1, (3, 1), 14))


@parca("konut", "Georgian düz duvar: tuğla, kör pencere")
def g_duz(g):
    c = on()
    panel(g, c)
    cokgen(g, c, [(0.88, 0.8), (1.92, 0.8), (1.92, 2.35), (0.88, 2.35)], D2, 0.012)
    duz_kemer(g, c, 0.88, 1.92, 2.35)
    c.kutu(g, 0.8, 2.0, 0, 0.1, 0.72, 0.79, PORTLAND)
    tugla_izi(g, c, 15, 4, yasak=(0.88, 1.92, 0.8, 2.35))


@parca("bant", "Georgian zemin kat silmesi (sıva bant)")
def g_silme(g):
    c = on()
    c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.06, -0.14, -0.02, SIVA_L)
    c.kutu(g, -0.05, HUCRE + 0.05, 0, 0.11, -0.02, 0.06, SIVA_L2)


@parca("kose", "Georgian zemin köşesi (sıva köşe taşları)")
def g_kose(g):
    z, i = 0.02, 0
    while z < KAT - 0.3:
        w = 0.44 if i % 2 == 0 else 0.3
        kutu(g, -A - 0.03, -A + w, -A - 0.03, -A + (0.74 - w), z, z + 0.38, SIVA_L2, atla=())
        z += 0.44
        i += 1


# ---------------------------------------------------------------- beyaz sıva teras (Belgravia) (s_)
# Tamamı krem / beyaz sıva (bloğun duvar rengi), sütunlu portiko, birinci kat boyunca sürekli demir balkon.

def _s_zemin(g, on_yuz):
    c = on()
    panel(g, c)
    z0 = 1.1 if on_yuz else 0.95
    rustika(g, c, yasak=(0.8, 2.0, z0 - 0.1, z0 + 1.6), mat=IZ_D)
    sash(g, c, 0.9, 1.9, z0, z0 + 0.95, (3, 2), "yarim", perde=1)
    if on_yuz:
        avlu(g, c, 0.0, HUCRE, (1.0, 1.8))
    else:
        c.kutu(g, -0.04, HUCRE + 0.04, 0, 0.07, 0.0, 0.36, D2)


ekle("s_zemin", "konut", "Sıva teras zemin (ön): rustika, kemerli pencere, bodrum aydınlığı, korkuluk", lambda g: _s_zemin(g, True))
ekle("s_zemin_yan", "konut", "Sıva teras zemin (yan / arka)", lambda g: _s_zemin(g, False))


@parca("konut", "Sıva teras kapısı: sütunlu portiko, basamak, siyah kapı, korkuluk")
def s_kapi(g):
    c = on()
    panel(g, c)
    zk = 0.45
    panelli_kapi(g, c, 0.92, 1.88, zk, 2.2, mat=VR)
    cokgen(g, c, [(0.92, 2.26), (1.88, 2.26), (1.88, 2.5), (0.92, 2.5)], CAM, 0.012)
    c.kutu(g, 0.8, 0.92, 0, 0.08, zk, 2.56, SILME)
    c.kutu(g, 1.88, 2.0, 0, 0.08, zk, 2.56, SILME)
    c.kutu(g, 0.92, 1.88, 0, 0.06, 2.2, 2.26, SILME)
    basamaklar(g, c, 0.62, 2.18, AVLU + 0.25, zk)
    for uu in (0.5, 2.3):                                                             # Dor sütunlar
        p = c.p(uu, 1.02, 0)
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 0.0, zk + 0.12, D)
        dsil(g, p[0], p[1], zk + 0.12, 2.42, 0.15, 8, D, r1=0.13, kapak=(False, False))
        kutu(g, p[0] - 0.19, p[0] + 0.19, p[1] - 0.19, p[1] + 0.19, 2.42, 2.52, D, atla=())
    c.kutu(g, 0.26, 2.54, 0, 1.24, 2.52, 2.8, D)                                      # saçaklık
    c.kutu(g, 0.2, 2.6, 0, 1.3, 2.8, 2.9, SILME2)
    avlu(g, c, 0.0, 0.28)
    avlu(g, c, 2.52, HUCRE)


def _s_ust(g, z0, z1, bolme, alinlik, balkon=False, tohum=0):
    c = on()
    panel(g, c)
    sash(g, c, 0.88, 1.92, z0, z1, bolme, perde=2 if z1 - z0 > 1.6 else 1, denizlik=not balkon)
    for a, b in ((0.74, 0.82), (1.98, 2.06)):                                         # sıva söve
        c.kutu(g, a, b, 0, 0.06, z0 - 0.06, z1 + 0.06, SILME)
    sacak_lento(g, c, 0.88, 1.92, z1 + 0.06, alinlik, SILME)
    if balkon:
        c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.62, 0.02, 0.14, SILME2)                  # sürekli balkon tablası
        parmaklik(g, c, 0.0, HUCRE, 0.57, 0.14, 1.0, 0.18, kaide=False)
        for uu in (0.5, 2.3):                                                         # konsollar
            prizma(g, [c.p(uu, 0, 0.02), c.p(uu, 0.5, 0.02), c.p(uu, 0, -0.3)], (c.u[0] * 0.12, c.u[1] * 0.12, 0), SILME2)


ekle("s_kat1", "konut", "Sıva teras birinci kat (ön): uzun pencere, alınlıklı başlık, sürekli demir balkon", lambda g: _s_ust(g, 0.2, 2.2, (2, 2), True, True))
ekle("s_kat1_yan", "konut", "Sıva teras birinci kat (yan / arka): alınlıklı pencere", lambda g: _s_ust(g, 0.7, 2.2, (3, 2), True))
ekle("s_ust", "konut", "Sıva teras üst kat: kornişli pencere", lambda g: _s_ust(g, 0.8, 2.3, (3, 2), False))
ekle("s_tavan", "konut", "Sıva teras en üst kat: küçük pencere", lambda g: _s_ust(g, 1.0, 2.05, (3, 1), False))


@parca("konut", "Sıva düz duvar (kör niş)")
def s_duz(g):
    c = on()
    panel(g, c)
    cokgen(g, c, [(0.88, 0.8), (1.92, 0.8), (1.92, 2.3), (0.88, 2.3)], D2, 0.012)
    sacak_lento(g, c, 0.88, 1.92, 2.36, False, SILME)


# ---------------------------------------------------------------- Victorian / Edwardian tuğla ev (v_)
# Kırmızı tuğla (bloğun duvar rengi) + beyaz taş söveler; zemin ve birinci katta çokgen cumba; kemerli giriş, karo yol,
# alçak bahçe duvarı + korkuluk.

BAHCE = 1.75         # ön bahçe duvarının cepheden uzaklığı


def bahce_duvari(g, c, u0, u1):
    """Alçak tuğla bahçe duvarı + taş harpuşta + kısa demir korkuluk."""
    c.kutu(g, u0, u1, BAHCE - 0.09, BAHCE + 0.09, 0.0, 0.48, D, arka=True)
    c.kutu(g, u0, u1, BAHCE - 0.12, BAHCE + 0.12, 0.48, 0.56, PORTLAND, arka=True)
    parmaklik(g, c, u0, u1, BAHCE, 0.56, 0.5, 0.2, kaide=False)


def _cumba(g, c, alt=True, ust_kapak=True, bahce=False, tohum=5):
    """Çokgen cumba (canted bay): tuğla etek, taş denizlik ve lento, üç yüzde giyotin cam. ust_kapak: üstü arduvaz / kurşun
    kırma çatıcıkla biter; değilse düz taş kornişle (üst kattaki cumba üstüne oturur)."""
    panel(g, c)
    dis = [(0.34, 0.0), (0.86, 0.66), (1.94, 0.66), (2.46, 0.0)]
    ic = [(0.4, 0.0), (0.9, 0.6), (1.9, 0.6), (2.4, 0.0)]
    zb = 0.82 if alt else 0.7

    def kat(nokta, z0, z1, mat):
        prizma(g, [c.p(u, o, z0) for u, o in nokta], (0, 0, z1 - z0), mat)
    kat(ic, 0.0, zb, D)
    kat(dis, zb, zb + 0.09, PORTLAND)
    kat(ic, zb + 0.09, 2.4, CAM)
    kat(dis, 2.4, 2.64, PORTLAND)
    for u, o in ic:                                                                   # taş dikmeler
        p = c.p(u, o, 0)
        kutu(g, p[0] - 0.07, p[0] + 0.07, p[1] - 0.07, p[1] + 0.07, zb + 0.09, 2.4, PORTLAND, atla=("zn", "zp"))
    zm = zb + 0.09 + (2.31 - zb) * 0.55
    for (ua, oa), (ub, ob) in zip(ic, ic[1:]):                                        # buluşma kayıtları
        cubuk(g, c.p(ua, oa + 0.012, zm), c.p(ub, ob + 0.012, zm), 0.05, SILME)
    iz(g, c, 1.386, 1.414, zb + 0.09, zm, IZ_BEYAZ, 0.61)
    iz(g, c, 1.386, 1.414, zm, 2.4, IZ_BEYAZ, 0.61)
    if ust_kapak:
        taban = [c.p(u, o, 2.64) for u, o in dis]
        tepe = [c.p(0.7, 0.0, 2.98), c.p(2.1, 0.0, 2.98)]
        kati(g, taban + tepe, [[0, 1, 4], [1, 2, 5, 4], [2, 3, 5]], KURSUN, ic=c.p(1.4, 0.2, 2.6))
    else:
        kat(ic, 2.64, KAT, D)
    tugla_izi(g, c, tohum, 2, u0=0.1, u1=HUCRE - 0.1, z0=2.5, z1=2.9)
    if bahce:
        bahce_duvari(g, c, 0.0, HUCRE)


ekle("v_cumba", "konut", "Victorian zemin: çokgen cumba, önde alçak bahçe duvarı + korkuluk", lambda g: _cumba(g, on(), True, False, True, 5))
ekle("v_cumba_ust", "konut", "Victorian birinci kat: cumbanın devamı, üstü kurşun çatıcık", lambda g: _cumba(g, on(), False, True, False, 6))
ekle("v_cumba_tek", "konut", "Victorian zemin: tek katlı cumba (üstü kurşun çatıcık), bahçe duvarı", lambda g: _cumba(g, on(), True, True, True, 7))


@parca("konut", "Victorian kapı: taş kemerli giriş, vitray camlı kapı, karo yol, bahçe kapısı babaları")
def v_kapi(g):
    c = on()
    panel(g, c)
    zk = 0.24
    for a, b in ((0.72, 0.9), (1.9, 2.08)):                                           # taş söveler
        c.kutu(g, a, b, 0, 0.1, 0.0, 2.1, PORTLAND)
        c.kutu(g, a - 0.03, b + 0.03, 0, 0.13, 2.0, 2.12, PORTLAND2)
    kemer_halka(g, c, 1.4, 2.1, 0.5, 0.2, PORTLAND, "yarim", 6, 0.1)
    c.kutu(g, 1.31, 1.49, 0, 0.14, 2.56, 2.86, PORTLAND2)                            # kilit taşı
    panelli_kapi(g, c, 0.9, 1.9, zk, 2.1, cam_ust=True)
    cokgen(g, c, _yay(1.4, 2.1, 0.5, "yarim", 6), CAM2, 0.012)
    iz(g, c, 1.386, 1.414, 2.1, 2.6, IZ_BEYAZ, 0.02)
    c.kutu(g, 0.7, 2.1, 0, 0.5, 0.0, zk, PORTLAND)
    c.kutu(g, 0.76, 2.04, 0.5, 0.78, 0.0, zk / 2, PORTLAND2)
    for i in range(4):                                                                # karo yol (dama)
        for j in range(3):
            yer_levha(g, c, 0.95 + j * 0.3, 1.25 + j * 0.3, 0.8 + i * 0.22, 1.02 + i * 0.22, 0.02, IZ_KARO[0 if (i + j) % 2 == 0 else 2])
    for a, b in ((0.66, 0.9), (1.9, 2.14)):                                           # bahçe kapısı babaları
        c.kutu(g, a, b, BAHCE - 0.12, BAHCE + 0.12, 0.0, 0.95, D, arka=True)
        c.kutu(g, a - 0.03, b + 0.03, BAHCE - 0.15, BAHCE + 0.15, 0.95, 1.04, PORTLAND, arka=True)
    bahce_duvari(g, c, 0.0, 0.66)
    bahce_duvari(g, c, 2.14, HUCRE)
    tugla_izi(g, c, 8, 2, z0=2.6, z1=2.9)


def _v_ust(g, cift, tohum, z0=0.8, z1=2.4):
    c = on()
    panel(g, c)
    if cift:
        sash(g, c, 0.5, 1.22, z0, z1, (1, 1), perde=1)
        sash(g, c, 1.58, 2.3, z0, z1, (1, 1), perde=1)
        c.kutu(g, 1.28, 1.52, 0, 0.07, z0 - 0.06, z1 + 0.06, PORTLAND)             # taş orta dikme
        tas_lento(g, c, 0.5, 2.3, z1 + 0.06)
        yasak = (0.5, 2.3, z0, z1)
    else:
        sash(g, c, 0.9, 1.9, z0, z1, (2, 1), perde=1)
        tas_lento(g, c, 0.9, 1.9, z1 + 0.06)
        c.kutu(g, 1.32, 1.48, 0, 0.1, z1 + 0.04, z1 + 0.32, PORTLAND2)             # kilit taşı
        yasak = (0.9, 1.9, z0, z1)
    tugla_izi(g, c, tohum, 4, yasak=yasak)


ekle("v_ust", "konut", "Victorian pencere: 2/2 giyotin, taş lento ve kilit taşı", lambda g: _v_ust(g, False, 21))
ekle("v_ust_cift", "konut", "Victorian çift pencere: taş orta dikmeli", lambda g: _v_ust(g, True, 22))
ekle("v_zemin", "konut", "Victorian zemin (yan / arka): 2/2 giyotin", lambda g: _v_ust(g, False, 23, 0.95, 2.4))


@parca("konut", "Victorian düz duvar: tuğla, taş bant")
def v_duz(g):
    c = on()
    panel(g, c)
    iz(g, c, 0.0, HUCRE, 1.5, 1.58, IZ_TAS, 0.012)
    tugla_izi(g, c, 24, 5)


@parca("bant", "Victorian kat silmesi (ince taş bant)")
def v_silme(g):
    c = on()
    c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.07, -0.1, 0.0, PORTLAND)


# ---------------------------------------------------------------- mews evi (m_)
# Arnavut kaldırımlı dar sokakta iki katlı: altta garaj kapısı, pastel boyalı tuğla, saksılar.

@parca("konut", "Mews zemin: çift kanatlı garaj kapısı (camlı üst), basık tuğla kemer, saksı")
def m_garaj(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.3, 2.5, 0, 0.05, 0.03, 2.3, VR)
    iz(g, c, 1.388, 1.412, 0.03, 2.3, IZ_DEMIR, 0.056)
    for a, b in ((0.4, 1.3), (1.5, 2.4)):
        cokgen(g, c, [(a, 0.15), (b, 0.15), (b, 1.45), (a, 1.45)], VR, 0.066)
        cokgen(g, c, [(a, 1.6), (b, 1.6), (b, 2.2), (a, 2.2)], CAM2, 0.056)
        for k in (1, 2):
            uu = a + (b - a) * k / 3
            iz(g, c, uu - 0.012, uu + 0.012, 1.6, 2.2, IZ_BEYAZ, 0.06)
    c.kutu(g, 0.2, 0.3, 0, 0.07, 0.0, 2.3, SILME)
    c.kutu(g, 2.5, 2.6, 0, 0.07, 0.0, 2.3, SILME)
    cokgen(g, c, [(0.2, 2.3), (2.6, 2.3), (2.68, 2.56), (0.12, 2.56)], D2, 0.03)         # düz tuğla kemer
    p = c.p(0.12, 0.28, 0)
    saksi_k(g, p[0], p[1], 0.0, 0.17, 1)


@parca("konut", "Mews kapısı: boyalı kapı, küçük pencere, saksılar, fener")
def m_kapi(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.42, 0.52, 0, 0.07, 0.0, 2.3, SILME)
    c.kutu(g, 1.42, 1.52, 0, 0.07, 0.0, 2.3, SILME)
    c.kutu(g, 0.42, 1.52, 0, 0.09, 2.3, 2.4, SILME)
    panelli_kapi(g, c, 0.52, 1.42, 0.08, 2.3, cam_ust=True)
    c.kutu(g, 0.4, 1.54, 0, 0.4, 0.0, 0.08, PORTLAND)
    sash(g, c, 1.85, 2.45, 1.1, 2.1, (2, 1), perde=1)
    for uu, ci in ((0.24, 0), (1.72, 3)):
        p = c.p(uu, 0.3, 0)
        saksi_k(g, p[0], p[1], 0.0, 0.16, ci)
    p = c.p(1.66, 0.14, 0)
    kutu(g, p[0] - 0.06, p[0] + 0.06, p[1] - 0.06, p[1] + 0.06, 1.95, 2.15, ISIK, atla=())
    kutu(g, p[0] - 0.085, p[0] + 0.085, p[1] - 0.085, p[1] + 0.085, 2.15, 2.19, DEMIR_S, atla=())


@parca("konut", "Mews üst kat: giyotin pencere + çiçek kutusu")
def m_ust(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.9, 1.9, 0.9, 2.25, (2, 2), perde=2)
    cokgen(g, c, [(0.84, 2.31), (1.96, 2.31), (2.04, 2.52), (0.76, 2.52)], D2, 0.022)
    cicek_kutusu(g, c, 0.82, 1.98, 0.8)


@parca("konut", "Mews üst kat: samanlık kapısı (uzun pencere, demir korkuluk)")
def m_ust_kapi(g):
    c = on()
    panel(g, c)
    sash(g, c, 0.85, 1.95, 0.25, 2.3, (2, 2), perde=2, denizlik=False)
    cokgen(g, c, [(0.79, 2.36), (2.01, 2.36), (2.09, 2.57), (0.71, 2.57)], D2, 0.022)
    parmaklik(g, c, 0.75, 2.05, 0.14, 0.2, 0.9, 0.16, kaide=False)


@parca("konut", "Mews düz duvar (boyalı tuğla)")
def m_duz(g):
    c = on()
    panel(g, c)
    tugla_izi(g, c, 41, 4)


@parca("bant", "Mews silmesi (dişli tuğla sırası)")
def m_silme(g):
    c = on()
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.05, -0.12, 0.0, D2)


# ---------------------------------------------------------------- Portland taşı neoklasik (p_)
# Açık gri taş (bloğun duvar rengi): rustika zemin, pilastrlı üst katlar, alınlıklı pencereler, üstte saçaklık ve tavan arası.

def _pilastr(g, c, z0=0.0, z1=KAT):
    for a, b in ((0.0, 0.17), (HUCRE - 0.17, HUCRE)):
        c.kutu(g, a, b, 0, 0.1, z0, z1, D)
        c.kutu(g, a, b, 0, 0.14, z0, z0 + 0.16, D2)


def _p_zemin(g, kapi=False):
    c = on()
    panel(g, c)
    rustika(g, c, yasak=(0.7, 2.1, 0.5, 2.7), mat=IZ_D, adim=0.4)
    sash(g, c, 0.88, 1.92, 0.85, 1.85, (3, 2), "yarim", perde=1)
    kemer_halka(g, c, 1.4, 1.85, 0.58, 0.2, D2, "yarim", 6, 0.03)
    c.kutu(g, 1.31, 1.49, 0, 0.1, 2.36, 2.7, D2)                                      # kilit taşı
    c.kutu(g, -0.05, HUCRE + 0.05, 0, 0.08, 0.0, 0.42, D2)


ekle("p_zemin", "kamu", "Portland zemin: rustika, kemerli pencere, kilit taşı", lambda g: _p_zemin(g))


def _p_ust(g, z0, z1, alinlik, pilastr=True, sacaklik=False):
    c = on()
    panel(g, c)
    if pilastr:
        _pilastr(g, c, 0.0, KAT)
    if sacaklik:                                                                      # tavan arası: altta saçaklık (korniş)
        c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.12, 0.0, 0.2, D2)
        c.kutu(g, -0.06, HUCRE + 0.06, 0, 0.3, 0.2, 0.32, D)
    sash(g, c, 0.9, 1.9, z0, z1, (3, 2) if z1 - z0 > 1.2 else (3, 1), perde=1)
    for a, b in ((0.76, 0.84), (1.96, 2.04)):
        c.kutu(g, a, b, 0, 0.06, z0 - 0.06, z1 + 0.06, D2)
    sacak_lento(g, c, 0.9, 1.9, z1 + 0.06, alinlik, D2)


ekle("p_kat1", "kamu", "Portland birinci kat: pilastrlar, alınlıklı uzun pencere", lambda g: _p_ust(g, 0.55, 2.25, True))
ekle("p_ust", "kamu", "Portland üst kat: pilastrlar, kornişli pencere", lambda g: _p_ust(g, 0.75, 2.25, False))
ekle("p_tavan", "kamu", "Portland tavan arası: saçaklık üstünde küçük pencere", lambda g: _p_ust(g, 1.05, 2.05, False, False, True))


@parca("kamu", "Portland düz duvar: pilastrlar, kör niş")
def p_duz(g):
    c = on()
    panel(g, c)
    _pilastr(g, c)
    cokgen(g, c, [(0.9, 0.75), (1.9, 0.75), (1.9, 2.25), (0.9, 2.25)], D2, 0.012)
    sacak_lento(g, c, 0.9, 1.9, 2.31, False, D2)


@parca("bant", "Portland zemin kat silmesi")
def p_silme(g):
    c = on()
    c.kutu(g, -0.03, HUCRE + 0.03, 0, 0.08, -0.16, -0.04, D2)
    c.kutu(g, -0.06, HUCRE + 0.06, 0, 0.16, -0.04, 0.05, D)


@parca("kose", "Portland köşe: rustika köşe taşları")
def p_kose(g):
    z, i = 0.02, 0
    while z < KAT - 0.2:
        w = 0.5 if i % 2 == 0 else 0.34
        kutu(g, -A - 0.04, -A + w, -A - 0.04, -A + (0.84 - w), z, z + 0.42, D2, atla=())
        z += 0.5
        i += 1


@parca("kamu", "Sütunlu cephe (Portland taşı sütun dizisi, arkada kemerli pencere)")
def sutunlu(g):
    c = on()
    panel(g, c, PORTLAND)
    sash(g, c, 0.95, 1.85, 0.7, 1.5, (3, 2), "yarim", perde=1)
    for uu in (0.0, HUCRE):                                                           # hücre sınırında sütun (komşununkiyle çakışır)
        p = c.p(uu, 0.62, 0)
        kutu(g, p[0] - 0.21, p[0] + 0.21, p[1] - 0.21, p[1] + 0.21, 0.0, 0.2, PORTLAND2)
        dsil(g, p[0], p[1], 0.2, 1.98, 0.16, 8, PORTLAND, r1=0.135, kapak=(False, False))
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 1.98, 2.1, PORTLAND, atla=())
    # saçaklık tabela panosunun (2,3 m) altında biter: yazı frizde görünür
    c.kutu(g, 0.0, HUCRE, 0, 0.84, 2.1, 2.26, PORTLAND)
    c.kutu(g, 0.0, HUCRE, 0, 0.84, 0.0, 0.08, PORTLAND2)


@parca("kamu", "Sütunlu giriş (portiko: sütunlar, alınlık, çift kanat kapı, basamaklar)")
def kapi_sutunlu(g):
    c = on()
    panel(g, c, PORTLAND)
    c.kutu(g, 0.8, 0.92, 0, 0.1, 0.24, 2.1, PORTLAND2)
    c.kutu(g, 1.88, 2.0, 0, 0.1, 0.24, 2.1, PORTLAND2)
    panelli_kapi(g, c, 0.92, 1.88, 0.24, 2.08, cift=True, cam_ust=True)
    for uu in (0.0, HUCRE):
        p = c.p(uu, 0.62, 0)
        kutu(g, p[0] - 0.21, p[0] + 0.21, p[1] - 0.21, p[1] + 0.21, 0.0, 0.42, PORTLAND2)
        dsil(g, p[0], p[1], 0.42, 1.98, 0.16, 8, PORTLAND, r1=0.135, kapak=(False, False))
        kutu(g, p[0] - 0.2, p[0] + 0.2, p[1] - 0.2, p[1] + 0.2, 1.98, 2.1, PORTLAND, atla=())
    c.kutu(g, 0.0, HUCRE, 0, 0.84, 2.1, 2.26, PORTLAND)
    for i in range(3):
        c.kutu(g, 0.22, HUCRE - 0.22, 0, 0.84 + 0.3 * (2 - i) - 0.3, 0.08 * i, 0.08 * (i + 1), PORTLAND if i % 2 else PORTLAND2)


@parca("kamu", "Alınlık (çatıda: taş kaide, üçgen alınlık, madalyon, bayrak direği)")
def alinlik_tas(g):
    # kök: kapı hücresinin ortasından 0,3 m içeride, çatı tabanı. Cephe hizasında, parapetin üstüne oturur.
    c = C((-A, -A - 0.3), (1, 0), (0, -1), HUCRE)
    c.kutu(g, -0.02, HUCRE + 0.02, -0.5, 0.08, -0.02, 0.95, PORTLAND, arka=True)
    c.kutu(g, -0.1, HUCRE + 0.1, -0.5, 0.2, 0.95, 1.07, PORTLAND2, arka=True)
    c.prizma_uc(g, -0.1, HUCRE + 0.1, -0.5, 0.14, 1.07, 0.9, PORTLAND)
    p = c.p(1.4, 0.14, 1.4)
    silindir(g, p, (p[0], p[1] - 0.04, p[2]), 0.24, 0.24, 10, VR)
    cubuk(g, (0, -A, 1.9), (0, -A, 4.3), 0.06, PM("Metal", "#c9ccd0"))
    meyve(g, 0, -A, 4.3, 0.08, PIRINC)
    kutu(g, 0.04, 1.2, -A - 0.02, -A + 0.02, 3.5, 4.2, VR, atla=())
    kutu(g, 0.04, 1.2, -A - 0.026, -A + 0.026, 3.76, 3.94, SILME, atla=())


@parca("kamu", "Cephe saati (taş çerçeveli, siyah akrepli)")
def saat_cephe(g):
    c = on()
    p = c.p(1.4, 0.0, 1.7)
    silindir(g, p, (p[0], p[1] - 0.1, p[2]), 0.82, 0.82, 12, PORTLAND2)
    silindir(g, (p[0], p[1] - 0.1, p[2]), (p[0], p[1] - 0.13, p[2]), 0.66, 0.66, 12, SILME)
    iz(g, c, 1.375, 1.425, 1.7, 2.2, IZ_DEMIR, 0.14)
    iz(g, c, 1.4, 1.75, 1.675, 1.725, IZ_DEMIR, 0.14)
    for i in range(12):
        a = i * math.pi / 6
        u, z = 1.4 + 0.56 * math.cos(a), 1.7 + 0.56 * math.sin(a)
        s = 0.045 if i % 3 == 0 else 0.022
        iz(g, c, u - s, u + s, z - s, z + s, IZ_DEMIR, 0.14)


# ---------------------------------------------------------------- Victorian gotik (gt_): gar, kütüphane, okul
# Kırmızı tuğla + taş süsler: sivri kemerli pencereler, taş bantlar, payandalı köşeler, dik arduvaz çatı.

def _gt_duvar(g, c, tohum):
    panel(g, c)
    iz(g, c, 0.0, HUCRE, 0.5, 0.58, IZ_TAS, 0.012)
    iz(g, c, 0.0, HUCRE, 2.72, 2.8, IZ_TAS, 0.012)
    tugla_izi(g, c, tohum, 2, z0=0.7, z1=2.6, u0=0.1, u1=0.9)


def _gt_pencere(g, c, u0, u1, z0, zs, cam=CAM):
    """Sivri kemerli pencere + taş söve."""
    r = (u1 - u0) / 2
    sash(g, c, u0, u1, z0, zs, (2, 2), "sivri", perde=0, cam=cam, cerceve=PORTLAND)
    kemer_halka(g, c, (u0 + u1) / 2, zs, r + 0.06, 0.1, PORTLAND2, "sivri", 6, 0.07)


@parca("kamu", "Gotik pencere: sivri kemerli, taş söveli")
def gt_ust(g):
    c = on()
    _gt_duvar(g, c, 51)
    _gt_pencere(g, c, 0.95, 1.85, 0.8, 1.75)


@parca("kamu", "Gotik çift pencere: iki sivri kemer + üstte gül pencere")
def gt_ust_cift(g):
    c = on()
    _gt_duvar(g, c, 52)
    _gt_pencere(g, c, 0.62, 1.22, 0.8, 1.6)
    _gt_pencere(g, c, 1.58, 2.18, 0.8, 1.6)
    pts = [(1.4 + 0.2 * math.cos(2 * math.pi * i / 8), 2.42 + 0.2 * math.sin(2 * math.pi * i / 8)) for i in range(8)]
    cokgen(g, c, pts, CAM2, 0.012)
    pts2 = [(1.4 + 0.29 * math.cos(2 * math.pi * i / 8), 2.42 + 0.29 * math.sin(2 * math.pi * i / 8)) for i in range(8)]
    for i in range(8):
        j = (i + 1) % 8
        cokgen(g, c, [pts[i], pts2[i], pts2[j], pts[j]], PORTLAND, 0.05)


@parca("kamu", "Gotik zemin: sivri kemerli pencere, taş kaide")
def gt_zemin(g):
    c = on()
    _gt_duvar(g, c, 53)
    _gt_pencere(g, c, 0.95, 1.85, 0.95, 1.8)
    c.kutu(g, -0.05, HUCRE + 0.05, 0, 0.08, 0.0, 0.44, PORTLAND2)


@parca("kamu", "Gotik kapı: sivri kemerli taş portal, demir menteşeli ahşap kapı, basamak")
def gt_kapi(g):
    c = on()
    _gt_duvar(g, c, 54)
    um, zs, r = 1.4, 1.75, 0.62
    cokgen(g, c, [(um - r, 0.16), (um + r, 0.16)] + _yay(um, zs, r, "sivri", 6), AHSAP2, 0.03)
    iz(g, c, um - 0.013, um + 0.013, 0.16, zs + r * 1.7, IZ_DEMIR, 0.036)
    for zz in (0.6, 1.5):
        iz(g, c, um - r + 0.06, um + r - 0.06, zz, zz + 0.05, IZ_DEMIR, 0.036)
    for a, b in ((um - r - 0.2, um - r), (um + r, um + r + 0.2)):
        c.kutu(g, a, b, 0, 0.14, 0.0, zs, PORTLAND)
    kemer_halka(g, c, um, zs, r, 0.2, PORTLAND, "sivri", 6, 0.14)
    kemer_halka(g, c, um, zs, r + 0.2, 0.09, PORTLAND2, "sivri", 6, 0.08)
    c.kutu(g, 0.4, 2.4, 0, 0.6, 0.0, 0.16, PORTLAND)
    c.kutu(g, 0.3, 2.5, 0.6, 0.9, 0.0, 0.08, PORTLAND2)


@parca("kamu", "Gotik düz duvar: tuğla, taş bantlar, haç biçimli taş süs")
def gt_duz(g):
    c = on()
    _gt_duvar(g, c, 55)
    cokgen(g, c, [(1.3, 1.2), (1.5, 1.2), (1.5, 2.2), (1.3, 2.2)], PORTLAND, 0.02)
    cokgen(g, c, [(1.0, 1.65), (1.8, 1.65), (1.8, 1.85), (1.0, 1.85)], PORTLAND, 0.021)


@parca("kose", "Gotik köşe: kademeli taş payanda")
def gt_kose(g):
    kutu(g, -A - 0.16, -A + 0.3, -A - 0.16, -A + 0.3, 0.0, 1.9, D)
    kutu(g, -A - 0.2, -A + 0.34, -A - 0.2, -A + 0.34, 1.9, 2.04, PORTLAND, atla=())
    kutu(g, -A - 0.08, -A + 0.24, -A - 0.08, -A + 0.24, 2.04, KAT, D)
    kutu(g, -A - 0.2, -A + 0.34, -A - 0.2, -A + 0.34, 0.0, 0.44, PORTLAND2)


@parca("kose", "Gotik üst köşe: ince payanda + taş başlık")
def gt_kose_ust(g):
    kutu(g, -A - 0.08, -A + 0.24, -A - 0.08, -A + 0.24, 0.0, KAT, D)
    kutu(g, -A - 0.12, -A + 0.28, -A - 0.12, -A + 0.28, KAT - 0.3, KAT - 0.18, PORTLAND, atla=())


# ---------------------------------------------------------------- tuğla depo / atölye (d_)

def _d_pencere(g, z0, zs, tohum):
    c = on()
    panel(g, c)
    sash(g, c, 0.7, 2.1, z0, zs, (4, 2), "basik", perde=0, cerceve=SILME2)
    kemer_halka(g, c, 1.4, zs, 0.76, 0.18, D2, "basik", 6, 0.03)
    tugla_izi(g, c, tohum, 3, yasak=(0.7, 2.1, z0, zs + 0.3))


ekle("d_ust", "sanayi", "Depo penceresi: basık kemerli, küçük camlı", lambda g: _d_pencere(g, 0.9, 2.0, 61))
ekle("d_zemin", "sanayi", "Depo zemin penceresi: basık kemerli, yüksek", lambda g: _d_pencere(g, 1.2, 2.1, 62))


@parca("sanayi", "Depo düz duvar: tuğla, taş bant")
def d_duz(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.0, HUCRE, 0, 0.04, 2.0, 2.12, PORTLAND2)
    tugla_izi(g, c, 63, 5, z1=1.9)


@parca("kose", "Depo köşesi: tuğla pilastr")
def d_kose(g):
    kutu(g, -A - 0.06, -A + 0.3, -A - 0.06, -A + 0.3, 0.0, KAT, D2)


# ---------------------------------------------------------------- fabrika (f_), modern market / hastane (mo_)

DEMIR_KAYIT = PM("Metal", "#4f565e")


def _f_pencere(g, z0, zs, tohum):
    """Fabrika penceresi: tuğla duvarda geniş, çok bölmeli demir doğramalı pencere, basık kemer, taş denizlik."""
    c = on()
    panel(g, c)
    sash(g, c, 0.32, HUCRE - 0.32, z0, zs, (6, 4), "basik", perde=0, cerceve=DEMIR_KAYIT)
    kemer_halka(g, c, HUCRE / 2, zs, (HUCRE - 0.64) / 2 + 0.06, 0.2, D2, "basik", 6, 0.03)
    c.kutu(g, 0.24, HUCRE - 0.24, 0, 0.12, z0 - 0.12, z0, PORTLAND2)
    tugla_izi(g, c, tohum, 2, yasak=(0.32, HUCRE - 0.32, z0, zs + 0.3))


ekle("f_ust", "sanayi", "Fabrika penceresi: geniş, demir doğramalı", lambda g: _f_pencere(g, 0.75, 2.25, 71))
ekle("f_zemin", "sanayi", "Fabrika zemin penceresi: yüksek, demir doğramalı", lambda g: _f_pencere(g, 0.95, 2.35, 72))


@parca("sanayi", "Fabrika kepengi: geniş yükleme kapısı, üstünde vinç kirişi")
def f_kapi(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.2, HUCRE - 0.2, 0, 0.02, 0.0, 2.5, PM("Sac", "#7d8a94"))
    for i in range(10):
        zz = 0.1 + i * 0.24
        c.kutu(g, 0.22, HUCRE - 0.22, 0.02, 0.05, zz, zz + 0.04, PM("Sac", "#6c7882"))
    c.kutu(g, 0.1, 0.22, 0, 0.12, 0.0, 2.62, PORTLAND2)
    c.kutu(g, HUCRE - 0.22, HUCRE - 0.1, 0, 0.12, 0.0, 2.62, PORTLAND2)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.14, 2.5, 2.66, PORTLAND2)
    c.kutu(g, HUCRE / 2 - 0.06, HUCRE / 2 + 0.06, 0, 0.7, 2.72, 2.86, DEMIR_KAYIT)      # vinç kirişi
    c.kutu(g, HUCRE / 2 - 0.02, HUCRE / 2 + 0.02, 0.6, 0.64, 2.3, 2.72, DEMIR_KAYIT)
    c.kutu(g, 0.15, 0.95, 0, 0.35, 0.0, 0.14, PM("Beton", "#b8b4ac"))                  # yükleme rampası ağzı
    c.kutu(g, HUCRE - 0.95, HUCRE - 0.15, 0, 0.35, 0.0, 0.14, PM("Beton", "#b8b4ac"))


MO_PANEL = ROL("Duvar", "d", "#d9dde0")
MO_METAL = PM("Metal", "#8d969e")


def _mo_paneller(g, c, z0, z1):
    for uu in (HUCRE / 3, 2 * HUCRE / 3):
        iz(g, c, uu - 0.012, uu + 0.012, z0, z1, IZ_D, 0.015)


@parca("market", "Modern cephe: kaplama paneller, üstte metal bant")
def mo_duz(g):
    c = on()
    panel(g, c)
    _mo_paneller(g, c, 0.0, KAT - 0.3)
    iz(g, c, 0.0, HUCRE, 1.48, 1.51, IZ_D, 0.015)
    c.kutu(g, 0, HUCRE, 0, 0.06, KAT - 0.3, KAT, MO_METAL)


@parca("market", "Modern üst kat: yatay şerit pencere")
def mo_ust(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0, HUCRE, 0, 0.015, 0.85, 2.3, CAM2)
    for uu in (0.0, HUCRE / 3, 2 * HUCRE / 3, HUCRE - 0.05):
        c.kutu(g, uu, uu + 0.05, 0, 0.06, 0.85, 2.3, MO_METAL)
    c.kutu(g, 0, HUCRE, 0, 0.08, 0.78, 0.85, MO_METAL)
    c.kutu(g, 0, HUCRE, 0, 0.06, 2.3, 2.36, MO_METAL)
    _mo_paneller(g, c, 2.36, KAT)


@parca("market", "Modern zemin: boydan cam vitrin, üstte marka renginde bant")
def mo_vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.05, HUCRE - 0.05, 0, 0.015, 0.12, 2.2, VIT)
    for uu in (0.0, HUCRE / 2 - 0.03, HUCRE - 0.06):
        c.kutu(g, uu, uu + 0.06, 0, 0.07, 0.0, 2.2, MO_METAL)
    c.kutu(g, 0, HUCRE, 0, 0.07, 0.0, 0.12, MO_METAL)
    c.kutu(g, 0, HUCRE, 0, 0.08, 2.2, 2.26, MO_METAL)
    _mo_paneller(g, c, 2.26, KAT - 0.3)                                          # üstü kaplama: tabela bandı buraya oturur
    c.kutu(g, 0, HUCRE, 0, 0.06, KAT - 0.3, KAT, MO_METAL)
    c.kutu(g, 0.35, 1.0, 0.016, 0.02, 1.0, 1.7, PM("Afis", "#f5c84a"))


@parca("cati", "Düz çatıda klima / havalandırma ünitesi (modern binalar)")
def cati_klima(g):
    # kök çatıdan 0,9 m yukarıda (baca_ince ile aynı kural): gövde çatıya iner
    kutu(g, -0.55, 0.55, -0.35, 0.35, -0.85, -0.1, MO_METAL)
    kutu(g, -0.5, 0.5, -0.3, 0.3, -0.1, -0.06, PM("Metal", "#a7afb6"), atla=())
    for sx in (-0.26, 0.26):
        dsil(g, sx, 0.0, -0.06, -0.02, 0.2, 8, PM("Metal", "#5f666d"), kapak=(False, True))
    kutu(g, 0.55, 0.75, -0.08, 0.08, -0.85, -0.55, PM("Metal", "#7b838a"))


@parca("kose", "Modern köşe: metal köşe profili")
def mo_kose(g):
    kutu(g, -A - 0.05, -A + 0.12, -A - 0.05, -A + 0.12, 0.0, KAT, MO_METAL)


@parca("bant", "Modern kat bandı: ince metal")
def mo_bant(g):
    c = on()
    c.kutu(g, -0.02, HUCRE + 0.02, 0, 0.07, -0.08, 0.0, MO_METAL)


# ---------------------------------------------------------------- Londra sokak donatıları

def kare_yuz(n, e):
    """Merkezi kökte, yarı kenarı e olan karenin n yönüne bakan yüzünün cephe çerçevesi (u: 0…2e)."""
    return C((n[1] * e + n[0] * e, -n[0] * e + n[1] * e), (-n[1], n[0]), n, 2 * e)


@_cevre("Kırmızı telefon kulübesi (K6)", "donati:ilan_sutunu")
def telefon_kulubesi(g):
    e = 0.46
    kutu(g, -e - 0.04, e + 0.04, -e - 0.04, e + 0.04, 0.0, 0.1, DEMIR_S)
    kutu(g, -e, e, -e, e, 0.1, 2.3, KIRMIZI)
    kati(g, [(-e - 0.03, -e - 0.03, 2.3), (e + 0.03, -e - 0.03, 2.3), (e + 0.03, e + 0.03, 2.3), (-e - 0.03, e + 0.03, 2.3),
             (-e * 0.5, -e * 0.5, 2.5), (e * 0.5, -e * 0.5, 2.5), (e * 0.5, e * 0.5, 2.5), (-e * 0.5, e * 0.5, 2.5)],
         [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [4, 5, 6, 7], [0, 1, 2, 3]], KIRMIZI)
    for i, n in enumerate(YON):
        cy = kare_yuz(n, e)
        cokgen(g, cy, [(0.1, 0.35), (2 * e - 0.1, 0.35), (2 * e - 0.1, 1.98), (0.1, 1.98)], CAM2, 0.012)
        for k in (1, 2):
            uu = 0.1 + (2 * e - 0.2) * k / 3
            iz(g, cy, uu - 0.012, uu + 0.012, 0.35, 1.98, PM("Iz", "#c8413a"), 0.02)
        for k in range(1, 6):
            zz = 0.35 + 1.63 * k / 6
            iz(g, cy, 0.1, 2 * e - 0.1, zz - 0.012, zz + 0.012, PM("Iz", "#c8413a"), 0.02)
        iz(g, cy, 0.14, 2 * e - 0.14, 2.06, 2.22, PM("Iz", "#fbf7ee"), 0.012)           # "TELEPHONE" levhası


@_cevre("Kırmızı posta kutusu (pillar box)", "donati:posta_kutusu")
def posta_kutusu(g):
    dsil(g, 0, 0, 0.0, 0.16, 0.27, 10, DEMIR_S)
    dsil(g, 0, 0, 0.16, 1.2, 0.24, 10, KIRMIZI, kapak=(False, False))
    dsil(g, 0, 0, 1.2, 1.3, 0.29, 10, KIRMIZI)
    dsil(g, 0, 0, 1.3, 1.42, 0.27, 10, KIRMIZI, r1=0.1)
    c = C((-0.14, -0.245), (1, 0), (0, -1), 0.28)
    iz(g, c, 0.02, 0.26, 1.0, 1.06, IZ_DEMIR, 0.01)
    iz(g, c, 0.05, 0.23, 0.62, 0.86, PM("Iz", "#fbf7ee"), 0.01)


@_cevre("Siyah Victoria sokak lambası", "donati:lamba_klasik")
def sokak_lambasi(g):
    dsil(g, 0, 0, 0, 0.5, 0.17, 8, DEMIR_S, r1=0.1)
    dsil(g, 0, 0, 0.5, 0.62, 0.13, 8, DEMIR_S)
    dsil(g, 0, 0, 0.62, 3.0, 0.065, 6, DEMIR_S, r1=0.045, kapak=(False, False))
    cubuk(g, (-0.3, 0, 2.62), (0.3, 0, 2.62), 0.035, DEMIR_S)                          # merdiven kolu
    dsil(g, 0, 0, 3.0, 3.08, 0.12, 6, DEMIR_S)
    kati(g, [(-0.11, -0.11, 3.08), (0.11, -0.11, 3.08), (0.11, 0.11, 3.08), (-0.11, 0.11, 3.08),
             (-0.2, -0.2, 3.5), (0.2, -0.2, 3.5), (0.2, 0.2, 3.5), (-0.2, 0.2, 3.5)],
         [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7], [0, 1, 2, 3], [4, 5, 6, 7]], ISIK)
    kati(g, [(-0.24, -0.24, 3.5), (0.24, -0.24, 3.5), (0.24, 0.24, 3.5), (-0.24, 0.24, 3.5), (0, 0, 3.74)],
         [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], DEMIR_S)
    meyve(g, 0, 0, 3.72, 0.05, DEMIR_S, n=5, rz=0.16)


def _sokak_lambasi_hafif(g):
    cubuk(g, (0, 0, 0), (0, 0, 3.1), 0.12, DEMIR_S)
    kutu(g, -0.17, 0.17, -0.17, 0.17, 3.1, 3.55, ISIK, atla=())


PARCA_HAFIF["sokak_lambasi"] = _sokak_lambasi_hafif


@_cevre("Siyah dökme demir bollard", "donati:baba")
def bollard(g):
    dsil(g, 0, 0, 0.0, 0.12, 0.13, 8, DEMIR_S)
    dsil(g, 0, 0, 0.12, 0.8, 0.09, 8, DEMIR_S, r1=0.075, kapak=(False, False))
    dsil(g, 0, 0, 0.62, 0.7, 0.085, 8, SILME, kapak=(False, False))
    dsil(g, 0, 0, 0.8, 0.86, 0.11, 8, DEMIR_S)
    meyve(g, 0, 0, 0.86, 0.09, DEMIR_S, n=6, rz=0.12)


@_cevre("Yeşil bank (dökme demir ayaklı)", "donati:bank")
def bank(g):
    ys = PM("Boya", "#4f7a5d")
    for sx in (-0.72, 0.72):
        kutu(g, sx - 0.04, sx + 0.04, -0.24, 0.24, 0, 0.44, DEMIR_S)
        kutu(g, sx - 0.04, sx + 0.04, 0.16, 0.24, 0.44, 0.92, DEMIR_S)
        kutu(g, sx - 0.05, sx + 0.05, -0.26, 0.2, 0.6, 0.65, DEMIR_S, atla=())          # kolçak
    kutu(g, -0.8, 0.8, -0.24, 0.18, 0.44, 0.5, ys, atla=())
    kutu(g, -0.8, 0.8, 0.15, 0.2, 0.58, 0.92, ys, atla=())


@_cevre("Belisha feneri (zebra geçidi; siyah-beyaz direk, turuncu küre)")
def belisha(g):
    for i in range(6):
        dsil(g, 0, 0, i * 0.38, (i + 1) * 0.38, 0.055, 6, DEMIR_S if i % 2 == 0 else SILME, kapak=(False, i == 5))
    top(g, (0, 0, 2.46), 0.2, 0.2, 0.2, PM("Isik", "#f7a83e"))
    dsil(g, 0, 0, 0.0, 0.08, 0.1, 6, DEMIR_S)


def _belisha_hafif(g):
    cubuk(g, (0, 0, 0), (0, 0, 2.3), 0.1, SILME)
    kutu(g, -0.17, 0.17, -0.17, 0.17, 2.28, 2.62, PM("Isik", "#f7a83e"), atla=())


PARCA_HAFIF["belisha"] = _belisha_hafif


@_cevre("Otobüs durağı (direk + kırmızı yuvarlak levha, camlı sığınak)", "donati:durak")
def durak(g):
    for sx in (-1.3, 1.3):
        cubuk(g, (sx, 0.5, 0), (sx, 0.5, 2.35), 0.07, DEMIR_S)
        cubuk(g, (sx, -0.35, 2.3), (sx, 0.5, 2.35), 0.05, DEMIR_S)
    kutu(g, -1.45, 1.45, -0.5, 0.6, 2.35, 2.43, KIRMIZI, atla=())
    levha(g, [(-1.26, 0.5, 0.45), (1.26, 0.5, 0.45), (1.26, 0.5, 2.25), (-1.26, 0.5, 2.25)], CAM2, cift=True)
    kutu(g, -1.0, 1.0, 0.1, 0.42, 0.42, 0.48, KIRMIZI, atla=())
    for sx in (-0.8, 0.8):
        kutu(g, sx - 0.04, sx + 0.04, 0.12, 0.4, 0, 0.42, DEMIR_S)
    cubuk(g, (1.9, 0, 0), (1.9, 0, 2.75), 0.07, DEMIR_S)                               # durak direği
    kutu(g, 1.94, 2.5, -0.025, 0.025, 2.2, 2.75, SILME, atla=())
    for s in (-1, 1):                                                                  # kırmızı yuvarlak levha (iki yüzde)
        pts = [(2.22 + 0.19 * math.cos(2 * math.pi * i / 10) * s, s * 0.03, 2.5 + 0.19 * math.sin(2 * math.pi * i / 10)) for i in range(10)]
        levha(g, pts[::-1], PM("Iz", "#c8413a"))
        levha(g, [(2.0, s * 0.034, 2.47), (2.44, s * 0.034, 2.47), (2.44, s * 0.034, 2.53), (2.0, s * 0.034, 2.53)][::(-1 if s > 0 else 1)], PM("Iz", "#27386b"))
    kutu(g, 1.78, 2.02, -0.03, 0.03, 1.1, 1.7, SILME, atla=())                         # sefer çizelgesi


@_cevre("Siyah demir korkuluk (bir hücre; mızrak başlı, taş kaideli)")
def demir_korkuluk(g):
    parmaklik(g, C((-A, 0.0), (1, 0), (0, -1), HUCRE), 0.0, HUCRE, 0.0, 0.0, 1.25, 0.2)


@_cevre("Demir bahçe kapısı (taş babalar, alçak kapı)")
def demir_kapi(g):
    c = C((-A, 0.0), (1, 0), (0, -1), HUCRE)
    for a, b in ((0.0, 0.7), (2.1, HUCRE)):
        parmaklik(g, c, a, b, 0.0, 0.0, 1.25, 0.2)
    for uu in (0.82, 1.98):
        p = c.p(uu, 0, 0)
        kutu(g, p[0] - 0.13, p[0] + 0.13, p[1] - 0.13, p[1] + 0.13, 0.0, 1.35, PORTLAND)
        kutu(g, p[0] - 0.17, p[0] + 0.17, p[1] - 0.17, p[1] + 0.17, 1.35, 1.45, PORTLAND2, atla=())
        meyve(g, p[0], p[1], 1.45, 0.12, PORTLAND, n=6)


@_cevre("Polis feneri (mavi)")
def polis_feneri(g):
    dsil(g, 0, 0, 0, 0.3, 0.13, 6, DEMIR_S, r1=0.08)
    dsil(g, 0, 0, 0.3, 2.6, 0.05, 6, DEMIR_S, kapak=(False, False))
    kutu(g, -0.15, 0.15, -0.15, 0.15, 2.6, 3.0, PM("Isik", "#6f9be0"), atla=())
    kutu(g, -0.19, 0.19, -0.19, 0.19, 3.0, 3.06, DEMIR_S, atla=())


@_cevre("Çöp kutusu (siyah, altın bantlı)", "donati:cop_kutusu")
def cop_kutusu(g):
    dsil(g, 0, 0, 0, 0.85, 0.25, 8, DEMIR_S, r1=0.27, kapak=(False, True), kapak_mat=IZ_GOLGE)
    dsil(g, 0, 0, 0.6, 0.68, 0.275, 8, PIRINC, kapak=(False, False))
    dsil(g, 0, 0, 0.85, 0.9, 0.3, 8, DEMIR_S, kapak=(True, True))


# ---- yol: zebra geçidi, çift sarı çizgi

@_z("Yol: zebra geçidi (y ekseni boyunca giden yolda)")
def z_gecit(g):
    for i in range(4):
        x = -0.78 + i * 0.52
        _ust(g, x - 0.15, x + 0.15, -1.25, 1.25, YOL_Z + 0.025, SILME)


def _sari_cizgi(g, c, u0, u1):
    """Yol kenarında çift sarı çizgi (bordürün hemen önünde, asfaltta)."""
    for o in (-0.7, -0.82):
        levha(g, [c.p(u0, o, YOL_Z + 0.02), c.p(u1, o, YOL_Z + 0.02), c.p(u1, o - 0.06, YOL_Z + 0.02), c.p(u0, o - 0.06, YOL_Z + 0.02)], PM("Iz", "#f0c94a"))


@parca("zemin_kenar", "Yol kenarı: kaldırım şeridi + bordür + çift sarı çizgi")
def z_bordur(g):
    _kaldirim_seridi(g, on(), 0.6, HUCRE - 0.6)
    _sari_cizgi(g, on(), 0.6, HUCRE - 0.6)


@parca("zemin_kenar", "Yol kenarı: şeridin köşeye uzayan başı")
def z_bordur_uc(g):
    _kaldirim_seridi(g, on(), 0.0, 0.6)
    _sari_cizgi(g, on(), 0.0, 0.6)


@parca("zemin_kenar", "Yol kenarı: şeridin köşeye uzayan sonu")
def z_bordur_son(g):
    _kaldirim_seridi(g, on(), HUCRE - 0.6, HUCRE)
    _sari_cizgi(g, on(), HUCRE - 0.6, HUCRE)


# ================================== GERÇEKÇİ AĞAÇLAR (v7) ==================================
# Sanat stili aynı (pastel renk, kalem kontur, kâğıt); ağaçların FORMU gerçekçi:
#   • gövde hafif eğri, kökte genişler; ana dallar gövdeden ayrılıp taça girer, ince dallar her yaprak kümesine uzanır
#     (kışın tam dal iskeleti kalır);
#   • taç tek top değil: dallara oturan 12–30 düzensiz, kabarık yaprak kümesi; kümenin üstü açık sarımsı, yanı orta,
#     gölge yanı mavimsi, altı koyu ton (aynı ağaçta dört ton: roller Y, y, a, m), üstünde birkaç yaprak lekesi;
#   • kontur yalnız büyük dış kümelerin ve gövdenin ters kabuğunda (Pastel_Kabuk_*); yaprak ve gövde yüzeyleri "iz"
#     malzemelidir (Pastel_Yaprak_Dok_Iz_*, Pastel_Govde_Iz_*): iç küme sınırlarına kalem çizgisi çekilmez;
#   • köşeler kaynaklıdır (g.vn): Unity'de yumuşak gölgelenir (düz yüzlü low-poly görünüm yok);
#   • yaprak döken türlerin yaprakları "Pastel_Yaprak_Dok_…" adlıdır: oyunda kış gelince gölgelendirici bunları kırpar
#     (_PastelKis), dallardaki kar şeritleri (Pastel_DalKar_…) yalnız o zaman görünür. Önizlemede kış = yaprak rengi 7.
#   • üç ayrıntı düzeyi: yakın (≤ ~1500 üçgen), hafif (≤ ~250), uzak (≤ 40; JSON "uzaklar").
# Malzeme adları mevsim / kar koduyla uyumlu: etiket hâlâ "Yaprak" (City3D.SehirMalzemesi adın ikinci parçasına bakar).

YD = ROL("Yaprak_Dok_Iz", "Y", "#8fbf7a")          # yaprak döken: orta ton
YD_KOYU = ROL("Yaprak_Dok_Iz", "y", "#7aa868")     # küme altı
YD_ACIK = ROL("Yaprak_Dok_Iz", "a", "#b4d283")     # ışık alan üst (sarımsı)
YD_MAVI = ROL("Yaprak_Dok_Iz", "m", "#6f9f84")     # gölge yanı (mavimsi)
KABUK_DOK = ROL("Kabuk_Dok", "k", "#3e4a3a")       # yaprak döken tacın dış hattı (kışın gizlenir)
YS = ROL("Yaprak_Iz", "Y", "#8fbf7a")              # her dem yeşil çalı (şimşir, lavanta): kışın da durur
YS_KOYU = ROL("Yaprak_Iz", "y", "#7aa868")
YS_ACIK = ROL("Yaprak_Iz", "a", "#b4d283")
YS_MAVI = ROL("Yaprak_Iz", "m", "#6f9f84")
IGNE = [PM("Igne_Iz", h) for h in ("#6f9c7c", "#5c8a72", "#86ad82", "#557f73")]     # iğne yapraklı: orta, koyu, açık, mavimsi
KABUK_IGNE = PM("Kabuk", "#3d4f48")
KABUK_GOVDE = PM("Kabuk", "#5b4a44")
DAL_KAR = PM("DalKar_Iz", "#f4f6fa")
GOVDE_TON = {"kahve": ("#8a6a55", "#74574a"), "koyu": ("#77605a", "#614d4a"), "cinar": ("#b9b39c", "#a3977f"), "hus": ("#f1ede2", "#3f3d42"),
             "gri": ("#8f8a84", "#77716c")}
TON_DOK = (YD, YD_KOYU, YD_ACIK, YD_MAVI)
TON_SABIT = (YS, YS_KOYU, YS_ACIK, YS_MAVI)


def _govde_mat(ton, koyu_mu=False):
    return PM("Govde_Iz", GOVDE_TON[ton][1 if koyu_mu else 0])


_OKTA = None


def _okta(detay):
    """Birim küre ağı: 0 → sekizyüzlü (8 üçgen), 1 → bir kez bölünmüş (32), 2 → iki kez (128)."""
    global _OKTA
    if _OKTA is None:
        _OKTA = {}
        v = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
        f = [(0, 2, 4), (2, 1, 4), (1, 3, 4), (3, 0, 4), (2, 0, 5), (1, 2, 5), (3, 1, 5), (0, 3, 5)]
        _OKTA[0] = (list(v), list(f))
        for d in (1, 2):
            v = list(v)
            kenar = {}

            def orta(i, j):
                k = (min(i, j), max(i, j))
                if k not in kenar:
                    kenar[k] = len(v)
                    v.append(B.norm(B.mul(B.add(v[i], v[j]), 0.5)))
                return kenar[k]
            f2 = []
            for a, b, c in f:
                ab, bc, ca = orta(a, b), orta(b, c), orta(c, a)
                f2 += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
            f = f2
            _OKTA[d] = (list(v), list(f))
    return _OKTA[detay]


def _kume(g, c, rx, ry, rz, rnd, tonlar, detay=1, kabuk=None, puruz=0.2, leke=2, alt=0.62, ana=0):
    """Yaprak kümesi: kabarık, düzensiz küre (alt yarısı basık), köşeleri kaynaklı (yumuşak gölgelenir). Küme tek ana
    tondadır (ana: tonlar sırası), altı koyu ton. leke: yüzeyin hemen üstünde küçük yaprak lekeleri (kalem darbesi gibi;
    ana tondan açık / koyu). kabuk: verilirse biraz büyük ters kopya (dış hat çizgisi)."""
    v, f = _okta(detay)
    faz = [rnd.uniform(0, 6.28) for _ in range(3)]
    pts = []
    for p in v:
        k = 1.0 + rnd.uniform(-puruz, puruz) + 0.13 * math.sin(3.1 * p[0] + faz[0]) * math.cos(2.7 * p[1] + faz[1]) + 0.08 * math.sin(4.3 * p[2] + faz[2])
        z = p[2] * (alt if p[2] < 0 else 1.0)
        pts.append((c[0] + p[0] * rx * k, c[1] + p[1] * ry * k, c[2] + z * rz * k))
    g.sid += 1
    ust = []
    for a, b, d in f:
        P = [pts[a], pts[b], pts[d]]
        n = B.norm(B.newell(P))
        g.yuz(P, tonlar[1] if n[2] < -0.35 else tonlar[ana])
        if n[2] > -0.1:
            ust.append((P, n))
    acik, koyu_t = (tonlar[2] if ana != 2 else tonlar[0]), (tonlar[3] if ana != 3 else tonlar[1])
    for i in range(leke if ust else 0):                        # yaprak lekeleri: küçük üçgenler, yüzeyin 2,5 cm üstünde
        P, n = ust[rnd.randrange(len(ust))]
        w0, w1 = rnd.uniform(0.15, 0.6), rnd.uniform(0.15, 0.4)
        o = tuple(P[0][q] * (1 - w0 - w1) + P[1][q] * w0 + P[2][q] * w1 for q in range(3))
        u = B.norm(B.sub(P[1 + i % 2], P[0]))
        w = B.cross(n, u)
        s = rnd.uniform(0.09, 0.16)
        q0 = B.add(o, B.mul(n, 0.025))
        q1 = B.add(B.add(q0, B.mul(u, s * 1.6)), B.mul(w, s * 0.3))
        q2 = B.add(B.add(q0, B.mul(u, s * 0.6)), B.mul(w, s * 0.9))
        g.yuz([q0, q1, q2], acik if (n[2] > 0.3) == (i % 3 != 0) else koyu_t)
    if kabuk:
        t = 0.028
        for a, b, d in f:
            P = [pts[d], pts[b], pts[a]]
            g.yuz([(c[0] + (p[0] - c[0]) * (1 + t / max(rx, 0.2)), c[1] + (p[1] - c[1]) * (1 + t / max(ry, 0.2)), c[2] + (p[2] - c[2]) * (1 + t / max(rz, 0.2))) for p in P], kabuk)
    g.sid += 1


def _filiz(g, c, boy, rnd, mat, n=3):
    """Küme merkezinden yukarı / dışa açılan ince filiz dalları (iki yüzlü, sivrilen şerit): kışın ince dal dokusu."""
    g.sid += 1
    for i in range(n):
        a, e = rnd.uniform(0, 6.28), rnd.uniform(0.35, 1.25)
        L = boy * rnd.uniform(0.7, 1.15)
        d = (math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e))
        te = (-math.sin(a) * 0.022, math.cos(a) * 0.022, 0.0)
        orta = B.add(B.add(c, B.mul(d, L * 0.55)), (0, 0, L * 0.08))
        uc = B.add(c, B.mul(d, L))
        for pts in ([B.sub(c, te), B.add(c, te), B.add(orta, B.mul(te, 0.6)), B.sub(orta, B.mul(te, 0.6))], [B.sub(orta, B.mul(te, 0.6)), B.add(orta, B.mul(te, 0.6)), uc]):
            g.yuz(pts, mat)
            g.yuz(pts[::-1], mat)
        if i == 0:
            yan = B.add(orta, (math.cos(a + 1.1) * L * 0.4, math.sin(a + 1.1) * L * 0.4, L * 0.3))      # yan filiz
            pts = [B.sub(orta, B.mul(te, 0.5)), B.add(orta, B.mul(te, 0.5)), yan]
            g.yuz(pts, mat)
            g.yuz(pts[::-1], mat)
    g.sid += 1


def _boru(g, yol, yaricap, kenar, mat, kapak=False, faz=0.0, kabuk=None, mat2=None, rnd=None):
    """Yol boyunca (nokta listesi) halka halka boru. yaricap: her nokta için sayı ya da (kenar uzunluğunda) liste.
    Son yarıçap 0,02'den küçükse uç sivrilir. kabuk: ters kopya (dış hat). mat2 + rnd: bazı yüzler ikinci tonda."""
    halkalar = []
    n = len(yol)
    for i, p in enumerate(yol):
        d = B.norm(B.sub(yol[min(i + 1, n - 1)], yol[max(i - 1, 0)]))
        ref = (0, 0, 1) if abs(d[2]) < 0.9 else (1, 0, 0)
        a = B.norm(B.cross(ref, d))
        b = B.cross(d, a)
        if abs(d[2]) >= 0.9:                                  # dik gövdede halkalar x, y eksenine hizalı (burulma olmasın)
            a, b = (1, 0, 0), (0, 1, 0)
        r = yaricap[i]
        h = []
        for k in range(kenar):
            t = 2 * math.pi * k / kenar + faz
            rk = r[k] if isinstance(r, (list, tuple)) else r
            h.append(B.add(p, B.add(B.mul(a, math.cos(t) * rk), B.mul(b, math.sin(t) * rk))))
        halkalar.append(h)
    g.sid += 1
    for i in range(n - 1):
        for k in range(kenar):
            j = (k + 1) % kenar
            m = mat2 if (mat2 and rnd and rnd.random() < 0.3) else mat
            g.yuz([halkalar[i][k], halkalar[i][j], halkalar[i + 1][j], halkalar[i + 1][k]], m)
    if kapak:
        g.yuz(halkalar[-1], mat)
    if kabuk:
        t = 0.045
        for i in range(n - 1):
            for k in range(kenar):
                j = (k + 1) % kenar
                q = []
                for (hi, kk) in ((i + 1, k), (i + 1, j), (i, j), (i, k)):
                    p, o = halkalar[hi][kk], yol[hi]
                    d = B.sub(p, o)
                    L = math.sqrt(B.dot(d, d)) or 1.0
                    q.append(B.add(p, B.mul(d, t / L)))
                g.yuz(q, kabuk)
    g.sid += 1


def _egri(p0, p1, rnd, sapma=0.12, sark=0.0, n=3):
    """p0 → p1 arası hafif eğri yol (n nokta): ortası rastgele yana kayar; sark > 0 ise uç aşağı sarkar, < 0 yukarı kıvrılır."""
    d = B.sub(p1, p0)
    L = math.sqrt(B.dot(d, d))
    yol = []
    kay = (rnd.uniform(-sapma, sapma) * L, rnd.uniform(-sapma, sapma) * L, rnd.uniform(0.0, sapma) * L)
    for i in range(n):
        t = i / float(n - 1)
        w = math.sin(math.pi * t)
        yol.append((p0[0] + d[0] * t + kay[0] * w, p0[1] + d[1] * t + kay[1] * w, p0[2] + d[2] * t + kay[2] * w - sark * L * t * t + sark * L * t))
    yol[-1] = p1
    return yol


# Tür tablosu. govde: (boy, yarıçap, ton) · tac: (merkezin gövde tepesinden yüksekliği, rx, ry, rz) · kume: (sayı, yarıçap alt, üst)
# sark: dal uçları sarkar (söğüt, huş) · bosluk: 0…1 kümeler arası boşluk (seyrek taç) · alt_dal: en alt dalın gövdedeki yeri (0…1)
AGAC_TUR = {
    "cinar":   dict(ad="Londra çınarı (benekli gövde, geniş yayvan taç)", govde=(3.5, 0.2, "cinar"), tac=(1.5, 2.35, 2.3, 1.55), kume=(20, 0.72, 1.0), sark=0.0, bosluk=0.25, alt_dal=0.72),
    "mese":    dict(ad="Meşe (kalın gövde, düzensiz geniş taç)", govde=(2.3, 0.3, "koyu"), tac=(1.5, 2.6, 2.45, 1.7), kume=(24, 0.7, 1.05), sark=0.0, bosluk=0.35, alt_dal=0.6),
    "ihlamur": dict(ad="Ihlamur (uzun oval taç)", govde=(2.3, 0.19, "kahve"), tac=(2.3, 1.55, 1.5, 2.45), kume=(20, 0.6, 0.85), sark=0.0, bosluk=0.15, alt_dal=0.8),
    "hus":     dict(ad="Huş (beyaz ince gövde, seyrek sarkık taç)", govde=(3.2, 0.11, "hus"), tac=(1.3, 1.25, 1.2, 1.9), kume=(13, 0.42, 0.62), sark=0.35, bosluk=0.6, alt_dal=0.6),
    "kavak":   dict(ad="Kavak (dar, uzun)", govde=(1.1, 0.17, "gri"), tac=(3.1, 0.8, 0.78, 3.2), kume=(17, 0.5, 0.68), sark=-0.2, bosluk=0.1, alt_dal=0.9),
    "kestane": dict(ad="At kestanesi (yuvarlak, yoğun taç)", govde=(2.4, 0.25, "kahve"), tac=(1.75, 2.05, 2.0, 1.85), kume=(22, 0.72, 0.98), sark=0.0, bosluk=0.08, alt_dal=0.75),
    "sogut":   dict(ad="Söğüt (sarkık)", govde=(2.3, 0.27, "koyu"), tac=(1.1, 2.2, 2.15, 1.3), kume=(15, 0.62, 0.85), sark=0.3, bosluk=0.3, alt_dal=0.7),
    "meyve":   dict(ad="Elma ağacı (kırmızı meyveli)", govde=(1.5, 0.15, "kahve"), tac=(0.95, 1.45, 1.4, 1.0), kume=(13, 0.5, 0.7), sark=0.0, bosluk=0.3, alt_dal=0.65),
    "genc":    dict(ad="Genç sokak ağacı (koruma kafesli)", govde=(2.1, 0.06, "kahve"), tac=(0.85, 0.8, 0.78, 1.0), kume=(9, 0.36, 0.5), sark=0.0, bosluk=0.3, alt_dal=0.85),
}


def _iskelet(tur, tohum):
    """Ağacın iskeleti (türe ve tohuma göre, ayrıntı düzeyinden bağımsız: üç düzey aynı ağacı çizer).
    Dönen: gövde yolu, gövde yarıçapları, kümeler [(merkez, yarıçap, dış mı)], dallar [(yol, r0, r1, derinlik)]."""
    T = AGAC_TUR[tur]
    rnd = random.Random(tohum * 7919 + sum(ord(ch) for ch in tur))
    boy, r, ton = T["govde"]
    boy *= rnd.uniform(0.92, 1.1)
    egx, egy = rnd.uniform(-0.22, 0.22) * min(1.0, r / 0.2), rnd.uniform(-0.18, 0.18) * min(1.0, r / 0.2)
    govde = [(0.0, 0.0, 0.0), (0.0, 0.0, 0.3), (egx * 0.5, egy * 0.5, boy * 0.42), (egx * 0.2, egy * 0.9, boy * 0.75), (egx * 0.6, egy * 0.5, boy)]
    tepe = govde[-1]
    dz, rx, ry, rz = T["tac"]
    k = rnd.uniform(0.92, 1.08)
    rx, ry, rz = rx * k, ry * k * rnd.uniform(0.93, 1.07), rz * rnd.uniform(0.93, 1.07)
    mrk = (tepe[0] + rnd.uniform(-0.15, 0.15), tepe[1] + rnd.uniform(-0.15, 0.15), boy + dz)
    n, k0, k1 = T["kume"]
    kumeler = []
    alt_sinir = boy * T["alt_dal"]
    for i in range(n):
        # altın açı sarmalı: taç elipsoidinin kabuğuna yakın, düzensiz
        f = (i + 0.5) / n
        zz = 1 - 1.75 * f                                        # 1 (tepe) … −0.75 (alt): tacın dibi açık kalır
        aci = i * 2.39996 + rnd.uniform(-0.35, 0.35)
        rr = math.sqrt(max(0.0, 1 - zz * zz))
        kat = rnd.uniform(0.6, 0.86) if i % 4 else rnd.uniform(0.2, 0.4)       # dörtte biri içeride: taç dolu, ama boşluklu
        c = (mrk[0] + math.cos(aci) * rr * rx * kat, mrk[1] + math.sin(aci) * rr * ry * kat, max(alt_sinir + 0.3, mrk[2] + zz * rz * kat))
        kr = rnd.uniform(k0, k1) * (0.92 - 0.3 * T["bosluk"]) * (1.1 if kat > 0.5 else 0.95)
        kumeler.append((c, kr, kat > 0.5))
    # dallar: her küme merkezi, gövdeye daha yakın bağlanmış en yakın düğüme bağlanır (ağaç gibi dallanır)
    dugum = [(govde[3], 0), (tepe, 0)]
    dallar = []
    sira = sorted(range(n), key=lambda i: B.dot(B.sub(kumeler[i][0], tepe), B.sub(kumeler[i][0], tepe)))
    for i in sira:
        c = kumeler[i][0]
        en, eu = None, 1e9
        for p, der in dugum:
            d = B.sub(c, p)
            u = math.sqrt(B.dot(d, d)) * (1.0 + 0.25 * der)
            if u < eu and c[2] >= p[2] - 0.6:
                en, eu = (p, der), u
        if en is None:
            en = (tepe, 0)
        p, der = en
        der += 1
        r0 = r * (0.56 if der == 1 else 0.32 if der == 2 else 0.2)
        yol = _egri(p, c, rnd, 0.1, (T["sark"] if der > 1 else T["sark"] * 0.4) or -0.12)
        dallar.append((yol, max(0.03, r0), max(0.016, r0 * 0.42), der))
        dugum.append((c, der))
        dugum.append((yol[1], der))
    rg = [[r * (1.65 + 0.4 * math.sin(3 * (2 * math.pi * q / 6) + tohum)) for q in range(6)], r * 1.12, r, r * 0.86, r * 0.7]
    return dict(govde=govde, rg=rg, kumeler=kumeler, dallar=dallar, mrk=mrk, r3=(rx, ry, rz), ton=ton, boy=boy, r=r, rnd=rnd, T=T)


def _kabuk_izi(g, S, tur, rnd):
    """Kabuk dokusu: gövdede kısa düşey kalem taramaları; çınarda benekler, huşta siyah yatay izler."""
    govde, r, ton = S["govde"], S["r"], S["ton"]
    koyu_m = _govde_mat(ton, True)

    def nokta(t, aci, tasma=0.012):
        # gövde yolunda t (0…1) konumu + açıdaki yüzey noktası
        f = 1 + t * (len(govde) - 2)
        i = min(int(f), len(govde) - 2)
        u = f - i
        p = tuple(govde[i][k] + (govde[i + 1][k] - govde[i][k]) * u for k in range(3))
        rr = (r * 1.12 if i < 2 else r * 0.95) + tasma
        return (p[0] + math.cos(aci) * rr, p[1] + math.sin(aci) * rr, p[2]), (math.cos(aci), math.sin(aci), 0.0)
    if tur == "cinar":
        benek = (PM("Govde_Iz", "#ddd7bd"), PM("Govde_Iz", "#8f9a7a"), PM("Govde_Iz", "#a18a74"))
        for i in range(10):
            aci, t = rnd.uniform(0, 6.28), rnd.uniform(0.05, 0.95)
            p, n = nokta(t, aci)
            te = (-n[1], n[0], 0)
            w, h = rnd.uniform(0.06, 0.1), rnd.uniform(0.14, 0.3)
            levha(g, [B.add(p, B.mul(te, -w)), B.add(B.add(p, B.mul(te, w * 0.8)), (0, 0, 0.04)), B.add(B.add(p, B.mul(te, w)), (0, 0, h)), B.add(B.add(p, B.mul(te, -w * 0.7)), (0, 0, h * 0.85))], benek[i % 3])
    elif tur == "hus":
        for i in range(9):
            aci, t = rnd.uniform(0, 6.28), rnd.uniform(0.05, 0.98)
            p, n = nokta(t, aci)
            te = (-n[1], n[0], 0)
            w = rnd.uniform(0.04, 0.08)
            levha(g, [B.add(p, B.mul(te, -w)), B.add(p, B.mul(te, w)), B.add(B.add(p, B.mul(te, w * 0.7)), (0, 0, 0.035)), B.add(B.add(p, B.mul(te, -w * 0.8)), (0, 0, 0.03))], koyu_m)
    else:
        for i in range(8):
            aci, t = rnd.uniform(0, 6.28), rnd.uniform(0.02, 0.85)
            p, n = nokta(t, aci)
            te = (-n[1], n[0], 0)
            h = rnd.uniform(0.18, 0.4)
            levha(g, [B.add(p, B.mul(te, -0.012)), B.add(p, B.mul(te, 0.012)), B.add(B.add(p, B.mul(te, 0.012)), (0, 0, h)), B.add(B.add(p, B.mul(te, -0.012)), (0, 0, h))], koyu_m)


YD_KALEM = ROL("Yaprak_Dok_Iz", "k", "#3e4a3a")      # yaprağın kalem kenarı (yaprak renginin koyusu; kışın kırpılır)
YS_KALEM = ROL("Yaprak_Iz", "k", "#3e4a3a")

# Yaprak biçimleri (v8): (t, w) çiftleri; t = 0 sap … 1 uç, w = yarım genişlik (boya oranla). Ayna simetrik çizilir.
YAPRAK_BICIM = {
    "oval":  [(0.0, 0.0), (0.32, 0.3), (0.68, 0.17), (1.0, 0.0)],                                  # kestane, meyve, genç
    "lob":   [(0.0, 0.0), (0.42, 0.5), (0.56, 0.2), (1.0, 0.0)],                                    # çınar (loblu)
    "mese":  [(0.0, 0.0), (0.3, 0.3), (0.5, 0.14), (0.72, 0.26), (1.0, 0.0)],
    "kalp":  [(0.0, 0.0), (0.14, 0.4), (0.6, 0.24), (1.0, 0.0)],                                   # ıhlamur
    "kucuk": [(0.0, 0.0), (0.35, 0.4), (1.0, 0.0)],                                                # huş
    "ince":  [(0.0, 0.0), (0.4, 0.15), (1.0, 0.0)],                                                # kavak, söğüt
}
# tür → (biçim, yaprak boyu m, sayı çarpanı, sarkma 0…1)
YAPRAK_TUR = {"cinar": ("lob", 0.34, 1.0, 0.0), "mese": ("mese", 0.3, 1.0, 0.0), "ihlamur": ("kalp", 0.28, 1.1, 0.05),
              "hus": ("kucuk", 0.2, 1.15, 0.45), "kavak": ("ince", 0.26, 1.05, 0.0), "kestane": ("oval", 0.32, 1.05, 0.05),
              "sogut": ("ince", 0.36, 1.0, 0.85), "meyve": ("oval", 0.24, 1.0, 0.05), "genc": ("oval", 0.22, 1.0, 0.0)}
GUNES = B.norm((0.45, -0.55, 0.75))     # önizlemenin ve oyunun ana ışığına yakın: yaprak tonu bu yöne göre seçilir


def _yaprak_ciz(g, p, nl, t, boy, bicim, mat, kalem=None):
    """Tek yaprak: p sap noktası, nl yaprak yüzünün normali (dışa), t yaprak ekseni (sap → uç; nl'ye dik).
    kalem: verilirse yaprağın 1-2 cm arkasında biraz büyük koyu kopya (kalem kenarı gibi okunur)."""
    w = B.norm(B.cross(nl, t))
    P = bicim
    sag = [B.add(B.add(p, B.mul(t, a * boy)), B.mul(w, b * boy)) for a, b in P]
    sol = [B.add(B.add(p, B.mul(t, a * boy)), B.mul(w, -b * boy)) for a, b in P[1:-1]]
    pts = sag + sol[::-1]
    if B.dot(B.newell(pts), nl) < 0:
        pts = pts[::-1]
    g.yuz(pts, mat)
    if kalem:
        geri = B.mul(nl, -0.025)
        k = 1.25
        # dört köşeli koyu baklava: sap, sağ en geniş, uç, sol en geniş
        en = max(P, key=lambda q: q[1])
        q0 = B.add(B.add(p, B.mul(t, -0.05 * boy)), geri)
        q1 = B.add(B.add(B.add(p, B.mul(t, en[0] * boy)), B.mul(w, en[1] * boy * k + 0.012)), geri)
        q2 = B.add(B.add(p, B.mul(t, boy * 1.07)), geri)
        q3 = B.add(B.add(B.add(p, B.mul(t, en[0] * boy)), B.mul(w, -en[1] * boy * k - 0.012)), geri)
        kp = [q0, q1, q2, q3]
        if B.dot(B.newell(kp), nl) < 0:
            kp = kp[::-1]
        g.yuz(kp, kalem)


def _yaprak_kumesi(g, c, rx, ry, rz, rnd, tonlar, kalem, tur, n, olcek=1.0, mrk=None, alt=0.62, ic_sinir=0.58):
    """Kümenin yüzeyine n tek tek yaprak dağıtır (v8: "yaprak yaprak"). Yapraklar dışa ve biraz yukarı bakar, ekseni
    dışa / aşağı sarkar; tonu güneşe bakışa göre: ışık yanı açık sarımsı (a), orta (Y), gölge yanı mavimsi (m),
    altı koyu (y). Biraz içeride kalanlar koyu: taç derinlikli okunur."""
    bicim_ad, boy0, _, sark = YAPRAK_TUR.get(tur, YAPRAK_TUR["kestane"])
    bicim = YAPRAK_BICIM[bicim_ad]
    g.sid += 1
    for i in range(n):
        # yüzeyde düzgün dağılım (alt yarı seyrek)
        while True:
            z = rnd.uniform(-1.0, 1.0)
            if z >= -0.15 or rnd.random() < 0.55:
                break
        a = rnd.uniform(0, 6.283)
        s = math.sqrt(max(0.0, 1 - z * z))
        d = (s * math.cos(a), s * math.sin(a), z)
        u = 1.06 - (1.06 - ic_sinir) * rnd.random() ** 1.6      # katmanlı: çoğu dışta, bir kısmı derinde
        p = (c[0] + d[0] * rx * u, c[1] + d[1] * ry * u, c[2] + d[2] * rz * u * (alt if z < 0 else 1.0))
        nrm = B.norm((d[0] / max(rx, 0.1), d[1] / max(ry, 0.1), d[2] / max(rz, 0.1)))
        # taç ölçeğindeki normal (mrk'ten dışa): ton bunun güneşe bakışıyla seçilir
        tn = B.norm(B.sub(p, mrk)) if mrk else nrm
        isik = B.dot(B.norm(B.add(B.mul(tn, 0.7), B.mul(nrm, 0.3))), GUNES)
        r = rnd.uniform(-0.28, 0.28)
        derin = (1.06 - u) / max(0.05, 1.06 - ic_sinir)          # 0 dış … 1 en iç
        isik -= derin * 0.7
        if derin > 0.6 or (tn[2] < -0.5 and rnd.random() < 0.6):
            mat = tonlar[1]
        elif isik + r > 0.62:
            mat = tonlar[2]
        elif isik + r < 0.12:
            mat = tonlar[3]
        else:
            mat = tonlar[0]
        rast = B.norm((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
        nl = B.norm(B.add(B.add(B.mul(nrm, 0.9), B.mul(rast, 0.35)), (0, 0, 0.25)))
        eks = B.add(B.add(B.mul(nrm, 0.6), (0, 0, -0.35 - 1.6 * sark)), B.mul(rast, 0.6))
        eks = B.sub(eks, B.mul(nl, B.dot(eks, nl)))
        if B.dot(eks, eks) < 1e-6:
            eks = B.cross(nl, (1, 0, 0))
        t = B.norm(eks)
        boy = boy0 * olcek * rnd.uniform(0.8, 1.2)
        _yaprak_ciz(g, B.sub(p, B.mul(t, boy * 0.35)), nl, t, boy, bicim, mat, kalem if derin < 0.2 else None)
    g.sid += 1


def _sogut_sarkit(g, c, kr, rnd, tonlar, kalem, n_serit, olcek=1.0):
    """Söğüdün sarkan yaprak perdeleri: dış kümeden yere inen ince dalcık boyunca dizili ince uzun yapraklar."""
    bicim = YAPRAK_BICIM["ince"]
    for j in range(n_serit):
        a = rnd.uniform(0, 6.283)
        x, y = c[0] + math.cos(a) * kr * 0.85, c[1] + math.sin(a) * kr * 0.85
        L = rnd.uniform(1.0, 2.1)
        ust = (x, y, c[2] - kr * 0.1)
        dis = (math.cos(a), math.sin(a), 0.0)
        adim = 0.16 * olcek
        k = int(L / adim)
        for q in range(k):
            f = q / float(max(1, k))
            p = (ust[0] + dis[0] * 0.18 * f, ust[1] + dis[1] * 0.18 * f, ust[2] - L * f)
            yan = 1 if q % 2 else -1
            nl = B.norm((dis[0] + rnd.uniform(-0.4, 0.4), dis[1] + rnd.uniform(-0.4, 0.4), 0.15))
            w = B.cross(nl, (0, 0, 1))
            t = B.norm(B.add((0, 0, -1), B.mul(w, 0.45 * yan)))
            mat = tonlar[1] if rnd.random() < 0.25 else tonlar[2] if B.dot(nl, GUNES) > 0.3 else tonlar[0]
            _yaprak_ciz(g, p, nl, t, 0.3 * olcek * rnd.uniform(0.85, 1.15), bicim, mat, kalem)


def _tac_yapraklari(g, S, tur, rnd, duzey, tonlar=None, kalem=None):
    """v8 taç: tek tek yapraklar (dışta ışığa göre tonlu, derindekiler koyu: taç derinlikli okunur, aralardan dal ve gök
    görünür). Hafif düzeyde kümelerin içine koyu, kalemsiz bir hacim de konur (uzaktan boşluklu görünmesin).
    duzey 0: ~600–900 yaprak, kalem kenarlı. duzey 1: ~140 iri yaprak, kenarsız."""
    tonlar = tonlar or TON_DOK
    kalem = kalem if kalem is not None else YD_KALEM
    _, boy0, carp, _ = YAPRAK_TUR.get(tur, YAPRAK_TUR["kestane"])
    kumeler = S["kumeler"]
    toplam_alan = sum(kr * kr for _, kr, _ in kumeler) or 1.0
    hedef = (1150 if duzey == 0 else 150) * carp
    olcek = 0.85 if duzey == 0 else 2.0
    koyu = (tonlar[1],) * 4
    for c, kr, dis in kumeler:
        n = max(6, int(hedef * kr * kr / toplam_alan))
        ic = 0.8
        if duzey:       # uzaktan boşluklar kaybolsun: yalnız hafif düzeyde koyu iç hacim
            _kume(g, c, kr * ic, kr * ic, kr * ic * 0.8, rnd, (tonlar[1], tonlar[1], tonlar[1], tonlar[1]), detay=0, puruz=0.15, leke=0, ana=0)
        _yaprak_kumesi(g, c, kr * 1.05, kr * 1.05, kr * 0.88, rnd, tonlar, kalem if duzey == 0 else None, tur, n, olcek, S["mrk"])
    if tur == "sogut":
        for c, kr, dis in kumeler:
            if dis:
                _sogut_sarkit(g, c, kr, rnd, tonlar, kalem if duzey == 0 else None, 4 if duzey == 0 else 1, olcek=1.0 if duzey == 0 else 1.8)


def _agac7(g, tur, tohum, duzey=0, yaprak=True, ek=None):
    """Gerçekçi ağaç. duzey: 0 yakın · 1 hafif (birkaç küme + gövde + ana dallar) · 2 uzak (gövde + iki küme)."""
    S = _iskelet(tur, tohum)
    rnd = random.Random(tohum * 31 + 5)
    gm = _govde_mat(S["ton"])
    govde, rg = S["govde"], S["rg"]
    if duzey == 2:
        _boru(g, [govde[0], govde[-1]], [S["r"] * 1.3, S["r"] * 0.8], 4, gm, kapak=False, faz=0.785)
        if yaprak:
            rx, ry, rz = S["r3"]
            _kume(g, S["mrk"], rx * 0.92, ry * 0.92, rz * 0.9, rnd, TON_DOK, detay=0, puruz=0.1, leke=0, alt=0.8)
            _kume(g, (S["mrk"][0] + rx * 0.2, S["mrk"][1] - ry * 0.15, S["mrk"][2] + rz * 0.25), rx * 0.7, ry * 0.7, rz * 0.7, rnd, TON_DOK, detay=0, puruz=0.1, leke=0, alt=0.8)
        yumusat(g)
        return
    if duzey == 1:
        _boru(g, [govde[0], govde[2], govde[-1]], [S["r"] * 1.5, S["r"], S["r"] * 0.75], 4, gm, kapak=False, faz=0.785)
        for yol, r0, r1, der in S["dallar"]:
            if der == 1:
                _boru(g, [yol[0], yol[-1]], [r0, r1], 3, gm, kapak=False)
        if yaprak:
            _tac_yapraklari(g, S, tur, rnd, 1)
        yumusat(g)
        return
    # ---- yakın görünüm
    _boru(g, govde, rg, 6, gm, kapak=False, kabuk=KABUK_GOVDE)
    for yol, r0, r1, der in S["dallar"]:
        if der == 1:
            _boru(g, yol, [r0, (r0 + r1) / 2, r1], 4, gm, kapak=False)
            a, b = yol[1], yol[2]                               # dalın üstünde kar şeridi (yalnız kışın görünür)
            d = B.norm(B.sub(b, a))
            yan = B.norm(B.cross(d, (0, 0, 1))) if abs(d[2]) < 0.95 else (1, 0, 0)
            w = r0 * 0.8
            ust = (0, 0, (r0 + r1) / 2 + 0.015)
            levha(g, [B.add(B.add(a, B.mul(yan, -w)), ust), B.add(B.add(a, B.mul(yan, w)), ust), B.add(B.add(b, B.mul(yan, w * 0.6)), ust), B.add(B.add(b, B.mul(yan, -w * 0.6)), ust)], DAL_KAR)
        else:
            _boru(g, yol, [r0, r0 * 0.75, r1], 3, gm, kapak=False)
    _kabuk_izi(g, S, tur, rnd)
    for c, kr, dis in S["kumeler"]:
        if dis:
            _filiz(g, c, kr * 1.05, rnd, gm, 2)
    if yaprak:
        _tac_yapraklari(g, S, tur, rnd, 0)
    if ek:
        ek(g, S, rnd)
    yumusat(g)


def yumusat(g):
    """Köşe kaynağı: yaprak, gövde, iğne ve kabuk yüzlerinin aynı konumdaki köşelerine ortak (alan ağırlıklı) normal
    verir (g.vn). Unity'ye bu köşeler paylaşılarak yazılır (yumuşak gölgeleme); önizleme de bu normallerle boyar."""
    etiket = ("_Yaprak_", "_Govde_", "_Igne_", "_Kabuk_")
    top_ = {}
    yuz_n = []
    for f, mi in zip(g.f, g.fm):
        m = g.mats[mi]
        if not any(e in m for e in etiket):
            yuz_n.append(None)
            continue
        n = B.newell([g.v[i] for i in f])
        yuz_n.append(n)
        for i in f:
            k = (m.split("_")[1] == "Kabuk", round(g.v[i][0] * 500), round(g.v[i][1] * 500), round(g.v[i][2] * 500))
            a = top_.get(k)
            top_[k] = n if a is None else B.add(a, n)
    vn = [None] * len(g.v)
    for f, mi, n in zip(g.f, g.fm, yuz_n):
        if n is None:
            continue
        m = g.mats[mi]
        for i in f:
            k = (m.split("_")[1] == "Kabuk", round(g.v[i][0] * 500), round(g.v[i][1] * 500), round(g.v[i][2] * 500))
            vn[i] = B.norm(top_[k])
    g.vn = vn


def _ek_meyve(renk):
    def ek(g, S, rnd):
        mm = PM("Meyve", renk)
        for c, kr, dis in S["kumeler"]:
            if not dis:
                continue
            for j in range(2):
                a, b = rnd.uniform(0, 6.28), rnd.uniform(-0.5, 0.6)
                meyve(g, c[0] + math.cos(a) * kr * 0.95 * math.cos(b), c[1] + math.sin(a) * kr * 0.95 * math.cos(b), c[2] + math.sin(b) * kr * 0.6, 0.09, mm, n=5)
    return ek


def _ek_cicek(g, S, rnd):
    """At kestanesinin beyaz çiçek salkımları (mum gibi dik koniler), dış kümelerin üstünde."""
    bm = PM("Cicek_Iz", "#fbf3ea")
    for c, kr, dis in S["kumeler"]:
        if not dis or c[2] < S["mrk"][2] - 0.3:
            continue
        for j in range(2):
            a = rnd.uniform(0, 6.28)
            x, y = c[0] + math.cos(a) * kr * 0.5, c[1] + math.sin(a) * kr * 0.5
            dsil(g, x, y, c[2] + kr * 0.55, c[2] + kr * 0.55 + 0.38, 0.09, 4, bm, r1=0.0, kapak=(False, False))


def _ek_sogut(g, S, rnd):
    """Söğüdün sarkan dal perdeleri: dış kümelerden yere doğru inen ince, iki yüzlü şeritler."""
    for c, kr, dis in S["kumeler"]:
        if not dis:
            continue
        for j in range(5):
            a = rnd.uniform(0, 6.28)
            x, y = c[0] + math.cos(a) * kr * 0.8, c[1] + math.sin(a) * kr * 0.8
            L = rnd.uniform(1.0, 2.0)
            te = (-math.sin(a) * 0.06, math.cos(a) * 0.06, 0)
            ust, alt = (x, y, c[2] - kr * 0.2), (x + math.cos(a) * 0.12, y + math.sin(a) * 0.12, max(0.5, c[2] - kr * 0.2 - L))
            levha(g, [B.add(ust, B.mul(te, -1)), B.add(ust, te), B.add(alt, B.mul(te, 0.25)), B.add(alt, B.mul(te, -0.25))], YD_KOYU if j % 2 else YD, cift=True)


def _ek_kafes(g, S, rnd):
    """Genç sokak ağacının koruma kafesi: dört demir dikme + iki çember, yanında ahşap kazık ve bağ."""
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        cubuk(g, (math.cos(a) * 0.32, math.sin(a) * 0.32, 0), (math.cos(a) * 0.32, math.sin(a) * 0.32, 1.25), 0.03, DEMIR_S)
    for z in (0.45, 1.2):
        for i in range(4):
            a0, a1 = math.pi / 4 + i * math.pi / 2, math.pi / 4 + (i + 1) * math.pi / 2
            cubuk(g, (math.cos(a0) * 0.32, math.sin(a0) * 0.32, z), (math.cos(a1) * 0.32, math.sin(a1) * 0.32, z), 0.025, DEMIR_S)
    cubuk(g, (0.16, 0.05, 0), (0.16, 0.05, 1.7), 0.05, AHSAP)
    cubuk(g, (0.16, 0.05, 1.55), (0.02, 0.0, 1.55), 0.03, AHSAP2)


def _cam7(g, tohum, duzey=0):
    """Çam / köknar: düz gövde, üst üste binen, kenarı dişli konik katlar (iğne yapraklı: kışın da yeşil)."""
    rnd = random.Random(tohum * 131 + 9)
    h = rnd.uniform(0.9, 1.3)
    kat = 5 + tohum % 2
    R, boy = rnd.uniform(1.35, 1.6), rnd.uniform(5.2, 6.2)
    gm = _govde_mat("koyu")
    _boru(g, [(0, 0, 0), (0, 0, h + 0.5)], [0.2, 0.14], 4 if duzey else 6, gm, kapak=False, kabuk=None if duzey else KABUK_GOVDE)
    if duzey == 2:
        dsil(g, 0, 0, h, boy, R * 0.9, 5, IGNE[0], r1=0.0, kapak=(True, False))
        yumusat(g)
        return
    kat = 3 if duzey == 1 else kat
    kenar = 6 if duzey == 1 else 10
    z = h
    for i in range(kat):
        f = i / float(kat)
        r = R * (1 - f * 0.78)
        dz = (boy - h) / kat * 1.45
        tepe = (rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), z + dz)
        etek = []
        faz = rnd.uniform(0, 6.28)
        for k in range(kenar):
            a = 2 * math.pi * k / kenar + faz
            rr = r * (1.0 if k % 2 == 0 else 0.72) * rnd.uniform(0.92, 1.08)
            etek.append((math.cos(a) * rr, math.sin(a) * rr, z - (0.16 if k % 2 == 0 else 0.0) + rnd.uniform(-0.04, 0.04)))
        g.sid += 1
        for k in range(kenar):
            j = (k + 1) % kenar
            n = B.norm(B.newell([etek[k], etek[j], tepe]))
            t = rnd.random()
            g.yuz([etek[k], etek[j], tepe], IGNE[2] if (n[0] * 0.5 - n[1] * 0.6 > 0.25 and t < 0.7) else IGNE[3] if n[0] * 0.5 - n[1] * 0.6 < -0.3 else IGNE[0])
            g.yuz([etek[j], etek[k], (0, 0, z + 0.25)], IGNE[1])                    # katın koyu altı
            if duzey == 0 and i % 2 == 0:
                c = (0, 0, z + dz * 0.4)
                g.yuz([tuple(c[q] + (p[q] - c[q]) * 1.05 for q in range(3)) for p in (tepe, etek[j], etek[k])], KABUK_IGNE)
        g.sid += 1
        z += (boy - h) / kat
    yumusat(g)


def _cali7(g, tip, tohum, duzey=0):
    """Çalılar: genel (yaprak döken, birkaç küme), şimşir küre, ortanca (renkli çiçek topları), lavanta (mor başaklar)."""
    rnd = random.Random(tohum * 53 + len(tip))
    if tip == "simsir":
        dsil(g, 0, 0, 0, 0.3, 0.06, 4, _govde_mat("koyu"), kapak=(False, False))
        _kume(g, (0, 0, 0.72), 0.62, 0.62, 0.6, rnd, TON_SABIT, detay=2 if duzey == 0 else 1 if duzey == 1 else 0, kabuk=YS_KABUK if duzey == 0 else None, puruz=0.04, leke=6 if duzey == 0 else 0, alt=0.85)
    elif tip == "lavanta":
        _kume(g, (0, 0, 0.26), 0.5, 0.48, 0.34, rnd, TON_SABIT, detay=1 if duzey < 2 else 0, kabuk=YS_KABUK if duzey == 0 else None, leke=0, alt=0.5)
        if duzey < 2:
            mor = (PM("Cicek", "#9a86c8"), PM("Cicek", "#b39ad6"))
            for i in range(14 if duzey == 0 else 6):
                a, rr = rnd.uniform(0, 6.28), rnd.uniform(0.05, 0.42)
                x, y = math.cos(a) * rr, math.sin(a) * rr
                cubuk(g, (x, y, 0.4), (x + math.cos(a) * 0.08, y + math.sin(a) * 0.08, 0.72 + rnd.uniform(0, 0.12)), 0.035, mor[i % 2])
    else:
        n = {"genel": 4, "ortanca": 5}[tip] if duzey == 0 else 2 if duzey == 1 else 1
        ton = TON_DOK
        merkez = []
        for i in range(n):
            a = i * 2.4 + rnd.uniform(-0.3, 0.3)
            rr = 0.0 if i == 0 else rnd.uniform(0.35, 0.55)
            kr = rnd.uniform(0.42, 0.58) * (1.15 if i == 0 else 1.0) * (1.4 if duzey == 2 else 1.0)
            c = (math.cos(a) * rr, math.sin(a) * rr, kr * 0.72 + (0.12 if i == 0 else 0.0))
            merkez.append((c, kr))
            _kume(g, c, kr, kr, kr * 0.85, rnd, ton, detay=1 if duzey < 2 else 0, kabuk=KABUK_DOK if (duzey == 0 and i < 3) else None, leke=2 if duzey == 0 else 0, alt=0.7)
        if duzey == 0:
            cubuk(g, (0, 0, 0), (0.05, 0.02, 0.5), 0.06, _govde_mat("koyu"))
            cubuk(g, (0, 0, 0.1), (0.3, -0.2, 0.5), 0.04, _govde_mat("koyu"))
            cubuk(g, (0, 0, 0.1), (-0.28, 0.18, 0.5), 0.04, _govde_mat("koyu"))
        if tip == "ortanca" and duzey < 2:
            renk = (PM("Cicek", "#e9a3c1"), PM("Cicek", "#a9b9e6"), PM("Cicek", "#c9a3dc"), PM("Cicek", "#f3d3df"))
            for i, (c, kr) in enumerate(merkez):
                for j in range(3 if duzey == 0 else 1):
                    a, b = rnd.uniform(0, 6.28), rnd.uniform(0.15, 1.0)
                    p = (c[0] + math.cos(a) * kr * 0.85 * math.cos(b), c[1] + math.sin(a) * kr * 0.85 * math.cos(b), c[2] + math.sin(b) * kr * 0.7)
                    meyve(g, p[0], p[1], p[2] - 0.05, 0.17, renk[(i + j + tohum) % 4], n=6, rz=0.2)
        elif tip == "genel" and tohum % 3 == 2 and duzey == 0:
            for i in range(6):
                a = i * 1.05
                meyve(g, math.cos(a) * 0.5, math.sin(a) * 0.48, 0.75 + 0.1 * (i % 2), 0.08, CICEK[i % len(CICEK)], n=5)
    yumusat(g)


YS_KABUK = ROL("Kabuk", "k", "#3e4a3a")
PARCA_UZAK = {}     # ad -> en uzak görünüm üreticisi (yalnız ağaç ve çalılar; JSON "uzaklar")


def _agac_ekle(ad, aciklama, hedef, fn):
    """fn(g, duzey): üç ayrıntı düzeyini aynı adla kaydeder."""
    ekle(ad, "agac", aciklama, (lambda g, f=fn: f(g, 0)), hedef, hafif=(lambda g, f=fn: f(g, 1)))
    PARCA_UZAK[ad] = (lambda g, f=fn: f(g, 2))


# ad → (tür, tohum, ek). Eski adlar korunur (kayıtlı tasarımlar ve sahneler bozulmasın): yuvarlak = at kestanesi.
_AGAC_AD = [("yuvarlak", "kestane", 4, None), ("kestane", "kestane", 2, _ek_cicek), ("cinar", "cinar", 3, None), ("mese", "mese", 2, None),
            ("ihlamur", "ihlamur", 2, None), ("hus", "hus", 2, None), ("kavak", "kavak", 2, None), ("sogut", "sogut", 2, None),
            ("meyve", "meyve", 3, "meyve"), ("genc", "genc", 2, _ek_kafes)]
for _on, _tur, _n, _ek in _AGAC_AD:
    for _i in range(1, _n + 1):
        _e = _ek_meyve(("#d8402f", "#e8662f", "#c8343a")[_i % 3]) if _ek == "meyve" else _ek
        _to = _i + (40 if _on == "kestane" else 0)
        _agac_ekle("agac_%s_%d" % (_on, _i), "Ağaç: %s %d" % (AGAC_TUR[_tur]["ad"] + (", çiçekli" if _on == "kestane" else ""), _i), "agac",
                   (lambda g, d, t=_tur, s=_to, e=_e: _agac7(g, t, s, d, True, e if d == 0 else None)))
for _i in range(1, 4):
    _agac_ekle("agac_cam_%d" % _i, "Ağaç: çam / köknar (katmanlı konik) %d" % _i, "agac", (lambda g, d, s=_i: _cam7(g, s, d)))
_agac_ekle("agac_ciplak_1", "Ağaç: çıplak meşe (dal iskeleti)", "agac", (lambda g, d: _agac7(g, "mese", 9, d, False)))
for _i in range(1, 4):
    _agac_ekle("cali_%d" % _i, "Çalı %d" % _i, "cali", (lambda g, d, s=_i: _cali7(g, "genel", s, d)))
_agac_ekle("cali_simsir", "Çalı: şimşir küre (budanmış)", "cali", (lambda g, d: _cali7(g, "simsir", 1, d)))
_agac_ekle("cali_ortanca", "Çalı: ortanca (renkli çiçek topları)", "cali", (lambda g, d: _cali7(g, "ortanca", 2, d)))
_agac_ekle("cali_lavanta", "Çalı: lavanta", "cali", (lambda g, d: _cali7(g, "lavanta", 3, d)))


def uzak_parca(ad):
    """Parçanın en uzak görünümü (yalnız ağaç ve çalılarda; yoksa None)."""
    fn = PARCA_UZAK.get(ad)
    if fn is None:
        return None
    g = G(ad)
    fn(g)
    return g


@_cevre("Çit bitkisi (budanmış şimşir çit, bir hücre)")
def cit_bitki(g):
    rnd = random.Random(77)
    kutu(g, -A, A, -0.3, 0.3, 0.0, 0.98, YP, atla=("zn",))
    kutu(g, -A + 0.03, A - 0.03, -0.27, 0.27, 0.98, 1.06, YP2, atla=("zn",))
    for i in range(10):                                         # yaprak lekeleri (ön ve arka yüzde)
        x, z = rnd.uniform(-A + 0.15, A - 0.3), rnd.uniform(0.12, 0.8)
        s = -1 if i % 2 else 1
        y = s * 0.312
        pts = [(x, y, z), (x + 0.16, y, z + 0.03), (x + 0.07, y, z + 0.13)]
        levha(g, pts if s < 0 else pts[::-1], YS_KOYU if i % 3 else YS_MAVI)


# ================================== KULLANIM TÜRLERİ ==================================
# Her türün zemin katına kendine özgü parçalar gelir. Tablo JSON'a yazılır; Unity aynı tabloyu okur (kurallar ortak).
#   onek   : prefab adının başı (City3D havuzları ada bakar). "Bolge": Bolge_<bölge>. Dükkân adları oyundaki ShopModel önekleri.
#   tabela : varsayılan tabela yazısı (oyundaki İngilizce dükkân adları)        simge : tabela bandındaki madalyon
#   on     : zemin katta sokağa bakan yüz (kapı hücresi dışında)                 kapi  : kapı hücresinin yüzü
#   duvar  : bütün öteki yüzlerde duvar paneli (sera camı, sac ...)              tente : zemin kat ön yüzlerine tente
#   aks    : ön sıradaki hücrelerin önüne sırayla konan dekorlar                 cephe : kapı sütununun en üst katındaki cephe süsü
#   tepe   : kapı sütununun çatısına konan parça                                 vurgu : vurgu rengi (boşsa tohumdan)

def _tur(ad, etiket, grup, onek="Bolge", tabela="", simge="", on="", kapi="", duvar="", tente=0, aks=(), cephe="", tepe="", vurgu="", bolge="karma",
         mimari=None):
    """mimari: (üslup adı, duvar rengi, çatı rengi) ya da None. Verilirse türün binası fırçadaki tarzdan bağımsız o üslupla
    kurulur (taş kaide ve merdiven blokları hariç), çatı tarzı üslubunkidir; renkler boşsa bloğun rengi kalır."""
    kasa, ic, arka, zemin, calisan = IC_TUR.get(ad, IC_TUR["_"])
    us, dr, cr = mimari or ("", "", "")
    return dict(ad=ad, etiket=etiket, grup=grup, onek=onek, tabela=tabela, simge=simge, on=on, kapi=kapi, duvar=duvar, tente=tente,
                aks=list(aks), cephe=cephe, tepe=tepe, vurgu=vurgu, bolge=bolge, uslup=us, duvarRenk=dr, catiRenk=cr,
                icKasa=kasa, ic=list(ic), icArka=arka, zemin=zemin, calisan=calisan, kepenk=1 if grup in ("Dükkân", "Market") else 0)


# İç mekân: tür → (kasa hücresinin parçası, öteki hücrelerin parçaları (sırayla), kapı koridorunun dibindeki parça, zemin, çalışan sayısı)
_MK = ("ic_kasa", ("ic_raf", "ic_manav", "ic_raf", "ic_sogutucu"), "ic_sogutucu", "ic_zemin_karo", 1)
_FF = ("ic_tezgah", ("ic_masa", "ic_mutfak", "ic_masa"), "ic_mutfak", "ic_zemin_karo", 2)
IC_TUR = {
    "_": ("", ("ic_oturma", "ic_ofis", "ic_kitaplik"), "", "ic_zemin_ahsap", 0),
    "dukkan": ("ic_kasa", ("ic_raf", "ic_askilik"), "ic_raf", "ic_zemin_ahsap", 1),
    "burger": _FF, "mcdanolds": _FF, "kfg": _FF, "bufe": ("ic_tezgah", ("ic_mutfak", "ic_masa"), "", "ic_zemin_karo", 1),
    "pizza": ("ic_tezgah", ("ic_tas_firin", "ic_masa", "ic_masa"), "ic_tas_firin", "ic_zemin_karo", 2),
    "doner": ("ic_doner", ("ic_masa", "ic_mutfak"), "ic_masa", "ic_zemin_karo", 1), "gyros": ("ic_doner", ("ic_masa", "ic_mutfak"), "ic_masa", "ic_zemin_karo", 1),
    "sushi": ("ic_sushi", ("ic_masa", "ic_mutfak", "ic_masa"), "ic_masa", "ic_zemin_ahsap", 2),
    "kafe": ("ic_kahve", ("ic_koltuk", "ic_masa", "ic_koltuk"), "ic_masa", "ic_zemin_ahsap", 1),
    "balik": ("ic_tezgah", ("ic_mutfak", "ic_masa"), "ic_masa", "ic_zemin_karo", 1),
    "bistro": ("ic_bar", ("ic_masa", "ic_masa", "ic_mutfak"), "ic_masa", "ic_zemin_ahsap", 2),
    "pub": ("ic_bar", ("ic_masa", "ic_koltuk", "ic_masa"), "ic_masa", "ic_zemin_ahsap", 1),
    "firin": ("ic_kasa", ("ic_ekmek", "ic_ekmek", "ic_tas_firin"), "ic_ekmek", "ic_zemin_karo", 1),
    "kasap": ("ic_et", ("ic_sogutucu", "ic_et"), "ic_sogutucu", "ic_zemin_karo", 1),
    "cicekci": ("ic_kasa", ("ic_cicek", "ic_cicek"), "ic_cicek", "ic_zemin_ahsap", 1),
    "kitapci": ("ic_kasa", ("ic_kitaplik", "ic_kitaplik", "ic_okuma"), "ic_kitaplik", "ic_zemin_ahsap", 1),
    "eczane": ("ic_eczane", ("ic_raf", "ic_bekleme"), "ic_raf", "ic_zemin_karo", 1),
    "bakkal24": _MK, "minimart": _MK, "market": _MK, "supermarket": _MK, "market24": _MK, "bio": _MK,
    "belediye": ("ic_gise", ("ic_bekleme", "ic_ofis", "ic_bekleme"), "ic_ofis", "ic_zemin_ahsap", 1),
    "postane": ("ic_gise", ("ic_bekleme", "ic_palet"), "ic_ofis", "ic_zemin_karo", 1),
    "hastane": ("ic_resepsiyon", ("ic_bekleme", "ic_muayene", "ic_pano", "ic_sedye", "ic_muayene", "ic_tekerlekli"), "ic_bekleme", "ic_zemin_karo", 1),
    "polis": ("ic_gise", ("ic_ofis", "ic_bekleme", "ic_ofis"), "ic_ofis", "ic_zemin_karo", 1),
    "itfaiye": ("ic_ofis", ("ic_itfaiye", "ic_itfaiye", "ic_hortum", "ic_ekipman", "ic_dolap", "ic_hortum", "ic_dolap", "ic_ekipman"), "", "ic_zemin_beton", 1),
    "okul": ("ic_tahta", ("ic_sira", "ic_sira", "ic_kitaplik"), "ic_sira", "ic_zemin_ahsap", 1),
    "kutuphane": ("ic_gise", ("ic_kitaplik", "ic_okuma", "ic_kitaplik", "ic_koltuk"), "ic_okuma", "ic_zemin_ahsap", 1),
    "depo": ("ic_ofis", ("ic_palet", "ic_forklift", "ic_palet"), "ic_palet", "ic_zemin_beton", 1),
    "garaj": ("ic_ofis", ("ic_lift", "ic_palet"), "ic_lift", "ic_zemin_beton", 1),
    "fabrika": ("ic_ofis", ("ic_makine", "ic_makine", "ic_palet"), "ic_makine", "ic_zemin_beton", 1),
    "sera": ("", ("ic_cicek", "ic_manav"), "ic_cicek", "ic_zemin_beton", 0),
    "gar": ("ic_gise", ("ic_bekleme", "ic_bekleme", "ic_kahve"), "ic_bekleme", "ic_zemin_karo", 1),
    "otel": ("ic_resepsiyon", ("ic_koltuk", "ic_masa", "ic_koltuk"), "ic_koltuk", "ic_zemin_ahsap", 1),
    "avm": ("ic_kasa", ("ic_askilik", "ic_raf", "ic_yuruyen", "ic_askilik"), "ic_askilik", "ic_zemin_karo", 1),
    "pazar": ("ic_pazar", ("ic_pazar", "ic_manav", "ic_pazar", "ic_cicek"), "ic_pazar", "ic_zemin_beton", 1),
    "opera": ("ic_gise", ("ic_koltuklar", "ic_koltuklar", "ic_sahne"), "ic_sahne", "ic_zemin_ahsap", 1),
}
# Bir iç mekânda aynı mobilyadan en fazla kaç tane olabilir (yazılmayanlar IC_SINIR_VARSAYILAN). Sınır dolunca türün
# listesindeki sıradaki mobilyaya, o da dolunca IC_YEDEK'e geçilir; hepsi dolduysa hücre boş (açık alan) kalır.
IC_SINIR_VARSAYILAN = 6
IC_SINIR = {"ic_itfaiye": 2, "ic_hortum": 2, "ic_dolap": 3, "ic_ekipman": 3, "ic_sedye": 2, "ic_muayene": 4, "ic_tekerlekli": 2, "ic_pano": 2,
            "ic_bekleme": 3, "ic_tas_firin": 1, "ic_mutfak": 2, "ic_sahne": 1, "ic_yuruyen": 1, "ic_forklift": 2, "ic_lift": 2, "ic_makine": 4,
            "ic_koltuklar": 4, "ic_tahta": 1, "ic_kahve": 1, "ic_sogutucu": 4, "ic_manav": 3, "ic_kitaplik": 5, "ic_okuma": 4, "ic_ofis": 3,
            "ic_oturma": 2, "ic_koltuk": 4, "ic_bitki": 4, "ic_palet": 5, "ic_pazar": 5, "ic_cicek": 4, "ic_ekmek": 3, "ic_et": 2, "ic_askilik": 5}
IC_YEDEK = ["ic_bitki", "ic_koltuk", "ic_bekleme"]
_D = dict(on="vitrin", kapi="kapi_vitrin", tente=1)
_Y = dict(on="vitrin_genis", kapi="kapi_vitrin", tente=1)
TURLER = [
    _tur("konut", "Konut (genel)", "Konut", bolge="eski"),
    _tur("sira_evi", "Sıra evi (üsluba göre: Georgian, Victorian, sıva, mews)", "Konut", onek="Konut", bolge="eski"),
    _tur("villa", "Villa", "Konut", onek="Konut_villa", kapi="kapi_sutunlu", bolge="ev"),
    _tur("dukkan", "Dükkân (genel)", "Dükkân", tabela="SHOP", simge="simge_yildiz", **_D),
    _tur("burger", "Fast food: Burger Queen", "Dükkân", onek="FastFood_burger_queen", tabela="BURGER QUEEN", simge="simge_burger", vurgu="#d9522b", aks=("masa", "menu_panosu"), **_Y),
    _tur("mcdanolds", "Fast food: McDanold's", "Dükkân", onek="FastFood_mcdanolds", tabela="MCDANOLD'S", simge="simge_burger", vurgu="#c8342f", aks=("masa", "cop_kutusu"), **_Y),
    _tur("kfg", "Fast food: KFG", "Dükkân", onek="Dukkan_kfg", tabela="KFG", simge="simge_burger", vurgu="#b5303a", aks=("menu_panosu",), **_Y),
    _tur("pizza", "Pizzacı: Pizza Hat", "Dükkân", onek="Dukkan_pizza_hat", tabela="PIZZA HAT", simge="simge_pizza", vurgu="#b5303a", aks=("masa",), **_D),
    _tur("doner", "Dönerci: Kebab King", "Dükkân", onek="Dukkan_doner_kral", tabela="KEBAB KING", simge="simge_et", vurgu="#c46a2b", aks=("menu_panosu",), **_D),
    _tur("gyros", "Gyros: Non-Stop Gyros", "Dükkân", onek="Dukkan_nonstop_gyros", tabela="NON-STOP GYROS", simge="simge_et", vurgu="#3f5f8a", aks=("masa",), **_D),
    _tur("sushi", "Sushi: Sushi Samurai", "Dükkân", onek="Dukkan_sushi_samurai", tabela="SUSHI SAMURAI", simge="simge_balik", vurgu="#3a3d42", aks=("saksi",), **_Y),
    _tur("kafe", "Kafe: Starbocks", "Dükkân", onek="Dukkan_starbocks", tabela="STARBOCKS", simge="simge_fincan", vurgu="#2f6b57", aks=("masa", "masa", "menu_panosu"), **_Y),
    _tur("balik", "Fish & chips: Fish & Ships", "Dükkân", onek="Dukkan_fish_ships", tabela="FISH & SHIPS", simge="simge_balik", vurgu="#3f6f9a", aks=("fici", "menu_panosu"), **_D),
    _tur("bufe", "Büfe: Station Snack Bar", "Dükkân", onek="Dukkan_otogar_bufe", tabela="SNACK BAR", simge="simge_fincan", vurgu="#d9a441", aks=("cop_kutusu",), **_D),
    _tur("bistro", "Bistro: Villa Gourmet", "Dükkân", onek="Dukkan_villa_gourmet", tabela="VILLA GOURMET", simge="simge_bardak", vurgu="#6b4a7a", aks=("masa", "saksi"), **_D),
    _tur("pub", "Pub", "Dükkân", tabela="THE OLD PUB", simge="simge_bardak", vurgu="#2f4f40", aks=("fici", "masa"), on="pub_cephe", kapi="pub_kapi"),
    _tur("firin", "Fırın / pastane", "Dükkân", tabela="BAKERY", simge="simge_ekmek", vurgu="#c98a4a", aks=("kasalar",), **_D),
    _tur("kasap", "Kasap / şarküteri", "Dükkân", tabela="BUTCHER", simge="simge_et", vurgu="#a8342f", **_D),
    _tur("cicekci", "Çiçekçi", "Dükkân", tabela="FLOWERS", simge="simge_cicek", vurgu="#8a5a78", aks=("cicek_kovalari", "saksi"), **_D),
    _tur("kitapci", "Kitapçı", "Dükkân", tabela="BOOKS", simge="simge_kitap", vurgu="#3f5f8a", aks=("bank",), **_D),
    _tur("eczane", "Eczane", "Dükkân", onek="Dukkan_eczane", tabela="PHARMACY", simge="simge_hac", vurgu="#3f8a5a", cephe="hac_cephe", on="vitrin", kapi="kapi_vitrin"),
    _tur("bakkal24", "Köşe bakkal: 24/7 Corner Store", "Dükkân", onek="Dukkan_abc_nonstop", tabela="24/7 CORNER STORE", simge="simge_sepet", vurgu="#3f7d74", aks=("kasalar",), **_D),
    _tur("minimart", "Bakkal: Mini Mart", "Dükkân", onek="Dukkan_mini_abc", tabela="MINI MART", simge="simge_sepet", vurgu="#d9822b", aks=("kasalar",), **_D),
    _tur("market", "Market: Sparx Express", "Market", onek="Market_szpar_express", tabela="SPARX EXPRESS", simge="simge_sepet", vurgu="#2f8a4a", on="vitrin_genis", kapi="kapi_otomatik", aks=("araba_yeri", "kasalar"), mimari=("modern", "#e8ebee", "")),
    _tur("supermarket", "Süpermarket: InterSparx", "Market", onek="Market_interszpar", tabela="INTERSPARX", simge="simge_sepet", vurgu="#c8342f", on="vitrin_genis", kapi="kapi_otomatik", aks=("araba_yeri", "", "araba_yeri"), mimari=("modern", "#dfe4e8", "")),
    _tur("market24", "Market: Market 24", "Market", onek="Market_market_24", tabela="MARKET 24", simge="simge_sepet", vurgu="#3f6fd0", on="vitrin_genis", kapi="kapi_otomatik", aks=("kasalar",), mimari=("modern", "#e4e6ea", "")),
    _tur("bio", "Market: BioSparx", "Market", onek="Dukkan_bioszpar", tabela="BIOSPARX", simge="simge_cicek", vurgu="#5a8f4a", aks=("kasalar", "saksi"), **_D),
    _tur("belediye", "Belediye", "Kamu", onek="Kamu", tabela="TOWN HALL", on="sutunlu", kapi="kapi_sutunlu", cephe="saat_cephe", tepe="alinlik_tas", vurgu="#2c3d70", mimari=("portland", "#ece5d3", "#6f7781")),
    _tur("hastane", "Hastane", "Kamu", onek="Kamu", tabela="HOSPITAL", simge="simge_hac", kapi="kapi_otomatik", cephe="hac_cephe", vurgu="#c8342f", mimari=("modern", "#f0f1f3", "")),
    _tur("itfaiye", "İtfaiye", "Kamu", onek="Kamu", tabela="FIRE STATION", on="kapi_garaj", kapi="kapi_ahsap", vurgu="#c8342f", tepe="bayrak", mimari=("gotik", "#c25e4c", "#6f7781")),
    _tur("polis", "Polis", "Kamu", onek="Kamu", tabela="POLICE", simge="simge_yildiz", kapi="kapi_ahsap", vurgu="#2c3d70", aks=("polis_feneri",), mimari=("victorian", "#c4705a", "#6f7781")),
    _tur("postane", "Postane", "Kamu", onek="Kamu", tabela="POST OFFICE", kapi="kapi_vitrin", on="vitrin", vurgu="#b8382f", aks=("posta_kutusu",), mimari=("portland", "#e9e0cc", "#6f7781")),
    _tur("okul", "Okul", "Kamu", onek="Kamu", tabela="SCHOOL", cephe="saat_cephe", tepe="bayrak", vurgu="#2f6048", aks=("demir_korkuluk",), mimari=("gotik", "#d9c08c", "#6f7781")),
    _tur("kutuphane", "Kütüphane", "Kamu", onek="Kamu", tabela="LIBRARY", simge="simge_kitap", vurgu="#6b4a7a", mimari=("victorian", "#d9c08c", "#6f7781")),
    _tur("depo", "Depo", "Sanayi", onek="Sanayi_depo", tabela="DEPOT", on="kapi_garaj", kapi="kapi_garaj", vurgu="#2f6048", aks=("kasalar",), bolge="sanayi", mimari=("depo", "#b4664f", "#6f7781")),
    _tur("garaj", "Garaj / tamirhane", "Sanayi", onek="Sanayi_garaj", tabela="GARAGE", on="garaj_acik", kapi="kapi_ahsap", vurgu="#d9822b", aks=("fici",), bolge="sanayi", mimari=("depo", "#a87460", "#6f7781")),
    _tur("fabrika", "Fabrika", "Sanayi", onek="Sanayi_fabrika", tabela="FACTORY", kapi="kapi_garaj", tepe="baca_fabrika", vurgu="#2c3d70", bolge="sanayi", mimari=("fabrika", "#b9604b", "#6f7781")),
    _tur("sera", "Sera", "Sanayi", onek="Sanayi_sera", duvar="sera_cam", kapi="kapi_vitrin", vurgu="#5a8f4a", bolge="sanayi", mimari=("sera", "#f4f6f3", "#d4e7ee")),
    _tur("gar", "Gar", "Simge", tabela="CENTRAL STATION", cephe="saat_cephe", vurgu="#2f6048", mimari=("gotik", "#c25e4c", "#6f7781")),
    _tur("otel", "Otel", "Simge", tabela="GRAND HOTEL", simge="simge_yildiz", on="vitrin", kapi="kapi_vitrin", tente=1, tepe="bayrak", vurgu="#8a5a78", aks=("saksi",), mimari=("siva", "#f5f0e6", "#6f7781")),
    _tur("avm", "Büyük mağaza / AVM", "Simge", tabela="DEPARTMENT STORE", on="vitrin_genis", kapi="kapi_otomatik", tente=1, vurgu="#3f7d74", bolge="avm", mimari=("portland", "#e6dcc6", "#6f7781")),
    _tur("pazar", "Kapalı pazar", "Simge", tabela="MARKET HALL", on="kemer_cam", kapi="kapi_kemerli", vurgu="#c46a2b", aks=("kasalar", "cicek_kovalari"), mimari=("depo", "#d4b483", "#6f7781")),
    _tur("opera", "Opera / tiyatro", "Simge", tabela="OPERA", on="sutunlu", kapi="kapi_sutunlu", vurgu="#8a2f4a", tepe="alinlik_tas", mimari=("portland", "#efe8d8", "#6f7781")),
]
TUR = {t["ad"]: t for t in TURLER}


def tur_bul(t):
    ad = t.get("tur") or ("dukkan" if t.get("dukkan") else "konut")
    return TUR.get(ad, TUR["konut"])


# ================================== PARÇA GEOMETRİSİ ==================================

_PARCA_G, _HAFIF_G, _KUCUK = {}, {}, {}


def parca_g(ad):
    g = _PARCA_G.get(ad)
    if g is None:
        g = G(ad)
        PARCA[ad](g)
        _PARCA_G[ad] = g
    return g


def hafif_parca(ad):
    """Parçanın uzak görünüm hâli: özel üreticisi varsa o; yoksa küçük katılar ve kabuklar atılır (boş kalırsa asıl parça)."""
    g = _HAFIF_G.get(ad)
    if g is None:
        if ad in PARCA_HAFIF:
            g = G(ad)
            PARCA_HAFIF[ad](g)
        else:
            tam = parca_g(ad)
            h = B.hafif(tam, esik=0.3)
            g = G(ad)
            for f, mi, sd in zip(h.f, h.fm, h.fs):
                if "_Kabuk_" not in h.mats[mi]:
                    g.sid = sd
                    g.yuz([h.v[i] for i in f], h.mats[mi])
            if not g.f:
                g = tam
        _HAFIF_G[ad] = g
    return g


def kucuk(ad):
    """Küçük dekor mu (en büyük ölçüsü 1,2 m'nin altında)? Uzak görünümde atlanır."""
    k = _KUCUK.get(ad)
    if k is None:
        v = parca_g(ad).v
        k = _KUCUK[ad] = max(max(p[i] for p in v) - min(p[i] for p in v) for i in range(3)) < 1.2
    return k


# ================================== BİRLEŞTİRİCİ ==================================
# Tasarım: dict(ad, bolge, tohum, tur, tabela, tente, bloklar=[dict(x, y, k, tarz, renk, cati, ct)],
#               susler=[dict(ad, x, y, aci, olcek, renk)]).
# x, y 0…IZGARA−1 (y = 0 sokak tarafı), k kat; süslerde x, y metre (arsa ortası 0, sokak −Y), aci derece.
# Kurallar Scripts/World/Atolye/AtolyeKurucu.cs ile aynı (karma fonksiyonu dahil): birini değiştirince öbürünü de değiştir.

def karma(a, b, c, d):
    h = (a * 73856093) ^ (b * 19349663) ^ (c * 83492791) ^ (d * 2654435761)
    h &= 0xffffffff
    h ^= h >> 13
    h = (h * 0x5bd1e995) & 0xffffffff
    h ^= h >> 15
    return h


YON = [(0, -1), (1, 0), (0, 1), (-1, 0)]       # ön, sağ, arka, sol (dönüş 0, 1, 2, 3 × 90°)
APT_UST = ["duvar_pencere", "duvar_pencere", "duvar_cift", "duvar_kemerli", "duvar_saksi", "duvar_panjur", "duvar_dar", "duvar_balkon", "duvar_duz", "duvar_pencere",
           "duvar_alinlik", "duvar_panjur_kapali", "duvar_stor"]
FW_UST = ["fachwerk_pencere", "fachwerk_capraz", "fachwerk_saksi", "fachwerk_x", "fachwerk_pencere"]
TAS_UST = ["tas_duvar", "tas_mazgal", "tas_kemerli", "tas_duvar"]
SAKSI_ALIR = ("duvar_pencere", "duvar_cift", "duvar_panjur", "duvar_panjur_kapali")
# Çatı tarzı (bloğun ct alanı): 0 tarza göre · 1 kırma · 2 beşik · 3 mansart · 4 düz teras · 5 basamaklı alınlık · 6 testere
#                · 7 parapet arkasında arduvaz mansart (Londra): saçak yerine parapet, ortak duvarlarda sıra baca
CT_OTO, CT_KIRMA, CT_BESIK, CT_MANSART, CT_TERAS, CT_BASAMAK, CT_TESTERE, CT_PARAPET = range(8)
CATI_TARZ_AD = ["Tarza göre", "Kırma", "Beşik", "Mansart", "Düz teras (parapetli)", "Basamaklı alınlık (Hollanda)", "Testere (sanayi)",
                "Parapet arkasında mansart (Londra)"]
CATI_H = {CT_KIRMA: [0.0, 0.85, 1.7, 2.55, 3.4], CT_BESIK: [0.0, 1.25, 2.5, 3.75, 5.0], CT_MANSART: [0.0, 2.0, 2.45, 2.9, 3.35],
          CT_BASAMAK: [0.0, 1.25, 2.5, 3.75, 5.0], CT_PARAPET: [0.0, 1.3, 1.6, 1.9, 2.2]}
# köşe yükseklik deseni (h00, h10, h11, h01) → (karo, dönüş)
CATI_DESEN = {
    (0, 0, 0, 0): ("cati_duz", 0),
    (0, 0, 1, 1): ("cati_egim", 0), (1, 0, 0, 1): ("cati_egim", 1), (1, 1, 0, 0): ("cati_egim", 2), (0, 1, 1, 0): ("cati_egim", 3),
    (0, 0, 1, 0): ("cati_kose", 0), (0, 0, 0, 1): ("cati_kose", 1), (1, 0, 0, 0): ("cati_kose", 2), (0, 1, 0, 0): ("cati_kose", 3),
    (0, 1, 1, 1): ("cati_vadi", 0), (1, 0, 1, 1): ("cati_vadi", 1), (1, 1, 0, 1): ("cati_vadi", 2), (1, 1, 1, 0): ("cati_vadi", 3),
    (1, 0, 1, 0): ("cati_semer", 0), (0, 1, 0, 1): ("cati_semer", 1),
}
CATI_KAROLARI = ("cati_duz", "cati_egim", "cati_kose", "cati_vadi", "cati_semer")


# Londra duvar üslupları (bloğun tarz alanı USLUP_TABAN + sıra). Tablo JSON'a yazılır (usluplar); kurallar yerlesim() ve
# AtolyeKurucu.Yerlesim'de aynı:
#   kapi        : kapı hücresinin yüzü (türün kendi kapısı yoksa) ve konutta her kapiAralik hücrede bir yinelenen ek kapı
#   onZemin     : zemin katın öne (sokağa, −Y) bakan yüzleri (boşsa zemin)       zemin : zemin katın öbür yüzleri
#   onKat1/kat1 : birinci kat (ön / öbür yüzler; boşsa ust)                       ust   : ikinci kat ve üstü
#   tavan       : sütunun en üst katı (ikinci kat ve üstünde; boşsa ust)         duz   : cephe süsünün (saat…) arkasındaki düz duvar
#   koseZemin / kose : dış köşe parçası (boşsa yok)     kaide : zemin kat kaidesi     silmeZemin / silme : kat üstü silmesi
#   cati        : çatı tarzı "Tarza göre" iken kullanılan tarz
# Listelerden seçim sütun boyunca aynı karma ile yapılır (pencereler üst üste hizalı).
def _uslup(ad, etiket, kapi, zemin, ust, duz, on_zemin=(), kat1=(), on_kat1=(), tavan=(), kose="", kose_zemin="", kaide="",
           silme="", silme_zemin="", kapi_aralik=0, cati=CT_KIRMA):
    return dict(ad=ad, etiket=etiket, kapi=kapi, zemin=list(zemin), onZemin=list(on_zemin), kat1=list(kat1), onKat1=list(on_kat1),
                ust=list(ust), tavan=list(tavan), duz=duz, kose=kose, koseZemin=kose_zemin, kaide=kaide, silme=silme,
                silmeZemin=silme_zemin, kapiAralik=kapi_aralik, cati=cati)


USLUPLAR = [
    _uslup("georgian", "Georgian sıra ev (tuğla, sıva zemin, parapet)", "g_kapi", ["g_zemin_yan"], ["g_ust"], "g_duz", on_zemin=["g_zemin"],
           kat1=["g_kat1", "g_kat1", "g_kat1_korkuluk"], tavan=["g_tavan"], kose_zemin="g_kose", silme_zemin="g_silme", kapi_aralik=2, cati=CT_PARAPET),
    _uslup("victorian", "Victorian tuğla ev (cumbalı, taş söveli)", "v_kapi", ["v_zemin"], ["v_ust", "v_ust", "v_ust_cift"], "v_duz",
           on_zemin=["v_cumba"], on_kat1=["v_cumba_ust"], kaide="kaide", silme="v_silme", silme_zemin="v_silme", kapi_aralik=2, cati=CT_KIRMA),
    _uslup("siva", "Beyaz sıva teras (Belgravia: portiko, sürekli balkon)", "s_kapi", ["s_zemin_yan"], ["s_ust"], "s_duz", on_zemin=["s_zemin"],
           kat1=["s_kat1_yan"], on_kat1=["s_kat1"], tavan=["s_tavan"], kose="kose_dis", kose_zemin="kose_dis", silme="silme", silme_zemin="silme",
           kapi_aralik=2, cati=CT_PARAPET),
    _uslup("mews", "Mews evi (garaj kapılı, boyalı tuğla)", "m_kapi", ["m_duz", "m_ust"], ["m_ust", "m_ust_kapi", "m_ust"], "m_duz",
           on_zemin=["m_garaj"], silme="m_silme", silme_zemin="m_silme", kapi_aralik=2, cati=CT_PARAPET),
    _uslup("portland", "Portland taşı (neoklasik, pilastrlı)", "kapi_sutunlu", ["p_zemin"], ["p_ust"], "p_duz", kat1=["p_kat1"], tavan=["p_tavan"],
           kose="p_kose", kose_zemin="p_kose", silme_zemin="p_silme", cati=CT_PARAPET),
    _uslup("gotik", "Victorian gotik (kırmızı tuğla, sivri kemer)", "gt_kapi", ["gt_zemin"], ["gt_ust", "gt_ust_cift"], "gt_duz",
           kose="gt_kose_ust", kose_zemin="gt_kose", silme="v_silme", silme_zemin="v_silme", cati=CT_BESIK),
    _uslup("depo", "Tuğla depo / atölye", "kapi_garaj", ["d_zemin", "d_zemin", "d_duz"], ["d_ust"], "d_duz", kose="d_kose", kose_zemin="d_kose",
           kaide="kaide", silme="v_silme", silme_zemin="v_silme", cati=CT_BESIK),
    # türe özgü mimari (v9): yalnız türün kendisi seçer, fırçada da görünür
    _uslup("fabrika", "Fabrika (tuğla, demir doğramalı geniş pencere, testere çatı)", "f_kapi", ["f_zemin"], ["f_ust"], "d_duz",
           kose="d_kose", kose_zemin="d_kose", kaide="kaide", silme="v_silme", silme_zemin="v_silme", cati=CT_TESTERE),
    _uslup("modern", "Modern (kaplama paneller, şerit pencere, düz çatı)", "kapi_otomatik", ["mo_duz"], ["mo_ust"], "mo_duz",
           on_zemin=["mo_vitrin"], kose="mo_kose", kose_zemin="mo_kose", silme="mo_bant", silme_zemin="mo_bant", cati=CT_TERAS),
    _uslup("sera", "Sera (cam ve beyaz demir)", "kapi_vitrin", ["sera_cam"], ["sera_cam"], "sera_cam", cati=CT_BESIK),
]
TARZ_AD += [u["etiket"] for u in USLUPLAR]


def tur_mimarisi(b, T):
    """Türün kendi mimarisi varsa (TURLER: mimari) bloğun duvar tarzı o üsluba, çatı tarzı "Tarza göre"ye döner: fabrika,
    market, hastane, okul ... fırçada hangi tarz seçilmiş olursa olsun kendi binası olur. Taş kaide ve merdiven değişmez.
    C#: AtolyeKurucu.TurMimarisi."""
    ad = T.get("uslup")
    if not ad or b["tarz"] in (KAIDE, MERDIVEN):
        return b
    i = next((j for j, u in enumerate(USLUPLAR) if u["ad"] == ad), -1)
    if i < 0:
        return b
    return dict(b, tarz=USLUP_TABAN + i, ct=CT_OTO)


def uslup(tarz):
    """Tarzın Londra üslubu (tabloda yoksa None: eski tarzlar). C#: AtolyeKurucu.Uslup."""
    i = tarz - USLUP_TABAN
    return USLUPLAR[i] if 0 <= i < len(USLUPLAR) else None


def govdeli(tarz):
    """İçi olan, kapı alabilen gövde tarzı mı (kaide ve merdiven değil). C#: AtolyeKurucu.Govdeli."""
    return tarz in (APARTMAN, FACHWERK, TAS) or uslup(tarz) is not None


def _kafes(p):
    return ((p[0] - IZGARA) * A, (p[1] - IZGARA) * A)


def hucre_merkezi(x, y):
    return ((x - (IZGARA - 1) / 2.0) * HUCRE, (y - (IZGARA - 1) / 2.0) * HUCRE)


def cizgi_rengi(hexv):
    """Kalem çizgisi rengi: yüzey renginin koyusu ile sıcak koyu mor-kahvenin ortası (saf siyah değil)."""
    h = hexv.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    r = [c[i] * 0.33 + k for i, k in enumerate((0.17, 0.14, 0.18))]        # v6: daha açık (siyaha yakın değil)
    return "#%02x%02x%02x" % tuple(int(v * 255) for v in r)


DUVAR_GRUP = ["Duvar_On", "Duvar_Sag", "Duvar_Arka", "Duvar_Sol"]
GIZLI_GRUP = ("Ic", "Ampul", "Poset", "Kepenk")        # uzak görünümde (LOD1) ve tek parça önizlemede yok


def ic_plan(blok, kapi, T):
    """Zemin katın iç yerleşimi. Mobilyaların hepsi öne (−Y) bakar ve hücrenin arka yarısında durur: her sıranın ön yarısı
    yürüme koridorudur. Kapı sütunu (kapıdan arkaya) boş "geçit"tir; koridorlardan ulaşılamayan hücre kalırsa önündeki
    hücre de geçit olur (blok şekli ne olursa olsun kapıdan her hücreye yol açık). C# karşılığı: AtolyeKurucu.IcPlan.
    Dönen: hucreler (sıralı), gecit (küme), mobilya {hücre: parça}, kasa (hücre ya da None), kapi (x, y)."""
    Z = sorted((x, y) for (x, y, k), b in blok.items() if k == 0 and govdeli(b["tarz"]))
    plan = dict(hucreler=Z, gecit=set(), mobilya={}, kasa=None, kapi=(0, 0))
    if not kapi or not Z:
        plan["hucreler"] = []
        return plan
    zs = set(Z)
    ky, kx = kapi
    plan["kapi"] = (kx, ky)
    kolon = []
    y = ky
    while (kx, y) in zs:
        kolon.append((kx, y))
        y += 1
    E = set(kolon[:-1])
    while True:
        R, yigin = {(kx, ky)}, [(kx, ky)]
        while yigin:
            x, y = yigin.pop()
            for q, gec in (((x - 1, y), True), ((x + 1, y), True), ((x, y + 1), (x, y) in E), ((x, y - 1), (x, y - 1) in E)):
                if gec and q in zs and q not in R:
                    R.add(q)
                    yigin.append(q)
        if len(R) == len(zs):
            break
        ek = None
        for u in Z:
            if u in R:
                continue
            if (u[0], u[1] - 1) in R:
                ek = (u[0], u[1] - 1)       # öndeki hücre geçit olur
                break
            if (u[0], u[1] + 1) in R:
                ek = u
                break
        if ek is None:
            break                           # yalnız çaprazdan bağlı parça: olduğu gibi kalır
        E.add(ek)
    mob = {}
    say = {}

    def al(ad):
        """Mobilyanın sınırı dolmadıysa sayar ve True döner."""
        if say.get(ad, 0) >= IC_SINIR.get(ad, IC_SINIR_VARSAYILAN):
            return False
        say[ad] = say.get(ad, 0) + 1
        return True

    if T["icKasa"]:
        for c in ((kx + 1, ky), (kx - 1, ky), kolon[-1]):
            if c in zs and c not in E:
                plan["kasa"] = c
                mob[c] = T["icKasa"]
                al(T["icKasa"])
                break
    son = kolon[-1]
    if son not in E and son not in mob and T["icArka"] and al(T["icArka"]):
        mob[son] = T["icArka"]
    j = 0
    liste = T["ic"]
    for c in sorted(Z, key=lambda c: (c[1], c[0])):          # ön sıradan (sokak tarafı) arkaya, soldan sağa
        if c in E or c in mob or not liste:
            continue
        sec = None
        for d in range(len(liste)):
            ad = liste[(j + d) % len(liste)]
            if al(ad):
                sec = ad
                j += d + 1
                break
        if sec is None:
            for ad in IC_YEDEK:
                if al(ad):
                    sec = ad
                    break
        if sec:
            mob[c] = sec
    plan["gecit"] = E
    plan["mobilya"] = mob
    return plan


def yerlesim(t):
    """Tasarımdan parça yerleşimleri. Dönen: (yerler, çokgenler, kutular).
    yer: (parça, merkez x, merkez y, taban z, dönüş, z ölçeği, kaydırma, renkler, ölçek, açı)."""
    T = tur_bul(t)
    bloklar = [tur_mimarisi(b, T) for b in (t.get("bloklar") or [])]
    blok = {(b["x"], b["y"], b["k"]): b for b in bloklar}
    tohum = t.get("tohum", 1)
    metin = (t.get("tabela") or T["tabela"] or "").upper()
    yerler, cokgenler, kutular = [], [], []

    def dolu(x, y, k):
        b = blok.get((x, y, k))
        return b is not None and b["tarz"] != MERDIVEN

    def renk(b, yaprak=0):
        dv = T["duvarRenk"] or DUVAR_RENK[b["renk"] % len(DUVAR_RENK)]
        ct = T["catiRenk"] or CATI_RENK[b["cati"] % len(CATI_RENK)]
        vr = T["vurgu"] or VURGU_RENK[karma(tohum, b["renk"], b["cati"], 7) % len(VURGU_RENK)]
        yp = YAPRAK_RENK[yaprak % len(YAPRAK_RENK)]
        r = dict(D=dv, d=koyu(dv, 0.9), C=ct, c=koyu(ct, 0.86), K=vr, T=TENTE_RENK[t.get("tente", 0) % len(TENTE_RENK)], k=cizgi_rengi(yp))
        r.update(yaprak_tonlari(yp))
        r["kis"] = yaprak % len(YAPRAK_RENK) == KIS_RENGI          # önizleme: kışın yaprak döken ağaç çıplak
        return r

    zorla = [None]

    def koy(ad, x, y, z, don, r, zs=1.0, kay=(0.0, 0.0), olcek=1.0, grup=None):
        """grup: prefabdaki ayrı çocuk (Govde, Cati, Duvar_On/Sag/Arka/Sol, Ic, Ampul, Poset, Kepenk). Verilmezse
        birinci kat ve üstü "Cati" (kamera yaklaşınca kalkan parça), zemin "Govde"."""
        cx, cy = hucre_merkezi(x, y)
        if grup is None:
            grup = zorla[0] or ("Cati" if z >= KAT - 0.01 else "Govde")
        cam = karma(x * 4 + don % 4, y, int(round(z / KAT)), tohum + 41) % 3      # pencere ışık grubu (gece ayrı ayrı yanar)
        yerler.append((ad, cx, cy, z, don % 4, zs, kay, r, olcek, 0.0, grup, cam))

    def on_acik(x, y):
        b = blok.get((x, y, 0))
        return b is not None and govdeli(b["tarz"]) and not dolu(x, y - 1, 0)

    # ---- kapı hücresi: sokağa (y küçük) en yakın, zemindeki açık ön yüzlerden biri
    on_yuzler = sorted((b["y"], b["x"]) for b in bloklar if b["k"] == 0 and on_acik(b["x"], b["y"]))
    kapi = None
    if on_yuzler:
        y_min = on_yuzler[0][0]
        adaylar = [p for p in on_yuzler if p[0] == y_min]
        kapi = adaylar[karma(tohum, len(adaylar), 3, 1) % len(adaylar)]
    kapi_ust = 0
    if kapi:
        while dolu(kapi[1], kapi[0], kapi_ust + 1):
            kapi_ust += 1

    for (x, y, k), b in sorted(blok.items()):
        tarz = b["tarz"]
        r = renk(b)
        z = k * KAT
        if tarz == MERDIVEN:
            don = 0
            for i, (dx, dy) in enumerate(YON):
                if dolu(x + dx, y + dy, k):
                    don = (i + 2) % 4
                    break
            koy("tas_merdiven", x, y, z, don, r)
            continue
        alt_bos = k > 0 and not dolu(x, y, k - 1)
        if alt_bos:
            koy("taban", x, y, z, 0, r)
        kapi_hucresi = kapi == (y, x)
        U = uslup(tarz)
        sade = bool(T["duvar"]) and tarz == APARTMAN
        if tarz == KAIDE and k == 0 and dolu(x, y, 1):
            koy("taban", x, y, KAT, 0, r, olcek=0.97, grup="Govde")      # üst katlar kalkınca kaidenin üstü açık kalmasın
        for i, (dx, dy) in enumerate(YON):
            if dolu(x + dx, y + dy, k):
                continue
            dg = DUVAR_GRUP[i] if (k == 0 and tarz != KAIDE) else None      # zemin kat duvarı: kameraya bakınca iner
            h = karma(x * 4 + i, y, tohum, 11)                   # sütun boyunca aynı: pencereler üst üste hizalı
            hk = karma(x * 4 + i, y, tohum + k * 31, 17)
            kapi_yuzu = k == 0 and i == 0 and kapi_hucresi
            on_zemin = k == 0 and i == 0 and tarz != KAIDE
            cephe_yuzu = bool(T["cephe"]) and kapi_hucresi and i == 0 and k == kapi_ust and k > 0
            ek_kapi = False
            if tarz == KAIDE:
                ad = "tas_duvar"
            elif kapi_yuzu:
                ad = T["kapi"] or (U["kapi"] if U else "kapi_kemerli" if tarz == TAS else "kapi_ahsap")
            elif on_zemin and T["on"]:
                ad = T["on"]
            elif cephe_yuzu:
                ad = U["duz"] if U else "duvar_duz"
            elif sade:
                ad = T["duvar"]
            elif U:
                # Londra üslubu: kat ve yöne göre liste; konutta kapı sırasında her kapiAralik hücrede bir ek kapı
                if k == 0:
                    ek_kapi = (i == 0 and T["grup"] == "Konut" and U["kapiAralik"] > 0 and kapi is not None and y == kapi[0]
                               and (x - kapi[1]) % U["kapiAralik"] == 0)
                    liste = [U["kapi"]] if ek_kapi else (U["onZemin"] if i == 0 and U["onZemin"] else U["zemin"])
                elif k == 1:
                    liste = (U["onKat1"] if i == 0 and U["onKat1"] else U["kat1"]) or U["ust"]
                elif not dolu(x, y, k + 1):
                    liste = U["tavan"] or U["ust"]
                else:
                    liste = U["ust"]
                ad = liste[h % len(liste)]
            elif tarz == TAS:
                ad = TAS_UST[(h + (hk % 3 == 0)) % len(TAS_UST)]
            elif tarz == FACHWERK:
                ad = ("duvar_pencere" if h % 3 else "duvar_duz") if k == 0 else FW_UST[h % len(FW_UST)]
            elif k == 0:
                ad = "zemin_pencere" if h % 4 else "duvar_duz"
            else:
                ad = APT_UST[h % len(APT_UST)]
                if ad == "duvar_balkon" and hk % 2:
                    ad = "duvar_pencere"
            cizilen = ad
            if ad == "duvar_duz" and tarz == APARTMAN and not T["duvar"] and not cephe_yuzu:
                cizilen = duz_varyant(x, y, i, k, tohum)
            koy(cizilen, x, y, z, i, r, grup=dg)
            if cephe_yuzu and tarz != KAIDE:
                koy(T["cephe"], x, y, z, i, r)
            if ad in SAKSI_ALIR and hk % 5 == 0:
                koy("cicek_kutusu_p", x, y, z, i, r, grup=dg)
            if on_zemin and T["tente"] and (kapi_yuzu or T["on"]):
                koy("tente", x, y, z, i, r, grup=dg)
            if T["kepenk"] and on_zemin and (kapi_yuzu or T["on"]):
                koy("kepenk", x, y, z, i, r, grup="Kepenk")
            # bantlar. Kaide: kapı, dükkân cephesi ve ek kapı yüzlerinde yok (o parçaların kendi eteği var)
            if k == 0 and tarz != KAIDE and not sade and not kapi_yuzu and not (on_zemin and T["on"]) and not ek_kapi:
                kd = U["kaide"] if U else "kaide"
                if kd:
                    koy(kd, x, y, z, i, r, grup=dg)
            if U:
                sm = U["silmeZemin"] if k == 0 else U["silme"]
                if sm:
                    koy(sm, x, y, z + KAT, i, r)
            elif tarz == APARTMAN and not T["duvar"]:
                koy("silme", x, y, z + KAT, i, r)
            elif tarz == FACHWERK and k > 0:
                koy("silme_ahsap", x, y, z, i, r)
            # tohumlu küçük aksesuarlar (yan ve arka yüzlerde; ön sıra türün dekorlarına kalır)
            if k == 0 and i != 0 and tarz in (APARTMAN, FACHWERK) and not T["duvar"]:
                a = karma(x, y, i, tohum + 5) % 14
                if a == 0:
                    koy("fici", x, y, z, i, r, kay=(0.7, -A - 0.45))
                elif a == 1:
                    koy("saksi", x, y, z, i, r, kay=(-0.8, -A - 0.38))
                elif a == 2 and ad == "duvar_duz":
                    koy("sarmasik", x, y, z, i, r, grup=dg)
            if kapi_yuzu and not U:
                koy("paspas", x, y, z, i, r, kay=(0.0, -A - 0.75))
            if k == 0 and tarz == APARTMAN and ad == "zemin_pencere" and karma(x, y, i, tohum + 9) % 5 == 0:
                koy("fener", x, y, z, i, r, kay=(1.15, 0.0), grup=dg)
        # köşeler
        for i in range(4):
            (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
            a_bos, b_bos = not dolu(x + ax, y + ay, k), not dolu(x + bx, y + by, k)
            if a_bos and b_bos:
                if sade:
                    continue
                if U:
                    kose = U["koseZemin"] if k == 0 else U["kose"]
                else:
                    kose = "kose_tas" if tarz in (TAS, KAIDE) else "kose_ahsap" if (tarz == FACHWERK and k > 0) else "kose_dis"
                    if kose == "kose_dis" and karma(x, y, i, tohum + 71) % 4 == 0:
                        kose = "kose_boru"          # sütun boyunca aynı: yağmur iniş borusu çatıdan yere iner
                if kose:
                    koy(kose, x, y, z, i, r)
            elif not U and not a_bos and not b_bos and not dolu(x + ax + bx, y + ay + by, k):
                koy("kose_ic", x, y, z, i, r)
        # altı boş: kemerli geçit
        if alt_bos and not blok.get((x, y, k - 1)):
            for i, (dx, dy) in enumerate(YON):
                if not dolu(x + dx, y + dy, k - 1):
                    koy("kemer", x, y, z - KAT, i, r)
            for i in range(4):
                (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
                if not dolu(x + ax, y + ay, k - 1) and not dolu(x + bx, y + by, k - 1):
                    koy("kemer_ayak", x, y, z - KAT, i, r)

    # ---- kapı sırası: tabela bandı + yazı + simge, ön dekorlar, çatı parçası
    if kapi:
        ky, kx = kapi
        r = renk(blok[(kx, ky, 0)])
        sira = [kx]
        x = kx - 1
        while on_acik(x, ky):
            sira.insert(0, x)
            x -= 1
        x = kx + 1
        while on_acik(x, ky):
            sira.append(x)
            x += 1
        if metin:
            for x in sira:
                koy("tabela_bant", x, ky, 0.0, 0, r, grup="Duvar_On")
            W = len(sira) * HUCRE
            sol = 0.75 if T["simge"] else 0.2
            harfler = [harf_adi(ch) for ch in metin]
            px = min(0.075, (W - sol - 0.2) / (len(harfler) * 6 - 1))
            x0 = -A + sol + (W - sol - 0.2 - (len(harfler) * 6 - 1) * px) / 2
            for j, ha in enumerate(harfler):
                if ha:
                    koy(ha, sira[0], ky, 2.54 - 3.5 * px, 0, r, kay=(x0 + j * 6 * px, -A - 0.13), olcek=px, grup="Duvar_On")
            if T["simge"]:
                koy(T["simge"], sira[0], ky, 0.0, 0, r, kay=(-A + 0.4, 0.0), grup="Duvar_On")      # madalyon parçada hücrenin ortasında
        if T["aks"]:
            j = 0
            for x in sira:
                if x == kx:
                    continue
                ad = T["aks"][j % len(T["aks"])]
                j += 1
                if ad:
                    koy(ad, x, ky, 0.0, 0, r, kay=(0.0, -A - 1.05))
        if T["tepe"]:
            koy(T["tepe"], kx, ky, (kapi_ust + 1) * KAT, 0, r, kay=(0.0, 0.3))

    # ---- iç mekân (zemin kat): zemin kaplaması, tavan lambaları, türe göre mobilya
    plan = ic_plan(blok, kapi, T)
    for (x, y) in plan["hucreler"]:
        r = renk(blok[(x, y, 0)])
        koy(T["zemin"], x, y, 0.0, 0, r, grup="Ic")
        if (x + y) % 2 == (plan["kapi"][0] + plan["kapi"][1]) % 2:
            koy("ic_lamba", x, y, 0.0, 0, r, grup="Ampul")
        ad = plan["mobilya"].get((x, y))
        if ad:
            koy(ad, x, y, 0.0, 0, r, grup="Ic")
    if plan["kasa"] and T["kepenk"]:
        koy("ic_poset", plan["kasa"][0], plan["kasa"][1], 1.05, 0, renk(blok[plan["kasa"] + (0,)]), kay=(0.1, 0.45), grup="Poset")

    # ---- çatılar: her katta üstü açık hücreler; aynı tarz ve çatı tarzından bağlı bölgeler
    for k in sorted(set(b["k"] for b in bloklar)):
        ust = {(x, y): b for (x, y, kk), b in blok.items() if kk == k and b["tarz"] != MERDIVEN and not dolu(x, y, k + 1)}
        gorulen = set()
        for bas in sorted(ust):
            if bas in gorulen:
                continue
            anahtar = (ust[bas]["tarz"], ust[bas].get("ct", 0))
            bolge, yigin = [], [bas]
            gorulen.add(bas)
            while yigin:
                p = yigin.pop()
                bolge.append(p)
                for dx, dy in YON:
                    q = (p[0] + dx, p[1] + dy)
                    if q in ust and q not in gorulen and (ust[q]["tarz"], ust[q].get("ct", 0)) == anahtar:
                        gorulen.add(q)
                        yigin.append(q)
            # kaide / terasın üstü yerinde kalır; öbür çatılar (ve üst katlar) "Cati" grubunda kalkar
            cg = "Govde" if ust[bas]["tarz"] == KAIDE else "Cati"
            zorla[0] = cg
            n0, n1 = len(cokgenler), len(kutular)
            _cati(bolge, ust[bas], k, tohum, koy, renk, cokgenler, kutular, dolu, blok, bool(T["duvar"]))
            zorla[0] = None
            for i in range(n0, len(cokgenler)):
                cokgenler[i] = tuple(cokgenler[i]) + (cg,)
            for i in range(n1, len(kutular)):
                kutular[i] = tuple(kutular[i]) + (cg,)

    # ---- arsa süsleri (serbest yerleşim)
    for s in t.get("susler") or []:
        if s["ad"] not in PARCA:
            continue
        b0 = dict(renk=0, cati=s.get("renk", 0), tarz=0)
        r = renk(b0, s.get("renk", 0))
        r["K"] = VURGU_RENK[s.get("renk", 0) % len(VURGU_RENK)] if not T["vurgu"] else r["K"]
        yerler.append((s["ad"], s["x"], s["y"], 0.0, 0, 1.0, (0.0, 0.0), r, s.get("olcek", 1.0) or 1.0, math.radians(s.get("aci", 0.0)), "Govde", 0))
    return yerler, cokgenler, kutular


def _cati(bolge, b0, k, tohum, koy, renk, cokgenler, kutular, dolu, blok, sade):
    z = (k + 1) * KAT
    kume = set(bolge)
    xs, ys = [p[0] for p in bolge], [p[1] for p in bolge]
    gen, der = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
    tarz, ct = b0["tarz"], b0.get("ct", 0)
    r = renk(b0)

    def kenar_parcasi(ad, kose_ad=None):
        # kenar parçası hücrenin kendi rengini alır (farklı renkli sıra evlerde parapet / saçak eve göre)
        for (x, y) in bolge:
            rr = renk(blok[(x, y, k)])
            for i, (dx, dy) in enumerate(YON):
                if (x + dx, y + dy) not in kume:
                    koy(ad, x, y, z, i, rr)
            if kose_ad:
                for i in range(4):
                    (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
                    if (x + ax, y + ay) not in kume and (x + bx, y + by) not in kume:
                        koy(kose_ad, x, y, z, i, rr)

    if tarz == KAIDE:
        for (x, y) in bolge:
            koy("duz_cati", x, y, z, 0, r)
            for i, (dx, dy) in enumerate(YON):
                if (x + dx, y + dy) not in kume and not blok.get((x + dx, y + dy, k)) and not dolu(x + dx, y + dy, k + 1):
                    koy("korkuluk_demir", x, y, z, i, r)
        return
    if ct == CT_OTO:
        if len(bolge) == 1 and k >= 2:
            x, y = bolge[0]                                   # ince yüksek yığın: kule tepesi
            if tarz == TAS:
                koy("duz_cati", x, y, z, 0, r)
                for i in range(4):
                    koy("dama", x, y, z, i, r)
            else:
                koy("kule_kubbe" if karma(x, y, k, tohum) % 4 == 0 else "kule_sivri", x, y, z, 0, r)
            return
        if tarz == TAS:
            for (x, y) in bolge:
                koy("duz_cati", x, y, z, 0, r)
            kenar_parcasi("dama")
            return
        U = uslup(tarz)
        ct = U["cati"] if U else (CT_BESIK if tarz == FACHWERK else CT_KIRMA)
    if ct == CT_TERAS:
        for (x, y) in bolge:
            koy("duz_cati", x, y, z, 0, r)
        kenar_parcasi("korkuluk")
        Ut = uslup(tarz)
        modern = Ut is not None and Ut["ad"] == "modern"             # modern binada baca yok, klima ünitesi var
        for (x, y) in bolge:
            if karma(x, y, k, tohum + 21) % (3 if modern else 6) == 0:
                koy("cati_klima" if modern else "baca_ince", x, y, z + 0.9, 0, r, kay=(0.5, 0.4))
        return
    if ct == CT_TESTERE:
        for (x, y) in bolge:
            koy("cati_testere", x, y, z, 0, r)
        kenar_parcasi("sacak")
        return

    # ---- eğimli çatı: yarım hücrelik kafeste "dışarıya uzaklık" (kırma / mansart: 8 komşu; beşik: mahyaya dik eksen)
    besik = ct in (CT_BESIK, CT_BASAMAK)
    eksen = 0 if gen >= der else 1               # mahya ekseni (0: X boyunca)
    Hh = CATI_H[ct]
    ic = set()
    for (x, y) in bolge:
        for i in range(3):
            for j in range(3):
                ic.add((2 * x + i, 2 * y + j))
    d = {}
    for (px, py) in ic:
        cx0, cx1, cy0, cy1 = (px - 1) // 2, px // 2, (py - 1) // 2, py // 2
        if besik:
            if eksen == 0:
                once = (cx0, cy0) in kume or (cx1, cy0) in kume
                sonra = (cx0, cy1) in kume or (cx1, cy1) in kume
            else:
                once = (cx0, cy0) in kume or (cx0, cy1) in kume
                sonra = (cx1, cy0) in kume or (cx1, cy1) in kume
            sinir = not (once and sonra)
        else:
            sinir = not ((cx0, cy0) in kume and (cx1, cy0) in kume and (cx0, cy1) in kume and (cx1, cy1) in kume)
        d[(px, py)] = 0 if sinir else None
    komsular = ([(0, 1), (0, -1)] if eksen == 0 else [(1, 0), (-1, 0)]) if besik else \
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)]
    kuyruk = [p for p in ic if d[p] == 0]
    adim = 0
    while kuyruk:
        adim += 1
        yeni = []
        for (px, py) in kuyruk:
            for dx, dy in komsular:
                q = (px + dx, py + dy)
                if q in d and d[q] is None:
                    d[q] = adim
                    yeni.append(q)
        kuyruk = yeni
    ust_sinir = 2 if ct == CT_PARAPET else 4          # parapet arkası: iki kademeden sonra düz kurşun tepe (çapraz kırık çizgileri olmasın)
    for p in d:
        d[p] = min(d[p] or 0, ust_sinir)

    cm = "Pastel_Cati_" + r["C"].lstrip("#")
    for (x, y) in bolge:
        for i in range(2):
            for j in range(2):
                px, py = 2 * x + i, 2 * y + j
                h = [d[(px, py)], d[(px + 1, py)], d[(px + 1, py + 1)], d[(px, py + 1)]]
                h0 = min(h)
                desen = tuple(v - h0 for v in h)
                if desen not in CATI_DESEN:
                    k4 = [_kafes((px, py)), _kafes((px + 1, py)), _kafes((px + 1, py + 1)), _kafes((px, py + 1))]
                    p4 = [(k4[n][0], k4[n][1], z + Hh[h[n]]) for n in range(4)]
                    cokgenler.append(([p4[0], p4[1], p4[2]], cm, (0, 0, 1)))
                    cokgenler.append(([p4[0], p4[2], p4[3]], cm, (0, 0, 1)))
                    continue
                ad, don = CATI_DESEN[desen]
                zs = (Hh[h0 + 1] - Hh[h0]) if h0 < 4 else 1.0
                koy(ad, x, y, z + Hh[h0], don, r, zs=zs, kay=(-A + i * A, -A + j * A))
    if ct == CT_PARAPET:
        # Londra: saçak yerine parapet; ortak duvar hizalarında sıra baca; ön kenarda mansart pencereleri
        kenar_parcasi("parapet", "parapet_kose")
        a0, a1 = (min(xs), max(xs)) if eksen == 0 else (min(ys), max(ys))            # mahya ekseni boyunca
        c0, c1 = (min(ys), max(ys)) if eksen == 0 else (min(xs), max(xs))            # mahyaya dik
        cb = (c0 + c1) // 2
        kayma = ((c0 + c1) / 2.0 - cb) * HUCRE                                       # bölge çift sıra derinse baca mahya çizgisinde
        for a in range(a0, a1 + 1):
            hx, hy = (a, cb) if eksen == 0 else (cb, a)
            if (hx, hy) not in kume:
                continue
            ky = kayma if eksen == 0 else -kayma
            if (a - a0) % 2 == 0:
                koy("baca_sira", hx, hy, z, eksen, r, kay=(-A + 0.3, ky))
            if a == a1:
                koy("baca_sira", hx, hy, z, eksen, r, kay=(A - 0.3, ky))
        for (x, y) in bolge:
            if karma(x, y, k, tohum + 21) % 2 == 0 and (x, y - 1) not in kume and d[(2 * x + 1, 2 * y + 1)] >= 1 and not sade:
                koy("mansart_penceresi", x, y, z, 0, r, kay=(0.0, -A))
        return
    kenar_parcasi("sacak", "sacak_kose")
    # alınlıklar: bölgenin dış kenarında çatı sıfırdan yüksekse dik duvar (basamaklı tarzda çatıyı aşan kalın basamaklar)
    am = "Pastel_Siva_f6f0e2" if tarz == FACHWERK else "Pastel_Duvar_" + r["D"].lstrip("#")
    for (x, y) in bolge:
        for i, (dx, dy) in enumerate(YON):
            if (x + dx, y + dy) in kume:
                continue
            for s in range(2):
                if i == 0: a, b_ = (2 * x + s, 2 * y), (2 * x + s + 1, 2 * y)
                elif i == 1: a, b_ = (2 * x + 2, 2 * y + s), (2 * x + 2, 2 * y + s + 1)
                elif i == 2: a, b_ = (2 * x + 2 - s, 2 * y + 2), (2 * x + 1 - s, 2 * y + 2)
                else: a, b_ = (2 * x, 2 * y + 2 - s), (2 * x, 2 * y + 1 - s)
                ha, hb = Hh[d[a]], Hh[d[b_]]
                if ha <= 0 and hb <= 0:
                    continue
                pa, pb = _kafes(a), _kafes(b_)
                if ct == CT_BASAMAK:
                    ust = max(ha, hb) + 0.45
                    x0, x1 = min(pa[0], pb[0]), max(pa[0], pb[0])
                    y0, y1 = min(pa[1], pb[1]), max(pa[1], pb[1])
                    # duvar dış yüzden 2 cm dışarı, 28 cm içeri
                    if dx: x0, x1 = (pa[0] - 0.28, pa[0] + 0.02) if dx > 0 else (pa[0] - 0.02, pa[0] + 0.28)
                    else: y0, y1 = (pa[1] - 0.28, pa[1] + 0.02) if dy > 0 else (pa[1] - 0.02, pa[1] + 0.28)
                    kutular.append((x0, x1, y0, y1, z, z + ust, am))
                    kutular.append((x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, z + ust, z + ust + 0.09, SILME))
                else:
                    cokgenler.append(([(pa[0], pa[1], z), (pb[0], pb[1], z), (pb[0], pb[1], z + hb), (pa[0], pa[1], z + ha)], am, (dx, dy, 0)))
    # baca ve çatı penceresi (tohumlu)
    for (x, y) in bolge:
        hh = karma(x, y, k, tohum + 21) % 7
        orta = d[(2 * x + 1, 2 * y + 1)]
        if hh == 0:
            koy("baca", x, y, z + Hh[orta], 0, r, kay=(0.35, 0.2))
        elif hh == 1 and ct in (CT_KIRMA, CT_MANSART) and (x, y - 1) not in kume and orta >= 1 and not sade:
            koy("cati_penceresi", x, y, z, 0, r, kay=(0.0, -A))


def _renkli(ad, r, cam=0):
    """Rol adını gerçek renge çevirir: "Pastel_Duvar_@D" → "Pastel_Duvar_bcd9ea". Pencere camları üç ışık grubuna dağılır
    (Pastel_Cam_, Pastel_Cam2_, Pastel_Cam3_: GeceIsiklari her grubu ayrı saatte yakar / söndürür)."""
    if cam and ad.startswith("Pastel_Cam_"):
        yeni = "Pastel_Cam%d_%s" % (cam + 1, ad[11:])
        HEX[yeni] = HEX[ad]
        return yeni
    i = ad.find("_@")
    if i < 0:
        return ad
    hexv = r[ad[i + 2:]]
    yeni = ad[:i + 1] + hexv.lstrip("#")
    HEX[yeni] = hexv
    return yeni


EN_KISA = 0.13        # bundan kısa kenar çizilmez (yakın görünüm). C#: AtolyeKurucu.EnKisa
# v6: çizgiler inceldi. 1,35 m ve uzun kenarlar (bina köşesi, saçak, mahya, kat çizgisi) × 1,0; ayrıntı kenarları × 0,45.
# "Pastel_Iz_" malzemeli yüzeyler (tuğla / arduvaz sırası izi, cam kayıtları, korkuluk çubukları) hiç kontur almaz.
KALIN_BOY, KALIN, INCE = 1.35, 1.0, 0.45      # C#: AtolyeKurucu aynı sabitler


def kenarlar(g, en_kisa=EN_KISA):
    """Kalem çizgileri: sert kenarlar (iki yüz arası > 25°) ve açık kenarlar. Aynı düzlemde yan yana gelen parçaların
    ek yerleri (panel – panel) çizilmez. Dönen: (p0, p1, "#rrggbb") listesi. C# karşılığı: AtolyeKurucu.Kenarlar."""
    kayit = {}
    for f, mi in zip(g.f, g.fm):
        mat = g.mats[mi]
        if "_Kabuk_" in mat or "_Yazi_" in mat or "_Iz_" in mat:
            continue
        pts = [g.v[i] for i in f]
        n = B.norm(B.newell(pts))
        for i in range(len(pts)):
            a, b = pts[i], pts[(i + 1) % len(pts)]
            ka = (round(a[0] * 200), round(a[1] * 200), round(a[2] * 200))
            kb = (round(b[0] * 200), round(b[1] * 200), round(b[2] * 200))
            if ka == kb:
                continue
            ic = B.cross(n, B.norm(B.sub(b, a)))            # yüzün içine doğru (kenara dik, yüz düzleminde)
            kayit.setdefault((ka, kb) if ka < kb else (kb, ka), []).append((n, ic, mat, a, b))
    cikti = []
    for L in kayit.values():
        a, b = L[0][3], L[0][4]
        if (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2 < en_kisa * en_kisa:
            continue
        ciz = len(L) == 1
        if not ciz:
            dikis = keskin = False
            for i in range(len(L)):
                for j in range(i + 1, len(L)):
                    dn = B.dot(L[i][0], L[j][0])
                    if dn > 0.985 and B.dot(L[i][1], L[j][1]) < -0.5:
                        dikis = True
                    if dn < 0.906:
                        keskin = True
            ciz = keskin and not dikis
        if ciz:
            # kademeli kalınlık: büyük kenarlar (bina köşesi, saçak, kat çizgisi) kalın, ayrıntı kenarları ince
            boy2 = (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2
            cikti.append((a, b, cizgi_rengi(HEX[L[0][2]]), KALIN if boy2 >= KALIN_BOY * KALIN_BOY else INCE))
    return cikti


def _mevsim_atla(mat, r):
    """Önizlemede oyundaki _PastelKis davranışının taslağı: kışın (yaprak rengi KIS_RENGI) yaprak döken yapraklar
    ("_Dok_") çizilmez; dallardaki kar ("_DalKar_") yalnız kışın çizilir. Unity'de bunu gölgelendirici yapar (mesh aynı)."""
    kis = r.get("kis", False)
    return ("_Dok_" in mat and kis) or ("_DalKar_" in mat and not kis)


def _normal_tasi(g, normaller, aci=0.0, don=0):
    """Eklenecek yüzün köşe normallerini (parça ekseninde) hedef geometriye taşır: g.yuz'dan HEMEN ÖNCE çağrılır."""
    vn = getattr(g, "vn", None)
    if vn is None:
        vn = g.vn = []
    while len(vn) < len(g.v):
        vn.append(None)
    ca, sa = math.cos(aci), math.sin(aci)
    for n in normaller:
        if n is not None:
            nx, ny = n[0], n[1]
            if aci:
                nx, ny = nx * ca - ny * sa, nx * sa + ny * ca
            for _ in range(don % 4):
                nx, ny = -ny, nx
            n = (nx, ny, n[2])
        vn.append(n)


def bina(t, hafif=False, gizle=GIZLI_GRUP):
    """Tasarımdan tek geometri (metre, arsa ortalı, ön −Y, zemin z = 0). g.cizgiler: kalem çizgileri.
    gizle: atlanacak gruplar (varsayılan: iç mekân ve kepenk; iç görünüm için ör. ("Cati", "Duvar_On", "Kepenk", "Poset"))."""
    yerler, cokgenler, kutular = yerlesim(t)
    if hafif:
        gizle = GIZLI_GRUP
    cokgenler = [c for c in cokgenler if c[3] not in gizle]
    kutular = [c[:7] for c in kutular if c[7] not in gizle]
    g = G(t.get("ad", "bina") + ("_Hafif" if hafif else ""))
    for ad, cx, cy, z, don, zs, kay, r, olcek, aci, ygrup, cam in yerler:
        if ygrup in gizle:
            continue
        grup = PARCA_BILGI[ad][0]
        if hafif:
            if grup in ("ayrinti", "harf", "simge") or (grup == "cevre" and kucuk(ad)):
                if ad not in ("tas_merdiven", "baca"):
                    continue
            pg = hafif_parca(ad)
        else:
            pg = parca_g(ad)
        karo = ad in CATI_KAROLARI
        ca, sa = math.cos(aci), math.sin(aci)
        pvn = getattr(pg, "vn", None)
        for f, mi in zip(pg.f, pg.fm):
            if _mevsim_atla(pg.mats[mi], r):
                continue
            if pvn:
                _normal_tasi(g, [pvn[i] for i in f], aci, don)
            pts = []
            for i in f:
                px, py, pz = pg.v[i]
                px, py, pz = px * olcek, py * olcek, pz * olcek
                if aci:
                    px, py = px * ca - py * sa, px * sa + py * ca
                if karo:
                    px, py = px - A / 2, py - A / 2
                    for _ in range(don):
                        px, py = -py, px
                    px, py = px + A / 2 + kay[0], py + A / 2 + kay[1]
                else:
                    px, py = px + kay[0], py + kay[1]
                    for _ in range(don):
                        px, py = -py, px
                pts.append((cx + px, cy + py, z + pz * zs))
            g.yuz(pts, _renkli(pg.mats[mi], r, cam))
        g.sid += 1
    for pts, mat, n, _ in cokgenler:
        if mat not in HEX:
            HEX[mat] = "#" + mat.rsplit("_", 1)[1]
        nn = B.newell(pts)
        if nn[0] * n[0] + nn[1] * n[1] + nn[2] * n[2] < 0:
            pts = pts[::-1]
        g.yuz(pts, mat)
        g.sid += 1
    for x0, x1, y0, y1, z0, z1, mat in kutular:
        if mat not in HEX:
            HEX[mat] = "#" + mat.rsplit("_", 1)[1]
        kutu(g, x0, x1, y0, y1, z0, z1, mat, atla=())
    g.cizgiler = kenarlar(g, 0.9 if hafif else EN_KISA)
    return g


# ================================== ÖRNEK TASARIMLAR ==================================

def _yigin(x, y, kat, tarz, renk, cati, k0=0, ct=0):
    return [dict(x=x, y=y, k=k0 + k, tarz=tarz, renk=renk, cati=cati, ct=ct) for k in range(kat)]


def _blok(x0, x1, y0, y1, kat, tarz=APARTMAN, renk=0, cati=0, ct=0, k0=0):
    b = []
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            b += _yigin(x, y, kat, tarz, renk, cati, k0, ct)
    return b


def _sus(ad, x, y, aci=0.0, olcek=1.0, renk=0):
    return dict(ad=ad, x=x, y=y, aci=aci, olcek=olcek, renk=renk)


def _o(ad, tur, bloklar, tohum=1, tente=0, tabela="", susler=(), bolge=None):
    return dict(ad=ad, bolge=bolge or TUR[tur]["bolge"], tohum=tohum, dukkan=False, tur=tur, tabela=tabela, tente=tente,
                bloklar=bloklar, susler=list(susler))


def _th(n, hedef, bas=1):
    """Kapının ön sıradaki n aday hücreden hedef sıradakine (soldan, 0'dan) denk geldiği ilk tohum (bas ve sonrası).
    Kapı seçimi yerlesim()'deki karma ile aynı; bulunan tohum tasarıma yazılır (Unity'de ayrıca hesap yok)."""
    t = bas
    while karma(t, n, 3, 1) % n != hedef:
        t += 1
    return t


def ornekler():
    """53 örnek tasarım: Londra mimarisi (v6). Adlar, türler ve tabela adları v4 ile aynı; bloklar Londra üsluplarında."""
    P = CT_PARAPET
    o = []
    # ---- ilk sekiz (konut ve karışık)
    # Notting Hill: üç pastel boyalı sıra ev, altta çay salonu
    o.append(_o("pastel_kuleler", "dukkan", _blok(1, 1, 1, 2, 3, APARTMAN, 1, 6, P) + _blok(2, 2, 1, 2, 3, APARTMAN, 0, 6, P) + _blok(3, 3, 1, 2, 3, APARTMAN, 3, 6, P),
                _th(3, 1, 3), 9, "TEA ROOM", bolge="eski"))
    # banliyö: altı tuğla, üstü yarı ahşap (Tudor taklidi) ev
    o.append(_o("fachwerk_ev", "konut", _blok(1, 3, 1, 2, 1, VICTORIAN, 12, 8) + _blok(1, 3, 1, 2, 1, FACHWERK, 3, 8, k0=1), _th(3, 1, 5)))
    # işçi kulübesi: iki katlı küçük tuğla ev, ön bahçe
    o.append(_o("kucuk_ev", "konut", _blok(1, 2, 1, 2, 2, VICTORIAN, 10, 6, CT_BESIK), _th(2, 0, 2), bolge="ev",
                susler=[_sus("agac_meyve_1", 4.2, -1.5), _sus("cit_bitki", 4.2, -5.9), _sus("cali_2", 3.6, 3.5)]))
    # mews girişi: iki yanda mews evleri, ortada kemerli geçit
    b = []
    for y in (1, 2):
        b += _yigin(0, y, 2, MEWS, 1, 6, 0, P) + _yigin(1, y, 2, MEWS, 4, 6, 0, P) + _yigin(3, y, 2, MEWS, 0, 6, 0, P) + _yigin(4, y, 2, MEWS, 3, 6, 0, P)
        b += _yigin(2, y, 1, MEWS, 6, 6, k0=1, ct=P)
    o.append(_o("kemerli_gecit", "konut", b, _th(4, 1, 7)))
    # Belgravia: beyaz sıva teras, sütunlu portikolar, sürekli balkon
    o.append(_o("kaideli_ev", "konut", _blok(0, 4, 1, 2, 4, SIVA_TERAS, 14, 6, P), _th(5, 0, 11)))
    # Victorian gotik kuleli ev
    o.append(_o("tas_kule", "konut", _yigin(1, 2, 5, GOTIK, 12, 6) + _blok(2, 3, 2, 3, 2, GOTIK, 12, 6, CT_BESIK), _th(1, 0, 13)))
    o.append(_o("kose_apartmani", "dukkan", _blok(0, 3, 1, 2, 3, GEORGIAN, 10, 6, P) + _blok(0, 1, 3, 4, 3, GEORGIAN, 10, 6, P), _th(4, 1, 17), 6, "CORNER CAFE", bolge="karma"))
    # dar, yüksek kırmızı tuğla "mansion block", iki yanında tek katlı büfe kanatları
    o.append(_o("ince_kule", "dukkan", _blok(2, 2, 1, 2, 5, VICTORIAN, 12, 6, CT_BASAMAK) + _yigin(3, 1, 1, VICTORIAN, 12, 6, 0, CT_TERAS) + _yigin(1, 1, 1, VICTORIAN, 12, 6, 0, CT_TERAS),
                _th(3, 1, 19), 8, "KIOSK", bolge="kule"))
    # ---- dükkân ve restoranlar (altta Victoria tarzı dükkân cephesi, üstte Londra evi)
    o.append(_o("burger_queen", "burger", _blok(1, 3, 1, 2, 3, GEORGIAN, 10, 6, P), _th(3, 1, 21), 0))
    o.append(_o("mcdanolds", "mcdanolds", _blok(0, 3, 1, 2, 2, VICTORIAN, 12, 6, P) + _yigin(4, 1, 3, VICTORIAN, 12, 6), _th(5, 1, 22), 3))
    o.append(_o("kfg", "kfg", _blok(1, 3, 1, 2, 2, SIVA_TERAS, 14, 6, P), _th(3, 1, 23), 0))
    o.append(_o("pizza_hat", "pizza", _blok(1, 2, 1, 2, 3, GEORGIAN, 11, 6, P), _th(2, 0, 24), 7))
    o.append(_o("kebab_king", "doner", _blok(1, 3, 1, 2, 2, VICTORIAN, 13, 6, CT_KIRMA), _th(3, 1, 25), 3))
    o.append(_o("nonstop_gyros", "gyros", _blok(1, 2, 1, 2, 2, APARTMAN, 0, 6, P), _th(2, 1, 26), 8))
    o.append(_o("sushi_samurai", "sushi", _blok(1, 3, 1, 2, 2, SIVA_TERAS, 14, 6, P), _th(3, 1, 27), 9))
    o.append(_o("starbocks", "kafe", _blok(0, 3, 1, 2, 2, GEORGIAN, 11, 6, P) + _blok(3, 3, 3, 3, 2, GEORGIAN, 11, 6, P), _th(4, 1, 28), 6,
                susler=[_sus("agac_cinar_2", -5.4, 4.5), _sus("bollard", 5.9, -5.6), _sus("sokak_lambasi", 5.8, -6.4)]))
    o.append(_o("fish_ships", "balik", _blok(1, 3, 1, 2, 2, VICTORIAN, 12, 6, CT_BESIK), _th(3, 1, 29), 8))
    o.append(_o("snack_bar", "bufe", _blok(2, 3, 1, 1, 1, VICTORIAN, 10, 6, CT_TERAS), _th(2, 0, 30), 3))
    o.append(_o("villa_gourmet", "bistro", _blok(1, 3, 1, 2, 3, SIVA_TERAS, 14, 6, P), _th(3, 1, 31), 7))
    o.append(_o("old_pub", "pub", _blok(1, 3, 1, 2, 2, VICTORIAN, 13, 6, CT_KIRMA), _th(3, 1, 32)))
    o.append(_o("bakery", "firin", _blok(1, 2, 1, 2, 2, APARTMAN, 5, 6, P), _th(2, 0, 33), 9))
    o.append(_o("butcher", "kasap", _blok(2, 3, 1, 2, 2, VICTORIAN, 12, 6, CT_BESIK), _th(2, 1, 34), 0))
    o.append(_o("flowers", "cicekci", _blok(1, 2, 1, 2, 2, APARTMAN, 1, 6, P), _th(2, 0, 35), 6))
    o.append(_o("books", "kitapci", _blok(1, 2, 1, 2, 3, VICTORIAN, 13, 6, CT_BASAMAK), _th(2, 1, 36), 8))
    o.append(_o("pharmacy", "eczane", _blok(1, 3, 1, 2, 2, GEORGIAN, 11, 6, P), _th(3, 1, 37)))
    o.append(_o("corner_store_247", "bakkal24", _blok(1, 3, 1, 2, 2, VICTORIAN, 10, 6, CT_KIRMA), _th(3, 1, 38), 6))
    o.append(_o("mini_mart", "minimart", _blok(1, 2, 1, 2, 2, APARTMAN, 4, 6, P), _th(2, 0, 39), 3))
    # ---- marketler
    o.append(_o("sparx_express", "market", _blok(0, 3, 1, 3, 1, VICTORIAN, 10, 6, CT_TERAS), _th(4, 1, 40)))
    o.append(_o("intersparx", "supermarket", _blok(0, 4, 1, 3, 1, VICTORIAN, 12, 6, CT_TERAS) + _blok(1, 3, 2, 3, 1, VICTORIAN, 12, 6, CT_TERAS, k0=1), _th(5, 2, 41)))
    o.append(_o("market_24", "market24", _blok(1, 3, 1, 2, 2, GEORGIAN, 10, 6, P), _th(3, 1, 42)))
    o.append(_o("biosparx", "bio", _blok(1, 3, 1, 2, 2, APARTMAN, 4, 6, P), _th(3, 1, 43), 5))
    # ---- kamu
    o.append(_o("town_hall", "belediye", _blok(0, 4, 1, 2, 3, PORTLAND_T, 15, 6, P), _th(5, 2, 44)))
    o.append(_o("hospital", "hastane", _blok(0, 4, 1, 3, 3, VICTORIAN, 12, 6, CT_KIRMA), _th(5, 2, 45)))
    o.append(_o("fire_station", "itfaiye", _blok(0, 3, 1, 2, 2, VICTORIAN, 13, 6, CT_BESIK) + _yigin(4, 2, 4, VICTORIAN, 13, 6), _th(4, 3, 46)))
    o.append(_o("police", "polis", _blok(1, 3, 1, 2, 2, VICTORIAN, 12, 6, P), _th(3, 1, 47)))
    o.append(_o("post_office", "postane", _blok(1, 3, 1, 2, 2, PORTLAND_T, 15, 6, P), _th(3, 1, 48), 7))
    o.append(_o("school", "okul", _blok(0, 4, 1, 2, 2, GOTIK, 12, 6, CT_BESIK), _th(5, 2, 49),
                susler=[_sus("agac_cinar_1", -5.2, 4.6), _sus("agac_cinar_3", 5.0, 4.8), _sus("bank", 0, 3.6, 180)]))
    o.append(_o("library", "kutuphane", _blok(0, 4, 1, 2, 2, GOTIK, 13, 6, CT_BESIK) + _blok(1, 3, 3, 3, 2, GOTIK, 13, 6, CT_BESIK), _th(5, 2, 50)))
    # ---- sanayi (tuğla depo ve atölyeler)
    o.append(_o("depot", "depo", _blok(0, 4, 1, 3, 1, DEPO, 16, 6, CT_BESIK), _th(5, 2, 51)))
    o.append(_o("garage", "garaj", _blok(1, 3, 1, 2, 1, DEPO, 10, 6, CT_TERAS), _th(3, 2, 52)))
    o.append(_o("factory", "fabrika", _blok(0, 4, 1, 3, 2, DEPO, 16, 9, CT_TESTERE), _th(5, 2, 53)))
    o.append(_o("greenhouse", "sera", _blok(1, 3, 1, 3, 1, APARTMAN, 6, 7, CT_BESIK), _th(3, 1, 54),
                susler=[_sus("sebze_bahcesi", -5.4, 0, 90), _sus("kasalar", 5.3, -3)]))
    # ---- simge yapılar (14 m arsaya sığan boyda)
    o.append(_o("central_station", "gar", _blok(0, 4, 1, 2, 2, GOTIK, 12, 6, CT_BESIK) + _yigin(2, 1, 2, GOTIK, 12, 6, k0=2), _th(5, 2, 55)))
    o.append(_o("grand_hotel", "otel", _blok(1, 3, 1, 2, 5, SIVA_TERAS, 14, 6, P), _th(3, 1, 56), 7, bolge="kule"))
    o.append(_o("department_store", "avm", _blok(0, 4, 1, 3, 3, PORTLAND_T, 15, 6, P), _th(5, 2, 57), 6))
    o.append(_o("market_hall", "pazar", _blok(0, 4, 1, 3, 1, VICTORIAN, 12, 7, CT_BESIK), _th(5, 2, 58)))
    o.append(_o("opera", "opera", _blok(0, 4, 1, 3, 2, PORTLAND_T, 15, 6, P) + _blok(1, 3, 2, 3, 1, PORTLAND_T, 15, 9, P, k0=2), _th(5, 2, 59)))
    # ---- konut çeşitleri
    # Georgian sıra evler: stock tuğla, sıva zemin, parapet arkasında mansart, üç renkli kapı
    o.append(_o("terrace_houses", "sira_evi", _blok(0, 1, 1, 2, 3, GEORGIAN, 10, 6, P) + _blok(2, 3, 1, 2, 3, GEORGIAN, 11, 6, P) + _blok(4, 4, 1, 2, 3, GEORGIAN, 10, 7, P), _th(5, 0, 60)))
    # müstakil sıva villa
    o.append(_o("villa", "villa", _blok(1, 3, 1, 3, 2, SIVA_TERAS, 14, 6, CT_KIRMA), _th(3, 1, 61),
                susler=[_sus("agac_cinar_1", -5.3, 4.5), _sus("cit_bitki", -5.6, -5.6), _sus("cit_bitki", 5.6, -5.6), _sus("cicek_tarhi", -5.4, -2.0, 90), _sus("cesme", 5.2, 4.6, 0, 0.6)]))
    # "Pont Street Dutch": kırmızı tuğla, basamaklı Hollanda alınlığı
    o.append(_o("dutch_gable", "konut", _blok(2, 2, 1, 2, 4, VICTORIAN, 12, 6, CT_BASAMAK) + _blok(3, 3, 1, 2, 2, VICTORIAN, 13, 6, CT_BASAMAK), _th(2, 0, 62)))
    # ---- süslenmiş arsalar
    # Victorian ikiz ev (semi-detached): cumbalı, kırmızımsı kiremit, ön bahçe
    o.append(_o("bahceli_ev", "konut", _blok(1, 4, 2, 3, 2, VICTORIAN, 12, 8, CT_KIRMA), _th(4, 1, 63), bolge="ev", susler=[
        _sus("cit_bitki", -5.6, -5.4), _sus("agac_meyve_2", -5.6, 2.2), _sus("agac_cinar_2", -4.9, 5.4, 0, 0.8),
        _sus("cali_3", 0.2, -5.6), _sus("cicek_obegi", -1.2, -5.2), _sus("cicek_obegi", 4.4, -5.4, 40, 1.0, 1), _sus("bank", -5.6, -2.0, 90)]))
    o.append(_o("kafe_onu", "kafe", _blok(1, 3, 2, 3, 2, APARTMAN, 1, 6, P), _th(3, 1, 64), 9, "CAFE ROSE", susler=[
        _sus("masa", -3.2, -4.2, 20), _sus("masa", 3.3, -4.4, -15), _sus("masa", -5.3, -1.8, 80), _sus("saksi", -1.3, -5.9), _sus("saksi", 1.3, -5.9),
        _sus("agac_cinar_3", 5.6, -1.2, 0, 0.8), _sus("sokak_lambasi", -6.2, -6.2), _sus("cop_kutusu", 6.2, -6.2), _sus("bisiklet_park", 5.6, 2.2, 90)]))
    # bahçe meydanının köşesi: demir korkuluk, çınarlar, yeşil banklar, telefon kulübesi, posta kutusu
    o.append(_o("park_kosesi", "konut", [], 65, bolge="eski", susler=[
        _sus("cesme", 0, 0.6), _sus("bank", -3.2, 0.6, 90), _sus("bank", 3.2, 0.6, -90), _sus("bank", 0, 3.8, 180), _sus("agac_cinar_1", -4.6, 4.4),
        _sus("agac_cinar_2", 4.7, 4.2), _sus("agac_cinar_3", -4.9, -2.2, 0, 0.9), _sus("cali_1", 4.6, -2.6), _sus("cali_2", 2.4, 5.6), _sus("cicek_tarhi", 0, -2.6),
        _sus("sokak_lambasi", -6.3, -6.3), _sus("telefon_kulubesi", 5.4, -6.2), _sus("posta_kutusu", 3.9, -6.3), _sus("bollard", -4.6, -6.5), _sus("bollard", -3.0, -6.5),
        _sus("demir_korkuluk", -5.6, -5.4), _sus("demir_korkuluk", -2.8, -5.4), _sus("demir_kapi", 0.0, -5.4), _sus("demir_korkuluk", 2.8, -5.4), _sus("demir_korkuluk", 5.6, -5.4)]
        + [_sus("cicek_obegi" if i % 2 else "ot_obegi", -1.8 + (i * 37 % 40) / 10.0, 1.6 + (i * 53 % 30) / 10.0, i * 40, 1.0, i) for i in range(6)]))
    return o


# ================================== JSON (Unity) ==================================

# ================================== SAHNE (zemin + birden çok bina) ==================================

SAHNE_RENK = dict(D="#f6f0e2", d="#e3dccb", C="#c8553f", c="#ad4835", K="#3f7d74", T="#c8553f", k=cizgi_rengi(YAPRAK_RENK[0]), kis=False)
SAHNE_RENK.update(yaprak_tonlari(YAPRAK_RENK[0]))


def sahne_merkezi(n, x, y):
    return ((x - (n - 1) / 2.0) * HUCRE, (y - (n - 1) / 2.0) * HUCRE)


def zemin_yerlesim(sh):
    """Sahnenin zemin parçaları (yer listesi; biçim yerlesim() ile aynı). Boyanmamış hücre çim, kademe 0.
    Yol: komşuya göre şerit / yaya geçidi / rögar; yol olmayan kenarına kaldırım şeridi + bordür. Su: taş rıhtım. Çit: komşu çite kol.
    Kademe farkı olan kenara toprak yamaç (çim, patika, tarh, çit) ya da taş istinat duvarı (yol, kaldırım, meydan, su); dış kenara etek.
    C# karşılığı: AtolyeKurucu.ZeminYerlesim (karma dahil AYNI)."""
    n = sh.get("n", 16)
    tohum = sh.get("tohum", 1)
    hz = {}
    for x in range(n):
        for y in range(n):
            hz[(x, y)] = ("cim", 0)
    for h in sh.get("zemin") or []:
        if (h["x"], h["y"]) in hz:
            hz[(h["x"], h["y"])] = (h.get("t") or "cim", max(0, min(2, h.get("k", 0))))
    yerler = []
    r = SAHNE_RENK

    def tur(x, y):
        v = hz.get((x, y))
        return v[0] if v else None

    def koy(ad, x, y, z, don=0, zs=1.0):
        cx, cy = sahne_merkezi(n, x, y)
        yerler.append((ad, cx, cy, z, don % 4, zs, (0.0, 0.0), r, 1.0, 0.0, "Govde", 0))

    for (x, y) in sorted(hz):
        t, k = hz[(x, y)]
        z = k * KADEME
        kom = [tur(x + dx, y + dy) for dx, dy in YON]
        if t == "yol":
            koy("z_yol", x, y, z)
            yol = [v == "yol" for v in kom]
            c = sum(yol)
            yatay, dikey = yol[1] and yol[3], yol[0] and yol[2]
            eksen = None                                         # 0: y ekseni boyunca (ön–arka), 1: x ekseni boyunca
            if c == 1:
                eksen = 0 if (yol[0] or yol[2]) else 1
            elif yatay != dikey:
                eksen = 1 if yatay else 0
            if eksen is not None:
                kavsak = False                                   # yol boyunca komşu hücre kavşak içi mi (iki yönde de süren yol)
                for i in ((1, 3) if eksen == 1 else (0, 2)):
                    if yol[i]:
                        qx, qy = x + YON[i][0], y + YON[i][1]
                        q = [tur(qx + dx, qy + dy) == "yol" for dx, dy in YON]
                        if q[0] and q[2] and q[1] and q[3]:
                            kavsak = True
                yan0, yan1 = (yol[0], yol[2]) if eksen == 1 else (yol[3], yol[1])
                if kavsak:
                    koy("z_gecit", x, y, z, eksen)
                elif not yan0 and not yan1:
                    koy("z_serit", x, y, z, eksen)               # tek şeritli yol: ortada kesikli çizgi
                elif yan1 and not yan0:
                    koy("z_serit_kenar", x, y, z, 0 if eksen == 1 else 3)   # iki şeritli yol: çizgi iki hücrenin sınırında (bir kez)
                if not kavsak and karma(x, y, 3, tohum) % 9 == 0:
                    koy("z_rogar", x, y, z, eksen)
            for i in range(4):
                if not yol[i]:
                    koy("z_bordur", x, y, z, i)
            for i in range(4):
                (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
                a, b = yol[(i + 3) % 4], yol[i]
                if not a and not b:
                    koy("z_bordur_kose", x, y, z, i)
                elif a and not b:
                    koy("z_bordur_uc", x, y, z, i)
                elif b and not a:
                    koy("z_bordur_son", x, y, z, (i + 3) % 4)
                elif tur(x + ax + bx, y + ay + by) != "yol":
                    koy("z_bordur_kose", x, y, z, i)
        elif t == "kaldirim":
            koy("z_kaldirim", x, y, z)
        elif t == "meydan":
            koy("z_meydan_%d" % (karma(x, y, 5, tohum) % 2), x, y, z)
        elif t == "patika":
            koy("z_patika", x, y, z, karma(x, y, 6, tohum) % 4)
        elif t == "su":
            koy("z_su", x, y, z, karma(x, y, 7, tohum) % 4)
            su = [v == "su" for v in kom]
            for i in range(4):
                if not su[i]:
                    koy("z_su_kenar", x, y, z, i)
            for i in range(4):
                (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
                if not (su[(i + 3) % 4] and su[i] and tur(x + ax + bx, y + ay + by) == "su"):
                    koy("z_su_kose", x, y, z, i)
        elif t == "tarh":
            koy("z_tarh_%d" % (karma(x, y, 8, tohum) % 2), x, y, z)
        elif t == "cit":
            koy("z_cim_0", x, y, z)
            koy("z_cit_orta", x, y, z)
            for i in range(4):
                if kom[i] == "cit":
                    koy("z_cit_kol", x, y, z, i)
        else:
            koy("z_cim_%d" % (karma(x, y, 1, tohum) % 3), x, y, z, karma(x, y, 2, tohum) % 4)
        # yükselti kenarları ve sahnenin dış eteği
        for i, (dx, dy) in enumerate(YON):
            v = hz.get((x + dx, y + dy))
            if v is None:
                koy("z_etek", x, y, z, i, z + ETEK)
            elif v[1] < k:
                koy("z_istinat" if t in SERT_ZEMIN else "z_yamac", x, y, z, i, (k - v[1]) * KADEME)
    return yerler


def _yer_ekle(g, yer, hafif=False):
    """Bir yerleşimi geometriye ekler (bina() ile aynı dönüşüm)."""
    ad, cx, cy, z, don, zs, kay, r, olcek, aci, ygrup, cam = yer
    pg = hafif_parca(ad) if hafif else parca_g(ad)
    ca, sa = math.cos(aci), math.sin(aci)
    pvn = getattr(pg, "vn", None)
    for f, mi in zip(pg.f, pg.fm):
        if _mevsim_atla(pg.mats[mi], r):
            continue
        if pvn:
            _normal_tasi(g, [pvn[i] for i in f], aci, don)
        pts = []
        for i in f:
            px, py, pz = pg.v[i]
            px, py, pz = px * olcek, py * olcek, pz * olcek
            if aci:
                px, py = px * ca - py * sa, px * sa + py * ca
            px, py = px + kay[0], py + kay[1]
            for _ in range(don):
                px, py = -py, px
            pts.append((cx + px, cy + py, z + pz * zs))
        g.yuz(pts, _renkli(pg.mats[mi], r, cam))
    g.sid += 1


def sahne_kademe(sh, x, y):
    for h in sh.get("zemin") or []:
        if h["x"] == x and h["y"] == y:
            return max(0, min(2, h.get("k", 0)))
    return 0


def sahne(sh, tasarimlar=None, gece=None):
    """Sahnenin tek geometrisi: zemin + binalar (tasarım adıyla; 5 × 5 arsanın ortası verilen hücrede, don × 90° döner) + süsler.
    g.cizgiler: kalem çizgileri. gece: (malzeme, bina sırası, açık mı) → malzeme adı çeviren işlev (gece önizlemesi)."""
    n = sh.get("n", 16)
    tasarimlar = tasarimlar or {t["ad"]: t for t in ornekler()}
    g = G(sh.get("ad", "sahne"))
    zg = G("zemin")
    for yer in zemin_yerlesim(sh):
        _yer_ekle(zg, yer)
    susler = sh.get("susler") or []
    if susler:
        sg = bina(dict(ad="sus", tohum=sh.get("tohum", 1), tur="konut", bloklar=[], susler=susler))
        svn = getattr(sg, "vn", None)
        for f, mi in zip(sg.f, sg.fm):
            if svn:
                _normal_tasi(zg, [svn[j] if j < len(svn) else None for j in f])
            zg.yuz([sg.v[j] for j in f], sg.mats[mi])
    cz = kenarlar(zg)
    zvn = getattr(zg, "vn", None)
    for f, mi in zip(zg.f, zg.fm):
        if zvn:
            _normal_tasi(g, [zvn[j] if j < len(zvn) else None for j in f])
        g.yuz([zg.v[j] for j in f], gece(zg.mats[mi], -1, True) if gece else zg.mats[mi])
    if gece:
        cz = [(a, b, "#15131f", w) for a, b, c, w in cz]
    for bi, b in enumerate(sh.get("binalar") or []):
        t = tasarimlar.get(b["tasarim"])
        if not t:
            continue
        acik = not b.get("kapali")
        bg = bina(t, gizle=GIZLI_GRUP if acik else ("Ic", "Ampul", "Poset"))
        ox, oy = sahne_merkezi(n, b["x"], b["y"])
        oz = sahne_kademe(sh, b["x"], b["y"]) * KADEME + KALDIRIM_Z
        don = b.get("don", 0) % 4

        def cevir(p):
            px, py = p[0], p[1]
            for _ in range(don):
                px, py = -py, px
            return (ox + px, oy + py, oz + p[2])
        bvn = getattr(bg, "vn", None)
        for f, mi in zip(bg.f, bg.fm):
            if bvn:
                _normal_tasi(g, [bvn[j] if j < len(bvn) else None for j in f], 0.0, don)
            g.yuz([cevir(bg.v[j]) for j in f], gece(bg.mats[mi], bi, acik) if gece else bg.mats[mi])
        cz += [(cevir(a), cevir(b2), "#15131f" if gece else c, w) for a, b2, c, w in bg.cizgiler]
    g.cizgiler = cz
    return g


def _dikdortgen(x0, x1, y0, y1, t, k=0):
    return [dict(x=x, y=y, t=t, k=k) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)]


def _zemin_birlestir(*parcalar):
    """Sonraki boya öncekinin üstüne yazar."""
    d = {}
    for l in parcalar:
        for h in l:
            d[(h["x"], h["y"])] = h
    return [d[k] for k in sorted(d)]


def _b(tasarim, x, y, don=0, kapali=False):
    v = dict(tasarim=tasarim, x=x, y=y, don=don)
    if kapali:
        v["kapali"] = True
    return v


def _sira(ad, x0, x1, y, aci=0.0):
    """Hücre ortalarına dizilmiş süsler (x0…x1 hücreleri, y sabit; sahne metresi). 16 hücrelik sahne için."""
    return [_sus(ad, (x - 7.5) * HUCRE, y, aci) for x in range(x0, x1 + 1)]


def sahneler():
    """Örnek sahneler (16 × 16 hücre = 44,8 m). y = 0 ön (kameraya yakın). v6: Londra."""
    S = []
    # 1) Kasaba meydanı: ortada arnavut kaldırımı meydan + çeşme, çevresinde yol, dükkânlar, köşede demir korkuluklu park
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 6, 7, "yol"), _dikdortgen(5, 6, 0, 15, "yol"),
        _dikdortgen(7, 15, 8, 15, "kaldirim"), _dikdortgen(7, 12, 0, 5, "meydan"), _dikdortgen(0, 4, 8, 15, "kaldirim"),
        _dikdortgen(13, 15, 0, 5, "cim"), _dikdortgen(0, 4, 0, 5, "cim"), _dikdortgen(1, 3, 2, 2, "patika"), _dikdortgen(2, 2, 0, 5, "patika"),
        _dikdortgen(3, 4, 4, 5, "tarh"), _dikdortgen(13, 15, 4, 5, "tarh"))
    S.append(dict(ad="kasaba_meydani", n=16, tohum=3, zemin=z,
                  binalar=[_b("burger_queen", 8, 10), _b("starbocks", 12, 10), _b("books", 2, 10), _b("pastel_kuleler", 2, 14, kapali=True),
                           _b("old_pub", 8, 14), _b("dutch_gable", 13, 14), _b("flowers", 14, 2, 3)],
                  susler=[_sus("cesme", 5.6, -12.6), _sus("bank", 2.8, -15.2, 0), _sus("bank", 8.4, -15.2, 0), _sus("sokak_lambasi", -1.0, -6.6),
                          _sus("sokak_lambasi", 13.0, -6.6), _sus("agac_cinar_1", -18.5, -13.0), _sus("agac_cinar_2", -11.0, -19.0),
                          _sus("agac_cinar_3", -11.5, -10.5, 0, 0.85), _sus("bank", -15.2, -16.0, 90), _sus("agac_cinar_1", 18.5, -17.0), _sus("masa", 10.0, -9.0),
                          _sus("cop_kutusu", -1.6, -10.2), _sus("heykel", 12.4, -16.5), _sus("telefon_kulubesi", -0.4, -16.6), _sus("posta_kutusu", -0.4, -14.6),
                          _sus("belisha", -9.6, -6.6), _sus("belisha", -3.0, -6.6), _sus("belisha", -9.6, 1.4), _sus("bollard", -0.8, -12.4), _sus("bollard", -0.8, -8.6)]
                  + _sira("demir_korkuluk", 0, 4, -6.9) ))
    # 2) Nehir kenarı (Thames): önde su, rıhtım kaldırımı, arkada Georgian sıra evler ve balıkçı; rıhtımda lambalar
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 2, 4, "su"), _dikdortgen(0, 15, 5, 6, "kaldirim"), _dikdortgen(0, 15, 7, 8, "yol"), _dikdortgen(0, 15, 9, 15, "kaldirim"),
        _dikdortgen(0, 15, 0, 1, "cim"), _dikdortgen(10, 11, 0, 8, "yol"), _dikdortgen(10, 11, 2, 4, "meydan"), _dikdortgen(13, 15, 12, 15, "cim"),
        _dikdortgen(0, 2, 0, 0, "tarh"))
    S.append(dict(ad="nehir_kenari", n=16, tohum=5, zemin=z,
                  binalar=[_b("terrace_houses", 2, 11), _b("fish_ships", 7, 11), _b("dutch_gable", 12, 11, kapali=True), _b("fachwerk_ev", 14, 14)],
                  susler=[_sus("kayik", -14.0, -14.2, 80), _sus("kayik", 2.0, -13.6, 100), _sus("iskele", -6.0, -9.8), _sus("sokak_lambasi", -18.0, -7.6),
                          _sus("sokak_lambasi", -4.0, -7.6), _sus("bank", -11.0, -7.4, 180), _sus("agac_cinar_1", -19.5, -20.0), _sus("agac_cinar_2", -8.0, -20.5),
                          _sus("agac_cinar_3", 18.0, -20.0), _sus("fici", 1.0, -7.0), _sus("kasalar", 3.4, -6.8), _sus("bisiklet_park", 17.0, -7.2),
                          _sus("telefon_kulubesi", 4.6, 3.4), _sus("posta_kutusu", 13.4, 3.2), _sus("bollard", -14.5, -4.0), _sus("bollard", -11.5, -4.0), _sus("bollard", -8.5, -4.0)]))
    # 3) Banliyö sokağı: ortada yol, iki yanında cumbalı ikiz evler, çit bitkileri, çınarlar
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 7, 8, "yol"), _dikdortgen(0, 15, 6, 6, "kaldirim"), _dikdortgen(0, 15, 9, 9, "kaldirim"),
        _dikdortgen(0, 1, 0, 1, "tarh"), _dikdortgen(14, 15, 0, 1, "tarh"), _dikdortgen(5, 5, 12, 15, "cit"), _dikdortgen(11, 11, 12, 15, "cit"))
    S.append(dict(ad="banliyo_sokagi", n=16, tohum=7, zemin=z,
                  binalar=[_b("bahceli_ev", 2, 12), _b("kucuk_ev", 8, 13), _b("fachwerk_ev", 13, 13), _b("kucuk_ev", 3, 2, 2), _b("bahceli_ev", 12, 3, 2)],
                  susler=[_sus("sokak_lambasi", -15.0, 4.4), _sus("sokak_lambasi", 5.0, 4.4), _sus("posta_kutusu", -9.0, 4.6), _sus("agac_cinar_1", -1.0, 17.0),
                          _sus("agac_cinar_2", 20.0, 17.5), _sus("agac_cinar_3", -2.0, -17.0), _sus("agac_meyve_2", 3.5, -19.0), _sus("durak", 11.0, -3.6, 180),
                          _sus("cop_kutusu", 6.6, -3.4), _sus("telefon_kulubesi", 17.5, 4.6), _sus("agac_cinar_2", -19.0, -3.6, 0, 0.8), _sus("agac_cinar_1", 0.0, -3.6, 0, 0.8)]))
    # 4) Tepe kasabası: arkada iki kademe yükselen teraslar (istinat duvarı, yamaç), önde yol ve su
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 0, 1, "su"), _dikdortgen(0, 15, 2, 2, "kaldirim"), _dikdortgen(0, 15, 3, 4, "yol"), _dikdortgen(0, 15, 5, 9, "kaldirim"),
        _dikdortgen(0, 15, 10, 15, "cim", 1), _dikdortgen(0, 6, 10, 15, "meydan", 1), _dikdortgen(9, 15, 13, 15, "cim", 2), _dikdortgen(7, 8, 10, 12, "tarh", 1),
        _dikdortgen(12, 13, 5, 9, "cim"), _dikdortgen(12, 12, 5, 9, "cit"))
    S.append(dict(ad="tepe_kasabasi", n=16, tohum=9, zemin=z,
                  binalar=[_b("town_hall", 3, 13), _b("tas_kule", 12, 14), _b("bakery", 2, 7), _b("pharmacy", 8, 7), _b("kemerli_gecit", 13, 8)],
                  susler=[_sus("agac_cinar_1", 8.0, 8.0), _sus("agac_cinar_2", 20.0, 9.0), _sus("agac_meyve_1", 16.0, 14.0), _sus("sokak_lambasi", -8.0, -15.4),
                          _sus("sokak_lambasi", 8.0, -15.4), _sus("bank", 0.0, -15.6, 180), _sus("kayik", -12.0, -20.4, 90), _sus("posta_kutusu", 3.0, -6.6),
                          _sus("telefon_kulubesi", -9.5, -6.4)]))
    # 5) Georgian meydanı (garden square): ortada demir korkuluklu, çınarlı bahçe; üç yanında sıra evler, önde sokak
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 0, 15, "kaldirim"), _dikdortgen(4, 11, 4, 11, "yol"), _dikdortgen(5, 10, 5, 10, "cim"),
        _dikdortgen(0, 15, 4, 4, "yol"), _dikdortgen(4, 4, 0, 3, "yol"), _dikdortgen(11, 11, 0, 3, "yol"),
        _dikdortgen(7, 7, 5, 10, "patika"), _dikdortgen(5, 10, 8, 8, "patika"), _dikdortgen(0, 0, 5, 15, "cim"), _dikdortgen(15, 15, 5, 15, "cim"),
        _dikdortgen(5, 10, 0, 1, "meydan"), _dikdortgen(6, 6, 6, 6, "tarh"), _dikdortgen(9, 9, 9, 9, "tarh"))
    kor = []
    for i in range(5, 11):
        u = (i - 7.5) * HUCRE
        kor.append(_sus("demir_kapi" if i == 7 else "demir_korkuluk", u, -8.25))
        kor.append(_sus("demir_kapi" if i == 7 else "demir_korkuluk", u, 8.25, 180))
        kor.append(_sus("demir_kapi" if i == 8 else "demir_korkuluk", -8.25, u, -90))
        kor.append(_sus("demir_kapi" if i == 8 else "demir_korkuluk", 8.25, u, 90))
    S.append(dict(ad="georgian_meydan", n=16, tohum=11, zemin=z,
                  binalar=[_b("kaideli_ev", 2, 14), _b("terrace_houses", 7, 14), _b("kaideli_ev", 12, 14),
                           _b("terrace_houses", 1, 10, 1), _b("terrace_houses", 14, 10, 3), _b("kemerli_gecit", 1, 2, 1), _b("kemerli_gecit", 14, 2, 3)],
                  susler=kor + [_sus("agac_cinar_1", -5.6, -5.4), _sus("agac_mese_1", 5.8, -5.2), _sus("agac_kestane_1", -5.4, 5.6), _sus("agac_cinar_3", 5.6, 5.8),
                                _sus("agac_cinar_2", 4.6, -0.4, 0, 0.8), _sus("bank", -3.0, 2.6, 180), _sus("bank", 3.4, 2.6, 180), _sus("bank", -0.6, -4.4, 90),
                                _sus("cali_ortanca", -6.6, 0.6), _sus("cali_simsir", 6.8, 2.6), _sus("cali_lavanta", 2.6, 6.8), _sus("cali_ortanca", -2.6, 6.6), _sus("heykel", 2.6, -2.4, 0, 0.8),
                                _sus("sokak_lambasi", -12.4, -12.4), _sus("sokak_lambasi", 12.4, -12.4), _sus("sokak_lambasi", -12.4, 12.3), _sus("sokak_lambasi", 12.4, 12.3),
                                _sus("telefon_kulubesi", 0.6, -18.4), _sus("posta_kutusu", -1.4, -18.2), _sus("bank", -5.6, -19.0), _sus("bank", 6.4, -19.0),
                                _sus("belisha", -6.9, -12.6), _sus("belisha", 6.9, -12.6), _sus("belisha", -6.9, -7.2), _sus("belisha", 6.9, -7.2),
                                _sus("agac_cinar_3", -4.0, -20.6, 0, 0.85), _sus("agac_cinar_1", 4.4, -20.8, 0, 0.85),
                                _sus("bollard", -7.4, -14.6), _sus("bollard", -3.6, -14.6), _sus("bollard", 0.2, -14.6), _sus("bollard", 4.0, -14.6), _sus("bollard", 7.4, -14.6)]))
    # 6) High street: iki şeritli ana cadde, arkada Victoria tarzı dükkân sırası ve pub; kavşakta zebra geçidi ve Belisha
    #    fenerleri; önde otobüs durağı, posta kutusu, telefon kulübesi, büfe, küçük yeşil alan
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 0, 15, "kaldirim"), _dikdortgen(0, 15, 6, 7, "yol"), _dikdortgen(8, 8, 0, 15, "yol"),
        _dikdortgen(0, 7, 11, 15, "cim"), _dikdortgen(9, 15, 11, 15, "cim"), _dikdortgen(0, 3, 0, 2, "cim"), _dikdortgen(12, 15, 0, 1, "meydan"),
        _dikdortgen(0, 0, 0, 2, "cit"), _dikdortgen(0, 3, 0, 0, "cit"), _dikdortgen(1, 2, 1, 1, "tarh"))
    S.append(dict(ad="high_street", n=16, tohum=13, zemin=z,
                  binalar=[_b("burger_queen", 1, 10), _b("pizza_hat", 4, 10), _b("old_pub", 6, 10), _b("books", 10, 10), _b("market_24", 12, 10),
                           _b("bakery", 15, 10), _b("snack_bar", 12, 2, 2)],
                  susler=[_sus("durak", -11.0, -5.4, 180), _sus("posta_kutusu", -4.4, -5.6), _sus("telefon_kulubesi", 4.4, -6.0), _sus("cop_kutusu", -6.2, -5.4),
                          _sus("sokak_lambasi", -17.0, -4.5), _sus("sokak_lambasi", -2.6, -4.5), _sus("sokak_lambasi", 4.4, 1.5), _sus("sokak_lambasi", 18.0, 1.5),
                          _sus("sokak_lambasi", -11.0, 1.5), _sus("sokak_lambasi", 12.0, -4.5),
                          _sus("belisha", -2.8, -4.5), _sus("belisha", -2.8, 1.5), _sus("belisha", 4.3, -4.5), _sus("belisha", 4.3, 1.5),
                          _sus("bollard", -1.6, -5.6), _sus("bollard", -1.6, -7.6), _sus("bollard", 3.2, -5.6), _sus("bollard", 3.2, -7.6),
                          _sus("agac_cinar_1", -15.4, -17.4), _sus("agac_cinar_2", 7.5, -11.5, 0, 0.85), _sus("agac_cinar_3", 19.6, -9.0, 0, 0.85),
                          _sus("bank", -15.5, -12.2, 180), _sus("bank", 15.0, -6.0), _sus("masa", 9.5, -17.5), _sus("masa", 15.5, -19.0, 40),
                          _sus("bisiklet_park", -8.0, -9.0), _sus("agac_mese_2", -12.0, 17.5), _sus("agac_ihlamur_1", 8.0, 18.0), _sus("agac_hus_1", 17.5, 16.0), _sus("agac_hus_2", 14.5, 18.5),
                          _sus("agac_genc_1", -14.0, 1.9), _sus("agac_genc_2", -6.2, 1.9), _sus("agac_genc_1", 9.6, 1.9), _sus("agac_genc_2", 15.4, 1.9),
                          _sus("cali_1", -19.0, 13.0), _sus("cali_3", 5.5, 13.0),
                          _sus("agac_cinar_3", -9.0, -14.5, 0, 0.9), _sus("agac_cinar_2", -4.5, -19.0, 0, 0.85), _sus("bank", -9.0, -17.4, 180),
                          _sus("cicek_kovalari", 10.4, 2.6), _sus("menu_panosu", -6.5, 2.4)]))
    return S


def sahne_json_yaz(klasor):
    os.makedirs(klasor, exist_ok=True)
    for sh in sahneler():
        with open(os.path.join(klasor, "sahne_%s.json" % sh["ad"]), "w", encoding="utf-8") as f:
            json.dump(sh, f, ensure_ascii=False, separators=(",", ":"))
    print("YAZILDI", klasor, len(sahneler()), "sahne")


def _unity_parca(ad, g):
    """Parçayı Unity'nin okuyacağı düz listelere çevirir. Koordinatlar MODEL ekseninde (metre, Z yukarı, ön −Y) kalır:
    eksen çevirisini C# tarafı yapar. Yüzler üçgenlenir; her malzeme bir alt-mesh."""
    v = []
    alt = {}
    vn = getattr(g, "vn", None)
    kaynak = {}          # yumuşak (g.vn'si olan) köşeler konuma göre paylaşılır: Unity normalleri yumuşak hesaplar, dosya küçülür
    for f, mi in zip(g.f, g.fm):
        sira = []
        for i in f:
            p = g.v[i]
            hb = 2 if "_Yaprak_" in g.mats[mi] else 3        # tek tek yapraklar: santimetre yeter (dosya küçük kalsın)
            q = (round(p[0], hb), round(p[1], hb), round(p[2], hb))
            if vn and i < len(vn) and vn[i] is not None:
                k = ("_Kabuk_" in g.mats[mi],) + q
                j = kaynak.get(k)
                if j is None:
                    j = kaynak[k] = len(v) // 3
                    v += list(q)
            else:
                j = len(v) // 3
                v += [round(p[0], 4), round(p[1], 4), round(p[2], 4)]
            sira.append(j)
        uc = alt.setdefault(g.mats[mi], [])
        for i in range(1, len(sira) - 1):
            if len({sira[0], sira[i], sira[i + 1]}) == 3:
                uc += [sira[0], sira[i], sira[i + 1]]
    grup, aciklama, hedef = PARCA_BILGI[ad]
    return dict(ad=ad, grup=grup, aciklama=aciklama, hedef=hedef, kucuk=bool(kucuk(ad)), sinir=IC_SINIR.get(ad, 0), v=v, alt=[dict(malzeme=m, uc=u) for m, u in alt.items()])


def json_yaz(yol):
    parcalar = [_unity_parca(ad, parca_g(ad)) for ad in PARCA]
    hafifler = [_unity_parca(ad, hafif_parca(ad)) for ad in PARCA]
    uzaklar = [_unity_parca(ad, uzak_parca(ad)) for ad in PARCA if ad in PARCA_UZAK]      # en uzak görünüm (yalnız ağaç ve çalılar)
    veri = dict(surum=5, uzaklar=uzaklar, kisRengi=KIS_RENGI, tarzAd=TARZ_AD, uslupTaban=USLUP_TABAN, usluplar=USLUPLAR, hucre=HUCRE, kat=KAT, izgara=IZGARA, duvarRenk=DUVAR_RENK, catiRenk=CATI_RENK, vurguRenk=VURGU_RENK,
                tenteRenk=TENTE_RENK, yaprakRenk=YAPRAK_RENK, icSinir=IC_SINIR_VARSAYILAN, icYedek=IC_YEDEK,
                kademe=KADEME, etek=ETEK, kaldirimZ=KALDIRIM_Z, zeminTur=[dict(ad=a, etiket=e) for a, e in ZEMIN_TUR], sertZemin=list(SERT_ZEMIN), catiTarz=CATI_TARZ_AD, turler=TURLER, parcalar=parcalar, hafifler=hafifler)
    os.makedirs(os.path.dirname(yol), exist_ok=True)
    with open(yol, "w", encoding="utf-8") as f:
        json.dump(veri, f, ensure_ascii=False, separators=(",", ":"))
    print("YAZILDI", yol, "%d parca, %d tur, %.0f KB" % (len(parcalar), len(TURLER), os.path.getsize(yol) / 1024.0))


def ornek_json_yaz(klasor):
    os.makedirs(klasor, exist_ok=True)
    for t in ornekler():
        with open(os.path.join(klasor, "atolye_%s.json" % t["ad"]), "w", encoding="utf-8") as f:
            json.dump(t, f, ensure_ascii=False, indent=1)
    print("YAZILDI", klasor, len(ornekler()), "ornek")


def depo_koku():
    kok = os.environ.get("KURYE_TYCOON_KOK")
    if kok:
        return kok
    return os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(B.__file__)), "..", ".."))


# ================================== ÖNİZLEME ==================================
ARKA = (244, 238, 226)


def _kirp(im, boy, arka=ARKA):
    from PIL import Image, ImageChops
    fark = ImageChops.difference(im.convert("RGB"), Image.new("RGB", im.size, arka)).convert("L").point(lambda p: 255 if p > 10 else 0)
    k = fark.getbbox()
    if not k:
        return im.resize((boy, boy))
    w = max(k[2] - k[0], k[3] - k[1]) * 1.08
    cx, cy = (k[0] + k[2]) / 2, (k[1] + k[3]) / 2
    kare = Image.new("RGB", (int(w), int(w)), arka)
    kare.paste(im, (int(w / 2 - cx), int(w / 2 - cy)))
    return kare.resize((boy, boy), Image.LANCZOS)


def _ciz(g, boy, az=32, el=27, cizgiler=None):
    from bolge_onizleme import render
    im = render(g, HEX, size=int(boy * 1.5), az=az, el=el, arka=ARKA, kenar=0.12, pastel=True,
                cizgiler=cizgiler if cizgiler is not None else getattr(g, "cizgiler", None) or kenarlar(g))
    return _kirp(im, boy)


def _kolaj(resimler, adlar, sutun, boy, yol):
    from PIL import Image, ImageDraw
    satir = (len(resimler) + sutun - 1) // sutun
    k = Image.new("RGB", (sutun * boy, satir * (boy + 26)), ARKA)
    d = ImageDraw.Draw(k)
    for i, im in enumerate(resimler):
        k.paste(im, ((i % sutun) * boy, (i // sutun) * (boy + 26)))
        d.text(((i % sutun) * boy + 8, (i // sutun) * (boy + 26) + boy + 6), adlar[i], fill=(70, 60, 70))
    k.save(yol, quality=90)
    print(yol)


def _tek(ad, renk=0, olcek=1.0):
    return dict(ad=ad, tohum=1, tur="konut", bloklar=[], susler=[_sus(ad, 0, 0, 0, olcek, renk)])


def onizleme(klasor, sec=None):
    sys.path.insert(0, os.path.dirname(os.path.abspath(B.__file__)))
    os.makedirs(klasor, exist_ok=True)
    orn = ornekler()
    if not sec or "turler" in sec:
        res, adlar = [], []
        for t in orn:
            g = bina(t)
            h = bina(t, hafif=True)
            print("%-18s %6d ucgen + %5d cizgi (hafif %5d + %4d)" % (t["ad"], g.ucgen(), len(g.cizgiler), h.ucgen(), len(h.cizgiler)))
            im = _ciz(g, 620)
            im.save(os.path.join(klasor, "ornek_%s.png" % t["ad"]))
            res.append(im)
            adlar.append("%s  [%s]" % (t["ad"], TUR[t["tur"]]["etiket"]))
        _kolaj(res, adlar, 7, 620, os.path.join(klasor, "turler_kolaj.jpg"))
    if not sec or "cevre" in sec:
        res, adlar = [], []
        for ad in PARCA:
            if PARCA_BILGI[ad][0] not in ("agac", "cevre"):
                continue
            g = bina(_tek(ad, 3 if ad in ("agac_yuvarlak_3", "agac_meyve_2") else 0))
            res.append(_ciz(g, 300, 28, 22))
            adlar.append("%s (%d / %d)" % (ad, parca_g(ad).ucgen(), hafif_parca(ad).ucgen()))
        for i, hx in enumerate(YAPRAK_RENK):                       # mevsim renkleri
            res.append(_ciz(bina(_tek("agac_yuvarlak_1", i)), 300, 28, 22))
            adlar.append("agac_yuvarlak_1, renk %d" % i)
        _kolaj(res, adlar, 9, 300, os.path.join(klasor, "cevre_kolaj.jpg"))
    if not sec or "parcalar" in sec:
        res, adlar = [], []
        for ad in PARCA:
            if PARCA_BILGI[ad][0] in ("agac", "cevre", "harf"):
                continue
            g = parca_g(ad)
            res.append(_ciz(g, 240, 28, 22, kenarlar(g)))
            adlar.append("%s (%d)" % (ad, g.ucgen()))
        _kolaj(res, adlar, 10, 240, os.path.join(klasor, "parca_katalogu.jpg"))
    if not sec or "kasaba" in sec:
        # referans benzeri kasaba: örnek binalar iki sokak boyunca, aralarda ağaç ve dekor
        sec_ad = ["pastel_kuleler", "starbocks", "dutch_gable", "town_hall", "fachwerk_ev", "burger_queen", "tas_kule", "bahceli_ev",
                  "villa_gourmet", "kemerli_gecit", "books", "park_kosesi", "terrace_houses", "flowers", "kaideli_ev", "old_pub"]
        ad2 = {t["ad"]: t for t in orn}
        sokak = G("kasaba")
        cz = []
        for i, ad in enumerate(sec_ad):
            g = bina(ad2[ad])
            ox, oy = (i % 4) * 15.5, -(i // 4) * 17.0
            for f, mi in zip(g.f, g.fm):
                sokak.yuz([(g.v[j][0] + ox, g.v[j][1] + oy, g.v[j][2]) for j in f], g.mats[mi])
            cz += [((a[0] + ox, a[1] + oy, a[2]), (b[0] + ox, b[1] + oy, b[2]), c) for a, b, c in g.cizgiler]
        zemin, yol = PM("Zemin", "#dfe6c8"), PM("Zemin", "#e9dfc9")
        kutu(sokak, -9, 56, -60, 9, -0.25, -0.05, zemin)
        for r in range(4):
            kutu(sokak, -9, 56, -r * 17.0 - 9.3, -r * 17.0 - 7.6, -0.05, -0.02, yol)
        from bolge_onizleme import render
        im = render(sokak, HEX, size=3000, az=30, el=31, arka=ARKA, pastel=True, cizgiler=cz)
        _kirp(im, 2400).save(os.path.join(klasor, "kasaba.jpg"), quality=90)
        print(os.path.join(klasor, "kasaba.jpg"))


    if not sec or "ic" in sec:
        # çatısı kalkmış, kameraya bakan iki duvarı inmiş iç görünümler
        ad2 = {t["ad"]: t for t in orn}
        sec_ad = [a for a in ("sparx_express", "burger_queen", "old_pub", "starbocks", "pharmacy", "hospital", "fire_station", "library",
                              "pizza_hat", "bakery", "butcher", "flowers", "books", "sushi_samurai", "school", "depot", "garage", "factory",
                              "town_hall", "grand_hotel", "market_hall", "opera", "department_store", "fachwerk_ev") if a in ad2]
        res, adlar = [], []
        for ad in sec_ad:
            g = bina(ad2[ad], gizle=("Cati", "Duvar_On", "Duvar_Sag", "Kepenk"))
            res.append(_ciz(g, 700, 32, 38))
            adlar.append("%s  [%s]" % (ad, TUR[ad2[ad]["tur"]]["etiket"]))
        _kolaj(res[:8], adlar[:8], 4, 700, os.path.join(klasor, "ic_mekan_kolaj.jpg"))
        _kolaj(res, adlar, 6, 700, os.path.join(klasor, "ic_mekan_hepsi.jpg"))
    if not sec or "gece" in sec:
        _gece(klasor, orn)
    if sec and "sahne" in sec:
        # örnek sahneler: gündüz + (ilki) gece. Yavaş: yalnız istenince (--onizleme <klasör> sahne [ad])
        for sh in sahneler():
            if len(sec) > 1 and sh["ad"] not in sec:
                continue
            sahne_ciz(sh).save(os.path.join(klasor, "sahne_%s.jpg" % sh["ad"]), quality=90)
            print(os.path.join(klasor, "sahne_%s.jpg" % sh["ad"]))
            if "gece" in sec or sh["ad"] == "kasaba_meydani":
                sahne_ciz(sh, gece=True).save(os.path.join(klasor, "sahne_%s_gece.jpg" % sh["ad"]), quality=90)


def gece_cevirici():
    """(işlev, renk sözlüğü): malzemeyi gece hâline çevirir. Pencereler üç grupta (bina sırasına göre bir kısmı sönük), açık
    dükkânın vitrini ve tabelası yanık, kapalınınki karanlık; sokak lambaları ve iç ışıklar yanık; geri kalan lacivert tonlu."""
    gece_hex = {}

    def cevir(mat, i, acik):
        hexv = HEX[mat]
        c = [int(hexv[j:j + 2], 16) for j in (1, 3, 5)]
        isik = None
        if mat.startswith("Pastel_Cam3_"):
            isik = None if (i % 2 == 0 or not acik) else (255, 214, 140)
        elif mat.startswith("Pastel_Cam2_"):
            isik = (255, 226, 160) if acik or i % 3 else None
        elif mat.startswith("Pastel_Cam_"):
            isik = (255, 236, 180) if acik else ((255, 226, 160) if i % 2 else None)
        elif mat.startswith("Pastel_Vitrin_"):
            isik = (255, 240, 200) if acik else None
        elif mat.startswith("Pastel_Isik_"):
            isik = (255, 240, 190)
        elif mat.startswith("Pastel_Yazi_"):
            isik = (255, 246, 220) if acik else (150, 150, 170)
        if isik is None:
            c = (int(c[0] * 0.30 + 8), int(c[1] * 0.33 + 10), int(c[2] * 0.47 + 22))
            ad = "G_%02x%02x%02x" % c
        else:
            c = isik
            ad = "GI_%02x%02x%02x" % c
        gece_hex[ad] = "#%02x%02x%02x" % c
        return ad
    return cevir, gece_hex


def sahne_ciz(sh, boy=2200, gece=False, az=30, el=33):
    """Sahnenin önizleme resmi (gündüz ya da gece)."""
    from bolge_onizleme import render
    if gece:
        cevir, hx = gece_cevirici()
        g = sahne(sh, gece=cevir)
        arka = (24, 28, 44)
        im = render(g, hx, size=int(boy * 1.25), az=az, el=el, arka=arka, pastel=True, cizgiler=g.cizgiler)
        return _kirp(im, boy, arka)
    g = sahne(sh)
    im = render(g, HEX, size=int(boy * 1.25), az=az, el=el, arka=ARKA, pastel=True, cizgiler=g.cizgiler)
    return _kirp(im, boy)


def _gece(klasor, orn):
    """Gece kasaba görünümü (yaklaşık): pencereler üç grupta (biri sönük), açık dükkânın vitrini ve tabelası yanık,
    kapalı dükkânın kepengi inik ve vitrini karanlık. Oyundaki GeceIsiklari + DukkanCanli davranışının taslağı."""
    from bolge_onizleme import render
    sec_ad = ["pastel_kuleler", "starbocks", "dutch_gable", "town_hall", "fachwerk_ev", "burger_queen", "tas_kule", "bahceli_ev",
              "villa_gourmet", "kemerli_gecit", "books", "old_pub", "terrace_houses", "flowers", "pharmacy", "sparx_express"]
    kapali = {"starbocks", "books", "flowers", "town_hall", "villa_gourmet"}
    ad2 = {t["ad"]: t for t in orn}
    gece_hex = {}

    def gece_mat(mat, acik, i):
        hexv = HEX[mat]
        c = [int(hexv[j:j + 2], 16) for j in (1, 3, 5)]
        isik = None
        if mat.startswith("Pastel_Cam3_"):
            isik = None if (i % 2 == 0 or not acik) else (255, 214, 140)
        elif mat.startswith("Pastel_Cam2_"):
            isik = (255, 226, 160) if acik or i % 3 else None
        elif mat.startswith("Pastel_Cam_"):
            isik = (255, 236, 180) if acik else ((255, 226, 160) if i % 2 else None)
        elif mat.startswith("Pastel_Vitrin_"):
            isik = (255, 240, 200) if acik else None
        elif mat.startswith("Pastel_Isik_"):
            isik = (255, 240, 190)
        elif mat.startswith("Pastel_Yazi_"):
            isik = (255, 246, 220) if acik else (150, 150, 170)
        if isik is None:
            c = (int(c[0] * 0.30 + 8), int(c[1] * 0.33 + 10), int(c[2] * 0.47 + 22))
            ad = "G_%02x%02x%02x" % c
        else:
            c = isik
            ad = "GI_%02x%02x%02x" % c
        gece_hex[ad] = "#%02x%02x%02x" % c
        return ad

    sokak = G("gece")
    cz = []
    for i, ad in enumerate(sec_ad):
        if ad not in ad2:
            continue
        acik = ad not in kapali
        g = bina(ad2[ad], gizle=("Ic", "Ampul", "Poset", "Kepenk") if acik else ("Ic", "Ampul", "Poset"))
        ox, oy = (i % 4) * 15.5, -(i // 4) * 17.0
        for f, mi in zip(g.f, g.fm):
            sokak.yuz([(g.v[j][0] + ox, g.v[j][1] + oy, g.v[j][2]) for j in f], gece_mat(g.mats[mi], acik, i))
        cz += [((a[0] + ox, a[1] + oy, a[2]), (b[0] + ox, b[1] + oy, b[2]), "#15131f") for a, b, c in g.cizgiler]
    gece_hex["G_zemin"] = "#2a3140"
    gece_hex["G_yol"] = "#353a4a"
    kutu(sokak, -9, 56, -60, 9, -0.25, -0.05, "G_zemin")
    for r in range(4):
        kutu(sokak, -9, 56, -r * 17.0 - 9.3, -r * 17.0 - 7.6, -0.05, -0.02, "G_yol")
    arka = (24, 28, 44)
    im = render(sokak, gece_hex, size=3000, az=30, el=31, arka=arka, pastel=True, cizgiler=cz)
    _kirp(im, 2400, arka).save(os.path.join(klasor, "gece_kasaba.jpg"), quality=90)
    print(os.path.join(klasor, "gece_kasaba.jpg"))


if __name__ == "__main__":
    if "--json" in sys.argv:
        json_yaz(os.path.join(depo_koku(), "Resources", "Atolye", "pastel_parcalar.json"))
    if "--ornekler" in sys.argv:
        # klasör verilmezse oyunun okuduğu yer: Resources/Atolye/Tasarimlar (her tasarım oyunda bir bina olur)
        i = sys.argv.index("--ornekler")
        hedef = sys.argv[i + 1] if i + 1 < len(sys.argv) and not sys.argv[i + 1].startswith("--") else os.path.join(depo_koku(), "Resources", "Atolye", "Tasarimlar")
        ornek_json_yaz(hedef)
    if "--sahneler" in sys.argv:
        sahne_json_yaz(os.path.join(depo_koku(), "Resources", "Atolye", "Sahneler"))
    if "--onizleme" in sys.argv:
        i = sys.argv.index("--onizleme")
        onizleme(sys.argv[i + 1], sys.argv[i + 2:] or None)
