# -*- coding: utf-8 -*-
"""
KURYE ŞİRKETİ TYCOON · MODEL ÜRETİCİ (Blender scripti)
  • Binalar: sıra ev (tuğla / sıva) ve ofis kulesi (high-tech: kuleler, borular, cam cephe, cam tonoz)
  • Karakterler: kurye (şirket renginde mont, termal çanta, kask) ve yaya (renk, saç, kıyafet çeşitleri);
    iskeletli, "basic walk" ve "idle" animasyonları FBX'in içinde

Kullanım:
  1. Blender'ı aç (3.6 ya da 4.x), üstten "Scripting" sekmesine geç.
  2. Text > Open ile bu dosyayı aç (ya da yeni metin açıp içine yapıştır).
  3. İstersen aşağıdaki AYARLAR kısmını değiştir, sonra "Run Script" (▶) ya da Alt+P.
  4. Binalar "KuryeBinalar" koleksiyonunda yan yana çıkar, her biri FBX olarak KLASOR'e kaydedilir.
     Tekrar çalıştırınca eski binalar silinir, yenileri üretilir.

Unity'ye almak:
  • Binalar → Assets/Resources/SehirPaketi/Binalar (oyun ölçeği ve yönü kendisi ayarlar)
  • Kuryeler → Assets/Resources/KuryeKarakterleri, sonra o klasörü seçip "Kurye Tycoon > Karakter Animasyonlarını Kur"
  • Yayalar → Assets/Resources/SehirPaketi/Insanlar
  • "Cizgi" malzemesinin shader'ını Universal Render Pipeline/Unlit yap, rengi siyah kalsın.
  • Kuryelerdeki "SirketRengi" ve "SirketRengi2" malzemeleri oyunda oyuncunun seçtiği şirket renklerine boyanır.

Model: önü Blender'da −Y'ye bakar (Front görünümünde sana dönük), taban z = 0, ölçü metre.
Siyah dış çizgi "ters kabuk" yöntemiyle modelin içinde: Unity'de ek efekt gerekmez.
"""

import math
import os
import random

try:
    import bpy
    IN_BLENDER = True
except ImportError:          # Blender dışında (önizleme / test) sadece geometri üretilir
    IN_BLENDER = False

# ================================== AYARLAR ==================================
MODEL = "kurye"            # "sira_ev" · "ofis_kulesi" · "kurye" · "yaya"
STIL = "kirmizi_tugla"     # sıra ev için: "kirmizi_tugla" · "pembe_siva" · "kahve_siva" · "karisik"
OFIS_STIL = "beton"        # ofis kulesi için: "beton" · "gri" · "beyaz" · "karisik"
OFIS_KAT = (5, 7)          # ofis kulesinin kat sayısı (zemindeki lobi hariç)
VARYANT = 3                # kaç farklı bina üretilsin (yan yana dizilir, her biri ayrı FBX)
TOHUM = 1                  # aynı sayı = aynı binalar; değiştirince yeni varyantlar çıkar
EV_SAYISI = 2              # bir binada yan yana kaç ev (1–3)
KAT_ARALIGI = (3, 4)       # her evin kat sayısı, zemin kat dahil (en az, en çok)
CIZGI = True               # karikatür dış çizgisi (üçgen sayısını yaklaşık ikiye katlar)
CIZGI_KALINLIK = 0.06      # metre (oyunda uzaktan da görünsün diye kalınca)
ON_BAHCE = True            # önde kaldırım, çöp kutuları, bazen ağaç
YAN_PENCERE = True         # yan ve arka cephelerde de pencere
DISA_AKTAR = True          # FBX olarak kaydet
KLASOR = "~/Desktop/KuryeBinalar"
# =============================================================================

KAT_YUK = 3.0      # kat yüksekliği
TABAN = 0.9        # zemin katın yerden yüksekliği (merdivenle çıkılır)
DERINLIK = 7.5     # evin derinliği

STILLER = {
    "kirmizi_tugla": dict(duvar=["#9c4a35", "#8e4331", "#a9573d"], suslu="#efe6d6", taban="#cfc6b6",
                          cati="#c8573c", kapi=["#c8583f", "#7a4a2e", "#3f5f7a"], baca="#8e4331",
                          parapet="duvar", kemer="duz", tepe=0.5),
    "pembe_siva": dict(duvar=["#efc4b2", "#e8b7a6", "#f2cfbf"], suslu="#f7efe3", taban="#d6ccbf",
                       cati="#b9634a", kapi=["#c2456a", "#8a5530", "#4a6f8f"], baca="#dfae9c",
                       parapet="suslu", kemer="bloklu", tepe=0.0),
    "kahve_siva": dict(duvar=["#8a6f68", "#7d655f", "#967b73"], suslu="#e9e2d8", taban="#6f625c",
                       cati="#a5503a", kapi=["#5a3b2a", "#2f4a5f", "#7a2f2f"], baca="#7d655f",
                       parapet="suslu", kemer="duz", tepe=0.0),
}

SABIT = dict(cam="#a9c9de", cati_zemin="#8f8a84", demir="#1f1f22", saksi="#b5553a", bitki="#5f8f4a",
             agac=["#7fa04a", "#c9a43c", "#6f9a55"], govde="#6b4a33", cop=["#5f8a4f", "#4f6f99"],
             kaldirim="#d8d1c4", merdiven="#cdc6ba", klima="#a7abb0", boru="#3a3a3d", cizgi="#141414", tokmak="#2a2a2a")

HEX = {}   # malzeme adı → renk


def M(etiket, hexv):
    """Malzeme adı: aynı renk tek malzeme olur."""
    ad = f"{etiket}_{hexv.strip('#')}"
    HEX[ad] = hexv
    return ad


# ------------------------------------------------------------------ vektör

def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def norm(a):
    l = math.sqrt(dot(a, a))
    return (a[0] / l, a[1] / l, a[2] / l) if l > 1e-9 else (0.0, 0.0, 0.0)


def newell(pts):
    nx = ny = nz = 0.0
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        nx += (a[1] - b[1]) * (a[2] + b[2])
        ny += (a[2] - b[2]) * (a[0] + b[0])
        nz += (a[0] - b[0]) * (a[1] + b[1])
    return (nx, ny, nz)


# ------------------------------------------------------------------ geometri

class Govde:
    """Köşeler, yüzler ve yüz başına malzeme. Blender'a en sonda tek mesh olarak aktarılır."""

    def __init__(self):
        self.v, self.f, self.fm, self.kabuksuz, self.mats = [], [], [], [], []
        self._mi = {}
        self.vb, self.kemik = [], None      # köşe başına kemik (karakterler için)

    def mat(self, ad):
        if ad not in self._mi:
            self._mi[ad] = len(self.mats)
            self.mats.append(ad)
        return self._mi[ad]

    def yuz(self, idx, mat, kabuk=True):
        if len(self.vb) < len(self.v):
            self.vb += [self.kemik] * (len(self.v) - len(self.vb))
        self.f.append(list(idx))
        self.fm.append(self.mat(mat))
        self.kabuksuz.append(not kabuk)

    def ucgen_sayisi(self):
        return sum(len(f) - 2 for f in self.f)


# Kutunun yüzleri (dışa bakan sırayla): köşe 0..7 = x0y0z0, x1y0z0, x1y1z0, x0y1z0, x0y0z1, x1y0z1, x1y1z1, x0y1z1
KUTU_YUZ = {"zn": [0, 3, 2, 1], "zp": [4, 5, 6, 7], "yn": [0, 1, 5, 4],
            "yp": [3, 7, 6, 2], "xn": [0, 4, 7, 3], "xp": [1, 2, 6, 5]}


