# -*- coding: utf-8 -*-
"""
KURYE TYCOON · STİLİZE VARLIK ÜRETİCİSİ (Blender scripti; eski adı stilize_paket.py)

SimplePoly City ve City People paketlerinin yerine oyunun kendi tek tip görünümü: düşük poligonlu, yumuşak köşeli (pahlı kutu,
kapsül, top), sıcak renkli, mürekkep dış çizgili ve bütün renkleri tek ortak dokuda (StilizePalet.png) modeller.
  1. Kurye karakteri: şirket renginde mont, termal çanta ve kask; iskeletli, "basic walk", "idle" ve "pedal" animasyonları FBX'in içinde
  2. Teslimat araçları: bisiklet ve geç oyun kargo bisikleti (önde yük kasası); tekerler ve pedal ayrı obje (Unity'de döndürülür).
     Ölçü gerçek (metre): Erman'ın VW Polo'su (3,75 m) ile aynı ölçekte.
  3. Şehir donatıları: paket dolabı (şirket renginde yan paneller), iki ağaç (yuvarlak, kavak), sokak lambası (camı ayrı "Isik")
  4. Budapeşte tarzı binalar (14 m arsaya): bérház (okra, altta dükkânlar), köşe binası (pembe, bakır külahlı kule),
     dükkân binası (adaçayı, beşik çatı), apartman (sarı, arduvaz mansart çatı). Camlar ayrı "Cam" / "Vitrin" malzemesinde (gece yanar).

Çalıştırma (hepsi aynı çıktıyı verir):
  • Mac terminal (repo kökünden): Tools/varlik_uret.sh            (Blender'ı kendisi bulur, arka planda çalıştırır)
  • Unity: Kurye Tycoon > Blender > Stilize varlıkları üret
  • Terminal: blender --background --python Tools/Blender/varlik_uretici.py -- --cikti Art/Stilize
  • Blender içinde: Scripting > Text > Open ile bu dosyayı aç, Run Script (▶). Çıktı klasörü için aşağıda CIKTI.
  Blender'sız (sadece Python): python3 varlik_uretici.py --onizleme onizleme.png   → modellerin önizleme resmi
  Sadece bazıları: -- --modeller kurye,bina_berhaz

Çıktı (varsayılan Assets/KuryeTycoon/Art/Stilize): Kurye, Bisiklet, KargoBisikleti, PaketDolabi, Agac_Yuvarlak, Agac_Kavak,
SokakLambasi, Bina_Berhaz, Bina_Kose, Bina_Dukkan, Bina_Apartman (.fbx) ve StilizePalet.png. Unity'de
Kurye Tycoon > Stilize Prefabları Kur malzemeleri ve Prefabs/Stilize altındaki prefabları kurar.

Kurallar (kurye_modeller.py ile aynı, Unity tarafı bunlara göre kurulu):
  • Önü Blender'da −Y'ye bakar, sol el +X, taban z = 0, ölçü metre.
  • Kuryenin iskeleti ve animasyonları kurye_modeller.py'deki KEMIKLER / ANIMASYONLAR ile birebir aynı (oyun tek
    yürüme animasyonunu bütün karakterlere takar). Biri değişirse ikisi birlikte değişmeli. İskelet FBX'te "Iskelet" adını alır.
  • "SirketRengi" ve "SirketRengi2" ile başlayan malzemeler oyunda oyuncunun şirket renklerine boyanır (CharacterVisuals).
  • "Cizgi" malzemesi ters kabuk dış çizgidir: Unity'de Universal Render Pipeline/Unlit, siyah.
  • "Isik_", "Cam_" ve "Vitrin_" malzemeleri gündüz kendi renginde, gece oyun (GeceIsiklari) yakar.
  • Renkler tek dokuda (StilizePalet.png, 8×8 renk karesi): her yüz kendi renginin karesine bakar; karenin içinde yukarıdan
    aşağı hafif degrade var (üst sıcak ve açık, alt serin ve koyu), model boyunca boyanmış gibi görünür. Tek malzeme,
    tek çizim çağrısı; renk değiştirmek için dokuyu boyamak ya da PALET'i değiştirip yeniden çalıştırmak yeter.
"""

import math
import os
import random
import struct
import sys
import zlib

try:
    import bpy
    IN_BLENDER = True
except ImportError:          # Blender dışında sadece geometri ve önizleme
    IN_BLENDER = False

# ================================== AYARLAR ==================================
CIKTI = ""                   # boşsa: Assets/KuryeTycoon/Art/Stilize (bulunamazsa ~/Desktop/KuryeStilize)
CIZGI = True                 # mürekkep dış çizgi (ters kabuk; üçgen sayısını yaklaşık ikiye katlar)
CIZGI_KALINLIK = 0.012       # metre (kurye); modele göre çarpanla büyür
TOHUM = 7                    # ağaç yapraklarının pürüzü
MODELLER = ("kurye", "bisiklet", "kargo_bisikleti", "paket_dolabi", "agac_yuvarlak", "agac_kavak", "sokak_lambasi",
            "bina_berhaz", "bina_kose", "bina_dukkan", "bina_apartman")
DISA_AKTAR = True

SIRKET_RENGI = "#009ede"     # Unity'de oyuncunun şirket rengine boyanır
SIRKET_RENGI2 = "#ffffff"
ISIK_RENGI = "#ffd27a"       # lamba camı (gece yanar)
CIZGI_RENGI = "#1e1712"      # sıcak siyah mürekkep
CAM_RENGI = "#86a9ad"        # pencere camı (gece yanar)
VITRIN_RENGI = "#a3c4c0"     # dükkân vitrini (gece yanar)

# Sıcak palet: (ad, renk). Sıra dokudaki yer: 8 sütun, yukarıdan aşağı.
PALET = [
    ("ten", "#f1c7a5"), ("ten_koyu", "#c98f6a"), ("sac", "#4a3226"), ("goz", "#2a1d17"),
    ("pantolon", "#3d4a66"), ("ayakkabi", "#43342c"), ("taban", "#e9dcc6"), ("kayis", "#5a4436"),
    ("metal", "#b8b2a6"), ("metal_koyu", "#4a4744"), ("lastik", "#2e2a28"), ("sele", "#6b4430"),
    ("krem", "#f0e2c4"), ("krem_koyu", "#d9c7a4"), ("ekran", "#2f4f5c"), ("cam", "#bfe0dc"),
    ("yaprak1", "#86b35c"), ("yaprak2", "#5f9a4f"), ("yaprak3", "#a9c86a"), ("yaprak_koyu", "#3f7a48"),
    ("kabuk", "#7a5238"), ("toprak", "#8a6446"), ("cicek1", "#e2725f"), ("cicek2", "#f2b84b"),
    ("direk", "#3f4e4a"), ("direk_acik", "#5f716b"), ("saksi", "#c8744a"), ("yesil_isik", "#7ccf7a"),
    ("kirmizi", "#d65a46"), ("tahta", "#9a6a44"), ("beyaz", "#f7f1e6"), ("gri", "#8c8680"),
    # Budapeşte binaları: pastel / toprak cepheler, taş, kiremit, arduvaz, bakır patina
    ("tas", "#d8ccb2"), ("tas_koyu", "#a8987c"), ("okra", "#d9a55e"), ("okra_acik", "#e6bf80"),
    ("pembe", "#dea294"), ("pembe_acik", "#ecbcae"), ("adacayi", "#9fb48a"), ("sari", "#e9cf86"),
    ("kiremit", "#b85a3c"), ("kiremit_koyu", "#93412e"), ("arduvaz", "#5b6470"), ("bakir", "#6ea592"),
    ("baca", "#a5624a"), ("kapi", "#6a412c"), ("tente1", "#c4513f"), ("tente2", "#3f7a72"),
    ("demir", "#2f3437"),
]
SUTUN, KARE = 8, 32          # doku 256×256: 8×8 kare, her biri 32 piksel
PALET_DOKU = "StilizePalet.png"

# Dokusuz (düz) malzemeler: oyun bunları adından tanır
SR, SR2, ISIK, CIZ, CAM, VITRIN = "SirketRengi", "SirketRengi2", "Isik", "Cizgi", "Cam", "Vitrin"
DUZ = {SR: SIRKET_RENGI, SR2: SIRKET_RENGI2, ISIK: ISIK_RENGI, CIZ: CIZGI_RENGI, CAM: CAM_RENGI, VITRIN: VITRIN_RENGI}


def mal_adi(anahtar):
    """Blender/Unity malzeme adı: dokulu palet tek malzeme; düz renklerde ad_renk (kurye_modeller.M ile aynı biçim)."""
    if anahtar in DUZ:
        return f"{anahtar}_{DUZ[anahtar].strip('#')}"
    return "StilizePalet"


# ------------------------------------------------------------------ vektör

def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def norm(a):
    l = math.sqrt(dot(a, a))
    return (0.0, 0.0, 1.0) if l < 1e-12 else (a[0] / l, a[1] / l, a[2] / l)


def newell(pts):
    n = [0.0, 0.0, 0.0]
    for i, p in enumerate(pts):
        q = pts[(i + 1) % len(pts)]
        n[0] += (p[1] - q[1]) * (p[2] + q[2])
        n[1] += (p[2] - q[2]) * (p[0] + q[0])
        n[2] += (p[0] - q[0]) * (p[1] + q[1])
    return tuple(n)


def ort(pts):
    k = 1.0 / len(pts)
    return (sum(p[0] for p in pts) * k, sum(p[1] for p in pts) * k, sum(p[2] for p in pts) * k)


def eksen_cerceve(ax):
    """ax yönüne dik iki birim vektör (u, w): ax, u, w sağ elli."""
    ax = norm(ax)
    ref = (0.0, 0.0, 1.0) if abs(ax[2]) < 0.9 else (1.0, 0.0, 0.0)
    u = norm(cross(ref, ax))
    return ax, u, cross(ax, u)


# ------------------------------------------------------------------ gövde: köşe, yüz, malzeme, kemik

class Govde:
    """Saf Python ağ: Blender'sız da üretilir (önizleme). Her yüz: köşe sırası, renk anahtarı, yumuşak mı, degrade aralığı."""

    def __init__(self, ad):
        self.ad = ad
        self.v, self.vb = [], []                 # köşe, köşenin kemiği (karakter)
        self.f, self.fr, self.fy, self.fz = [], [], [], []   # yüz, renk anahtarı, yumuşak, (zmin, zmax)
        self.kemik = None                        # sıradaki parçaların kemiği
        self.parcalar = []                       # (ad, Govde, pivot): ayrı obje (teker, pedal, lamba camı)

    def ucgen_sayisi(self):
        n = sum(len(f) - 2 for f in self.f)
        return n + sum(p.ucgen_sayisi() for _, p, _ in self.parcalar)


def parca(g, pts, yuzler, renk, yumusak=False, kabuk=True, kalinlik=1.0, ref=None):
    """
    Dışbükey (ya da ref ile yüz yüz yönlendirilen) parça ekler. Yüzler dışa bakacak şekilde kendiliğinden çevrilir:
    ref yoksa parçanın merkezine göre, varsa ref(yüz merkezi) noktasına göre. kabuk: mürekkep dış çizgi.
    """
    m = ort(pts)
    duzgun = []
    for f in yuzler:
        fp = [pts[i] for i in f]
        n = newell(fp)
        c = ort(fp)
        r = ref(c) if ref else m
        duzgun.append(list(f) if dot(n, sub(c, r)) >= 0 else list(reversed(f)))
    z0 = min(p[2] for p in pts)
    z1 = max(p[2] for p in pts)
    b = len(g.v)
    g.v.extend(pts)
    g.vb.extend([g.kemik] * len(pts))
    for f in duzgun:
        g.f.append([b + i for i in f])
        g.fr.append(renk)
        g.fy.append(yumusak)
        g.fz.append((z0, z1))
    if CIZGI and kabuk:
        _kabuk(g, pts, duzgun, CIZGI_KALINLIK * kalinlik)


