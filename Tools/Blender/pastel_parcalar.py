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

Çalıştırma (Blender gerekmez):
  python3 Tools/Blender/pastel_parcalar.py --json                 parça kütüphanesini Unity için yazar
  python3 Tools/Blender/pastel_parcalar.py --onizleme klasor      parça kataloğu + örnek binalar (PNG, kolaj)
  python3 Tools/Blender/pastel_parcalar.py --ornekler klasor      örnek tasarımları JSON olarak yazar (araçta "İçe aktar")

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
TARZ_AD = ["Pastel apartman", "Yarı ahşap (fachwerk) ev", "Taş kule", "Taş kaide / teras", "Taş merdiven"]

DUVAR_RENK = ["#bcd9ea", "#f3c6cf", "#d9c8ea", "#f6ecc9", "#c5e6d0", "#f7d9b5", "#f4f1e8", "#e9b9a6", "#cfd6e6", "#f2e3a1"]
CATI_RENK = ["#c8553f", "#e08a45", "#5f8fb8", "#6fa37a", "#8a5a78", "#7d7f8c"]
VURGU_RENK = ["#3f7d74", "#b5524a", "#3f5f8a", "#8a6247", "#6b4a7a", "#d9a441"]
TENTE_RENK = ["#c8553f", "#3f7d74", "#3f5f8a", "#d9a441", "#8a5a78", "#5a8f4a"]
# yaz açık / yaz koyu / ilkbahar / sonbahar turuncu / kızıl / sarı / çiçekli pembe / kış (karlı)
YAPRAK_RENK = ["#8fbf7a", "#6fa968", "#a9c96a", "#e0a04a", "#c9603f", "#e8c95a", "#f2b6c8", "#dfe3e6"]
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


# ================================== KULLANIM TÜRLERİ ==================================
# Her türün zemin katına kendine özgü parçalar gelir. Tablo JSON'a yazılır; Unity aynı tabloyu okur (kurallar ortak).
#   onek   : prefab adının başı (City3D havuzları ada bakar). "Bolge": Bolge_<bölge>. Dükkân adları oyundaki ShopModel önekleri.
#   tabela : varsayılan tabela yazısı (oyundaki İngilizce dükkân adları)        simge : tabela bandındaki madalyon
#   on     : zemin katta sokağa bakan yüz (kapı hücresi dışında)                 kapi  : kapı hücresinin yüzü
#   duvar  : bütün öteki yüzlerde duvar paneli (sera camı, sac ...)              tente : zemin kat ön yüzlerine tente
#   aks    : ön sıradaki hücrelerin önüne sırayla konan dekorlar                 cephe : kapı sütununun en üst katındaki cephe süsü
#   tepe   : kapı sütununun çatısına konan parça                                 vurgu : vurgu rengi (boşsa tohumdan)