def kutu(g, x0, x1, y0, y1, z0, z1, mat, atla=(), yuzler=None):
    """Eksenlere hizalı kutu. atla: çizilmeyecek yüzler (duvara yapışan yüz gibi). yuzler: yüz başına başka malzeme."""
    x0, x1 = min(x0, x1), max(x0, x1)
    y0, y1 = min(y0, y1), max(y0, y1)
    z0, z1 = min(z0, z1), max(z0, z1)
    b = len(g.v)
    g.v += [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
            (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
    for k, idx in KUTU_YUZ.items():
        if k in atla:
            continue
        g.yuz([b + i for i in idx], (yuzler or {}).get(k, mat))


def dis_bukey(g, pts, yuzler, mat, kabuk=True):
    """Dışbükey parça: yüzlerin yönü merkeze göre düzeltilir."""
    b = len(g.v)
    g.v += list(pts)
    c = mul(pts[0], 0.0)
    for p in pts:
        c = add(c, p)
    c = mul(c, 1.0 / len(pts))
    for face in yuzler:
        fp = [pts[i] for i in face]
        fc = mul(fp[0], 0.0)
        for p in fp:
            fc = add(fc, p)
        fc = mul(fc, 1.0 / len(fp))
        if dot(newell(fp), sub(fc, c)) < 0:
            face = face[::-1]
        g.yuz([b + i for i in face], mat, kabuk)


OBOX_YUZ = [[0, 2, 6, 4], [1, 3, 7, 5], [0, 1, 5, 4], [2, 3, 7, 6], [0, 1, 3, 2], [4, 5, 7, 6]]


def yonlu_kutu(g, c, ax, ay, az, mat, kabuk=True):
    """Merkez ve üç yarı-kenar vektörüyle döndürülmüş kutu (çatı, korkuluk)."""
    pts = []
    for k in range(8):
        sx, sy, sz = (k & 1) * 2 - 1, ((k >> 1) & 1) * 2 - 1, ((k >> 2) & 1) * 2 - 1
        pts.append(add(add(add(c, mul(ax, sx)), mul(ay, sy)), mul(az, sz)))
    dis_bukey(g, pts, OBOX_YUZ, mat, kabuk)


def cubuk(g, p0, p1, t, mat, kabuk=True):
    """İki nokta arası kare kesitli çubuk. İnce koyu parçalarda (korkuluk) kabuk=False: çizgi onları kalınlaştırmasın."""
    d = sub(p1, p0)
    dn = norm(d)
    ref = (0.0, 0.0, 1.0) if abs(dn[2]) < 0.9 else (1.0, 0.0, 0.0)
    u = mul(norm(cross(dn, ref)), t / 2)
    v = mul(norm(cross(dn, u)), t / 2)
    yonlu_kutu(g, mul(add(p0, p1), 0.5), mul(d, 0.5), u, v, mat, kabuk)


def silindir(g, cx, cy, z0, h, r, n, mat):
    pts = []
    for z in (z0, z0 + h):
        for i in range(n):
            a = 2 * math.pi * i / n
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a), z))
    yuzler = [list(range(n)), list(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        yuzler.append([i, j, n + j, n + i])
    dis_bukey(g, pts, yuzler, mat)


ICO_V = None
ICO_F = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
         [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]]


def top(g, c, sx, sy, sz, mat):
    """Alçak poligon küre (ağaç tepesi, saksı bitkisi): 20 yüz."""
    global ICO_V
    if ICO_V is None:
        t = (1 + math.sqrt(5)) / 2
        raw = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
               (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
        ICO_V = [norm(p) for p in raw]
    pts = [(c[0] + p[0] * sx, c[1] + p[1] * sy, c[2] + p[2] * sz) for p in ICO_V]
    dis_bukey(g, pts, ICO_F, mat)


def prizma_xz(g, ucgen, y0, y1, mat):
    """XZ düzleminde üçgen, Y boyunca uzatılmış (arka üçgen duvar)."""
    pts = [(x, y0, z) for x, z in ucgen] + [(x, y1, z) for x, z in ucgen]
    dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], mat)


class Cephe:
    """Bir duvar yüzü: u duvar boyunca, o duvardan dışarı, z yukarı."""

    def __init__(self, origin, udir, out):
        self.o, self.u, self.out = origin, udir, out
        self.duvar_yuzu = {(0, -1): "yp", (0, 1): "yn", (-1, 0): "xp", (1, 0): "xn"}[(int(out[0]), int(out[1]))]

    def P(self, u, o, z):
        return add(add(self.o, mul(self.u, u)), (self.out[0] * o, self.out[1] * o, z))


def ckutu(g, fc, u0, u1, o0, o1, z0, z1, mat, atla=()):
    """Cepheye yapışık kutu; duvara değen yüzü çizilmez."""
    p, q = fc.P(u0, o0, z0), fc.P(u1, o1, z1)
    kutu(g, p[0], q[0], p[1], q[1], p[2], q[2], mat, atla=(fc.duvar_yuzu,) + tuple(atla))


def cdortgen(g, fc, u0, u1, o, z0, z1, mat):
    """Cephede tek yüz (cam): dışarı bakar, çizgi kabuğu yok."""
    pts = [fc.P(u0, o, z0), fc.P(u1, o, z0), fc.P(u1, o, z1), fc.P(u0, o, z1)]
    if dot(newell(pts), fc.out) < 0:
        pts = pts[::-1]
    b = len(g.v)
    g.v += pts
    g.yuz([b, b + 1, b + 2, b + 3], mat, kabuk=False)


def cprizma(g, fc, ucgen_uz, o0, o1, mat):
    pts = [fc.P(u, o0, z) for u, z in ucgen_uz] + [fc.P(u, o1, z) for u, z in ucgen_uz]
    dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], mat)


# ------------------------------------------------------------------ bina parçaları

def pencere(g, fc, cu, zb, w, h, r, sade=False):
    T = r["suslu"]
    cdortgen(g, fc, cu - w / 2, cu + w / 2, 0.012, zb, zb + h, r["cam"])
    t = 0.09
    ckutu(g, fc, cu - w / 2 - t, cu - w / 2, 0, 0.07, zb, zb + h, T)
    ckutu(g, fc, cu + w / 2, cu + w / 2 + t, 0, 0.07, zb, zb + h, T)
    ckutu(g, fc, cu - w / 2 - t, cu + w / 2 + t, 0, 0.07, zb + h, zb + h + t, T)
    ckutu(g, fc, cu - w / 2 - 0.14, cu + w / 2 + 0.14, 0, 0.15, zb - 0.1, zb, T)        # denizlik
    if sade:
        return
    ckutu(g, fc, cu - w / 2, cu + w / 2, 0, 0.045, zb + h * 0.52 - 0.03, zb + h * 0.52 + 0.03, T)   # orta kayıt
    top_z = zb + h + t
    ckutu(g, fc, cu - w / 2 - 0.17, cu + w / 2 + 0.17, 0, 0.12, top_z, top_z + 0.13, T)          # lento
    if r["kemer"] == "bloklu":
        for du in (-w / 2 - 0.06, 0.0, w / 2 + 0.06):
            ckutu(g, fc, cu + du - 0.065, cu + du + 0.065, 0, 0.11, top_z + 0.13, top_z + 0.27, T)
    else:
        ckutu(g, fc, cu - 0.08, cu + 0.08, 0, 0.14, top_z - 0.05, top_z + 0.2, T)             # kilit taşı


def kapi(g, fc, cu, r, rnd):
    T, z = r["suslu"], TABAN
    ckutu(g, fc, cu - 0.5, cu + 0.5, 0, 0.05, z, z + 2.3, r["kapi"])
    cdortgen(g, fc, cu - 0.26, cu + 0.26, 0.052, z + 1.55, z + 2.05, r["cam"])
    ckutu(g, fc, cu + 0.3, cu + 0.37, 0.05, 0.1, z + 1.05, z + 1.13, r["tokmak"])
    ckutu(g, fc, cu - 0.62, cu - 0.5, 0, 0.1, z, z + 2.42, T)
    ckutu(g, fc, cu + 0.5, cu + 0.62, 0, 0.1, z, z + 2.42, T)
    ckutu(g, fc, cu - 0.62, cu + 0.62, 0, 0.1, z + 2.3, z + 2.42, T)
    # Saçak: düz ya da üçgen alınlıklı
    ckutu(g, fc, cu - 0.82, cu + 0.82, 0, 0.62, z + 2.5, z + 2.62, T)
    for du in (-0.72, 0.72):
        ckutu(g, fc, cu + du - 0.05, cu + du + 0.05, 0, 0.4, z + 2.22, z + 2.5, T)
    if rnd.random() < 0.5:
        cprizma(g, fc, [(cu - 0.82, z + 2.62), (cu + 0.82, z + 2.62), (cu, z + 2.95)], 0, 0.62, T)


def merdiven(g, fc, cu, r):
    n, kosu, sahanlik = 5, 0.3, 0.6
    yuk = TABAN / n
    ckutu(g, fc, cu - 0.7, cu + 0.7, 0, sahanlik, 0, TABAN, r["merdiven"], atla=("zn",))
    for k in range(n - 1):
        ckutu(g, fc, cu - 0.7, cu + 0.7, 0, sahanlik + (n - 1 - k) * kosu, 0, (k + 1) * yuk, r["merdiven"], atla=("zn",))
    alt = sahanlik + (n - 1) * kosu - 0.08
    for du in (-0.64, 0.64):
        u = cu + du
        A, B, C = fc.P(u, alt, yuk + 0.9), fc.P(u, sahanlik - 0.04, TABAN + 0.9), fc.P(u, 0.1, TABAN + 0.9)
        cubuk(g, fc.P(u, alt, yuk), A, 0.05, r["demir"])
        cubuk(g, fc.P(u, sahanlik - 0.04, TABAN), B, 0.05, r["demir"])
        cubuk(g, A, B, 0.05, r["demir"])
        cubuk(g, B, C, 0.05, r["demir"])