def _kabuk(g, pts, yuzler, t):
    """Ters kabuk: köşeler yüz normallerinin ortalaması boyunca t kadar dışarı, yüzler ters (arka yüz gizlemeyle çizgi olur)."""
    vn = [(0.0, 0.0, 0.0)] * len(pts)
    for f in yuzler:
        n = newell([pts[i] for i in f])          # alanla ağırlıklı
        for i in f:
            vn[i] = add(vn[i], n)
    yeni = [add(p, mul(norm(n), t)) for p, n in zip(pts, vn)]
    b = len(g.v)
    g.v.extend(yeni)
    g.vb.extend([g.kemik] * len(yeni))
    z0 = min(p[2] for p in yeni)
    z1 = max(p[2] for p in yeni)
    for f in yuzler:
        g.f.append([b + i for i in reversed(f)])
        g.fr.append(CIZ)
        g.fy.append(True)
        g.fz.append((z0, z1))


# ------------------------------------------------------------------ yumuşak köşeli ilkel şekiller

def pah_kutu(g, c, h, p, renk, yumusak=True, kabuk=True, kalinlik=1.0, aci=0.0):
    """Pahlı kutu: merkez c, yarı boyutlar h=(hx, hy, hz), kenar pahı p; yumuşak gölgelemeyle yuvarlak durur. aci: Z dönüşü (rad)."""
    hx, hy, hz = h
    p = max(1e-4, min(p, hx * 0.95, hy * 0.95, hz * 0.95))
    P, sira = {}, []
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                for e, q in (("x", (sx * hx, sy * (hy - p), sz * (hz - p))),
                             ("y", (sx * (hx - p), sy * hy, sz * (hz - p))),
                             ("z", (sx * (hx - p), sy * (hy - p), sz * hz))):
                    P[(sx, sy, sz, e)] = len(sira)
                    sira.append(q)
    ca, sa = math.cos(aci), math.sin(aci)
    pts = [(c[0] + q[0] * ca - q[1] * sa, c[1] + q[0] * sa + q[1] * ca, c[2] + q[2]) for q in sira]
    halka = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    F = []
    for s in (-1, 1):
        F.append([P[(s, a, b, "x")] for a, b in halka])
        F.append([P[(a, s, b, "y")] for a, b in halka])
        F.append([P[(a, b, s, "z")] for a, b in halka])
    for a in (-1, 1):
        for b in (-1, 1):
            F.append([P[(-1, a, b, "y")], P[(1, a, b, "y")], P[(1, a, b, "z")], P[(-1, a, b, "z")]])
            F.append([P[(a, -1, b, "x")], P[(a, 1, b, "x")], P[(a, 1, b, "z")], P[(a, -1, b, "z")]])
            F.append([P[(a, b, -1, "x")], P[(a, b, 1, "x")], P[(a, b, 1, "y")], P[(a, b, -1, "y")]])
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                F.append([P[(sx, sy, sz, "x")], P[(sx, sy, sz, "y")], P[(sx, sy, sz, "z")]])
    parca(g, pts, F, renk, yumusak, kabuk, kalinlik)


def kapsul(g, p0, p1, r, renk, n=8, k=2, kabuk=True, kalinlik=1.0):
    """p0–p1 arasında yarıçapı r kapsül (iki uç yarım küre)."""
    ax, u, w = eksen_cerceve(sub(p1, p0))
    pts, halkalar = [], []

    def halka_ekle(mrk, rr):
        bas = len(pts)
        for i in range(n):
            a = 2 * math.pi * i / n
            pts.append(add(mrk, add(mul(u, rr * math.cos(a)), mul(w, rr * math.sin(a)))))
        halkalar.append(bas)

    pts.append(sub(p0, mul(ax, r)))                                     # alt kutup
    for j in range(1, k + 1):
        a = 0.5 * math.pi * j / k
        halka_ekle(sub(p0, mul(ax, r * math.cos(a))), r * math.sin(a))
    for j in range(k, 0, -1):
        a = 0.5 * math.pi * j / k
        halka_ekle(add(p1, mul(ax, r * math.cos(a))), r * math.sin(a))
    pts.append(add(p1, mul(ax, r)))                                     # üst kutup
    ust = len(pts) - 1
    F = [[0, halkalar[0] + i, halkalar[0] + (i + 1) % n] for i in range(n)]
    for a, b in zip(halkalar, halkalar[1:]):
        F += [[a + i, a + (i + 1) % n, b + (i + 1) % n, b + i] for i in range(n)]
    F += [[ust, halkalar[-1] + (i + 1) % n, halkalar[-1] + i] for i in range(n)]
    parca(g, pts, F, renk, True, kabuk, kalinlik)


