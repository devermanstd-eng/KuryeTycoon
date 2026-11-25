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
  • Unity tarafı parçaları FBX'ten değil, bu betiğin yazdığı JSON'dan okur (Editor/BinaAtolyesi/pastel_parcalar.json):
    Blender gerekmez, araç parçaları anında kurar. Köşeler yüz başına ayrıdır (düz gölgeleme); normalleri Unity hesaplar. Parça değişince:  python3 Tools/Blender/pastel_parcalar.py --json
  • Birleştirme kuralları (bina()) Editor/BinaAtolyesi.cs'teki C# birleştiricisiyle AYNI olmalı: birini değiştirince öbürünü de.

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
SILME = PM("Silme", "#fbf7ee")
SILME2 = PM("Silme", "#e9e2d2")
CAM = PM("Cam", "#c3dbe6")
CAM2 = PM("Cam", "#a9c6d6")
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
PARCA_BILGI = {}    # ad -> (grup, açıklama)


def parca(grup, aciklama):
    def sar(fn):
        PARCA[fn.__name__] = fn
        PARCA_BILGI[fn.__name__] = (grup, aciklama)
        return fn
    return sar


def panel(g, c, mat=None, z0=0.0, z1=KAT):
    """Duvar panelinin gövdesi (hücrenin içine DK kadar)."""
    c.kutu(g, 0, HUCRE, -DK, 0, z0, z1, mat or D, arka=True)


def pencere(g, c, u0, u1, z0, z1, kemer=False, bolme=1, yatay=True, cam=CAM, t=0.09):
    """Beyaz çerçeveli pencere (duvarın önüne hafif çıkar): cam, kasa, kayıtlar, denizlik."""
    c.kutu(g, u0, u1, 0, 0.03, z0, z1, cam)
    c.kutu(g, u0 - t, u0, 0, 0.07, z0 - t, z1 + (0 if kemer else t), SILME)
    c.kutu(g, u1, u1 + t, 0, 0.07, z0 - t, z1 + (0 if kemer else t), SILME)
    c.kutu(g, u0, u1, 0, 0.07, z0 - t, z0, SILME)
    if kemer:
        r = (u1 - u0) / 2
        um = (u0 + u1) / 2
        c.yarim_disk(g, um, z1, r, 0, 0.03, cam, n=8)
        n = 6
        R = r + t / 2
        for i in range(n):
            a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
            cubuk(g, c.p(um + R * math.cos(a0), 0.035, z1 + R * math.sin(a0)), c.p(um + R * math.cos(a1), 0.035, z1 + R * math.sin(a1)), t, SILME, 0.07)
    else:
        c.kutu(g, u0, u1, 0, 0.07, z1, z1 + t, SILME)
    for i in range(1, bolme + 1):
        if bolme < 1:
            break
        uu = u0 + (u1 - u0) * i / (bolme + 1)
        c.kutu(g, uu - 0.025, uu + 0.025, 0.03, 0.055, z0, z1, SILME)
    if yatay:
        zz = z0 + (z1 - z0) * 0.62
        c.kutu(g, u0, u1, 0.03, 0.055, zz - 0.025, zz + 0.025, SILME)
    c.kutu(g, u0 - t - 0.06, u1 + t + 0.06, 0, 0.14, z0 - t - 0.06, z0 - t, SILME2)       # denizlik


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


@parca("zemin", "Dükkân vitrini (tenteli)")
def vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.07, 0.0, 0.5, VR)                       # kaide panosu
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.03, 0.5, 2.15, CAM)
    for uu in (0.15, 1.36, HUCRE - 0.23):
        c.kutu(g, uu, uu + 0.08, 0, 0.08, 0.5, 2.15, SILME)
    c.kutu(g, 0.15, HUCRE - 0.15, 0, 0.08, 2.15, 2.25, SILME)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.1, 2.25, 2.62, VR)                        # tabela bandı
    for i in range(5):
        uu = 0.6 + i * 0.36
        c.kutu(g, uu, uu + 0.24, 0.1, 0.115, 2.36, 2.52, ISIK if i != 2 else SILME)
    n = 7                                                                       # çizgili tente
    for i in range(n):
        a, b = 0.1 + (HUCRE - 0.2) * i / n, 0.1 + (HUCRE - 0.2) * (i + 1) / n
        m = VR if i % 2 == 0 else SILME
        pts = [c.p(a, 0.1, 2.25), c.p(b, 0.1, 2.25), c.p(b, 0.95, 1.98), c.p(a, 0.95, 1.98)]
        q = [(p[0], p[1], p[2] - 0.035) for p in pts]
        kati(g, pts + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], m)
        c.kutu(g, a, b, 0.93, 0.95, 1.8, 1.98, m)


