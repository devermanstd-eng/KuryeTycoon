# -*- coding: utf-8 -*-
"""
Kurye Tycoon arayüzü: Grease Pencil ile elle çizilmiş görünümlü simgeler ve panel çerçeveleri (CityRT tarzı).

Blender Python konsolunda:
exec(open(r"C:\\UnityProjectsSSD\\KuryeTycoon\\Assets\\KuryeTycoon\\Tools\\Blender\\arayuz_gp.py", encoding="utf-8").read())

Her simge ayrı bir Grease Pencil nesnesi olarak çizilir (koyu kahve mürekkep çizgi + renkli dolgu, hafif titrek kenar),
sonra şeffaf arka planla PNG olarak kaydedilir:
  Resources/UI/Ikonlar/<Ad>.png   (Unity'deki Icons.Kind adlarıyla aynı)
  Resources/UI/Panel/<ad>.png     (9 dilimli panel, kart, düğme çerçeveleri)
Unity bu dosyaları bulursa kendi çizdiği simgelerin yerine bunları kullanır.
"""
import bpy
import math
import os
import random

KOK = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Resources\UI"
IKON_PX = 128
PANEL_PX = 96

MUREKKEP = "#3a2c25"     # koyu kahve çizgi (referanstaki gibi)
KALINLIK = 0.045         # çizgi yarıçapı (simge −1…1 alanında)


def hex2rgba(h, a=1.0):
    h = h.strip("#")
    srgb = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    return (lin[0], lin[1], lin[2], a)


# ------------------------------------------------------------------ Grease Pencil yardımcıları (Blender 4.3+ / 5.x)

def gp_veri(ad):
    coll = getattr(bpy.data, "grease_pencils_v3", None) or getattr(bpy.data, "grease_pencils")
    return coll.new(ad)


_mat_cache = {}


def gp_malzeme(dolgu, cizgi=MUREKKEP):
    key = (dolgu, cizgi)
    if key in _mat_cache and _mat_cache[key].name in bpy.data.materials:
        return _mat_cache[key]
    m = bpy.data.materials.new(f"GP_{dolgu or 'yok'}_{cizgi or 'yok'}")
    bpy.data.materials.create_gpencil_data(m)
    g = m.grease_pencil
    g.show_stroke = cizgi is not None
    if cizgi:
        g.color = hex2rgba(cizgi)
    g.show_fill = dolgu is not None
    if dolgu:
        g.fill_color = hex2rgba(dolgu)
    _mat_cache[key] = m
    return m