def silindir(g, p0, p1, r0, r1, renk, n=8, yumusak=True, kabuk=True, kalinlik=1.0, kapak=True):
    """p0'da r0, p1'de r1 yarıçaplı kesik koni (r0 = r1: silindir)."""
    ax, u, w = eksen_cerceve(sub(p1, p0))
    pts = []
    for mrk, rr in ((p0, r0), (p1, r1)):
        for i in range(n):
            a = 2 * math.pi * i / n
            pts.append(add(mrk, add(mul(u, rr * math.cos(a)), mul(w, rr * math.sin(a)))))
    F = [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    if kapak:
        F += [list(range(n)), list(range(n, 2 * n))]
    parca(g, pts, F, renk, yumusak, kabuk, kalinlik)


def top(g, c, r, renk, n=10, m=7, puruz=0.0, rnd=None, kabuk=True, kalinlik=1.0, yumusak=True):
    """Elipsoid (r = (rx, ry, rz)); puruz > 0: köşeler biraz oynar (yaprak öbeği)."""
    rx, ry, rz = r
    pts = [(c[0], c[1], c[2] - rz)]
    for j in range(1, m):
        b = math.pi * j / m - math.pi / 2
        for i in range(n):
            a = 2 * math.pi * i / n + (0.5 * math.pi / n if j % 2 else 0.0)
            q = (rx * math.cos(b) * math.cos(a), ry * math.cos(b) * math.sin(a), rz * math.sin(b))
            if puruz and rnd is not None:
                q = mul(q, 1.0 + rnd.uniform(-puruz, puruz))
            pts.append(add(c, q))
    pts.append((c[0], c[1], c[2] + rz))
    ust = len(pts) - 1
    F = [[0, 1 + (i + 1) % n, 1 + i] for i in range(n)]
    for j in range(m - 2):
        a, b = 1 + j * n, 1 + (j + 1) * n
        F += [[a + i, a + (i + 1) % n, b + (i + 1) % n, b + i] for i in range(n)]
    s = 1 + (m - 2) * n
    F += [[s + i, s + (i + 1) % n, ust] for i in range(n)]
    parca(g, pts, F, renk, yumusak, kabuk, kalinlik)


def yay(g, c, R, r, renk, a0=0.0, a1=2 * math.pi, n=16, m=6, kabuk=True, kalinlik=1.0):
    """X eksenli simit (teker, çamurluk): merkez c, ana yarıçap R, boru yarıçapı r; a0–a1 açı aralığı (YZ düzleminde)."""
    tam = abs(a1 - a0 - 2 * math.pi) < 1e-6
    adim = n if tam else n + 1
    pts = []
    for i in range(adim):
        a = a0 + (a1 - a0) * i / n
        ry, rz = math.cos(a), math.sin(a)
        for j in range(m):
            b = 2 * math.pi * j / m
            rad = R + r * math.cos(b)
            pts.append((c[0] + r * math.sin(b), c[1] + rad * ry, c[2] + rad * rz))
    F = []
    for i in range(n):
        i2 = (i + 1) % adim
        for j in range(m):
            j2 = (j + 1) % m
            F.append([i * m + j, i * m + j2, i2 * m + j2, i2 * m + j])
    if not tam:
        F += [list(range(m)), list(range(n * m, (n + 1) * m))]

    def boru_merkezi(q):     # yüzü borunun ortasına göre çevir (simit dışbükey değil)
        a = math.atan2(q[2] - c[2], q[1] - c[1])
        return (c[0], c[1] + R * math.cos(a), c[2] + R * math.sin(a))

    parca(g, pts, F, renk, True, kabuk, kalinlik, ref=boru_merkezi)


# ------------------------------------------------------------------ 1. kurye (iskelet ve animasyon kurye_modeller.py ile aynı)

# Karakterin önü −Y, sol eli +X. Kemik adları Mixamo/Unity'nin tanıdığı adlar.
KEMIKLER = {
    "Hips": ((0.0, 0.0, 0.88), (0.0, 0.0, 0.98), None),
    "Spine": ((0.0, 0.0, 0.98), (0.0, 0.0, 1.38), "Hips"),
    "Head": ((0.0, 0.0, 1.42), (0.0, 0.0, 1.72), "Spine"),
    "LeftArm": ((0.245, 0.0, 1.37), (0.245, 0.0, 1.11), "Spine"),
    "LeftForeArm": ((0.245, 0.0, 1.11), (0.245, 0.0, 0.86), "LeftArm"),
    "RightArm": ((-0.245, 0.0, 1.37), (-0.245, 0.0, 1.11), "Spine"),
    "RightForeArm": ((-0.245, 0.0, 1.11), (-0.245, 0.0, 0.86), "RightArm"),
    "LeftUpLeg": ((0.1, 0.0, 0.86), (0.1, 0.0, 0.47), "Hips"),
    "LeftLeg": ((0.1, 0.0, 0.47), (0.1, 0.0, 0.08), "LeftUpLeg"),
    "RightUpLeg": ((-0.1, 0.0, 0.86), (-0.1, 0.0, 0.47), "Hips"),
    "RightLeg": ((-0.1, 0.0, 0.47), (-0.1, 0.0, 0.08), "RightUpLeg"),
}
# Oyun "walk" ve "idle" geçen adları arar; "pedal" bisiklet sürerken (bisiklet animasyonu kurulunca)
ANIMASYONLAR = {"basic walk": 24, "idle": 48, "pedal": 24}


def kurye():
    g = Govde("Kurye")
    # Kalça ve bacaklar
    g.kemik = "Hips"
    pah_kutu(g, (0.0, 0.005, 0.905), (0.165, 0.11, 0.085), 0.045, "pantolon")
    for s, ust, alt in ((1, "LeftUpLeg", "LeftLeg"), (-1, "RightUpLeg", "RightLeg")):
        x = 0.1 * s
        g.kemik = ust
        kapsul(g, (x, 0.0, 0.83), (x, 0.0, 0.52), 0.072, "pantolon")
        g.kemik = alt
        kapsul(g, (x, 0.0, 0.46), (x, 0.0, 0.15), 0.062, "pantolon")
        pah_kutu(g, (x, -0.035, 0.058), (0.062, 0.122, 0.048), 0.032, "ayakkabi")            # burun önde (−Y)
        pah_kutu(g, (x, -0.035, 0.013), (0.066, 0.128, 0.013), 0.008, "taban", kabuk=False)
    # Gövde: mont, fermuar, omuzlar, boyun, termal çanta
    g.kemik = "Spine"
    pah_kutu(g, (0.0, 0.0, 1.17), (0.19, 0.125, 0.215), 0.075, SR)
    pah_kutu(g, (0.0, -0.121, 1.16), (0.016, 0.008, 0.19), 0.005, SR2, kabuk=False)
    for s in (1, -1):
        top(g, (0.19 * s, 0.0, 1.33), (0.08, 0.08, 0.07), SR, n=8, m=6)
        pah_kutu(g, (0.1 * s, -0.124, 1.21), (0.02, 0.008, 0.15), 0.005, "kayis", kabuk=False)    # çanta askıları
    silindir(g, (0.0, 0.0, 1.36), (0.0, 0.0, 1.45), 0.058, 0.052, "ten", n=8)
    pah_kutu(g, (0.0, 0.0, 1.375), (0.1, 0.09, 0.025), 0.02, SR2)                                # yaka
    pah_kutu(g, (0.0, 0.255, 1.18), (0.18, 0.12, 0.205), 0.05, SR)                              # termal çanta
    pah_kutu(g, (0.0, 0.255, 1.385), (0.185, 0.125, 0.025), 0.015, SR2)                          # kapak şeridi
    pah_kutu(g, (0.0, 0.378, 1.17), (0.075, 0.006, 0.075), 0.006, SR2, kabuk=False)              # arkada logo yeri
    # Kollar ve eller
    for s, ust, alt in ((1, "LeftArm", "LeftForeArm"), (-1, "RightArm", "RightForeArm")):
        x = 0.245 * s
        g.kemik = ust
        kapsul(g, (x, 0.0, 1.32), (x, 0.0, 1.14), 0.058, SR)
        g.kemik = alt
        kapsul(g, (x, 0.0, 1.09), (x, 0.0, 0.92), 0.052, SR)
        silindir(g, (x, 0.0, 0.905), (x, 0.0, 0.935), 0.056, 0.056, SR2, kabuk=False)            # kol ağzı
        top(g, (x, -0.005, 0.845), (0.05, 0.045, 0.06), "ten", n=8, m=6)
    # Baş: yüz, gözler, burun, kulaklar, saç, kask
    g.kemik = "Head"
    top(g, (0.0, -0.005, 1.58), (0.15, 0.145, 0.16), "ten", n=12, m=8)
    for s in (1, -1):
        top(g, (0.055 * s, -0.142, 1.59), (0.018, 0.01, 0.024), "goz", n=6, m=4, kabuk=False)
        top(g, (0.148 * s, 0.0, 1.57), (0.022, 0.03, 0.035), "ten", n=6, m=4, kabuk=False)
    top(g, (0.0, -0.151, 1.555), (0.02, 0.018, 0.02), "ten_koyu", n=6, m=4, kabuk=False)
    top(g, (0.0, 0.055, 1.55), (0.14, 0.11, 0.1), "sac", n=10, m=6, kabuk=False)
    top(g, (0.0, 0.005, 1.64), (0.162, 0.162, 0.105), SR, n=12, m=8)                             # kask (alt yarısı başın içinde)
    pah_kutu(g, (0.0, -0.152, 1.645), (0.11, 0.04, 0.01), 0.008, SR2)                            # siper
    return g


# ---------- animasyon (kurye_modeller.poz ile aynı; "pedal" bu pakette eklendi)

def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return ((1, 0, 0), (0, c, -s), (0, s, c))


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return ((c, -s, 0), (s, c, 0), (0, 0, 1))


def mat_mul(A, B):
    return tuple(tuple(sum(A[i][k] * B[k][j] for k in range(3)) for j in range(3)) for i in range(3))


def mat_vec(A, v):
    return tuple(A[i][0] * v[0] + A[i][1] * v[1] + A[i][2] * v[2] for i in range(3))


def fk_nokta(R, kay, kemik, p):
    """Noktayı kemiğinden köke doğru dönüşlerle taşır (Blender'ın kemik hesabıyla aynı)."""
    q, b = p, kemik
    while b is not None:
        bas, _, ebe = KEMIKLER[b]
        q = add(mat_vec(R[b], sub(q, bas)), bas)
        if ebe is None:
            q = add(q, kay)
        b = ebe
    return q


def poz(klip, t):
    """t (0–1) anındaki poz: kemik → dönüş matrisi, kalça kayması. İleri adım = X ekseni etrafında eksi açı."""
    d = math.radians
    phi = 2 * math.pi * t
    R = {b: rot_x(0.0) for b in KEMIKLER}
    if klip == "basic walk":
        A, K, B = d(27), d(42), d(24)
        s, c = math.sin(phi), math.cos(phi)
        R["LeftUpLeg"] = rot_x(-A * s)
        R["RightUpLeg"] = rot_x(A * s)
        R["LeftLeg"] = rot_x(d(4) + K * max(0.0, c))
        R["RightLeg"] = rot_x(d(4) + K * max(0.0, -c))
        R["LeftArm"] = rot_x(B * s)
        R["RightArm"] = rot_x(-B * s)
        R["LeftForeArm"] = rot_x(-d(16) - d(10) * max(0.0, -s))
        R["RightForeArm"] = rot_x(-d(16) - d(10) * max(0.0, s))
        R["Hips"] = rot_z(d(4) * s)
        R["Spine"] = mat_mul(rot_z(-d(6) * s), rot_x(d(4)))
        R["Head"] = rot_x(-d(3))
    elif klip == "pedal":
        # Selede oturur: uyluklar öne, dizler bükük, pedal dairesi; kollar gidona uzanır, gövde öne eğik.
        # Kalça yeri değişmez: oyun kuryeyi bisikletin selesine kendisi oturtur.
        s, c = math.sin(phi), math.cos(phi)
        R["LeftUpLeg"] = rot_x(-d(68) - d(22) * s)
        R["RightUpLeg"] = rot_x(-d(68) + d(22) * s)
        R["LeftLeg"] = rot_x(d(78) + d(24) * c)
        R["RightLeg"] = rot_x(d(78) - d(24) * c)
        R["Spine"] = rot_x(d(18) + d(1.5) * math.sin(2 * phi))
        R["LeftArm"] = rot_x(-d(52))
        R["RightArm"] = rot_x(-d(52))
        R["LeftForeArm"] = rot_x(-d(12))
        R["RightForeArm"] = rot_x(-d(12))
        R["Head"] = rot_x(-d(14))
        return R, (0.0, 0.0, 0.0)
    else:   # idle: nefes alma, hafif sallanma, etrafa bakma
        s = math.sin(phi)
        R["Spine"] = rot_x(d(1.5) * s)
        R["LeftArm"] = rot_x(d(2.5) * s)
        R["RightArm"] = rot_x(d(2.5) * math.sin(phi + 0.6))
        R["LeftForeArm"] = rot_x(-d(8))
        R["RightForeArm"] = rot_x(-d(8))
        R["Head"] = rot_z(d(7) * math.sin(phi + 1.0))
    # Kalça yüksekliği: yere basan ayak tam zemine değsin
    taban = []
    for b, x in (("LeftLeg", 0.1), ("RightLeg", -0.1)):
        for y in (0.085, -0.155):
            taban.append(fk_nokta(R, (0.0, 0.0, 0.0), b, (x, y, 0.0))[2])
    return R, (0.0, 0.0, -min(taban))


# ------------------------------------------------------------------ 2a. bisiklet

def bisiklet():
    g = Govde("Bisiklet")
    k = 0.6                                                    # ince borularda daha ince çizgi
    arka, on, orta = (0.0, 0.52, 0.34), (0.0, -0.53, 0.34), (0.0, 0.06, 0.29)
    sele_alt, gidon_alt, gidon_ust = (0.0, 0.16, 0.78), (0.0, -0.38, 0.62), (0.0, -0.42, 0.86)
    boru = lambda a, b, r=0.022, renk=SR: silindir(g, a, b, r, r, renk, n=8, kalinlik=k)
    boru(arka, orta)                                           # zincir kolu
    boru(orta, sele_alt, 0.026)                                # sele borusu
    boru((0.0, 0.15, 0.75), (0.0, -0.39, 0.80), 0.024)         # üst boru
    boru(orta, gidon_alt, 0.028)                               # alt boru
    boru(arka, (0.0, 0.15, 0.73), 0.018)                       # sele kolu
    silindir(g, gidon_alt, gidon_ust, 0.034, 0.034, SR, n=8, kalinlik=k)              # baş borusu
    for s in (1, -1):                                          # çatal
        silindir(g, (0.045 * s, -0.39, 0.62), (0.045 * s, -0.53, 0.34), 0.016, 0.014, SR, n=6, kalinlik=k)
    silindir(g, (-0.05, -0.39, 0.62), (0.05, -0.39, 0.62), 0.02, 0.02, SR, n=6, kalinlik=k)
    # Gidon, elcikler, sele
    silindir(g, gidon_ust, (0.0, -0.40, 0.98), 0.018, 0.018, "metal_koyu", n=6, kalinlik=k)
    silindir(g, (-0.27, -0.38, 0.98), (0.27, -0.38, 0.98), 0.015, 0.015, "metal_koyu", n=6, kalinlik=k)
    for s in (1, -1):
        kapsul(g, (0.2 * s, -0.38, 0.98), (0.29 * s, -0.38, 0.98), 0.022, "lastik", n=6, k=1, kalinlik=k)
    silindir(g, sele_alt, (0.0, 0.19, 0.88), 0.014, 0.014, "metal", n=6, kalinlik=k)
    pah_kutu(g, (0.0, 0.17, 0.9), (0.075, 0.13, 0.028), 0.022, "sele", kalinlik=k)
    # Çamurluklar (krem) ve ön lamba
    yay(g, arka, 0.37, 0.022, "krem", math.radians(15), math.radians(165), n=10, m=4, kalinlik=k)
    yay(g, on, 0.37, 0.022, "krem", math.radians(20), math.radians(150), n=9, m=4, kalinlik=k)
    pah_kutu(g, (0.0, -0.46, 0.9), (0.04, 0.035, 0.035), 0.015, "krem", kalinlik=k)
    pah_kutu(g, (0.0, -0.497, 0.9), (0.028, 0.004, 0.025), 0.004, ISIK, kabuk=False)
    # Arka bagaj ve teslimat kutusu (şirket renginde)
    for s in (1, -1):
        silindir(g, (0.07 * s, 0.6, 0.36), (0.07 * s, 0.52, 0.76), 0.012, 0.012, "metal_koyu", n=6, kabuk=False)
    pah_kutu(g, (0.0, 0.5, 0.775), (0.12, 0.17, 0.012), 0.008, "metal_koyu", kabuk=False)
    pah_kutu(g, (0.0, 0.5, 0.95), (0.18, 0.19, 0.16), 0.05, SR)
    pah_kutu(g, (0.0, 0.5, 1.115), (0.185, 0.195, 0.022), 0.015, SR2)
    pah_kutu(g, (0.0, 0.692, 0.95), (0.07, 0.005, 0.07), 0.005, SR2, kabuk=False)            # arkada logo yeri
    # Tekerler ve pedal ayrı obje (pivot: göbek ve orta göbek)
    for ad, p in (("Teker_On", on), ("Teker_Arka", arka)):
        t = Govde(ad)
        yay(t, (0.0, 0.0, 0.0), 0.305, 0.04, "lastik", n=16, m=6, kalinlik=k)
        yay(t, (0.0, 0.0, 0.0), 0.262, 0.014, "metal", n=16, m=4, kabuk=False)
        silindir(t, (-0.045, 0.0, 0.0), (0.045, 0.0, 0.0), 0.03, 0.03, "metal_koyu", n=8, kalinlik=k)
        for i in range(4):
            a = math.pi * i / 4
            silindir(t, (0.0, 0.255 * math.cos(a), 0.255 * math.sin(a)), (0.0, -0.255 * math.cos(a), -0.255 * math.sin(a)),
                     0.005, 0.005, "metal", n=4, kabuk=False)
        g.parcalar.append((ad, t, p))
    p = Govde("Pedal")
    silindir(p, (0.04, 0.0, 0.0), (0.055, 0.0, 0.0), 0.09, 0.09, "metal", n=12, kalinlik=k)   # aynalı dişli
    for s in (1, -1):
        silindir(p, (0.075 * s, 0.0, 0.0), (0.075 * s, 0.0, -0.16 * s), 0.013, 0.013, "metal_koyu", n=6, kabuk=False)
        pah_kutu(p, (0.115 * s, 0.0, -0.16 * s), (0.045, 0.03, 0.01), 0.006, "lastik", kalinlik=k)
    g.parcalar.append(("Pedal", p, orta))
    return g


# ------------------------------------------------------------------ 2b. paket dolabı

def paket_dolabi():
    g = Govde("PaketDolabi")
    k = 1.6
    pah_kutu(g, (0.0, 0.0, 0.06), (0.92, 0.31, 0.06), 0.02, "metal_koyu", kalinlik=k)          # kaide
    pah_kutu(g, (0.0, 0.02, 1.02), (0.9, 0.28, 0.9), 0.04, "krem_koyu", yumusak=False, kalinlik=k)
    for s in (1, -1):                                                                          # yan paneller
        pah_kutu(g, (0.905 * s, 0.02, 1.02), (0.03, 0.3, 0.92), 0.02, SR, kalinlik=k)
    pah_kutu(g, (0.0, 0.02, 2.0), (0.95, 0.33, 0.08), 0.03, SR, kalinlik=k)                    # başlık
    pah_kutu(g, (0.0, -0.315, 2.0), (0.18, 0.012, 0.05), 0.01, SR2, kabuk=False)               # logo yeri
    pah_kutu(g, (0.0, -0.02, 2.11), (0.99, 0.4, 0.03), 0.02, "krem", kalinlik=k)               # saçak
    y = -0.272
    sutunlar = [                       # (x0, x1, kapak yükseklikleri alttan üste; None: ekran paneli)
        (-0.86, -0.45, [0.56, 0.37, 0.37, 0.36]),
        (-0.43, -0.02, [0.37, 0.37, 0.28, 0.28, 0.34]),
        (0.0, 0.41, [0.56, None, 0.34]),
        (0.43, 0.86, [0.28, 0.28, 0.37, 0.37, 0.34]),
    ]
    for x0, x1, hs in sutunlar:
        bos = 1.72 - sum(h for h in hs if h is not None) - 0.02 * (len(hs) - 1)   # ekran panelinin yüksekliği
        z = 0.16
        for h in hs:
            hh = h if h is not None else bos
            cx, cz = (x0 + x1) / 2, z + hh / 2
            if h is None:
                pah_kutu(g, (cx, y + 0.004, cz), ((x1 - x0) / 2, 0.01, hh / 2), 0.008, "krem", kabuk=False)
                pah_kutu(g, (cx, y - 0.008, cz + 0.17), (0.17, 0.012, 0.14), 0.01, "metal_koyu", kabuk=False)
                pah_kutu(g, (cx, y - 0.02, cz + 0.17), (0.15, 0.004, 0.12), 0.006, "ekran", kabuk=False)
                for i in range(3):                                                       # tuş takımı
                    for j in range(3):
                        pah_kutu(g, (cx - 0.07 + i * 0.07, y - 0.012, cz - 0.07 - j * 0.06), (0.025, 0.006, 0.02),
                                 0.005, "metal", kabuk=False)
                pah_kutu(g, (cx, y - 0.01, cz - 0.27), (0.12, 0.006, 0.012), 0.004, "metal_koyu", kabuk=False)   # kargo ağzı
            else:
                pah_kutu(g, (cx, y, cz), ((x1 - x0) / 2 - 0.006, 0.012, hh / 2 - 0.006), 0.01, "krem", kabuk=False)
                pah_kutu(g, (x1 - 0.05, y - 0.016, cz), (0.008, 0.006, min(0.06, hh * 0.18)), 0.004, "metal_koyu", kabuk=False)
                pah_kutu(g, (x1 - 0.05, y - 0.016, cz + hh / 2 - 0.04), (0.008, 0.004, 0.008), 0.003, "yesil_isik", kabuk=False)
            z += hh + 0.02
    return g


# ------------------------------------------------------------------ 3. ağaç ve sokak lambası

def agac_yuvarlak(rnd):
    g = Govde("Agac_Yuvarlak")
    k = 1.7
    silindir(g, (0.0, 0.0, 0.0), (0.0, 0.0, 0.035), 0.55, 0.5, "toprak", n=10, kabuk=False)
    silindir(g, (0.0, 0.0, 0.0), (0.0, 0.0, 0.2), 0.22, 0.14, "kabuk", n=8, kalinlik=k)
    silindir(g, (0.0, 0.0, 0.18), (0.0, 0.0, 1.6), 0.14, 0.08, "kabuk", n=8, kalinlik=k)
    silindir(g, (0.0, 0.0, 1.1), (0.38, 0.05, 1.62), 0.06, 0.035, "kabuk", n=6, kalinlik=k)
    silindir(g, (0.0, 0.0, 1.25), (-0.3, -0.1, 1.75), 0.05, 0.03, "kabuk", n=6, kalinlik=k)
    for c, r, renk in (((0.0, 0.0, 2.25), (0.95, 0.9, 0.8), "yaprak1"),
                       ((0.55, 0.15, 1.95), (0.6, 0.55, 0.5), "yaprak2"),
                       ((-0.5, -0.2, 2.0), (0.62, 0.58, 0.52), "yaprak2"),
                       ((0.1, -0.05, 2.85), (0.58, 0.55, 0.48), "yaprak3")):
        top(g, c, r, renk, n=10, m=7, puruz=0.07, rnd=rnd, kalinlik=k)
    return g


def agac_kavak(rnd):
    g = Govde("Agac_Kavak")
    k = 1.7
    silindir(g, (0.0, 0.0, 0.0), (0.0, 0.0, 0.035), 0.42, 0.38, "toprak", n=10, kabuk=False)
    silindir(g, (0.0, 0.0, 0.0), (0.0, 0.0, 1.25), 0.11, 0.07, "kabuk", n=8, kalinlik=k)
    for c, r, renk in (((0.0, 0.0, 1.75), (0.62, 0.6, 0.75), "yaprak_koyu"),
                       ((0.0, 0.0, 2.45), (0.5, 0.48, 0.62), "yaprak2"),
                       ((0.0, 0.0, 3.05), (0.34, 0.32, 0.45), "yaprak1")):
        top(g, c, r, renk, n=10, m=7, puruz=0.06, rnd=rnd, kalinlik=k)
    return g


def sokak_lambasi(rnd):
    g = Govde("SokakLambasi")
    k = 1.2
    pah_kutu(g, (0.0, 0.0, 0.12), (0.17, 0.17, 0.12), 0.04, "direk", kalinlik=k)
    silindir(g, (0.0, 0.0, 0.24), (0.0, 0.0, 0.34), 0.11, 0.08, "direk_acik", n=8, kalinlik=k)
    silindir(g, (0.0, 0.0, 0.34), (0.0, 0.0, 3.2), 0.065, 0.05, "direk", n=8, kalinlik=k)
    for z, r in ((1.0, 0.08), (3.0, 0.07)):                                                     # süs halkaları
        silindir(g, (0.0, 0.0, z), (0.0, 0.0, z + 0.06), r, r, "direk_acik", n=8, kabuk=False)
    top(g, (0.0, 0.0, 3.24), (0.065, 0.065, 0.07), "direk_acik", n=8, m=5, kalinlik=k)
    # Kıvrık kol (ikinci derece Bezier)
    P0, P1, P2 = (0.0, 0.0, 3.12), (0.0, -0.18, 3.42), (0.0, -0.62, 3.38)
    nokta = lambda t: add(add(mul(P0, (1 - t) ** 2), mul(P1, 2 * (1 - t) * t)), mul(P2, t * t))
    for i in range(5):
        silindir(g, nokta(i / 5), nokta((i + 1) / 5), 0.03, 0.03, "direk", n=6, kalinlik=k * 0.7)
    # Lamba başı: askı, şapka; cam ayrı obje ("Isik": oyun gece yakar)
    silindir(g, (0.0, -0.62, 3.38), (0.0, -0.62, 3.27), 0.015, 0.015, "direk", n=6, kabuk=False)
    silindir(g, (0.0, -0.62, 3.28), (0.0, -0.62, 3.12), 0.07, 0.22, "direk", n=10, kalinlik=k)
    silindir(g, (0.0, -0.62, 3.13), (0.0, -0.62, 3.10), 0.225, 0.225, "direk_acik", n=10, kabuk=False)
    isik = Govde("Isik")
    top(isik, (0.0, 0.0, 0.0), (0.13, 0.13, 0.08), ISIK, n=10, m=5, kabuk=False)
    g.parcalar.append(("Isik", isik, (0.0, -0.62, 3.1)))
    # Asma çiçek sepeti (sıcak şehir dokunuşu)
    silindir(g, (0.0, 0.0, 2.32), (0.0, -0.22, 2.32), 0.014, 0.014, "direk", n=6, kabuk=False)
    silindir(g, (0.0, -0.22, 2.32), (0.0, -0.22, 2.24), 0.008, 0.008, "direk", n=4, kabuk=False)
    silindir(g, (0.0, -0.22, 2.08), (0.0, -0.22, 2.22), 0.08, 0.13, "saksi", n=10, kalinlik=k)
    for (dx, dy), renk in (((0.0, 0.0), "yaprak2"), ((0.07, -0.03), "cicek1"), ((-0.06, 0.04), "cicek2"),
                           ((0.03, 0.07), "cicek1"), ((-0.05, -0.06), "yaprak1")):
        top(g, (dx, -0.22 + dy, 2.27), (0.07, 0.07, 0.06), renk, n=8, m=5, puruz=0.08, rnd=rnd, kalinlik=k)
    return g


# ------------------------------------------------------------------ 2c. kargo bisikleti (geç oyun)

def kargo_bisikleti():
    """Önde büyük yük kasalı "long john" kargo bisikleti. Ölçü gerçek (metre): Polo (3,75 m) ile aynı ölçekte ~2,5 m."""
    g = Govde("KargoBisikleti")
    k = 0.6
    arka, on = (0.0, 0.62, 0.33), (0.0, -1.28, 0.25)          # arka 26", ön 20" teker
    orta = (0.0, 0.18, 0.3)
    sele_alt, gidon_alt, gidon_ust = (0.0, 0.26, 0.8), (0.0, -0.22, 0.66), (0.0, -0.26, 0.92)
    boru = lambda a, b, r=0.024, renk=SR: silindir(g, a, b, r, r, renk, n=8, kalinlik=k)
    boru(arka, orta)
    boru(orta, sele_alt, 0.028)
    boru((0.0, 0.25, 0.77), (0.0, -0.22, 0.8), 0.026)          # üst boru
    boru(orta, gidon_alt, 0.03)
    boru(arka, (0.0, 0.25, 0.75), 0.02)
    # Uzun alt şasi: orta göbekten öne, kasanın altından ön çatala (iki paralel boru)
    for s in (1, -1):
        boru((0.11 * s, 0.12, 0.26), (0.11 * s, -1.05, 0.2), 0.022, "metal_koyu")
    boru((0.0, 0.12, 0.26), (0.0, 0.18, 0.3), 0.024, "metal_koyu")
    silindir(g, (-0.12, -1.06, 0.2), (0.12, -1.06, 0.2), 0.022, 0.022, "metal_koyu", n=6, kalinlik=k)
    silindir(g, (0.0, -1.08, 0.2), (0.0, -1.16, 0.56), 0.03, 0.03, "metal_koyu", n=8, kalinlik=k)   # ön baş borusu
    for s in (1, -1):                                                                               # ön çatal
        silindir(g, (0.04 * s, -1.16, 0.52), (0.04 * s, -1.28, 0.25), 0.015, 0.013, "metal_koyu", n=6, kalinlik=k)
    silindir(g, gidon_alt, gidon_ust, 0.034, 0.034, SR, n=8, kalinlik=k)                           # arka baş borusu
    silindir(g, (0.0, -0.24, 0.45), (0.0, -1.1, 0.5), 0.009, 0.009, "metal", n=4, kabuk=False)     # direksiyon çubuğu
    # Gidon (geniş, geriye kıvrık), sele
    silindir(g, gidon_ust, (0.0, -0.24, 1.0), 0.018, 0.018, "metal_koyu", n=6, kalinlik=k)
    silindir(g, (-0.3, -0.18, 1.0), (0.3, -0.18, 1.0), 0.015, 0.015, "metal_koyu", n=6, kalinlik=k)
    for s in (1, -1):
        kapsul(g, (0.24 * s, -0.17, 1.0), (0.32 * s, -0.12, 1.0), 0.022, "lastik", n=6, k=1, kalinlik=k)
    silindir(g, sele_alt, (0.0, 0.29, 0.9), 0.014, 0.014, "metal", n=6, kalinlik=k)
    pah_kutu(g, (0.0, 0.28, 0.92), (0.08, 0.14, 0.03), 0.022, "sele", kalinlik=k)
    # Yük kasası (şirket rengi), kapak (ikinci renk), logo yeri, tahta taban, yan tutamak
    pah_kutu(g, (0.0, -0.66, 0.27), (0.3, 0.36, 0.025), 0.01, "tahta", kalinlik=k)
    pah_kutu(g, (0.0, -0.66, 0.53), (0.31, 0.37, 0.24), 0.05, SR, kalinlik=k)
    pah_kutu(g, (0.0, -0.66, 0.79), (0.32, 0.38, 0.03), 0.02, SR2, kalinlik=k)
    pah_kutu(g, (0.0, -1.034, 0.55), (0.16, 0.005, 0.1), 0.005, SR2, kabuk=False)                   # önde logo yeri
    for s in (1, -1):
        pah_kutu(g, (0.315 * s, -0.66, 0.55), (0.005, 0.2, 0.09), 0.004, SR2, kabuk=False)          # yanlarda logo yeri
    # Çamurluk, ön lamba, ayaklık (park ayağı)
    yay(g, arka, 0.4, 0.022, "krem", math.radians(15), math.radians(165), n=10, m=4, kalinlik=k)
    yay(g, on, 0.29, 0.02, "krem", math.radians(25), math.radians(150), n=8, m=4, kalinlik=k)
    pah_kutu(g, (0.0, -1.06, 0.36), (0.04, 0.03, 0.03), 0.012, "krem", kalinlik=k)
    pah_kutu(g, (0.0, -1.092, 0.36), (0.028, 0.004, 0.022), 0.004, ISIK, kabuk=False)
    for s in (1, -1):
        silindir(g, (0.12 * s, -0.66, 0.2), (0.2 * s, -0.66, 0.02), 0.013, 0.013, "metal_koyu", n=6, kabuk=False)
    # Tekerler ve pedal ayrı obje (bisikletle aynı adlar: oyun aynı şekilde döndürür)
    for ad, p, R in (("Teker_On", on, 0.23), ("Teker_Arka", arka, 0.31)):
        t = Govde(ad)
        yay(t, (0.0, 0.0, 0.0), R, 0.042, "lastik", n=16, m=6, kalinlik=k)
        yay(t, (0.0, 0.0, 0.0), R - 0.045, 0.014, "metal", n=16, m=4, kabuk=False)
        silindir(t, (-0.05, 0.0, 0.0), (0.05, 0.0, 0.0), 0.032, 0.032, "metal_koyu", n=8, kalinlik=k)
        for i in range(4):
            a = math.pi * i / 4
            rr = R - 0.05
            silindir(t, (0.0, rr * math.cos(a), rr * math.sin(a)), (0.0, -rr * math.cos(a), -rr * math.sin(a)),
                     0.005, 0.005, "metal", n=4, kabuk=False)
        g.parcalar.append((ad, t, p))
    p = Govde("Pedal")
    silindir(p, (0.04, 0.0, 0.0), (0.055, 0.0, 0.0), 0.1, 0.1, "metal", n=12, kalinlik=k)
    for s in (1, -1):
        silindir(p, (0.075 * s, 0.0, 0.0), (0.075 * s, 0.0, -0.17 * s), 0.013, 0.013, "metal_koyu", n=6, kabuk=False)
        pah_kutu(p, (0.115 * s, 0.0, -0.17 * s), (0.045, 0.03, 0.01), 0.006, "lastik", kalinlik=k)
    g.parcalar.append(("Pedal", p, orta))
    return g


# ------------------------------------------------------------------ 4. Budapeşte tarzı binalar
# Arsa 14 m (City3D'de bir kare ≈ 0,97 ölçekle): taban en fazla ~13,6 × 13 m, önü −Y, taban z = 0, arsa levhası yok.
# Pencere camları "Cam", vitrinler "Vitrin" düz malzemesinde: gündüz cam rengi, gece oyun (GeceIsiklari) yakar.

def kutu(g, c, h, renk, kabuk=False, kalinlik=1.0):
    """Pahsız düz kutu (12 üçgen): pencere, bant, kapı gibi çok tekrar eden küçük ayrıntılar için."""
    pts = [(c[0] + sx * h[0], c[1] + sy * h[1], c[2] + sz * h[2]) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    F = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]]
    parca(g, pts, F, renk, False, kabuk, kalinlik)