def baca(g, x, y, z, r):
    kutu(g, x - 0.38, x + 0.38, y - 0.3, y + 0.3, z, z + 1.25, r["baca"])
    kutu(g, x - 0.44, x + 0.44, y - 0.36, y + 0.36, z + 1.25, z + 1.36, r["suslu"])
    for dx in (-0.16, 0.16):
        silindir(g, x + dx, y, z + 1.36, 0.3, 0.09, 7, r["saksi"])


def duz_cati(g, x0, W, D, fy, Ht, r, rnd):
    T = r["suslu"]
    P = r["duvar"] if r["parapet"] == "duvar" else T
    kutu(g, x0 - 0.1, x0 + W + 0.1, fy - 0.3, D + 0.1, Ht, Ht + 0.22, T)                      # korniş
    fc = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    nb = max(3, int(W / 0.62))
    for i in range(nb):
        u = 0.25 + (W - 0.5) * i / (nb - 1)
        ckutu(g, fc, u - 0.06, u + 0.06, 0, 0.24, Ht - 0.24, Ht, T)                               # konsollar
    z0, z1 = Ht + 0.22, Ht + 0.72
    kutu(g, x0 - 0.1, x0 + W + 0.1, fy - 0.3, fy - 0.08, z0, z1, P)                            # parapet
    kutu(g, x0 - 0.1, x0 + W + 0.1, D - 0.12, D + 0.1, z0, z1, P)
    kutu(g, x0 - 0.1, x0 + 0.12, fy - 0.08, D - 0.12, z0, z1, P)
    kutu(g, x0 + W - 0.12, x0 + W + 0.1, fy - 0.08, D - 0.12, z0, z1, P)
    c0, c1 = z1, z1 + 0.08                                                                       # harpuşta
    kutu(g, x0 - 0.13, x0 + W + 0.13, fy - 0.33, fy - 0.05, c0, c1, T)
    kutu(g, x0 - 0.13, x0 + W + 0.13, D - 0.15, D + 0.13, c0, c1, T)
    kutu(g, x0 - 0.13, x0 + 0.15, fy - 0.05, D - 0.15, c0, c1, T)
    kutu(g, x0 + W - 0.15, x0 + W + 0.13, fy - 0.05, D - 0.15, c0, c1, T)
    kutu(g, x0 + 0.12, x0 + W - 0.12, fy - 0.08, D - 0.12, z0, z0 + 0.04, r["cati_zemin"])     # çatı zemini
    zr = z0 + 0.04
    # Baca bir köşede, klima, havalandırma borusu, bazen saksılar
    bx = x0 + (0.5 if rnd.random() < 0.5 else W - 0.5)
    by = (fy + 0.55) if rnd.random() < 0.5 else (D - 0.55)
    baca(g, bx, by, zr, r)
    kx = x0 + rnd.uniform(1.0, W - 1.0)
    ky = rnd.uniform(fy + 2.0, D - 2.0)
    kutu(g, kx - 0.48, kx + 0.48, ky - 0.33, ky + 0.33, zr, zr + 0.42, r["klima"])
    silindir(g, x0 + rnd.uniform(0.6, W - 0.6), rnd.uniform(fy + 1.2, D - 1.2), zr, 0.6, 0.055, 6, r["boru"])
    if rnd.random() < 0.4:
        sx = x0 + rnd.uniform(0.6, W - 1.2)
        for dx in (0.0, 0.42):
            silindir(g, sx + dx, fy + 0.55, zr, 0.26, 0.16, 7, r["saksi"])
            top(g, (sx + dx, fy + 0.55, zr + 0.42), 0.21, 0.21, 0.2, r["bitki"])


def tepeli_cati(g, x0, W, D, fy, Ht, r):
    """Kademeli ön alınlık (Hollanda tarzı) ve arkasında kırmızı beşik çatı."""
    T, G = r["suslu"], W * 0.62
    cx = x0 + W / 2
    kutu(g, x0 - 0.03, x0 + W + 0.03, fy - 0.08, fy + 0.02, Ht - 0.12, Ht, T)                   # alınlık altı bant
    s = G / 3.6
    z = Ht
    for wi, hi in ((W, s), (W * 0.74, s), (W * 0.48, s), (W * 0.24, s * 0.6)):
        kutu(g, cx - wi / 2, cx + wi / 2, fy, fy + 0.32, z, z + hi, r["duvar"])
        kutu(g, cx - wi / 2 - 0.05, cx + wi / 2 + 0.05, fy - 0.05, fy + 0.37, z + hi, z + hi + 0.1, T)
        z += hi
    fc = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    pencere(g, fc, W / 2, Ht + 0.3, 0.7, 1.0, r)
    # Çatı: iki eğik levha, sırt alınlığın biraz altında
    y0, y1 = fy + 0.32, D + 0.15
    eL, ridge, eR = (x0 - 0.15, Ht - 0.05), (cx, Ht + G - 0.25), (x0 + W + 0.15, Ht - 0.05)
    koyu = r["cati_koyu"]
    for a, b in ((eL, ridge), (ridge, eR)):
        dx, dz = b[0] - a[0], b[1] - a[1]
        L = math.hypot(dx, dz)
        n = (-dz / L, dx / L)
        th = 0.07
        mid = ((a[0] + b[0]) / 2 + n[0] * th, (y0 + y1) / 2, (a[1] + b[1]) / 2 + n[1] * th)
        yonlu_kutu(g, mid, (dx / 2, 0.0, dz / 2), (0.0, (y1 - y0) / 2, 0.0), (n[0] * th, 0.0, n[1] * th), r["cati"])
        for f in (0.3, 0.6, 0.85):                                                                 # kiremit sıraları
            px, pz = a[0] + dx * f + n[0] * (2 * th + 0.02), a[1] + dz * f + n[1] * (2 * th + 0.02)
            cubuk(g, (px, y0 + 0.08, pz), (px, y1 - 0.04, pz), 0.05, koyu)
    prizma_xz(g, [(x0, Ht), (x0 + W, Ht), (cx, Ht + G - 0.35)], D - 0.3, D, r["duvar"])       # arka üçgen duvar
    zc = Ht + G * 0.45
    kutu(g, cx + W * 0.18 - 0.35, cx + W * 0.18 + 0.35, D - 1.5, D - 0.95, zc, Ht + G + 0.35, r["baca"])
    kutu(g, cx + W * 0.18 - 0.41, cx + W * 0.18 + 0.41, D - 1.56, D - 0.89, Ht + G + 0.35, Ht + G + 0.45, T)
    for dx in (-0.15, 0.15):
        silindir(g, cx + W * 0.18 + dx, D - 1.22, Ht + G + 0.45, 0.28, 0.08, 7, r["saksi"])