def _tur(ad, etiket, grup, onek="Bolge", tabela="", simge="", on="", kapi="", duvar="", tente=0, aks=(), cephe="", tepe="", vurgu="", bolge="karma"):
    kasa, ic, arka, zemin, calisan = IC_TUR.get(ad, IC_TUR["_"])
    return dict(ad=ad, etiket=etiket, grup=grup, onek=onek, tabela=tabela, simge=simge, on=on, kapi=kapi, duvar=duvar, tente=tente,
                aks=list(aks), cephe=cephe, tepe=tepe, vurgu=vurgu, bolge=bolge,
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
    _tur("sira_evi", "Sıra evi (cumbalı, merdivenli)", "Konut", onek="Konut", on="bay_window", kapi="kapi_merdivenli", bolge="eski"),
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
    _tur("pub", "Pub", "Dükkân", tabela="THE OLD PUB", simge="simge_bardak", vurgu="#7a3f35", aks=("fici", "masa"), on="vitrin", kapi="kapi_ahsap"),
    _tur("firin", "Fırın / pastane", "Dükkân", tabela="BAKERY", simge="simge_ekmek", vurgu="#c98a4a", aks=("kasalar",), **_D),
    _tur("kasap", "Kasap / şarküteri", "Dükkân", tabela="BUTCHER", simge="simge_et", vurgu="#a8342f", **_D),
    _tur("cicekci", "Çiçekçi", "Dükkân", tabela="FLOWERS", simge="simge_cicek", vurgu="#8a5a78", aks=("cicek_kovalari", "saksi"), **_D),
    _tur("kitapci", "Kitapçı", "Dükkân", tabela="BOOKS", simge="simge_kitap", vurgu="#3f5f8a", aks=("bank",), **_D),
    _tur("eczane", "Eczane", "Dükkân", onek="Dukkan_eczane", tabela="PHARMACY", simge="simge_hac", vurgu="#3f8a5a", cephe="hac_cephe", on="vitrin", kapi="kapi_vitrin"),
    _tur("bakkal24", "Köşe bakkal: 24/7 Corner Store", "Dükkân", onek="Dukkan_abc_nonstop", tabela="24/7 CORNER STORE", simge="simge_sepet", vurgu="#3f7d74", aks=("kasalar",), **_D),
    _tur("minimart", "Bakkal: Mini Mart", "Dükkân", onek="Dukkan_mini_abc", tabela="MINI MART", simge="simge_sepet", vurgu="#d9822b", aks=("kasalar",), **_D),
    _tur("market", "Market: Sparx Express", "Market", onek="Market_szpar_express", tabela="SPARX EXPRESS", simge="simge_sepet", vurgu="#2f8a4a", on="vitrin_genis", kapi="kapi_otomatik", aks=("araba_yeri", "kasalar")),
    _tur("supermarket", "Süpermarket: InterSparx", "Market", onek="Market_interszpar", tabela="INTERSPARX", simge="simge_sepet", vurgu="#c8342f", on="vitrin_genis", kapi="kapi_otomatik", aks=("araba_yeri", "", "araba_yeri")),
    _tur("market24", "Market: Market 24", "Market", onek="Market_market_24", tabela="MARKET 24", simge="simge_sepet", vurgu="#3f6fd0", on="vitrin_genis", kapi="kapi_otomatik", aks=("kasalar",)),
    _tur("bio", "Market: BioSparx", "Market", onek="Dukkan_bioszpar", tabela="BIOSPARX", simge="simge_cicek", vurgu="#5a8f4a", aks=("kasalar", "saksi"), **_D),
    _tur("belediye", "Belediye", "Kamu", onek="Kamu", tabela="TOWN HALL", on="sutunlu", kapi="kapi_sutunlu", cephe="saat_cephe", tepe="bayrak", vurgu="#3f5f8a"),
    _tur("hastane", "Hastane", "Kamu", onek="Kamu", tabela="HOSPITAL", simge="simge_hac", kapi="kapi_otomatik", cephe="hac_cephe", vurgu="#c8342f"),
    _tur("itfaiye", "İtfaiye", "Kamu", onek="Kamu", tabela="FIRE STATION", on="kapi_garaj", kapi="kapi_ahsap", vurgu="#c8342f", tepe="bayrak"),
    _tur("polis", "Polis", "Kamu", onek="Kamu", tabela="POLICE", simge="simge_yildiz", kapi="kapi_vitrin", vurgu="#2f4f9a", aks=("sokak_lambasi",)),
    _tur("postane", "Postane", "Kamu", onek="Kamu", tabela="POST OFFICE", kapi="kapi_vitrin", on="vitrin", vurgu="#d9a441", aks=("posta_kutusu",)),
    _tur("okul", "Okul", "Kamu", onek="Kamu", tabela="SCHOOL", kapi="kapi_kemerli", cephe="saat_cephe", tepe="bayrak", vurgu="#8a6247", aks=("cit_ahsap",)),
    _tur("kutuphane", "Kütüphane", "Kamu", onek="Kamu", tabela="LIBRARY", simge="simge_kitap", on="sutunlu", kapi="kapi_sutunlu", vurgu="#6b4a7a"),
    _tur("depo", "Depo", "Sanayi", onek="Sanayi_depo", tabela="DEPOT", duvar="sac_duvar", on="kapi_garaj", kapi="kapi_garaj", vurgu="#5f6f7d", aks=("kasalar",), bolge="sanayi"),
    _tur("garaj", "Garaj / tamirhane", "Sanayi", onek="Sanayi_garaj", tabela="GARAGE", on="garaj_acik", kapi="kapi_ahsap", vurgu="#d9822b", aks=("fici",), bolge="sanayi"),
    _tur("fabrika", "Fabrika", "Sanayi", onek="Sanayi_fabrika", tabela="FACTORY", on="duvar_genis", kapi="kapi_garaj", tepe="baca_fabrika", vurgu="#5f6f7d", bolge="sanayi"),
    _tur("sera", "Sera", "Sanayi", onek="Sanayi_sera", duvar="sera_cam", kapi="kapi_vitrin", vurgu="#5a8f4a", bolge="sanayi"),
    _tur("gar", "Gar", "Simge", tabela="CENTRAL STATION", on="kemer_cam", kapi="kapi_kemerli", cephe="saat_cephe", tepe="bayrak", vurgu="#8a6247"),
    _tur("otel", "Otel", "Simge", tabela="GRAND HOTEL", simge="simge_yildiz", on="vitrin", kapi="kapi_vitrin", tente=1, tepe="bayrak", vurgu="#8a5a78", aks=("saksi",)),
    _tur("avm", "Büyük mağaza / AVM", "Simge", tabela="DEPARTMENT STORE", on="vitrin_genis", kapi="kapi_otomatik", tente=1, vurgu="#3f7d74", bolge="avm"),
    _tur("pazar", "Kapalı pazar", "Simge", tabela="MARKET HALL", on="kemer_cam", kapi="kapi_kemerli", vurgu="#c46a2b", aks=("kasalar", "cicek_kovalari")),
    _tur("opera", "Opera / tiyatro", "Simge", tabela="OPERA", on="sutunlu", kapi="kapi_sutunlu", vurgu="#8a2f4a", tepe="bayrak"),
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
APT_UST = ["duvar_pencere", "duvar_pencere", "duvar_cift", "duvar_kemerli", "duvar_saksi", "duvar_panjur", "duvar_dar", "duvar_balkon", "duvar_duz", "duvar_yuvarlak",
           "duvar_alinlik", "duvar_panjur_kapali", "duvar_stor"]
FW_UST = ["fachwerk_pencere", "fachwerk_capraz", "fachwerk_saksi", "fachwerk_x", "fachwerk_pencere"]
TAS_UST = ["tas_duvar", "tas_mazgal", "tas_kemerli", "tas_duvar"]
SAKSI_ALIR = ("duvar_pencere", "duvar_cift", "duvar_panjur", "duvar_panjur_kapali")
# Çatı tarzı (bloğun ct alanı): 0 tarza göre · 1 kırma · 2 beşik · 3 mansart · 4 düz teras · 5 basamaklı alınlık · 6 testere
CT_OTO, CT_KIRMA, CT_BESIK, CT_MANSART, CT_TERAS, CT_BASAMAK, CT_TESTERE = range(7)
CATI_TARZ_AD = ["Tarza göre", "Kırma", "Beşik", "Mansart", "Düz teras (korkuluklu)", "Basamaklı alınlık (Hollanda)", "Testere (sanayi)"]
CATI_H = {CT_KIRMA: [0.0, 0.85, 1.7, 2.55, 3.4], CT_BESIK: [0.0, 1.25, 2.5, 3.75, 5.0], CT_MANSART: [0.0, 2.0, 2.45, 2.9, 3.35],
          CT_BASAMAK: [0.0, 1.25, 2.5, 3.75, 5.0]}
# köşe yükseklik deseni (h00, h10, h11, h01) → (karo, dönüş)
CATI_DESEN = {
    (0, 0, 0, 0): ("cati_duz", 0),
    (0, 0, 1, 1): ("cati_egim", 0), (1, 0, 0, 1): ("cati_egim", 1), (1, 1, 0, 0): ("cati_egim", 2), (0, 1, 1, 0): ("cati_egim", 3),
    (0, 0, 1, 0): ("cati_kose", 0), (0, 0, 0, 1): ("cati_kose", 1), (1, 0, 0, 0): ("cati_kose", 2), (0, 1, 0, 0): ("cati_kose", 3),
    (0, 1, 1, 1): ("cati_vadi", 0), (1, 0, 1, 1): ("cati_vadi", 1), (1, 1, 0, 1): ("cati_vadi", 2), (1, 1, 1, 0): ("cati_vadi", 3),
    (1, 0, 1, 0): ("cati_semer", 0), (0, 1, 0, 1): ("cati_semer", 1),
}
CATI_KAROLARI = ("cati_duz", "cati_egim", "cati_kose", "cati_vadi", "cati_semer")


def _kafes(p):
    return ((p[0] - IZGARA) * A, (p[1] - IZGARA) * A)


def hucre_merkezi(x, y):
    return ((x - (IZGARA - 1) / 2.0) * HUCRE, (y - (IZGARA - 1) / 2.0) * HUCRE)


def cizgi_rengi(hexv):
    """Kalem çizgisi rengi: yüzey renginin koyusu ile sıcak koyu mor-kahvenin ortası (saf siyah değil)."""
    h = hexv.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    r = [c[i] * 0.45 * 0.5 + k * 0.5 for i, k in enumerate((0.27, 0.2, 0.27))]
    return "#%02x%02x%02x" % tuple(int(v * 255) for v in r)


DUVAR_GRUP = ["Duvar_On", "Duvar_Sag", "Duvar_Arka", "Duvar_Sol"]
GIZLI_GRUP = ("Ic", "Ampul", "Poset", "Kepenk")        # uzak görünümde (LOD1) ve tek parça önizlemede yok


def ic_plan(blok, kapi, T):
    """Zemin katın iç yerleşimi. Mobilyaların hepsi öne (−Y) bakar ve hücrenin arka yarısında durur: her sıranın ön yarısı
    yürüme koridorudur. Kapı sütunu (kapıdan arkaya) boş "geçit"tir; koridorlardan ulaşılamayan hücre kalırsa önündeki
    hücre de geçit olur (blok şekli ne olursa olsun kapıdan her hücreye yol açık). C# karşılığı: AtolyeKurucu.IcPlan.
    Dönen: hucreler (sıralı), gecit (küme), mobilya {hücre: parça}, kasa (hücre ya da None), kapi (x, y)."""
    Z = sorted((x, y) for (x, y, k), b in blok.items() if k == 0 and b["tarz"] in (APARTMAN, FACHWERK, TAS))
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
    bloklar = t.get("bloklar") or []
    blok = {(b["x"], b["y"], b["k"]): b for b in bloklar}
    tohum = t.get("tohum", 1)
    T = tur_bul(t)
    metin = (t.get("tabela") or T["tabela"] or "").upper()
    yerler, cokgenler, kutular = [], [], []

    def dolu(x, y, k):
        b = blok.get((x, y, k))
        return b is not None and b["tarz"] != MERDIVEN

    def renk(b, yaprak=0):
        dv = DUVAR_RENK[b["renk"] % len(DUVAR_RENK)]
        ct = CATI_RENK[b["cati"] % len(CATI_RENK)]
        vr = T["vurgu"] or VURGU_RENK[karma(tohum, b["renk"], b["cati"], 7) % len(VURGU_RENK)]
        yp = YAPRAK_RENK[yaprak % len(YAPRAK_RENK)]
        return dict(D=dv, d=koyu(dv, 0.9), C=ct, c=koyu(ct, 0.86), K=vr, T=TENTE_RENK[t.get("tente", 0) % len(TENTE_RENK)],
                    Y=yp, y=koyu(yp, 0.88), k=cizgi_rengi(yp))

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
        return b is not None and b["tarz"] in (APARTMAN, FACHWERK, TAS) and not dolu(x, y - 1, 0)

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
            if tarz == KAIDE:
                ad = "tas_duvar"
            elif kapi_yuzu:
                ad = T["kapi"] or ("kapi_kemerli" if tarz == TAS else "kapi_ahsap")
            elif on_zemin and T["on"]:
                ad = T["on"]
            elif T["cephe"] and kapi_hucresi and i == 0 and k == kapi_ust and k > 0:
                ad = "duvar_duz"
            elif T["duvar"] and tarz == APARTMAN:
                ad = T["duvar"]
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
            if ad == "duvar_duz" and tarz == APARTMAN and not T["duvar"] and not (T["cephe"] and kapi_hucresi and i == 0 and k == kapi_ust and k > 0):
                cizilen = duz_varyant(x, y, i, k, tohum)
            koy(cizilen, x, y, z, i, r, grup=dg)
            if ad == "duvar_duz" and T["cephe"] and kapi_hucresi and i == 0 and k == kapi_ust and k > 0:
                koy(T["cephe"], x, y, z, i, r)
            if ad in SAKSI_ALIR and hk % 5 == 0:
                koy("cicek_kutusu_p", x, y, z, i, r, grup=dg)
            if on_zemin and T["tente"] and (kapi_yuzu or T["on"]):
                koy("tente", x, y, z, i, r, grup=dg)
            if T["kepenk"] and on_zemin and (kapi_yuzu or T["on"]):
                koy("kepenk", x, y, z, i, r, grup="Kepenk")
            # bantlar
            if k == 0 and tarz != KAIDE and not (T["duvar"] and tarz == APARTMAN):
                koy("kaide", x, y, z, i, r, grup=dg)
            if tarz == APARTMAN and not T["duvar"]:
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
            if kapi_yuzu:
                koy("paspas", x, y, z, i, r, kay=(0.0, -A - 0.75))
            if k == 0 and tarz == APARTMAN and ad == "zemin_pencere" and karma(x, y, i, tohum + 9) % 5 == 0:
                koy("fener", x, y, z, i, r, kay=(1.15, 0.0), grup=dg)
        # köşeler
        for i in range(4):
            (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
            a_bos, b_bos = not dolu(x + ax, y + ay, k), not dolu(x + bx, y + by, k)
            if a_bos and b_bos:
                if T["duvar"] and tarz == APARTMAN:
                    continue
                kose = "kose_tas" if tarz in (TAS, KAIDE) else "kose_ahsap" if (tarz == FACHWERK and k > 0) else "kose_dis"
                if kose == "kose_dis" and karma(x, y, i, tohum + 71) % 4 == 0:
                    kose = "kose_boru"          # sütun boyunca aynı: yağmur iniş borusu çatıdan yere iner
                koy(kose, x, y, z, i, r)
            elif not a_bos and not b_bos and not dolu(x + ax + bx, y + ay + by, k):
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
        for (x, y) in bolge:
            for i, (dx, dy) in enumerate(YON):
                if (x + dx, y + dy) not in kume:
                    koy(ad, x, y, z, i, r)
            if kose_ad:
                for i in range(4):
                    (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
                    if (x + ax, y + ay) not in kume and (x + bx, y + by) not in kume:
                        koy(kose_ad, x, y, z, i, r)

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
        ct = CT_BESIK if tarz == FACHWERK else CT_KIRMA
    if ct == CT_TERAS:
        for (x, y) in bolge:
            koy("duz_cati", x, y, z, 0, r)
        kenar_parcasi("korkuluk")
        for (x, y) in bolge:
            if karma(x, y, k, tohum + 21) % 6 == 0:
                koy("baca_ince", x, y, z + 0.9, 0, r, kay=(0.5, 0.4))
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
    for p in d:
        d[p] = min(d[p] or 0, 4)

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
KALIN_BOY, KALIN, INCE = 2.0, 1.25, 0.62      # 2 m ve uzun kenarlar kalın (× 1,25), kısalar ince (× 0,62). C#: AtolyeKurucu aynı sabitler


def kenarlar(g, en_kisa=EN_KISA):
    """Kalem çizgileri: sert kenarlar (iki yüz arası > 25°) ve açık kenarlar. Aynı düzlemde yan yana gelen parçaların
    ek yerleri (panel – panel) çizilmez. Dönen: (p0, p1, "#rrggbb") listesi. C# karşılığı: AtolyeKurucu.Kenarlar."""
    kayit = {}
    for f, mi in zip(g.f, g.fm):
        mat = g.mats[mi]
        if "_Kabuk_" in mat or "_Yazi_" in mat:
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
        for f, mi in zip(pg.f, pg.fm):
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


def ornekler():
    o = []
    # ---- ilk sekiz (konut ve karışık)
    o.append(_o("pastel_kuleler", "dukkan", _yigin(1, 2, 6, APARTMAN, 0, 0) + _yigin(3, 2, 5, APARTMAN, 1, 2) + _yigin(2, 2, 2, APARTMAN, 3, 1), 3, 4, "TEA ROOM", bolge="eski"))
    o.append(_o("fachwerk_ev", "konut", _blok(1, 3, 1, 2, 2, FACHWERK, 3, 0) + _yigin(3, 3, 1, FACHWERK, 3, 0), 5))
    o.append(_o("kucuk_ev", "konut", _blok(1, 2, 1, 3, 1, FACHWERK, 5, 3), 2, bolge="ev",
                susler=[_sus("agac_meyve_1", 4.2, -2.5), _sus("cit_ahsap", 4.2, -5.4), _sus("cit_ahsap", 1.4, -5.4), _sus("odun_yigini", 3.6, 3.5, 90)]))
    b = []
    for y in (1, 2):
        b += _yigin(0, y, 3, APARTMAN, 2, 0) + _yigin(1, y, 3, APARTMAN, 2, 0) + _yigin(4, y, 3, APARTMAN, 4, 0) + _yigin(3, y, 3, APARTMAN, 4, 0)
        b += _yigin(2, y, 2, APARTMAN, 6, 0, k0=1)
    o.append(_o("kemerli_gecit", "konut", b, 7))
    b = _blok(1, 3, 1, 3, 1, KAIDE) + [dict(x=2, y=0, k=0, tarz=MERDIVEN, renk=0, cati=0, ct=0)] + _blok(1, 2, 2, 3, 2, APARTMAN, 1, 1, k0=1)
    o.append(_o("kaideli_ev", "konut", b, 11))
    o.append(_o("tas_kule", "konut", _yigin(1, 2, 5, TAS, 0, 0) + _blok(2, 3, 2, 3, 2, FACHWERK, 6, 4), 13))
    o.append(_o("kose_apartmani", "dukkan", _blok(0, 3, 1, 2, 3, APARTMAN, 4, 2) + _blok(0, 1, 3, 4, 3, APARTMAN, 4, 2), 17, 1, "CORNER CAFE", bolge="karma"))
    o.append(_o("ince_kule", "dukkan", _yigin(2, 2, 7, APARTMAN, 5, 4) + _yigin(2, 1, 2, APARTMAN, 0, 0) + _yigin(3, 1, 1, APARTMAN, 0, 0) + _yigin(1, 1, 1, APARTMAN, 2, 0), 19, 2, "KIOSK", bolge="kule"))
    # ---- dükkân ve restoranlar
    o.append(_o("burger_queen", "burger", _blok(1, 3, 1, 2, 2, APARTMAN, 7, 0, CT_MANSART), 21, 0))
    o.append(_o("mcdanolds", "mcdanolds", _blok(0, 3, 1, 2, 1, APARTMAN, 6, 0, CT_TERAS) + _yigin(4, 1, 3, APARTMAN, 9, 0), 22, 3))
    o.append(_o("kfg", "kfg", _blok(1, 3, 1, 2, 1, APARTMAN, 6, 0, CT_TERAS), 23, 0))
    o.append(_o("pizza_hat", "pizza", _blok(1, 2, 1, 2, 3, APARTMAN, 3, 0), 24, 0))
    o.append(_o("kebab_king", "doner", _blok(1, 3, 1, 2, 2, APARTMAN, 5, 1), 25, 3))
    o.append(_o("nonstop_gyros", "gyros", _blok(1, 2, 1, 2, 2, APARTMAN, 0, 2), 26, 2))
    o.append(_o("sushi_samurai", "sushi", _blok(1, 3, 1, 2, 2, APARTMAN, 6, 5, CT_TERAS), 27, 4))
    o.append(_o("starbocks", "kafe", _blok(0, 3, 1, 2, 2, APARTMAN, 4, 3) + _blok(3, 3, 3, 3, 2, APARTMAN, 4, 3), 28, 1,
                susler=[_sus("agac_yuvarlak_2", -5.4, 4.5), _sus("saksi", 5.6, -4.6), _sus("sokak_lambasi", 5.8, -5.9, 90)]))
    o.append(_o("fish_ships", "balik", _blok(1, 3, 1, 2, 2, FACHWERK, 0, 2), 29, 2))
    o.append(_o("snack_bar", "bufe", _blok(2, 3, 1, 1, 1, APARTMAN, 9, 1, CT_TERAS), 30, 3))
    o.append(_o("villa_gourmet", "bistro", _blok(1, 3, 1, 2, 3, APARTMAN, 2, 5, CT_MANSART), 31, 4))
    o.append(_o("old_pub", "pub", _blok(1, 3, 1, 2, 2, FACHWERK, 3, 0), 32))
    o.append(_o("bakery", "firin", _blok(1, 2, 1, 2, 2, APARTMAN, 5, 1), 33, 3))
    o.append(_o("butcher", "kasap", _blok(2, 3, 1, 2, 2, APARTMAN, 7, 0), 34, 0))
    o.append(_o("flowers", "cicekci", _blok(1, 2, 1, 2, 2, APARTMAN, 1, 3), 35, 4))
    o.append(_o("books", "kitapci", _blok(1, 2, 1, 2, 3, APARTMAN, 2, 2, CT_BASAMAK), 36, 2))
    o.append(_o("pharmacy", "eczane", _blok(1, 3, 1, 2, 2, APARTMAN, 6, 3), 37))
    o.append(_o("corner_store_247", "bakkal24", _blok(1, 3, 1, 2, 2, APARTMAN, 3, 0), 38, 1))
    o.append(_o("mini_mart", "minimart", _blok(1, 2, 1, 2, 2, APARTMAN, 8, 1), 39, 3))
    # ---- marketler
    o.append(_o("sparx_express", "market", _blok(0, 3, 1, 3, 1, APARTMAN, 6, 3, CT_TERAS), 40))
    o.append(_o("intersparx", "supermarket", _blok(0, 4, 1, 3, 1, APARTMAN, 6, 0, CT_TERAS) + _blok(1, 3, 2, 3, 1, APARTMAN, 6, 0, CT_TERAS, k0=1), 41))
    o.append(_o("market_24", "market24", _blok(1, 3, 1, 2, 2, APARTMAN, 0, 2), 42))
    o.append(_o("biosparx", "bio", _blok(1, 3, 1, 2, 2, FACHWERK, 4, 3), 43, 5))
    # ---- kamu
    o.append(_o("town_hall", "belediye", _blok(0, 4, 1, 2, 2, APARTMAN, 3, 4) + _yigin(2, 1, 2, APARTMAN, 3, 4, k0=2), 44))
    o.append(_o("hospital", "hastane", _blok(0, 4, 1, 3, 3, APARTMAN, 6, 1, CT_TERAS), 45))
    o.append(_o("fire_station", "itfaiye", _blok(0, 3, 1, 2, 2, APARTMAN, 9, 0) + _yigin(4, 2, 4, TAS, 0, 0), 46))
    o.append(_o("police", "polis", _blok(1, 3, 1, 2, 2, APARTMAN, 8, 2), 47))
    o.append(_o("post_office", "postane", _blok(1, 3, 1, 2, 2, APARTMAN, 5, 0, CT_MANSART), 48))
    o.append(_o("school", "okul", _blok(0, 4, 1, 2, 2, APARTMAN, 3, 0, CT_BESIK), 49,
                susler=[_sus("agac_yuvarlak_1", -5.2, 4.6), _sus("agac_yuvarlak_3", 5.0, 4.8), _sus("bank", 0, 3.6, 180)]))
    o.append(_o("library", "kutuphane", _blok(0, 4, 1, 3, 2, APARTMAN, 6, 5), 50))
    # ---- sanayi
    o.append(_o("depot", "depo", _blok(0, 4, 1, 3, 1, APARTMAN, 8, 5, CT_BESIK), 51))
    o.append(_o("garage", "garaj", _blok(1, 3, 1, 2, 1, APARTMAN, 6, 1, CT_TERAS), 52))
    o.append(_o("factory", "fabrika", _blok(0, 4, 1, 3, 2, APARTMAN, 7, 5, CT_TESTERE), 53))
    o.append(_o("greenhouse", "sera", _blok(1, 3, 1, 3, 1, APARTMAN, 6, 3, CT_BESIK), 54,
                susler=[_sus("sebze_bahcesi", -5.4, 0, 90), _sus("kasalar", 5.3, -3)]))
    # ---- simge yapılar (14 m arsaya sığan boyda)
    o.append(_o("central_station", "gar", _blok(0, 4, 1, 2, 2, APARTMAN, 3, 5, CT_MANSART) + _yigin(2, 1, 1, APARTMAN, 3, 5, k0=2), 55))
    o.append(_o("grand_hotel", "otel", _blok(1, 3, 1, 2, 5, APARTMAN, 1, 5, CT_MANSART), 56, 4, bolge="kule"))
    o.append(_o("department_store", "avm", _blok(0, 4, 1, 3, 3, APARTMAN, 6, 3, CT_TERAS), 57, 1))
    o.append(_o("market_hall", "pazar", _blok(0, 4, 1, 3, 1, APARTMAN, 5, 0, CT_BESIK), 58))
    o.append(_o("opera", "opera", _blok(0, 4, 1, 3, 2, APARTMAN, 6, 4) + _blok(1, 3, 2, 3, 1, APARTMAN, 6, 4, k0=2), 59))
    # ---- konut çeşitleri
    o.append(_o("terrace_houses", "sira_evi", _blok(0, 1, 1, 2, 3, APARTMAN, 7, 0, CT_BESIK) + _blok(2, 3, 1, 2, 3, APARTMAN, 0, 5, CT_BESIK) + _yigin(4, 1, 3, APARTMAN, 1, 0, 0, CT_BASAMAK) + _yigin(4, 2, 3, APARTMAN, 1, 0, 0, CT_BASAMAK), 60))
    o.append(_o("villa", "villa", _blok(1, 3, 1, 3, 2, APARTMAN, 6, 0), 61,
                susler=[_sus("agac_sogut_1", -5.3, 4.5), _sus("cit_bitki", -5.6, -5.6), _sus("cit_bitki", 5.6, -5.6), _sus("cicek_tarhi", -5.4, -2.0, 90), _sus("cesme", 5.2, 4.6, 0, 0.6)]))
    o.append(_o("dutch_gable", "konut", _blok(2, 2, 1, 2, 4, APARTMAN, 1, 0, CT_BASAMAK) + _blok(3, 3, 1, 2, 2, APARTMAN, 0, 2, CT_BASAMAK), 62))
    # ---- süslenmiş arsalar
    o.append(_o("bahceli_ev", "konut", _blok(2, 3, 2, 3, 1, FACHWERK, 3, 0) + _yigin(2, 2, 1, FACHWERK, 3, 0, k0=1) + _yigin(3, 2, 1, FACHWERK, 3, 0, k0=1), 63, bolge="ev", susler=[
        _sus("cit_ahsap", -5.6, -6.6), _sus("cit_ahsap", -2.8, -6.6), _sus("cit_ahsap", 2.8, -6.6), _sus("cit_ahsap", 5.6, -6.6),
        _sus("sebze_bahcesi", -4.6, -2.6), _sus("agac_meyve_2", -5.0, 2.2), _sus("agac_meyve_3", -4.4, 5.2, 0, 0.9), _sus("odun_yigini", 5.9, 2.5, 90),
        _sus("kuyu", 3.2, -4.3, 0, 0.8), _sus("cali_3", 0.2, -5.5), _sus("cicek_obegi", -1.2, -4.2), _sus("cicek_obegi", -0.4, -3.1, 40, 1.0, 1),
        _sus("ot_obegi", 1.4, -3.4), _sus("koyun", -1.6, -1.2, 200), _sus("kutuk", 5.5, -1.5)]))
    o.append(_o("kafe_onu", "kafe", _blok(1, 3, 2, 3, 2, APARTMAN, 1, 2), 64, 0, "CAFE ROSE", susler=[
        _sus("masa", -3.2, -4.2, 20), _sus("masa", 3.3, -4.4, -15), _sus("masa", -5.3, -1.8, 80), _sus("saksi", -1.3, -5.9), _sus("saksi", 1.3, -5.9),
        _sus("agac_yuvarlak_4", 5.6, -1.2, 0, 0.9), _sus("sokak_lambasi", -6.2, -6.2), _sus("cop_kutusu", 6.2, -6.2), _sus("bisiklet_park", 5.6, 2.2, 90)]))
    o.append(_o("park_kosesi", "konut", [], 65, bolge="eski", susler=[
        _sus("cesme", 0, 0), _sus("bank", -3.2, 0, 90), _sus("bank", 3.2, 0, -90), _sus("bank", 0, 3.2, 180), _sus("agac_yuvarlak_1", -4.8, 4.6),
        _sus("agac_yuvarlak_3", 4.9, 4.4), _sus("agac_kavak_1", -5.6, -4.8), _sus("agac_cam_1", 5.4, -4.6), _sus("agac_sogut_2", 0.4, 5.6, 0, 0.9),
        _sus("cali_1", -2.6, -4.9), _sus("cali_2", 2.4, -5.2), _sus("cicek_tarhi", 0, -4.0), _sus("sokak_lambasi", -2.4, 2.6, 45), _sus("heykel", 5.6, 0.2, -90, 0.8),
        _sus("cop_kutusu", 2.6, 2.4), _sus("kaya", -5.6, 0.6)] + [_sus("cicek_obegi" if i % 2 else "ot_obegi", -1.8 + (i * 37 % 40) / 10.0, -1.9 + (i * 53 % 38) / 10.0, i * 40, 1.0, i) for i in range(8)]))
    return o


# ================================== JSON (Unity) ==================================

# ================================== SAHNE (zemin + birden çok bina) ==================================

SAHNE_RENK = dict(D="#f6f0e2", d="#e3dccb", C="#c8553f", c="#ad4835", K="#3f7d74", T="#c8553f", Y=YAPRAK_RENK[0], y=koyu(YAPRAK_RENK[0], 0.88), k=cizgi_rengi(YAPRAK_RENK[0]))


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
    for f, mi in zip(pg.f, pg.fm):
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
        for f, mi in zip(sg.f, sg.fm):
            zg.yuz([sg.v[j] for j in f], sg.mats[mi])
    cz = kenarlar(zg)
    for f, mi in zip(zg.f, zg.fm):
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
        for f, mi in zip(bg.f, bg.fm):
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


def sahneler():
    """Örnek sahneler (16 × 16 hücre = 44,8 m). y = 0 ön (kameraya yakın)."""
    S = []
    # 1) Kasaba meydanı: ortada arnavut kaldırımı meydan + çeşme, çevresinde yol, dükkânlar, köşede park
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 6, 7, "yol"), _dikdortgen(5, 6, 0, 15, "yol"),
        _dikdortgen(7, 15, 8, 15, "kaldirim"), _dikdortgen(7, 12, 0, 5, "meydan"), _dikdortgen(0, 4, 8, 15, "kaldirim"),
        _dikdortgen(13, 15, 0, 5, "cim"), _dikdortgen(0, 4, 0, 5, "cim"), _dikdortgen(1, 3, 2, 2, "patika"), _dikdortgen(2, 2, 0, 5, "patika"),
        _dikdortgen(0, 0, 0, 5, "cit"), _dikdortgen(0, 4, 0, 0, "cit"), [dict(x=2, y=0, t="patika", k=0)], _dikdortgen(3, 4, 4, 5, "tarh"),
        _dikdortgen(13, 15, 4, 5, "tarh"))
    S.append(dict(ad="kasaba_meydani", n=16, tohum=3, zemin=z,
                  binalar=[_b("burger_queen", 8, 9), _b("starbocks", 12, 10), _b("books", 2, 9), _b("pastel_kuleler", 2, 13, kapali=True),
                           _b("old_pub", 8, 13), _b("dutch_gable", 13, 14), _b("flowers", 14, 2, 3)],
                  susler=[_sus("cesme", 2.8, -12.6), _sus("bank", 0.0, -15.2, 0), _sus("bank", 5.6, -15.2, 0), _sus("sokak_lambasi", -1.0, -8.6),
                          _sus("sokak_lambasi", 7.6, -8.6), _sus("agac_yuvarlak_1", -19.0, -14.0), _sus("agac_yuvarlak_2", -11.5, -19.0),
                          _sus("agac_meyve_1", -12.0, -10.0), _sus("bank", -15.2, -16.0, 90), _sus("agac_yuvarlak_3", 18.5, -17.0), _sus("masa", 4.0, -9.4),
                          _sus("cop_kutusu", -1.6, -10.2), _sus("heykel", 9.8, -16.5)]))
    # 2) Nehir kenarı: önde kanal, rıhtım kaldırımı, arkada sıra evler ve kafe; köprü yerine iskele
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 2, 4, "su"), _dikdortgen(0, 15, 5, 6, "kaldirim"), _dikdortgen(0, 15, 7, 8, "yol"), _dikdortgen(0, 15, 9, 15, "kaldirim"),
        _dikdortgen(0, 15, 0, 1, "cim"), _dikdortgen(10, 11, 0, 8, "yol"), _dikdortgen(10, 11, 2, 4, "meydan"), _dikdortgen(13, 15, 12, 15, "cim"),
        _dikdortgen(13, 15, 12, 12, "cit"), _dikdortgen(0, 2, 0, 0, "tarh"))
    S.append(dict(ad="nehir_kenari", n=16, tohum=5, zemin=z,
                  binalar=[_b("terrace_houses", 2, 12), _b("fish_ships", 7, 12), _b("dutch_gable", 13, 10, kapali=True), _b("fachwerk_ev", 14, 14)],
                  susler=[_sus("kayik", -14.0, -14.2, 80), _sus("kayik", 2.0, -13.6, 100), _sus("iskele", -6.0, -9.8), _sus("sokak_lambasi", -18.0, -7.6),
                          _sus("sokak_lambasi", -4.0, -7.6), _sus("bank", -11.0, -7.4, 180), _sus("agac_yuvarlak_1", -19.5, -20.0), _sus("agac_yuvarlak_2", -8.0, -20.5),
                          _sus("agac_meyve_1", 18.0, -20.0), _sus("fici", 1.0, -7.0), _sus("kasalar", 3.4, -6.8), _sus("bisiklet_park", 17.0, -7.2)]))
    # 3) Banliyö sokağı: ortada yol, iki yanında bahçeli evler, çitler, patikalar
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 7, 8, "yol"), _dikdortgen(7, 8, 0, 6, "patika"), _dikdortgen(0, 15, 6, 6, "kaldirim"), _dikdortgen(0, 15, 9, 9, "kaldirim"),
        _dikdortgen(0, 15, 5, 5, "cit"), [dict(x=7, y=5, t="patika", k=0), dict(x=8, y=5, t="patika", k=0)],
        _dikdortgen(2, 2, 10, 10, "patika"), _dikdortgen(8, 8, 10, 10, "patika"), _dikdortgen(13, 13, 10, 10, "patika"),
        _dikdortgen(0, 1, 0, 1, "tarh"), _dikdortgen(14, 15, 0, 1, "tarh"), _dikdortgen(5, 5, 11, 15, "cit"), _dikdortgen(11, 11, 11, 15, "cit"))
    S.append(dict(ad="banliyo_sokagi", n=16, tohum=7, zemin=z,
                  binalar=[_b("bahceli_ev", 2, 13), _b("kucuk_ev", 8, 13), _b("fachwerk_ev", 13, 13), _b("kucuk_ev", 3, 2, 2), _b("bahceli_ev", 12, 2, 2)],
                  susler=[_sus("sokak_lambasi", -15.0, 0.2), _sus("sokak_lambasi", 5.0, 0.2), _sus("posta_kutusu", -9.0, 5.2), _sus("agac_yuvarlak_1", -1.0, 17.0),
                          _sus("agac_meyve_1", 20.0, 17.5), _sus("agac_yuvarlak_3", -2.0, -17.0), _sus("agac_meyve_2", 3.5, -19.0), _sus("durak", 12.0, -3.4, 180),
                          _sus("cop_kutusu", 9.6, -3.4), _sus("koyun", 0.0, -12.0, 40)]))
    # 4) Tepe kasabası: arkada iki kademe yükselen teraslar (istinat duvarı, yamaç), önde yol ve su
    z = _zemin_birlestir(
        _dikdortgen(0, 15, 0, 1, "su"), _dikdortgen(0, 15, 2, 2, "kaldirim"), _dikdortgen(0, 15, 3, 4, "yol"), _dikdortgen(0, 15, 5, 9, "kaldirim"),
        _dikdortgen(0, 15, 10, 15, "cim", 1), _dikdortgen(0, 6, 10, 15, "meydan", 1), _dikdortgen(9, 15, 13, 15, "cim", 2), _dikdortgen(7, 8, 10, 12, "tarh", 1),
        _dikdortgen(12, 13, 5, 9, "cim"), _dikdortgen(12, 12, 5, 9, "cit"))
    S.append(dict(ad="tepe_kasabasi", n=16, tohum=9, zemin=z,
                  binalar=[_b("town_hall", 3, 13), _b("tas_kule", 12, 14), _b("bakery", 2, 7), _b("pharmacy", 8, 7), _b("kaideli_ev", 14, 7)],
                  susler=[_sus("agac_yuvarlak_1", 8.0, 8.0), _sus("agac_yuvarlak_2", 20.0, 9.0), _sus("agac_meyve_1", 16.0, 14.0), _sus("sokak_lambasi", -8.0, -15.4),
                          _sus("sokak_lambasi", 8.0, -15.4), _sus("bank", 0.0, -15.6, 180), _sus("kayik", -12.0, -20.4, 90), _sus("kuyu", 17.5, -3.0)]))
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
    for f, mi in zip(g.f, g.fm):
        pts = [g.v[i] for i in f]
        bas = len(v) // 3
        for p in pts:
            v += [round(p[0], 4), round(p[1], 4), round(p[2], 4)]
        uc = alt.setdefault(g.mats[mi], [])
        for i in range(1, len(pts) - 1):
            uc += [bas, bas + i, bas + i + 1]
    grup, aciklama, hedef = PARCA_BILGI[ad]
    return dict(ad=ad, grup=grup, aciklama=aciklama, hedef=hedef, kucuk=bool(kucuk(ad)), sinir=IC_SINIR.get(ad, 0), v=v, alt=[dict(malzeme=m, uc=u) for m, u in alt.items()])


def json_yaz(yol):
    parcalar = [_unity_parca(ad, parca_g(ad)) for ad in PARCA]
    hafifler = [_unity_parca(ad, hafif_parca(ad)) for ad in PARCA]
    veri = dict(surum=3, hucre=HUCRE, kat=KAT, izgara=IZGARA, duvarRenk=DUVAR_RENK, catiRenk=CATI_RENK, vurguRenk=VURGU_RENK,
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