def kesik_piramit(g, x0, x1, y0, y1, z, iceri, h, renk, kalinlik=1.0):
    """Alt dikdörtgenden 'iceri' kadar daralan üst dikdörtgene (h yukarıda) kapalı gövde: mansart çatının dik eteği."""
    a = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
    b = [(x0 + iceri, y0 + iceri, z + h), (x1 - iceri, y0 + iceri, z + h), (x1 - iceri, y1 - iceri, z + h),
         (x0 + iceri, y1 - iceri, z + h)]
    F = [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]]
    parca(g, a + b, F, renk, False, True, kalinlik)


def cati_kirma(g, x0, x1, y0, y1, z, h, renk, kalinlik=1.0):
    """Kırma çatı (dört yana eğimli): mahya uzun kenar boyunca; kare tabanda piramit."""
    w, d = x1 - x0, y1 - y0
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    taban = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)]
    r = abs(w - d) / 2
    if r < 0.05:
        parca(g, taban + [(cx, cy, z + h)], [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4], [0, 3, 2, 1]], renk, False, True, kalinlik)
        return
    if w > d:
        a, b = (cx - r, cy, z + h), (cx + r, cy, z + h)
        F = [[0, 1, 5, 4], [1, 2, 5], [2, 3, 4, 5], [3, 0, 4], [0, 3, 2, 1]]
    else:
        a, b = (cx, cy - r, z + h), (cx, cy + r, z + h)
        F = [[0, 1, 4], [1, 2, 5, 4], [2, 3, 5], [3, 0, 4, 5], [0, 3, 2, 1]]
    parca(g, taban + [a, b], F, renk, False, True, kalinlik)