def ev(g, rnd, r, x0, W, D, fy, kat, tepe, kapi_sag, uc_sol, uc_sag):
    """Tek sıra ev. Dönüş: (kapının x'i, zemin kat penceresinin x'i)."""
    Ht = TABAN + kat * KAT_YUK
    kutu(g, x0, x0 + W, fy, D, 0.0, Ht, r["duvar"], atla=("zn",), yuzler={"zp": r["cati_zemin"]})
    kutu(g, x0 - 0.03, x0 + W + 0.03, fy - 0.04, D + 0.03, 0.0, TABAN, r["taban"], atla=("zn",))  # bodrum / taban
    fc = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    ckutu(g, fc, -0.02, W + 0.02, 0, 0.07, TABAN + KAT_YUK - 0.06, TABAN + KAT_YUK + 0.07, r["suslu"])  # kat bandı

    sutun = [W * 0.28, W * 0.72]
    ku = sutun[1] if kapi_sag else sutun[0]
    pu = sutun[0] if kapi_sag else sutun[1]
    kapi(g, fc, ku, r, rnd)
    merdiven(g, fc, ku, r)
    pencere(g, fc, pu, TABAN + 0.85, 0.85, 1.5, r)
    cdortgen(g, fc, pu - 0.36, pu + 0.36, 0.052, 0.22, 0.62, r["cam"])                          # bodrum penceresi
    ckutu(g, fc, pu - 0.44, pu + 0.44, 0.04, 0.1, 0.14, 0.22, r["suslu"])
    for f in range(1, kat):
        for u in sutun:
            pencere(g, fc, u, TABAN + f * KAT_YUK + 0.85, 0.85, 1.5, r)

    if YAN_PENCERE:
        arka = Cephe((x0 + W, D, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
        for f in range(kat):
            for u in sutun:
                pencere(g, arka, u, TABAN + f * KAT_YUK + 0.85, 0.85, 1.4, r, sade=True)
        derin = D - fy
        for yan, var in ((Cephe((x0, D, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0)), uc_sol),
                         (Cephe((x0 + W, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), uc_sag)):
            if not var:
                continue
            for f in range(1, kat):
                pencere(g, yan, derin * 0.5, TABAN + f * KAT_YUK + 0.85, 0.8, 1.4, r, sade=True)

    if tepe:
        tepeli_cati(g, x0, W, D, fy, Ht, r)
    else:
        duz_cati(g, x0, W, D, fy, Ht, r, rnd)
    return x0 + ku, x0 + pu


def bahce(g, rnd, evler, genislik, r):
    kutu(g, -0.3, genislik + 0.3, -2.3, 0.3, 0.0, 0.05, r["kaldirim"], atla=("zn",))
    for (x0, W, fy, kx, px) in evler:
        if rnd.random() < 0.75:
            for dx, renk in ((-0.3, r["cop"][0]), (0.3, r["cop"][1])):
                cx, y0, y1 = px + dx, fy - 0.9, fy - 0.42
                kutu(g, cx - 0.24, cx + 0.24, y0, y1, 0.05, 0.86, renk)
                kutu(g, cx - 0.27, cx + 0.27, y0 - 0.03, y1 + 0.03, 0.86, 0.93, renk)
    if rnd.random() < 0.6:
        sinirlar = [e[0] + e[1] for e in evler[:-1]] or [evler[0][0] + 0.6]
        tx = rnd.choice(sinirlar)
        silindir(g, tx, -1.85, 0.05, 1.35, 0.07, 6, r["govde"])
        top(g, (tx, -1.85, 2.05), 0.5, 0.5, 0.85, rnd.choice(r["agac"]))


def kabuk(g, w):
    """Ters kabuk: her parçanın biraz büyütülmüş, yüzleri ters çevrilmiş siyah kopyası (karikatür çizgisi)."""
    nv = len(g.v)
    acc = [[0.0, 0.0, 0.0] for _ in range(nv)]
    fnorms = []
    for face, ks in zip(g.f, g.kabuksuz):
        n = norm(newell([g.v[i] for i in face]))
        fnorms.append(n)
        if ks:
            continue
        for i in face:
            a = acc[i]
            a[0] += n[0]; a[1] += n[1]; a[2] += n[2]
    vn = [norm(tuple(a)) for a in acc]
    mindot = [1.0] * nv
    for face, ks, n in zip(g.f, g.kabuksuz, fnorms):
        if ks:
            continue
        for i in face:
            mindot[i] = min(mindot[i], max(0.35, dot(vn[i], n)))
    yeni, ref = {}, []
    cizgi = M("Cizgi", SABIT["cizgi"])
    for face, ks in zip(list(g.f), list(g.kabuksuz)):
        if ks:
            continue
        idx = []
        for i in face:
            if i not in yeni:
                yeni[i] = len(g.v)
                g.v.append(add(g.v[i], mul(vn[i], w / mindot[i])))
                g.vb.append(g.vb[i] if i < len(g.vb) else None)
            idx.append(yeni[i])
        ref.append(idx[::-1])
    for f in ref:
        g.yuz(f, cizgi, kabuk=False)


def ortala(g):
    xs = [p[0] for p in g.v]
    ys = [p[1] for p in g.v]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    g.v = [(p[0] - cx, p[1] - cy, p[2]) for p in g.v]
    return max(xs) - min(xs)


def renkler(st, rnd, onceki_duvar):
    secenek = [c for c in st["duvar"] if c != onceki_duvar] or st["duvar"]
    duvar = rnd.choice(secenek)
    cati = st["cati"]
    koyu = "#%02x%02x%02x" % tuple(int(int(cati[i:i + 2], 16) * 0.78) for i in (1, 3, 5))
    return {
        "duvar": M("Duvar", duvar), "suslu": M("Suslu", st["suslu"]), "taban": M("Taban", st["taban"]),
        "cati": M("Cati", cati), "cati_koyu": M("CatiKoyu", koyu), "kapi": M("Kapi", rnd.choice(st["kapi"])),
        "baca": M("Baca", st["baca"]), "parapet": st["parapet"], "kemer": st["kemer"],
        "cam": M("Cam", SABIT["cam"]), "cati_zemin": M("CatiZemin", SABIT["cati_zemin"]), "demir": M("Demir", SABIT["demir"]),
        "saksi": M("Saksi", SABIT["saksi"]), "bitki": M("Bitki", SABIT["bitki"]), "govde": M("AgacGovde", SABIT["govde"]),
        "agac": [M("Agac", c) for c in SABIT["agac"]], "cop": [M("Cop", c) for c in SABIT["cop"]],
        "kaldirim": M("Kaldirim", SABIT["kaldirim"]), "merdiven": M("Merdiven", SABIT["merdiven"]), "klima": M("Klima", SABIT["klima"]), "boru": M("Boru", SABIT["boru"]),
        "tokmak": M("Tokmak", SABIT["tokmak"]), "_duvar_hex": duvar,
    }


def bina(rnd, stil):
    g = Govde()
    n = max(1, min(3, EV_SAYISI))
    x, evler, onceki, r = 0.0, [], None, None
    for i in range(n):
        st = STILLER[rnd.choice(list(STILLER)) if stil == "karisik" else stil]
        r = renkler(st, rnd, onceki)
        onceki = r["_duvar_hex"]
        W = rnd.uniform(3.9, 4.5)
        kat = rnd.randint(KAT_ARALIGI[0], KAT_ARALIGI[1])
        tepe = rnd.random() < st["tepe"]
        fy = 0.0 if i % 2 == 0 else rnd.choice([-0.1, 0.08])
        kx, px = ev(g, rnd, r, x, W, DERINLIK, fy, kat, tepe, rnd.random() < 0.5, i == 0, i == n - 1)
        evler.append((x, W, fy, kx, px))
        x += W
    if ON_BAHCE:
        bahce(g, rnd, evler, x, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    ortala(g)
    return g, x


# ------------------------------------------------------------------ ofis kulesi (high-tech)

OFIS_STILLER = {
    "beton": dict(beton="#b9a8a4", koyu="#8f817e", meydan="#d6c0b6", cam="#93a7b8"),
    "gri": dict(beton="#aaa9ad", koyu="#7d7c82", meydan="#d9cfc3", cam="#8296aa"),
    "beyaz": dict(beton="#dad6cf", koyu="#a7a39c", meydan="#e4ddd2", cam="#a3c0cf"),
}

OFIS_SABIT = dict(cam_acik="#c9d6df", cam_isik="#e6d39e", yansima="#dde6ec", lobi="#55626f", merdiven="#232327",
                  vinc="#2e5c9e", korkuluk="#4a4a4f", klima="#a7abb0", baba="#2f2f33", saksi="#bdb5ad",
                  bitki="#5f8f4a", cati_zemin="#9a9590", kanopi="#dfe7ec", kapi="#2b3138")

OFIS_KAT_YUK = 3.6


def boru(g, p0, p1, r, n, mat):
    """İki nokta arasında yatay ya da eğik silindir (çatıdaki boru)."""
    d = norm(sub(p1, p0))
    ref = (0.0, 0.0, 1.0) if abs(d[2]) < 0.9 else (1.0, 0.0, 0.0)
    u = norm(cross(d, ref))
    v = norm(cross(d, u))
    pts = []
    for p in (p0, p1):
        for i in range(n):
            a = 2 * math.pi * i / n
            pts.append(add(p, add(mul(u, r * math.cos(a)), mul(v, r * math.sin(a)))))
    yuzler = [list(range(n)), list(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        yuzler.append([i, j, n + j, n + i])
    dis_bukey(g, pts, yuzler, mat)


def cpoligon(g, fc, uoz, mat):
    """Cephede düz çokgen (camdaki yansıma şeridi gibi): dışarı bakar, çizgi kabuğu yok."""
    pts = [fc.P(u, o, z) for u, o, z in uoz]
    if dot(newell(pts), fc.out) < 0:
        pts = pts[::-1]
    b = len(g.v)
    g.v += pts
    g.yuz(list(range(b, b + len(pts))), mat, kabuk=False)


def vinc(g, x, y, z, yon, mat):
    """Kulenin tepesinde küçük cephe temizlik vinci. yon: kolun uzandığı yön (+1 / −1, x ekseninde)."""
    tepe = (x, y, z + 2.4)
    cubuk(g, (x, y, z), tepe, 0.17, mat)
    uc = (x + yon * 2.6, y, z + 2.4)
    cubuk(g, (x - yon * 0.5, y, z + 2.4), uc, 0.13, mat)
    cubuk(g, (x, y, z + 1.3), (x + yon * 1.9, y, z + 2.35), 0.09, mat)
    cubuk(g, (x - yon * 0.5, y, z + 2.4), (x, y, z + 3.0), 0.08, mat)
    cubuk(g, (x, y, z + 3.0), (x + yon * 2.0, y, z + 2.45), 0.06, mat)
    cubuk(g, (uc[0] - yon * 0.2, y, z + 2.35), (uc[0] - yon * 0.2, y, z + 1.4), 0.04, mat)
    kutu(g, uc[0] - yon * 0.2 - 0.14, uc[0] - yon * 0.2 + 0.14, y - 0.14, y + 0.14, z + 1.15, z + 1.4, mat)


def ofis_cephe(g, rnd, fc, L, zg, Htop, nf, c, s, yansima):
    """Cam cephe: her kat ve bölmede bir cam (bazıları açık renk ya da içi ışıklı), bölme dikmeleri."""
    nb = max(1, int(round(L / 1.6)))
    bw = L / nb
    H = OFIS_KAT_YUK
    for f in range(nf):
        z0, z1 = zg + f * H + 0.35, zg + (f + 1) * H
        h = z1 - z0
        for b in range(nb):
            u0, u1 = b * bw, (b + 1) * bw
            x = rnd.random()
            renk = s["cam_isik"] if x < 0.14 else s["cam_acik"] if x < 0.3 else c["cam"]
            cdortgen(g, fc, u0 + 0.04, u1 - 0.04, 0.012, z0, z1, renk)
            if yansima and rnd.random() < 0.45:
                cpoligon(g, fc, [(u0 + 0.12 * bw, 0.02, z0 + 0.12 * h), (u0 + 0.36 * bw, 0.02, z0 + 0.12 * h),
                                 (u0 + 0.86 * bw, 0.02, z1 - 0.12 * h), (u0 + 0.62 * bw, 0.02, z1 - 0.12 * h)], s["yansima"])
    for b in range(nb + 1):
        u = b * bw
        ckutu(g, fc, u - 0.05, u + 0.05, 0, 0.07, zg, Htop, c["koyu"])


def servis_kulesi(g, rnd, x0, x1, y0, y1, zp, T, dis, c, s):
    """Yan servis kulesi: dikey koyu şeritler, tepede geniş başlık, yanında kalın boru. dis: dışa bakan yan (−1 sol, +1 sağ)."""
    kutu(g, x0, x1, y0, y1, zp, T - 2.4, c["beton"], atla=("zn",))
    kutu(g, x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, T - 2.4, T, c["beton"])
    kutu(g, x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.08, T, T + 0.12, c["koyu"])
    on = Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    yan = Cephe((x0, y1, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0)) if dis < 0 else Cephe((x1, y0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
    for fc, L in ((on, x1 - x0), (yan, y1 - y0)):
        for u in (L * 0.3, L * 0.7):
            ckutu(g, fc, u - 0.17, u + 0.17, 0, 0.06, zp + 0.6, T - 2.6, c["koyu"])
    bx = x0 - 0.4 if dis < 0 else x1 + 0.4
    by = y0 + 0.6
    silindir(g, bx, by, zp, T - 1.2 - zp, 0.28, 8, c["beton"])
    z = zp + 4.0
    while z < T - 2.0:
        silindir(g, bx, by, z, 0.28, 0.36, 8, c["koyu"])
        z += 7.2


def merdiven_kulesi(g, tx, zp, Htop, T, c, s):
    """Önde ortada, içinden zikzak merdiveni görünen kule."""
    H = OFIS_KAT_YUK
    gov = T - 2.6
    kutu(g, tx - 1.5, tx + 1.5, -0.75, -0.3, zp, gov, c["beton"], atla=("zn",))      # arka duvar
    kutu(g, tx - 1.5, tx - 1.22, -2.9, -0.75, zp, gov, c["beton"], atla=("zn",))     # yan duvarlar (önden direk gibi)
    kutu(g, tx + 1.22, tx + 1.5, -2.9, -0.75, zp, gov, c["beton"], atla=("zn",))
    n = int((gov - zp - 0.3) / (H / 2))
    for i in range(n):
        z0, z1 = zp + i * H / 2, zp + (i + 1) * H / 2
        xa, xb = (tx - 1.0, tx + 1.0) if i % 2 == 0 else (tx + 1.0, tx - 1.0)
        for y in (-1.35, -2.2):
            cubuk(g, (xa, y, z0), (xb, y, z1), 0.13, s["merdiven"], kabuk=False)
        lx = tx + 1.08 if xb > tx else tx - 1.08
        kutu(g, lx - 0.14, lx + 0.14, -2.5, -1.0, z1 - 0.12, z1, c["koyu"])
    kutu(g, tx - 1.7, tx + 1.7, -3.1, -0.1, gov, T, c["beton"])
    kutu(g, tx - 1.6, tx + 1.6, -3.0, -0.2, T, T + 0.12, c["koyu"])
    bas = Cephe((tx - 1.7, -3.1, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    for u in (0.85, 1.7, 2.55):
        cdortgen(g, bas, u - 0.03, u + 0.03, 0.01, gov + 0.2, T - 0.2, c["koyu"])


def ofis(rnd, stil):
    st = OFIS_STILLER[rnd.choice(list(OFIS_STILLER)) if stil == "karisik" else stil]
    c = {k: M("Ofis" + k.capitalize(), v) for k, v in st.items()}
    s = {k: M("Ofis" + "".join(p.capitalize() for p in k.split("_")), v) for k, v in OFIS_SABIT.items()}
    g = Govde()
    H = OFIS_KAT_YUK
    W = rnd.choice([12.8, 14.4, 16.0])
    D = rnd.choice([11.2, 12.8])
    nf = rnd.randint(OFIS_KAT[0], OFIS_KAT[1])
    zp = 0.2
    zg = zp + 4.0
    Htop = zg + nf * H
    tx = W / 2

    # Meydan, lobi (zemin kat içeride), giriş saçağı
    kutu(g, -3.6, W + 3.6, -5.2, D + 2.0, 0.0, zp, c["meydan"], atla=("zn",))
    kutu(g, 0.5, W - 0.5, 1.4, D - 0.5, zp, zg, s["lobi"], atla=("zn",))
    lobi = Cephe((0.5, 1.4, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    u = 0.0
    while u <= W - 1.0 + 1e-6:
        ckutu(g, lobi, u - 0.05, u + 0.05, 0, 0.06, zp, zg, c["koyu"])
        u += 1.6
    on_kolon = [0.4, tx - 2.6, tx + 2.6, W - 0.4]
    kx = (on_kolon[0] + on_kolon[1]) / 2
    kw = min(1.7, (on_kolon[1] - on_kolon[0]) / 2 - 0.5)
    for du in (-0.95, 0.05):
        cdortgen(g, lobi, kx - 0.5 + du, kx - 0.5 + du + 0.9, 0.012, zp, zp + 2.5, s["kapi"])
    kutu(g, kx - kw, kx + kw, -2.4, 1.4, 3.3, 3.42, s["kanopi"])
    for dx in (-kw + 0.2, kw - 0.2):
        cubuk(g, (kx + dx, -0.05, zg - 0.2), (kx + dx, -2.3, 3.42), 0.07, s["korkuluk"], kabuk=False)

    # Cam gövde, kat döşemeleri, cepheler
    kutu(g, 0.0, W, 0.0, D, zg, Htop, c["cam"], yuzler={"zn": c["beton"]})
    for f in range(nf):
        z = zg + f * H
        kutu(g, -0.3, W + 0.3, -0.3, D + 0.3, z, z + 0.35, c["beton"])
    kutu(g, -0.3, W + 0.3, -0.3, D + 0.3, Htop, Htop + 0.6, c["beton"], yuzler={"zp": s["cati_zemin"]})
    for fc, L, yans in ((Cephe((0.0, 0.0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0)), W, True),
                        (Cephe((W, 0.0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), D, True),
                        (Cephe((W, D, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0)), W, False),
                        (Cephe((0.0, D, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0)), D, True)):
        ofis_cephe(g, rnd, fc, L, zg, Htop, nf, c, s, yans)

    # Kuleler
    T_mer = Htop + rnd.uniform(4.5, 6.5)
    merdiven_kulesi(g, tx, zp, Htop, T_mer, c, s)
    kuleler = []
    sol_kule = rnd.random() < 0.85
    sag_kule = rnd.random() < 0.85 or not sol_kule      # en az bir servis kulesi olsun
    if sol_kule:
        y0 = D * rnd.uniform(0.38, 0.5)
        T = Htop + rnd.uniform(5.5, 8.0)
        servis_kulesi(g, rnd, -3.0, -0.3, y0, y0 + 2.6, zp, T, -1, c, s)
        kuleler.append((-1, y0, y0 + 2.6, -1.65, T))
    if sag_kule:
        y0 = D * rnd.uniform(0.06, 0.2)
        T = Htop + rnd.uniform(3.0, 6.0)
        servis_kulesi(g, rnd, W + 0.3, W + 3.0, y0, y0 + 2.6, zp, T, 1, c, s)
        kuleler.append((1, y0, y0 + 2.6, W + 1.65, T))

    # Dıştaki yuvarlak kolonlar (bilezikli), döşemelere konsolla bağlı
    kolonlar = [(x, -1.1, "on") for x in on_kolon]
    for yan, kxo in ((-1, -1.1), (1, W + 1.1)):
        for y in (1.2, D - 1.2):
            if any(k[0] == yan and k[1] - 0.8 < y < k[2] + 0.8 for k in kuleler):
                continue
            kolonlar.append((kxo, y, "sol" if yan < 0 else "sag"))
    for x, y, taraf in kolonlar:
        silindir(g, x, y, zp, Htop + 1.2 - zp, 0.36, 8, c["beton"])
        z = zg
        while z < Htop:
            silindir(g, x, y, z - 0.15, 0.3, 0.45, 8, c["koyu"])
            z += 2 * H
        for f in range(nf + 1):
            z = zg + f * H if f < nf else Htop
            if taraf == "on":
                kutu(g, x - 0.12, x + 0.12, y + 0.25, -0.3, z + 0.05, z + 0.3, c["koyu"])
            elif taraf == "sol":
                kutu(g, x + 0.25, -0.3, y - 0.12, y + 0.12, z + 0.05, z + 0.3, c["koyu"])
            else:
                kutu(g, W + 0.3, x - 0.25, y - 0.12, y + 0.12, z + 0.05, z + 0.3, c["koyu"])

    # Çatı: korkuluk, boru, klimalar, cam tonozlu atriyum
    zr = Htop + 0.6
    kenar = [(-0.15, -0.15), (W + 0.15, -0.15), (W + 0.15, D + 0.15), (-0.15, D + 0.15)]
    for i in range(4):
        a, b = kenar[i], kenar[(i + 1) % 4]
        L = math.hypot(b[0] - a[0], b[1] - a[1])
        n = max(1, int(L / 2.4))
        for k in range(n):
            t = k / n
            p = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
            cubuk(g, (p[0], p[1], zr), (p[0], p[1], zr + 1.1), 0.07, s["korkuluk"], kabuk=False)
        for h in (0.55, 1.1):
            cubuk(g, (a[0], a[1], zr + h), (b[0], b[1], zr + h), 0.06, s["korkuluk"], kabuk=False)
    boru(g, (1.0, 0.75, zr + 0.65), (W - 1.0, 0.75, zr + 0.65), 0.22, 8, c["beton"])
    for x in (1.6, W / 2, W - 1.6):
        kutu(g, x - 0.15, x + 0.15, 0.6, 0.9, zr, zr + 0.45, c["koyu"])
    for x, y in ((1.6, D - 1.6), (W - 1.8, D - 1.5)):
        kutu(g, x - 0.6, x + 0.6, y - 0.45, y + 0.45, zr, zr + 0.6, s["klima"])
    if rnd.random() < 0.9:
        ax0, ax1, ay0, ay1 = W * 0.22, W * 0.78, D * 0.2, D * 0.72
        zb1 = zr + rnd.uniform(3.0, 4.2)
        kutu(g, ax0, ax1, ay0, ay1, zr, zb1, c["cam"], atla=("zn",))
        for fc, L in ((Cephe((ax0, ay0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0)), ax1 - ax0),
                      (Cephe((ax1, ay0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), ay1 - ay0),
                      (Cephe((ax0, ay1, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0)), ay1 - ay0)):
            nb = max(1, int(round(L / 1.6)))
            for b in range(nb + 1):
                u = L * b / nb
                ckutu(g, fc, u - 0.05, u + 0.05, 0, 0.06, zr, zb1, c["koyu"])
            for b in range(nb):
                if rnd.random() < 0.5:
                    u0, u1 = L * b / nb, L * (b + 1) / nb
                    cpoligon(g, fc, [(u0 + 0.15 * (u1 - u0), 0.02, zr + 0.4), (u0 + 0.4 * (u1 - u0), 0.02, zr + 0.4),
                                     (u0 + 0.85 * (u1 - u0), 0.02, zb1 - 0.4), (u0 + 0.6 * (u1 - u0), 0.02, zb1 - 0.4)], s["yansima"])
        kutu(g, ax0 - 0.08, ax1 + 0.08, ay0 - 0.08, ay1 + 0.08, zb1 - 0.2, zb1, c["koyu"])
        rr, yc, n = (ay1 - ay0) / 2, (ay0 + ay1) / 2, 8
        def yay(k, r):
            a = math.pi * k / n
            return yc + r * math.cos(a), zb1 + r * math.sin(a) * 0.9
        pts = [(x,) + tuple(yay(k, rr)) for x in (ax0, ax1) for k in range(n + 1)]
        pts = [(p[0], p[1], p[2]) for p in pts]
        yuzler = [list(range(n + 1)), list(range(n + 1, 2 * n + 2)), [0, n, 2 * n + 1, n + 1]]
        yuzler += [[k, k + 1, n + 2 + k, n + 1 + k] for k in range(n)]
        dis_bukey(g, pts, yuzler, s["cam_acik"])
        nr = max(2, int(round((ax1 - ax0) / 1.6)) + 1)
        for i in range(nr):
            x = ax0 + (ax1 - ax0) * i / (nr - 1)
            for k in range(n):
                y0_, z0_ = yay(k, rr + 0.04)
                y1_, z1_ = yay(k + 1, rr + 0.04)
                cubuk(g, (x, y0_, z0_), (x, y1_, z1_), 0.09, c["koyu"])

    # Vinçler: bir servis kulesinde ve bazen merdiven kulesinde
    if kuleler and rnd.random() < 0.8:
        yan, y0, y1, cx, T = rnd.choice(kuleler)
        vinc(g, cx, (y0 + y1) / 2, T + 0.12, yan, s["vinc"])
    if rnd.random() < 0.5:
        vinc(g, tx, -1.6, T_mer + 0.12, rnd.choice([-1, 1]), s["vinc"])

    # Meydan: babalar ve saksılar
    x = -3.0
    while x <= W + 3.0 + 1e-6:
        silindir(g, x, -4.75, zp, 0.7, 0.12, 6, s["baba"])
        x += 1.5
    for x in (-2.3, W + 2.3):
        silindir(g, x, -3.6, zp, 0.55, 0.62, 8, s["saksi"])
        top(g, (x, -3.6, zp + 1.05), 0.7, 0.7, 0.62, s["bitki"])

    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    gen = ortala(g)
    return g, gen


def olcek(g):
    """Oyun her binayı kareye sığdırır: çizgi kalınlığı binanın boyuyla orantılı olsun ki hepsinde aynı görünsün."""
    xs = [p[0] for p in g.v]
    ys = [p[1] for p in g.v]
    return max(max(xs) - min(xs), max(ys) - min(ys)) / 10.0


# ------------------------------------------------------------------ karakterler (kurye ve yaya)

# Bütün kurye ve yayalar aynı iskeleti kullanır: oyun tek bir yürüme animasyonunu hepsine takar.
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

ANIMASYONLAR = {"basic walk": 24, "idle": 48}   # ad: kare sayısı (24 kare/sn). Oyun "walk" ve "idle" geçen adları arar.

TENLER = ["#f6d7c3", "#eec4a6", "#d9a47f", "#b77e57", "#8d5b3c", "#5f3d29"]
SACLAR = ["#1c1715", "#3a2a20", "#6b4a2f", "#b08a52", "#d9c08a", "#8a3b22", "#9a9a9e"]
USTLER = ["#c94f4f", "#4f7fc9", "#5fa36a", "#e0b24a", "#8a63b8", "#e8e4dc", "#3c4048", "#d9774a",
          "#4fa3a3", "#c95f8f", "#6f7f3f", "#2f4f7f"]
ALTLAR = ["#2b3a55", "#3a3f47", "#5a4a3a", "#7a6a55", "#1f2226", "#4a5a6a"]
AYAKKABI = ["#1f1f22", "#e8e8e8", "#6b4a33", "#8a2f2f"]
SIRKET_RENGI = "#009ede"     # Unity'de oyuncunun seçtiği şirket rengine boyanır
SIRKET_RENGI2 = "#ffffff"    # ikinci şirket rengi


def pahli_kutu(g, c, h, ch, mat, ust=None):
    """
    Kenarları pahlı kutu (yumuşak köşeli blok). h: yarı boyutlar. ust: üst yüzün (x, y) yarı boyutları
    verilirse aşağıdan yukarı daralan/genişleyen kutu (gövde).
    """
    hxb, hyb, hz = h
    hxt, hyt = ust if ust else (hxb, hyb)
    pts = []
    for k in range(8):
        sx, sy, sz = (k & 1) * 2 - 1, ((k >> 1) & 1) * 2 - 1, ((k >> 2) & 1) * 2 - 1
        hx, hy = (hxt, hyt) if sz > 0 else (hxb, hyb)
        pts.append((c[0] + sx * hx, c[1] + sy * (hy - ch), c[2] + sz * (hz - ch)))
        pts.append((c[0] + sx * (hx - ch), c[1] + sy * hy, c[2] + sz * (hz - ch)))
        pts.append((c[0] + sx * (hx - ch), c[1] + sy * (hy - ch), c[2] + sz * hz))

    def kose(sx, sy, sz):
        return ((sx > 0) * 1) | ((sy > 0) * 2) | ((sz > 0) * 4)

    yuzler = []
    dongu = [(-1, -1), (1, -1), (1, 1), (-1, 1)]
    for a in range(3):
        b_, c_ = (a + 1) % 3, (a + 2) % 3
        for s in (-1, 1):
            f = []
            for sb, sc in dongu:
                sg = [0, 0, 0]
                sg[a], sg[b_], sg[c_] = s, sb, sc
                f.append(3 * kose(*sg) + a)
            yuzler.append(f)
    for a, b_ in ((0, 1), (0, 2), (1, 2)):
        c_ = 3 - a - b_
        for sa in (-1, 1):
            for sb in (-1, 1):
                s1, s2 = [0, 0, 0], [0, 0, 0]
                s1[a], s1[b_], s1[c_] = sa, sb, -1
                s2[a], s2[b_], s2[c_] = sa, sb, 1
                k1, k2 = kose(*s1), kose(*s2)
                yuzler.append([3 * k1 + a, 3 * k1 + b_, 3 * k2 + b_, 3 * k2 + a])
    for k in range(8):
        yuzler.append([3 * k, 3 * k + 1, 3 * k + 2])
    dis_bukey(g, pts, yuzler, mat)


def on_dortgen(g, x0, x1, y, z0, z1, mat, arkaya=False):
    """Yüze/sırta yapışık ince düz parça (göz, logo): −Y'ye (arkaya=True ise +Y'ye) bakar, kabuksuz."""
    pts = [(x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1)]
    if (newell(pts)[1] < 0) == arkaya:
        pts = pts[::-1]
    b = len(g.v)
    g.v += pts
    g.yuz([b, b + 1, b + 2, b + 3], mat, kabuk=False)


def karakter(rnd, kurye):
    g = Govde()
    ten = M("Ten", rnd.choice(TENLER))
    sac = M("Sac", rnd.choice(SACLAR))
    goz = M("Goz", "#1d1a1a")
    ayak = M("Ayakkabi", rnd.choice(AYAKKABI))
    if kurye:
        ust = M("SirketRengi", SIRKET_RENGI)
        alt = M("Pantolon", rnd.choice(["#2b2f36", "#3a3f47", "#26303d"]))
        kisa_kol, sort = False, False
    else:
        ust = M("Ust", rnd.choice(USTLER))
        alt = M("Alt", rnd.choice(ALTLAR))
        kisa_kol = rnd.random() < 0.4
        sort = rnd.random() < 0.15
    w = rnd.uniform(0.95, 1.08)          # gövde genişliği (iskelet aynı kalır)

    # Bacaklar ve ayakkabılar
    for s, yan in ((1, "Left"), (-1, "Right")):
        x = 0.1 * s
        g.kemik = yan + "Leg"
        pahli_kutu(g, (x, -0.035, 0.045), (0.065, 0.12, 0.045), 0.02, ayak)
        pahli_kutu(g, (x, 0.0, 0.28), (0.06, 0.065, 0.21), 0.02, ten if sort else alt)
        g.kemik = yan + "UpLeg"
        pahli_kutu(g, (x, 0.0, 0.66), (0.07, 0.075, 0.21), 0.02, alt)
    g.kemik = "Hips"
    pahli_kutu(g, (0.0, 0.0, 0.9), (0.17 * w, 0.1, 0.09), 0.025, alt)
    if not kurye and rnd.random() < 0.25:                              # yan çanta
        pahli_kutu(g, (-0.215, 0.0, 0.93), (0.035, 0.1, 0.08), 0.015, M("Canta", rnd.choice(["#6b4a33", "#2f2f33", "#a33f3f"])))

    # Gövde ve kollar
    g.kemik = "Spine"
    pahli_kutu(g, (0.0, 0.0, 1.17), (0.165 * w, 0.105, 0.23), 0.03, ust, ust=(0.2 * w, 0.115))
    for s, yan in ((1, "Left"), (-1, "Right")):
        x = 0.245 * s
        g.kemik = yan + "Arm"
        pahli_kutu(g, (x, 0.0, 1.25), (0.055, 0.06, 0.13), 0.02, ust)
        g.kemik = yan + "ForeArm"
        pahli_kutu(g, (x, 0.0, 0.99), (0.05, 0.055, 0.12), 0.02, ten if kisa_kol else ust)
        pahli_kutu(g, (x, -0.01, 0.83), (0.045, 0.05, 0.05), 0.015, ten)

    # Kurye: sırtta termal çanta (şirket rengi, ikinci renkte bant ve logo), önde askılar
    if kurye:
        g.kemik = "Spine"
        ikinci = M("SirketRengi2", SIRKET_RENGI2)
        pahli_kutu(g, (0.0, 0.25, 1.24), (0.19, 0.13, 0.21), 0.03, ust)
        pahli_kutu(g, (0.0, 0.25, 1.4), (0.2, 0.14, 0.035), 0.015, ikinci)
        on_dortgen(g, -0.08, 0.08, 0.3815, 1.16, 1.3, ikinci, arkaya=True)
        koyu = M("Aski", "#26262a")
        for s in (1, -1):
            kutu(g, 0.1 * s - 0.025, 0.1 * s + 0.025, -0.128, -0.098, 1.02, 1.39, koyu)

    # Boyun, baş, yüz
    g.kemik = "Head"
    pahli_kutu(g, (0.0, 0.0, 1.43), (0.05, 0.05, 0.04), 0.01, ten)
    pahli_kutu(g, (0.0, 0.0, 1.6), (0.13, 0.135, 0.15), 0.035, ten)
    for s in (1, -1):
        on_dortgen(g, 0.035 * s - 0.018, 0.035 * s + 0.018, -0.1365, 1.585, 1.635, goz)
    sac_on = lambda: pahli_kutu(g, (0.0, -0.12, 1.715), (0.137, 0.025, 0.035), 0.012, sac)
    sac_ust = lambda: pahli_kutu(g, (0.0, 0.01, 1.74), (0.137, 0.142, 0.03), 0.02, sac)
    sac_arka = lambda: pahli_kutu(g, (0.0, 0.07, 1.64), (0.137, 0.075, 0.09), 0.02, sac)

    if kurye:
        sac_arka()
        if rnd.random() < 0.7:                                          # kask + vizör
            pahli_kutu(g, (0.0, 0.005, 1.725), (0.148, 0.152, 0.075), 0.04, ikinci)
            pahli_kutu(g, (0.0, -0.155, 1.672), (0.125, 0.028, 0.02), 0.01, M("Vizor", "#2a3440"))
        else:                                                           # şirket şapkası
            pahli_kutu(g, (0.0, 0.005, 1.735), (0.138, 0.143, 0.05), 0.03, ust)
            pahli_kutu(g, (0.0, -0.17, 1.7), (0.11, 0.06, 0.012), 0.006, ust)
    else:
        model = rnd.randrange(6)
        if model in (0, 1, 2):
            sac_ust(); sac_arka(); sac_on()
            if model == 1:
                pahli_kutu(g, (0.0, 0.1, 1.5), (0.14, 0.05, 0.18), 0.02, sac)          # uzun saç
                for s in (1, -1):
                    pahli_kutu(g, (0.137 * s, 0.03, 1.56), (0.02, 0.09, 0.13), 0.01, sac)
            elif model == 2:
                pahli_kutu(g, (0.0, 0.08, 1.8), (0.06, 0.06, 0.05), 0.02, sac)          # topuz
        elif model == 3:
            pahli_kutu(g, (0.0, 0.01, 1.745), (0.133, 0.138, 0.012), 0.008, sac)       # çok kısa
            sac_arka()
        elif model == 4:                                                              # şapka
            sac_arka()
            sapka = M("Sapka", rnd.choice(USTLER))
            pahli_kutu(g, (0.0, 0.005, 1.735), (0.138, 0.143, 0.05), 0.03, sapka)
            pahli_kutu(g, (0.0, -0.17, 1.7), (0.11, 0.06, 0.012), 0.006, sapka)
        else:                                                                         # kıvırcık / gür
            pahli_kutu(g, (0.0, 0.025, 1.72), (0.16, 0.165, 0.08), 0.05, sac)
            pahli_kutu(g, (0.0, 0.09, 1.6), (0.15, 0.06, 0.08), 0.04, sac)
        if model in (0, 3, 4, 5) and rnd.random() < 0.18:                             # sakal
            pahli_kutu(g, (0.0, -0.118, 1.5), (0.12, 0.03, 0.05), 0.015, sac)

    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * 0.5)     # karakterler oyunda binalara göre büyük çizildiği için yarı kalınlık
    return g, 0.6


# ---------- animasyon (Blender'sız da hesaplanır: önizleme aynı pozları çizer)

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


def poz(klip, t):
    """
    t (0–1) anındaki poz: kemik → (iskelet eksenlerinde dönüş matrisi, kalça kayması).
    İleri adım = X ekseni etrafında eksi açı (karakter −Y'ye bakıyor).
    """
    d = math.radians
    phi = 2 * math.pi * t
    R = {b: rot_x(0.0) for b in KEMIKLER}
    kay = (0.0, 0.0, 0.0)
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
        R["Spine"] = mat_mul(rot_z(-d(6) * s), rot_x(d(4)))        # hafif öne eğik (yukarı bakan kemikte artı açı öne)
        R["Head"] = rot_x(-d(3))
    else:   # idle: nefes alma, hafif sallanma, etrafa bakma
        s = math.sin(phi)
        R["Spine"] = rot_x(d(1.5) * s)
        R["LeftArm"] = rot_x(d(2.5) * s)
        R["RightArm"] = rot_x(d(2.5) * math.sin(phi + 0.6))
        R["LeftForeArm"] = rot_x(-d(8))
        R["RightForeArm"] = rot_x(-d(8))
        R["Head"] = rot_z(d(7) * math.sin(phi + 1.0))
    # Kalça yüksekliği: yere basan ayak tam zemine değsin (adım açıldıkça kalça iner, doğal iniş-çıkış)
    taban = []
    for b, x in (("LeftLeg", 0.1), ("RightLeg", -0.1)):
        for y in (0.085, -0.155):
            taban.append(fk_nokta(R, (0.0, 0.0, 0.0), b, (x, y, 0.0))[2])
    kay = (0.0, 0.0, -min(taban))
    return R, kay


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


def poz_uygula(g, klip, t):
    """Önizleme için: köşeleri pozdaki yerlerine taşır (Blender'daki kemik hesabının aynısı)."""
    R, kay = poz(klip, t)
    g.v = [fk_nokta(R, kay, b, p) if b else p for p, b in zip(g.v, g.vb)]



def uret():
    sonuc = []
    for i in range(max(1, VARYANT)):
        rnd = random.Random(TOHUM * 1000 + i)
        if MODEL == "ofis_kulesi":
            g, gen = ofis(rnd, OFIS_STIL)
            sonuc.append((f"OfisKulesi_{OFIS_STIL}_{TOHUM}_{i + 1}", g, gen))
        elif MODEL in ("kurye", "yaya"):
            g, gen = karakter(rnd, MODEL == "kurye")
            sonuc.append((f"{'Kurye' if MODEL == 'kurye' else 'Yaya'}_{TOHUM}_{i + 1}", g, gen))
        else:
            g, gen = bina(rnd, STIL)
            sonuc.append((f"SiraEv_{STIL}_{TOHUM}_{i + 1}", g, gen))
    return sonuc


# ------------------------------------------------------------------ Blender tarafı

def srgb_lineer(hexv):
    out = []
    for i in (1, 3, 5):
        c = int(hexv[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return out


def malzeme(ad):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    r, g, b = srgb_lineer(HEX[ad])
    m.diffuse_color = (r, g, b, 1.0)
    m.use_backface_culling = True          # çizgi kabuğu ancak arka yüzler gizliyken görünür
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = 1.0 if ad.startswith("Cizgi") else 0.85
    return m


def koleksiyon():
    ad = "KuryeBinalar"
    k = bpy.data.collections.get(ad)
    if k is None:
        k = bpy.data.collections.new(ad)
    if k.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(k)
    for o in list(k.objects):
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
    return k


def blender_obje(g, ad, k):
    me = bpy.data.meshes.new(ad)
    me.from_pydata(g.v, [], g.f)
    for m in g.mats:
        me.materials.append(malzeme(m))
    me.polygons.foreach_set("material_index", g.fm)
    me.update()
    ob = bpy.data.objects.new(ad, me)
    k.objects.link(ob)
    return ob


def fbx_kaydet(ob, yol):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    eski = tuple(ob.location)
    ob.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=yol, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, add_leaf_bones=False)
    ob.location = eski


def gorunum():
    """
    Bütün çalışma alanlarındaki (Layout, Scripting, Modeling...) 3D görünümlerde malzeme renkleri ve
    arka yüz gizleme açık olsun. Arka yüz gizleme kapalıyken siyah çizgi kabuğu binanın üstünü örter, bina simsiyah görünür.
    """
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type != 'VIEW_3D':
                continue
            for sp in area.spaces:
                if sp.type == 'VIEW_3D':
                    if sp.shading.type == 'WIREFRAME':
                        sp.shading.type = 'SOLID'
                    sp.shading.color_type = 'MATERIAL'
                    sp.shading.show_backface_culling = True


def mesaj(satirlar):
    def ciz(self, context):
        for s in satirlar:
            self.layout.label(text=s)
    bpy.context.window_manager.popup_menu(ciz, title="Kurye binaları", icon='INFO')




# ------------------------------------------------------------------ Blender: iskelet, deri, animasyon

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
    """Her parça tek kemiğe bağlı (ağırlık 1): parçalı oyuncak karakter gibi hareket eder."""
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
        ad.action_slot = act.slots[0]      # Blender 4.4+ (slotlu aksiyonlar)


def animasyonlari_kur(arm):
    """Yürüme ve bekleme aksiyonları bir kez kurulur, bütün karakterler aynısını kullanır."""
    from mathutils import Matrix, Vector
    arm.animation_data_create()
    for klip, n in ANIMASYONLAR.items():
        eski = bpy.data.actions.get(klip)
        if eski is not None:
            bpy.data.actions.remove(eski)
        act = bpy.data.actions.new(klip)
        act.use_fake_user = True
        arm.animation_data.action = act
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


def fbx_karakter(arm, ob, yol):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.select_set(True)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = arm
    eski_ad, eski_yer = arm.name, tuple(arm.location)
    arm.location = (0.0, 0.0, 0.0)
    arm.name = "Iskelet"          # bütün dosyalarda aynı ad: oyun tek animasyonu hepsine takabilsin
    bpy.context.view_layer.update()
    try:
        bpy.ops.export_scene.fbx(filepath=yol, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                                 bake_space_transform=False, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                                 add_leaf_bones=False, use_armature_deform_only=False,
                                 bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
                                 bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
                                 bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    finally:
        arm.name = eski_ad
        arm.location = eski_yer


def main():
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    k = koleksiyon()
    klasor = os.path.expanduser(KLASOR)
    if DISA_AKTAR:
        os.makedirs(klasor, exist_ok=True)
    x, satir = 0.0, []
    karakter_mi = MODEL in ("kurye", "yaya")
    ilk_iskelet = None
    for ad, g, gen in uret():
        ob = blender_obje(g, ad, k)
        if karakter_mi:
            arm = iskelet_kur(ad + "_Iskelet", k)
            deri_bagla(ob, arm, g)
            if ilk_iskelet is None:
                animasyonlari_kur(arm)
                ilk_iskelet = arm
            else:
                aksiyon_tak(arm, bpy.data.actions["basic walk"])
            arm.location = (x, 0.0, 0.0)
            x += 1.0
            if DISA_AKTAR:
                try:
                    fbx_karakter(arm, ob, os.path.join(klasor, ad + ".fbx"))
                except Exception as e:      # hata olursa en azından ne olduğunu göster
                    satir.append(f"{ad}: FBX hatası: {e}")
        else:
            ob.location = (x + gen / 2, 0.0, 0.0)
            x += gen + 4.0
            if DISA_AKTAR:
                fbx_kaydet(ob, os.path.join(klasor, ad + ".fbx"))
        satir.append(f"{ad}: {g.ucgen_sayisi()} üçgen")
    gorunum()
    if karakter_mi:
        satir.append("Yürüme animasyonunu görmek için Space (oynat). Bekleme: aksiyonu \"idle\" yap.")
    if DISA_AKTAR:
        satir.append(f"FBX: {klasor}")
    print("\n".join(satir))
    mesaj(satir)


if IN_BLENDER and __name__ == "__main__":
    main()
