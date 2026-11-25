# -*- coding: utf-8 -*-
"""
KURYE TYCOON · MAHALLE BAKKALI (Blender scripti)

Oyundaki "Corner Shop" dükkânının binası: köşesi pahlı, kiremit çatılı, tek katlı mahalle bakkalı. İçi de modellidir
(raflar, soğutucular, kasa, şarküteri, dondurma dolabı ...); çatı ayrı bir objedir, oyunda kamera yaklaşınca kalkar
(Scripts/World/CatiKaldir.cs) ve dükkânın içi görünür.

Geometri çekirdeği bolge_modelleri.py'den gelir (saf Python; bpy yalnızca mesh + FBX dışa aktarmada kullanılır).

Çıktı (Modeller/Binalar):
  Dukkan_bakkal_1.fbx        34 obje: Dukkan_bakkal_1_Govde (bel altı duvarlar, dış detaylar, tente, direkler)
                                      Dukkan_bakkal_1_Ic    (iç mekân: yalnızca çatı kalkınca görünür)
                                      Dukkan_bakkal_1_Cati  (tavan, saçak, çatı, baca, saçak tabelası: kalkan parça)
                                      ve hareketli / değişen adlı parçalar (aşağıda "HAREKETLİ PARÇALAR"): _Kapi, _Kepenk1-3,
                                      _Duvar_On/Pah/Sag/Arka/Sol, _Urun_<raf>_<1-3>, _Ampul, _Poset, _Kamyonet, _Koliler,
                                      _Kasa2, _Sogutucu4, _Vitrin2
  Dukkan_bakkal_1_Hafif.fbx  tek obje, uzak görünüm: gövde + duvarlar + çatı, küçük parçalar ve iç mekân yok

Çalıştırma:
  • Unity: Kurye Tycoon > Blender > Bakkalı üret   (bitince prefab adımı kendiliğinden çalışır)
  • Terminal: blender -b --factory-startup -P Tools/Blender/bakkal.py
  • Blender'sız göz kontrolü: python3 Tools/Blender/bakkal.py --onizleme klasor   (PNG'ler + bakkal_kolaj.jpg + bakkal_canli_kolaj.jpg)
  • C# tablosu (yol noktaları, pivotlar): python3 Tools/Blender/bakkal.py --tablo   → Scripts/World/BakkalCanli.cs

Kurallar bolge_modelleri.py ile aynı: ölçü metre, ön (sokak) −Y, arsa 14 × 14 m ve ortada, zemin z = 0,08 (ZL).
Malzeme adı "Etiket_rrggbb"; Vitrin_ / Cam_ / Yazi_ / Isik_ geceleri yanar. Duvar_ / Cati_ / Kapi_ / Panjur_ adlı
malzemelerin tonu oyunda her karede biraz değişir (City3D.Vary): iki bakkal birbirinin kopyası gibi durmaz.
"""

import math
import os
import random
import sys


def _cekirdek():
    """bolge_modelleri.py'yi modül olarak yükler (Unity sarıcısı exec ile çalıştırdığı için __file__ güvenilmez)."""
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
    adaylar.append(os.getcwd())
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
    raise RuntimeError("bolge_modelleri.py bulunamadi (aranan yerler: %s)" % ", ".join(adaylar))


B = _cekirdek()
G, M, C = B.G, B.M, B.C
kutu, obox, cubuk, silindir, dsil, top, prizma, kati = B.kutu, B.obox, B.cubuk, B.silindir, B.dsil, B.top, B.prizma, B.kati
koyu, acik = B.koyu, B.acik
ZL = B.ZL

# ================================== AYARLAR ==================================
AD = "Dukkan_bakkal_1"
TOHUM = 2611
TABELA = "CORNER SHOP"      # oyundaki dükkân adı (tabela metinleri İngilizce)

# Gövde dikdörtgeni (dış yüzler) ve pahlı köşe (ön sağ): kapı pahın ortasında
X0, X1, Y0, Y1 = -5.2, 4.8, -3.6, 4.6
PAH = 1.9
T = 0.22                    # duvar kalınlığı
ZF = 0.26                   # dükkân zemini (kaldırımdan bir basamak yukarıda)
ZT = 3.36                   # duvar üstü; bunun üstündeki her şey çatı objesinde
ZB = ZF + 0.6               # bel hizası: kameraya bakan duvarların bunun üstü oyunda iner (Duvar_* objeleri), altı sabit kalır
ZC = ZT + 0.08              # tavan döşemesinin üstü: çatı buradan başlar
SACAK = 0.36                # çatı saçağının duvardan taşması (ön saçağın önünde tabela durur)
IX0, IX1, IY0, IY1 = X0 + T, X1 - T, Y0 + T, Y1 - T
S2 = math.sqrt(0.5)
PAH_IC = (X1 - PAH) - Y0 - T * math.sqrt(2.0)   # iç pah çizgisi: x − y = PAH_IC

URUN = ["#d94f3d", "#e8a23a", "#f2d04a", "#5a9a4a", "#2f7d8c", "#3d5a8a", "#8a4a6b", "#f4f0e6", "#c46a2b", "#7ab8d6",
        "#e07a9a", "#6b4a32"]
SISE = ["#4a2a1c", "#3f7d4a", "#bfe0ee", "#f08a2a", "#c8342f", "#f2e6a0"]


# ================================== HAREKETLİ PARÇALAR VE YOL NOKTALARI ==================================
# Oyunda hareket eden / açılıp kapanan her şey ayrı, adlı bir objedir (Scripts/World/BakkalCanli.cs adla bulur):
#   Kapi (menteşede döner) · Kepenk1-3 (üstten sarılır) · Poset (tezgâhtaki sipariş poşeti) · Kamyonet, Koliler (sabah
#   tedariki) · Urun_<raf>_<1-3> (raflar boşaldıkça gizlenen ürün grupları) · Ampul (kapalıyken söner) ·
#   Kasa2, Sogutucu4, Vitrin2 (kozmetik yükseltmeler).
# Objenin kökü (pivot) EK_PIVOT'taki noktadadır; C# tarafı dört objenin pivotundan model → Unity dönüşümünü kendisi çıkarır
# (eksen çevirisine ve ölçeğe bağlı kalmaz), bu yüzden aşağıdaki tablo ile BakkalCanli.cs'teki tablo AYNI olmalı:
# değiştirince `python3 bakkal.py --tablo` çıktısını C#'a yapıştır.
EK = {}                     # ad -> G (bakkal() her çağrıda sıfırlar)
URUN_HEDEF = None           # ürün dizilirken: [sabit G, seviye 1, seviye 2, seviye 3] (None: hepsi verilen G'ye)
KAPI_KAPALI_YON = (-S2, -S2)   # kapalı kanadın menteşeden uca yönü; içeri açılırken (−S2, +S2) yönüne döner
POSET_YER = (2.98, 1.6, ZF + 0.95)
KAMYONET_YER = (-2.6, 6.15, ZL)
KOLI_YER = (-0.2, 6.4, ZL)
RAFLAR = ("G1", "G2", "G3", "DR", "SG")      # üç gondol, duvar rafı, soğutucular
# Yol noktaları: ad -> (x, y, z). Kenarlar NOKTA_KENAR'da. Metre, model ekseni (ön −Y).
_KX = (-3.8, -1.92, 0.03)
NOKTA = {
    "S1": (5.6, -6.5, ZL), "S2": (-1.5, -6.6, ZL), "ON": (3.3, -5.3, ZL), "ESIK": (3.75, -3.65, ZF), "KAPI": (3.85, -2.65, ZF),
    "GIR": (3.25, -2.3, ZF), "K3": (2.2, -2.25, ZF), "B3": (2.2, 2.95, ZF),
    "Q3": (2.2, -1.0, ZF), "Q2": (2.2, -0.1, ZF), "Q1": (2.2, 0.8, ZF), "Q0": (2.2, 1.7, ZF), "KUR": (2.2, 2.4, ZF),
    "KC": (3.75, 3.0, ZF), "KAS": (3.95, 1.75, ZF), "AI": (3.05, 3.8, ZF), "AD": (3.0, 5.45, ZL), "KV": (0.55, 6.3, ZL),
}
NOKTA_KENAR = [("S1", "ON"), ("S2", "ON"), ("ON", "ESIK"), ("ESIK", "KAPI"), ("KAPI", "GIR"), ("GIR", "K3"),
               ("K3", "Q3"), ("Q3", "Q2"), ("Q2", "Q1"), ("Q1", "Q0"), ("Q0", "KUR"), ("KUR", "B3"),
               ("B3", "KC"), ("KC", "KAS"), ("B3", "AI"), ("AI", "AD"), ("AD", "KV")]
for _i, _x in enumerate(_KX):
    NOKTA["K%d" % _i] = (_x, -2.25, ZF)
    NOKTA["B%d" % _i] = (_x, 2.95, ZF)
    for _j, _y in enumerate((-0.8, 0.4, 1.6)):
        NOKTA["A%d%s" % (_i, "abc"[_j])] = (_x, _y, ZF)
    NOKTA_KENAR += [("K%d" % _i, "A%da" % _i), ("A%da" % _i, "A%db" % _i), ("A%db" % _i, "A%dc" % _i), ("A%dc" % _i, "B%d" % _i),
                    ("K%d" % _i, "K%d" % (_i + 1)), ("B%d" % _i, "B%d" % (_i + 1))]


def ek(ad, pivot=(0.0, 0.0, 0.0)):
    """Adlı ek parça (yoksa oluşturur). pivot: objenin kökü (dışa aktarımda köşeler buna göre kaydırılır)."""
    g = EK.get(ad)
    if g is None:
        g = EK[ad] = G(AD + "_" + ad)
        g.pivot = pivot
    return g


def urun_g(g, rnd):
    """Bir ürün grubunun gideceği geometri: raf boşalırken sırayla gizlenen üç seviyeden biri ya da hep duran sabit kısım."""
    if URUN_HEDEF is None:
        return g
    return URUN_HEDEF[rnd.randrange(len(URUN_HEDEF))]


def dondur(g, pivot, aci):
    """Önizleme için: geometriyi pivotun çevresinde Z ekseninde döndürür (kapıyı açık göstermek gibi)."""
    ca, sa = math.cos(aci), math.sin(aci)
    g.v = [(pivot[0] + (p[0] - pivot[0]) * ca - (p[1] - pivot[1]) * sa, pivot[1] + (p[0] - pivot[0]) * sa + (p[1] - pivot[1]) * ca, p[2]) for p in g.v]


# ================================== KÜÇÜK YARDIMCILAR ==================================

def cer(o, n, L):
    """Cephe / mobilya çerçevesi: o başlangıç köşesi, n baktığı yön; u, dışarıdan bakınca soldan sağa."""
    return C(o, (-n[1], n[0]), n, L)


def dkutu(g, cx, cy, z0, z1, w, d, aci, mat, alt=False):
    """Z ekseni etrafında döndürülmüş kutu (aci radyan): w kendi x'i, d kendi y'si boyunca."""
    ca, sa = math.cos(aci), math.sin(aci)
    obox(g, (cx, cy, (z0 + z1) / 2), (ca * w / 2, sa * w / 2, 0), (-sa * d / 2, ca * d / 2, 0), (0, 0, (z1 - z0) / 2), mat,
         () if alt else ("2-",))


def meyve(g, x, y, z, r, mat, n=6, rz=None):
    """Ucuz yarım meyve (kasada duran): altıgen etek + düz tepe, 16 üçgen."""
    rz = rz or r
    alt, ust = [], []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        alt.append((x + math.cos(a) * r, y + math.sin(a) * r, z))
        ust.append((x + math.cos(a) * r * 0.58, y + math.sin(a) * r * 0.58, z + rz * 0.78))
    yuzler = [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)] + [list(range(n, 2 * n))]
    kati(g, alt + ust, yuzler, mat, ic=(x, y, z + rz * 0.3))


def meyve_yigini(g, x0, x1, y0, y1, z, r, renkler_, rnd, etiket="Meyve", oran=1.0):
    """Dikdörtgen alana sıkışık dizilmiş meyveler (ikinci sıra yarım adım kaydırılır)."""
    nx = max(1, int((x1 - x0) / (r * 1.9)))
    ny = max(1, int((y1 - y0) / (r * 1.9)))
    dx, dy = (x1 - x0) / nx, (y1 - y0) / ny
    for j in range(ny):
        for i in range(nx):
            if rnd.random() > oran:
                continue
            m = M(etiket, rnd.choice(renkler_))
            meyve(g, x0 + dx * (i + 0.5) + rnd.uniform(-0.01, 0.01), y0 + dy * (j + 0.5) + rnd.uniform(-0.01, 0.01),
                  z, r * rnd.uniform(0.9, 1.05), m)


def kasa(g, cx, cy, z, w, d, h, aci, govde, dolgu, cita=True):
    """Tahta / plastik kasa: gövde + kenardan içeride dolgu yüzeyi (ürünler bunun üstüne dizilir). Dolgunun üstünü döndürür."""
    dkutu(g, cx, cy, z, z + h, w, d, aci, govde)
    dkutu(g, cx, cy, z + h - 0.005, z + h + 0.012, w - 0.07, d - 0.07, aci, dolgu)
    if cita and h > 0.14:
        dkutu(g, cx, cy, z + h * 0.38, z + h * 0.56, w + 0.016, d + 0.016, aci, M("Kasa", koyu(B.HEX[govde], 0.82)))
    return z + h + 0.012


def sise(g, x, y, z, h, r, mat, kapak):
    dsil(g, x, y, z, z + h * 0.6, r, 6, mat, kapak=(False, False))
    dsil(g, x, y, z + h * 0.6, z + h * 0.9, r, 6, mat, r1=r * 0.4, kapak=(False, False))
    dsil(g, x, y, z + h * 0.9, z + h, r * 0.45, 5, kapak, kapak=(False, True))


def lamba_fener(g, c, u, z, R):
    """Duvar feneri: demir kol + ışıklı fener (Isik_: geceleri yanar)."""
    c.kutu(g, u - 0.05, u + 0.05, 0, 0.03, z + 0.12, z + 0.38, R["demir"])
    cubuk(g, c.p(u, 0.03, z + 0.3), c.p(u, 0.26, z + 0.3), 0.035, R["demir"])
    p = c.p(u, 0.26, z)
    kutu(g, p[0] - 0.085, p[0] + 0.085, p[1] - 0.085, p[1] + 0.085, z, z + 0.24, R["isik"], atla=())
    kutu(g, p[0] - 0.115, p[0] + 0.115, p[1] - 0.115, p[1] + 0.115, z + 0.24, z + 0.275, R["demir"], atla=())
    kutu(g, p[0] - 0.1, p[0] + 0.1, p[1] - 0.1, p[1] + 0.1, z - 0.03, z, R["demir"], atla=())
    kutu(g, p[0] - 0.04, p[0] + 0.04, p[1] - 0.04, p[1] + 0.04, z + 0.275, z + 0.33, R["demir"])


def saksi(g, x, y, z, r, rnd, R, cicek=None, boy=1.0):
    """Toprak saksı + çalı; cicek renk listesi verilirse üstünde çiçekler."""
    dsil(g, x, y, z, z + r * 1.15, r * 0.78, 8, R["saksi"], r1=r, kapak=(False, True), kapak_mat=R["toprak"])
    dsil(g, x, y, z + r * 1.15, z + r * 1.27, r * 1.08, 8, R["saksi"], kapak=(True, True), kapak_mat=R["toprak"])
    top(g, (x, y, z + r * 1.25 + r * 0.75 * boy), r * 1.15, r * 1.15, r * 0.95 * boy, M("Cali", rnd.choice(("#5f9a4f", "#6ea552", "#4f8c4a"))), rnd, 0.1)
    if cicek:
        for i in range(6):
            a = rnd.uniform(0, 6.28)
            rr = rnd.uniform(0.3, 0.95) * r
            meyve(g, x + math.cos(a) * rr, y + math.sin(a) * rr, z + r * 1.25 + r * (1.35 - 0.6 * (rr / r) ** 2) * boy, r * 0.22,
                  M("Cicek", rnd.choice(cicek)), n=5)


# ------------------------------------------------------------------ piksel yazı