def cati_besik(g, x0, x1, y0, y1, z, h, renk, alin, kalinlik=1.0):
    """Beşik çatı, mahya sokağa paralel (X boyunca): eğimler çatı renginde, iki yan üçgen (alınlık) cephe renginde."""
    cy = (y0 + y1) / 2
    p = [(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z), (x0, cy, z + h), (x1, cy, z + h)]
    parca(g, p, [[0, 1, 5, 4], [2, 3, 4, 5]], renk, False, True, kalinlik)
    parca(g, p, [[1, 2, 5], [3, 0, 4], [0, 3, 2, 1]], alin, False, True, kalinlik)


def baca(g, x, y, z, h, kalinlik=1.0):
    pah_kutu(g, (x, y, z + h / 2), (0.32, 0.32, h / 2), 0.04, "baca", kalinlik=kalinlik)
    kutu(g, (x, y, z + h + 0.06), (0.38, 0.38, 0.06), "tas_koyu")


def pencere(g, x, yf, z, w, h, yon=1, cerceve="beyaz", cam="Cam", denizlik=True, panjur=None):
    """
    Cepheye pencere: çerçeve plakası, önünde biraz küçük cam, altında denizlik (12'şer üçgen, dış çizgisiz).
    yon=1: ön cephe (yf: cephenin y'si, dışarısı −Y). yon=0: yan cephe (yf: cephenin x'i, dışarısı işaretine göre).
    """
    def kut(dx, dd, dz, hx, hd, hz, renk):
        if yon == 1:
            kutu(g, (x + dx, yf - dd, z + dz), (hx, hd, hz), renk)
        else:
            s = 1.0 if yf > 0 else -1.0
            kutu(g, (yf + s * dd, x + dx, z + dz), (hd, hx, hz), renk)
    kut(0.0, 0.03, 0.0, w / 2 + 0.1, 0.03, h / 2 + 0.1, cerceve)
    kut(0.0, 0.065, 0.0, w / 2, 0.012, h / 2, cam)
    if denizlik:
        kut(0.0, 0.08, -h / 2 - 0.13, w / 2 + 0.16, 0.08, 0.045, "tas")
    if panjur:
        for s in (-1, 1):
            kut(s * (w / 2 + 0.32), 0.05, 0.0, 0.2, 0.025, h / 2 + 0.04, panjur)


def bant(g, x0, x1, yf, z, kalinlik_z, cikinti, renk="tas"):
    """Ön cephede yatay korniş / kat bandı."""
    kutu(g, ((x0 + x1) / 2, yf - cikinti / 2, z), ((x1 - x0) / 2 + cikinti, cikinti / 2, kalinlik_z / 2), renk)


def yan_bant(g, y0, y1, xf, z, kalinlik_z, cikinti, renk="tas"):
    s = 1.0 if xf > 0 else -1.0
    kutu(g, (xf + s * cikinti / 2, (y0 + y1) / 2, z), (cikinti / 2, (y1 - y0) / 2 + cikinti, kalinlik_z / 2), renk)


def tente(g, x0, x1, yf, z, derinlik=0.95, dusus=0.45, renkler=("tente1", "beyaz"), dilim=6):
    """Çizgili tente: duvardan çıkan eğik kama, dilim dilim iki renk."""
    w = (x1 - x0) / dilim
    for i in range(dilim):
        a, b = x0 + i * w, x0 + (i + 1) * w
        p = [(a, yf, z), (b, yf, z), (b, yf - derinlik, z - dusus), (a, yf - derinlik, z - dusus),
             (a, yf, z - 0.12), (b, yf, z - 0.12)]
        parca(g, p, [[0, 1, 2, 3], [3, 2, 5, 4], [0, 3, 4], [1, 5, 2], [0, 4, 5, 1]], renkler[i % 2], False, i in (0, dilim - 1), 1.5)
    kutu(g, ((x0 + x1) / 2, yf - derinlik - 0.01, z - dusus - 0.09), ((x1 - x0) / 2, 0.012, 0.09), renkler[0])   # saçak


def korkuluk(g, x0, x1, yf, z, derinlik, renk="demir"):
    """Balkon: taş döşeme ve demir korkuluk (üst tırabzan + dikmeler)."""
    kutu(g, ((x0 + x1) / 2, yf - derinlik / 2, z - 0.09), ((x1 - x0) / 2, derinlik / 2, 0.09), "tas", kabuk=True, kalinlik=2.0)
    yk = yf - derinlik + 0.04
    kutu(g, ((x0 + x1) / 2, yk, z + 0.95), ((x1 - x0) / 2, 0.025, 0.025), renk)
    for s in (-1, 1):
        kutu(g, (x0 if s < 0 else x1, yf - derinlik / 2, z + 0.95), (0.025, derinlik / 2, 0.025), renk)
    n = max(2, int((x1 - x0) / 0.28))
    for i in range(n + 1):
        kutu(g, (x0 + (x1 - x0) * i / n, yk, z + 0.47), (0.012, 0.012, 0.47), renk)