@parca("zemin", "Vitrinli kapı")
def kapi_vitrin(g):
    c = on()
    panel(g, c)
    c.kutu(g, 0.15, 1.2, 0, 0.07, 0.0, 0.5, VR)
    c.kutu(g, 0.15, 1.2, 0, 0.03, 0.5, 2.15, CAM)
    c.kutu(g, 0.15, 0.23, 0, 0.08, 0.5, 2.15, SILME)
    c.kutu(g, 1.2, 1.3, 0, 0.09, 0.0, 2.25, SILME)
    c.kutu(g, 2.5, 2.6, 0, 0.09, 0.0, 2.25, SILME)
    c.kutu(g, 0.15, 2.6, 0, 0.09, 2.15, 2.25, SILME)
    c.kutu(g, 1.3, 2.5, 0, 0.03, 0.12, 2.15, CAM2)
    c.kutu(g, 1.3, 2.5, 0.03, 0.06, 0.12, 0.45, VR)
    c.kutu(g, 1.3, 2.5, 0.03, 0.06, 1.1, 1.17, VR)
    c.kutu(g, 2.3, 2.38, 0.03, 0.1, 1.0, 1.3, PIRINC)
    c.kutu(g, 1.2, 2.6, 0, 0.4, 0.0, 0.12, TAS)
    c.kutu(g, 0.1, HUCRE - 0.1, 0, 0.1, 2.25, 2.62, VR)
    for i in range(4):
        uu = 0.7 + i * 0.4
        c.kutu(g, uu, uu + 0.26, 0.1, 0.115, 2.36, 2.52, ISIK)


@parca("zemin", "Zemin kat penceresi (parmaklıklı)")
def zemin_pencere(g):
    c = on()
    panel(g, c)
    pencere(g, c, 0.8, 2.0, 1.05, 2.35, bolme=1, yatay=False)
    for i in range(6):
        uu = 0.86 + 1.08 * i / 5
        c.kutu(g, uu - 0.012, uu + 0.012, 0.07, 0.09, 1.05, 2.35, DEMIR)
    c.kutu(g, 0.8, 2.0, 0.07, 0.09, 1.68, 1.71, DEMIR)


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
    kutu(g, -0.36, 0.36, -0.31, 0.31, 1.5, 1.62, SILME2, atla=())
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


# ================================== PARÇA GEOMETRİSİ ==================================

_PARCA_G = {}


def parca_g(ad):
    """Parçanın geometrisi (önbellekli)."""
    g = _PARCA_G.get(ad)
    if g is None:
        g = G(ad)
        PARCA[ad](g)
        _PARCA_G[ad] = g
    return g


# ================================== BİRLEŞTİRİCİ ==================================
# Tasarım: dict(ad, tohum, dukkan, bloklar=[dict(x, y, k, tarz, renk, cati)]). x, y 0…IZGARA−1 (y = 0 sokak tarafı), k kat.
# Kurallar Editor/BinaAtolyesi.cs ile aynı (karma fonksiyonu dahil).

def karma(a, b, c, d):
    h = (a * 73856093) ^ (b * 19349663) ^ (c * 83492791) ^ (d * 2654435761)
    h &= 0xffffffff
    h ^= h >> 13
    h = (h * 0x5bd1e995) & 0xffffffff
    h ^= h >> 15
    return h


YON = [(0, -1), (1, 0), (0, 1), (-1, 0)]       # ön, sağ, arka, sol (dönüş 0, 1, 2, 3 × 90°)

APT_UST = ["duvar_pencere", "duvar_pencere", "duvar_cift", "duvar_kemerli", "duvar_saksi", "duvar_panjur", "duvar_dar", "duvar_balkon", "duvar_duz", "duvar_yuvarlak"]
FW_UST = ["fachwerk_pencere", "fachwerk_capraz", "fachwerk_saksi", "fachwerk_x", "fachwerk_pencere"]
TAS_UST = ["tas_duvar", "tas_mazgal", "tas_kemerli", "tas_duvar"]