HARF = {
    "A": (".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"),
    "B": ("####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."),
    "C": (".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."),
    "D": ("####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."),
    "E": ("#####", "#....", "#....", "####.", "#....", "#....", "#####"),
    "H": ("#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"),
    "I": ("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####"),
    "K": ("#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"),
    "L": ("#....", "#....", "#....", "#....", "#....", "#....", "#####"),
    "M": ("#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"),
    "N": ("#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"),
    "O": (".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
    "P": ("####.", "#...#", "#...#", "####.", "#....", "#....", "#...."),
    "R": ("####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"),
    "S": (".####", "#....", "#....", ".###.", "....#", "....#", "####."),
    "T": ("#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."),
    "U": ("#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
    "W": ("#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"),
    "Y": ("#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."),
    " ": (".....",) * 7,
}


def yazi(g, c, uc, zc, metin, px, o0, o1, mat):
    """5 × 7 piksel harflerle yazı (cephe üzerinde, ortası uc / zc). Yatay piksel dizileri tek kutuda birleşir."""
    en = len(metin) * 6 - 1
    u0 = uc - en * px / 2
    zt = zc + 3.5 * px
    for k, h in enumerate(metin):
        sek = HARF.get(h.upper(), HARF[" "])
        for r, satir in enumerate(sek):
            i = 0
            while i < 5:
                if satir[i] != "#":
                    i += 1
                    continue
                j = i
                while j < 5 and satir[j] == "#":
                    j += 1
                c.kutu(g, u0 + (k * 6 + i) * px, u0 + (k * 6 + j) * px, o0, o1, zt - (r + 1) * px, zt - r * px, mat)
                i = j
    return en * px


# ------------------------------------------------------------------ duvar

def duvar(g, c, z0, z1, dis, ic, ust, acikliklar):
    """Kalınlığı T olan, gerçek boşluklu duvar. c: dış yüzün cephesi. acikliklar: (u0, u1, za, zb) listesi.
    Dış yüz dis, iç yüz ic, üst yüz (çatı kalkınca görünen kesit) ust malzemesi."""
    parcalar, u = [], 0.0
    for a, b, za, zb in sorted(acikliklar):
        if a > u + 1e-6:
            parcalar.append((u, a, z0, z1, False))
        if za > z0 + 1e-6:
            parcalar.append((a, b, z0, za, True))
        if zb < z1 - 1e-6:
            parcalar.append((a, b, zb, z1, False))
        u = b
    if u < c.L - 1e-6:
        parcalar.append((u, c.L, z0, z1, False))
    for a, b, p, q, alt_parca in parcalar:
        # boşluğun altındaki parçanın üstü (denizlik altı) iç duvar renginde; diğerlerinin üstü kesit rengi.
        # Bel hizasını (ZB) geçen parça ikiye bölünür: üstü ayrı objeye gider (duvar_ayir), altı yerinde kalır.
        dilimler = [(p, q)] if not (p < ZB - 1e-6 < q - 1e-6) else [(p, ZB), (ZB, q)]
        for p2, q2 in dilimler:
            kesit_ust = ust if (q2 >= z1 - 1e-6 or (q2 == ZB and q > ZB)) else ic
            c.kutu(g, a, b, -T, 0.0, p2, q2, {"*": ic, "1+": dis, "2+": kesit_ust, "2-": ic}, arka=True)


def duvar_ayir(g, izler):
    """Gövdenin cepheye bağlı, bel hizasının üstünde kalan katılarını Duvar_<cephe> objelerine taşır.
    izler: (katı numarası, cephe adı ya da None) listesi; bir iz, kendinden sonraki katıların cephesini belirler."""
    sinir = {}
    for f, sd in zip(g.f, g.fs):
        b = sinir.get(sd)
        for i in f:
            z = g.v[i][2]
            if b is None:
                b = sinir[sd] = [z, z]
            else:
                if z < b[0]: b[0] = z
                if z > b[1]: b[1] = z

    def cephe(sd):
        ad = None
        for bas, a in izler:
            if sd > bas:
                ad = a
            else:
                break
        return ad
    kalan = ([], [], [])
    for f, m, sd in zip(g.f, g.fm, g.fs):
        ad = cephe(sd)
        zmin, zmax = sinir[sd]
        ustte = zmin >= ZB - 0.012 or (zmin + zmax) / 2 > ZB + 0.05
        if ad is None or not ustte:
            kalan[0].append(f); kalan[1].append(m); kalan[2].append(sd)
            continue
        h = ek("Duvar_" + ad, (0.0, 0.0, ZB))
        h.sid = sd
        h.yuz([g.v[i] for i in f], g.mats[m])
    g.f, g.fm, g.fs = list(kalan[0]), list(kalan[1]), list(kalan[2])


def cam_bosluk(g, c, u0, u1, z0, z1, cam_mat, kasa_mat, bolme=(), yatay=(), t=0.07, dis=0.05, ic=0.03):
    """Duvar boşluğuna kasa (duvarın iki yanından biraz taşar), cam ve kayıtlar. bolme: dikey kayıt u'ları, yatay: z'leri."""
    o0, o1 = -T - ic, dis
    c.kutu(g, u0, u0 + t, o0, o1, z0, z1, kasa_mat, arka=True)
    c.kutu(g, u1 - t, u1, o0, o1, z0, z1, kasa_mat, arka=True)
    c.kutu(g, u0 + t, u1 - t, o0, o1, z0, z0 + t, kasa_mat, arka=True)
    c.kutu(g, u0 + t, u1 - t, o0, o1, z1 - t, z1, kasa_mat, arka=True)
    c.kutu(g, u0 + t, u1 - t, -T / 2 - 0.015, -T / 2 + 0.015, z0 + t, z1 - t, cam_mat, arka=True)
    for u in bolme:
        c.kutu(g, u - 0.03, u + 0.03, -T / 2 - 0.045, -T / 2 + 0.045, z0 + t, z1 - t, kasa_mat, arka=True)
    for z in yatay:
        c.kutu(g, u0 + t, u1 - t, -T / 2 - 0.045, -T / 2 + 0.045, z - 0.03, z + 0.03, kasa_mat, arka=True)


# ================================== RENKLER ==================================

def renkler():
    R = dict(
        duvar=M("Duvar", "#f1dfb2"), duvar_ust=M("Kesit", "#4a3f3a"), ic_duvar=M("IcDuvar", "#f6eedb"),
        ic_alt=M("IcDuvar", "#c9d8c2"), kaide=M("Tas", "#b3a999"), tas=M("Tas", "#cfc7b8"), tas_koyu=M("Tas", "#9c9488"),
        trim=M("Korniz", "#f7f1e3"), ahsap_boya=M("Dograma", "#2f6b57"), ahsap_boya2=M("Dograma", "#3f8068"),
        vitrin=M("Vitrin", "#cfe3ea"), cam=M("Cam", "#a9c9de"), cam2=M("Cam2", "#a9c9de"), kristal=M("Kristal", "#d5ebf2"),
        kapi=M("Kapi", "#2f6b57"), panjur=M("Panjur", "#2f6b57"), tabela=M("Tabela", "#234f41"), yazi=M("Yazi", "#fff2c8"),
        tente1="#c8452f", tente2="#f6eedd", cati=M("Cati", "#b5553a"), alinlik=M("Duvar", "#f1dfb2"),
        tavan=M("Tavan", "#f4efe4"), sacak=M("Sacakalti", "#d9d2c4"), oluk=M("Oluk", "#8f989d"),
        demir=M("Demir", "#2b2d31"), metal=M("Metal", "#c7c9cc"), metal_koyu=M("Metal", "#7d8388"), beyaz=M("Emaye", "#f4f4f0"),
        pirinc=M("Pirinc", "#c9a24a"), isik=M("Isik", "#ffe2a0"), isik_soguk=M("Isik", "#eaf6ff"),
        ahsap=M("Ahsap", "#9a6a42"), ahsap_acik=M("Ahsap", "#c89a66"), ahsap_koyu=M("Ahsap", "#6b452a"),
        zemin=M("Karo", "#efe6d2"), karo=M("Karo", "#8fb8b0"), karo_kenar=M("Karo", "#5d7b78"), basamak=M("Tas", "#bdb5a6"),
        raf=M("Raf", "#f1ece2"), raf_govde=M("Raf", "#d6d0c4"), raf_panel=M("Raf", "#e6dfd0"), fiyat=M("Fiyat", "#f2d04a"),
        saksi=M("Saksi", "#b5553a"), toprak=M("Toprak", "#7a5a3e"), paspas=M("Paspas", "#7a4a3a"), paspas2=M("Paspas", "#a8684a"),
        karton=M("Karton", "#c49a6c"), karton2=M("Karton", "#a8824f"), kagit=M("Kagit", "#f2efe8"), kagit2=M("Kagit", "#d8d4ca"),
        tugla=M("Tugla", "#a65a44"), plastik_k=M("Plastik", "#c8342f"), plastik_m=M("Plastik", "#2f5a9a"),
        plastik_s=M("Plastik", "#e0b23a"), plastik_y=M("Plastik", "#3f8a4a"), lastik=M("Lastik", "#2b2d31"),
    )
    return R


# ================================== GÖVDE (DIŞ) ==================================

def cepheler():
    return dict(
        on=cer((X0, Y0), (0, -1), X1 - PAH - X0),
        pah=cer((X1 - PAH, Y0), (S2, -S2), PAH * math.sqrt(2.0)),
        sag=cer((X1, Y0 + PAH), (1, 0), Y1 - Y0 - PAH),
        arka=cer((X1, Y1), (0, 1), X1 - X0),
        sol=cer((X0, Y1), (-1, 0), Y1 - Y0),
    )


# Boşluklar (u0, u1, z0, z1): vitrinler, kapılar, pencereler
V1 = (0.7, 3.7, ZF + 0.6, ZF + 2.25)
V2 = (4.3, 7.3, ZF + 0.6, ZF + 2.25)
KAPI = (0.79, 1.89, ZF, ZF + 2.12)
SAG_P = (0.7, 2.1, ZF + 0.95, ZF + 2.2)
DEPO = (1.1, 2.2, ZF, ZF + 2.05)
ARKA_P = (7.4, 8.4, ZF + 1.55, ZF + 2.3)
SOL_P1 = (1.4, 2.6, ZF + 2.05, ZF + 2.65)
SOL_P2 = (5.8, 7.0, ZF + 2.05, ZF + 2.65)


def govde_kur(g, R, rnd):
    f = cepheler()
    on, pah, sag, arka, sol = f["on"], f["pah"], f["sag"], f["arka"], f["sol"]

    # ---- döşeme: pahlı beşgen, kaldırımdan dükkân zeminine
    taban = [(X0 - 0.05, Y0 - 0.05, ZL), (X1 - PAH + 0.02, Y0 - 0.05, ZL), (X1 + 0.05, Y0 + PAH - 0.02, ZL),
             (X1 + 0.05, Y1 + 0.05, ZL), (X0 - 0.05, Y1 + 0.05, ZL)]
    prizma(g, taban, (0, 0, ZF - ZL - 0.03), R["kaide"], kapak=R["kaide"])

    # Cepheye bağlı parçalar: iz(ad) çağrısından sonraki katılar o cephenin sayılır; bel hizasının üstünde kalanları
    # sonda duvar_ayir Duvar_<ad> objesine taşır (oyunda kameraya bakan duvar iner). iz(None): yerinde kalır.
    izler = []

    def iz(ad):
        izler.append((g.sid, ad))

    # ---- duvarlar (gerçek boşluklu)
    iz("On"); duvar(g, on, ZF, ZT, R["duvar"], R["ic_duvar"], R["duvar_ust"], [V1, V2])
    iz("Pah"); duvar(g, pah, ZF, ZT, R["duvar"], R["ic_duvar"], R["duvar_ust"], [KAPI])
    iz("Sag"); duvar(g, sag, ZF, ZT, R["duvar"], R["ic_duvar"], R["duvar_ust"], [SAG_P])
    iz("Arka"); duvar(g, arka, ZF, ZT, R["duvar"], R["ic_duvar"], R["duvar_ust"], [DEPO, ARKA_P])
    iz("Sol"); duvar(g, sol, ZF, ZT, R["duvar"], R["ic_duvar"], R["duvar_ust"], [SOL_P1, SOL_P2])
    iz(None)

    # ---- süpürgelik (taş kaide bandı) ve köşe taşları
    def kaide(c, aralar, h=0.5):
        u = 0.0
        for a, b in sorted(aralar) + [(c.L, c.L)]:
            if a > u + 0.02:
                c.kutu(g, u, a, 0, 0.05, ZF - 0.03, ZF + h, R["kaide"])
                c.kutu(g, u, a, 0.05, 0.075, ZF + h - 0.07, ZF + h, R["tas"])
            u = b
    kaide(on, [(V1[0] - 0.12, V1[1] + 0.12), (V2[0] - 0.12, V2[1] + 0.12)])
    kaide(pah, [(KAPI[0] - 0.12, KAPI[1] + 0.12)])
    kaide(sag, [])
    kaide(arka, [(DEPO[0] - 0.12, DEPO[1] + 0.12)])
    kaide(sol, [])

    def kose_tasi(c, bas):
        """Cephenin başında (bas=True) ya da sonunda dişli köşe taşları."""
        z, i = ZF + 0.5, 0
        while z + 0.26 < ZT - 0.24:
            w = 0.42 if i % 2 == 0 else 0.26
            if bas:
                c.kutu(g, 0, w, 0, 0.035, z + 0.02, z + 0.26, R["trim"])
            else:
                c.kutu(g, c.L - w, c.L, 0, 0.035, z + 0.02, z + 0.26, R["trim"])
            z += 0.28
            i += 1
    iz("On"); kose_tasi(on, True)
    iz("Sol"); kose_tasi(sol, False); kose_tasi(sol, True)
    iz("Arka"); kose_tasi(arka, False); kose_tasi(arka, True)
    iz("Sag"); kose_tasi(sag, False)

    # =================================================== ÖN CEPHE: vitrinler
    dg, dg2 = R["ahsap_boya"], R["ahsap_boya2"]
    for v in (V1, V2):
        u0, u1, z0, z1 = v
        um = (u0 + u1) / 2
        zk = z0 + (z1 - z0) * 0.74
        iz("On")
        # ahşap vitrin çerçevesi: geniş dış kasa, orta kayıt, üst vasistas bandı
        cam_bosluk(g, on, u0, u1, z0, z1, R["vitrin"], dg, bolme=(um,), yatay=(zk,), t=0.09, dis=0.07)
        for uu in (u0 + (um - u0) / 2, um + (u1 - um) / 2):
            on.kutu(g, uu - 0.02, uu + 0.02, -T / 2 - 0.04, -T / 2 + 0.04, zk + 0.03, z1 - 0.09, dg, arka=True)
        # dış söve (pilastr) ve başlıklar: duvar inince de ayakta kalır, lentoyu ve tenteyi taşır
        iz(None)
        for uu in (u0 - 0.13, u1 + 0.01):
            on.kutu(g, uu, uu + 0.12, 0, 0.09, ZF - 0.03, z1 + 0.1, dg)
            on.kutu(g, uu - 0.02, uu + 0.14, 0, 0.11, ZF - 0.03, ZF + 0.14, dg2)
            on.kutu(g, uu - 0.02, uu + 0.14, 0, 0.11, z1 - 0.02, z1 + 0.1, dg2)
        # alt ahşap kaide: tablalı panolar
        on.kutu(g, u0 - 0.01, u1 + 0.01, 0, 0.06, ZF - 0.03, z0 - 0.06, dg)
        n = 3
        for i in range(n):
            a = u0 + 0.1 + (u1 - u0 - 0.2) * i / n
            b = u0 + 0.1 + (u1 - u0 - 0.2) * (i + 1) / n
            on.kutu(g, a + 0.06, b - 0.06, 0.06, 0.085, ZF + 0.1, z0 - 0.16, dg2)
        # denizlik
        on.kutu(g, u0 - 0.14, u1 + 0.14, 0, 0.16, z0 - 0.06, z0, R["trim"])
        # cama yapıştırılmış kampanya kâğıtları
        iz("On")
        on.kutu(g, u0 + 0.22, u0 + 0.72, -T / 2 + 0.015, -T / 2 + 0.03, z0 + 0.55, z0 + 0.95, M("Afis", "#f2d04a"))
        on.kutu(g, u0 + 0.27, u0 + 0.67, -T / 2 + 0.03, -T / 2 + 0.04, z0 + 0.79, z0 + 0.88, M("Afis", "#c8342f"))
        on.kutu(g, u1 - 0.62, u1 - 0.2, -T / 2 + 0.015, -T / 2 + 0.03, z0 + 0.2, z0 + 0.52, M("Afis", "#f4f0e6"))
        on.kutu(g, u1 - 0.57, u1 - 0.25, -T / 2 + 0.03, -T / 2 + 0.04, z0 + 0.3, z0 + 0.38, M("Afis", "#2f7d8c"))

    iz(None)
    # kepenkler (Kepenk1, Kepenk2: vitrinler; Kepenk3: kapı): kapalı hâlde modellenir, kökleri üst ortada; oyunda
    # dükkân kapalıyken iner. Sarıldıkları kutular gövdede sabit durur.
    for i, (c, v) in enumerate(((on, V1), (on, V2), (pah, KAPI))):
        ua, ub, za, zb = v
        if c is pah:
            za, zb = ZF - 0.02, zb + 0.04
        o0, o1 = 0.085, 0.12
        gkp = ek("Kepenk%d" % (i + 1), c.p((ua + ub) / 2, (o0 + o1) / 2, zb + 0.04))
        n = int((zb + 0.04 - za) / 0.115)
        dz = (zb + 0.04 - za) / n
        for k in range(n):
            c.kutu(gkp, ua, ub, o0, o1 if k % 2 else o1 - 0.012, za + k * dz, za + (k + 1) * dz,
                   M("Kepenk", "#aab1b5") if k % 2 else M("Kepenk", "#8f979c"), arka=True)
        c.kutu(gkp, ua + (ub - ua) * 0.42, ua + (ub - ua) * 0.58, o1, o1 + 0.02, za + 0.06, za + 0.14, R["demir"])
        c.kutu(g, ua - 0.02, ub + 0.02, 0.07, 0.15, zb + 0.02, zb + 0.13, R["metal_koyu"])

    # ön cephede vitrinlerin üstünde ahşap lento (asıl tabela saçakta, çatı objesinde: çatıyla birlikte kalkar)
    zs0, zs1 = ZF + 2.4, ZF + 2.98
    on.kutu(g, 0.12, on.L + 0.02, 0, 0.1, zs0 - 0.04, zs0 + 0.28, dg)
    on.kutu(g, 0.08, on.L + 0.04, 0, 0.14, zs0 + 0.28, zs0 + 0.34, dg2)
    iz("Pah")
    # pah üstünde küçük tabela (sundurmanın altında)
    pah.kutu(g, -0.02, pah.L + 0.02, 0, 0.12, zs0, zs1, R["tabela"])
    pah.kutu(g, -0.04, pah.L + 0.04, 0, 0.16, zs1, zs1 + 0.07, dg2)
    pah.kutu(g, -0.04, pah.L + 0.04, 0, 0.16, zs0 - 0.06, zs0, dg2)
    yazi(g, pah, pah.L / 2, (zs0 + zs1) / 2, "OPEN", 0.07, 0.12, 0.155, R["yazi"])

    iz(None)
    # çizgili tente (iki vitrini örter) + demir kollar
    B.tente(g, on, 0.35, on.L - 0.25, zs0 + 0.26, 1.55, R["tente1"], R["tente2"], dusus=0.52)
    for uu in (0.37, on.L / 2 + 0.05, on.L - 0.27):
        cubuk(g, on.p(uu, 0.02, zs0 - 0.5), on.p(uu, 1.5, zs0 - 0.3), 0.03, R["demir"])

    iz("On")
    # duvar fenerleri (ön cephede üç ayak üstünde)
    for uu in (0.33, (V1[1] + V2[0]) / 2, V2[1] + 0.42):
        lamba_fener(g, on, uu, ZF + 1.72, R)

    iz(None)
    # bayrak tabela: sol ön köşede, sokağa dik
    ub = 0.16
    on.kutu(g, ub - 0.03, ub + 0.03, 0, 1.0, zs1 + 0.02, zs1 + 0.07, R["demir"])
    cubuk(g, on.p(ub, 0.02, zs1 - 0.42), on.p(ub, 0.62, zs1 + 0.03), 0.03, R["demir"])
    on.kutu(g, ub - 0.035, ub + 0.035, 0.28, 0.98, zs1 - 0.72, zs1 - 0.04, R["ahsap_boya"], arka=True)
    for s in (-1, 1):
        # iki yüzünde sepet ikonu: sap + gövde
        us = ub + s * 0.045
        on.kutu(g, us - 0.012, us + 0.012, 0.43, 0.83, zs1 - 0.55, zs1 - 0.36, R["yazi"], arka=True)
        on.kutu(g, us - 0.012, us + 0.012, 0.5, 0.76, zs1 - 0.3, zs1 - 0.25, R["yazi"], arka=True)
        on.kutu(g, us - 0.012, us + 0.012, 0.5, 0.55, zs1 - 0.36, zs1 - 0.25, R["yazi"], arka=True)
        on.kutu(g, us - 0.012, us + 0.012, 0.71, 0.76, zs1 - 0.36, zs1 - 0.25, R["yazi"], arka=True)
    for oo in (0.36, 0.9):
        on.kutu(g, ub - 0.01, ub + 0.01, oo - 0.01, oo + 0.01, zs1 - 0.04, zs1 + 0.02, R["demir"], arka=True)

    # =================================================== PAH: cam kapı
    iz("Pah")
    u0, u1, z0, z1 = KAPI
    t = 0.08
    pah.kutu(g, u0 - t, u0, -T - 0.03, 0.07, z0, z1 + t, dg, arka=True)
    pah.kutu(g, u1, u1 + t, -T - 0.03, 0.07, z0, z1 + t, dg, arka=True)
    pah.kutu(g, u0, u1, -T - 0.03, 0.07, z1, z1 + t, dg, arka=True)
    pah.kutu(g, u0, u1, -T, 0.02, ZF - 0.03, ZF + 0.02, R["pirinc"], arka=True)      # eşik
    # kapı kanadı ayrı obje (Kapi): kapalı hâlde modellenir, menteşe u1 ucunda; oyunda içeri doğru açılır
    hinge = pah.p(u1 - 0.02, -T / 2, ZF)
    gk = ek("Kapi", hinge)
    kw = u1 - u0 - 0.04
    kanat = C((hinge[0], hinge[1]), (-pah.u[0], -pah.u[1]), (-pah.n[0], -pah.n[1]), kw)

    def kk(a, b, p, q, mat, o0=-0.025, o1=0.025):
        kanat.kutu(gk, a, b, o0, o1, p, q, mat, arka=True)
    kk(0, 0.09, z0 + 0.02, z1 - 0.02, R["kapi"])
    kk(kw - 0.09, kw, z0 + 0.02, z1 - 0.02, R["kapi"])
    kk(0.09, kw - 0.09, z0 + 0.02, z0 + 0.3, R["kapi"])
    kk(0.09, kw - 0.09, z1 - 0.12, z1 - 0.02, R["kapi"])
    kk(0.09, kw - 0.09, z0 + 1.0, z0 + 1.07, R["kapi"])
    kk(0.09, kw - 0.09, z0 + 0.3, z1 - 0.12, R["vitrin"], -0.01, 0.01)
    kk(kw - 0.13, kw - 0.09, z0 + 0.85, z0 + 1.25, R["pirinc"], -0.07, 0.07)       # kulp
    kk(0.3, 0.72, z0 + 1.35, z0 + 1.6, M("Afis", "#f4f0e6"), -0.022, -0.01)        # "açık" kartı (dış yüzde)
    kk(0.34, 0.68, z0 + 1.43, z0 + 1.52, M("Afis", "#c8342f"), -0.03, -0.022)
    # kapı üstü: fener ve zil
    lamba_fener(g, pah, (u0 + u1) / 2, z1 + 0.04, R)
    zp = pah.p(u1 + 0.05, -T - 0.12, z1 - 0.08)
    cubuk(g, pah.p(u1 + 0.05, -T, z1 + 0.02), (zp[0], zp[1], z1 + 0.02), 0.02, R["demir"])
    dsil(g, zp[0], zp[1], z1 - 0.14, z1 - 0.02, 0.07, 6, R["pirinc"], r1=0.025, kapak=(True, True))
    # kapının iki yanında dar ilan panoları
    for a, b in ((0.12, 0.62), (pah.L - 0.62, pah.L - 0.12)):
        pah.kutu(g, a, b, 0, 0.03, ZF + 0.9, ZF + 1.75, R["ahsap_koyu"])
        pah.kutu(g, a + 0.04, b - 0.04, 0.03, 0.04, ZF + 0.94, ZF + 1.71, R["karton"])
        for i in range(3):
            pah.kutu(g, a + 0.08, b - 0.1 - 0.06 * (i % 2), 0.04, 0.048, ZF + 1.02 + i * 0.23, ZF + 1.18 + i * 0.23,
                     M("Afis", ("#f4f0e6", "#f2d04a", "#7ab8d6")[i]))

    iz(None)
    # pah önü: taş sahanlık, basamak, köşe direği (saçağı taşır)
    sah = [(X1 - PAH + 0.02, Y0 - 0.05, ZL), (X1 + 0.05, Y0 - 0.05, ZL), (X1 + 0.05, Y0 + PAH - 0.02, ZL)]
    prizma(g, sah, (0, 0, ZF - ZL - 0.03), R["basamak"], kapak=R["tas"])
    bas = [(X1 - PAH - 0.3, Y0 - 0.38, ZL), (X1 + 0.38, Y0 - 0.38, ZL), (X1 + 0.38, Y0 + PAH + 0.3, ZL),
           (X1 + 0.05, Y0 + PAH + 0.3, ZL), (X1 + 0.05, Y0 - 0.05, ZL), (X1 - PAH - 0.3, Y0 - 0.05, ZL)]
    # L biçimli alt basamak iki dışbükey parça hâlinde
    prizma(g, [bas[0], bas[1], (X1 + 0.38, Y0 - 0.05, ZL), bas[5]], (0, 0, (ZF - ZL) * 0.5), R["basamak"], kapak=R["tas"])
    prizma(g, [(X1 + 0.05, Y0 - 0.05, ZL), (X1 + 0.38, Y0 - 0.05, ZL), bas[2], bas[3]], (0, 0, (ZF - ZL) * 0.5), R["basamak"], kapak=R["tas"])
    dx, dy = X1 - 0.17, Y0 + 0.17
    kutu(g, dx - 0.15, dx + 0.15, dy - 0.15, dy + 0.15, ZF - 0.03, ZF + 0.28, R["tas"])
    dsil(g, dx, dy, ZF + 0.28, ZT - 0.22, 0.085, 8, dg, kapak=(False, False))
    dsil(g, dx, dy, ZF + 0.28, ZF + 0.36, 0.12, 8, dg2, kapak=(False, True))
    kutu(g, dx - 0.15, dx + 0.15, dy - 0.15, dy + 0.15, ZT - 0.22, ZT, dg2)
    # direkten duvarlara süs payandaları
    cubuk(g, (dx - 0.1, dy, ZT - 0.24), (dx - 0.55, dy, ZT - 0.02), 0.05, dg)
    cubuk(g, (dx, dy + 0.1, ZT - 0.24), (dx, dy + 0.55, ZT - 0.02), 0.05, dg)
    # dış paspas
    pm = pah.p((u0 + u1) / 2, 0.42, 0)
    dkutu(g, pm[0], pm[1], ZF - 0.03, ZF - 0.005, 1.0, 0.55, math.radians(45), R["paspas"])
    dkutu(g, pm[0], pm[1], ZF - 0.005, ZF + 0.002, 0.82, 0.38, math.radians(45), R["paspas2"])

    # =================================================== SAĞ CEPHE
    iz("Sag")
    u0, u1, z0, z1 = SAG_P
    cam_bosluk(g, sag, u0, u1, z0, z1, R["cam"], R["trim"], bolme=((u0 + u1) / 2,), yatay=(z0 + (z1 - z0) * 0.68,))
    sag.kutu(g, u0 - 0.12, u1 + 0.12, 0, 0.16, z0 - 0.08, z0, R["tas"])
    sag.kutu(g, u0 - 0.14, u1 + 0.14, 0, 0.1, z1, z1 + 0.16, R["trim"])
    for a, b in ((u0 - 0.58, u0 - 0.06), (u1 + 0.06, u1 + 0.58)):       # panjurlar
        sag.kutu(g, a, b, 0, 0.05, z0, z1, R["panjur"])
        for i in range(1, 6):
            zz = z0 + (z1 - z0) * i / 6
            sag.kutu(g, a + 0.04, b - 0.04, 0.05, 0.068, zz - 0.02, zz + 0.02, M("Panjur", "#25574a"))
    # pencere çiçekliği
    sag.kutu(g, u0 - 0.05, u1 + 0.05, 0.03, 0.3, z0 - 0.33, z0 - 0.09, R["ahsap_koyu"])
    for i in range(6):
        p = sag.p(u0 + 0.1 + (u1 - u0 - 0.2) * i / 5, 0.165, z0 - 0.09)
        meyve(g, p[0], p[1], p[2], 0.13, M("Cali", "#5f9a4f"))
        meyve(g, p[0] + 0.02, p[1], p[2] + 0.08, 0.07, M("Cicek", ("#e0525a", "#f2d04a", "#ffffff", "#f06a8a")[i % 4]), n=5)
    lamba_fener(g, sag, u1 + 0.95, ZF + 1.8, R)
    # klima dış ünitesi + duvar afişi (dondurma)
    B.klima(g, sag, 5.35, ZF + 2.05)
    for uu in (5.1, 5.6):
        sag.kutu(g, uu - 0.02, uu + 0.02, 0, 0.3, ZF + 1.98, ZF + 2.05, R["demir"])
    sag.kutu(g, 3.55, 4.55, 0, 0.035, ZF + 0.95, ZF + 2.25, R["ahsap_koyu"])
    sag.kutu(g, 3.6, 4.5, 0.035, 0.045, ZF + 1.0, ZF + 2.2, M("Afis", "#7ab8d6"))
    pc = sag.p(4.05, 0.05, ZF + 1.3)
    silindir(g, (pc[0], pc[1], ZF + 1.18), (pc[0], pc[1], ZF + 1.6), 0.03, 0.17, 6, M("Afis", "#e8b46a"), kapak=(False, False))
    for i, r in enumerate(("#f06a8a", "#f4f0e6", "#8a5a3a")):
        meyve(g, pc[0] + 0.012, pc[1] + (i - 1) * 0.1, ZF + 1.58 + (0.1 if i == 1 else 0), 0.11, M("Afis", r))
    # yağmur borusu (arka sağ köşe)
    iz(None)
    yagmur_borusu(g, X1 + 0.07, Y1 - 0.25, R)

    # =================================================== ARKA CEPHE
    iz("Arka")
    u0, u1, z0, z1 = DEPO
    t = 0.08
    arka.kutu(g, u0 - t, u0, -T - 0.02, 0.06, z0, z1 + t, R["tas_koyu"], arka=True)
    arka.kutu(g, u1, u1 + t, -T - 0.02, 0.06, z0, z1 + t, R["tas_koyu"], arka=True)
    arka.kutu(g, u0, u1, -T - 0.02, 0.06, z1, z1 + t, R["tas_koyu"], arka=True)
    dm = M("Kapi", "#5a6b72")
    arka.kutu(g, u0, u1, -T / 2 - 0.03, -T / 2 + 0.03, z0, z1, dm, arka=True)
    for i in range(1, 6):                                  # iki yüzde yatay çıtalar
        zz = z0 + (z1 - z0) * i / 6
        arka.kutu(g, u0 + 0.06, u1 - 0.06, -T / 2 + 0.03, -T / 2 + 0.045, zz - 0.02, zz + 0.02, M("Kapi", "#4a5a60"))
        arka.kutu(g, u0 + 0.06, u1 - 0.06, -T / 2 - 0.045, -T / 2 - 0.03, zz - 0.02, zz + 0.02, M("Kapi", "#4a5a60"), arka=True)
    arka.kutu(g, u1 - 0.2, u1 - 0.1, -T / 2 - 0.09, -T / 2 + 0.09, z0 + 0.95, z0 + 1.02, R["metal"], arka=True)
    iz(None)
    arka.kutu(g, u0 - 0.2, u1 + 0.2, 0, 0.5, ZL, ZF - 0.03, R["basamak"])               # arka basamak
    iz("Arka")
    arka.kutu(g, u0 - 0.3, u1 + 0.3, 0, 0.75, z1 + 0.22, z1 + 0.28, R["metal_koyu"])       # sundurma
    for uu in (u0 - 0.25, u1 + 0.25):
        cubuk(g, arka.p(uu, 0.7, z1 + 0.22), arka.p(uu, 0.02, z1 - 0.15), 0.03, R["demir"])
    lamba_fener(g, arka, u1 + 0.5, ZF + 1.8, R)
    u0, u1, z0, z1 = ARKA_P
    cam_bosluk(g, arka, u0, u1, z0, z1, R["cam2"], R["trim"], bolme=((u0 + u1) / 2,))
    arka.kutu(g, u0 - 0.1, u1 + 0.1, 0, 0.14, z0 - 0.08, z0, R["tas"])
    for i in range(5):                                    # demir parmaklık
        uu = u0 + 0.1 + (u1 - u0 - 0.2) * i / 4
        arka.kutu(g, uu - 0.012, uu + 0.012, 0.06, 0.085, z0, z1, R["demir"])
    arka.kutu(g, u0, u1, 0.06, 0.085, (z0 + z1) / 2 - 0.012, (z0 + z1) / 2 + 0.012, R["demir"])
    # elektrik sayacı ve boru
    arka.kutu(g, 4.0, 4.55, 0, 0.16, ZF + 1.0, ZF + 1.7, R["metal"])
    arka.kutu(g, 4.08, 4.47, 0.16, 0.175, ZF + 1.32, ZF + 1.6, R["demir"])
    arka.kutu(g, 4.24, 4.31, 0, 0.05, ZF + 1.7, ZT - 0.25, R["metal_koyu"])
    # havalandırma menfezi (soğutucuların arkası)
    arka.kutu(g, 5.3, 6.1, 0, 0.05, ZF + 0.35, ZF + 0.85, R["metal"])
    for i in range(4):
        arka.kutu(g, 5.36, 6.04, 0.05, 0.065, ZF + 0.43 + i * 0.1, ZF + 0.47 + i * 0.1, R["metal_koyu"])

    # =================================================== SOL CEPHE
    iz("Sol")
    for v in (SOL_P1, SOL_P2):
        u0, u1, z0, z1 = v
        cam_bosluk(g, sol, u0, u1, z0, z1, R["cam2"] if v is SOL_P1 else R["cam"], R["trim"], bolme=((u0 + u1) / 2,))
        sol.kutu(g, u0 - 0.1, u1 + 0.1, 0, 0.14, z0 - 0.08, z0, R["tas"])
        sol.kutu(g, u0 - 0.12, u1 + 0.12, 0, 0.09, z1, z1 + 0.13, R["trim"])
    iz(None)
    yagmur_borusu(g, X0 - 0.07, Y1 - 0.3, R)
    iz("Sol")
    # duvara boyanmış eski reklam: soluk çerçeve ve şerit
    sol.kutu(g, 3.3, 5.2, 0, 0.02, ZF + 0.85, ZF + 1.85, M("Afis", "#e9cfa0"))
    sol.kutu(g, 3.4, 5.1, 0.02, 0.03, ZF + 1.48, ZF + 1.72, M("Afis", "#c8452f"))
    yazi(g, sol, 4.25, ZF + 1.17, "COLA", 0.045, 0.02, 0.03, M("Afis", "#8a2f2a"))
    iz(None)
    duvar_ayir(g, izler)

    return f


def yagmur_borusu(g, x, y, R):
    dsil(g, x, y, ZL, ZT - 0.02, 0.05, 6, R["oluk"], kapak=(False, False))
    for z in (ZL + 0.5, ZL + 1.7, ZT - 0.5):
        kutu(g, x - 0.07, x + 0.07, y - 0.07, y + 0.07, z, z + 0.05, R["metal_koyu"], atla=())
    # dirsek: ağzı dışarı bakar
    kutu(g, x - 0.06, x + 0.06, y - 0.06, y + 0.06, ZL, ZL + 0.1, R["oluk"])


# ================================== DIŞ EŞYALAR ==================================

def bisiklet(g, x, y, aci, renk, R, yatik=0.16):
    """Duvara yaslı bisiklet (çekirdekteki bisiklet zemini 0 sayıyor: burada ZL'ye taşınır ve hafifçe yatırılır)."""
    t = G("tmp")
    B.bisiklet(t, 0, 0, 0, renk)
    # gidon ve sepet
    cubuk(t, (0.3, -0.22, 0.87), (0.3, 0.22, 0.87), 0.035, M("Metal", "#c7c9cc"))
    kutu(t, 0.36, 0.62, -0.17, 0.17, 0.62, 0.84, M("Hasir", "#c49a6c"))
    kutu(t, 0.39, 0.59, -0.14, 0.14, 0.835, 0.85, M("Hasir", "#8a6a42"))
    ca, sa = math.cos(aci), math.sin(aci)
    for f, m in zip(t.f, t.fm):
        pts = []
        for i in f:
            px, py, pz = t.v[i]
            py = py + pz * yatik               # yana yatır (tekerlek tabanı yerinde kalır)
            pts.append((x + px * ca - py * sa, y + px * sa + py * ca, pz + ZL))
        g.sid += 1 if (not pts) else 0
        g.yuz(pts, t.mats[m])
    g.sid += 1


def sise_sandigi(g, cx, cy, z, aci, renk, rnd, dolu=True, kapak=None):
    """Plastik içecek kasası: üstünde şişe ağızları (dolu) ya da boş gözler."""
    m = M("Plastik", renk)
    w, d, h = 0.42, 0.32, 0.27
    dkutu(g, cx, cy, z, z + h, w, d, aci, m)
    dkutu(g, cx, cy, z + h - 0.004, z + h + 0.008, w - 0.06, d - 0.06, aci, M("Plastik", koyu(renk, 0.55)))
    dkutu(g, cx, cy, z + h * 0.45, z + h * 0.72, w + 0.012, d + 0.012, aci, M("Plastik", koyu(renk, 0.82)))
    if dolu:
        ca, sa = math.cos(aci), math.sin(aci)
        km = M("Kapak", kapak or "#d8d8d8")
        for i in range(4):
            for j in range(3):
                lx, ly = (i - 1.5) * 0.085, (j - 1) * 0.085
                dsil(g, cx + lx * ca - ly * sa, cy + lx * sa + ly * ca, z + h, z + h + 0.045, 0.027, 5, km)
    return z + h


def el_arabasi(g, x, y, aci, R):
    """Depo el arabası (duvara dayalı)."""
    ca, sa = math.cos(aci), math.sin(aci)

    def P(u, v, z):
        return (x + u * ca - v * sa, y + u * sa + v * ca, z)
    for s in (-0.2, 0.2):
        cubuk(g, P(s, 0.0, ZL + 0.12), P(s, 0.28, ZL + 1.15), 0.035, M("Metal", "#c8342f"))
        silindir(g, P(s * 1.25 - 0.02, 0, ZL + 0.11), P(s * 1.25 + 0.02, 0, ZL + 0.11), 0.11, 0.11, 8, R["lastik"])
    cubuk(g, P(-0.2, 0.28, ZL + 1.15), P(0.2, 0.28, ZL + 1.15), 0.035, M("Metal", "#c8342f"))
    cubuk(g, P(-0.2, 0.12, ZL + 0.55), P(0.2, 0.12, ZL + 0.55), 0.03, M("Metal", "#c8342f"))
    obox(g, P(0, -0.15, ZL + 0.03), (0.22 * ca, 0.22 * sa, 0), (-0.16 * sa, 0.16 * ca, 0), (0, 0, 0.012), R["metal_koyu"])


def tezgah_meyve(g, R, rnd):
    """Kaldırıma taşan manav tezgâhı: sol vitrinin önünde üç kademeli stand, sağ vitrinin önünde sehpa ve kasalar."""
    ah, ah2 = R["ahsap"], R["ahsap_acik"]
    # ---- kademeli stand (sol vitrin önü)
    xa, xb = -4.45, -1.5
    yd = Y0 - 0.14                     # duvar dibi
    kademe = [(yd - 0.42, yd, ZL + 0.92), (yd - 0.84, yd - 0.42, ZL + 0.66), (yd - 1.28, yd - 0.84, ZL + 0.4)]
    for x in (xa + 0.06, (xa + xb) / 2, xb - 0.06):              # yan taşıyıcılar
        for ya, yb, zz in kademe:
            kutu(g, x - 0.04, x + 0.04, ya, yb, ZL, zz - 0.04, ah)
    for ya, yb, zz in kademe:
        kutu(g, xa, xb, ya - 0.02, yb, zz - 0.04, zz, ah2, atla=())
    cesit = [
        [("#d8402f", "#c8342f", "#e05a3a"), ("#f08a2a", "#f29a3a"), ("#f2d04a", "#ead24a"), ("#8ab84a", "#7aa83f")],          # elma, portakal, limon, armut
        [("#d8402f", "#e0503a"), ("#6a9a3a", "#5a8a34"), ("#7a3a6a", "#8a4a7a"), ("#f2a03a", "#f0b04a")],                   # domates, biber, patlıcan, mandalina
        [("#c9a26a", "#b8905a"), ("#b8773a", "#c8854a"), ("#e8e0c8", "#f0e8d0"), ("#5a9a4a", "#6aaa52")],                   # patates, soğan, sarımsak, salatalık
    ]
    for k, (ya, yb, zz) in enumerate(kademe):
        n = 4
        w = (xb - xa - 0.16) / n
        for i in range(n):
            cx = xa + 0.08 + w * (i + 0.5)
            cy = (ya + yb) / 2 - 0.01
            ust = kasa(g, cx, cy, zz, w - 0.07, 0.36, 0.17, 0, R["ahsap_acik"] if (i + k) % 2 else R["ahsap"], M("Dolgu", koyu(cesit[k][i][0], 0.86)))
            meyve_yigini(g, cx - w / 2 + 0.08, cx + w / 2 - 0.08, cy - 0.15, cy + 0.15, ust - 0.01, 0.062 if k < 2 else 0.055,
                         cesit[k][i], rnd)
            # fiyat etiketi (kasanın önünde küçük kart)
            kutu(g, cx - 0.07, cx + 0.07, ya - 0.035, ya - 0.022, zz + 0.05, zz + 0.16, R["kagit"], atla=())
            kutu(g, cx - 0.045, cx + 0.045, ya - 0.042, ya - 0.035, zz + 0.09, zz + 0.12, M("Afis", "#c8342f"), atla=())
    # muz hevenkleri: standın üst rafının üstünde asılı çubuk
    for x in (xa + 0.05, xb - 0.05):
        cubuk(g, (x, yd - 0.3, ZL + 0.92), (x, yd - 0.3, ZL + 1.75), 0.04, ah)
    cubuk(g, (xa + 0.05, yd - 0.3, ZL + 1.73), (xb - 0.05, yd - 0.3, ZL + 1.73), 0.04, ah)
    mz = M("Meyve", "#f2d04a")
    for i in range(5):
        x = xa + 0.45 + i * 0.52
        cubuk(g, (x, yd - 0.3, ZL + 1.71), (x, yd - 0.3, ZL + 1.6), 0.015, R["demir"])
        for j in range(4):
            a = (j - 1.5) * 0.3
            cubuk(g, (x + math.sin(a) * 0.03, yd - 0.3, ZL + 1.6), (x + math.sin(a) * 0.16, yd - 0.33, ZL + 1.36), 0.05, mz)

    # ---- sehpa + sebze kasaları (sağ vitrin önü)
    xa, xb = -0.85, 2.0
    ya, yb = yd - 0.8, yd
    for x in (xa + 0.12, xb - 0.12):
        for y in (ya + 0.08, yb - 0.08):
            cubuk(g, (x, y, ZL), (x, y, ZL + 0.66), 0.06, ah)
        cubuk(g, (x, ya + 0.08, ZL + 0.25), (x, yb - 0.08, ZL + 0.25), 0.04, ah)
    kutu(g, xa, xb, ya, yb, ZL + 0.66, ZL + 0.72, ah2, atla=())
    sebze = [("#e8823a", "#f0923f"), ("#8fc45a", "#a0d06a"), ("#f4f0e0", "#e8e4d0"), ("#c8342f", "#b82a2a"), ("#7ab84a", "#5f9a3f"), ("#e8c84a", "#f2d86a")]
    for j in range(2):
        for i in range(3):
            cx = xa + 0.5 + i * 0.93
            cy = ya + 0.22 + j * 0.38
            ust = kasa(g, cx, cy, ZL + 0.72 + j * 0.06, 0.84, 0.34, 0.16 + j * 0.05, 0, ah if (i + j) % 2 else R["ahsap_acik"],
                       M("Dolgu", koyu(sebze[j * 3 + i][0], 0.86)))
            meyve_yigini(g, cx - 0.36, cx + 0.36, cy - 0.12, cy + 0.12, ust - 0.01, 0.06, sebze[j * 3 + i], rnd)
    # yerde karpuz kasası + yarım karpuz + kavunlar
    kasa(g, xa + 0.55, ya - 0.5, ZL, 0.95, 0.62, 0.36, 0.06, ah, M("Dolgu", "#5a4a32"))
    for (dx, dy) in ((-0.22, -0.1), (0.2, -0.12), (-0.02, 0.14)):
        top(g, (xa + 0.55 + dx, ya - 0.5 + dy, ZL + 0.5), 0.2, 0.2, 0.18, M("Meyve", "#2f6b34"))
    top(g, (xa + 0.86, ya - 0.33, ZL + 0.46), 0.14, 0.14, 0.13, M("Meyve", "#e8c84a"))
    # yarım karpuz (kesik yüzü kırmızı) sehpanın ucunda
    dsil(g, xb - 0.2, ya + 0.22, ZL + 0.72, ZL + 0.86, 0.1, 8, M("Meyve", "#2f6b34"), r1=0.19, kapak=(True, True), kapak_mat=M("Meyve", "#e0483f"))
    # terazi (asma kefeli) sehpanın üstünde
    tx, ty = xb - 0.22, yb - 0.22
    kutu(g, tx - 0.14, tx + 0.14, ty - 0.1, ty + 0.1, ZL + 0.72, ZL + 0.8, R["metal_koyu"])
    cubuk(g, (tx, ty, ZL + 0.8), (tx, ty, ZL + 1.15), 0.03, R["metal_koyu"])
    dsil(g, tx, ty, ZL + 1.15, ZL + 1.18, 0.17, 8, R["metal"], kapak=(True, True))
    # yerde patates ve soğan çuvalları
    for i, (r, r2) in enumerate((("#b8905a", "#c9a26a"), ("#a8552f", "#b8773a"))):
        x, y = xb - 0.55 + i * 0.5, ya - 0.42 - i * 0.05
        dsil(g, x, y, ZL, ZL + 0.42, 0.2, 8, M("Cuval", "#c9b48a"), r1=0.17, kapak=(False, True), kapak_mat=M("Dolgu", koyu(r, 0.75)))
        meyve_yigini(g, x - 0.12, x + 0.12, y - 0.12, y + 0.12, ZL + 0.41, 0.055, (r, r2), rnd)


def dis_esyalar(g, R, rnd, f):
    on, pah, sag, arka, sol = f["on"], f["pah"], f["sag"], f["arka"], f["sol"]
    tezgah_meyve(g, R, rnd)

    # ---- tentenin ön kenarı boyunca ampul dizisi (Isik_: gece yanar)
    zs0 = ZF + 2.4
    ya, za = Y0 - 1.57, zs0 + 0.26 - 0.52 - 0.25
    xa, xb = X0 + 0.38, X0 + on.L - 0.28
    cubuk(g, (xa, ya, za + 0.05), (xb, ya, za + 0.05), 0.02, R["demir"])
    n = 15
    for i in range(n):
        x = xa + 0.12 + (xb - xa - 0.24) * i / (n - 1)
        meyve(g, x, ya, za + 0.05, 0.055, R["isik"], n=5, rz=-0.11)
    # ---- sakız makinesi ve posta kutusu (sağ duvarın başında, sahanlığın yanında)
    mx, my = X1 + 0.3, Y0 + PAH + 0.27
    dsil(g, mx, my, ZL, ZL + 0.06, 0.17, 8, R["demir"])
    dsil(g, mx, my, ZL + 0.06, ZL + 0.78, 0.045, 6, R["demir"], kapak=(False, False))
    kutu(g, mx - 0.13, mx + 0.13, my - 0.13, my + 0.13, ZL + 0.78, ZL + 0.98, M("Plastik", "#c8342f"))
    kutu(g, mx + 0.13, mx + 0.15, my - 0.05, my + 0.05, ZL + 0.82, ZL + 0.9, R["metal"], atla=())
    top(g, (mx, my, ZL + 1.14), 0.17, 0.17, 0.17, M("Kristal", "#d5ebf2"))
    for i in range(7):
        a = i * 0.9
        meyve(g, mx + math.cos(a) * 0.12, my + math.sin(a) * 0.12, ZL + 1.0 + (i % 3) * 0.05, 0.05,
              M("Urun", ("#d94f3d", "#f2d04a", "#5a9a4a", "#3d5a8a", "#e07a9a")[i % 5]), n=5)
    dsil(g, mx, my, ZL + 1.29, ZL + 1.35, 0.09, 6, M("Plastik", "#c8342f"), r1=0.04, kapak=(False, True))
    sag.kutu(g, 2.95, 3.3, 0, 0.16, ZF + 1.05, ZF + 1.5, M("Plastik", "#e0b23a"))
    sag.kutu(g, 2.93, 3.32, 0, 0.18, ZF + 1.5, ZF + 1.55, M("Plastik", "#c79a2a"))
    sag.kutu(g, 3.0, 3.25, 0.16, 0.17, ZF + 1.36, ZF + 1.4, R["demir"])

    # ---- ekmek dolabı (sol ön ayakta, vitrinin solunda)
    ex0, ex1, ey1 = X0 + 0.02, X0 + 0.62, Y0 - 0.1
    ey0 = ey1 - 0.5
    kutu(g, ex0, ex1, ey0, ey1, ZL, ZL + 0.5, R["ahsap_koyu"])
    kutu(g, ex0, ex1, ey1 - 0.04, ey1, ZL + 0.5, ZL + 1.75, R["ahsap_koyu"])                     # arka
    for x in (ex0, ex1 - 0.04):
        kutu(g, x, x + 0.04, ey0, ey1 - 0.04, ZL + 0.5, ZL + 1.75, R["ahsap_koyu"])
    kutu(g, ex0 - 0.03, ex1 + 0.03, ey0 - 0.03, ey1, ZL + 1.75, ZL + 1.83, R["ahsap"], atla=())
    for i, zz in enumerate((ZL + 0.5, ZL + 0.92, ZL + 1.34)):
        kutu(g, ex0 + 0.04, ex1 - 0.04, ey0 + 0.02, ey1 - 0.04, zz, zz + 0.03, R["ahsap_acik"])
        for k in range(3):                                         # somunlar
            x = ex0 + 0.13 + k * 0.17
            top(g, (x, ey0 + 0.2, zz + 0.11), 0.075, 0.15, 0.085, M("Ekmek", ("#c98a4a", "#b8773a", "#d69a5a")[(i + k) % 3]))
    for x in (ex0 + 0.04, ex1 - 0.065):                              # cam kapak çerçevesi
        kutu(g, x, x + 0.025, ey0, ey0 + 0.02, ZL + 0.52, ZL + 1.73, R["ahsap"])
    yazi(g, cer((ex0, ey0), (0, -1), 0.6), 0.3, ZL + 0.27, "EKMEK", 0.017, 0.0, 0.012, R["yazi"])

    # ---- gazete standı (sağ ayakta, kapının yanında)
    gx, gy = X1 - PAH - 0.42, Y0 - 0.34
    for s in (-0.3, 0.3):
        cubuk(g, (gx + s, gy - 0.16, ZL + 0.006), (gx + s, gy + 0.14, ZL + 1.3), 0.03, R["demir"])
        cubuk(g, (gx + s, gy + 0.2, ZL + 0.006), (gx + s, gy + 0.14, ZL + 1.3), 0.03, R["demir"])
    for i in range(4):
        zz = ZL + 0.22 + i * 0.27
        yy = gy - 0.12 + i * 0.065
        obox(g, (gx, yy, zz), (0.31, 0, 0), (0, 0.11, 0.04), (0, -0.006, 0.016), R["demir"])
        for k in range(2):
            x = gx - 0.15 + k * 0.3
            obox(g, (x, yy - 0.01, zz + 0.1), (0.125, 0, 0), (0, 0.03, 0.1), (0, -0.02, 0.006), R["kagit"] if (i + k) % 2 else R["kagit2"])
            obox(g, (x, yy - 0.032, zz + 0.155), (0.1, 0, 0), (0, 0.007, 0.025), (0, -0.004, 0.0012),
                 M("Afis", ("#2b2d31", "#c8342f", "#2f5a9a", "#2b2d31")[(i + k) % 4]))
    kutu(g, gx - 0.33, gx + 0.33, gy + 0.11, gy + 0.14, ZL + 1.3, ZL + 1.5, M("Afis", "#2f5a9a"), atla=())
    yazi(g, cer((gx - 0.33, gy + 0.11), (0, -1), 0.66), 0.33, ZL + 1.4, "NEWS", 0.02, 0.0, 0.01, R["yazi"])

    # ---- kola / su kasaları ve su damacanaları (sol ön köşe, yan bahçenin ağzı)
    bx, by = X0 - 0.75, Y0 + 0.55
    renk = ("#c8342f", "#c8342f", "#2f5a9a", "#e0b23a", "#3f8a4a")
    for i, (dx, dy, kat) in enumerate(((0, 0, 4), (0.5, 0.02, 3), (0.02, 0.42, 3), (0.52, 0.44, 2), (-0.45, 0.2, 2))):
        z = ZL
        for k in range(kat):
            r = renk[(i + k // 2) % len(renk)]
            z = sise_sandigi(g, bx + dx, by + dy, z, rnd.uniform(-0.06, 0.06), r, rnd, dolu=(k == kat - 1),
                             kapak="#f4f0e6" if r != "#2f5a9a" else "#7ab8d6")
    for i in range(3):                                                # damacanalar
        x, y = bx - 0.5 + i * 0.36, by - 0.5
        dsil(g, x, y, ZL, ZL + 0.36, 0.14, 8, M("Kristal", "#9fd0e6"), kapak=(False, False))
        dsil(g, x, y, ZL + 0.36, ZL + 0.46, 0.14, 8, M("Kristal", "#9fd0e6"), r1=0.05, kapak=(False, False))
        dsil(g, x, y, ZL + 0.46, ZL + 0.52, 0.05, 6, M("Kapak", "#2f5a9a"))

    # ---- sağ yan: dondurma dolabı, bank, bisiklet, çöp kutusu, saksılar
    dondurma_dolabi(g, cer((X1 + 0.03, Y0 + PAH + 0.55), (1, 0), 1.5), 0.0, 1.5, R, rnd, dis=True)
    # güneş şemsiyesi (dondurma dolabının üstünde)
    sx, sy = X1 + 1.1, Y0 + PAH + 1.3
    dsil(g, sx, sy, ZL, ZL + 0.1, 0.24, 8, R["tas_koyu"])
    cubuk(g, (sx, sy, ZL + 0.1), (sx, sy, ZL + 2.45), 0.05, R["beyaz"])
    n = 8
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        m = M("Tente", R["tente1"] if i % 2 == 0 else R["tente2"])
        p0 = (sx, sy, ZL + 2.52)
        p1 = (sx + math.cos(a0) * 0.98, sy + math.sin(a0) * 0.98, ZL + 2.14)
        p2 = (sx + math.cos(a1) * 0.98, sy + math.sin(a1) * 0.98, ZL + 2.14)
        q = [(p[0], p[1], p[2] - 0.035) for p in (p0, p1, p2)]
        kati(g, [p0, p1, p2] + q, [[0, 1, 2], [3, 4, 5], [1, 2, 5, 4], [0, 1, 4, 3], [0, 2, 5, 3]], m)
    dsil(g, sx, sy, ZL + 2.5, ZL + 2.6, 0.04, 6, R["pirinc"])

    by0 = 1.25
    t = G("tmp")
    B.bank(t, 0, 0, rnd, 0.0, "#2f6b57")
    for fc, m in zip(t.f, t.fm):          # bank: sırtı duvara, yüzü +X'e
        g.yuz([(X1 + 0.42 + p[1] * -1.0, by0 + p[0], p[2]) for p in (t.v[i] for i in reversed(fc))], t.mats[m])
    g.sid += 1
    # bankın üstünde unutulmuş file ve yanında kedi
    kutu(g, X1 + 0.3, X1 + 0.55, by0 - 0.6, by0 - 0.35, ZL + 0.49, ZL + 0.72, M("Hasir", "#c49a6c"))
    kedi(g, X1 + 0.62, by0 + 0.55, ZL + 0.49, math.radians(100), "#e8a23a")
    bisiklet(g, X1 + 0.4, 3.55, math.radians(90), "#c8452f", R)
    saksi(g, X1 + 0.45, 2.45, ZL, 0.24, rnd, R, cicek=("#e0525a", "#f2d04a", "#ffffff"))

    # çöp kutusu (köşe direğinin sokak tarafında) ve iki saksı (kapının iki yanında)
    cx, cy = X1 + 1.35, Y0 - 0.2
    dsil(g, cx, cy, ZL, ZL + 0.8, 0.24, 10, M("Metal", "#3f6b45"), r1=0.28, kapak=(False, True), kapak_mat=R["demir"])
    dsil(g, cx, cy, ZL + 0.8, ZL + 0.86, 0.3, 10, R["demir"], kapak=(True, True))
    dsil(g, cx, cy, ZL + 0.3, ZL + 0.36, 0.265, 10, M("Metal", "#2f5a3a"), kapak=(False, False))
    p = pah.p(-0.28, 0.5, 0)
    saksi(g, p[0], p[1], ZL, 0.3, rnd, R, cicek=("#e0525a", "#f06a8a", "#ffffff"), boy=1.1)
    p = pah.p(pah.L + 0.36, 0.52, 0)
    saksi(g, p[0], p[1], ZL, 0.3, rnd, R, cicek=("#f2d04a", "#ff8a3c", "#ffffff"), boy=1.1)

    # A tabela (kaldırımda, kapının önünde)
    ax, ay, aa = X1 - 0.35, Y0 - 2.2, math.radians(-20)
    ZA = ZL + 0.012          # A tabelanın eğik ayakları zeminin altına inmesin
    ca, sa = math.cos(aa), math.sin(aa)
    for s in (-1, 1):
        nrm = (-sa * s, ca * s)
        c = (ax + nrm[0] * 0.13, ay + nrm[1] * 0.13, ZA + 0.5)
        obox(g, c, (ca * 0.3, sa * 0.3, 0), (nrm[0] * 0.012, nrm[1] * 0.012, 0.004), (-nrm[0] * 0.13, -nrm[1] * 0.13, 0.5), R["ahsap_koyu"])
        # tebeşir tahtası yüzü: eğim küçük olduğu için dik sayılır
        obox(g, (ax + nrm[0] * 0.15, ay + nrm[1] * 0.15, ZA + 0.52), (ca * 0.24, sa * 0.24, 0), (nrm[0] * 0.004, nrm[1] * 0.004, 0.001),
             (-nrm[0] * 0.1, -nrm[1] * 0.1, 0.38), M("Tabela", "#2b3a34"))
        for i in range(4):
            zz = ZA + 0.76 - i * 0.14
            off = 0.105 + (0.5 - (zz - ZA)) * 0.26 + 0.058
            obox(g, (ax + nrm[0] * off, ay + nrm[1] * off, zz), (ca * (0.17 - 0.03 * (i % 2)), sa * (0.17 - 0.03 * (i % 2)), 0),
                 (nrm[0] * 0.004, nrm[1] * 0.004, 0), (0, 0, 0.03), R["yazi"] if i != 1 else M("Yazi", "#f2d04a"))

    # ---- arka avlu: depo kapısının çevresi
    # palet + koli yığını
    px, py = X1 - 4.1, Y1 + 0.95
    for i in range(3):
        kutu(g, px - 0.6 + i * 0.5, px - 0.4 + i * 0.5, py - 0.4, py + 0.4, ZL, ZL + 0.1, R["ahsap"])
    kutu(g, px - 0.6, px + 0.6, py - 0.4, py + 0.4, ZL + 0.1, ZL + 0.14, R["ahsap_acik"], atla=())
    koli = [(-0.3, -0.12, 0.5, 0.42, 0.36, 0), (0.28, -0.1, 0.46, 0.4, 0.3, 0.05), (-0.25, 0.0, 0.45, 0.38, 0.3, 0.1), (0.3, 0.02, 0.4, 0.36, 0.26, -0.08)]
    z1 = ZL + 0.14
    for i, (dx, dy, w, d, h, a) in enumerate(koli):
        zz = z1 if i < 2 else z1 + 0.33
        dkutu(g, px + dx, py + dy, zz, zz + h, w, d, a, R["karton"] if i % 2 == 0 else R["karton2"])
        dkutu(g, px + dx, py + dy, zz + h, zz + h + 0.004, w * 0.16, d, a, M("Bant", "#e8dcc0"))
    # boş şişe sandıkları (depo kapısının öbür yanı)
    sx0, sy0 = X1 - 0.75, Y1 + 0.5
    for i, (dx, dy, kat) in enumerate(((0, 0, 4), (0.0, 0.42, 3), (-0.52, 0.02, 2), (-0.5, 0.45, 5))):
        z = ZL
        for k in range(kat):
            z = sise_sandigi(g, sx0 + dx, sy0 + dy, z, math.pi / 2 + rnd.uniform(-0.05, 0.05),
                             ("#3f8a4a", "#e0b23a", "#c8342f", "#2f5a9a")[(i + k) % 4], rnd, dolu=(k == kat - 1 and i % 2 == 0), kapak="#6a8a5a")
    el_arabasi(g, X1 - 3.45, Y1 + 0.2, math.pi, R)
    # tekerlekli çöp konteyneri
    kx, ky = X0 - 1.15, Y1 - 0.75        # sol yan geçidin arka ucunda (arka avlu tedarik kamyonetine kalır)
    kutu(g, kx - 0.4, kx + 0.4, ky - 0.55, ky + 0.55, ZL + 0.12, ZL + 1.05, M("Plastik", "#3f6b45"))
    obox(g, (kx + 0.02, ky, ZL + 1.09), (0.45, 0, 0.03), (0, 0.6, 0), (-0.003, 0, 0.035), M("Plastik", "#2f5a3a"))
    for sx_ in (-0.3, 0.3):
        for sy_ in (-0.42, 0.42):
            silindir(g, (kx + sx_, ky + sy_ - 0.03, ZL + 0.08), (kx + sx_, ky + sy_ + 0.03, ZL + 0.08), 0.08, 0.08, 6, R["lastik"])
    # tüp (gaz) ve kova, süpürge
    dsil(g, X0 + 3.2, Y1 + 0.3, ZL, ZL + 0.55, 0.16, 8, M("Metal", "#3d6a9a"), kapak=(False, True))
    dsil(g, X0 + 3.2, Y1 + 0.3, ZL + 0.55, ZL + 0.65, 0.09, 6, R["metal_koyu"], kapak=(False, True))
    dsil(g, X0 + 3.75, Y1 + 0.35, ZL, ZL + 0.3, 0.13, 8, M("Plastik", "#2f5a9a"), r1=0.16, kapak=(False, True), kapak_mat=M("Kristal", "#9fd0e6"))
    cubuk(g, (X0 + 4.2, Y1 + 0.42, ZL + 0.12), (X0 + 4.2, Y1 + 0.08, ZL + 1.5), 0.035, R["ahsap_acik"])
    obox(g, (X0 + 4.2, Y1 + 0.44, ZL + 0.095), (0.17, 0, 0), (0, 0.05, 0.01), (0, -0.015, 0.08), M("Hasir", "#d8b46a"))
    # çamaşır ipi yerine: duvara dayalı tahta merdiven
    for s in (-0.22, 0.22):
        cubuk(g, (X0 + 5.4 + s, Y1 + 0.75, ZL + 0.03), (X0 + 5.4 + s, Y1 + 0.06, ZL + 2.6), 0.05, R["ahsap_acik"])
    for i in range(7):
        tt = (i + 0.7) / 8
        cubuk(g, (X0 + 5.18, Y1 + 0.75 - 0.69 * tt, ZL + 2.6 * tt), (X0 + 5.62, Y1 + 0.75 - 0.69 * tt, ZL + 2.6 * tt), 0.04, R["ahsap_acik"])

    # ---- sol yan: dar geçit
    # yağmur fıçısı, tahta paletler (duvara dayalı), saksıda domates
    dsil(g, X0 - 0.5, Y0 + 1.1, ZL, ZL + 0.85, 0.3, 10, M("Ahsap", "#7a5a3a"), kapak=(False, True), kapak_mat=M("Kristal", "#7ab8d6"))
    for z in (ZL + 0.15, ZL + 0.65):
        dsil(g, X0 - 0.5, Y0 + 1.1, z, z + 0.05, 0.315, 10, R["demir"], kapak=(False, False))
    for i in range(2):
        yy = 1.2 + i * 0.1
        xx = X0 - 0.12 - i * 0.12
        for k in range(4):
            obox(g, (xx - 0.1, yy + 1.0, ZL + 0.12 + k * 0.3), (0.012, 0, 0.004), (0, 0.6, 0), (-0.02, 0, 0.05), R["ahsap_acik"])
        for k in range(3):
            obox(g, (xx - 0.07, yy + 0.45 + k * 0.55, ZL + 0.58), (0.03, 0, 0.008), (0, 0.045, 0), (-0.14, 0, 0.56), R["ahsap"])
    for i in range(3):
        saksi(g, X0 - 0.55, -0.3 - i * 0.62 + 0.0, ZL, 0.2, rnd, R, cicek=("#d8402f", "#e0503a") if i != 1 else ("#f2d04a",), boy=1.5 if i != 1 else 1.0)
    # arka sol köşede yaşlı asma: gövde + yaprak kümeleri duvarda
    ax, ay = X0 - 0.12, Y1 - 0.55
    cubuk(g, (ax, ay, ZL + 0.02), (ax + 0.02, ay - 0.25, ZL + 1.5), 0.07, M("Ahsap", "#6b4a32"))
    cubuk(g, (ax + 0.02, ay - 0.25, ZL + 1.5), (ax, ay - 1.2, ZL + 2.6), 0.055, M("Ahsap", "#6b4a32"))
    for i in range(9):
        yy = ay - 0.1 - i * 0.2 + rnd.uniform(-0.05, 0.05)
        zz = ZL + 1.4 + i * 0.14 + rnd.uniform(-0.15, 0.2)
        top(g, (ax - 0.02, yy, zz), 0.13, 0.3, 0.26, M("Cali", rnd.choice(("#5f9a4f", "#4f8c4a", "#6ea552"))), rnd, 0.12)


def kedi(g, x, y, z, aci, renk):
    """Oturan kedi (birkaç kutu ve küre)."""
    m = M("Kedi", renk)
    ca, sa = math.cos(aci), math.sin(aci)
    top(g, (x, y, z + 0.12), 0.13, 0.13, 0.13, m)
    top(g, (x + ca * 0.1, y + sa * 0.1, z + 0.28), 0.085, 0.085, 0.08, m)
    for s in (-1, 1):
        kx, ky = x + ca * 0.11 - sa * 0.05 * s, y + sa * 0.11 + ca * 0.05 * s
        dsil(g, kx, ky, z + 0.33, z + 0.4, 0.03, 4, m, r1=0.0)
    cubuk(g, (x - ca * 0.1, y - sa * 0.1, z + 0.04), (x - ca * 0.14 - sa * 0.16, y - sa * 0.14 + ca * 0.16, z + 0.05), 0.04, m)


def dondurma_dolabi(g, c, u0, w, R, rnd, dis=False):
    """Sandık tipi dondurma dolabı: arkası c'nin o = 0 çizgisinde, önü n yönünde. Yarı açık sürgülü cam kapak."""
    d, h = 0.72, 0.9
    z = ZL if dis else ZF
    by = R["beyaz"]
    kuyu = M("Kuyu", "#2f4a6b")
    c.kutu(g, u0, u0 + w, 0, d, z, z + 0.12, R["metal_koyu"], arka=True)
    c.kutu(g, u0, u0 + w, 0, 0.07, z + 0.12, z + h, by, arka=True)
    c.kutu(g, u0, u0 + w, d - 0.07, d, z + 0.12, z + h, by, arka=True)
    c.kutu(g, u0, u0 + 0.07, 0.07, d - 0.07, z + 0.12, z + h, by, arka=True)
    c.kutu(g, u0 + w - 0.07, u0 + w, 0.07, d - 0.07, z + 0.12, z + h, by, arka=True)
    c.kutu(g, u0 + 0.07, u0 + w - 0.07, 0.07, d - 0.07, z + 0.12, z + h - 0.3, kuyu, arka=True)
    # içindeki dondurma kutuları
    renk = ("#f06a8a", "#f4f0e6", "#8a5a3a", "#f2d04a", "#7ab8d6", "#5a9a4a", "#e8823a", "#c8342f")
    nx, ny = int((w - 0.2) / 0.19), 2
    for i in range(nx):
        for j in range(ny):
            if rnd.random() < 0.12:
                continue
            uu = u0 + 0.11 + i * (w - 0.22) / nx
            oo = 0.1 + j * 0.27
            c.kutu(g, uu, uu + 0.15, oo, oo + 0.23, z + h - 0.3, z + h - 0.3 + rnd.uniform(0.07, 0.16), M("Urun", rnd.choice(renk)), arka=True)
    # sürgülü cam kapak: bir yarısı kapalı
    c.kutu(g, u0 + w * 0.52, u0 + w - 0.03, 0.04, d - 0.04, z + h, z + h + 0.02, R["kristal"], arka=True)
    c.kutu(g, u0 + 0.03, u0 + w - 0.03, 0.03, 0.055, z + h, z + h + 0.035, R["metal"], arka=True)
    c.kutu(g, u0 + 0.03, u0 + w - 0.03, d - 0.055, d - 0.03, z + h, z + h + 0.035, R["metal"], arka=True)
    c.kutu(g, u0 + w * 0.5, u0 + w * 0.54, 0.055, d - 0.055, z + h, z + h + 0.035, R["metal"], arka=True)
    # ön yüzde renkli şerit ve külah ikonları
    c.kutu(g, u0, u0 + w, d, d + 0.012, z + 0.3, z + 0.7, M("Afis", "#e8527a"))
    c.kutu(g, u0, u0 + w, d + 0.012, d + 0.02, z + 0.62, z + 0.66, R["yazi"] if dis else M("Afis", "#fff2c8"))
    for k in range(3):
        p = c.p(u0 + w * (0.22 + 0.28 * k), d + 0.014, z + 0.36)
        silindir(g, (p[0], p[1], z + 0.34), (p[0], p[1], z + 0.48), 0.012, 0.055, 5, M("Afis", "#e8b46a"), kapak=(False, False))
        meyve(g, p[0], p[1], z + 0.47, 0.06, M("Afis", ("#f4f0e6", "#8a5a3a", "#f2d04a")[k]), n=5)


# ================================== İÇ MEKÂN ==================================

def urun_dizisi(g, c, u0, u1, od, z, hmax, rnd, tur=None, dp=0.16):
    """Rafın ön kenarına (o = od) ürün grupları dizer: kutu, şişe, kavanoz, konserve, paket, yatık kutu."""
    u = u0 + 0.02
    while u < u1 - 0.1:
        t = tur or rnd.choice(("kutu", "kutu", "kutu", "sise", "kavanoz", "konserve", "paket", "yatik", "kutu"))
        renk = rnd.choice(URUN)
        m = M("Urun", renk)
        adet = rnd.randint(2, 5)
        gs = g
        g = urun_g(gs, rnd)          # bu grup hangi stok seviyesinde (raf boşalırken gizlenir)
        if t == "kutu":
            w = rnd.uniform(0.11, 0.2)
            h = min(hmax - 0.04, rnd.uniform(0.16, 0.3))
            etiket = M("Urun", rnd.choice(URUN)) if rnd.random() < 0.45 else None
            for _ in range(adet):
                if u + w > u1 - 0.01:
                    break
                c.kutu(g, u, u + w - 0.015, od - dp, od - 0.02, z, z + h, m, arka=True)
                if etiket:
                    c.kutu(g, u + 0.015, u + w - 0.03, od - 0.02, od - 0.012, z + h * 0.3, z + h * 0.68, etiket)
                u += w
        elif t == "sise":
            r = rnd.uniform(0.036, 0.046)
            h = min(hmax - 0.03, rnd.uniform(0.24, 0.31))
            sm = M("Sise", rnd.choice(SISE))
            km = M("Kapak", rnd.choice(("#c8342f", "#f4f0e6", "#2f5a9a", "#e0b23a")))
            for _ in range(adet + 1):
                if u + 2 * r > u1 - 0.01:
                    break
                p = c.p(u + r, od - 0.03 - r, 0)
                sise(g, p[0], p[1], z, h, r, sm, km)
                u += 2 * r + 0.012
        elif t == "kavanoz":
            r = 0.05
            h = min(hmax - 0.03, rnd.uniform(0.11, 0.16))
            km = M("Kapak", rnd.choice(("#c8342f", "#e0b23a", "#3f8a4a", "#f4f0e6")))
            for _ in range(adet):
                if u + 2 * r > u1 - 0.01:
                    break
                p = c.p(u + r, od - 0.03 - r, 0)
                dsil(g, p[0], p[1], z, z + h, r, 6, m, kapak=(False, True), kapak_mat=km)
                u += 2 * r + 0.012
        elif t == "konserve":
            r = 0.045
            kat = 2 if hmax > 0.28 else 1
            for _ in range(adet):
                if u + 2 * r > u1 - 0.01:
                    break
                p = c.p(u + r, od - 0.03 - r, 0)
                for k in range(kat):
                    dsil(g, p[0], p[1], z + k * 0.115, z + k * 0.115 + 0.11, r, 6, m, kapak=(False, True), kapak_mat=M("Metal", "#c7c9cc"))
                u += 2 * r + 0.01
        elif t == "paket":
            w = rnd.uniform(0.14, 0.18)
            h = min(hmax - 0.04, rnd.uniform(0.2, 0.27))
            m2 = M("Urun", acik(renk, 0.45))
            for _ in range(adet):
                if u + w > u1 - 0.01:
                    break
                c.kutu(g, u, u + w - 0.02, od - 0.11, od - 0.03, z, z + h, m, arka=True)
                c.kutu(g, u + 0.02, u + w - 0.04, od - 0.03, od - 0.022, z + h * 0.4, z + h * 0.75, m2)
                u += w
        else:  # yatık kutular üst üste
            w = rnd.uniform(0.22, 0.3)
            for _ in range(max(1, adet // 2)):
                if u + w > u1 - 0.01:
                    break
                for k in range(rnd.randint(2, 3)):
                    if (k + 1) * 0.065 > hmax - 0.03:
                        break
                    c.kutu(g, u + 0.005 * k, u + w - 0.02, od - dp, od - 0.02, z + k * 0.065, z + k * 0.065 + 0.058,
                           m if k % 2 == 0 else M("Urun", koyu(renk, 0.85)), arka=True)
                u += w
        g = gs
        u += rnd.uniform(0.02, 0.05)


def raf_yuzu(g, c, R, rnd, kotlar, d0, kademe, h_ust, tur=None, fiyat=True):
    """Tek yüzlü raf: kotlar raf tablası yükseklikleri (zeminden), d0 en alt rafın derinliği, kademe her katta kısalma
    (üst raflar daha sığ: tepeden bakınca her katın ürünü görünür)."""
    for i, k in enumerate(kotlar):
        d = d0 - i * kademe
        z = ZF + k
        c.kutu(g, 0.03, c.L - 0.03, 0.02, d, z - 0.03, z, R["raf"], arka=True)
        if fiyat:
            c.kutu(g, 0.03, c.L - 0.03, d, d + 0.01, z - 0.04, z + 0.012, R["fiyat"])
        hmax = (kotlar[i + 1] - k - 0.03) if i + 1 < len(kotlar) else h_ust
        urun_dizisi(g, c, 0.05, c.L - 0.05, d, z, hmax, rnd, tur=tur, dp=min(0.16, d - 0.06))


def gondol(g, xc, y0, y1, R, rnd, uc_renk):
    """Çift yüzlü orta raf (Y boyunca): kademeli raflar, uç panelleri, tepe bandı."""
    yarim, h = 0.42, 1.42
    kutu(g, xc - yarim, xc + yarim, y0, y1, ZF, ZF + 0.1, R["raf_govde"])
    kutu(g, xc - 0.025, xc + 0.025, y0, y1, ZF + 0.1, ZF + h, R["raf_panel"])
    for ya, yb in ((y0 - 0.03, y0 + 0.01), (y1 - 0.01, y1 + 0.03)):
        kutu(g, xc - yarim, xc + yarim, ya, yb, ZF, ZF + h - 0.25, R["raf_govde"])
        kutu(g, xc - 0.2, xc + 0.2, ya, yb, ZF + h - 0.25, ZF + h, R["raf_govde"])
    kutu(g, xc - 0.07, xc + 0.07, y0 - 0.03, y1 + 0.03, ZF + h, ZF + h + 0.035, M("Raf", uc_renk))
    for n in ((-1, 0), (1, 0)):
        o = (xc + n[0] * 0.025, y1 if n[0] < 0 else y0)
        c = cer(o, n, y1 - y0)
        raf_yuzu(g, c, R, rnd, (0.13, 0.5, 0.84, 1.14), yarim - 0.03, 0.065, 0.26)
    # uçlarda kampanya sepetleri (ön uç)
    kutu(g, xc - 0.3, xc + 0.3, y0 - 0.42, y0 - 0.05, ZF, ZF + 0.5, M("Raf", uc_renk))
    kutu(g, xc - 0.26, xc + 0.26, y0 - 0.38, y0 - 0.09, ZF + 0.5, ZF + 0.515, M("Dolgu", "#4a3f3a"))
    for i in range(3):
        for j in range(2):
            kutu(g, xc - 0.24 + i * 0.165, xc - 0.1 + i * 0.165, y0 - 0.36 + j * 0.14, y0 - 0.24 + j * 0.14, ZF + 0.515,
                 ZF + 0.6 + rnd.uniform(0, 0.08), M("Urun", rnd.choice(URUN)))
    kutu(g, xc - 0.2, xc + 0.2, y0 - 0.43, y0 - 0.42, ZF + 0.18, ZF + 0.42, R["fiyat"], atla=())


def sogutucu(g, c, u0, w, R, rnd, renk, ic_tur=0):
    """Cam kapaklı dik içecek soğutucusu (kapak camı çizilmez: yalnızca çerçeve, içi görünsün). İç ışığı Isik_."""
    d, h = 0.68, 2.05
    by = R["beyaz"]
    z = ZF
    c.kutu(g, u0, u0 + w, 0, 0.05, z, z + h, by, arka=True)
    c.kutu(g, u0, u0 + 0.05, 0.05, d, z, z + h, by, arka=True)
    c.kutu(g, u0 + w - 0.05, u0 + w, 0.05, d, z, z + h, by, arka=True)
    c.kutu(g, u0 + 0.05, u0 + w - 0.05, 0.05, d, z, z + 0.24, by, arka=True)
    c.kutu(g, u0 + 0.12, u0 + w - 0.12, d, d + 0.012, z + 0.06, z + 0.17, R["metal_koyu"])
    c.kutu(g, u0 + 0.05, u0 + w - 0.05, 0.05, d + 0.02, z + h - 0.3, z + h, M("Sogutucu", renk), arka=True)
    c.kutu(g, u0 + 0.15, u0 + w - 0.15, d + 0.02, d + 0.03, z + h - 0.23, z + h - 0.08, R["isik_soguk"])
    c.kutu(g, u0 + 0.05, u0 + w - 0.05, 0.05, 0.065, z + 0.24, z + h - 0.3, R["isik_soguk"])
    c.kutu(g, u0 + 0.07, u0 + w - 0.07, 0.1, d - 0.1, z + h, z + h + 0.03, R["metal_koyu"], arka=True)     # tepe ızgarası
    kotlar = (0.24, 0.6, 0.96, 1.32)
    setler = [(SISE[0], "#c8342f"), (SISE[2], "#2f5a9a"), (SISE[3], "#f4f0e6"), (SISE[1], "#e0b23a"), (SISE[4], "#f4f0e6"), (SISE[5], "#3f8a4a")]
    for i, k in enumerate(kotlar):
        zz = z + k
        if i:
            c.kutu(g, u0 + 0.05, u0 + w - 0.05, 0.065, d - 0.06, zz - 0.02, zz, R["metal"], arka=True)
        sr, kr = setler[(i + ic_tur) % len(setler)]
        sm, km = M("Sise", sr), M("Kapak", kr)
        n = int((w - 0.16) / 0.105)
        gs = g
        for j in range(n):
            if j % 2 == 0:
                g = urun_g(gs, rnd)      # ikişer ikişer aynı stok seviyesinde
            uu = u0 + 0.08 + (w - 0.16) * (j + 0.5) / n
            if (i + ic_tur) % 3 == 2:            # kutu içecekler (iki kat)
                p = c.p(uu, d - 0.12, 0)
                for q in range(2):
                    dsil(g, p[0], p[1], zz + q * 0.125, zz + q * 0.125 + 0.12, 0.036, 6, M("Urun", URUN[(j + q + ic_tur) % 6]), kapak=(False, True), kapak_mat=R["metal"])
            else:
                p = c.p(uu, d - 0.12, 0)
                sise(g, p[0], p[1], zz, 0.3, 0.04, sm, km)
        g = gs
    # kapı çerçevesi ve kulp
    t = 0.035
    c.kutu(g, u0 + 0.05, u0 + 0.05 + t, d - 0.03, d + 0.01, z + 0.26, z + h - 0.32, R["metal_koyu"], arka=True)
    c.kutu(g, u0 + w - 0.05 - t, u0 + w - 0.05, d - 0.03, d + 0.01, z + 0.26, z + h - 0.32, R["metal_koyu"], arka=True)
    c.kutu(g, u0 + 0.05, u0 + w - 0.05, d - 0.03, d + 0.01, z + 0.24, z + 0.24 + t, R["metal_koyu"], arka=True)
    c.kutu(g, u0 + 0.05, u0 + w - 0.05, d - 0.03, d + 0.01, z + h - 0.3 - t, z + h - 0.3, R["metal_koyu"], arka=True)
    c.kutu(g, u0 + w - 0.16, u0 + w - 0.13, d + 0.01, d + 0.05, z + 0.9, z + 1.35, R["metal"], arka=True)


def insan(g, x, y, z, aci, ust, alt, sac, R, onluk=None, sepet=False, ten="#e8b890"):
    """Stilize insan: bacaklar, gövde, baş, saç, kollar. aci: baktığı yön (radyan, +X'ten)."""
    ca, sa = math.cos(aci), math.sin(aci)
    um, am, tm, sm = M("Giysi", ust), M("Giysi", alt), M("Ten", ten), M("Sac", sac)

    def P(ileri, yan, zz):
        return (x + ca * ileri - sa * yan, y + sa * ileri + ca * yan, z + zz)

    def kb(ileri, yan, z0, z1, w, d, mat):
        c = P(ileri, yan, (z0 + z1) / 2)
        obox(g, c, (ca * d / 2, sa * d / 2, 0), (-sa * w / 2, ca * w / 2, 0), (0, 0, (z1 - z0) / 2), mat)
    for s in (-1, 1):
        kb(0, s * 0.09, 0.0, 0.82, 0.14, 0.17, am)
        kb(0.03, s * 0.09, 0.0, 0.07, 0.15, 0.26, M("Giysi", "#3a2a22"))
    kb(0, 0, 0.8, 1.42, 0.42, 0.24, um)
    if onluk:
        kb(0.125, 0, 0.55, 1.3, 0.34, 0.02, M("Giysi", onluk))
    kb(0, 0, 1.42, 1.48, 0.12, 0.12, tm)
    top(g, P(0, 0, 1.61), 0.135, 0.135, 0.15, tm)
    top(g, P(-0.03, 0, 1.67), 0.145, 0.15, 0.12, sm)
    # kollar
    for s in (-1, 1):
        om = P(0, s * 0.25, 1.36)
        if sepet and s > 0:
            el = P(0.08, s * 0.3, 0.84)
        else:
            el = P(0.3 if onluk else 0.1, s * 0.22, 1.0 if onluk else 0.82)
        cubuk(g, om, el, 0.1, um)
        top(g, el, 0.055, 0.055, 0.055, tm)
    if sepet:
        el = P(0.08, 0.3, 0.84)
        market_sepeti(g, el[0] + ca * 0.02, el[1] + sa * 0.02, z + 0.42, aci, R, dolu=True, rnd=random.Random(5))
        for s in (-1, 1):
            a = P(0.08 + 0.0, 0.3 + 0.0, 0.84)
            b = (el[0] + ca * 0.02 - sa * 0 + ca * s * 0.0, el[1], z + 0.66)
            cubuk(g, a, (b[0] + (-sa) * 0.0 + ca * 0.16 * s, b[1] + sa * 0.16 * s, b[2]), 0.02, R["demir"])


def market_sepeti(g, x, y, z, aci, R, dolu=False, rnd=None, renk="#c8342f"):
    """Plastik alışveriş sepeti."""
    m = M("Plastik", renk)
    dkutu(g, x, y, z, z + 0.22, 0.42, 0.3, aci, m)
    dkutu(g, x, y, z + 0.215, z + 0.228, 0.37, 0.25, aci, M("Plastik", koyu(renk, 0.6)))
    if dolu and rnd:
        ca, sa = math.cos(aci), math.sin(aci)
        for i in range(4):
            lx, ly = (i % 2 - 0.5) * 0.17, (i // 2 - 0.5) * 0.11
            dkutu(g, x + lx * ca - ly * sa, y + lx * sa + ly * ca, z + 0.2, z + 0.27 + rnd.uniform(0, 0.08), 0.13, 0.09, aci, M("Urun", rnd.choice(URUN)))


def ic_kur(g, R, rnd):
    global URUN_HEDEF
    # ---- karo zemin: açık renk taban + dama deseni + duvar dibinde koyu bordür
    ic_taban = [(X0 + 0.02, Y0 + 0.02, ZF - 0.03), (X1 - PAH - 0.01, Y0 + 0.02, ZF - 0.03), (X1 - 0.02, Y0 + PAH + 0.01, ZF - 0.03),
                (X1 - 0.02, Y1 - 0.02, ZF - 0.03), (X0 + 0.02, Y1 - 0.02, ZF - 0.03)]
    prizma(g, ic_taban, (0, 0, 0.03), R["zemin"], kapak=R["zemin"])
    k = 0.6
    nx, ny = int(math.ceil((IX1 - IX0) / k)), int(math.ceil((IY1 - IY0) / k))
    z = ZF + 0.012
    for i in range(nx):
        for j in range(ny):
            if (i + j) % 2:
                continue
            xa, xb = IX0 + i * k, min(IX1, IX0 + (i + 1) * k)
            ya, yb = IY0 + j * k, min(IY1, IY0 + (j + 1) * k)
            if xb - ya > PAH_IC - 0.02:           # pahın dışına taşan karolar
                if xa - yb > PAH_IC - 0.02:
                    continue
                # köşesi kesilen karo: çokgeni pah çizgisine göre kırp
                pts = _kirp([(xa, ya), (xb, ya), (xb, yb), (xa, yb)], PAH_IC - 0.02)
                if len(pts) >= 3:
                    g.yuz([(p[0], p[1], z) for p in pts], R["karo"])
                continue
            g.yuz([(xa, ya, z), (xb, ya, z), (xb, yb, z), (xa, yb, z)], R["karo"])
    g.sid += 1
    # bordür (duvar dibi şeridi) ve süpürgelik
    bm = R["karo_kenar"]
    kutu(g, IX0, IX1 - PAH + 0.2, IY0, IY0 + 0.1, ZF, ZF + 0.02, bm)
    kutu(g, IX0, IX1, IY1 - 0.1, IY1, ZF, ZF + 0.02, bm)
    kutu(g, IX0, IX0 + 0.1, IY0, IY1, ZF, ZF + 0.02, bm)
    kutu(g, IX1 - 0.1, IX1, IY0 + PAH - 0.2, IY1, ZF, ZF + 0.02, bm)
    # iç duvarlarda alt renk kuşağı (lambri): arka ve sağ duvarın boş yerleri
    lam = R["ic_alt"]
    kutu(g, IX1 - 0.02, IX1, Y0 + PAH + 0.1, IY1, ZF, ZF + 0.95, lam, atla=("zn", "xp"))
    kutu(g, IX1 - 0.03, IX1, Y0 + PAH + 0.1, IY1, ZF + 0.95, ZF + 1.0, R["trim"], atla=("zn", "xp"))

    # ---- tavan kirişleri ve sarkıt lambalar (çatı kalkınca görünür)
    for by in (-1.95, 0.55, 3.05):
        kutu(g, IX0, IX1, by - 0.07, by + 0.07, ZT - 0.2, ZT - 0.02, R["ahsap_koyu"], atla=())
        for lx in (-3.85, -1.85, 0.2, 2.3):
            if by < -1.5 and lx > 2.0:
                continue
            cubuk(g, (lx, by, ZT - 0.2), (lx, by, ZT - 0.72), 0.02, R["demir"])
            dsil(g, lx, by, ZT - 0.95, ZT - 0.74, 0.24, 8, M("Emaye", "#2f6b57"), r1=0.06, kapak=(False, True))
            dsil(g, lx, by, ZT - 0.97, ZT - 0.95, 0.245, 8, R["trim"], kapak=(True, False))
            meyve(ek("Ampul"), lx, by, ZT - 0.99 + 0.0, 0.08, R["isik"], n=6, rz=-0.1)

    # =================================================== SOL DUVAR: yüksek raf + şarküteri
    ya, yb = IY0 + 0.25, 1.15
    c = cer((IX0, ya), (1, 0), yb - ya)
    c.kutu(g, 0, c.L, 0, 0.03, ZF, ZF + 1.98, R["raf_panel"], arka=True)
    for uu in (0.0, c.L / 2 - 0.02, c.L - 0.04):
        c.kutu(g, uu, uu + 0.04, 0.03, 0.46 if uu in (0.0, c.L - 0.04) else 0.3, ZF, ZF + 1.98, R["raf_govde"], arka=True)
    c.kutu(g, 0, c.L, 0.03, 0.46, ZF, ZF + 0.1, R["raf_govde"], arka=True)
    c.kutu(g, 0, c.L, 0, 0.2, ZF + 1.98, ZF + 2.03, R["raf_govde"], arka=True)
    URUN_HEDEF = [g] + [ek("Urun_DR_%d" % k) for k in (1, 2, 3)]
    raf_yuzu(g, c, R, rnd, (0.13, 0.5, 0.86, 1.2, 1.52), 0.44, 0.06, 0.3)
    URUN_HEDEF = None
    # rafın tepesinde koliler
    for i in range(4):
        uu = 0.3 + i * 1.05
        p = c.p(uu, 0.1, 0)
        dkutu(g, p[0] + 0.02, p[1], ZF + 2.03, ZF + 2.03 + (0.22 if i % 2 else 0.3), 0.18, 0.5, 0.0, R["karton"] if i % 2 else R["karton2"])

    # şarküteri dolabı (sol duvarın arka yarısı): eğik camlı vitrin, içinde peynir ve salam
    sa0, sa1 = 1.75, IY1 - 0.15
    c = cer((IX0, sa0), (1, 0), sa1 - sa0)
    dd = 0.85
    c.kutu(g, 0, c.L, 0.25, dd, ZF, ZF + 0.85, R["beyaz"], arka=True)
    c.kutu(g, 0, c.L, 0.25, dd + 0.012, ZF + 0.2, ZF + 0.42, M("Sogutucu", "#c8452f"), arka=True)
    c.kutu(g, 0.04, c.L - 0.04, 0.29, dd - 0.04, ZF + 0.85, ZF + 0.87, R["isik_soguk"], arka=True)      # ışıklı tabla
    for uu in (0.0, c.L - 0.04):                                                                       # yan camlar (çerçeve)
        c.kutu(g, uu, uu + 0.04, 0.25, dd, ZF + 0.85, ZF + 1.3, R["metal"], arka=True)
    c.kutu(g, 0, c.L, 0.25, 0.3, ZF + 0.85, ZF + 1.3, R["beyaz"], arka=True)
    c.kutu(g, 0, c.L, 0.25, 0.62, ZF + 1.3, ZF + 1.33, R["metal"], arka=True)                           # üst tabla (servis)
    cubuk(g, c.p(0.02, 0.62, ZF + 1.32), c.p(0.02, dd, ZF + 0.9), 0.03, R["metal"])
    cubuk(g, c.p(c.L - 0.02, 0.62, ZF + 1.32), c.p(c.L - 0.02, dd, ZF + 0.9), 0.03, R["metal"])
    cubuk(g, c.p(0.02, dd - 0.01, ZF + 0.89), c.p(c.L - 0.02, dd - 0.01, ZF + 0.89), 0.03, R["metal"])
    # tepsiler: peynir tekerleri, kalıplar, salamlar, sosisler, zeytin kapları
    for i in range(5):
        uu = 0.12 + i * (c.L - 0.24) / 5
        w = (c.L - 0.24) / 5 - 0.05
        c.kutu(g, uu, uu + w, 0.33, dd - 0.08, ZF + 0.87, ZF + 0.885, R["metal"], arka=True)
        p = c.p(uu + w / 2, 0.55, 0)
        if i == 0:
            dsil(g, p[0], p[1] , ZF + 0.885, ZF + 0.985, 0.15, 8, M("Sarkuteri", "#f2d04a"), kapak=(False, True), kapak_mat=M("Sarkuteri", "#f6e08a"))
            dsil(g, p[0] + 0.15, p[1] + 0.02, ZF + 0.885, ZF + 0.96, 0.09, 8, M("Sarkuteri", "#e8a23a"), kapak=(False, True))
        elif i == 1:
            for q in range(3):
                silindir(g, (p[0] - 0.14, p[1] - 0.12 + q * 0.12, ZF + 0.93), (p[0] + 0.2, p[1] - 0.12 + q * 0.12, ZF + 0.93), 0.045, 0.045, 6,
                         M("Sarkuteri", ("#a8342f", "#8a2f2a", "#b84a3a")[q]), kapak_mat=M("Sarkuteri", "#e8a09a"))
        elif i == 2:
            for q in range(2):
                for w2 in range(2):
                    kutu(g, p[0] - 0.14 + q * 0.2, p[0] + 0.02 + q * 0.2, p[1] - 0.14 + w2 * 0.16, p[1] - 0.02 + w2 * 0.16, ZF + 0.885, ZF + 0.96,
                         M("Sarkuteri", ("#f6e9b8", "#f2d04a", "#f4f0e6", "#e8c46a")[q * 2 + w2]))
        elif i == 3:
            for q in range(5):
                silindir(g, (p[0] - 0.12, p[1] - 0.16 + q * 0.08, ZF + 0.91), (p[0] + 0.2, p[1] - 0.16 + q * 0.08, ZF + 0.91), 0.025, 0.025, 5, M("Sarkuteri", "#c8553a"))
        else:
            for q in range(2):
                for w2 in range(2):
                    dsil(g, p[0] - 0.06 + q * 0.17, p[1] - 0.08 + w2 * 0.17, ZF + 0.885, ZF + 0.95, 0.07, 6, R["beyaz"], kapak=(False, True),
                         kapak_mat=M("Sarkuteri", ("#3f5a2f", "#2b2d31", "#c8342f", "#6a8a3a")[q * 2 + w2]))
    # servis tablasının üstünde terazi ve kâğıt rulosu
    p = c.p(0.5, 0.44, ZF + 1.33)
    kutu(g, p[0] - 0.13, p[0] + 0.13, p[1] - 0.16, p[1] + 0.16, ZF + 1.33, ZF + 1.4, R["metal_koyu"])
    kutu(g, p[0] - 0.12, p[0] + 0.12, p[1] - 0.15, p[1] + 0.15, ZF + 1.4, ZF + 1.415, R["metal"], atla=())
    kutu(g, p[0] - 0.14, p[0] - 0.1, p[1] - 0.1, p[1] + 0.1, ZF + 1.4, ZF + 1.62, R["metal_koyu"])
    p = c.p(1.6, 0.44, ZF + 1.33)
    silindir(g, (p[0], p[1] - 0.14, ZF + 1.4), (p[0], p[1] + 0.14, ZF + 1.4), 0.07, 0.07, 8, R["kagit"])
    # şarküterinin arkasındaki duvar rafı: kavanozlar, asılı sucuklar
    c2 = cer((IX0, sa0), (1, 0), sa1 - sa0)
    for zz in (ZF + 1.55, ZF + 1.95):
        c2.kutu(g, 0.05, c2.L - 0.05, 0, 0.22, zz - 0.03, zz, R["ahsap"], arka=True)
        urun_dizisi(g, c2, 0.08, c2.L - 0.08, 0.22, zz, 0.34, rnd, tur="kavanoz" if zz < ZF + 1.8 else "konserve", dp=0.14)
    cubuk(g, c2.p(0.1, 0.2, ZF + 2.5), c2.p(c2.L - 0.1, 0.2, ZF + 2.5), 0.03, R["demir"])
    for i in range(7):
        p = c2.p(0.25 + i * 0.32, 0.2, 0)
        dsil(g, p[0], p[1], ZF + 2.2 - (i % 2) * 0.08, ZF + 2.48, 0.035, 5, M("Sarkuteri", ("#8a2f2a", "#a8342f")[i % 2]), kapak=(True, False))

    # =================================================== ARKA DUVAR: dondurma dolabı + üç soğutucu
    cb = cer((IX0 + 1.0, IY1), (0, -1), 1.7)
    dondurma_dolabi(g, cb, 0.0, 1.7, R, rnd)
    cs = cer((-1.95, IY1), (0, -1), 4.2)
    for i, renk in enumerate(("#c8342f", "#2f5a9a", "#3f8a4a")):
        URUN_HEDEF = [g] + [ek("Urun_SG_%d" % k) for k in (1, 2, 3)]
        sogutucu(g, cs, 0.02 + i * 1.32, 1.28, R, rnd, renk, ic_tur=i * 2)
        URUN_HEDEF = None
        # tepesinde koliler
        p = cs.p(0.66 + i * 1.32, 0.34, 0)
        dkutu(g, p[0] - 0.2, p[1], ZF + 2.08, ZF + 2.38, 0.5, 0.4, 0.1 * (i - 1), R["karton"])
        dkutu(g, p[0] + 0.32, p[1] + 0.03, ZF + 2.08, ZF + 2.3, 0.4, 0.34, -0.12, R["karton2"])
    # soğutucuların üstünde duvar panoları (kampanya afişleri) ve saat
    ca_ = cer((IX1, IY1), (0, -1), IX1 - IX0)          # arka duvarın iç yüzü: u sağdan sola
    ca_ = cer((IX0, IY1), (0, -1), IX1 - IX0)
    for i, (r1, r2) in enumerate((("#f2d04a", "#c8342f"), ("#7ab8d6", "#f4f0e6"), ("#e8527a", "#fff2c8"))):
        ua = 3.25 + i * 1.32
        ca_.kutu(g, ua, ua + 1.1, 0, 0.03, ZF + 2.42, ZF + 2.98, R["ahsap_koyu"])
        ca_.kutu(g, ua + 0.05, ua + 1.05, 0.03, 0.04, ZF + 2.47, ZF + 2.93, M("Pano", r1))
        ca_.kutu(g, ua + 0.12, ua + 0.98, 0.04, 0.048, ZF + 2.76, ZF + 2.86, M("Pano", r2))
        ca_.kutu(g, ua + 0.12, ua + 0.6, 0.04, 0.048, ZF + 2.56, ZF + 2.66, M("Pano", r2))
    # depo kapısının üstünde saat ve "çıkış" levhası
    dxm = X1 - (DEPO[0] + DEPO[1]) / 2
    silindir(g, (dxm, IY1, ZF + 2.62), (dxm, IY1 - 0.04, ZF + 2.62), 0.2, 0.2, 10, R["demir"], kapak_mat=R["kagit"])
    kutu(g, dxm - 0.012, dxm + 0.012, IY1 - 0.05, IY1 - 0.04, ZF + 2.62, ZF + 2.76, R["demir"], atla=())
    kutu(g, dxm, dxm + 0.1, IY1 - 0.05, IY1 - 0.04, ZF + 2.608, ZF + 2.632, R["demir"], atla=())
    # depo kapısının yanında koliler, süpürge
    for i, (dx, dy, w, d, h) in enumerate(((0.5, -0.36, 0.5, 0.42, 0.4), (0.5, -0.38, 0.42, 0.36, 0.3))):
        zz = ZF if i < 1 else ZF + 0.4
        dkutu(g, IX1 - dx, IY1 + dy, zz, zz + h, w, d, 0.08 * i, R["karton"] if i != 1 else R["karton2"])
        dkutu(g, IX1 - dx, IY1 + dy, zz + h, zz + h + 0.004, w * 0.16, d, 0.08 * i, M("Bant", "#e8dcc0"))
    # iç paspas (depo kapısı)
    kutu(g, dxm - 0.5, dxm + 0.5, IY1 - 0.75, IY1 - 0.15, ZF + 0.02, ZF + 0.035, M("Paspas", "#5d6b6b"))

    # =================================================== ORTA: üç gondol
    for i, xc in enumerate((-2.9, -0.95, 1.0)):
        URUN_HEDEF = [g] + [ek("Urun_G%d_%d" % (i + 1, k)) for k in (1, 2, 3)]
        gondol(g, xc, -1.35, 2.25, R, rnd, ("#c8452f", "#2f6b57", "#e0b23a")[i])
        URUN_HEDEF = None

    # =================================================== ÖN: vitrin içi teşhir setleri
    for v, tur in ((V1, "ekmek"), (V2, "meyve")):
        xa, xb = X0 + v[0] + 0.05, X0 + v[1] - 0.05
        kutu(g, xa, xb, IY0, IY0 + 0.62, ZF, ZF + 0.56, R["ahsap"])
        kutu(g, xa - 0.02, xb + 0.02, IY0, IY0 + 0.65, ZF + 0.56, ZF + 0.6, R["ahsap_acik"], atla=())
        if tur == "ekmek":
            for i in range(4):
                cx = xa + 0.38 + i * 0.72
                dsil(g, cx, IY0 + 0.33, ZF + 0.6, ZF + 0.78, 0.22, 8, M("Hasir", "#c49a6c"), r1=0.28, kapak=(False, True), kapak_mat=M("Dolgu", "#8a6a42"))
                for q in range(3):
                    a = q * 2.1 + i
                    top(g, (cx + math.cos(a) * 0.1, IY0 + 0.33 + math.sin(a) * 0.1, ZF + 0.83), 0.085, 0.16 if i % 2 else 0.1, 0.075,
                        M("Ekmek", ("#c98a4a", "#d69a5a", "#b8773a")[(q + i) % 3]))
        else:
            mv = [("#d8402f", "#e05a3a"), ("#f08a2a", "#f29a3a"), ("#8ab84a", "#7aa83f"), ("#f2d04a", "#ead24a")]
            for i in range(4):
                cx = xa + 0.38 + i * 0.72
                ust = kasa(g, cx, IY0 + 0.33, ZF + 0.6, 0.62, 0.44, 0.16, 0, R["ahsap_acik"], M("Dolgu", koyu(mv[i][0], 0.86)))
                meyve_yigini(g, cx - 0.26, cx + 0.26, IY0 + 0.15, IY0 + 0.51, ust - 0.01, 0.062, mv[i], rnd)
    # ön duvarın iki vitrin arasındaki ayağında mantar pano
    cf = cer((X0 + V2[0] - 0.05, IY0), (0, 1), 0.5)
    cf.kutu(g, 0, 0.5, 0, 0.025, ZF + 1.2, ZF + 1.95, R["ahsap_koyu"])
    cf.kutu(g, 0.04, 0.46, 0.025, 0.035, ZF + 1.24, ZF + 1.91, R["karton"])
    for i in range(4):
        cf.kutu(g, 0.07 + (i % 2) * 0.2, 0.22 + (i % 2) * 0.2, 0.035, 0.042, ZF + 1.3 + (i // 2) * 0.3, ZF + 1.52 + (i // 2) * 0.3,
                M("Pano", ("#f2d04a", "#f4f0e6", "#7ab8d6", "#f06a8a")[i]))

    # =================================================== SAĞ: kasa tezgâhı
    tx0, tx1 = 2.75, 3.4                 # tezgâhın ana kolu (Y boyunca)
    ty0, ty1 = 0.35, 2.85
    th = 0.95
    tg, tu = R["ahsap_boya"], R["ahsap_acik"]
    kutu(g, tx0, tx1, ty0, ty1, ZF, ZF + th - 0.04, tg)
    kutu(g, tx0 - 0.04, tx1 + 0.03, ty0 - 0.04, ty1 + 0.04, ZF + th - 0.04, ZF + th, tu, atla=())
    kutu(g, tx1, IX1 - 0.5, ty0, ty0 + 0.55, ZF, ZF + th - 0.04, tg)                        # kısa kol (kapıya bakan)
    kutu(g, tx1 + 0.03, IX1 - 0.46, ty0 - 0.04, ty0 + 0.59, ZF + th - 0.04, ZF + th, tu, atla=())
    for i in range(4):                                                                     # müşteri yüzünde tablalar
        ya_ = ty0 + 0.1 + i * 0.6
        kutu(g, tx0 - 0.015, tx0, ya_, ya_ + 0.5, ZF + 0.5, ZF + 0.82, R["ahsap_boya2"], atla=())
    # tezgâh önü çikolata / sakız rafı (müşteri tarafı, alçak)
    c = cer((tx0, ty1 - 0.05), (-1, 0), ty1 - ty0 - 0.1)
    for i, zz in enumerate((0.1, 0.3)):
        dd = 0.22 - i * 0.07
        c.kutu(g, 0.05, c.L - 0.05, 0, dd, ZF + zz - 0.02, ZF + zz, R["raf"], arka=True)
        c.kutu(g, 0.05, c.L - 0.05, dd, dd + 0.01, ZF + zz - 0.02, ZF + zz + 0.03, R["fiyat"])
        u = 0.08
        while u < c.L - 0.2:
            w = rnd.uniform(0.14, 0.2)
            c.kutu(g, u, u + w - 0.02, dd - 0.12, dd - 0.01, ZF + zz, ZF + zz + rnd.uniform(0.05, 0.1), M("Urun", rnd.choice(URUN)), arka=True)
            u += w
    # tezgâhın üstü: yazar kasa, terazi, sakız / şeker standı, kavanozlar, kart cihazı, gazete yığını
    zt = ZF + th
    kx, ky = 3.02, 1.95                                                                   # yazar kasa
    kutu(g, kx - 0.2, kx + 0.2, ky - 0.2, ky + 0.2, zt, zt + 0.12, M("Kasa", "#3a3d42"))
    obox(g, (kx + 0.04, ky, zt + 0.17), (0.14, 0, 0.05), (0, 0.18, 0), (-0.02, 0, 0.05), M("Kasa", "#4a4d52"))
    for i in range(3):
        for j in range(4):
            kutu(g, kx - 0.02 + i * 0.065, kx + 0.03 + i * 0.065, ky - 0.13 + j * 0.07, ky - 0.08 + j * 0.07, zt + 0.215 + i * 0.022 - 0.01, zt + 0.235 + i * 0.022,
                 M("Kasa", "#f4f0e6") if (i + j) % 5 else M("Kasa", "#c8342f"))
    kutu(g, kx - 0.19, kx - 0.11, ky - 0.15, ky + 0.15, zt + 0.12, zt + 0.3, M("Kasa", "#2b2d31"))            # müşteri ekranı
    kutu(g, kx - 0.195, kx - 0.19, ky - 0.11, ky + 0.11, zt + 0.17, zt + 0.27, M("Isik", "#8fe0a0"), atla=())
    tx, ty = 3.05, 1.25                                                                    # terazi
    kutu(g, tx - 0.17, tx + 0.17, ty - 0.2, ty + 0.2, zt, zt + 0.09, R["beyaz"])
    kutu(g, tx - 0.15, tx + 0.15, ty - 0.18, ty + 0.18, zt + 0.1, zt + 0.115, R["metal"], atla=())
    cubuk(g, (tx + 0.13, ty, zt + 0.09), (tx + 0.13, ty, zt + 0.42), 0.035, R["beyaz"])
    kutu(g, tx + 0.09, tx + 0.17, ty - 0.11, ty + 0.11, zt + 0.42, zt + 0.56, R["beyaz"], atla=())
    kutu(g, tx + 0.085, tx + 0.09, ty - 0.08, ty + 0.08, zt + 0.45, zt + 0.53, M("Isik", "#8fe0a0"), atla=())
    for q, (dx, dy) in enumerate(((-0.04, -0.05), (0.03, 0.06), (-0.06, 0.07))):
        meyve(g, tx + dx - 0.02, ty + dy, zt + 0.115, 0.06, M("Meyve", ("#d8402f", "#e05a3a", "#8ab84a")[q]))
    sx, sy = 3.07, 0.62                                                                    # sakız / şeker standı (üç kademe)
    for i in range(3):
        kutu(g, sx - 0.22 + i * 0.07, sx + 0.2, sy - 0.2, sy + 0.2, zt + i * 0.09, zt + (i + 1) * 0.09, M("Raf", "#c8452f"))
        for j in range(5):
            kutu(g, sx - 0.21 + i * 0.07, sx - 0.155 + i * 0.07, sy - 0.19 + j * 0.078, sy - 0.125 + j * 0.078, zt + (i + 1) * 0.09, zt + (i + 1) * 0.09 + 0.045,
                 M("Urun", URUN[(i * 3 + j * 2) % len(URUN)]))
    for i, r in enumerate(("#e07a9a", "#f2d04a", "#7ab8d6")):                               # şeker kavanozları
        px, py = 3.1, 2.42 + i * 0.16
        dsil(g, px, py, zt, zt + 0.17, 0.06, 6, M("Kristal", acik(r, 0.3)), kapak=(False, True), kapak_mat=M("Kapak", "#c8342f"))
    kutu(g, 2.86, 2.95, 2.3, 2.45, zt, zt + 0.035, M("Kasa", "#2b2d31"))                    # kart cihazı
    kutu(g, 2.865, 2.945, 2.38, 2.44, zt + 0.035, zt + 0.04, M("Isik", "#8fe0a0"), atla=())
    for k in range(4):                                                                     # gazete yığını (kısa kolun üstü)
        kutu(g, 3.6, 3.95, ty0 + 0.08 + k * 0.004, ty0 + 0.48 + k * 0.004, zt + k * 0.012, zt + (k + 1) * 0.012, R["kagit"] if k % 2 else R["kagit2"], atla=())
    kutu(g, 3.62, 3.93, ty0 + 0.36, ty0 + 0.45, zt + 0.048, zt + 0.051, M("Afis", "#2b2d31"), atla=())
    # piyango / çakmak standı
    kutu(g, 3.5, 3.56, ty0 + 0.05, ty0 + 0.5, zt, zt + 0.32, M("Raf", "#e0b23a"))
    for j in range(4):
        kutu(g, 3.56, 3.575, ty0 + 0.07 + j * 0.105, ty0 + 0.16 + j * 0.105, zt + 0.05, zt + 0.28, M("Urun", URUN[(j * 3 + 1) % len(URUN)]), atla=())

    # sigara / değerli ürün rafı (kasiyerin arkasında, sağ duvarda)
    c = cer((IX1, 2.95), (-1, 0), 2.0)
    c.kutu(g, 0, c.L, 0, 0.03, ZF, ZF + 2.15, R["ahsap_koyu"], arka=True)
    c.kutu(g, 0, c.L, 0.03, 0.4, ZF, ZF + 0.85, R["ahsap_boya"], arka=True)
    c.kutu(g, -0.02, c.L + 0.02, 0, 0.44, ZF + 0.85, ZF + 0.89, R["ahsap_acik"], arka=True)
    for uu in (0.0, c.L - 0.04):
        c.kutu(g, uu, uu + 0.04, 0.03, 0.26, ZF + 0.89, ZF + 2.15, R["ahsap_koyu"], arka=True)
    c.kutu(g, 0, c.L, 0, 0.3, ZF + 2.15, ZF + 2.2, R["ahsap_koyu"], arka=True)
    for i, zz in enumerate((1.05, 1.3, 1.55, 1.8)):
        dd = 0.24 - i * 0.03
        c.kutu(g, 0.04, c.L - 0.04, 0.03, dd, ZF + zz - 0.02, ZF + zz, R["ahsap"], arka=True)
        u = 0.07
        while u < c.L - 0.14:                                                              # sigara paketleri: küçük, sıkı dizili
            w = 0.085
            mm = M("Urun", rnd.choice(("#c8342f", "#f4f0e6", "#2f5a9a", "#e0b23a", "#2b2d31", "#3f8a4a")))
            for _ in range(rnd.randint(2, 4)):
                if u > c.L - 0.14:
                    break
                c.kutu(g, u, u + w - 0.012, dd - 0.07, dd - 0.01, ZF + zz, ZF + zz + 0.13, mm, arka=True)
                u += w
            u += 0.02
    # alt tezgâhta: içki şişeleri ve radyo
    for i in range(6):
        p = c.p(0.2 + i * 0.13, 0.3, 0)
        sise(g, p[0], p[1], ZF + 0.89, 0.3, 0.04, M("Sise", SISE[(i * 2) % len(SISE)]), M("Kapak", "#e0b23a"))
    p = c.p(1.5, 0.26, 0)
    kutu(g, p[0] - 0.1, p[0] + 0.1, p[1] - 0.2, p[1] + 0.2, ZF + 0.89, ZF + 1.0, M("Kasa", "#6b452a"))
    cubuk(g, (p[0], p[1] + 0.17, ZF + 1.0), (p[0] + 0.05, p[1] + 0.3, ZF + 1.0 + 0.0), 0.012, R["metal"])

    # (kasiyer ve müşteriler modelde yok: oyunda BakkalCanli hareketli figürleri kurar; önizlemede onizleme_insanlari çizer)
    kutu(g, 3.6, 4.3, 1.35, 2.2, ZF + 0.02, ZF + 0.035, M("Paspas", "#5d6b6b"))              # kasiyerin ayak paspası
    # tabure
    dsil(g, 4.2, 2.55, ZF + 0.55, ZF + 0.6, 0.17, 8, R["ahsap_acik"], kapak=(True, True))
    for a in range(3):
        aa = a * 2.094
        cubuk(g, (4.2 + math.cos(aa) * 0.13, 2.55 + math.sin(aa) * 0.13, ZF), (4.2 + math.cos(aa) * 0.08, 2.55 + math.sin(aa) * 0.08, ZF + 0.55), 0.035, R["ahsap"])

    # =================================================== GİRİŞ: paspas, sepet yığını, su kolileri, sağ pencere önü
    pm = (X1 - PAH / 2 - 0.62, Y0 + PAH / 2 + 0.62)
    dkutu(g, pm[0], pm[1], ZF + 0.02, ZF + 0.04, 1.25, 0.75, math.radians(45), R["paspas"])
    dkutu(g, pm[0], pm[1], ZF + 0.04, ZF + 0.046, 1.05, 0.55, math.radians(45), R["paspas2"])
    for k in range(5):                                                                     # iç içe sepetler
        market_sepeti(g, 2.35, -2.75, ZF + k * 0.07, 0.1, R, renk="#c8342f" if k % 2 == 0 else "#2f5a9a")
    cubuk(g, (2.2, -2.75, ZF + 0.5), (2.5, -2.75, ZF + 0.5), 0.02, R["demir"])
    # sağ pencerenin önünde alçak sehpa: çiçekler ve dergiler
    cw = cer((IX1, Y0 + PAH + SAG_P[1] + 0.1), (-1, 0), SAG_P[1] - SAG_P[0] + 0.2)
    cw.kutu(g, 0, cw.L, 0, 0.4, ZF, ZF + 0.6, R["ahsap"], arka=True)
    cw.kutu(g, -0.02, cw.L + 0.02, 0, 0.43, ZF + 0.6, ZF + 0.64, R["ahsap_acik"], arka=True)
    for i in range(3):
        p = cw.p(0.3 + i * 0.5, 0.2, 0)
        dsil(g, p[0], p[1], ZF + 0.64, ZF + 0.84, 0.1, 6, M("Plastik", "#3a3d42"), r1=0.13, kapak=(False, True), kapak_mat=M("Cali", "#4f8c4a"))
        for q in range(4):
            meyve(g, p[0] + math.cos(q * 1.6 + i) * 0.07, p[1] + math.sin(q * 1.6 + i) * 0.07, ZF + 0.86, 0.06,
                  M("Cicek", ("#e0525a", "#f2d04a", "#ffffff", "#f06a8a", "#b07ad8")[(q + i * 2) % 5]), n=5)
    # su kolileri (girişte, ortaya doğru): 6'lı paketler üst üste
    for i, (dx, dy) in enumerate(((0, 0), (0.0, 0.34))):
        for k in range(3 if i != 1 else 2):
            x, y, zz = -1.2 + dx, -3.17 + dy, ZF + k * 0.27
            kutu(g, x - 0.2, x + 0.2, y - 0.15, y + 0.15, zz, zz + 0.24, M("Kristal", "#bfe0ee"))
            kutu(g, x - 0.205, x + 0.205, y - 0.155, y + 0.155, zz + 0.07, zz + 0.16, M("Urun", "#2f5a9a"), atla=("zn", "zp"))
            if k == (2 if i != 1 else 1):
                for a in range(3):
                    for b in range(2):
                        dsil(g, x - 0.13 + a * 0.13, y - 0.07 + b * 0.14, zz + 0.24, zz + 0.275, 0.025, 5, M("Kapak", "#2f5a9a"))


def ic_ekler(g, R, rnd):
    """İç mekânın küçük zenginlikleri: kartpostal standı, tavan pervanesi, yangın tüpü, ecza dolabı."""
    # dönen kartpostal / dergi standı (girişte)
    sx, sy = 3.9, -0.95
    dsil(g, sx, sy, ZF, ZF + 0.06, 0.22, 8, R["demir"])
    cubuk(g, (sx, sy, ZF + 0.06), (sx, sy, ZF + 1.6), 0.04, R["demir"])
    dsil(g, sx, sy, ZF + 1.6, ZF + 1.68, 0.16, 6, M("Raf", "#c8452f"), r1=0.03, kapak=(True, False))
    for kat in range(4):
        for i in range(6):
            a = 2 * math.pi * (i + 0.5 * (kat % 2)) / 6
            n = (math.cos(a), math.sin(a))
            c = cer((sx + n[0] * 0.13 + n[1] * 0.07, sy + n[1] * 0.13 - n[0] * 0.07), n, 0.14)
            c.kutu(g, 0, 0.14, 0, 0.02, ZF + 0.3 + kat * 0.32, ZF + 0.55 + kat * 0.32, M("Urun", URUN[(i * 5 + kat * 3) % len(URUN)]), arka=True)
    # tavan pervanesi (ince bir ara kirişin altında, iki gondolun arasındaki koridorda)
    fx, fy = -1.92, -0.7
    kutu(g, IX0, IX1, fy - 0.04, fy + 0.04, ZT - 0.14, ZT - 0.02, R["ahsap_koyu"], atla=())
    cubuk(g, (fx, fy, ZT - 0.14), (fx, fy, ZT - 0.42), 0.035, R["demir"])
    dsil(g, fx, fy, ZT - 0.52, ZT - 0.42, 0.1, 8, R["pirinc"], kapak=(True, True))
    dsil(g, fx, fy, ZT - 0.6, ZT - 0.52, 0.07, 8, R["isik"], r1=0.1, kapak=(True, False))
    for i in range(4):
        a = i * math.pi / 2 + 0.5
        dkutu(g, fx + math.cos(a) * 0.2, fy + math.sin(a) * 0.2, ZT - 0.485, ZT - 0.465, 0.22, 0.05, a, R["demir"], alt=True)
        dkutu(g, fx + math.cos(a) * 0.52, fy + math.sin(a) * 0.52, ZT - 0.49, ZT - 0.47, 0.46, 0.15, a, R["ahsap_acik"], alt=True)
    # yangın tüpü ve ecza dolabı (arka duvar, depo kapısının sağı)
    dsil(g, 4.05, IY1 - 0.12, ZF + 0.75, ZF + 1.25, 0.08, 8, M("Plastik", "#c8342f"), kapak=(True, True))
    dsil(g, 4.05, IY1 - 0.12, ZF + 1.25, ZF + 1.33, 0.035, 6, R["demir"], kapak=(False, True))
    kutu(g, 3.95, 4.35, IY1 - 0.12, IY1, ZF + 1.45, ZF + 1.85, R["beyaz"], atla=())
    kutu(g, 4.12, 4.18, IY1 - 0.13, IY1 - 0.12, ZF + 1.53, ZF + 1.77, M("Plastik", "#c8342f"), atla=())
    kutu(g, 4.04, 4.26, IY1 - 0.13, IY1 - 0.12, ZF + 1.62, ZF + 1.68, M("Plastik", "#c8342f"), atla=())


def _kirp(pts, lim):
    """Çokgeni x − y ≤ lim yarı düzlemine kırpar (Sutherland–Hodgman, tek kenar)."""
    out = []
    n = len(pts)
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        da, db = a[0] - a[1] - lim, b[0] - b[1] - lim
        if da <= 0:
            out.append(a)
        if (da < 0 < db) or (db < 0 < da):
            t = da / (da - db)
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    temiz = []
    for q in out:                      # çakışık noktalar (köşesi tam çizgiye gelen karo) yüzü bozmasın
        if not temiz or abs(q[0] - temiz[-1][0]) + abs(q[1] - temiz[-1][1]) > 1e-6:
            temiz.append(q)
    if len(temiz) > 1 and abs(temiz[0][0] - temiz[-1][0]) + abs(temiz[0][1] - temiz[-1][1]) <= 1e-6:
        temiz.pop()
    return temiz


# ================================== CANLI PARÇALAR ==================================

def poset(g, x, y, z, R, w=0.26, d=0.17, h=0.32):
    """Kraft kâğıt sipariş poşeti: aşağı doğru daralan gövde, katlanmış ağız, iki sap, şirket etiketi."""
    kg = M("Karton", "#c49a6c")
    pts = [(x - w * 0.44, y - d * 0.44, z), (x + w * 0.44, y - d * 0.44, z), (x + w * 0.44, y + d * 0.44, z), (x - w * 0.44, y + d * 0.44, z),
           (x - w / 2, y - d / 2, z + h), (x + w / 2, y - d / 2, z + h), (x + w / 2, y + d / 2, z + h), (x - w / 2, y + d / 2, z + h)]
    kati(g, pts, [[4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], kg)
    kutu(g, x - w / 2 - 0.006, x + w / 2 + 0.006, y - d * 0.2, y + d * 0.2, z + h - 0.05, z + h + 0.012, M("Karton", "#a8824f"), atla=())
    for s_ in (-1, 1):
        cubuk(g, (x - 0.06, y + s_ * 0.03, z + h), (x - 0.04, y + s_ * 0.03, z + h + 0.09), 0.014, R["ahsap_koyu"])
        cubuk(g, (x + 0.06, y + s_ * 0.03, z + h), (x + 0.04, y + s_ * 0.03, z + h + 0.09), 0.014, R["ahsap_koyu"])
        cubuk(g, (x - 0.045, y + s_ * 0.03, z + h + 0.088), (x + 0.045, y + s_ * 0.03, z + h + 0.088), 0.014, R["ahsap_koyu"])
    kutu(g, x - 0.07, x + 0.07, y - d / 2 - 0.004, y - d / 2 + 0.002, z + 0.1, z + 0.22, M("Afis", "#2f6b57"), atla=())


def kamyonet(g, x, y, z, R):
    """Tedarik kamyoneti: burnu −X'e bakar, kasası +X'te (arka kapakları depo kapısına yakın). Kök: gövdenin ortası, yerde."""
    by, yk, ys = R["beyaz"], M("Plastik", "#2f6b57"), R["lastik"]
    kutu(g, x - 0.75, x + 1.85, y - 0.82, y + 0.82, z + 0.42, z + 2.05, by)                       # kasa
    kutu(g, x - 0.75, x + 1.85, y - 0.83, y + 0.83, z + 0.95, z + 1.3, yk, atla=("zn", "zp"))     # yeşil kuşak
    kutu(g, x - 1.55, x - 0.75, y - 0.78, y + 0.78, z + 0.42, z + 1.62, by)                       # kabin
    kutu(g, x - 1.95, x - 1.55, y - 0.78, y + 0.78, z + 0.42, z + 1.02, by)                       # motor kaputu
    obox(g, (x - 1.57, y, z + 1.3), (0.015, 0, 0.02), (0, 0.7, 0), (-0.11, 0, 0.27), R["kristal"])   # ön cam (eğik)
    for s_ in (-1, 1):
        kutu(g, x - 1.45, x - 0.9, y + s_ * 0.78, y + s_ * 0.795, z + 1.05, z + 1.5, R["kristal"], atla=())
        kutu(g, x - 1.97, x - 1.95, y + s_ * 0.5 - 0.12, y + s_ * 0.5 + 0.12, z + 0.72, z + 0.86, M("Isik", "#fff2c8"), atla=())
        kutu(g, x + 1.85, x + 1.87, y + s_ * 0.66 - 0.07, y + s_ * 0.66 + 0.07, z + 0.6, z + 0.8, M("Plastik", "#c8342f"), atla=())
        for wx in (-1.3, 1.15):
            silindir(g, (x + wx, y + s_ * 0.62, z + 0.33), (x + wx, y + s_ * 0.84, z + 0.33), 0.33, 0.33, 10, ys, kapak_mat=R["metal"])
    kutu(g, x - 2.0, x - 1.9, y - 0.8, y + 0.8, z + 0.36, z + 0.52, R["metal_koyu"])               # tampon
    kutu(g, x + 1.85, x + 1.9, y - 0.8, y + 0.8, z + 0.36, z + 0.5, R["metal_koyu"])
    # arka kapaklar açık (iki yana) ve kasanın içi koyu
    kutu(g, x + 1.855, x + 1.865, y - 0.74, y + 0.74, z + 0.5, z + 1.98, M("Dolgu", "#3a3d42"), atla=())
    for s_ in (-1, 1):
        obox(g, (x + 2.2, y + s_ * 1.05, z + 1.24), (0.37, s_ * 0.25, 0), (0.012, -s_ * 0.018, 0), (0, 0, 0.74), by)
    yazi(g, cer((x - 0.75, y - 0.83), (0, -1), 2.6), 1.3, z + 1.65, "MARKET", 0.05, 0.0, 0.012, yk)
    yazi(g, cer((x + 1.85, y + 0.83), (0, 1), 2.6), 1.3, z + 1.65, "MARKET", 0.05, 0.0, 0.012, yk)


def koliler(g, x, y, z, R, rnd):
    """Kamyonetten indirilen koli ve kasalar (kök: yığının ortası, yerde)."""
    for i, (dx, dy, k) in enumerate(((-0.24, -0.22, 3), (0.24, -0.2, 2), (-0.22, 0.24, 2), (0.25, 0.25, 3))):
        zz = z
        for j in range(k):
            h = 0.3 + 0.06 * ((i + j) % 2)
            dkutu(g, x + dx, y + dy, zz, zz + h, 0.44, 0.4, 0.07 * (j - 1), R["karton"] if (i + j) % 2 else R["karton2"])
            dkutu(g, x + dx, y + dy, zz + h, zz + h + 0.004, 0.07, 0.4, 0.07 * (j - 1), M("Bant", "#e8dcc0"))
            zz += h
    sise_sandigi(g, x + 0.72, y - 0.05, z, 0.2, "#c8342f", rnd, kapak="#f4f0e6")
    sise_sandigi(g, x + 0.72, y - 0.05, z + 0.27, 0.1, "#2f5a9a", rnd, kapak="#7ab8d6")


def canli_parcalar(R, rnd):
    """Oyunda hareket eden ya da açılıp kapanan parçalar (adlı ek objeler): poşet, kamyonet, koliler ve kozmetik yükseltmeler."""
    global URUN_HEDEF
    poset(ek("Poset", POSET_YER), POSET_YER[0], POSET_YER[1], POSET_YER[2], R)
    kamyonet(ek("Kamyonet", KAMYONET_YER), KAMYONET_YER[0], KAMYONET_YER[1], KAMYONET_YER[2], R)
    koliler(ek("Koliler", KOLI_YER), KOLI_YER[0], KOLI_YER[1], KOLI_YER[2], R, rnd)

    # ---- Kasa2: tezgâhın kapıya bakan ucuna eklenen ikinci kasa
    g = ek("Kasa2")
    x0, x1, y0, y1, zt = 2.75, 3.4, -0.5, 0.31, ZF + 0.95
    kutu(g, x0, x1, y0, y1, ZF, zt - 0.04, R["ahsap_boya"])
    kutu(g, x0 - 0.04, x1 + 0.03, y0 - 0.04, y1, zt - 0.04, zt, R["ahsap_acik"], atla=())
    kutu(g, x0 - 0.015, x0, y0 + 0.1, y1 - 0.1, ZF + 0.5, ZF + 0.82, R["ahsap_boya2"], atla=())
    kx, ky = 3.05, -0.1
    kutu(g, kx - 0.2, kx + 0.2, ky - 0.2, ky + 0.2, zt, zt + 0.12, M("Kasa", "#3a3d42"))
    obox(g, (kx + 0.04, ky, zt + 0.17), (0.14, 0, 0.05), (0, 0.18, 0), (-0.02, 0, 0.05), M("Kasa", "#4a4d52"))
    kutu(g, kx - 0.19, kx - 0.11, ky - 0.15, ky + 0.15, zt + 0.12, zt + 0.3, M("Kasa", "#2b2d31"))
    kutu(g, kx - 0.195, kx - 0.19, ky - 0.11, ky + 0.11, zt + 0.17, zt + 0.27, M("Isik", "#8fe0a0"), atla=())
    kutu(g, 2.86, 2.95, -0.42, -0.3, zt, zt + 0.035, M("Kasa", "#2b2d31"))
    cubuk(g, (3.3, -0.4, zt), (3.3, -0.4, zt + 0.5), 0.03, R["metal"])
    kutu(g, 3.14, 3.34, -0.44, -0.36, zt + 0.5, zt + 0.62, M("Raf", "#e0b23a"), atla=())      # "2" numaralı kasa levhası
    kutu(g, 3.6, 4.3, -0.45, 0.3, ZF + 0.02, ZF + 0.035, M("Paspas", "#5d6b6b"))

    # ---- Sogutucu4: sağ duvarın arka köşesine dördüncü içecek soğutucusu
    g = ek("Sogutucu4")
    URUN_HEDEF = None
    sogutucu(g, cer((IX1, IY1 - 0.02), (-1, 0), 1.32), 0.02, 1.28, R, rnd, "#e0b23a", ic_tur=1)

    # ---- Vitrin2: yenilenmiş vitrin (içeriden ampul dizisi, ışıklı yazı, süslü sepetler)
    g = ek("Vitrin2")
    for v in (V1, V2):
        xa, xb = X0 + v[0] + 0.1, X0 + v[1] - 0.1
        cubuk(g, (xa, IY0 + 0.05, v[3] - 0.12), (xb, IY0 + 0.05, v[3] - 0.12), 0.015, R["demir"])
        n = 11
        for i in range(n):
            x = xa + 0.08 + (xb - xa - 0.16) * i / (n - 1)
            meyve(g, x, IY0 + 0.05, v[3] - 0.13, 0.045, M("Isik", ("#ffe2a0", "#ff9ab0", "#a0e0ff")[i % 3]), n=5, rz=-0.09)
        for i in range(2):                                     # kaideli hediye sepetleri
            cx = xa + 0.75 + i * 1.3
            kutu(g, cx - 0.2, cx + 0.2, IY0 + 0.12, IY0 + 0.52, ZF + 0.6, ZF + 0.95, R["trim"])
            dsil(g, cx, IY0 + 0.32, ZF + 0.95, ZF + 1.12, 0.17, 8, M("Hasir", "#c49a6c"), r1=0.22, kapak=(False, True), kapak_mat=M("Dolgu", "#8a6a42"))
            for q in range(5):
                a = q * 1.26 + i
                meyve(g, cx + math.cos(a) * 0.1, IY0 + 0.32 + math.sin(a) * 0.1, ZF + 1.11, 0.07,
                      M("Meyve", ("#d8402f", "#f08a2a", "#8ab84a", "#f2d04a", "#7a3a6a")[q]))
            kutu(g, cx - 0.23, cx + 0.23, IY0 + 0.3, IY0 + 0.34, ZF + 1.12, ZF + 1.3, M("Afis", "#e8527a"), atla=())
    yazi(g, cer((X0 + V2[1] - 0.5, IY0 + 0.02), (0, 1), 2.0), 1.0, V2[2] + 0.95, "OPEN", 0.04, 0.0, 0.015, M("Isik", "#ff9ab0"))


def onizleme_insanlari(R, durum="dukkan"):
    """Önizleme resimleri için sabit figürler (oyunda bunların yerine BakkalCanli'nin hareketli figürleri var)."""
    g = G("insanlar")
    n = NOKTA
    insan(g, n["KAS"][0], n["KAS"][1], ZF, math.pi, "#f4f0e6", "#3d5a8a", "#4a2f20", R, onluk="#2f6b57")
    insan(g, n["Q0"][0], n["Q0"][1], ZF, 0.0, "#e8a23a", "#4a4d52", "#2b2d31", R, sepet=True)
    insan(g, n["Q1"][0], n["Q1"][1], ZF, math.pi / 2, "#8a4a6b", "#3a3d42", "#6b452a", R, sepet=True)
    insan(g, n["A1b"][0], n["A1b"][1], ZF, math.pi, "#7ab8d6", "#6b452a", "#c9a24a", R, ten="#f0c8a0")
    insan(g, n["B2"][0], n["B2"][1] + 0.1, ZF, math.pi / 2, "#5a9a4a", "#3d5a8a", "#2b2d31", R)
    insan(g, n["A0c"][0], n["A0c"][1], ZF, math.pi, "#f2d04a", "#4a4d52", "#8a5a3a", R, sepet=True)
    insan(g, n["ESIK"][0], n["ESIK"][1] - 0.5, ZL, math.pi / 2, "#d94f3d", "#3a3d42", "#2b2d31", R)
    # kurye: şirket renginde mont, sırtında çanta; poşeti almaya gelmiş
    kx, ky = n["KUR"][0], n["KUR"][1]
    insan(g, kx, ky, ZF, 0.0, "#009ede", "#2b2d31", "#2b2d31", R)
    kutu(g, kx - 0.42, kx - 0.14, ky - 0.2, ky + 0.2, ZF + 0.95, ZF + 1.45, M("Giysi", "#009ede"))
    if durum == "tedarik":
        insan(g, n["AD"][0], n["AD"][1] + 0.5, ZL, -math.pi / 2, "#3f8a4a", "#3a3d42", "#6b452a", R)
        kutu(g, n["AD"][0] - 0.2, n["AD"][0] + 0.2, n["AD"][1] + 0.05, n["AD"][1] + 0.4, ZL + 0.95, ZL + 1.25, R["karton"])
    return g


# ================================== ÇATI ==================================

def cati_kur(g, R, rnd):
    f = cepheler()
    # tavan döşemesi: duvarların üstünde, pahın üstünü de örter (köşe sundurması)
    kutu(g, X0 - 0.04, X1 + 0.04, Y0 - 0.04, Y1 + 0.04, ZT, ZC, {"*": R["trim"], "zn": R["tavan"]}, atla=())
    # saçak altı korniş: dört yanda profilli
    kc = dict(on=cer((X0, Y0), (0, -1), X1 - X0), sag=cer((X1, Y0), (1, 0), Y1 - Y0), arka=f["arka"], sol=f["sol"])
    for k in ("on", "sag", "arka", "sol"):
        c = kc[k]
        c.kutu(g, -0.1, c.L + 0.1, 0, 0.1, ZT - 0.2, ZT - 0.09, R["trim"])
        c.kutu(g, -0.17, c.L + 0.17, 0, 0.17, ZT - 0.09, ZT + 0.0, R["trim"])
        n = int(c.L / 0.42)                                  # diş sırası
        for i in range(n):
            uu = (c.L - (n - 1) * 0.42) / 2 + i * 0.42
            c.kutu(g, uu - 0.06, uu + 0.06, 0.1, 0.16, ZT - 0.19, ZT - 0.09, R["trim"])
    egim, sacak = 0.56, SACAK
    zr = B.besik_cati(g, X0, X1, Y0, Y1, ZC, egim, R["cati"], R["alinlik"], sacak=sacak, yan=0.34, kalin=0.14)
    ym = (Y0 + Y1) / 2
    hh = (Y1 - Y0) / 2 * egim
    # alınlıklarda yuvarlak tavan arası penceresi ve ahşap saçak tahtası
    for xs, n in ((X1, (1, 0)), (X0, (-1, 0))):
        c = cer((xs, Y0 if n[0] > 0 else Y1), n, Y1 - Y0)
        uc = c.L / 2
        zc = ZC + hh * 0.42
        c.yarim_disk(g, uc, zc, 0.42, 0, 0.06, R["trim"], n=8)
        c.yarim_disk(g, uc, zc, 0.33, 0.06, 0.08, R["cam2"], n=8)
        c.kutu(g, uc - 0.5, uc + 0.5, 0, 0.1, zc - 0.09, zc, R["trim"])
        c.kutu(g, uc - 0.02, uc + 0.02, 0.08, 0.095, zc, zc + 0.33, R["trim"])
        for s in (-1, 1):                                     # rüzgâr tahtaları (alınlık kenarı)
            a = c.p(uc + s * (c.L / 2 + sacak), 0.36, ZC - sacak * egim + 0.02)
            b = c.p(uc, 0.36, ZC + hh + 0.06)
            cubuk(g, a, b, 0.05, R["ahsap_boya"], 0.2)
        # tepe süsü
        b = c.p(uc, 0.36, ZC + hh)
        kutu(g, b[0] - 0.05, b[0] + 0.05, b[1] - 0.05, b[1] + 0.05, ZC + hh - 0.15, ZC + hh + 0.5, R["ahsap_boya"])
        meyve(g, b[0], b[1], ZC + hh + 0.5, 0.08, R["pirinc"])
    # çatı pencereleri (ön etek: iki tane; arka etek: yatık çatı camı)
    st = dict(tip="kare", cerceve=R["trim"], denizlik=None, lento=None, panjur=None, saksi=0, cam="#a9c9de")
    for xc in (-2.6, 1.2):
        B.cati_penceresi(g, xc, 1.25, Y0 + 0.75, ym, zr, R["cati"], R["alinlik"], rnd, st)
    ya, yb = Y1 - 2.4, Y1 - 1.5
    p = [(-0.9, ya, zr(ya) + 0.17), (0.3, ya, zr(ya) + 0.17), (0.3, yb, zr(yb) + 0.17), (-0.9, yb, zr(yb) + 0.17)]
    q = [(a[0], a[1], a[2] - 0.05) for a in p]
    kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]],
         [R["cam"], None, R["metal_koyu"], R["metal_koyu"], R["metal_koyu"], R["metal_koyu"]])
    # baca (tuğla, şapkalı), yanında ikinci ince baca borusu
    B.baca(g, 3.1, ym + 1.0, zr(ym + 1.0) - 0.1, ZC + hh + 0.95, R["tugla"], w=0.62, d=0.5)
    for i in range(3):
        kutu(g, 3.1 - 0.33, 3.1 + 0.33, ym + 1.0 - 0.27, ym + 1.0 + 0.27, ZC + hh + 0.1 + i * 0.3, ZC + hh + 0.14 + i * 0.3, M("Tugla", "#8f4a38"))
    kutu(g, 3.1 - 0.36, 3.1 + 0.36, ym + 1.0 - 0.3, ym + 1.0 + 0.3, ZC + hh + 1.22, ZC + hh + 1.28, R["metal_koyu"], atla=())
    for sx in (-0.2, 0.2):
        kutu(g, 3.1 + sx - 0.03, 3.1 + sx + 0.03, ym + 1.0 - 0.22, ym + 1.0 + 0.22, ZC + hh + 1.05, ZC + hh + 1.22, R["metal_koyu"])
    dsil(g, -4.0, ym - 1.2, zr(ym - 1.2), ZC + hh + 0.45, 0.09, 8, R["metal_koyu"], kapak=(False, True))
    dsil(g, -4.0, ym - 1.2, ZC + hh + 0.45, ZC + hh + 0.55, 0.16, 8, R["metal_koyu"], r1=0.03, kapak=(True, False))
    # anten ve çanak
    ax, ay = 0.2, ym + 0.05
    cubuk(g, (ax, ay, ZC + hh), (ax, ay, ZC + hh + 1.35), 0.045, R["metal"])
    for i, (zz, L) in enumerate(((1.28, 0.5), (1.06, 0.62), (0.84, 0.74))):
        cubuk(g, (ax - L, ay, ZC + hh + zz), (ax + L, ay, ZC + hh + zz), 0.03, R["metal"])
        for s in (-1, 1):
            cubuk(g, (ax + s * L * 0.7, ay - 0.16, ZC + hh + zz), (ax + s * L * 0.7, ay + 0.16, ZC + hh + zz), 0.025, R["metal"])
    B.canak(g, -1.3, ym - 0.6, zr(ym - 0.6) + 0.1, (0.5, -0.86))
    # yağmur oluğu (arka saçak boyunca; önde saçağın önünü tabela kapatır) ve borulara inen dirsekler
    ye = Y1 + sacak + 0.07
    ze = ZC - sacak * egim - 0.03
    kutu(g, X0 - 0.34, X1 + 0.34, ye - 0.07, ye + 0.07, ze - 0.06, ze + 0.05, {"*": R["oluk"], "zp": R["demir"]}, atla=())
    for x in (X0 + 1.0, 0.0, X1 - 1.0):
        kutu(g, x - 0.02, x + 0.02, ye - 0.085, ye + 0.085, ze - 0.075, ze + 0.06, R["metal_koyu"], atla=())
    cubuk(g, (X1 + 0.07, ye, ze - 0.06), (X1 + 0.07, Y1 - 0.25, ZT - 0.02), 0.09, R["oluk"])
    cubuk(g, (X0 - 0.07, ye, ze - 0.06), (X0 - 0.07, Y1 - 0.3, ZT - 0.02), 0.09, R["oluk"])

    # ---- saçak tabelası: ön saçağın önünde duran büyük tabela (oyunun yukarıdan bakan kamerasında da okunur)
    dg, dg2 = R["ahsap_boya"], R["ahsap_boya2"]
    tx0, tx1 = X0 + 0.35, X1 - PAH - 0.15
    ty = Y0 - sacak - 0.04
    tz0, tz1 = ZC - 0.5, ZC + 0.44
    ct = cer((tx0, ty), (0, -1), tx1 - tx0)
    ct.kutu(g, 0, ct.L, 0, 0.1, tz0, tz1, R["tabela"], arka=True)
    ct.kutu(g, -0.06, ct.L + 0.06, -0.02, 0.15, tz1, tz1 + 0.08, dg2, arka=True)
    ct.kutu(g, -0.06, ct.L + 0.06, -0.02, 0.15, tz0 - 0.08, tz0, dg2, arka=True)
    for uu in (-0.06, ct.L - 0.02):
        ct.kutu(g, uu, uu + 0.08, -0.02, 0.15, tz0, tz1, dg2, arka=True)
    ct.kutu(g, 0.12, ct.L - 0.12, 0.1, 0.112, tz0 + 0.1, tz0 + 0.13, R["pirinc"])
    ct.kutu(g, 0.12, ct.L - 0.12, 0.1, 0.112, tz1 - 0.13, tz1 - 0.1, R["pirinc"])
    zm = (tz0 + tz1) / 2
    yazi(g, ct, ct.L / 2, zm, TABELA, 0.082, 0.1, 0.145, R["yazi"])
    for uu in (0.55, ct.L - 0.55):                      # iki uçta süs: pirinç karo içinde kırmızı elmas
        ct.kutu(g, uu - 0.17, uu + 0.17, 0.1, 0.13, zm - 0.17, zm + 0.17, R["pirinc"])
        ct.kutu(g, uu - 0.09, uu + 0.09, 0.13, 0.145, zm - 0.09, zm + 0.09, M("Yazi", "#e0503a"))
    for uu in (0.5, ct.L * 0.33, ct.L * 0.67, ct.L - 0.5):
        # duvardan tabelaya payanda, arkadan çatıya gergi, üstte kaz boynu lamba
        cubuk(g, ct.p(uu, -sacak - 0.03, ZT - 0.38), ct.p(uu, 0.02, tz0 + 0.02), 0.06, dg)
        cubuk(g, ct.p(uu, 0.0, tz1 - 0.05), ct.p(uu, -0.75, zr(Y0 + 0.4) + 0.2), 0.035, R["demir"])
        cubuk(g, ct.p(uu, 0.05, tz1 + 0.08), ct.p(uu, 0.05, tz1 + 0.3), 0.03, R["demir"])
        cubuk(g, ct.p(uu, 0.05, tz1 + 0.3), ct.p(uu, 0.36, tz1 + 0.24), 0.03, R["demir"])
        lp = ct.p(uu, 0.36, tz1 + 0.1)
        dsil(g, lp[0], lp[1], tz1 + 0.1, tz1 + 0.26, 0.11, 6, R["demir"], r1=0.04, kapak=(False, True))
        dsil(g, lp[0], lp[1], tz1 + 0.07, tz1 + 0.1, 0.085, 6, R["isik"], kapak=(True, False))

    # çatıda kedi ve iki güvercin
    kedi(g, 2.0, ym + 0.02, ZC + hh + 0.18, math.radians(-60), "#3a3d42")
    for i, bx in enumerate((-3.0, -2.72)):
        top(g, (bx, ym, ZC + hh + 0.28), 0.1, 0.07, 0.07, M("Kus", "#9aa3a8"))
        top(g, (bx + 0.08, ym, ZC + hh + 0.36), 0.045, 0.04, 0.045, M("Kus", "#7d8388"))
    return zr


# ================================== MODEL ==================================

def birlestir(ad, *parcalar):
    """Birkaç geometriyi tek G'de toplar (önizleme ve hafif sürüm için). Katı numaraları çakışmasın diye kaydırılır."""
    h = G(ad)
    kay = 0
    for p in parcalar:
        for f, m, s in zip(p.f, p.fm, p.fs):
            h.sid = kay + s
            h.yuz([p.v[i] for i in f], p.mats[m])
        kay += p.sid + 1
    h.sid = kay
    return h


# Oyunda adına göre özel davranan malzeme etiketleri (dışarıda korunur): renk çeşitlemesi (Duvar, Cati, Kapi, Panjur),
# gece ışığı (Cam*, Vitrin, Yazi, Isik), mevsim ve kar (Cati, Tas, Toprak, Cali, Cicek, Tente).
# Kesit: duvarların çatı kalkınca görünen üst yüzü (kar katmanı tutmaz: City3D.KarTutmaz).
ANLAMLI = ("Duvar", "Cati", "Kapi", "Panjur", "Cam", "Cam2", "Cam3", "Vitrin", "Yazi", "Isik", "Cali", "Cicek", "Tas", "Toprak", "Tente", "Kesit", "Kepenk")
ISIKLI = ("Cam", "Cam2", "Cam3", "Vitrin", "Yazi", "Isik")
RENK_ESIK = 30.0      # bu kadar yakın renkler (RGB uzaklığı, 0–441) tek malzemede birleşir


def sadelestir(parcalar):
    """Malzeme sayısını azaltır (her malzeme Unity'de ayrı bir çizim): özel anlamı olmayan etiketler "Boya" olur, birbirine
    yakın renkler birleşir. İç mekânda mevsim / kar etiketleri de "Boya"ya çevrilir (içeride kar yağmasın, yaprak sararmasın).
    parcalar: (geometri, ic_mi) listesi. Malzemeler ayrıca yukarı bakan alana göre sıralanır (kar katmanı ilk 30 malzemeye bakar)."""
    def rgb(h):
        return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))
    kullanim = {}
    yeni_etiket = {}
    for g, ic_mi in parcalar:
        for f, mi in zip(g.f, g.fm):
            ad = g.mats[mi]
            et = ad.rsplit("_", 1)[0]
            ye = et if (et in ISIKLI or (not ic_mi and et in ANLAMLI)) else "Boya"
            yeni_etiket[(id(g), ad)] = ye
            k = (ye, B.HEX[ad])
            kullanim[k] = kullanim.get(k, 0) + len(f) - 2
    temsilci = {}          # etiket -> [hex]
    esle = {}
    for (ye, hx), _ in sorted(kullanim.items(), key=lambda kv: (-kv[1], kv[0])):
        liste = temsilci.setdefault(ye, [])
        c = rgb(hx)
        en, eniyi = None, RENK_ESIK if ye == "Boya" else 14.0
        for t in liste:
            d = math.sqrt(sum((a - b) ** 2 for a, b in zip(c, rgb(t))))
            if d < eniyi:
                en, eniyi = t, d
        if en is None:
            liste.append(hx)
            en = hx
        esle[(ye, hx)] = M(ye, en)
    for g, ic_mi in parcalar:
        eski = g.mats
        g.mats, g._mi = [], {}
        alan = {}
        yeni_fm = []
        for f, mi in zip(g.f, g.fm):
            ad = eski[mi]
            yad = esle[(yeni_etiket[(id(g), ad)], B.HEX[ad])]
            yeni_fm.append(yad)
            n = B.newell([g.v[i] for i in f])
            L = math.sqrt(B.dot(n, n))
            if L > 1e-12 and n[2] / L > 0.8:
                alan[yad] = alan.get(yad, 0.0) + L * 0.5
            else:
                alan.setdefault(yad, 0.0)
        for ad in sorted(alan, key=lambda a: (-alan[a], a)):
            g.mi(ad)
        g.fm = [g._mi[a] for a in yeni_fm]


def temizle(g):
    """Alanı sıfır olan yüzleri atar (Blender ve Unity'de bozuk normal / uyarı çıkmasın). Atılan yüz sayısını döndürür."""
    f2, fm2, fs2, atilan = [], [], [], 0
    for f, m, sd in zip(g.f, g.fm, g.fs):
        n = B.newell([g.v[i] for i in f])
        if len(f) < 3 or B.dot(n, n) < 1e-16:
            atilan += 1
            continue
        f2.append(f); fm2.append(m); fs2.append(sd)
    g.f, g.fm, g.fs = f2, fm2, fs2
    return atilan


def bakkal():
    """(gövde, iç, çatı) geometrileri. Adlı ek parçalar (kapı, kepenk, ürün grupları ...) EK sözlüğünde."""
    rnd = random.Random(TOHUM)
    R = renkler()
    EK.clear()
    gv, ic, ct = G(AD + "_Govde"), G(AD + "_Ic"), G(AD + "_Cati")
    f = govde_kur(gv, R, rnd)
    dis_esyalar(gv, R, rnd, f)
    ic_kur(ic, R, rnd)
    ic_ekler(ic, R, rnd)
    cati_kur(ct, R, rnd)
    canli_parcalar(R, rnd)
    for g in [gv, ic, ct] + list(EK.values()):
        temizle(g)
    # dışarıda duran ek parçalar mevsim / kar etiketlerini korur; içeridekiler "Boya" olur
    sadelestir([(gv, False), (ic, True), (ct, False)] + [(g, ad not in DIS_EK) for ad, g in EK.items()])
    return gv, ic, ct


DIS_EK = ("Kapi", "Kepenk1", "Kepenk2", "Kepenk3", "Kamyonet", "Koliler", "Duvar_On", "Duvar_Pah", "Duvar_Sag", "Duvar_Arka", "Duvar_Sol")     # dışarıda görünen ek parçalar
IC_EK_ONEK = ("Urun_", "Ampul", "Poset", "Kasa2", "Sogutucu4", "Vitrin2")     # çatı açıkken görünenler


def duvarlar(haric=()):
    """Duvarların bel üstü objeleri (haric: inmiş sayılacak cepheler, ör. ("On", "Pah"))."""
    return [g for ad, g in EK.items() if ad.startswith("Duvar_") and ad[6:] not in haric]


def hafif_surum(gv, ct):
    """Uzak görünüm: gövde + çatı tek objede; küçük parçalar atılır, iç mekân hiç yok."""
    h = B.hafif(birlestir(AD, gv, ct, EK["Kapi"], *duvarlar()), esik=0.36)
    h.ad = AD + "_Hafif"
    # tabela harfleri küçük oldukları için atıldı: yerlerine kelime başına tek açık renk blok (uzaktan yazı gibi okunur)
    tx0, tx1 = X0 + 0.35, X1 - PAH - 0.15
    c = cer((tx0, Y0 - SACAK - 0.04), (0, -1), tx1 - tx0)
    px = 0.082
    en = (len(TABELA) * 6 - 1) * px
    u = c.L / 2 - en / 2
    ym = M("Yazi", "#fff2c8")
    for kelime in TABELA.split(" "):
        w = (len(kelime) * 6 - 1) * px
        c.kutu(h, u, u + w, 0.1, 0.13, ZC - 0.03 - 2.2 * px, ZC - 0.03 + 2.2 * px, ym)
        u += w + 7 * px
    return h


# ================================== BLENDER ==================================

def main():
    import bpy
    klasor = os.path.abspath(B.cikti_klasoru())
    os.makedirs(klasor, exist_ok=True)
    k = bpy.data.collections.get("KuryeBakkal") or bpy.data.collections.new("KuryeBakkal")
    if k.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(k)
    gv, ic, ct = bakkal()
    hf = hafif_surum(gv, ct)
    for dosya, parcalar in ((AD, [gv, ic, ct] + list(EK.values())), (AD + "_Hafif", [hf])):
        obler = []
        for g in parcalar:
            pv = getattr(g, "pivot", (0.0, 0.0, 0.0))
            if pv != (0.0, 0.0, 0.0):
                # kök pivotta dursun: köşeler pivota göre yazılır, obje pivota taşınır
                g2 = G(g.ad)
                g2.v = [(p[0] - pv[0], p[1] - pv[1], p[2] - pv[2]) for p in g.v]
                g2.f, g2.fm, g2.mats = g.f, g.fm, g.mats
                ob = B.blender_obje(g2, g.ad, k)
                ob.location = pv
            else:
                ob = B.blender_obje(g, g.ad, k)
            obler.append(ob)
        B.fbx_kaydet(obler, os.path.join(klasor, dosya + ".fbx"))
        for ob in obler:
            me = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            bpy.data.meshes.remove(me)
        print("KAYDEDILDI", dosya, "%d obje, %d ucgen" % (len(parcalar), sum(g.ucgen() for g in parcalar)))
    print("BAKKAL BITTI -> %s" % klasor)


# ================================== ÖNİZLEME (Blender'sız) ==================================

def _kirp_resim(im, boy, arka=(236, 232, 224)):
    """Resmi içeriğe göre kırpar (küçük pay bırakır), kareye tamamlar ve boy × boy yapar."""
    from PIL import Image, ImageChops
    fark = ImageChops.difference(im.convert("RGB"), Image.new("RGB", im.size, arka)).convert("L").point(lambda p: 255 if p > 8 else 0)
    kutu_ = fark.getbbox()
    if not kutu_:
        return im.resize((boy, boy))
    x0, y0, x1, y1 = kutu_
    w = max(x1 - x0, y1 - y0) * 1.06
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    kare = Image.new("RGB", (int(w), int(w)), arka)
    kare.paste(im, (int(w / 2 - cx), int(w / 2 - cy)))
    return kare.resize((boy, boy), Image.LANCZOS)


def _kopya(g, ad=None):
    h = G(ad or g.ad)
    h.v, h.f, h.fm, h.fs, h.mats, h.sid = list(g.v), list(g.f), list(g.fm), list(g.fs), list(g.mats), g.sid
    h._mi = dict(g._mi)
    return h


def _kapi_acik(derece):
    """Kapı kanadının önizleme kopyası (oyundaki gibi içeri doğru açılmış)."""
    k = _kopya(EK["Kapi"])
    dondur(k, EK["Kapi"].pivot, -math.radians(derece))
    return k


def onizleme(klasor):
    sys.path.insert(0, os.path.dirname(os.path.abspath(B.__file__)))
    from bolge_onizleme import render
    from PIL import Image
    os.makedirs(klasor, exist_ok=True)
    gv, ic, ct = bakkal()
    hf = hafif_surum(gv, ct)
    R = renkler()
    urunler = [g for ad, g in EK.items() if ad.startswith("Urun_")]
    kapi = _kapi_acik(38)
    gv_alt = gv                                   # yalnız bel altı ve cepheden bağımsız parçalar
    gv = birlestir(gv.ad, gv, *duvarlar())        # önizlemelerde gövde = bütün duvarlar yerinde
    tam = birlestir("tam", gv, ct, kapi)
    dolu = [gv, ic, EK["Ampul"]] + urunler
    acik_ = birlestir("acik", *(dolu + [kapi]))
    ek_ucgen = sum(g.ucgen() for g in EK.values())
    print("ucgen: govde %d, ic %d, cati %d, ek parcalar %d (%d obje), toplam %d, hafif %d; malzeme %d" %
          (gv.ucgen(), ic.ucgen(), ct.ucgen(), ek_ucgen, len(EK), gv.ucgen() + ic.ucgen() + ct.ucgen() + ek_ucgen, hf.ucgen(),
           len(set(gv.mats) | set(ic.mats) | set(ct.mats) | set(m for g in EK.values() for m in g.mats))))
    B_ = 900

    def ciz(liste, kolaj_adi, sutun):
        resimler = []
        for ad, g, az, el in liste:
            yol = os.path.join(klasor, "bakkal_%s.png" % ad)
            im = render(g, B.HEX, size=int(B_ * 1.5), az=az, el=el, kenar=0.12, normal_kenar=True)
            im = _kirp_resim(im, B_)
            im.save(yol)
            resimler.append(im)
            print(yol)
        satir = (len(resimler) + sutun - 1) // sutun
        kolaj = Image.new("RGB", (B_ * sutun, B_ * satir), (236, 232, 224))
        for i, im in enumerate(resimler):
            kolaj.paste(im, ((i % sutun) * B_, (i // sutun) * B_))
        kolaj.save(os.path.join(klasor, kolaj_adi), quality=90)
        print(os.path.join(klasor, kolaj_adi))

    ciz([
        ("1_on_capraz", tam, 35, 24), ("2_on_sol", tam, -40, 26), ("3_arka_capraz", tam, 145, 28), ("4_arka_sol", tam, -150, 28),
        ("5_oyun_acisi", tam, 30, 46), ("6_catisiz_tepeden", acik_, 0, 80), ("7_catisiz_capraz", acik_, 35, 46),
        ("8_catisiz_arka", acik_, -145, 48), ("9_hafif", hf, 35, 30),
    ], "bakkal_kolaj.jpg", 3)

    # ---- canlı durumlar (oyunda BakkalCanli'nin kurduğu sahnelerin durağan karşılıkları)
    kapali = birlestir("kapali", gv, ct, EK["Kapi"], EK["Kepenk1"], EK["Kepenk2"], EK["Kepenk3"])
    tedarik = birlestir("tedarik", gv, ct, kapi, EK["Kamyonet"], EK["Koliler"], onizleme_insanlari(R, "tedarik"))
    canli = birlestir("canli", *(dolu + [_kapi_acik(80), EK["Poset"], onizleme_insanlari(R)]))
    bos = birlestir("bos", gv, ic, kapi, EK["Ampul"], *[g for ad, g in EK.items() if ad.startswith("Urun_") and ad.endswith("_1")])
    yukseltme = birlestir("yukseltme", *(dolu + [kapi, EK["Kasa2"], EK["Sogutucu4"], EK["Vitrin2"]]))
    inik = birlestir("inik", gv_alt, ic, EK["Ampul"], EK["Poset"], onizleme_insanlari(R), *(urunler + duvarlar(("On", "Pah", "Sag"))))
    ciz([
        ("16_on_duvarlar_inik", inik, 35, 40),
        ("10_kepenk_kapali", kapali, 35, 24), ("11_tedarik_kamyoneti", tedarik, 150, 36), ("12_canli_capraz", canli, 35, 48),
        ("13_canli_tepeden", canli, 0, 80), ("14_raflar_bosalmis", bos, 35, 48), ("15_yukseltmeler", yukseltme, -150, 50),
    ], "bakkal_canli_kolaj.jpg", 3)


def tablo():
    """BakkalCanli.cs'e yapıştırılacak C# tablosu (yol noktaları, kenarlar, referans pivotlar)."""
    bakkal()
    def v3(p):
        return "new Vector3(%sf, %sf, %sf)" % tuple(("%.3f" % a).rstrip("0").rstrip(".") if "." in ("%.3f" % a) else "%.3f" % a for a in p)
    print("        // ---- bakkal.py --tablo çıktısı (elle değiştirme) ----")
    print("        static readonly (string ad, Vector3 p)[] Noktalar =\n        {")
    for ad in sorted(NOKTA):
        print("            (\"%s\", %s)," % (ad, v3(NOKTA[ad])))
    print("        };")
    print("        static readonly string[] Kenarlar =\n        {")
    print("            " + ", ".join("\"%s-%s\"" % k for k in NOKTA_KENAR))
    print("        };")
    print("        static readonly (string ek, Vector3 p)[] Pivotlar =\n        {")
    for ad in ("Kapi", "Kepenk1", "Kamyonet", "Poset", "Kepenk2", "Kepenk3", "Koliler"):
        print("            (\"_%s\", %s)," % (ad, v3(EK[ad].pivot)))
    print("        };")
    print("        // ---- tablo sonu ----")


if B.IN_BLENDER and __name__ == "__main__":
    main()
elif __name__ == "__main__" and "--onizleme" in sys.argv:
    onizleme(sys.argv[sys.argv.index("--onizleme") + 1])
elif __name__ == "__main__" and "--tablo" in sys.argv:
    tablo()