def kapi(g, x, yf, w, h, renk="kapi", ust_cam=True):
    kutu(g, (x, yf - 0.04, h / 2 + 0.05), (w / 2 + 0.14, 0.04, h / 2 + 0.12), "tas")
    kutu(g, (x, yf - 0.07, h / 2), (w / 2, 0.03, h / 2), renk)
    if ust_cam:
        kutu(g, (x, yf - 0.1, h - 0.28), (w / 2 - 0.08, 0.012, 0.18), "Cam")
    kutu(g, (x + w * 0.32, yf - 0.11, h * 0.48), (0.03, 0.02, 0.09), "metal")


def cati_penceresi(g, x, y_on, z_alt, renk_cati, yukseklik=1.2):
    """
    Çatı eğiminden çıkan küçük çatı penceresi (dormer): dikey ön yüzü y_on'da, alt kenarı z_alt'ta (çatı yüzeyinin
    o noktadaki yüksekliğinin biraz altında seçilmeli ki cam çatının içinde kalmasın), geri doğru çatının içine gömülür.
    """
    kutu(g, (x, y_on + 0.8, z_alt + yukseklik / 2), (0.55, 0.8, yukseklik / 2), "beyaz", kabuk=True, kalinlik=2.0)
    kutu(g, (x, y_on - 0.01, z_alt + yukseklik / 2 + 0.05), (0.34, 0.012, yukseklik / 2 - 0.22), "Cam")
    cati_kirma(g, x - 0.68, x + 0.68, y_on - 0.12, y_on + 1.7, z_alt + yukseklik, 0.42, renk_cati, 2.0)


def bina_berhaz():
    """Klasik bérház (kiralık apartman): okra cephe, altta taş zemin katta iki dükkân ve tenteler, üstte 3 kat, kiremit kırma çatı."""
    g = Govde("Bina_Berhaz")
    k = 4.0
    W, D, ZK, KAT, NK = 13.0, 12.0, 4.2, 3.4, 3
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    H = ZK + KAT * NK
    pah_kutu(g, (0.0, 0.0, ZK / 2), (W / 2, D / 2, ZK / 2), 0.08, "tas", yumusak=False, kalinlik=k)       # taş zemin kat
    pah_kutu(g, (0.0, 0.0, ZK + KAT * NK / 2), (W / 2 - 0.05, D / 2 - 0.05, KAT * NK / 2), 0.08, "okra", yumusak=False, kalinlik=k)
    bant(g, x0, x1, y0, 0.3, 0.6, 0.06, "tas_koyu")                                                       # su basmanı
    bant(g, x0, x1, y0, ZK, 0.32, 0.16, "krem")
    for i in range(1, NK):
        bant(g, x0, x1, y0, ZK + KAT * i, 0.16, 0.08, "okra_acik")
    bant(g, x0 - 0.1, x1 + 0.1, y0, H + 0.12, 0.38, 0.42, "krem")                                          # ana korniş
    for xs in (x0, x1):
        yan_bant(g, y0, y1, xs, ZK, 0.32, 0.16, "krem")
        yan_bant(g, y0, y1, xs, H + 0.12, 0.38, 0.42, "krem")
    # Zemin kat: iki vitrin + ortada bina kapısı, vitrinlerin üstünde çizgili tente ve tabela
    for cx in (-3.6, 3.6):
        kutu(g, (cx, y0 - 0.03, 1.9), (2.0, 0.03, 1.35), "tahta")
        kutu(g, (cx, y0 - 0.07, 1.85), (1.85, 0.012, 1.2), "Vitrin")
        kutu(g, (cx, y0 - 0.06, 3.55), (2.0, 0.05, 0.28), "krem")
        tente(g, cx - 2.05, cx + 2.05, y0, 3.2, renkler=("tente1", "beyaz") if cx < 0 else ("tente2", "beyaz"))
    kapi(g, 0.0, y0, 1.5, 3.0)
    # Üst katlar: önde 5, yanlarda 3 pencere; orta pencerede küçük balkon
    for i in range(NK):
        zc = ZK + KAT * i + 1.65
        for j, cx in enumerate((-4.8, -2.4, 0.0, 2.4, 4.8)):
            pencere(g, cx, y0, zc, 1.15, 1.75)
        for cy in (-3.6, 0.0, 3.6):
            pencere(g, cy, x0, zc, 1.1, 1.7, yon=0)
            pencere(g, cy, x1, zc, 1.1, 1.7, yon=0)
    korkuluk(g, -0.95, 0.95, y0, ZK + KAT * 1 + 0.12, 0.7)
    # Çatı: kiremit kırma çatı, iki çatı penceresi, iki baca
    cati_kirma(g, x0 - 0.35, x1 + 0.35, y0 - 0.35, y1 + 0.35, H + 0.3, 4.2, "kiremit", k)
    egim = 4.2 / (D / 2 + 0.35)
    for cx in (-3.0, 3.0):                                       # ön yüzü çatı saçağından 0,95 m içeride
        cati_penceresi(g, cx, y0 + 0.6, H + 0.3 + 0.95 * egim - 0.12, "kiremit_koyu")
    baca(g, -2.2, 2.6, H + 2.6, 2.0, 2.0)
    baca(g, 3.6, 1.2, H + 3.3, 1.6, 2.0)
    return g


def bina_kose():
    """Köşe binası: pastel pembe, sol önde yuvarlak kule ve bakır (yeşil patina) külah, demir balkonlar."""
    g = Govde("Bina_Kose")
    k = 4.0
    W, D, ZK, KAT, NK = 12.4, 12.4, 4.0, 3.4, 2
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    H = ZK + KAT * NK
    pah_kutu(g, (0.0, 0.0, ZK / 2), (W / 2, D / 2, ZK / 2), 0.08, "pembe", yumusak=False, kalinlik=k)
    pah_kutu(g, (0.0, 0.0, ZK + KAT * NK / 2), (W / 2 - 0.04, D / 2 - 0.04, KAT * NK / 2), 0.08, "pembe_acik", yumusak=False, kalinlik=k)
    bant(g, x0, x1, y0, 0.3, 0.6, 0.06, "tas_koyu")
    yan_bant(g, y0, y1, x0, 0.3, 0.6, 0.06, "tas_koyu")
    for z, t, c in ((ZK, 0.28, 0.14), (ZK + KAT, 0.14, 0.07), (H + 0.12, 0.36, 0.38)):
        bant(g, x0, x1, y0, z, t, c, "beyaz")
        yan_bant(g, y0, y1, x0, z, t, c, "beyaz")
        yan_bant(g, y0, y1, x1, z, t, c, "beyaz")
    # Köşe kulesi: bir kat yüksek, silindir gövde, bakır külah ve tepe süsü
    kx, ky, kr = x0 + 0.6, y0 + 0.6, 2.3
    silindir(g, (kx, ky, 0.0), (kx, ky, H + KAT), kr, kr, "pembe", n=16, yumusak=True, kalinlik=k)
    for z in (ZK, H, H + KAT):
        silindir(g, (kx, ky, z - 0.14), (kx, ky, z + 0.14), kr + 0.12, kr + 0.12, "beyaz", n=16, kabuk=False)
    silindir(g, (kx, ky, H + KAT + 0.1), (kx, ky, H + KAT + 3.4), kr + 0.3, 0.12, "bakir", n=16, kalinlik=k)
    silindir(g, (kx, ky, H + KAT + 3.3), (kx, ky, H + KAT + 4.1), 0.06, 0.03, "demir", n=6, kabuk=False)
    top(g, (kx, ky, H + KAT + 3.55), (0.16, 0.16, 0.16), "cicek2", n=8, m=5, kabuk=False)
    for kat in range(NK + 2):
        zc = (1.9 if kat == 0 else ZK + KAT * (kat - 1) + 1.65)
        for t in (0.25, 0.785, 1.32):              # kulenin sokağa bakan çeyreğinde (−X ile −Y arası) üç pencere
            a = math.pi + t
            px, py = kx + (kr + 0.01) * math.cos(a), ky + (kr + 0.01) * math.sin(a)
            pah_kutu(g, (px, py, zc), (0.05, 0.4, 0.75 if kat else 1.0), 0.01, "Cam", kabuk=False, aci=a)
    # Zemin kat: önde kafe vitrini + tente, yanda kapı
    kutu(g, (2.2, y0 - 0.03, 1.9), (2.6, 0.03, 1.35), "tahta")
    kutu(g, (2.2, y0 - 0.07, 1.85), (2.45, 0.012, 1.2), "Vitrin")
    tente(g, -0.4, 4.8, y0, 3.25, renkler=("tente2", "beyaz"), dilim=8)
    for cy in (1.0, 4.0):
        pencere(g, cy, x1, 2.0, 1.1, 1.6, yon=0)
    kutu(g, (x0 - 0.04, 3.0, 1.5), (0.04, 0.85, 1.5), "kapi")
    # Üst katlar: önde 3, yanlarda 3 pencere; ilk katta önde uzun demir balkon
    for i in range(NK):
        zc = ZK + KAT * i + 1.65
        for cx in (0.2, 2.6, 5.0):
            pencere(g, cx, y0, zc, 1.1, 1.75, cerceve="beyaz")
        for cy in (0.2, 2.6, 5.0):
            pencere(g, cy, x0, zc, 1.1, 1.75, yon=0)
        for cy in (-3.6, 0.0, 3.6):
            pencere(g, cy, x1, zc, 1.1, 1.7, yon=0)
    korkuluk(g, -0.6, 5.8, y0, ZK + 0.14, 0.8)
    cati_kirma(g, x0 - 0.3, x1 + 0.3, y0 - 0.3, y1 + 0.3, H + 0.3, 3.6, "kiremit_koyu", k)
    baca(g, 2.8, 2.4, H + 1.9, 2.0, 2.0)
    return g