def yerlesim(t):
    """Tasarımdan parça yerleşimleri: (parça, hücre x, y, taban z, dönüş, z ölçeği, x-y kaydırma, renkler) listesi ve
    alınlık çokgenleri. Dönen: (yerler, cokgenler)."""
    blok = {(b["x"], b["y"], b["k"]): b for b in t["bloklar"]}
    tohum = t.get("tohum", 1)
    yerler, cokgenler = [], []

    def dolu(x, y, k):
        b = blok.get((x, y, k))
        return b is not None and b["tarz"] != MERDIVEN

    def renk(b):
        dv = DUVAR_RENK[b["renk"] % len(DUVAR_RENK)]
        ct = CATI_RENK[b["cati"] % len(CATI_RENK)]
        vr = VURGU_RENK[karma(tohum, b["renk"], b["cati"], 7) % len(VURGU_RENK)]
        return dict(D=dv, d=koyu(dv, 0.9), C=ct, c=koyu(ct, 0.86), K=vr)

    def koy(ad, x, y, z, don, r, zs=1.0, kay=(0.0, 0.0)):
        yerler.append((ad, x, y, z, don % 4, zs, kay, r))

    # ---- kapı hücresi: sokağa (y küçük) en yakın, zemindeki açık ön yüzlerden biri
    on_yuzler = sorted((b["y"], b["x"]) for b in t["bloklar"] if b["k"] == 0 and b["tarz"] in (APARTMAN, FACHWERK, TAS) and not dolu(b["x"], b["y"] - 1, 0))
    kapi = None
    if on_yuzler:
        y_min = on_yuzler[0][0]
        adaylar = [p for p in on_yuzler if p[0] == y_min]
        kapi = adaylar[karma(tohum, len(adaylar), 3, 1) % len(adaylar)]

    for (x, y, k), b in sorted(blok.items()):
        tarz = b["tarz"]
        r = renk(b)
        z = k * KAT
        if tarz == MERDIVEN:
            # bitişikteki dolu hücreye doğru yükselir (yoksa arkaya doğru)
            don = 0
            for i, (dx, dy) in enumerate(YON):
                if dolu(x + dx, y + dy, k):
                    don = (i + 2) % 4
                    break
            koy("tas_merdiven", x, y, z, don, r)
            continue
        ust_bos = not dolu(x, y, k + 1)
        alt_bos = k > 0 and not dolu(x, y, k - 1)
        if alt_bos:
            koy("taban", x, y, z, 0, r)
        for i, (dx, dy) in enumerate(YON):
            if dolu(x + dx, y + dy, k):
                continue
            h = karma(x * 4 + i, y, tohum, 11)                   # sütun boyunca aynı: pencereler üst üste hizalı
            hk = karma(x * 4 + i, y, tohum + k * 31, 17)
            if tarz == KAIDE:
                ad = "tas_duvar"
            elif tarz == TAS:
                ad = "kapi_kemerli" if (k == 0 and i == 0 and kapi == (y, x)) else TAS_UST[(h + (hk % 3 == 0)) % len(TAS_UST)]
            elif tarz == FACHWERK:
                if k == 0:
                    ad = "kapi_ahsap" if (i == 0 and kapi == (y, x)) else ("duvar_pencere" if h % 3 else "duvar_duz")
                else:
                    ad = FW_UST[h % len(FW_UST)]
            else:
                if k == 0:
                    if i == 0 and kapi == (y, x):
                        ad = "kapi_vitrin" if t.get("dukkan") else "kapi_ahsap"
                    elif i == 0 and t.get("dukkan"):
                        ad = "vitrin"
                    else:
                        ad = "zemin_pencere" if h % 4 else "duvar_duz"
                else:
                    ad = APT_UST[h % len(APT_UST)]
                    if ad == "duvar_balkon" and hk % 2:
                        ad = "duvar_pencere"
            koy(ad, x, y, z, i, r)
            # bantlar
            if k == 0 and tarz != KAIDE:
                koy("kaide", x, y, z, i, r)
            if tarz == APARTMAN:
                koy("silme", x, y, z + KAT, i, r)
            elif tarz == FACHWERK and k > 0:
                koy("silme_ahsap", x, y, z, i, r)
            # aksesuarlar (zeminde, tohumlu)
            if k == 0 and tarz in (APARTMAN, FACHWERK) and not (i == 0 and kapi == (y, x)):
                a = karma(x, y, i, tohum + 5) % 14
                if a == 0:
                    koy("fici", x, y, z, i, r, kay=(0.7, -A - 0.45))
                elif a == 1:
                    koy("saksi", x, y, z, i, r, kay=(-0.8, -A - 0.38))
                elif a == 2 and ad == "duvar_duz":
                    koy("sarmasik", x, y, z, i, r)
                elif a == 3:
                    koy("bank", x, y, z, i, r, kay=(0.0, -A - 0.4))
            if k == 0 and i == 0 and kapi == (y, x):
                koy("paspas", x, y, z, i, r, kay=(0.0, -A - 0.75))
                if tarz == APARTMAN and t.get("dukkan"):
                    koy("tabela", x, y, z, i, r)
            if k == 0 and tarz == APARTMAN and ad == "zemin_pencere" and karma(x, y, i, tohum + 9) % 5 == 0:
                koy("fener", x, y, z, i, r, kay=(1.15, 0.0))
        # dış köşeler: iki komşu yüz de açıksa
        for i in range(4):
            (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
            a_bos, b_bos = not dolu(x + ax, y + ay, k), not dolu(x + bx, y + by, k)
            if a_bos and b_bos:
                koy("kose_tas" if tarz in (TAS, KAIDE) else "kose_ahsap" if (tarz == FACHWERK and k > 0) else "kose_dis", x, y, z, i, r)
            elif not a_bos and not b_bos and not dolu(x + ax + bx, y + ay + by, k):
                koy("kose_ic", x, y, z, i, r)
        # altı boş: kemerli geçit (yalnız hemen altındaki boş hücrede)
        if alt_bos and not blok.get((x, y, k - 1)):
            for i, (dx, dy) in enumerate(YON):
                if not dolu(x + dx, y + dy, k - 1):
                    koy("kemer", x, y, z - KAT, i, r)
            for i in range(4):
                (ax, ay), (bx, by) = YON[(i + 3) % 4], YON[i]
                if not dolu(x + ax, y + ay, k - 1) and not dolu(x + bx, y + by, k - 1):
                    koy("kemer_ayak", x, y, z - KAT, i, r)

    # ---- çatılar: her katta üstü açık hücreler
    for k in sorted(set(b["k"] for b in t["bloklar"])):
        ust = {(x, y): b for (x, y, kk), b in blok.items() if kk == k and b["tarz"] != MERDIVEN and not dolu(x, y, k + 1)}
        gorulen = set()
        for bas in sorted(ust):
            if bas in gorulen:
                continue
            # bağlı bölge (aynı tarz)
            tarz = ust[bas]["tarz"]
            bolge, yigin = [], [bas]
            gorulen.add(bas)
            while yigin:
                p = yigin.pop()
                bolge.append(p)
                for dx, dy in YON:
                    q = (p[0] + dx, p[1] + dy)
                    if q in ust and q not in gorulen and ust[q]["tarz"] == tarz:
                        gorulen.add(q)
                        yigin.append(q)
            _cati(bolge, ust, k, tarz, tohum, koy, renk, cokgenler, dolu, blok)
    return yerler, cokgenler


def _cati(bolge, ust, k, tarz, tohum, koy, renk, cokgenler, dolu, blok):
    z = (k + 1) * KAT
    kume = set(bolge)
    xs, ys = [p[0] for p in bolge], [p[1] for p in bolge]
    gen, der = max(xs) - min(xs) + 1, max(ys) - min(ys) + 1
    b0 = ust[bolge[0]]
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
        for (x, y) in bolge:
            for i, (dx, dy) in enumerate(YON):
                # merdivenin ya da daha yüksek bloğun geldiği kenarda korkuluk olmaz
                if (x + dx, y + dy) not in kume and not blok.get((x + dx, y + dy, k)) and not dolu(x + dx, y + dy, k + 1):
                    koy("korkuluk_demir", x, y, z, i, r)
        return
    if len(bolge) == 1 and k >= 2:
        # ince yüksek yığın: kule tepesi
        x, y = bolge[0]
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

    # ---- eğimli çatı: yarım hücrelik kafeste "dışarıya uzaklık" (kırma: 8 komşu; beşik: yalnız mahyaya dik eksen)
    besik = tarz == FACHWERK
    eksen = 0 if gen >= der else 1               # mahya ekseni (0: X boyunca)
    egim = 1.25 if besik else 0.85               # yarım hücre başına yükselme (m)
    ic = set()
    for (x, y) in bolge:
        for i in range(3):
            for j in range(3):
                ic.add((2 * x + i, 2 * y + j))
    def sinirda(px, py):
        # kafes noktasına değen dört hücreden biri bölgede değilse sınır
        for cx in ((px - 1) // 2, px // 2):
            for cy in ((py - 1) // 2, py // 2):
                if (cx, cy) not in kume:
                    return True
        return False
    d = {p: (0 if sinirda(*p) else None) for p in ic}
    komsular = ([(0, 1), (0, -1)] if eksen == 0 else [(1, 0), (-1, 0)]) if besik else \
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)]
    if besik:
        # Beşik çatı yalnız mahyaya dik eksende yükselir: bir kafes noktası, o eksende iki yanında da bölge hücresi
        # varsa içeridedir (alınlık kenarındaki noktalar da böyle: orada çatı yüksek kalır, altını alınlık duvarı kapatır).
        for (px, py) in ic:
            if eksen == 0:
                once = any((cx, (py - 1) // 2) in kume for cx in ((px - 1) // 2, px // 2))
                sonra = any((cx, py // 2) in kume for cx in ((px - 1) // 2, px // 2))
            else:
                once = any(((px - 1) // 2, cy) in kume for cy in ((py - 1) // 2, py // 2))
                sonra = any((px // 2, cy) in kume for cy in ((py - 1) // 2, py // 2))
            d[(px, py)] = None if (once and sonra) else 0
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
        if d[p] is None:
            d[p] = 0
        d[p] = min(d[p], 4)

    for (x, y) in bolge:
        for i in range(2):
            for j in range(2):
                px, py = 2 * x + i, 2 * y + j
                h = [d[(px, py)], d[(px + 1, py)], d[(px + 1, py + 1)], d[(px, py + 1)]]
                h0 = min(h)
                desen = tuple(v - h0 for v in h)
                if desen not in CATI_DESEN:
                    # tabloda olmayan (iki basamak birden yükselen) karo: düz iki üçgen
                    k4 = [_kafes((px, py)), _kafes((px + 1, py)), _kafes((px + 1, py + 1)), _kafes((px, py + 1))]
                    p4 = [(k4[n][0], k4[n][1], z + h[n] * egim) for n in range(4)]
                    cm = "Pastel_Cati_" + r["C"].lstrip("#")
                    cokgenler.append(([p4[0], p4[1], p4[2]], cm, (0, 0, 1)))
                    cokgenler.append(([p4[0], p4[2], p4[3]], cm, (0, 0, 1)))
                    continue
                ad, don = CATI_DESEN[desen]
                # karo kökü: yarım hücrenin (0, 0) köşesi; dönüş karonun kendi ortası çevresinde
                koy(ad, x, y, z + h0 * egim, don, r, zs=egim, kay=(-A + i * A, -A + j * A))
    kenar_parcasi("sacak", "sacak_kose")
    # alınlıklar: bölgenin dış kenarında çatı sıfırdan yüksekse dik üçgen / yamuk duvar
    for (x, y) in bolge:
        for i, (dx, dy) in enumerate(YON):
            if (x + dx, y + dy) in kume:
                continue
            for s in range(2):
                if i == 0: a, b_ = (2 * x + s, 2 * y), (2 * x + s + 1, 2 * y)
                elif i == 1: a, b_ = (2 * x + 2, 2 * y + s), (2 * x + 2, 2 * y + s + 1)
                elif i == 2: a, b_ = (2 * x + 2 - s, 2 * y + 2), (2 * x + 1 - s, 2 * y + 2)
                else: a, b_ = (2 * x, 2 * y + 2 - s), (2 * x, 2 * y + 1 - s)
                ha, hb = d[a] * egim, d[b_] * egim
                if ha <= 0 and hb <= 0:
                    continue
                pa, pb = _kafes(a), _kafes(b_)
                cokgenler.append(([(pa[0], pa[1], z), (pb[0], pb[1], z), (pb[0], pb[1], z + hb), (pa[0], pa[1], z + ha)],
                                  "Pastel_Siva_f6f0e2" if besik else "Pastel_Duvar_" + r["D"].lstrip("#"), (dx, dy, 0)))
    # baca ve çatı penceresi (tohumlu)
    for (x, y) in bolge:
        hh = karma(x, y, k, tohum + 21) % 7
        if hh == 0:
            koy("baca" if besik else "baca_ince", x, y, z + d[(2 * x + 1, 2 * y + 1)] * egim, 0, r, kay=(0.35, 0.2))
        elif hh == 1 and not besik and (x, y - 1) not in kume and d[(2 * x + 1, 2 * y + 1)] >= 1:
            koy("cati_penceresi", x, y, z, 0, r, kay=(0.0, -A))


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
    """Yarım hücre kafes noktası → metre (arsa ortalı)."""
    return ((p[0] - IZGARA) * A, (p[1] - IZGARA) * A)


def hucre_merkezi(x, y):
    return ((x - (IZGARA - 1) / 2.0) * HUCRE, (y - (IZGARA - 1) / 2.0) * HUCRE)


def _renkli(ad, r):
    """Rol adını gerçek renge çevirir: "Pastel_Duvar_@D" → "Pastel_Duvar_bcd9ea"."""
    i = ad.find("_@")
    if i < 0:
        return ad
    hexv = r[ad[i + 2:]]
    yeni = ad[:i + 1] + hexv.lstrip("#")
    HEX[yeni] = hexv
    return yeni


def bina(t, hafif=False):
    """Tasarımdan tek geometri (metre, arsa ortalı, ön −Y, zemin z = 0)."""
    yerler, cokgenler = yerlesim(t)
    g = G(t.get("ad", "bina") + ("_Hafif" if hafif else ""))
    for ad, x, y, z, don, zs, kay, r in yerler:
        pg = parca_g(ad)
        if hafif:
            if PARCA_BILGI[ad][0] == "ayrinti" and ad not in ("tas_merdiven", "baca"):
                continue
            pg = hafif_parca(ad)
        cx, cy = hucre_merkezi(x, y)
        karo = ad in CATI_KAROLARI
        for f, mi, sd in zip(pg.f, pg.fm, pg.fs):
            pts = []
            for i in f:
                px, py, pz = pg.v[i]
                if karo:
                    # karo kendi ortası (A/2, A/2) çevresinde döner, sonra yarım hücreye kayar
                    px, py = px - A / 2, py - A / 2
                    for _ in range(don):
                        px, py = -py, px
                    px, py = px + A / 2 + kay[0], py + A / 2 + kay[1]
                else:
                    px, py = px + kay[0], py + kay[1]
                    for _ in range(don):
                        px, py = -py, px
                pts.append((cx + px, cy + py, z + pz * zs))
            g.yuz(pts, _renkli(pg.mats[mi], r))
        g.sid += 1
    for pts, mat, n in cokgenler:
        if mat not in HEX:
            HEX[mat] = "#" + mat.rsplit("_", 1)[1]
        # dışa bakan yön n: sıra ona göre
        nn = B.newell(pts)
        if nn[0] * n[0] + nn[1] * n[1] + nn[2] * n[2] < 0:
            pts = pts[::-1]
        g.yuz(pts, mat)
        g.sid += 1
    return g


_HAFIF_G = {}


def hafif_parca(ad):
    """Parçanın uzak görünüm hâli: küçük katılar atılır (boş kalırsa asıl parça)."""
    g = _HAFIF_G.get(ad)
    if g is None:
        tam = parca_g(ad)
        # her yüz ayrı katı sayılmasın diye katı numaraları parçanın kendi numaraları
        g = B.hafif(tam, esik=0.3)
        if not g.f:
            g = tam
        _HAFIF_G[ad] = g
    return g


# ================================== ÖRNEK TASARIMLAR ==================================

def _yigin(x, y, kat, tarz, renk, cati, k0=0):
    return [dict(x=x, y=y, k=k0 + k, tarz=tarz, renk=renk, cati=cati) for k in range(kat)]


def ornekler():
    o = []
    # 1) iki pastel kule ve aralarında alçak ek
    b = _yigin(1, 2, 6, APARTMAN, 0, 0) + _yigin(3, 2, 5, APARTMAN, 1, 2) + _yigin(2, 2, 2, APARTMAN, 3, 1)
    o.append(dict(ad="pastel_kuleler", bolge="eski", tohum=3, dukkan=True, bloklar=b))
    # 2) fachwerk ev: zemin sıvalı, üstü yarı ahşap, beşik çatı
    b = []
    for x in (1, 2, 3):
        for y in (1, 2):
            b += _yigin(x, y, 2, FACHWERK, 3, 0)
    b += _yigin(3, 3, 1, FACHWERK, 3, 0)
    o.append(dict(ad="fachwerk_ev", bolge="eski", tohum=5, dukkan=False, bloklar=b))
    # 3) küçük beşik çatılı ev
    b = []
    for x in (1, 2):
        for y in (1, 2, 3):
            b += _yigin(x, y, 1, FACHWERK, 5, 3)
    o.append(dict(ad="kucuk_ev", bolge="ev", tohum=2, dukkan=False, bloklar=b))
    # 4) kemerli geçit: iki kanat, üstte köprü kat
    b = []
    for y in (1, 2):
        b += _yigin(0, y, 3, APARTMAN, 2, 0) + _yigin(1, y, 3, APARTMAN, 2, 0) + _yigin(4, y, 3, APARTMAN, 4, 0) + _yigin(3, y, 3, APARTMAN, 4, 0)
        b += _yigin(2, y, 2, APARTMAN, 6, 0, k0=1)
    o.append(dict(ad="kemerli_gecit", bolge="eski", tohum=7, dukkan=False, bloklar=b))
    # 5) merdivenli taş kaide üstünde ev
    b = []
    for x in (1, 2, 3):
        for y in (1, 2, 3):
            b += _yigin(x, y, 1, KAIDE, 0, 0)
    b += [dict(x=2, y=0, k=0, tarz=MERDIVEN, renk=0, cati=0)]
    for x in (1, 2):
        for y in (2, 3):
            b += _yigin(x, y, 2, APARTMAN, 1, 1, k0=1)
    o.append(dict(ad="kaideli_ev", bolge="eski", tohum=11, dukkan=False, bloklar=b))
    # 6) taş kule ve bitişiğinde ev
    b = _yigin(1, 2, 5, TAS, 0, 0)
    for x in (2, 3):
        for y in (2, 3):
            b += _yigin(x, y, 2, FACHWERK, 6, 4)
    o.append(dict(ad="tas_kule", bolge="eski", tohum=13, dukkan=False, bloklar=b))
    # 7) dükkânlı köşe apartmanı (L biçimli, kırma çatı)
    b = []
    for x in (0, 1, 2, 3):
        b += _yigin(x, 0, 3, APARTMAN, 4, 2) + _yigin(x, 1, 3, APARTMAN, 4, 2)
    for y in (2, 3):
        b += _yigin(0, y, 3, APARTMAN, 4, 2) + _yigin(1, y, 3, APARTMAN, 4, 2)
    o.append(dict(ad="kose_apartmani", bolge="karma", tohum=17, dukkan=True, bloklar=b))
    # 8) dar yüksek pastel kule + alçak dükkân
    b = _yigin(2, 2, 7, APARTMAN, 5, 4) + _yigin(2, 1, 2, APARTMAN, 0, 0) + _yigin(3, 1, 1, APARTMAN, 0, 0) + _yigin(1, 1, 1, APARTMAN, 2, 0)
    o.append(dict(ad="ince_kule", bolge="kule", tohum=19, dukkan=True, bloklar=b))
    return o


# ================================== JSON (Unity) ==================================

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
    return dict(ad=ad, grup=PARCA_BILGI[ad][0], aciklama=PARCA_BILGI[ad][1], v=v,
                alt=[dict(malzeme=m, uc=u) for m, u in alt.items()])


def json_yaz(yol):
    parcalar, hafifler = [], []
    for ad in PARCA:
        parcalar.append(_unity_parca(ad, parca_g(ad)))
        hafifler.append(_unity_parca(ad, hafif_parca(ad)))
    veri = dict(surum=1, hucre=HUCRE, kat=KAT, izgara=IZGARA, duvarRenk=DUVAR_RENK, catiRenk=CATI_RENK, vurguRenk=VURGU_RENK,
                parcalar=parcalar, hafifler=hafifler)
    os.makedirs(os.path.dirname(yol), exist_ok=True)
    with open(yol, "w", encoding="utf-8") as f:
        json.dump(veri, f, ensure_ascii=False, separators=(",", ":"))
    print("YAZILDI", yol, "%d parca, %.0f KB" % (len(parcalar), os.path.getsize(yol) / 1024.0))


def ornek_json_yaz(klasor):
    os.makedirs(klasor, exist_ok=True)
    for t in ornekler():
        yol = os.path.join(klasor, "atolye_%s.json" % t["ad"])
        with open(yol, "w", encoding="utf-8") as f:
            json.dump(t, f, ensure_ascii=False, indent=1)
        print("YAZILDI", yol)


def depo_koku():
    kok = os.environ.get("KURYE_TYCOON_KOK")
    if kok:
        return kok
    return os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(B.__file__)), "..", ".."))


# ================================== ÖNİZLEME ==================================

def _kirp(im, boy, arka):
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


def onizleme(klasor):
    sys.path.insert(0, os.path.dirname(os.path.abspath(B.__file__)))
    from bolge_onizleme import render
    from PIL import Image, ImageDraw
    os.makedirs(klasor, exist_ok=True)
    arka = (244, 238, 226)
    # ---- parça kataloğu
    adlar = list(PARCA)
    sutun, boy = 9, 260
    satir = (len(adlar) + sutun - 1) // sutun
    kolaj = Image.new("RGB", (sutun * boy, satir * (boy + 22)), arka)
    ciz = ImageDraw.Draw(kolaj)
    toplam = 0
    for i, ad in enumerate(adlar):
        g = parca_g(ad)
        toplam += g.ucgen()
        im = render(g, HEX, size=boy * 2, az=28, el=22, arka=arka, kenar=0.1, normal_kenar=True, pastel=True)
        im = _kirp(im, boy, arka)
        kolaj.paste(im, ((i % sutun) * boy, (i // sutun) * (boy + 22)))
        ciz.text(((i % sutun) * boy + 6, (i // sutun) * (boy + 22) + boy + 4), "%s (%d)" % (ad, g.ucgen()), fill=(70, 60, 70))
    kolaj.save(os.path.join(klasor, "parca_katalogu.jpg"), quality=90)
    print("katalog: %d parca, %d ucgen" % (len(adlar), toplam))
    # ---- örnek binalar
    resimler = []
    B_ = 760
    for t in ornekler():
        g = bina(t)
        h = bina(t, hafif=True)
        print("%-16s %6d ucgen (hafif %d), %d blok" % (t["ad"], g.ucgen(), h.ucgen(), len(t["bloklar"])))
        for ek, az, el in (("", 32, 26), ("_arka", -148, 30)):
            im = render(g, HEX, size=int(B_ * 1.5), az=az, el=el, arka=arka, kenar=0.12, normal_kenar=True, pastel=True)
            im = _kirp(im, B_, arka)
            im.save(os.path.join(klasor, "ornek_%s%s.png" % (t["ad"], ek)))
            if not ek:
                resimler.append(im)
    kolaj = Image.new("RGB", (B_ * 4, B_ * ((len(resimler) + 3) // 4)), arka)
    for i, im in enumerate(resimler):
        kolaj.paste(im, ((i % 4) * B_, (i // 4) * B_))
    kolaj.save(os.path.join(klasor, "ornek_binalar_kolaj.jpg"), quality=90)
    # ---- hepsi bir arada: küçük bir sokak (örnekler yan yana)
    sokak = G("sokak")
    for i, t in enumerate(ornekler()):
        g = bina(t)
        ox, oy = (i % 4) * 15.0, -(i // 4) * 16.0
        for f, mi in zip(g.f, g.fm):
            sokak.yuz([(g.v[j][0] + ox, g.v[j][1] + oy, g.v[j][2]) for j in f], g.mats[mi])
    zemin = PM("Zemin", "#e6dcc6")
    kutu(sokak, -9, 54, -25, 9, -0.2, 0.0, zemin)
    im = render(sokak, HEX, size=2200, az=28, el=30, arka=arka, kenar=0.14, normal_kenar=True, pastel=True)
    _kirp(im, 1800, arka).save(os.path.join(klasor, "kasaba.jpg"), quality=90)
    print("bitti ->", klasor)


if __name__ == "__main__":
    if "--json" in sys.argv:
        json_yaz(os.path.join(depo_koku(), "Editor", "BinaAtolyesi", "pastel_parcalar.json"))
    if "--ornekler" in sys.argv:
        ornek_json_yaz(sys.argv[sys.argv.index("--ornekler") + 1])
    if "--onizleme" in sys.argv:
        onizleme(sys.argv[sys.argv.index("--onizleme") + 1])