class Cizim:
    """Bir simgenin çizimi: çokgenler ve çizgiler sırayla eklenir (sonraki üstte)."""

    def __init__(self, ad, tohum=1):
        self.ad = ad
        self.rnd = random.Random(tohum)
        self.parca = []     # (noktalar, kapali, dolgu, cizgi, kalinlik)

    def titret(self, pts, miktar=0.012):
        return [(x + self.rnd.uniform(-miktar, miktar), y + self.rnd.uniform(-miktar, miktar)) for x, y in pts]

    def sekil(self, pts, dolgu, cizgi=MUREKKEP, k=KALINLIK, titrek=True):
        self.parca.append((self.titret(pts) if titrek else pts, True, dolgu, cizgi, k))

    def cizgi(self, pts, renk=MUREKKEP, k=KALINLIK, kapali=False):
        self.parca.append((self.titret(pts, 0.008), kapali, None, renk, k))

    # --- hazır biçimler
    def daire(self, cx, cy, r, dolgu, cizgi=MUREKKEP, n=28, k=KALINLIK):
        self.sekil([(cx + r * math.cos(2 * math.pi * i / n), cy + r * math.sin(2 * math.pi * i / n)) for i in range(n)], dolgu, cizgi, k)

    def elips(self, cx, cy, rx, ry, dolgu, cizgi=MUREKKEP, n=28, k=KALINLIK):
        self.sekil([(cx + rx * math.cos(2 * math.pi * i / n), cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n)], dolgu, cizgi, k)

    def kutu(self, x0, y0, x1, y1, dolgu, cizgi=MUREKKEP, r=0.0, k=KALINLIK):
        if r <= 0:
            self.sekil([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], dolgu, cizgi, k)
            return
        pts = []
        for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
            for i in range(6):
                a = math.radians(a0 + 90 * i / 5)
                pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
        self.sekil(pts, dolgu, cizgi, k)

    def yay(self, cx, cy, r, a0, a1, renk=MUREKKEP, k=KALINLIK, n=16):
        self.cizgi([(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
                    for i in range(n + 1)], renk, k)

    def nesne(self, z=0.0):
        """Grease Pencil nesnesini oluşturur."""
        gpd = gp_veri("GP_" + self.ad)
        ob = bpy.data.objects.new("GP_" + self.ad, gpd)
        bpy.context.scene.collection.objects.link(ob)
        mats = []
        for _, _, dolgu, cizgi, _ in self.parca:
            m = gp_malzeme(dolgu, cizgi)
            if m.name not in [x.name for x in gpd.materials]:
                gpd.materials.append(m)
            mats.append([x.name for x in gpd.materials].index(m.name))
        layer = gpd.layers.new("Cizim")
        frame = layer.frames.new(bpy.context.scene.frame_current)
        dr = frame.drawing
        dr.add_strokes([len(p[0]) for p in self.parca])
        for i, (pts, kapali, dolgu, cizgi, k) in enumerate(self.parca):
            st = dr.strokes[i]
            st.cyclic = kapali
            st.material_index = mats[i]
            for j, (x, y) in enumerate(pts):
                pt = st.points[j]
                pt.position = (x, y, z + i * 0.0005)
                pt.radius = k * (0.85 + 0.3 * self.rnd.random()) if cizgi else 0.001
        return ob


# ------------------------------------------------------------------ renkler (CityRT paleti)
R = dict(krem="#fbf3e2", kagit="#fff9ee", kahve="#8a5a3a", acik_kahve="#c99a63", kirmizi="#e0533f", turuncu="#f0913a",
         sari="#f6c544", yesil="#6fb35a", koyu_yesil="#3f8a4a", mavi="#4f8fd8", acik_mavi="#a9d3ee", mor="#9a7ac8",
         pembe="#ef8fa6", gri="#9aa0a6", koyu_gri="#5a6068", beyaz="#ffffff", ten="#f2c9a4", lacivert="#2f4a7a")


# ------------------------------------------------------------------ simgeler (Unity Icons.Kind adları)

def _cikis_daire(d, cx, cy, r):
    # Merkezden (0,0) d yönündeki ışının daireden çıktığı uzaklık (yoksa 0)
    b = d[0] * cx + d[1] * cy
    disk = b * b - (cx * cx + cy * cy - r * r)
    return b + math.sqrt(disk) if disk >= 0 else 0.0


def _cikis_kutu(d, x0, y0, x1, y1):
    t = 1e9
    for comp, lo, hi in ((d[0], x0, x1), (d[1], y0, y1)):
        if abs(comp) > 1e-6:
            t = min(t, max(lo / comp, hi / comp))
    return t if t < 1e8 else 0.0


def bulut(c, ox=0.0, oy=0.0, s=1.0, renk=R["beyaz"]):
    """Üç top ve düz taban: merkezden ışınlarla dış zarf (tek parça, girintisiz)."""
    pts = []
    n = 48
    for i in range(n):
        a = 2 * math.pi * i / n
        d = (math.cos(a), math.sin(a))
        t = max(_cikis_daire(d, -0.42, -0.1, 0.3), _cikis_daire(d, -0.05, 0.12, 0.4), _cikis_daire(d, 0.42, -0.06, 0.3),
                _cikis_kutu(d, -0.7, -0.38, 0.7, 0.0))
        pts.append((ox + d[0] * t * s, oy + d[1] * t * s))
    c.sekil(pts, renk)


def yildiz(c, cx, cy, r, ri, renk, n=5):
    c.sekil([(cx + (r if i % 2 == 0 else ri) * math.cos(math.pi / 2 + math.pi * i / n),
              cy + (r if i % 2 == 0 else ri) * math.sin(math.pi / 2 + math.pi * i / n)) for i in range(2 * n)], renk)


def simsek(c, ox=0.0, oy=0.0, s=1.0):
    c.sekil([(ox + x * s, oy + y * s) for x, y in ((0.12, 0.85), (-0.42, 0.0), (-0.02, 0.0), (-0.16, -0.85), (0.42, 0.12), (0.02, 0.12))], R["sari"])


def yuz(c, mod):
    c.daire(0, 0, 0.8, R["sari"])
    c.daire(-0.28, 0.2, 0.08, MUREKKEP)
    c.daire(0.28, 0.2, 0.08, MUREKKEP)
    if mod == "gul":
        c.yay(0, -0.05, 0.42, 200, 340)
    elif mod == "duz":
        c.cizgi([(-0.3, -0.3), (0.3, -0.3)])
    else:
        c.yay(0, -0.62, 0.38, 25, 155)


def teker(c, x, y, r=0.24):
    c.daire(x, y, r, R["koyu_gri"])
    c.daire(x, y, r * 0.4, R["gri"])


def ikonlar():
    S = {}

    def ik(ad):
        def dek(fn):
            S[ad] = fn
            return fn
        return dek

    @ik("Orders")
    def _(c):                                        # sipariş çantası
        c.kutu(-0.6, -0.75, 0.6, 0.45, R["acik_kahve"], r=0.08)
        c.yay(0, 0.45, 0.32, 0, 180, k=0.06)
        c.kutu(-0.6, 0.05, 0.6, 0.18, R["kahve"], k=0.03)

    @ik("Couriers")
    def _(c):
        c.sekil([(-0.65, -0.85), (0.65, -0.85), (0.55, -0.15), (0.0, 0.08), (-0.55, -0.15)], R["turuncu"])
        c.daire(0, 0.42, 0.32, R["ten"])
        c.sekil([(-0.34, 0.5), (0.34, 0.5), (0.3, 0.78), (0.0, 0.86), (-0.3, 0.78)], R["kirmizi"])

    @ik("Hiring")
    def _(c):
        c.sekil([(-0.75, -0.85), (0.35, -0.85), (0.3, -0.2), (-0.2, 0.0), (-0.7, -0.2)], R["mavi"])
        c.daire(-0.2, 0.35, 0.3, R["ten"])
        c.daire(0.52, 0.42, 0.3, R["yesil"])
        c.cizgi([(0.52, 0.26), (0.52, 0.58)], k=0.05)
        c.cizgi([(0.36, 0.42), (0.68, 0.42)], k=0.05)

    @ik("Support")
    def _(c):                                        # konuşma balonu
        c.sekil([(-0.8, -0.15), (-0.8, 0.6), (0.8, 0.6), (0.8, -0.15), (-0.1, -0.15), (-0.45, -0.6), (-0.4, -0.15)], R["acik_mavi"])
        for x in (-0.38, 0.0, 0.38):
            c.daire(x, 0.22, 0.08, MUREKKEP)

    @ik("Upgrades")
    def _(c):
        c.daire(0, 0, 0.82, R["yesil"])
        c.sekil([(0, 0.55), (0.45, 0.05), (0.18, 0.05), (0.18, -0.5), (-0.18, -0.5), (-0.18, 0.05), (-0.45, 0.05)], R["beyaz"])

    @ik("Shops")
    def _(c):
        c.kutu(-0.7, -0.8, 0.7, 0.15, R["krem"])
        c.kutu(-0.25, -0.8, 0.2, -0.15, R["kahve"])
        c.kutu(0.3, -0.45, 0.6, -0.15, R["acik_mavi"], k=0.03)
        for i in range(5):
            x0 = -0.8 + i * 0.32
            c.sekil([(x0, 0.15), (x0 + 0.32, 0.15), (x0 + 0.32, 0.55), (x0, 0.55)], R["kirmizi"] if i % 2 == 0 else R["beyaz"], k=0.03)
        c.cizgi([(-0.8, 0.55), (0.8, 0.55)])

    @ik("Equipment")
    def _(c):                                        # kurye çantası
        c.kutu(-0.65, -0.75, 0.65, 0.55, R["koyu_yesil"], r=0.12)
        c.kutu(-0.45, -0.1, 0.45, 0.3, R["yesil"], k=0.03)
        c.yay(0, 0.55, 0.25, 0, 180, k=0.05)

    @ik("Campaigns")
    def _(c):                                        # megafon
        c.sekil([(-0.6, -0.2), (-0.6, 0.2), (0.45, 0.65), (0.45, -0.65)], R["kirmizi"])
        c.kutu(-0.8, -0.2, -0.6, 0.2, R["krem"])
        c.kutu(-0.55, -0.55, -0.35, -0.2, R["koyu_gri"], k=0.03)
        c.yay(0.55, 0, 0.3, -40, 40, k=0.04)

    @ik("Finance")
    def _(c):
        for x, h, col in ((-0.55, 0.5, R["mavi"]), (-0.05, 0.9, R["yesil"]), (0.45, 1.3, R["sari"])):
            c.kutu(x - 0.2, -0.75, x + 0.2, -0.75 + h, col)
        c.cizgi([(-0.85, -0.78), (0.85, -0.78)])

    @ik("Settings")
    def _(c):
        pts = []
        for i in range(32):
            a = 2 * math.pi * i / 32
            r = 0.8 if (i // 2) % 2 == 0 else 0.6
            pts.append((r * math.cos(a), r * math.sin(a)))
        c.sekil(pts, R["gri"])
        c.daire(0, 0, 0.26, R["krem"])

    @ik("Close")
    def _(c):
        c.daire(0, 0, 0.8, R["kirmizi"])
        c.cizgi([(-0.32, -0.32), (0.32, 0.32)], R["beyaz"], k=0.08)
        c.cizgi([(-0.32, 0.32), (0.32, -0.32)], R["beyaz"], k=0.08)

    @ik("Wallet")
    def _(c):
        c.kutu(-0.75, -0.6, 0.75, 0.5, R["yesil"], r=0.12)
        c.kutu(0.25, -0.2, 0.85, 0.15, R["koyu_yesil"], r=0.08)
        c.daire(0.45, -0.02, 0.07, R["sari"], k=0.025)

    @ik("Coins")
    def _(c):
        for i in range(3):
            c.elips(-0.15, -0.55 + i * 0.25, 0.5, 0.18, R["sari"])
        c.daire(0.35, 0.25, 0.42, R["sari"])
        c.daire(0.35, 0.25, 0.26, "#f9d97a", k=0.03)

    @ik("Star")
    def _(c):
        yildiz(c, 0, -0.05, 0.85, 0.38, R["sari"])

    @ik("Clock")
    def _(c):
        c.daire(0, 0, 0.82, R["krem"])
        c.cizgi([(0, 0), (0, 0.5)], k=0.055)
        c.cizgi([(0, 0), (0.35, -0.12)], k=0.055)
        c.daire(0, 0, 0.06, MUREKKEP)

    @ik("Sun")
    def _(c):
        for i in range(8):
            a = 2 * math.pi * i / 8
            c.cizgi([(0.58 * math.cos(a), 0.58 * math.sin(a)), (0.85 * math.cos(a), 0.85 * math.sin(a))], R["turuncu"], k=0.06)
        c.daire(0, 0, 0.45, R["sari"])

    @ik("Cloud")
    def _(c):
        bulut(c, 0, 0.0, 1.1)

    @ik("Rain")
    def _(c):
        bulut(c, 0, 0.25, 0.95)
        for x in (-0.4, 0.0, 0.4):
            c.sekil([(x, -0.35), (x + 0.1, -0.6), (x, -0.7), (x - 0.1, -0.6)], R["mavi"], k=0.025)

    @ik("Snow")
    def _(c):
        for i in range(3):
            a = math.pi * i / 3
            c.cizgi([(-0.75 * math.cos(a), -0.75 * math.sin(a)), (0.75 * math.cos(a), 0.75 * math.sin(a))], R["mavi"], k=0.06)
        c.daire(0, 0, 0.16, R["beyaz"])

    @ik("Storm")
    def _(c):
        bulut(c, 0, 0.25, 0.95, R["koyu_gri"])
        simsek(c, 0.05, -0.45, 0.55)

    @ik("Bolt")
    def _(c):
        simsek(c)

    @ik("Pause")
    def _(c):
        c.kutu(-0.45, -0.6, -0.12, 0.6, R["beyaz"], r=0.06)
        c.kutu(0.12, -0.6, 0.45, 0.6, R["beyaz"], r=0.06)

    @ik("Play")
    def _(c):
        c.sekil([(-0.38, -0.62), (-0.38, 0.62), (0.6, 0.0)], R["beyaz"])

    @ik("Warning")
    def _(c):
        c.sekil([(0, 0.82), (0.85, -0.68), (-0.85, -0.68)], R["sari"])
        c.kutu(-0.07, -0.2, 0.07, 0.38, MUREKKEP, k=0.01)
        c.daire(0, -0.42, 0.08, MUREKKEP, k=0.01)

    @ik("Heart")
    def _(c):
        pts = []
        for i in range(40):
            t = 2 * math.pi * i / 40
            x = 16 * math.sin(t) ** 3
            y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
            pts.append((x / 19.0, y / 19.0 + 0.05))
        c.sekil(pts, R["kirmizi"])

    @ik("Flag")
    def _(c):
        c.cizgi([(-0.55, -0.85), (-0.55, 0.8)], k=0.06)
        c.sekil([(-0.5, 0.75), (0.7, 0.55), (0.35, 0.3), (0.7, 0.05), (-0.5, 0.05)], R["kirmizi"])

    @ik("Phone")
    def _(c):
        c.kutu(-0.42, -0.85, 0.42, 0.85, R["lacivert"], r=0.12)
        c.kutu(-0.3, -0.55, 0.3, 0.62, R["acik_mavi"], k=0.03)
        c.daire(0, -0.7, 0.07, R["beyaz"], k=0.02)

    @ik("Basket")
    def _(c):
        c.sekil([(-0.8, 0.05), (0.8, 0.05), (0.55, -0.75), (-0.55, -0.75)], R["acik_kahve"])
        c.yay(0, 0.05, 0.5, 15, 165, k=0.06)
        for x in (-0.3, 0.0, 0.3):
            c.cizgi([(x, -0.05), (x * 0.8, -0.65)], k=0.03)

    @ik("Chart")
    def _(c):
        c.kutu(-0.8, -0.75, 0.8, 0.75, R["krem"], r=0.08)
        c.cizgi([(-0.6, -0.45), (-0.2, 0.0), (0.15, -0.2), (0.6, 0.45)], R["kirmizi"], k=0.06)

    @ik("Moon")
    def _(c):                                        # hilal: dış daireden iç daire çıkarılır
        R0, cx, cy, ri = 0.78, 0.34, 0.2, 0.62
        icinde = lambda x, y: (x - cx) ** 2 + (y - cy) ** 2 < ri * ri
        dis = [(R0 * math.cos(2 * math.pi * i / 90), R0 * math.sin(2 * math.pi * i / 90)) for i in range(90)]
        k = next(i for i in range(90) if icinde(*dis[i]) and not icinde(*dis[i - 1]))      # iç daireye girilen yer
        dis = dis[k:] + dis[:k]
        dis = [p for p in dis if not icinde(*p)]
        a0 = math.atan2(dis[-1][1] - cy, dis[-1][0] - cx)
        a1 = math.atan2(dis[0][1] - cy, dis[0][0] - cx)
        while a1 > a0:
            a1 -= 2 * math.pi
        ic = [(cx + ri * math.cos(a0 + (a1 - a0) * j / 20), cy + ri * math.sin(a0 + (a1 - a0) * j / 20)) for j in range(1, 20)]
        c.sekil(dis + ic, R["sari"])

    @ik("Calendar")
    def _(c):
        c.kutu(-0.75, -0.75, 0.75, 0.6, R["kagit"], r=0.08)
        c.kutu(-0.75, 0.3, 0.75, 0.6, R["kirmizi"], k=0.03)
        for x in (-0.4, 0.4):
            c.kutu(x - 0.06, 0.48, x + 0.06, 0.78, R["koyu_gri"], k=0.02)
        for i in range(3):
            for j in range(2):
                c.kutu(-0.5 + i * 0.38, -0.5 + j * 0.38, -0.32 + i * 0.38, -0.32 + j * 0.38, R["acik_kahve"], k=0.02)

    @ik("Save")
    def _(c):
        c.kutu(-0.75, -0.75, 0.75, 0.75, R["mavi"], r=0.08)
        c.kutu(-0.45, 0.25, 0.45, 0.75, R["beyaz"], k=0.03)
        c.kutu(-0.5, -0.65, 0.5, -0.05, R["krem"], k=0.03)

    @ik("Bike")
    def _(c):
        teker(c, -0.5, -0.3, 0.32)
        teker(c, 0.5, -0.3, 0.32)
        c.cizgi([(-0.5, -0.3), (-0.1, 0.25), (0.35, 0.25), (0.5, -0.3)], R["kirmizi"], k=0.06)
        c.cizgi([(-0.1, 0.25), (0.05, -0.3), (0.35, 0.25)], R["kirmizi"], k=0.05)
        c.cizgi([(0.3, 0.5), (0.45, 0.5)], k=0.06)

    @ik("Scooter")
    def _(c):
        teker(c, -0.55, -0.45, 0.22)
        teker(c, 0.5, -0.45, 0.22)
        c.kutu(-0.55, -0.4, 0.4, -0.28, R["mavi"], k=0.03)
        c.cizgi([(0.4, -0.35), (0.35, 0.6)], R["koyu_gri"], k=0.06)
        c.cizgi([(0.18, 0.6), (0.52, 0.6)], k=0.06)

    @ik("Moto")
    def _(c):
        teker(c, -0.5, -0.4, 0.28)
        teker(c, 0.5, -0.4, 0.28)
        c.sekil([(-0.55, -0.15), (0.35, -0.15), (0.5, 0.15), (-0.1, 0.25), (-0.55, 0.1)], R["kirmizi"])
        c.cizgi([(0.35, 0.15), (0.45, 0.5)], k=0.05)

    @ik("Car")
    def _(c):
        c.sekil([(-0.85, -0.35), (0.85, -0.35), (0.85, 0.0), (0.5, 0.1), (0.3, 0.45), (-0.35, 0.45), (-0.55, 0.1), (-0.85, 0.05)], R["kirmizi"])
        c.sekil([(-0.3, 0.12), (0.25, 0.12), (0.12, 0.36), (-0.2, 0.36)], R["acik_mavi"], k=0.03)
        teker(c, -0.45, -0.38, 0.2)
        teker(c, 0.45, -0.38, 0.2)

    @ik("Smile")
    def _(c):
        yuz(c, "gul")

    @ik("Meh")
    def _(c):
        yuz(c, "duz")

    @ik("Frown")
    def _(c):
        yuz(c, "uzgun")

    return S


def paneller():
    """9 dilimli çerçeveler: krem panel, açık kart, düğme, vurgulu (kırmızı-turuncu) düğme, koyu ipucu balonu."""
    P = {}

    def cerceve(dolgu, cizgi=MUREKKEP, k=0.035):
        def fn(c):
            c.kutu(-1.05, -1.05, 1.05, 1.05, dolgu, cizgi=cizgi, r=0.45, k=k)
        return fn

    P["panel"] = cerceve(R["krem"])
    P["kart"] = cerceve(R["kagit"], k=0.028)
    P["dugme"] = cerceve("#fffaf0", k=0.03)
    P["dugme_ust"] = cerceve("#fff2d6", k=0.03)
    P["vurgu"] = cerceve("#ee6a45", k=0.03)
    P["vurgu_ust"] = cerceve("#f5845f", k=0.03)
    P["cip"] = cerceve("#f3e6cc", k=0.026)
    return P


# ------------------------------------------------------------------ çizim ve kayıt

def sahne_hazirla():
    sc = bpy.context.scene
    for ob in list(sc.collection.objects):
        if ob.name.startswith("GP_") or ob.name == "ArayuzKamera":
            bpy.data.objects.remove(ob, do_unlink=True)
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try:
            sc.render.engine = eng
            break
        except Exception:
            continue
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    try:
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "None"
    except Exception:
        pass
    try:
        sc.render.filter_size = 1.2
    except Exception:
        pass
    cam_d = bpy.data.cameras.new("ArayuzKamera")
    cam_d.type = "ORTHO"
    cam_d.ortho_scale = 2.2
    cam = bpy.data.objects.new("ArayuzKamera", cam_d)
    sc.collection.objects.link(cam)
    cam.location = (0.0, 0.0, 10.0)
    cam.rotation_euler = (0.0, 0.0, 0.0)
    sc.camera = cam
    return sc


def ciz_ve_kaydet(sc, ad, fn, klasor, px, tohum):
    c = Cizim(ad, tohum)
    fn(c)
    ob = c.nesne()
    sc.render.resolution_x = sc.render.resolution_y = px
    sc.render.resolution_percentage = 100
    sc.render.filepath = os.path.join(klasor, ad + ".png")
    bpy.ops.render.render(write_still=True)
    ob.hide_render = True
    ob.hide_viewport = True
    return ob


def main():
    sc = sahne_hazirla()
    ik_kl = os.path.join(KOK, "Ikonlar")
    pn_kl = os.path.join(KOK, "Panel")
    os.makedirs(ik_kl, exist_ok=True)
    os.makedirs(pn_kl, exist_ok=True)
    gizle = [o for o in sc.collection.objects if o.type not in ("CAMERA",) and not o.name.startswith("GP_")]
    eski = [(o, o.hide_render) for o in gizle]
    for o in gizle:
        o.hide_render = True
    n = 0
    try:
        for i, (ad, fn) in enumerate(ikonlar().items()):
            ciz_ve_kaydet(sc, ad, fn, ik_kl, IKON_PX, 100 + i)
            n += 1
        for i, (ad, fn) in enumerate(paneller().items()):
            ciz_ve_kaydet(sc, ad, fn, pn_kl, PANEL_PX, 500 + i)
            n += 1
    finally:
        for o, h in eski:
            o.hide_render = h
    print(f"ARAYUZ TAMAM: {n} görsel → {KOK}")


main()