def bina_dukkan():
    """İki katlı dükkân binası: adaçayı yeşili cephe, tam boy vitrin, çizgili tente, tabela, beşik kiremit çatı, panjurlar."""
    g = Govde("Bina_Dukkan")
    k = 4.0
    W, D, ZK, KAT = 10.0, 9.6, 4.0, 3.3
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    H = ZK + KAT
    pah_kutu(g, (0.0, 0.0, H / 2), (W / 2, D / 2, H / 2), 0.08, "adacayi", yumusak=False, kalinlik=k)
    bant(g, x0, x1, y0, 0.3, 0.6, 0.06, "tas_koyu")
    bant(g, x0, x1, y0, ZK, 0.26, 0.12, "beyaz")
    bant(g, x0, x1, y0, H + 0.1, 0.3, 0.3, "beyaz")
    for xs in (x0, x1):
        yan_bant(g, y0, y1, xs, H + 0.1, 0.3, 0.3, "beyaz")
    # Vitrin (kapı dahil), tabela (tahta) ve geniş tente
    kutu(g, (0.0, y0 - 0.03, 1.85), (4.4, 0.03, 1.4), "tahta")
    kutu(g, (-1.0, y0 - 0.07, 1.8), (3.2, 0.012, 1.25), "Vitrin")
    kutu(g, (3.35, y0 - 0.07, 1.55), (0.7, 0.03, 1.5), "kapi")
    kutu(g, (3.35, y0 - 0.1, 2.5), (0.5, 0.012, 0.42), "Vitrin")
    kutu(g, (0.0, y0 - 0.08, 3.62), (4.0, 0.06, 0.3), "krem")
    for i in range(7):                                                                            # tabelada "yazı"
        kutu(g, (-2.7 + i * 0.9, y0 - 0.15, 3.62), (0.26, 0.015, 0.14), "kapi")
    tente(g, x0 + 0.3, x1 - 0.3, y0, 3.25, derinlik=1.15, dusus=0.5, renkler=("tente2", "beyaz"), dilim=10)
    for s in (-1, 1):                                                                             # vitrin önü saksılar
        silindir(g, (s * 4.55, y0 - 0.35, 0.0), (s * 4.55, y0 - 0.35, 0.55), 0.24, 0.3, "saksi", n=10, kalinlik=2.0)
        top(g, (s * 4.55, y0 - 0.35, 0.85), (0.38, 0.38, 0.4), "yaprak2", n=8, m=6, kalinlik=2.0)
    # Üst kat: üç panjurlu pencere; yanlarda iki pencere
    for cx in (-3.0, 0.0, 3.0):
        pencere(g, cx, y0, ZK + 1.65, 1.1, 1.7, panjur="tente2")
    for cy in (-2.2, 2.2):
        pencere(g, cy, x0, ZK + 1.65, 1.0, 1.6, yon=0)
        pencere(g, cy, x1, ZK + 1.65, 1.0, 1.6, yon=0)
    cati_besik(g, x0 - 0.1, x1 + 0.1, y0 - 0.55, y1 + 0.55, H + 0.25, 3.0, "kiremit", "adacayi", k)
    baca(g, 2.6, 1.6, H + 1.4, 2.0, 2.0)
    return g


def bina_apartman():
    """Beş katlı apartman: açık Budapeşte sarısı, taş rustik zemin kat, Fransız balkonları, arduvaz mansart çatı ve çatı pencereleri."""
    g = Govde("Bina_Apartman")
    k = 4.0
    W, D, ZK, KAT, NK = 13.4, 12.4, 4.2, 3.3, 4
    x0, x1, y0, y1 = -W / 2, W / 2, -D / 2, D / 2
    H = ZK + KAT * NK
    pah_kutu(g, (0.0, 0.0, ZK / 2), (W / 2, D / 2, ZK / 2), 0.08, "tas", yumusak=False, kalinlik=k)
    for i in range(1, 6):                                                                         # rustik derzler
        bant(g, x0, x1, y0, ZK * i / 6, 0.05, 0.03, "tas_koyu")
    pah_kutu(g, (0.0, 0.0, ZK + KAT * NK / 2), (W / 2 - 0.05, D / 2 - 0.05, KAT * NK / 2), 0.08, "sari", yumusak=False, kalinlik=k)
    bant(g, x0, x1, y0, ZK, 0.34, 0.18, "krem")
    bant(g, x0, x1, y0, ZK + KAT * 2, 0.16, 0.08, "krem")
    bant(g, x0 - 0.15, x1 + 0.15, y0, H + 0.15, 0.45, 0.5, "krem")
    for xs in (x0, x1):
        yan_bant(g, y0, y1, xs, ZK, 0.34, 0.18, "krem")
        yan_bant(g, y0, y1, xs, H + 0.15, 0.45, 0.5, "krem")
    kapi(g, 0.0, y0, 1.7, 3.3)
    for cx in (-4.6, -2.3, 2.3, 4.6):
        pencere(g, cx, y0, 2.1, 1.2, 1.9, cerceve="tas_koyu")
    for i in range(NK):
        zc = ZK + KAT * i + 1.6
        for cx in (-4.6, -2.3, 0.0, 2.3, 4.6):
            pencere(g, cx, y0, zc, 1.15, 1.85)
            if i in (0, 1) and cx != 0.0:
                korkuluk(g, cx - 0.75, cx + 0.75, y0, zc - 1.0, 0.35)
        for cy in (-3.8, 0.0, 3.8):
            pencere(g, cy, x0, zc, 1.1, 1.75, yon=0)
            pencere(g, cy, x1, zc, 1.1, 1.75, yon=0)
    korkuluk(g, -1.0, 1.0, y0, ZK + 0.15, 0.85)                                                   # ortada asıl balkon
    # Mansart çatı: dik arduvaz etek, üstte basık kırma çatı; etekte dört çatı penceresi
    kesik_piramit(g, x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, H + 0.38, 1.0, 2.8, "arduvaz", k)
    cati_kirma(g, x0 + 0.8, x1 - 0.8, y0 + 0.8, y1 - 0.8, H + 3.18, 1.2, "arduvaz", k)
    for cx in (-4.2, -1.4, 1.4, 4.2):
        kutu(g, (cx, y0 + 0.75, H + 1.95), (0.55, 0.6, 0.75), "beyaz", kabuk=True, kalinlik=2.0)
        kutu(g, (cx, y0 + 0.14, H + 1.9), (0.36, 0.012, 0.45), "Cam")
        cati_kirma(g, cx - 0.68, cx + 0.68, y0 + 0.02, y0 + 1.4, H + 2.7, 0.45, "arduvaz", 2.0)
    for bx, by in ((-3.4, 3.2), (3.4, 3.2), (0.0, -1.2)):
        baca(g, bx, by, H + 3.2, 1.3, 2.0)
    return g


def uret():
    rnd = random.Random(TOHUM)
    yap = {
        "kurye": kurye,
        "bisiklet": bisiklet,
        "kargo_bisikleti": kargo_bisikleti,
        "paket_dolabi": paket_dolabi,
        "agac_yuvarlak": lambda: agac_yuvarlak(rnd),
        "agac_kavak": lambda: agac_kavak(rnd),
        "sokak_lambasi": lambda: sokak_lambasi(rnd),
        "bina_berhaz": bina_berhaz,
        "bina_kose": bina_kose,
        "bina_dukkan": bina_dukkan,
        "bina_apartman": bina_apartman,
    }
    return [(m, yap[m]()) for m in MODELLER]


# ------------------------------------------------------------------ palet dokusu (saf Python PNG)

def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (1, 3, 5))


def palet_kare(ad):
    for i, (a, _) in enumerate(PALET):
        if a == ad:
            return i
    raise KeyError("Palette yok: " + ad)


def palet_png(yol):
    """8×8 kareli 256×256 doku. Her karede yukarıdan aşağı degrade: üst açık ve sıcak, alt koyu ve serin (boyanmış görünüm)."""
    W = SUTUN * KARE
    satirlar = []
    for y in range(W):
        satir = bytearray()
        sira, iy = divmod(y, KARE)
        t = iy / (KARE - 1)                                   # 0 üst, 1 alt
        for sut in range(SUTUN):
            i = sira * SUTUN + sut
            r, gg, b = hex_rgb(PALET[i][1]) if i < len(PALET) else (0.5, 0.5, 0.5)
            ust = (min(1.0, r * 1.07 + 0.02), min(1.0, gg * 1.06 + 0.01), min(1.0, b * 1.02))
            alt = (r * 0.86, gg * 0.86, min(1.0, b * 0.9 + 0.02))
            c = tuple(ust[j] + (alt[j] - ust[j]) * t for j in range(3))
            px = bytes(int(max(0.0, min(1.0, v)) * 255 + 0.5) for v in c)
            satir += px * KARE
        satirlar.append(bytes(satir))
    raw = b"".join(b"\x00" + s for s in satirlar)

    def parca_(tip, veri):
        return struct.pack(">I", len(veri)) + tip + veri + struct.pack(">I", zlib.crc32(tip + veri) & 0xFFFFFFFF)

    with open(yol, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + parca_(b"IHDR", struct.pack(">IIBBBBB", W, W, 8, 2, 0, 0, 0)) +
                parca_(b"IDAT", zlib.compress(raw, 9)) + parca_(b"IEND", b""))


def yuz_uv(g, fi):
    """Yüzün köşe UV'leri: renginin karesi; karenin içinde köşenin parçadaki yüksekliğine göre (üst açık, alt koyu)."""
    renk = g.fr[fi]
    if renk in DUZ:
        return [(0.5, 0.5)] * len(g.f[fi])
    i = palet_kare(renk)
    sira, sut = divmod(i, SUTUN)
    W = float(SUTUN * KARE)
    pay = 3.0 / W                                             # kenardan 3 piksel içeride: komşu kareye taşmasın
    u = (sut + 0.5) / SUTUN
    v_ust = 1.0 - sira / SUTUN - pay
    v_alt = 1.0 - (sira + 1) / SUTUN + pay
    z0, z1 = g.fz[fi]
    olc = 1.0 / max(1e-6, z1 - z0)
    return [(u, v_alt + (v_ust - v_alt) * (g.v[k][2] - z0) * olc) for k in g.f[fi]]


# ------------------------------------------------------------------ Blender: malzeme, obje, iskelet, dışa aktarma

def srgb_lineer(rgb):
    return tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in rgb)


def malzemeler(palet_yolu):
    """Palet (dokulu) ve düz malzemeler; aynı adlı varsa yeniden kullanılır."""
    out = {}
    img = bpy.data.images.load(palet_yolu, check_existing=True)
    img.reload()
    for anahtar in ["StilizePalet"] + list(DUZ):
        ad = "StilizePalet" if anahtar == "StilizePalet" else mal_adi(anahtar)
        m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
        m.use_backface_culling = True                     # çizgi kabuğu ancak arka yüzler gizliyken görünür
        if bpy.app.version < (5, 0, 0):
            m.use_nodes = True
        nt = m.node_tree
        bsdf = nt.nodes.get("Principled BSDF") if nt is not None else None
        if anahtar == "StilizePalet":
            m.diffuse_color = (1.0, 1.0, 1.0, 1.0)
            if bsdf is not None:
                tex = nt.nodes.get("Palet") or nt.nodes.new("ShaderNodeTexImage")
                tex.name = "Palet"
                tex.image = img
                tex.interpolation = 'Linear'
                tex.location = (-320, 260)
                nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        else:
            r, gg, b = srgb_lineer(hex_rgb(DUZ[anahtar]))
            m.diffuse_color = (r, gg, b, 1.0)
            if bsdf is not None:
                bsdf.inputs["Base Color"].default_value = (r, gg, b, 1.0)
                if anahtar == ISIK:
                    for ad_ in ("Emission Color", "Emission"):
                        if ad_ in bsdf.inputs:
                            bsdf.inputs[ad_].default_value = (r, gg, b, 1.0)
                            break
                    if "Emission Strength" in bsdf.inputs:
                        bsdf.inputs["Emission Strength"].default_value = 3.0
        if bsdf is not None:
            if "Roughness" in bsdf.inputs:
                bsdf.inputs["Roughness"].default_value = 1.0 if anahtar == CIZ else 0.82
            for ad_ in ("Specular IOR Level", "Specular"):   # Blender 4+ / 3.x
                if ad_ in bsdf.inputs:
                    bsdf.inputs[ad_].default_value = 0.2
                    break
        out["StilizePalet" if anahtar == "StilizePalet" else anahtar] = m
    return out


def blender_obje(g, ad, k, mats):
    me = bpy.data.meshes.new(ad)
    me.from_pydata(g.v, [], g.f)
    sira = []
    for r in g.fr:
        a = r if r in DUZ else "StilizePalet"
        if a not in sira:
            sira.append(a)
    for a in sira:
        me.materials.append(mats[a])
    me.polygons.foreach_set("material_index", [sira.index(r if r in DUZ else "StilizePalet") for r in g.fr])
    me.polygons.foreach_set("use_smooth", g.fy)
    uv = me.uv_layers.new(name="UVMap")
    for fi, poly in enumerate(me.polygons):
        for li, (u, v) in zip(poly.loop_indices, yuz_uv(g, fi)):
            uv.data[li].uv = (u, v)
    me.update()
    ob = bpy.data.objects.new(ad, me)
    k.objects.link(ob)
    return ob


def parcalari_kur(ob, g, k, mats):
    """Teker, pedal, lamba camı: kendi pivotunda ayrı obje, ana objenin çocuğu (Unity'de döndürülür / yakılır)."""
    for pad, pg, pivot in g.parcalar:
        po = blender_obje(pg, pad, k, mats)
        po.parent = ob
        po.location = pivot


def koleksiyon():
    ad = "StilizePaket"
    k = bpy.data.collections.get(ad) or bpy.data.collections.new(ad)
    if k.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(k)
    for o in list(k.objects):                                 # tekrar çalıştırınca eskiler silinir
        veri = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if veri is not None and veri.users == 0:
            if isinstance(veri, bpy.types.Mesh):
                bpy.data.meshes.remove(veri)
            elif isinstance(veri, bpy.types.Armature):
                bpy.data.armatures.remove(veri)
    return k


def iskelet_kur(ad, k):
    data = bpy.data.armatures.new(ad)
    arm = bpy.data.objects.new(ad, data)
    k.objects.link(arm)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = {}
    for b, (bas, uc, ebe) in KEMIKLER.items():
        e = data.edit_bones.new(b)
        e.head, e.tail, e.roll = bas, uc, 0.0
        if ebe is not None:
            e.parent = eb[ebe]
            e.use_connect = False
        eb[b] = e
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def deri_bagla(ob, arm, g):
    """Her parça tek kemiğe bağlı (ağırlık 1): parçalı oyuncak karakter gibi; çizgi kabuğu da kendi parçasıyla oynar."""
    for b in KEMIKLER:
        vg = ob.vertex_groups.new(name=b)
        idx = [i for i, kb in enumerate(g.vb) if kb == b]
        if idx:
            vg.add(idx, 1.0, 'REPLACE')
    mod = ob.modifiers.new("Iskelet", 'ARMATURE')
    mod.object = arm
    ob.parent = arm


def aksiyon_tak(arm, act):
    arm.animation_data_create()
    arm.animation_data.action = act
    ad = arm.animation_data
    if hasattr(ad, "action_slot") and getattr(ad, "action_slot", None) is None and hasattr(act, "slots") and len(act.slots) > 0:
        ad.action_slot = act.slots[0]                         # Blender 4.4+ (slotlu aksiyonlar)


def animasyonlari_kur(arm):
    from mathutils import Matrix, Vector
    arm.animation_data_create()
    for klip, n in ANIMASYONLAR.items():
        eski = bpy.data.actions.get(klip)
        if eski is not None:
            bpy.data.actions.remove(eski)
        act = bpy.data.actions.new(klip)
        act.use_fake_user = True
        aksiyon_tak(arm, act)
        for f in range(n + 1):
            R, kay = poz(klip, f / n)
            for b, (_, _, ebe) in KEMIKLER.items():
                pb = arm.pose.bones[b]
                pb.rotation_mode = 'QUATERNION'
                Mr = arm.data.bones[b].matrix_local.to_3x3()
                pb.rotation_quaternion = (Mr.inverted() @ Matrix(R[b]) @ Mr).to_quaternion()
                pb.keyframe_insert("rotation_quaternion", frame=f + 1, group=b)
                if ebe is None:
                    pb.location = Mr.inverted() @ Vector(kay)
                    pb.keyframe_insert("location", frame=f + 1, group=b)
    sc = bpy.context.scene
    sc.frame_start, sc.frame_end = 1, ANIMASYONLAR["basic walk"] + 1
    aksiyon_tak(arm, bpy.data.actions["basic walk"])


FBX_ORTAK = dict(apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE',
                 use_mesh_modifiers=True, add_leaf_bones=False, path_mode='RELATIVE')


def sec(objeler, aktif):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objeler:
        o.select_set(True)
    bpy.context.view_layer.objects.active = aktif


def fbx_model(ob, yol):
    sec([ob] + list(ob.children_recursive), ob)
    eski = tuple(ob.location)
    ob.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    try:
        bpy.ops.export_scene.fbx(filepath=yol, use_selection=True, object_types={'MESH'}, bake_space_transform=True, **FBX_ORTAK)
    finally:
        ob.location = eski


def fbx_karakter(arm, ob, yol):
    sec([arm, ob], arm)
    eski_ad, eski_yer = arm.name, tuple(arm.location)
    arm.location = (0.0, 0.0, 0.0)
    arm.name = "Iskelet"          # bütün kurye dosyalarında aynı ad: oyun tek animasyonu hepsine takar
    bpy.context.view_layer.update()
    try:
        bpy.ops.export_scene.fbx(filepath=yol, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 bake_space_transform=False, use_armature_deform_only=False,
                                 bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                                 bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                                 bake_anim_step=1.0, bake_anim_simplify_factor=0.0, **FBX_ORTAK)
    finally:
        arm.name = eski_ad
        arm.location = eski_yer


DOSYA_ADI = {"kurye": "Kurye", "bisiklet": "Bisiklet", "kargo_bisikleti": "KargoBisikleti", "paket_dolabi": "PaketDolabi",
             "agac_yuvarlak": "Agac_Yuvarlak", "agac_kavak": "Agac_Kavak", "sokak_lambasi": "SokakLambasi",
             "bina_berhaz": "Bina_Berhaz", "bina_kose": "Bina_Kose", "bina_dukkan": "Bina_Dukkan", "bina_apartman": "Bina_Apartman"}


# ------------------------------------------------------------------ çıktı klasörü ve komut satırı

def argumanlar():
    """Blender'da '--' sonrasındaki, Python'da bütün argümanlar: --cikti KLASOR, --cizgi 0/1, --onizleme DOSYA, --modeller a,b."""
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else (sys.argv[1:] if not IN_BLENDER else [])
    out = {}
    i = 0
    while i < len(a):
        if a[i] in ("--cikti", "--cizgi", "--onizleme", "--modeller") and i + 1 < len(a):
            out[a[i][2:]] = a[i + 1]
            i += 2
        else:
            i += 1
    return out


def cikti_klasoru(arg):
    if arg:
        return os.path.abspath(os.path.expanduser(arg))
    if CIKTI:
        return os.path.abspath(os.path.expanduser(CIKTI))
    kok = os.environ.get("KURYE_TYCOON_KOK")                  # Unity'deki menü verir: Assets/KuryeTycoon
    if kok and os.path.isdir(kok):
        return os.path.join(kok, "Art", "Stilize")
    f = globals().get("__file__")
    if f:
        d = os.path.dirname(os.path.abspath(f))
        if os.path.basename(d) == "Blender" and os.path.basename(os.path.dirname(d)) == "Tools":
            return os.path.join(os.path.dirname(os.path.dirname(d)), "Art", "Stilize")
    return os.path.expanduser("~/Desktop/KuryeStilize")


def main():
    global CIZGI
    arg = argumanlar()
    if "cizgi" in arg:
        CIZGI = arg["cizgi"] not in ("0", "hayir", "false")
    global MODELLER
    if arg.get("modeller"):
        istenen = tuple(m.strip() for m in arg["modeller"].split(",") if m.strip())
        bilinmeyen = [m for m in istenen if m not in DOSYA_ADI]
        if bilinmeyen:
            raise SystemExit("Bilinmeyen model: " + ", ".join(bilinmeyen) + " (seçenekler: " + ", ".join(DOSYA_ADI) + ")")
        MODELLER = istenen
    modeller = uret()
    satir = [f"{DOSYA_ADI[m]}: {g.ucgen_sayisi()} üçgen" for m, g in modeller]
    if not IN_BLENDER:
        if "onizleme" in arg:
            onizleme(modeller, arg["onizleme"])
            satir.append("Önizleme: " + arg["onizleme"])
        print("\n".join(satir))
        return

    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    klasor = cikti_klasoru(arg.get("cikti"))
    os.makedirs(klasor, exist_ok=True)
    palet_yolu = os.path.join(klasor, PALET_DOKU)
    palet_png(palet_yolu)
    mats = malzemeler(palet_yolu)
    k = koleksiyon()
    x = 0.0
    for m, g in modeller:
        ad = DOSYA_ADI[m]
        yol = os.path.join(klasor, ad + ".fbx")
        ob = blender_obje(g, ad, k, mats)
        if m == "kurye":
            arm = iskelet_kur(ad + "_Iskelet", k)
            deri_bagla(ob, arm, g)
            animasyonlari_kur(arm)
            arm.location = (x, 0.0, 0.0)
            if DISA_AKTAR:
                fbx_karakter(arm, ob, yol)
        else:
            parcalari_kur(ob, g, k, mats)
            ob.location = (x, 0.0, 0.0)
            if DISA_AKTAR:
                fbx_model(ob, yol)
        genislik = max(p[0] for p in g.v) - min(p[0] for p in g.v) if g.v else 1.0
        x += max(2.6, genislik + 2.0)
    satir.append(f"FBX ve {PALET_DOKU}: {klasor}")
    print("STILIZE VARLIKLAR\n" + "\n".join(satir))


# ------------------------------------------------------------------ Blender'sız önizleme (matplotlib)

def onizleme(modeller, yol):
    """Modelleri yan yana çizer (çizgi kabuğu yerine yüz kenarları ince mürekkep). Sadece göz kontrolü için."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection

    def renk(r):
        if r in DUZ:
            return hex_rgb(DUZ[r])
        return hex_rgb(PALET[palet_kare(r)][1])

    isik = norm((-0.5, -0.8, 0.9))
    sutun = min(len(modeller), 6)
    satir_n = (len(modeller) + sutun - 1) // sutun
    fig = plt.figure(figsize=(3.2 * sutun, 4.6 * satir_n), dpi=110)
    fig.patch.set_facecolor("#efe6d4")
    for i, (m, g) in enumerate(modeller):
        ax = fig.add_subplot(satir_n, sutun, i + 1, projection="3d")
        ax.set_facecolor("#efe6d4")
        polys, renkler = [], []

        def ekle(gg, ofs):
            for f, r in zip(gg.f, gg.fr):
                if r == CIZ:
                    continue
                p = [add(gg.v[k], ofs) for k in f]
                n = norm(newell(p))
                l = 0.55 + 0.45 * max(0.0, dot(n, isik))
                c = renk(r)
                if r == ISIK:
                    l = 1.0
                polys.append(p)
                renkler.append((min(1, c[0] * l), min(1, c[1] * l), min(1, c[2] * l)))

        ekle(g, (0.0, 0.0, 0.0))
        for _, pg, pv in g.parcalar:
            ekle(pg, pv)
        pc = Poly3DCollection(polys, facecolors=renkler, edgecolors=(0.12, 0.09, 0.07, 0.35), linewidths=0.25)
        ax.add_collection3d(pc)
        xs = [p[0] for pl in polys for p in pl]
        ys = [p[1] for pl in polys for p in pl]
        zs = [p[2] for pl in polys for p in pl]
        r = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)) / 2 * 1.05
        cx, cy, cz = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, (max(zs) + min(zs)) / 2
        ax.set_xlim(cx - r, cx + r)
        ax.set_ylim(cy - r, cy + r)
        ax.set_zlim(cz - r, cz + r)
        ax.set_box_aspect((1, 1, 1))
        ax.view_init(elev=14, azim=-62)
        ax.set_axis_off()
        ax.set_title(f"{DOSYA_ADI[m]}\n{g.ucgen_sayisi()} üçgen", fontsize=10, color="#26221e")
    plt.subplots_adjust(left=0, right=1, top=0.9, bottom=0, wspace=0)
    fig.savefig(yol, facecolor=fig.get_facecolor())


if __name__ == "__main__":
    main()
