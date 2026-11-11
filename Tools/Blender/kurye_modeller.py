# -*- coding: utf-8 -*-
"""
KURYE ŞİRKETİ TYCOON · MODEL ÜRETİCİ (Blender scripti)
  • Binalar: sıra ev (tuğla / sıva), ofis kulesi (high-tech: kuleler, borular, cam cephe, cam tonoz)
    ve müstakil ev (Avrupa: kiremit beşik çatı, panjurlu pencereler, çitli bahçe, ağaçlar)
  • Karakterler: kurye (şirket renginde mont, termal çanta, kask) ve yaya (renk, saç, kıyafet çeşitleri);
    iskeletli, "basic walk" ve "idle" animasyonları FBX'in içinde

Kullanım:
  1. Blender'ı aç (3.6 ya da 4.x), üstten "Scripting" sekmesine geç.
  2. Text > Open ile bu dosyayı aç (ya da yeni metin açıp içine yapıştır).
  3. İstersen aşağıdaki AYARLAR kısmını değiştir, sonra "Run Script" (▶) ya da Alt+P.
  4. Binalar "KuryeBinalar" koleksiyonunda yan yana çıkar, her biri FBX olarak KLASOR'e kaydedilir.
     Tekrar çalıştırınca eski binalar silinir, yenileri üretilir.

Unity'ye almak:
  • Binalar → FBX'ler Assets/KuryeTycoon/Modeller/Binalar'a kaydedilir. Unity'de "Kurye Tycoon > Blender Binalarını
    Prefab Yap" prefabları Resources/SehirPaketi/Binalar'a kurar (malzemeler ve siyah çizgi dahil; oyun ölçeği ve yönü kendisi ayarlar)
  • Kuryeler → Assets/Resources/KuryeKarakterleri, sonra o klasörü seçip "Kurye Tycoon > Karakter Animasyonlarını Kur"
  • Yayalar → Assets/Resources/SehirPaketi/Insanlar
  • Kurye/yaya FBX'lerinde "Cizgi" malzemesinin shader'ını Universal Render Pipeline/Unlit yap, rengi siyah kalsın
    (binalarda prefab aracı bunu kendisi yapar).
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
MODEL = "mustakil_ev"      # "sira_ev" · "ofis_kulesi" · "mustakil_ev" · "market" · "fastfood" · "apartman" · "dukkan" (altı dükkanlı binalar) · "sehir" (yollar, zemin, ağaçlar, parklar) · "katalog" (konut, büro, sanayi, kamu, yeni parklar) · "kurye" · "yaya"
STIL = "kirmizi_tugla"     # sıra ev için: "kirmizi_tugla" · "pembe_siva" · "kahve_siva" · "karisik"
OFIS_STIL = "beton"        # ofis kulesi için: "beton" · "gri" · "beyaz" · "karisik"
OFIS_KAT = (5, 7)          # ofis kulesinin kat sayısı (zemindeki lobi hariç)
EV_STIL = "avrupa"         # müstakil ev için: "avrupa"
EV_TIP = "karisik"         # müstakil ev gövdesi: "sade" · "alinli" (sokağa üçgen alınlık) · "L" (önde kanat) · "karisik"
EV_KAT = (1, 2)            # müstakil evin kat sayısı (çatı katı hariç)
CIT = "karisik"            # bahçe çiti: "beyaz" · "ahsap" · "cali" · "tas" · "karisik"
MARKET_TIP = "karisik"     # market: "szpar_express" (küçük) · "market_24" (altı market, üstü daire) · "interszpar" (hipermarket) · "karisik"
FASTFOOD_MARKA = "karisik" # fast food: "burger_queen" · "mcdanolds" · "karisik"
APT_KAT = (4, 6)           # apartmanın kat sayısı
KARAKTER_STIL = "yuvarlak" # kurye / yaya için: "yuvarlak" (kapsül ve elipsler) · "blok" (köşeli bloklar)
VARYANT = 3                # kaç farklı bina üretilsin (yan yana dizilir, her biri ayrı FBX)
TOHUM = 1                  # aynı sayı = aynı binalar; değiştirince yeni varyantlar çıkar
EV_SAYISI = 2              # bir binada yan yana kaç ev (1–3)
KAT_ARALIGI = (3, 4)       # her evin kat sayısı, zemin kat dahil (en az, en çok)
CIZGI = True               # karikatür dış çizgisi (üçgen sayısını yaklaşık ikiye katlar)
CIZGI_KALINLIK = 0.03      # metre (ince karikatür çizgisi)
ON_BAHCE = True            # önde kaldırım, çöp kutuları, bazen ağaç
YAN_PENCERE = True         # yan ve arka cephelerde de pencere
DISA_AKTAR = True          # FBX olarak kaydet
LOD = True                 # her modelin bir de hafif sürümünü (_Hafif: detaysız, çizgisiz) kaydet; Unity uzakta onu gösterir
DETAY = True               # küçük detaylar (oluk, klima, anten, panel, lamba); LOD1 üretilirken kapanır
KLASOR = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Modeller\Binalar"
YEDEK_KLASOR = "~/Desktop/KuryeBinalar"   # KLASOR bu bilgisayarda yoksa buraya kaydedilir
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
        self.yum = []                       # yüz başına yumuşak gölge (yuvarlak parçalar)

    def mat(self, ad):
        if ad not in self._mi:
            self._mi[ad] = len(self.mats)
            self.mats.append(ad)
        return self._mi[ad]

    def yuz(self, idx, mat, kabuk=True, yumusak=False):
        if len(self.vb) < len(self.v):
            self.vb += [self.kemik] * (len(self.v) - len(self.vb))
        self.f.append(list(idx))
        self.fm.append(self.mat(mat))
        self.kabuksuz.append(not kabuk)
        self.yum.append(yumusak)

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


def dis_bukey(g, pts, yuzler, mat, kabuk=True, yumusak=False):
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
        g.yuz([b + i for i in face], mat, kabuk, yumusak)


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


def silindir(g, cx, cy, z0, h, r, n, mat, kabuk=True):
    pts = []
    for z in (z0, z0 + h):
        for i in range(n):
            a = 2 * math.pi * i / n
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a), z))
    yuzler = [list(range(n)), list(range(n, 2 * n))]
    for i in range(n):
        j = (i + 1) % n
        yuzler.append([i, j, n + j, n + i])
    dis_bukey(g, pts, yuzler, mat, kabuk=kabuk)


ICO_V = None
ICO_F = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6], [7, 1, 8],
         [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]]


ICO2 = None


def _ico2():
    """Bir kez bölünmüş ikosahedron (80 yüz): büyük ağaç tepeleri daha yuvarlak görünsün."""
    global ICO2
    if ICO2 is None:
        vs = list(ICO_V)
        orta = {}

        def mid(i, j):
            k = (min(i, j), max(i, j))
            if k not in orta:
                orta[k] = len(vs)
                vs.append(norm(mul(add(vs[i], vs[j]), 0.5)))
            return orta[k]
        fs = []
        for a, b, c in ICO_F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            fs += [[a, ab, ca], [b, bc, ab], [c, ca, bc], [ab, bc, ca]]
        ICO2 = (vs, fs)
    return ICO2


def top(g, c, sx, sy, sz, mat):
    """Alçak poligon küre (ağaç tepesi, saksı bitkisi): küçükse 20, büyükse 80 yüz."""
    global ICO_V
    if ICO_V is None:
        t = (1 + math.sqrt(5)) / 2
        raw = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
               (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
        ICO_V = [norm(p) for p in raw]
    vs, fs = (_ico2() if DETAY and max(sx, sy, sz) >= 0.4 else (ICO_V, ICO_F))
    pts = [(c[0] + p[0] * sx, c[1] + p[1] * sy, c[2] + p[2] * sz) for p in vs]
    dis_bukey(g, pts, fs, mat)


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
    levha(g, -0.3, genislik + 0.3, -2.3, 0.3, 0.0, 0.05, r["kaldirim"])
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



# ------------------------------------------------------------------ ortak: arsa levhası ve küçük detaylar

def levha(g, x0, x1, y0, y1, z0, z1, mat):
    """Arsa / zemin levhası: dış çizgi yok, kenarlar üst yüzeyle aynı renk (oyunda zemine gömülür)."""
    b = len(g.f)
    kutu(g, x0, x1, y0, y1, z0, z1, mat, atla=("zn",))
    for i in range(b, len(g.f)):
        g.kabuksuz[i] = True


def D(ad, hexv):
    return M(ad, hexv)


def oluk(g, x, y, z0, z1):
    """Yağmur borusu (köşede dikey boru, altta dirsek)."""
    if not DETAY:
        return
    m = D("Oluk", "#8f949a")
    silindir(g, x, y, z0 + 0.15, z1 - z0 - 0.15, 0.055, 6, m)
    kutu(g, x - 0.07, x + 0.07, y - 0.07, y + 0.07, z0 + 0.05, z0 + 0.2, m)


def klima_dis(g, fc, u, z):
    """Duvarda klima dış ünitesi."""
    if not DETAY:
        return
    m, k = D("Klima", "#c9ccd0"), D("Koyu", "#2b2d31")
    ckutu(g, fc, u - 0.4, u + 0.4, 0.05, 0.4, z, z + 0.55, m)
    cdortgen(g, fc, u - 0.3, u + 0.12, 0.405, z + 0.08, z + 0.47, k)
    for du in (-0.32, 0.32):
        ckutu(g, fc, u + du - 0.03, u + du + 0.03, 0, 0.42, z - 0.05, z, k)


def canak_anten(g, fc, u, o, z):
    """Çanak anten (duvara ya da korkuluğa bağlı)."""
    if not DETAY:
        return
    m, k = D("Canak", "#e8e6e0"), D("Koyu", "#2b2d31")
    a, b = fc.P(u, o, z), fc.P(u, o + 0.25, z + 0.05)
    cubuk(g, a, b, 0.04, k)
    ox, oy = fc.out[0], fc.out[1]
    top(g, (b[0] + ox * 0.08, b[1] + oy * 0.08, b[2] + 0.1), 0.32 if ox == 0 else 0.08, 0.32 if oy == 0 else 0.08, 0.3, m)


def kapi_lambasi(g, fc, u, z):
    if not DETAY:
        return
    k, i = D("Koyu", "#2b2d31"), D("Isik", "#f3e3a0")
    ckutu(g, fc, u - 0.08, u + 0.08, 0, 0.12, z, z + 0.25, k)
    ckutu(g, fc, u - 0.06, u + 0.06, 0.12, 0.2, z + 0.03, z + 0.22, i)


def duvar_lambasi(g, fc, u, z):
    """Vitrin üstünde kuğu boyunlu lamba."""
    if not DETAY:
        return
    k, i = D("Koyu", "#2b2d31"), D("Isik", "#f3e3a0")
    cubuk(g, fc.P(u, 0.0, z), fc.P(u, 0.45, z + 0.25), 0.04, k)
    ckutu(g, fc, u - 0.16, u + 0.16, 0.38, 0.62, z + 0.12, z + 0.26, k)
    ckutu(g, fc, u - 0.12, u + 0.12, 0.42, 0.58, z + 0.08, z + 0.12, i)


def gunes_paneli(g, cx, cy, z, w, d, egim=0.45):
    """Düz çatıda eğik güneş paneli (önü −Y'ye, kameraya doğru)."""
    if not DETAY:
        return
    p, k = D("GunesPaneli", "#2f4a6f"), D("Koyu", "#2b2d31")
    c, s = math.cos(egim), math.sin(egim)
    yonlu_kutu(g, (cx, cy, z + 0.35), (w / 2, 0, 0), (0, d / 2 * c, d / 2 * s), (0, -0.03 * s, 0.03 * c), p)
    for dx in (-w / 2 + 0.1, w / 2 - 0.1):
        cubuk(g, (cx + dx, cy + d / 2 * c - 0.05, z), (cx + dx, cy + d / 2 * c - 0.05, z + 0.35 + d / 2 * s), 0.05, k)
    for i in range(1, 3):                                                     # hücre çizgileri
        dx = -w / 2 + i * w / 3
        yonlu_kutu(g, (cx + dx, cy, z + 0.35 + 0.035), (0.015, 0, 0), (0, d / 2 * c, d / 2 * s), (0, -0.005 * s, 0.005 * c), D("Metal", "#a7abb0"), kabuk=False)


def cati_anteni(g, x, y, z):
    if not DETAY:
        return
    k = D("Koyu", "#2b2d31")
    cubuk(g, (x, y, z), (x, y, z + 1.6), 0.05, k)
    for zz, w in ((z + 1.2, 0.5), (z + 1.45, 0.35)):
        cubuk(g, (x - w, y, zz), (x + w, y, zz), 0.03, k, kabuk=False)


def rogar(g, x, y, z):
    if not DETAY:
        return
    k = D("Rogar", "#3f4246")
    b = len(g.v)
    g.v += [(x + 0.45 * math.cos(2 * math.pi * i / 8), y + 0.45 * math.sin(2 * math.pi * i / 8), z + 0.01) for i in range(8)]
    g.yuz(list(range(b, b + 8)), k, kabuk=False)


def izgara(g, x0, x1, y0, y1, z):
    if not DETAY:
        return
    k = D("Rogar", "#3f4246")
    b = len(g.v)
    g.v += [(x0, y0, z + 0.01), (x1, y0, z + 0.01), (x1, y1, z + 0.01), (x0, y1, z + 0.01)]
    g.yuz([b, b + 1, b + 2, b + 3], k, kabuk=False)

# ------------------------------------------------------------------ müstakil ev (Avrupa)

EV_STILLER = {
    "avrupa": dict(
        duvar=["#f1e4c8", "#ecd3a4", "#f0cdb0", "#f4f0e6", "#efdf9e", "#d9dfc6"],
        suslu="#f8f4ea", taban="#b8b0a3",
        cati=["#c0573a", "#b04a33", "#c96a42", "#5b5d63"],          # son renk antrasit (arada bir çıkar)
        kapi=["#7a4a2e", "#3f5f7a", "#5f7f4a", "#8a2f2f", "#4a4a52"],
        panjur=["#5f8a6a", "#4f6f8f", "#8a5a3a", "#f2efe8", "#a8463a"],
        baca=["#c9b9a3", "#a8553f"],
    ),
}

EV_SABIT = dict(cim="#9cc46a", toprak="#8a6a4a", yol="#d8d1c4", yol_koyu="#bdb5a8", cicek=["#e0525a", "#f0a0c0", "#f2d04a", "#ffffff"],
                cit_beyaz="#f2efe8", cit_ahsap="#8a6a4a", cit_tas="#b9b2a6", cali="#5f8f4a", posta="#c8473a")

EV_ZEMIN = 0.08      # çim tabakasının üstü
EV_KAT_YUK = 2.8
EV_SUBASMAN = 0.45   # zemin katın çimden yüksekliği (iki basamak)
EV_EGIM = 0.84       # çatı eğimi (tan 40°)


def koyu_ton(hexv, f=0.78):
    return "#%02x%02x%02x" % tuple(int(int(hexv[i:i + 2], 16) * f) for i in (1, 3, 5))


def ev_renkleri(st, rnd):
    duvar = rnd.choice(st["duvar"])
    cati = st["cati"][-1] if rnd.random() < 0.12 else rnd.choice(st["cati"][:-1])
    return {
        "duvar": M("Duvar", duvar), "suslu": M("Suslu", st["suslu"]), "taban": M("Taban", st["taban"]),
        "cati": M("Cati", cati), "cati_koyu": M("CatiKoyu", koyu_ton(cati)), "kapi": M("Kapi", rnd.choice(st["kapi"])),
        "panjur": M("Panjur", rnd.choice(st["panjur"])), "baca": M("Baca", rnd.choice(st["baca"])),
        "kemer": "bloklu" if rnd.random() < 0.3 else "duz",
        "cam": M("Cam", SABIT["cam"]), "demir": M("Demir", SABIT["demir"]), "saksi": M("Saksi", SABIT["saksi"]),
        "bitki": M("Bitki", SABIT["bitki"]), "govde": M("AgacGovde", SABIT["govde"]),
        "agac": [M("Agac", c) for c in SABIT["agac"]], "tokmak": M("Tokmak", SABIT["tokmak"]),
        "cim": M("Cim", EV_SABIT["cim"]), "toprak": M("Toprak", EV_SABIT["toprak"]),
        "yol": M("Yol", EV_SABIT["yol"]), "yol_koyu": M("YolKoyu", EV_SABIT["yol_koyu"]),
        "cicek": [M("Cicek", c) for c in EV_SABIT["cicek"]], "cali": M("Cali", EV_SABIT["cali"]),
        "cit_beyaz": M("CitBeyaz", EV_SABIT["cit_beyaz"]), "cit_ahsap": M("CitAhsap", EV_SABIT["cit_ahsap"]),
        "cit_tas": M("CitTas", EV_SABIT["cit_tas"]), "posta": M("Posta", EV_SABIT["posta"]),
    }


def _eslem(yon):
    """Çatı hesabı (u sırt boyunca, v sırta dik) → dünya koordinatı."""
    if yon == "x":
        return (lambda u, v, z: (u, v, z)), (lambda du, dv, dz: (du, dv, dz))
    return (lambda u, v, z: (v, u, z)), (lambda du, dv, dz: (dv, du, dz))


def egim_levha(g, yon, u0, u1, a, b, mat, koyu=None, th=0.07, sira=3):
    """Eğik çatı levhası: u0..u1 boyunca, kesitte a=(v,z) noktasından b=(v,z) noktasına. Üstünde kiremit sıraları."""
    P, V = _eslem(yon)
    dv, dz = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dv, dz)
    n = (-dz / L, dv / L) if dv > 0 else (dz / L, -dv / L)
    if n[1] < 0:
        n = (-n[0], -n[1])
    mid = P((u0 + u1) / 2, (a[0] + b[0]) / 2 + n[0] * th, (a[1] + b[1]) / 2 + n[1] * th)
    yonlu_kutu(g, mid, V((u1 - u0) / 2, 0.0, 0.0), V(0.0, dv / 2, dz / 2), V(0.0, n[0] * th, n[1] * th), mat)
    if koyu:
        for k in range(sira):
            f = (k + 0.6) / (sira + 0.4)
            pv, pz = a[0] + dv * f + n[0] * (2 * th + 0.02), a[1] + dz * f + n[1] * (2 * th + 0.02)
            cubuk(g, P(u0 + 0.06, pv, pz), P(u1 - 0.06, pv, pz), 0.05, koyu)


def besik_cati(g, yon, u0, u1, v0, v1, zt, r, sacak=0.38, yan_sacak=0.3, bosluk=None, alin=True):
    """
    Beşik çatı. u0..u1 sırt boyunca, v0..v1 duvardan duvara, zt saçak (duvar üstü) yüksekliği.
    bosluk=(ua, ub): ön eğimde bu aralığın saçağı kesilir (önüne çıkan kanat için).
    Dönüş: sırt yüksekliği.
    """
    P, V = _eslem(yon)
    vc, yari = (v0 + v1) / 2, (v1 - v0) / 2
    rise = yari * EV_EGIM
    zr = zt + rise
    ea = (v0 - sacak, zt - sacak * EV_EGIM)
    eb = (v1 + sacak, zt - sacak * EV_EGIM)
    tepe = (vc, zr)
    U0, U1 = u0 - yan_sacak, u1 + yan_sacak
    if bosluk:
        ua, ub = bosluk
        ic = (v0 + 0.02, zt + 0.02 * EV_EGIM)
        if ua > U0:
            egim_levha(g, yon, U0, ua, ea, tepe, r["cati"], r["cati_koyu"])
        egim_levha(g, yon, ua, ub, ic, tepe, r["cati"], r["cati_koyu"], sira=2)
        if ub < U1:
            egim_levha(g, yon, ub, U1, ea, tepe, r["cati"], r["cati_koyu"])
    else:
        egim_levha(g, yon, U0, U1, ea, tepe, r["cati"], r["cati_koyu"])
    egim_levha(g, yon, U0, U1, tepe, eb, r["cati"], r["cati_koyu"])
    cubuk(g, P(U0 - 0.02, vc, zr + 0.16), P(U1 + 0.02, vc, zr + 0.16), 0.16, r["cati_koyu"])      # mahya
    if alin:
        # Üçgen alınlık duvarları (iki uçta) ve kenar tahtaları
        # Çatının altında kalan iç kısım çizilmez: sadece iki uçta ince üçgen duvar
        tri = [(v0, zt), (v1, zt), (vc, zr - 0.02)]
        for ua, ub in ((u0, u0 + 0.25), (u1 - 0.25, u1)):
            pts = [P(ua, v, z) for v, z in tri] + [P(ub, v, z) for v, z in tri]
            dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["duvar"])
        for uu in (U0 + 0.05, U1 - 0.05):
            for s in (ea, eb):
                cubuk(g, P(uu, s[0], s[1] + 0.02), P(uu, vc, zr + 0.08), 0.1, r["suslu"])
    return zr


def cati_z(zt, v0, v1, v):
    """Beşik çatının v noktasındaki üst yüzey yüksekliği (yaklaşık)."""
    vc, yari = (v0 + v1) / 2, (v1 - v0) / 2
    return zt + (yari - abs(v - vc)) * EV_EGIM


def ev_pencere(g, fc, cu, zb, w, h, r, rnd, panjur=False, saksi=False):
    pencere(g, fc, cu, zb, w, h, r, sade=r["kemer"] == "yok")
    if panjur:
        pw = w * 0.5 + 0.02
        for s in (-1, 1):
            a = cu + s * (w / 2 + 0.11)
            b = a + s * pw
            ckutu(g, fc, min(a, b), max(a, b), 0, 0.06, zb, zb + h + 0.06, r["panjur"])
            ckutu(g, fc, min(a, b) + 0.06, max(a, b) - 0.06, 0.06, 0.075, zb + h * 0.5 - 0.03, zb + h * 0.5 + 0.03,
                  r["panjur"])
    if saksi:
        z1 = zb - 0.1
        ckutu(g, fc, cu - w / 2, cu + w / 2, 0.05, 0.3, z1 - 0.22, z1 - 0.02, r["saksi"])
        n = max(2, int(w / 0.3))
        for i in range(n):
            u = cu - w / 2 + 0.15 + (w - 0.3) * i / (n - 1)
            p = fc.P(u, 0.18, z1 + 0.04)
            top(g, p, 0.13, 0.11, 0.11, rnd.choice(r["cicek"]) if i % 2 == 0 else r["bitki"])


def ev_kapi(g, fc, cu, zb, r, rnd, sundurma):
    T = r["suslu"]
    ckutu(g, fc, cu - 0.5, cu + 0.5, 0, 0.05, zb, zb + 2.15, r["kapi"])
    ckutu(g, fc, cu - 0.36, cu + 0.36, 0.05, 0.07, zb + 1.2, zb + 1.95, r["kapi"])       # kapı paneli
    cdortgen(g, fc, cu - 0.22, cu + 0.22, 0.072, zb + 1.4, zb + 1.85, r["cam"])
    ckutu(g, fc, cu + 0.3, cu + 0.37, 0.05, 0.11, zb + 0.95, zb + 1.03, r["tokmak"])
    ckutu(g, fc, cu - 0.62, cu - 0.5, 0, 0.1, zb, zb + 2.27, T)
    ckutu(g, fc, cu + 0.5, cu + 0.62, 0, 0.1, zb, zb + 2.27, T)
    ckutu(g, fc, cu - 0.62, cu + 0.62, 0, 0.1, zb + 2.15, zb + 2.27, T)
    # Basamaklar
    n = 2
    for k in range(n):
        ckutu(g, fc, cu - 0.75 - 0.08 * k, cu + 0.75 + 0.08 * k, 0, 0.45 + 0.32 * k,
              EV_ZEMIN, EV_ZEMIN + (zb - EV_ZEMIN) * (n - k) / n, r["taban"], atla=("zn",))
    if sundurma == "ucgen":
        # Küçük üçgen saçak, iki konsol üstünde
        z0 = zb + 2.4
        for du in (-0.72, 0.72):
            ckutu(g, fc, cu + du - 0.05, cu + du + 0.05, 0, 0.55, z0 - 0.3, z0, T)
        ckutu(g, fc, cu - 0.85, cu + 0.85, 0, 0.7, z0, z0 + 0.08, T)
        cprizma(g, fc, [(cu - 0.95, z0 + 0.08), (cu + 0.95, z0 + 0.08), (cu, z0 + 0.62)], 0.0, 0.78, r["cati"])
    elif sundurma == "direkli":
        # İki direkli giriş saçağı (küçük veranda)
        z0 = zb + 2.45
        for du in (-0.85, 0.85):
            p0, p1 = fc.P(cu + du, 1.15, EV_ZEMIN), fc.P(cu + du, 1.15, z0)
            cubuk(g, p0, p1, 0.12, T)
        ckutu(g, fc, cu - 1.0, cu + 1.0, 0, 1.3, z0, z0 + 0.12, T)
        a, b = fc.P(cu - 1.1, 1.45, z0 + 0.12), fc.P(cu + 1.1, 0.0, z0 + 0.5)
        yonlu_kutu(g, mul(add(a, b), 0.5), mul(sub(fc.P(cu + 1.1, 0, 0), fc.P(cu - 1.1, 0, 0)), 0.5),
                   mul(sub(fc.P(0, 0.0, z0 + 0.5), fc.P(0, 1.45, z0 + 0.12)), 0.5),
                   (0.0, 0.0, 0.06), r["cati"])
    else:
        ckutu(g, fc, cu - 0.8, cu + 0.8, 0, 0.55, zb + 2.4, zb + 2.5, T)


def ev_baca(g, x, y, z0, z1, r):
    kutu(g, x - 0.32, x + 0.32, y - 0.32, y + 0.32, z0, z1, r["baca"])
    kutu(g, x - 0.4, x + 0.4, y - 0.4, y + 0.4, z1, z1 + 0.1, r["suslu"])
    kutu(g, x - 0.22, x + 0.22, y - 0.22, y + 0.22, z1 + 0.1, z1 + 0.22, r["cati_koyu"])


def cephe_pencereleri(g, fc, L, zb, kat, r, rnd, panjur, saksi_olas, kapi_u=None, atla=(), w=0.85, h=1.35):
    """Bir duvar boyunca kat kat pencere. atla: pencere konmayacak u aralıkları."""
    n = max(1, int((L - 0.6) / (2.3 if panjur else 1.75)))
    yer = [L * (i + 0.5) / n for i in range(n)]
    for f in range(kat):
        z = zb + f * EV_KAT_YUK + 0.8
        for u in yer:
            if any(a - 0.7 < u < b + 0.7 for a, b in atla):
                continue
            if f == 0 and kapi_u is not None and abs(u - kapi_u) < 1.3:
                continue
            ev_pencere(g, fc, u, z, w, h, r, rnd, panjur, saksi=rnd.random() < saksi_olas)


def ev_govde(g, x0, x1, y0, y1, zb, Ht, r, atla=("zn",)):
    kutu(g, x0, x1, y0, y1, EV_ZEMIN, Ht, r["duvar"], atla=atla)
    kutu(g, x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, EV_ZEMIN, zb, r["taban"], atla=("zn",))      # subasman
    kutu(g, x0 - 0.04, x1 + 0.04, y0 - 0.04, y1 + 0.04, Ht - 0.16, Ht, r["suslu"], atla=("zn", "zp"))   # saçak bandı


def mustakil_ev(rnd, stil):
    g = Govde()
    st = EV_STILLER[stil]
    r = ev_renkleri(st, rnd)
    tip = EV_TIP if EV_TIP != "karisik" else rnd.choice(["sade", "alinli", "L", "L"])
    kat = rnd.randint(EV_KAT[0], EV_KAT[1])
    if tip == "alinli":
        W, D = rnd.uniform(6.0, 7.0), rnd.uniform(7.5, 8.5)
    else:
        W, D = rnd.uniform(7.4, 8.6), rnd.uniform(5.8, 6.6)
    on_bahce, arka_bahce, yan = rnd.uniform(3.2, 3.8), 2.2, rnd.uniform(1.6, 2.2)
    PW = W + 2 * yan
    if tip == "L":
        on_bahce += 1.6
    PD = on_bahce + D + arka_bahce
    x0 = yan
    fy, by = on_bahce, on_bahce + D
    x1 = x0 + W
    zb = EV_ZEMIN + EV_SUBASMAN
    Ht = zb + kat * EV_KAT_YUK + 0.15
    panjur = rnd.random() < 0.65

    # Arsa: çim (çizgisiz, kenarları da çim: oyunda zemine gömülü durur, tabla gibi görünmez)
    levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["cim"])

    on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    arka = Cephe((x1, by, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
    sol = Cephe((x0, by, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    sag = Cephe((x1, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))

    ev_govde(g, x0, x1, fy, by, zb, Ht, r)
    for f in range(1, kat):
        ckutu(g, on, -0.03, W + 0.03, 0, 0.06, zb + f * EV_KAT_YUK - 0.05, zb + f * EV_KAT_YUK + 0.07, r["suslu"])

    sundurma = rnd.choice(["ucgen", "direkli", "duz"])
    kapi_x = None
    nd = -1
    if tip == "sade":
        kapi_u = W * rnd.choice([0.5, 0.36, 0.64])
        ev_kapi(g, on, kapi_u, zb, r, rnd, sundurma)
        cephe_pencereleri(g, on, W, zb, kat, r, rnd, panjur, 0.5, kapi_u)
        zr = besik_cati(g, "x", x0, x1, fy, by, Ht, r)
        # Çatı pencereleri (tek katlıda daha çok)
        nd = 2 if kat == 1 else rnd.choice([0, 1, 2])
        for i in range(nd):
            cx = x0 + W * ((i + 1) / (nd + 1))
            yd = fy + 0.9
            zs = cati_z(Ht, fy, by, yd)
            kutu(g, cx - 0.65, cx + 0.65, yd, fy + D / 2 - 0.2, zs - 0.4, zs + 0.95, r["duvar"], atla=("zn", "yp"))
            dfc = Cephe((cx - 0.65, yd, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
            pencere(g, dfc, 0.65, zs + 0.22, 0.7, 0.58, r, sade=True)
            besik_cati(g, "y", yd, fy + D / 2 - 0.3, cx - 0.65, cx + 0.65, zs + 0.95, r, sacak=0.15, yan_sacak=0.12)
        kapi_x = x0 + kapi_u
    elif tip == "alinli":
        kapi_u = W * rnd.choice([0.3, 0.7])
        ev_kapi(g, on, kapi_u, zb, r, rnd, sundurma)
        cephe_pencereleri(g, on, W, zb, kat, r, rnd, panjur, 0.6, kapi_u)
        zr = besik_cati(g, "y", fy, by, x0, x1, Ht, r)
        # Alınlıkta çatı katı penceresi (bazen yuvarlak yerine iki küçük)
        if rnd.random() < 0.5:
            pencere(g, on, W / 2, Ht + 0.35, 0.8, 1.0, r, sade=True)
        else:
            for du in (-0.55, 0.55):
                pencere(g, on, W / 2 + du, Ht + 0.3, 0.5, 0.8, r, sade=True)
        kapi_x = x0 + kapi_u
    else:   # "L": önde sokağa alınlık veren kanat
        WW = rnd.uniform(3.2, 3.7)
        sagda = rnd.random() < 0.5
        wx0 = x1 - WW - 0.4 if sagda else x0 + 0.4
        wx1 = wx0 + WW
        Pj = 1.8
        wy0 = fy - Pj
        kutu(g, wx0, wx1, wy0, fy + 0.02, EV_ZEMIN, Ht, r["duvar"], atla=("zn", "yp"))
        kutu(g, wx0 - 0.05, wx1 + 0.05, wy0 - 0.05, fy, EV_ZEMIN, zb, r["taban"], atla=("zn", "yp"))
        kutu(g, wx0 - 0.04, wx1 + 0.04, wy0 - 0.04, fy, Ht - 0.16, Ht, r["suslu"], atla=("zn", "zp", "yp"))
        kfc = Cephe((wx0, wy0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        for f in range(kat):
            z = zb + f * EV_KAT_YUK + 0.8
            if f == 0 and rnd.random() < 0.6:
                # Zemin katta geniş üçlü pencere
                for du in (-0.95, 0.0, 0.95):
                    ev_pencere(g, kfc, WW / 2 + du, z, 0.75, 1.45, r, rnd)
            else:
                ev_pencere(g, kfc, WW / 2, z, 0.95, 1.35, r, rnd, panjur, saksi=rnd.random() < 0.6)
        pencere(g, kfc, WW / 2, Ht + 0.35, 0.6, 0.85, r, sade=True)
        # Kanadın yan duvarında pencere
        yfc = Cephe((wx1, wy0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)) if not sagda else \
            Cephe((wx0, fy, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
        ev_pencere(g, yfc, Pj / 2, zb + 0.8, 0.6, 1.3, r, rnd)
        # Ana gövdenin önü: kanadın yanındaki kısım
        kalan0, kalan1 = (0.0, wx0 - x0) if sagda else (wx1 - x0, W)
        kapi_u = kalan1 - 1.0 if sagda else kalan0 + 1.0
        ev_kapi(g, on, kapi_u, zb, r, rnd, "duz" if sundurma == "direkli" else sundurma)
        cephe_pencereleri(g, on, W, zb, kat, r, rnd, panjur, 0.5, kapi_u, atla=[(wx0 - x0, wx1 - x0)])
        zr = besik_cati(g, "x", x0, x1, fy, by, Ht, r, bosluk=(wx0 - 0.3, wx1 + 0.3))
        besik_cati(g, "y", wy0, fy + D / 2 - 0.2, wx0, wx1, Ht, r, yan_sacak=0.3)
        kapi_x = x0 + kapi_u

    # Arka ve yan cepheler (sade pencereler)
    cephe_pencereleri(g, arka, W, zb, kat, r, rnd, False, 0.0)
    for fc in (sol, sag):
        cephe_pencereleri(g, fc, D, zb, kat, r, rnd, panjur and rnd.random() < 0.5, 0.0)

    # Baca: sırtın arkasında, sırtı aşacak boyda
    if tip == "alinli":
        bx, byy = x0 + W / 2 + rnd.choice([-1, 1]) * 0.9, by - 1.6
        ev_baca(g, bx, byy, cati_z(Ht, x0, x1, bx) - 0.3, zr + 0.4, r)
    else:
        bx = x0 + W * rnd.choice([0.22, 0.78])
        byy = fy + D / 2 + 0.55
        ev_baca(g, bx, byy, cati_z(Ht, fy, by, byy) - 0.3, zr + 0.45, r)

    ev_bahce(g, rnd, r, PW, PD, kapi_x, x0, x1, fy, by, tip)

    # Detaylar: yağmur boruları, ön oluk, kapı lambası, çanak anten, güneş paneli
    for xx in (x0 - 0.07, x1 + 0.07):
        oluk(g, xx, fy - 0.07, EV_ZEMIN, Ht - 0.15)
    if tip != "alinli" and DETAY:
        cubuk(g, (x0 - 0.3, fy - 0.45, Ht - 0.33), (x1 + 0.3, fy - 0.45, Ht - 0.33), 0.1, M("Oluk", "#8f949a"))
    kapi_lambasi(g, on, kapi_x - x0 + 0.85, zb + 1.9)
    if rnd.random() < 0.45:
        canak_anten(g, sag, D * 0.3, 0.0, Ht - 0.7)
    if tip == "sade" and nd == 0 and DETAY:
        e = EV_EGIM
        c, s = 1 / math.sqrt(1 + e * e), e / math.sqrt(1 + e * e)
        for px in (x0 + W * 0.3, x0 + W * 0.62):
            v = fy + D * 0.27
            yonlu_kutu(g, (px, v, cati_z(Ht, fy, by, v) + 0.17), (1.0, 0, 0), (0, 0.85 * c, 0.85 * s), (0, -0.03 * s, 0.03 * c),
                       M("GunesPaneli", "#2f4a6f"))

    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    gen = ortala(g)
    return g, gen


def ev_bahce(g, rnd, r, PW, PD, kapi_x, x0, x1, fy, by, tip):
    z = EV_ZEMIN
    # Bahçe yolu: kapıdan sokağa taşlar
    yol_y1 = fy
    n = max(3, int(yol_y1 / 0.62))
    for i in range(n):
        y = 0.12 + i * (yol_y1 - 0.4) / max(1, n - 1)
        renk = r["yol"] if i % 2 == 0 else r["yol_koyu"]
        kutu(g, kapi_x - 0.55, kapi_x + 0.55, y, y + 0.48, z, z + 0.035, renk, atla=("zn",))

    # Çit
    tur = CIT if CIT != "karisik" else rnd.choice(["beyaz", "cali", "tas", "ahsap"])
    gap0, gap1 = kapi_x - 0.75, kapi_x + 0.75
    e = 0.15          # arsa kenarından içeri

    def cizgi_cit(p0, p1, on_cephe):
        L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
        if L < 0.3:
            return
        dx, dy = (p1[0] - p0[0]) / L, (p1[1] - p0[1]) / L
        if tur == "cali":
            kutu(g, min(p0[0], p1[0]) - 0.22 * abs(dy), max(p0[0], p1[0]) + 0.22 * abs(dy),
                 min(p0[1], p1[1]) - 0.22 * abs(dx), max(p0[1], p1[1]) + 0.22 * abs(dx), z, z + 0.85, r["cali"], atla=("zn",))
        elif tur == "tas":
            kutu(g, min(p0[0], p1[0]) - 0.17 * abs(dy), max(p0[0], p1[0]) + 0.17 * abs(dy),
                 min(p0[1], p1[1]) - 0.17 * abs(dx), max(p0[1], p1[1]) + 0.17 * abs(dx), z, z + 0.5, r["cit_tas"], atla=("zn",))
            kutu(g, min(p0[0], p1[0]) - 0.21 * abs(dy), max(p0[0], p1[0]) + 0.21 * abs(dy),
                 min(p0[1], p1[1]) - 0.21 * abs(dx), max(p0[1], p1[1]) + 0.21 * abs(dx), z + 0.5, z + 0.58, r["suslu"], atla=("zn",))
        else:
            renk = r["cit_beyaz"] if tur == "beyaz" else r["cit_ahsap"]
            ara = 0.42 if on_cephe else 1.6
            k = max(1, int(L / ara))
            for i in range(k + 1):
                px, py = p0[0] + dx * L * i / k, p0[1] + dy * L * i / k
                hh = 0.85 if on_cephe else 0.95
                kutu(g, px - 0.05, px + 0.05, py - 0.05, py + 0.05, z, z + hh, renk, atla=("zn",))
                if on_cephe:
                    kutu(g, px - 0.035, px + 0.035, py - 0.035, py + 0.035, z + hh, z + hh + 0.07, renk, atla=("zn",))
            for hz in (0.3, 0.7):
                cubuk(g, (p0[0], p0[1], z + hz), (p1[0], p1[1], z + hz), 0.06, renk)

    A, B, C, Dd = (e, e), (PW - e, e), (PW - e, PD - e), (e, PD - e)
    cizgi_cit(A, (gap0, e), True)
    cizgi_cit((gap1, e), B, True)
    cizgi_cit(B, C, False)
    cizgi_cit(C, Dd, False)
    cizgi_cit(Dd, A, False)
    # Kapı direkleri ve posta kutusu
    for gx in (gap0, gap1):
        kutu(g, gx - 0.12, gx + 0.12, e - 0.12, e + 0.12, z, z + 1.05, r["suslu"] if tur != "ahsap" else r["cit_ahsap"], atla=("zn",))
        kutu(g, gx - 0.15, gx + 0.15, e - 0.15, e + 0.15, z + 1.05, z + 1.12, r["suslu"], atla=("zn",))
    mx = gap1 + 0.55
    silindir(g, mx, e + 0.45, z, 1.0, 0.05, 6, r["demir"])
    kutu(g, mx - 0.17, mx + 0.17, e + 0.25, e + 0.65, z + 1.0, z + 1.28, r["posta"])

    # Ağaçlar ve çalılar
    yerler = [(0.9, PD - 1.0), (PW - 0.9, PD - 1.0), (0.95, fy * 0.5), (PW - 0.95, fy * 0.5)]
    rnd.shuffle(yerler)
    for (tx, ty) in yerler[:rnd.randint(1, 3)]:
        if abs(tx - kapi_x) < 1.4 and ty < fy:
            continue
        hboy = rnd.uniform(1.1, 1.6)
        silindir(g, tx, ty, z, hboy, 0.09, 6, r["govde"])
        rr = rnd.uniform(0.75, 1.0)
        renk = rnd.choice(r["agac"])
        top(g, (tx, ty, z + hboy + rr * 0.8), rr, rr, rr * 1.05, renk)
        if rnd.random() < 0.5:
            top(g, (tx + rr * 0.45, ty - rr * 0.2, z + hboy + rr * 0.35), rr * 0.6, rr * 0.6, rr * 0.55, renk)
    # Ev köşelerinde çalılar
    for (cx, cy) in ((x0 - 0.35, fy - 0.35), (x1 + 0.35, fy - 0.35)):
        if rnd.random() < 0.7 and abs(cx - kapi_x) > 1.4:
            top(g, (cx, cy, z + 0.32), 0.42, 0.42, 0.36, r["cali"])
            if rnd.random() < 0.5:
                top(g, (cx + 0.2, cy - 0.25, z + 0.5), 0.12, 0.12, 0.12, rnd.choice(r["cicek"]))


# ------------------------------------------------------------------ ticari binalar ve apartman

YAZI_FONT = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Resources\Fonts\Inter-SemiBold.ttf"

TIC_SABIT = dict(asfalt="#5b5e63", serit="#f2f2ee", kaldirim="#d8d1c4", bordur="#bdb5a8", cerceve="#3a3d42",
                 metal="#a7abb0", beton="#cfc8bc", koyu="#2b2d31", cim="#9cc46a", golge_cam="#7f9fb8",
                 ahsap="#a7773f", beyaz="#f6f3ec", siyah="#1f1f22", kirmizi="#c8473a", sari_isik="#f3e3a0")

MARKALAR = {
    # Oyundaki parodi markalar (Data/GameData.cs). Logolar özgün, gerçek markalara ait öğe yok.
    "burger_queen": dict(ad="BURGER QUEEN", ana="#f07326", ikinci="#7a3e2a", yazi="#fff3e0", duvar="#efe2c8", alt="#7a3e2a"),
    "mcdanolds": dict(ad="McDanold's", ana="#f2c51a", ikinci="#3b3f45", yazi="#2b2b2b", duvar="#3b3f45", alt="#2f3237"),
    "szpar_express": dict(ad="SZPAR EXPRESS", ana="#279e4d", ikinci="#1d7a3a", yazi="#ffffff", duvar="#ece6da", alt="#9a9387"),
    "market_24": dict(ad="MARKET 24", ana="#3f73e6", ikinci="#2a52a8", yazi="#ffffff", duvar="#f1e4c8", alt="#b8b0a3"),
    "interszpar": dict(ad="INTERSZPAR", ana="#d91f2e", ikinci="#a3141f", yazi="#ffffff", duvar="#d9dadc", alt="#8f9296"),
}


def tic_renkleri(marka, rnd):
    mk = MARKALAR[marka]
    r = {k: M("T" + k.capitalize(), v) for k, v in TIC_SABIT.items()}
    r.update({
        "ana": M("Marka", mk["ana"]), "ikinci": M("Marka2", mk["ikinci"]), "yazi": M("Yazi", mk["yazi"]),
        "duvar": M("Duvar", mk["duvar"]), "taban": M("Taban", mk["alt"]), "suslu": M("Suslu", TIC_SABIT["beyaz"]),
        "cam": M("Cam", SABIT["cam"]), "bitki": M("Bitki", SABIT["bitki"]), "govde": M("AgacGovde", SABIT["govde"]),
        "agac": [M("Agac", c) for c in SABIT["agac"]], "klima": M("Klima", SABIT["klima"]), "boru": M("Boru", SABIT["boru"]),
        "saksi": M("Saksi", SABIT["saksi"]), "demir": M("Demir", SABIT["demir"]), "kemer": "duz", "_ad": mk["ad"],
        "cicek": [M("Cicek", c) for c in EV_SABIT["cicek"]], "cali": M("Cali", EV_SABIT["cali"]),
        "urun": [M("Urun", c) for c in ("#e0525a", "#f2b84a", "#6fae4a", "#f2e3c0", "#8a5a9a")],
    })
    return r


# ---------- yazı (Blender'da harf geometrisi; Blender dışındaki önizlemede çizilmez)

def yazi(g, metin, fc, cu, zc, o, boy, max_g, mat, derinlik=0.06):
    """Cephede ortalanmış kabartma yazı: cu/zc merkez, boy harf yüksekliği, max_g en fazla genişlik."""
    if not IN_BLENDER or not metin:
        return
    cv = bpy.data.curves.new("_yazi", 'FONT')
    cv.body = metin
    cv.align_x, cv.align_y = 'CENTER', 'CENTER'
    cv.size, cv.extrude, cv.resolution_u = 1.0, 0.5, 2
    try:
        if os.path.isfile(YAZI_FONT):
            ad = os.path.basename(YAZI_FONT)
            cv.font = bpy.data.fonts.get(ad) or bpy.data.fonts.load(YAZI_FONT, check_existing=True)
    except Exception:
        pass
    ob = bpy.data.objects.new("_yazi", cv)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.update()
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = ev.to_mesh()
    try:
        vs = [tuple(v.co) for v in me.vertices]
        if vs:
            xs, ys = [p[0] for p in vs], [p[1] for p in vs]
            w, h = max(xs) - min(xs), max(ys) - min(ys)
            s = min(boy / max(h, 1e-6), max_g / max(w, 1e-6))
            mx, my = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
            b = len(g.v)
            for x, y, z in vs:
                g.v.append(fc.P(cu + (x - mx) * s, o + (z + 0.5) * derinlik, zc + (y - my) * s))
            ters = dot(cross(fc.u, (0.0, 0.0, 1.0)), fc.out) < 0
            for p in me.polygons:
                idx = [b + i for i in p.vertices]
                g.yuz(idx[::-1] if ters else idx, mat, kabuk=False)
    finally:
        ev.to_mesh_clear()
        bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.curves.remove(cv)


# ---------- ortak parçalar

def asfalt_arsa(g, PW, PD, r, kaldirim_d=2.2):
    levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["asfalt"])
    levha(g, 0.0, PW, 0.0, kaldirim_d, EV_ZEMIN, EV_ZEMIN + 0.12, r["kaldirim"])      # kaldırım
    levha(g, 0.0, PW, kaldirim_d - 0.15, kaldirim_d, EV_ZEMIN + 0.12, EV_ZEMIN + 0.14, r["bordur"])


def park_yerleri(g, x0, y0, n, r, gen=2.5, der=4.6, yon=1):
    """x0'dan başlayan n park yeri; çizgiler y0'dan yon yönünde der kadar."""
    z = EV_ZEMIN
    for i in range(n + 1):
        x = x0 + i * gen
        ya, yb = sorted((y0, y0 + yon * der))
        kutu(g, x - 0.06, x + 0.06, ya, yb, z, z + 0.012, r["serit"], atla=("zn",))


def vitrin(g, fc, u0, u1, z0, z1, r, aralik=1.5, kasa=None, poster=0.0, rnd=None):
    kasa = kasa or r["cerceve"]
    cdortgen(g, fc, u0, u1, 0.02, z0, z1, r["cam"])
    n = max(1, int(round((u1 - u0) / aralik)))
    for i in range(n + 1):
        u = u0 + (u1 - u0) * i / n
        ckutu(g, fc, u - 0.06, u + 0.06, 0, 0.1, z0, z1, kasa)
    ckutu(g, fc, u0 - 0.06, u1 + 0.06, 0, 0.1, z0 - 0.1, z0, kasa)
    ckutu(g, fc, u0 - 0.06, u1 + 0.06, 0, 0.1, z1, z1 + 0.1, kasa)
    if poster and rnd is not None:
        for i in range(n):
            if rnd.random() < poster:
                a = u0 + (u1 - u0) * (i + 0.2) / n
                b = u0 + (u1 - u0) * (i + 0.8) / n
                cdortgen(g, fc, a, b, 0.03, z0 + 0.3, z0 + 0.3 + (b - a) * 1.2, rnd.choice(r["urun"]))


def tabela(g, fc, cu, zc, w, h, d, r, metin, renk=None, yazi_renk=None, oran=0.62):
    renk = renk or r["ana"]
    ckutu(g, fc, cu - w / 2, cu + w / 2, 0, d, zc - h / 2, zc + h / 2, renk)
    yazi(g, metin, fc, cu, zc, d, h * oran, w * 0.9, yazi_renk or r["yazi"])


def direk_tabela(g, x, y, H, w, h, r, metin, renk=None, ikon=None):
    """Sokak kenarında yüksek direkli tabela (iki yüzü de yazılı)."""
    silindir(g, x - w * 0.28, y, EV_ZEMIN + 0.14, H, 0.07, 8, r["metal"], kabuk=False)
    silindir(g, x + w * 0.28, y, EV_ZEMIN + 0.14, H, 0.07, 8, r["metal"], kabuk=False)
    z0 = EV_ZEMIN + 0.14 + H
    kutu(g, x - w / 2, x + w / 2, y - 0.25, y + 0.25, z0, z0 + h, renk or r["ana"])
    kutu(g, x - w / 2 - 0.06, x + w / 2 + 0.06, y - 0.29, y + 0.29, z0 + h, z0 + h + 0.12, r["ikinci"])
    on = Cephe((x - w / 2, y - 0.25, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    arka = Cephe((x + w / 2, y + 0.25, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
    for fc in (on, arka):
        yazi(g, metin, fc, w / 2, z0 + h * 0.5, 0.0, h * 0.42, w * 0.86, r["yazi"])
    if ikon:
        ikon(g, x, y, z0 + h + 0.12)


def lamba(g, x, y, r, h=4.2):
    silindir(g, x, y, EV_ZEMIN, h, 0.035, 6, r["koyu"], kabuk=False)
    cubuk(g, (x, y, EV_ZEMIN + h - 0.05), (x + 0.5, y, EV_ZEMIN + h - 0.05), 0.05, r["koyu"], kabuk=False)
    kutu(g, x + 0.35, x + 0.75, y - 0.14, y + 0.14, EV_ZEMIN + h - 0.2, EV_ZEMIN + h - 0.02, r["koyu"])
    kutu(g, x + 0.38, x + 0.72, y - 0.11, y + 0.11, EV_ZEMIN + h - 0.24, EV_ZEMIN + h - 0.2, r["sari_isik"])


def cop_kutusu(g, x, y, r, renk=None):
    silindir(g, x, y, EV_ZEMIN + 0.12, 0.85, 0.24, 8, renk or r["koyu"])
    silindir(g, x, y, EV_ZEMIN + 0.97, 0.06, 0.27, 8, r["metal"])


def calilik(g, x0, x1, y, r, rnd):
    n = max(1, int((x1 - x0) / 0.8))
    for i in range(n):
        x = x0 + (x1 - x0) * (i + 0.5) / n
        top(g, (x, y, EV_ZEMIN + 0.34), 0.42, 0.36, 0.34, r["cali"])
        if rnd.random() < 0.3:
            top(g, (x + 0.15, y - 0.2, EV_ZEMIN + 0.55), 0.1, 0.1, 0.1, rnd.choice(r["cicek"]))


def agac(g, x, y, r, rnd, boy=1.4, rr=0.85):
    silindir(g, x, y, EV_ZEMIN, boy, 0.09, 6, r["govde"])
    top(g, (x, y, EV_ZEMIN + boy + rr * 0.8), rr, rr, rr * 1.05, rnd.choice(r["agac"]))


def duz_cati_ust(g, x0, x1, y0, y1, H, r, rnd, parapet_renk, klima=2):
    kutu(g, x0 - 0.08, x1 + 0.08, y0 - 0.08, y1 + 0.08, H, H + 0.55, parapet_renk, atla=("zn",),
         yuzler={"zp": r["beton"]})
    kutu(g, x0 + 0.2, x1 - 0.2, y0 + 0.2, y1 - 0.2, H + 0.55, H + 0.57, r["beton"], atla=("zn",))
    for _ in range(klima):
        kx, ky = rnd.uniform(x0 + 1.0, x1 - 1.0), rnd.uniform(y0 + 1.2, y1 - 1.2)
        kutu(g, kx - 0.55, kx + 0.55, ky - 0.4, ky + 0.4, H + 0.57, H + 1.15, r["klima"])
        silindir(g, kx, ky, H + 1.15, 0.05, 0.32, 8, r["koyu"])


def sundurma_kutu(g, fc, u0, u1, z, cikma, r, renk):
    ckutu(g, fc, u0, u1, 0, cikma, z, z + 0.28, renk)
    ckutu(g, fc, u0 + 0.05, u1 - 0.05, cikma - 0.05, cikma, z - 0.04, z, r["sari_isik"])


def tente(g, fc, u0, u1, z, cikma, r, renk, renk2=None):
    """Eğik bez tente (çizgili)."""
    a = fc.P(u0, 0.0, z + 0.55)
    yonlu_kutu(g, fc.P((u0 + u1) / 2, cikma / 2, z + 0.275), mul(fc.u, (u1 - u0) / 2),
               (fc.out[0] * cikma / 2, fc.out[1] * cikma / 2, -0.275), (0.0, 0.0, 0.04), renk)
    if renk2:
        n = max(2, int((u1 - u0) / 0.5))
        for i in range(0, n, 2):
            ua = u0 + (u1 - u0) * i / n
            ub = u0 + (u1 - u0) * (i + 1) / n
            yonlu_kutu(g, add(fc.P((ua + ub) / 2, cikma / 2, z + 0.275), (0.0, 0.0, 0.045)), mul(fc.u, (ub - ua) / 2),
                       (fc.out[0] * cikma / 2, fc.out[1] * cikma / 2, -0.275), (0.0, 0.0, 0.01), renk2)
    ckutu(g, fc, u0, u1, cikma - 0.03, cikma, z - 0.25, z + 0.02, renk)          # sarkan saçak
    del a


# ---------- fast food

def tac_ikon(g, x, y, z, r):
    """Burger Queen: özgün taç (üç sivri uç)."""
    kutu(g, x - 0.7, x + 0.7, y - 0.15, y + 0.15, z, z + 0.3, r["yazi"])
    for dx in (-0.55, 0.0, 0.55):
        pts = [(x + dx - 0.18, y - 0.15, z + 0.3), (x + dx + 0.18, y - 0.15, z + 0.3), (x + dx, y - 0.15, z + 0.75),
               (x + dx - 0.18, y + 0.15, z + 0.3), (x + dx + 0.18, y + 0.15, z + 0.3), (x + dx, y + 0.15, z + 0.75)]
        dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["yazi"])


def burger_ikon(g, x, y, z, r):
    """McDanold's: özgün burger simgesi (ekmek, köfte, marul)."""
    ekmek, kofte, marul = M("BurgerEkmek", "#e0a24a"), M("BurgerKofte", "#6b3a22"), M("BurgerMarul", "#6fae4a")
    silindir(g, x, y, z, 0.16, 0.55, 10, ekmek)
    silindir(g, x, y, z + 0.16, 0.1, 0.6, 10, marul)
    silindir(g, x, y, z + 0.26, 0.18, 0.57, 10, kofte)
    top(g, (x, y, z + 0.5), 0.58, 0.58, 0.3, ekmek)


def fastfood(rnd, marka):
    g = Govde()
    r = tic_renkleri(marka, rnd)
    bq = marka == "burger_queen"
    PW, PD = 17.0, 15.5
    asfalt_arsa(g, PW, PD, r)
    W, D = 10.5, 7.6
    x0, fy = 1.0, 6.9
    x1, by = x0 + W, fy + D
    z0 = EV_ZEMIN
    H = 4.3
    on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    sol = Cephe((x0, by, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    sag = Cephe((x1, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
    arka = Cephe((x1, by, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))

    # Bina yaya platformu (kaldırım seviyesi)
    kutu(g, x0 - 1.2, x1 + 0.4, fy - 1.6, by + 0.4, z0, z0 + 0.14, r["kaldirim"], atla=("zn",))
    zb = z0 + 0.14
    if bq:
        kutu(g, x0, x1, fy, by, zb, zb + 1.0, r["taban"], atla=("zn",))                 # tuğla alt bant
        kutu(g, x0, x1, fy, by, zb + 1.0, H, r["duvar"], atla=("zn",))
        kutu(g, x0 - 0.12, x1 + 0.12, fy - 0.12, by + 0.12, H - 0.75, H + 0.25, r["ana"], atla=("zn",))   # turuncu saçak bandı
        kutu(g, x0 - 0.16, x1 + 0.16, fy - 0.16, by + 0.16, H + 0.25, H + 0.4, r["ikinci"], atla=("zn",), yuzler={"zp": r["beton"]})
        kutu(g, x0 + 0.2, x1 - 0.2, fy + 0.2, by - 0.2, H + 0.25, H + 0.3, r["beton"], atla=("zn",))
    else:
        kutu(g, x0, x1, fy, by, zb, H + 0.3, r["duvar"], atla=("zn",), yuzler={"zp": r["beton"]})
        kutu(g, x0 - 0.1, x1 + 0.1, fy - 0.1, by + 0.1, H + 0.3, H + 0.45, r["koyu"], atla=("zn",))
        # Ahşap çıtalı cephe parçası (solda)
        for i in range(9):
            u = 0.25 + i * 0.32
            ckutu(g, on, u - 0.07, u + 0.07, 0, 0.12, zb + 0.4, H + 0.2, r["ahsap"])
        # Girişte yüksek sarı kule
        tx0, tx1 = x0 + W * 0.55, x0 + W * 0.55 + 2.6
        kutu(g, tx0, tx1, fy - 0.5, fy + 2.0, zb, H + 1.9, r["ana"], atla=("zn",))
        kutu(g, tx0 - 0.08, tx1 + 0.08, fy - 0.58, fy + 2.08, H + 1.9, H + 2.05, r["koyu"], atla=("zn",))
        tfc = Cephe((tx0, fy - 0.5, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        # Kule yüzünde yuvarlak burger rozeti ve kapı
        ckutu(g, tfc, 0.35, 2.25, 0, 0.06, zb, zb + 2.4, r["cerceve"])
        cdortgen(g, tfc, 0.45, 2.15, 0.07, zb + 0.05, zb + 2.3, r["cam"])
        ckutu(g, tfc, 1.27, 1.33, 0.07, 0.1, zb + 0.05, zb + 2.3, r["cerceve"])
        tabela(g, tfc, 1.3, H + 0.95, 2.2, 0.75, 0.08, r, r["_ad"], renk=r["koyu"], yazi_renk=r["ana"], oran=0.55)

    # Vitrinler: önde ve solda
    if bq:
        kapi_u = W * 0.5
        vitrin(g, on, 0.6, kapi_u - 1.2, zb + 1.0, zb + 2.9, r, aralik=1.4, kasa=r["ikinci"])
        vitrin(g, on, kapi_u + 1.2, W - 0.6, zb + 1.0, zb + 2.9, r, aralik=1.4, kasa=r["ikinci"])
        ckutu(g, on, kapi_u - 1.0, kapi_u + 1.0, 0, 0.06, zb, zb + 2.5, r["ikinci"])
        cdortgen(g, on, kapi_u - 0.9, kapi_u + 0.9, 0.07, zb + 0.05, zb + 2.4, r["cam"])
        ckutu(g, on, kapi_u - 0.03, kapi_u + 0.03, 0.07, 0.1, zb + 0.05, zb + 2.4, r["ikinci"])
        sundurma_kutu(g, on, kapi_u - 1.5, kapi_u + 1.5, zb + 2.6, 1.3, r, r["ana"])
        tabela(g, on, kapi_u, H + 0.95, 6.0, 1.05, 0.25, r, r["_ad"], renk=r["ana"], oran=0.55)
        tac_ikon(g, x0 + kapi_u, fy - 0.12, H + 1.47, r)
        vitrin(g, sol, 1.0, D - 1.2, zb + 1.0, zb + 2.9, r, aralik=1.6, kasa=r["ikinci"])
    else:
        vitrin(g, on, 3.3, W * 0.55 - 0.3, zb + 0.4, zb + 3.0, r, aralik=1.3, kasa=r["koyu"])
        vitrin(g, on, W * 0.55 + 2.9, W - 0.5, zb + 0.4, zb + 3.0, r, aralik=1.3, kasa=r["koyu"])
        vitrin(g, sol, 0.8, D - 0.8, zb + 0.4, zb + 3.0, r, aralik=1.6, kasa=r["koyu"])
        ckutu(g, on, -0.05, W + 0.05, 0, 0.2, H - 0.4, H - 0.2, r["ana"])                       # sarı ince bant

    # Arabaya servis penceresi (sağ duvar) ve şeridi
    zd = zb + 1.0
    ckutu(g, sag, 2.6, 4.0, 0, 0.08, zd, zd + 1.3, r["cerceve"])
    cdortgen(g, sag, 2.7, 3.9, 0.09, zd + 0.1, zd + 1.2, r["cam"])
    sundurma_kutu(g, sag, 2.2, 4.4, zd + 1.5, 1.1, r, r["ana"])
    tabela(g, sag, 5.6, zb + 3.2, 2.2, 0.55, 0.08, r, "DRIVE", renk=r["ikinci"], oran=0.6)
    for k in range(4):                                                    # şerit okları
        yy = fy + 0.5 + k * 2.2
        xx = x1 + 2.2
        kutu(g, xx - 0.08, xx + 0.08, yy, yy + 1.0, z0, z0 + 0.012, r["serit"], atla=("zn",))
        dis_bukey(g, [(xx - 0.35, yy + 1.0, z0), (xx + 0.35, yy + 1.0, z0), (xx, yy + 1.5, z0),
                      (xx - 0.35, yy + 1.0, z0 + 0.012), (xx + 0.35, yy + 1.0, z0 + 0.012), (xx, yy + 1.5, z0 + 0.012)],
                  [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["serit"], kabuk=False)
    # Menü panosu
    mx, my = x1 + 1.0, by - 1.0
    for dy in (-0.5, 0.5):
        silindir(g, mx, my + dy, z0, 1.3, 0.05, 6, r["koyu"])
    kutu(g, mx - 0.12, mx + 0.12, my - 0.75, my + 0.75, z0 + 1.0, z0 + 2.2, r["koyu"])
    mfc = Cephe((mx + 0.12, my - 0.75, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
    for i in range(3):
        cdortgen(g, mfc, 0.12 + i * 0.44, 0.5 + i * 0.44, 0.01, z0 + 1.15, z0 + 2.05, rnd.choice(r["urun"]))

    # Önde park yerleri ya da masalar (solda), girişe yürüme yolu
    park_yerleri(g, x0 + 4.0 if bq else x0 + 0.2, 2.3, 3 if bq else 2, r, der=4.2)
    if bq:
        # Dış masalar ve şemsiyeler
        for (tx, ty) in ((x0 + 0.9, 3.4), (x0 + 2.8, 3.4), (x0 + 1.85, 5.2)):
            silindir(g, tx, ty, z0, 0.72, 0.06, 6, r["koyu"])
            silindir(g, tx, ty, z0 + 0.72, 0.05, 0.5, 8, r["beyaz"])
            silindir(g, tx, ty, z0 + 0.77, 1.4, 0.035, 6, r["metal"])
            pts = []
            for i in range(8):
                a = 2 * math.pi * i / 8
                pts.append((tx + 1.0 * math.cos(a), ty + 1.0 * math.sin(a), z0 + 2.0))
            pts.append((tx, ty, z0 + 2.45))
            dis_bukey(g, pts, [[i, (i + 1) % 8, 8] for i in range(8)] + [list(range(8))[::-1]], r["ana"])
            for a in (0.0, math.pi):
                kutu(g, tx + 0.8 * math.cos(a) - 0.2, tx + 0.8 * math.cos(a) + 0.2, ty - 0.2, ty + 0.2, z0, z0 + 0.45, r["ikinci"])
    else:
        for (tx, ty) in ((x0 + 7.0, 3.6), (x0 + 8.6, 3.6)):
            calilik(g, tx - 0.6, tx + 0.6, ty, r, rnd)
    for xx in (x0 - 0.07, x1 + 0.07):
        oluk(g, xx, fy - 0.07, zb, H - 0.1)
    for u in (1.4, W - 1.4):
        duvar_lambasi(g, on, u, zb + 3.1)
    direk_tabela(g, PW - 1.6, 1.2, 5.2, 3.0, 1.5, r, r["_ad"], ikon=(lambda gg, x, y, z: tac_ikon(gg, x, y, z, r)) if bq
                 else (lambda gg, x, y, z: burger_ikon(gg, x, y, z, r)))
    lamba(g, x0 - 0.4, 2.6, r)
    lamba(g, PW - 0.7, PD - 1.0, r)
    cop_kutusu(g, x0 + W * 0.5 + 2.0, fy - 1.2, r, r["ikinci"])
    agac(g, 0.9, PD - 1.0, r, rnd)
    for _ in range(2):
        kx, ky = rnd.uniform(x0 + 1.5, x0 + 4.5), rnd.uniform(fy + 2.0, by - 1.5)
        zr = H + (0.3 if bq else 0.45)
        kutu(g, kx - 0.55, kx + 0.55, ky - 0.4, ky + 0.4, zr, zr + 0.6, r["klima"])
        silindir(g, kx, ky, zr + 0.6, 0.05, 0.32, 8, r["koyu"])

    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------- marketler

def market_dukkan(g, r, rnd, x0, W, fy, D, H, kapi_u, poster=0.5):
    """Tek katlı market kutusu: cam vitrin, otomatik kapı, markalı saçak ve tabela."""
    x1, by = x0 + W, fy + D
    zb = EV_ZEMIN + 0.14
    on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    kutu(g, x0, x1, fy, by, zb, H, r["duvar"], atla=("zn",))
    kutu(g, x0 - 0.05, x1 + 0.05, fy - 0.05, by + 0.05, zb, zb + 0.4, r["taban"], atla=("zn",))
    kutu(g, x0 - 0.15, x1 + 0.15, fy - 0.15, by + 0.15, H - 1.0, H + 0.3, r["ana"], atla=("zn",))      # marka bandı
    kutu(g, x0 - 0.18, x1 + 0.18, fy - 0.18, by + 0.18, H + 0.3, H + 0.42, r["ikinci"], atla=("zn",), yuzler={"zp": r["beton"]})
    kutu(g, x0 + 0.2, x1 - 0.2, fy + 0.2, by - 0.2, H + 0.3, H + 0.34, r["beton"], atla=("zn",))
    vitrin(g, on, 0.5, kapi_u - 1.1, zb + 0.4, H - 1.2, r, aralik=1.5, poster=poster, rnd=rnd)
    vitrin(g, on, kapi_u + 1.1, W - 0.5, zb + 0.4, H - 1.2, r, aralik=1.5, poster=poster, rnd=rnd)
    ckutu(g, on, kapi_u - 1.0, kapi_u + 1.0, 0, 0.08, zb, H - 1.1, r["cerceve"])
    cdortgen(g, on, kapi_u - 0.9, kapi_u + 0.9, 0.09, zb + 0.05, H - 1.2, r["cam"])
    ckutu(g, on, kapi_u - 0.03, kapi_u + 0.03, 0.09, 0.12, zb + 0.05, H - 1.2, r["cerceve"])
    sundurma_kutu(g, on, kapi_u - 1.6, kapi_u + 1.6, H - 1.1, 1.4, r, r["ikinci"])
    for xx in (x0 - 0.17, x1 + 0.17):
        oluk(g, xx, fy - 0.17, zb, H - 1.0)
    return on


def alisveris_arabasi(g, x, y, r, renk):
    kutu(g, x - 0.25, x + 0.25, y - 0.4, y + 0.4, EV_ZEMIN + 0.45, EV_ZEMIN + 0.95, renk)
    for dx in (-0.2, 0.2):
        for dy in (-0.3, 0.3):
            silindir(g, x + dx, y + dy, EV_ZEMIN, 0.45, 0.03, 4, r["metal"])


def market(rnd, tip):
    g = Govde()
    r = tic_renkleri(tip, rnd)
    z0 = EV_ZEMIN
    if tip == "szpar_express":
        PW, PD = 14.0, 13.5
        asfalt_arsa(g, PW, PD, r)
        W, D, H = 10.0, 7.4, 4.4
        x0, fy = 2.0, 5.4
        kutu(g, x0 - 2.0, x0 + W + 2.0, fy - 2.4, fy + D + 0.6, z0, z0 + 0.14, r["kaldirim"], atla=("zn",))
        on = market_dukkan(g, r, rnd, x0, W, fy, D, H, W * 0.62)
        tabela(g, on, W * 0.3, H - 0.35, 4.6, 0.85, 0.18, r, r["_ad"], renk=r["ana"], oran=0.5)
        # Bisiklet parkı, araba yeri, bank, saksılar
        for i in range(4):
            bx = x0 + 0.6 + i * 0.6
            cubuk(g, (bx, fy - 1.6, z0 + 0.14), (bx, fy - 1.6, z0 + 0.84), 0.05, r["metal"])
            cubuk(g, (bx, fy - 1.6, z0 + 0.84), (bx, fy - 1.0, z0 + 0.84), 0.05, r["metal"])
            cubuk(g, (bx, fy - 1.0, z0 + 0.84), (bx, fy - 1.0, z0 + 0.14), 0.05, r["metal"])
        kutu(g, x0 + W - 2.6, x0 + W - 0.4, fy - 1.9, fy - 1.0, z0 + 0.14, z0 + 1.0, r["ikinci"], atla=("zn",))
        for k in range(3):
            alisveris_arabasi(g, x0 + W - 2.2 + k * 0.7, fy - 1.45, r, r["ana"])
        direk_tabela(g, 1.4, 1.2, 3.2, 2.6, 1.1, r, "SZPAR", ikon=None)
        lamba(g, PW - 1.0, 1.0, r)
        agac(g, PW - 0.9, PD - 1.0, r, rnd)
        agac(g, 0.9, PD - 1.2, r, rnd)
        kx, ky = x0 + W * 0.3, fy + D * 0.6
        kutu(g, kx - 0.6, kx + 0.6, ky - 0.4, ky + 0.4, H + 0.34, H + 0.95, r["klima"])
    elif tip == "market_24":
        # Altta market, üstte iki kat daire (köşe binası)
        PW, PD = 12.0, 12.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN + 0.14, r["kaldirim"])
        W, D = 9.6, 8.6
        x0, fy = 1.2, 2.6
        x1, by = x0 + W, fy + D
        zb = z0 + 0.14
        Hm = zb + 3.6
        kat = rnd.choice([2, 3])
        Ht = Hm + kat * 2.9
        on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        sag = Cephe((x1, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
        sol = Cephe((x0, by, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
        arka = Cephe((x1, by, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
        kutu(g, x0, x1, fy, by, zb, Ht, r["duvar"], atla=("zn",))
        kutu(g, x0 - 0.06, x1 + 0.06, fy - 0.06, by + 0.06, zb, Hm, r["ana"], atla=("zn",))         # zemin kat mavi
        kutu(g, x0 - 0.1, x1 + 0.1, fy - 0.1, by + 0.1, Hm, Hm + 0.18, r["suslu"], atla=("zn",))
        vitrin(g, on, 0.5, 3.6, zb + 0.35, Hm - 0.9, r, aralik=1.0, poster=0.6, rnd=rnd)
        vitrin(g, on, 5.6, W - 0.5, zb + 0.35, Hm - 0.9, r, aralik=1.3, poster=0.6, rnd=rnd)
        vitrin(g, sag, 0.6, D - 3.0, zb + 0.35, Hm - 0.9, r, aralik=1.3, poster=0.4, rnd=rnd)
        ckutu(g, on, 3.8, 5.4, 0, 0.08, zb, Hm - 0.8, r["cerceve"])
        cdortgen(g, on, 3.9, 5.3, 0.09, zb + 0.05, Hm - 0.9, r["cam"])
        tente(g, on, 0.3, W - 0.3, Hm - 0.75, 1.5, r, r["ana"], r["suslu"])
        tabela(g, on, W / 2, Hm - 0.4, 5.0, 0.6, 0.12, r, r["_ad"], renk=r["ikinci"], oran=0.62)
        # Köşede dik "24" tabelası
        bfc_x, bfc_y = x1, fy + 0.4
        kutu(g, bfc_x + 0.05, bfc_x + 1.05, bfc_y - 0.12, bfc_y + 0.12, Hm + 0.3, Hm + 1.5, r["ana"], atla=("xn",))
        bfc = Cephe((bfc_x + 0.05, bfc_y - 0.12, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        yazi(g, "24", bfc, 0.5, Hm + 0.9, 0.0, 0.75, 0.85, r["yazi"])
        bfc2 = Cephe((bfc_x + 1.05, bfc_y + 0.12, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
        yazi(g, "24", bfc2, 0.5, Hm + 0.9, 0.0, 0.75, 0.85, r["yazi"])
        # Üst katlar: pencereler, balkonlar
        for f in range(kat):
            z = Hm + f * 2.9 + 0.75
            for u in (1.4, 3.6, 6.0, 8.2):
                ev_pencere(g, on, u, z, 0.85, 1.4, r, rnd, saksi=rnd.random() < 0.35)
            if f >= 0 and rnd.random() < 0.7:
                ckutu(g, on, 4.6, 7.4, 0, 1.0, z - 0.35, z - 0.2, r["beton"])
                for k in range(8):
                    uu = 4.65 + k * 0.385
                    ckutu(g, on, uu - 0.02, uu + 0.02, 0.92, 0.97, z - 0.2, z + 0.6, r["demir"])
                ckutu(g, on, 4.6, 7.4, 0.9, 0.99, z + 0.6, z + 0.66, r["demir"])
            for u in (2.0, 5.0, 7.0):
                ev_pencere(g, sag, u, z, 0.8, 1.35, r, rnd)
            for u in (1.5, 4.0, 6.5, 8.6):
                pencere(g, arka, u, z, 0.8, 1.3, r, sade=True)
            for u in (2.0, 4.6, 7.0):
                pencere(g, sol, u, z, 0.8, 1.3, r, sade=True)
            if f < kat - 1:
                ckutu(g, on, -0.02, W + 0.02, 0, 0.06, Hm + (f + 1) * 2.9 - 0.05, Hm + (f + 1) * 2.9 + 0.06, r["suslu"])
        # Çatı: kiremit beşik ya da düz
        if rnd.random() < 0.5:
            rr = dict(r)
            rr["cati"], rr["cati_koyu"] = M("Cati", "#c0573a"), M("CatiKoyu", koyu_ton("#c0573a"))
            rr["baca"] = r["duvar"]
            kutu(g, x0 - 0.04, x1 + 0.04, fy - 0.04, by + 0.04, Ht - 0.16, Ht, r["suslu"], atla=("zn", "zp"))
            zr = besik_cati(g, "x", x0, x1, fy, by, Ht, rr)
            ev_baca(g, x0 + W * 0.75, fy + D / 2 + 0.6, cati_z(Ht, fy, by, fy + D / 2 + 0.6) - 0.3, zr + 0.45, rr)
        else:
            duz_cati_ust(g, x0, x1, fy, by, Ht, r, rnd, r["suslu"], klima=1)
        # Önde meyve kasaları, bank, çöp
        for i in range(3):
            cx = x0 + 0.8 + i * 0.75
            kutu(g, cx - 0.32, cx + 0.32, fy - 0.9, fy - 0.35, zb, zb + 0.55, r["ahsap"])
            kutu(g, cx - 0.28, cx + 0.28, fy - 0.86, fy - 0.39, zb + 0.55, zb + 0.62, rnd.choice(r["urun"]))
        cop_kutusu(g, x1 - 0.5, fy - 1.0, r, r["ikinci"])
        lamba(g, 0.6, 0.7, r)
        agac(g, PW - 0.8, PD - 0.9, r, rnd)
    else:   # interszpar: büyük hipermarket ve otopark
        PW, PD = 24.0, 21.0
        asfalt_arsa(g, PW, PD, r)
        W, D, H = 19.0, 10.5, 6.8
        x0, fy = 2.5, 9.6
        x1, by = x0 + W, fy + D
        zb = z0 + 0.14
        kutu(g, x0 - 1.0, x1 + 1.0, fy - 2.0, by + 0.4, z0, zb, r["kaldirim"], atla=("zn",))
        kutu(g, x0, x1, fy, by, zb, H, r["duvar"], atla=("zn",))
        kutu(g, x0 - 0.06, x1 + 0.06, fy - 0.06, by + 0.06, zb, zb + 0.6, r["taban"], atla=("zn",))
        kutu(g, x0 - 0.15, x1 + 0.15, fy - 0.15, by + 0.15, H, H + 0.6, r["ana"], atla=("zn",), yuzler={"zp": r["beton"]})
        on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        sag = Cephe((x1, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
        sol = Cephe((x0, by, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
        # Yan duvarlarda dikey oluklar
        for fc, L in ((sag, D), (sol, D)):
            n = int(L / 0.9)
            for i in range(1, n):
                ckutu(g, fc, i * L / n - 0.05, i * L / n + 0.05, 0, 0.06, zb + 0.6, H, r["metal"])
        # Ön cephe: kırmızı bant, büyük harfler, cam giriş kutusu
        ckutu(g, on, 0.0, W, 0, 0.1, H - 2.2, H - 2.0, r["ana"])
        for i in range(1, 12):
            u = i * W / 12
            if 6.8 < u < 12.2:
                continue
            ckutu(g, on, u - 0.05, u + 0.05, 0, 0.06, zb + 0.6, H - 2.2, r["metal"])
        yazi(g, r["_ad"], on, W / 2, H - 1.0, 0.1, 1.3, W * 0.62, r["ana"], derinlik=0.18)
        ex0, ex1 = 7.0, 12.0
        kutu(g, x0 + ex0, x0 + ex1, fy - 1.6, fy + 0.05, zb, zb + 3.6, r["cerceve"], atla=("zn", "yp"))
        efc = Cephe((x0 + ex0, fy - 1.6, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        vitrin(g, efc, 0.15, ex1 - ex0 - 0.15, zb + 0.05, zb + 3.3, r, aralik=1.0)
        ckutu(g, efc, -0.6, ex1 - ex0 + 0.6, 0, 1.6, zb + 3.6, zb + 3.95, r["ana"])
        ckutu(g, efc, -0.55, ex1 - ex0 + 0.55, 1.55, 1.6, zb + 3.5, zb + 3.6, r["sari_isik"])
        vitrin(g, on, 0.8, 6.4, zb + 0.9, zb + 3.0, r, aralik=1.4, poster=0.5, rnd=rnd)
        vitrin(g, on, 12.6, W - 0.8, zb + 0.9, zb + 3.0, r, aralik=1.4, poster=0.5, rnd=rnd)
        # Araba sundurması ve arabalar
        ax0 = x0 + W - 4.2
        for dx in (0.0, 3.4):
            silindir(g, ax0 + dx, fy - 3.6, z0, 2.2, 0.06, 6, r["metal"])
            silindir(g, ax0 + dx, fy - 2.4, z0, 2.2, 0.06, 6, r["metal"])
        kutu(g, ax0 - 0.2, ax0 + 3.6, fy - 3.8, fy - 2.2, z0 + 2.2, z0 + 2.35, r["ana"])
        for k in range(4):
            alisveris_arabasi(g, ax0 + 0.5 + k * 0.75, fy - 3.0, r, r["ikinci"])
        # Otopark: iki sıra
        park_yerleri(g, 1.0, 2.3, 6, r, der=4.4)
        lamba(g, 0.7, 7.0, r, h=5.0)
        lamba(g, PW - 1.2, 7.0, r, h=5.0)
        direk_tabela(g, PW - 2.4, 1.2, 6.0, 3.6, 1.4, r, r["_ad"])
        calilik(g, x0 - 0.5, x0 + 6.0, fy - 1.6, r, rnd)
        agac(g, 1.0, PD - 1.0, r, rnd, boy=1.6, rr=1.0)
        agac(g, PW - 1.0, PD - 1.0, r, rnd, boy=1.6, rr=1.0)
        for _ in range(3):
            kx, ky = rnd.uniform(x0 + 2.0, x1 - 2.0), rnd.uniform(fy + 2.5, by - 1.5)
            kutu(g, kx - 0.8, kx + 0.8, ky - 0.5, ky + 0.5, H + 0.6, H + 1.3, r["klima"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------- apartman

APT_DUVAR = ["#f1e4c8", "#ecd3a4", "#f0cdb0", "#d9dfc6", "#cfdbe3", "#e8d6c8", "#f4f0e6"]


def apartman(rnd, _stil=None):
    g = Govde()
    st = dict(EV_STILLER["avrupa"])
    st["duvar"] = APT_DUVAR
    r = ev_renkleri(st, rnd)
    for k, v in TIC_SABIT.items():
        r["t_" + k] = M("T" + k.capitalize(), v)
    kat = rnd.randint(APT_KAT[0], APT_KAT[1])
    W, D = rnd.uniform(11.5, 13.5), rnd.uniform(9.0, 10.0)
    PW, PD = W + 2.4, D + 5.4
    x0, fy = 1.2, 3.6
    x1, by = x0 + W, fy + D
    z0 = EV_ZEMIN
    zb = z0 + 0.6
    KY = EV_KAT_YUK
    Ht = zb + kat * KY + 0.2
    # Arsa: kaldırım + önde çim şeritler
    levha(g, 0.0, PW, 0.0, PD, 0.0, z0, r["cim"])
    levha(g, 0.0, PW, 0.0, 2.0, z0, z0 + 0.12, r["t_kaldirim"])
    on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    arka = Cephe((x1, by, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
    sol = Cephe((x0, by, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    sag = Cephe((x1, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
    kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
    kutu(g, x0 - 0.06, x1 + 0.06, fy - 0.06, by + 0.06, z0, zb + 0.3, r["taban"], atla=("zn",))
    zemin_renk = rnd.random() < 0.5
    if zemin_renk:      # zemin kat farklı (taş) renkte
        kutu(g, x0 - 0.04, x1 + 0.04, fy - 0.04, by + 0.04, zb + 0.3, zb + KY, r["t_beton"], atla=("zn",))
    for f in range(1, kat):
        ckutu(g, on, -0.03, W + 0.03, 0, 0.07, zb + f * KY - 0.06, zb + f * KY + 0.06, r["suslu"])

    # Ön cephe düzeni: ortada giriş ve merdiven boşluğu, iki yanda daireler
    cu = W / 2
    yan = [W * 0.12, W * 0.3, W * 0.7, W * 0.88]
    balkon_tip = rnd.choice(["demir", "dolu"])
    for f in range(kat):
        z = zb + f * KY + 0.75
        if f > 0:
            ev_pencere(g, on, cu, z + 0.2, 0.6, 1.0, r, rnd)                     # merdiven penceresi
        for i, u in enumerate(yan):
            balkon = f > 0 and i in (1, 2)
            if balkon:
                # Balkon kapısı + balkon
                ckutu(g, on, u - 0.48, u + 0.48, 0, 0.06, z - 0.6, z + 1.45, r["suslu"])
                cdortgen(g, on, u - 0.38, u + 0.38, 0.07, z - 0.55, z + 1.35, r["cam"])
                bx0, bx1 = u - 1.25, u + 1.25
                ckutu(g, on, bx0, bx1, 0, 1.15, z - 0.75, z - 0.6, r["t_beton"])
                if balkon_tip == "dolu":
                    ckutu(g, on, bx0, bx1, 1.05, 1.15, z - 0.6, z + 0.35, r["suslu"])
                    ckutu(g, on, bx0, bx0 + 0.1, 0.0, 1.05, z - 0.6, z + 0.35, r["suslu"])
                    ckutu(g, on, bx1 - 0.1, bx1, 0.0, 1.05, z - 0.6, z + 0.35, r["suslu"])
                else:
                    n = 10
                    for k in range(n + 1):
                        uu = bx0 + 0.05 + (bx1 - bx0 - 0.1) * k / n
                        ckutu(g, on, uu - 0.02, uu + 0.02, 1.05, 1.1, z - 0.6, z + 0.3, r["demir"])
                    ckutu(g, on, bx0, bx1, 1.03, 1.12, z + 0.3, z + 0.37, r["demir"])
                if rnd.random() < 0.5:
                    p = on.P(u + 0.9, 0.7, z - 0.4)
                    silindir(g, p[0], p[1], z - 0.6, 0.3, 0.16, 7, r["saksi"])
                    top(g, (p[0], p[1], z - 0.1), 0.22, 0.22, 0.25, r["bitki"])
            else:
                if f == 0 and abs(u - cu) < 1.5:
                    continue
                ev_pencere(g, on, u, z, 0.9, 1.4, r, rnd, saksi=f > 0 and rnd.random() < 0.3)
    # Giriş
    ckutu(g, on, cu - 0.8, cu + 0.8, 0, 0.06, zb, zb + 2.4, r["suslu"])
    ckutu(g, on, cu - 0.65, cu + 0.65, 0.06, 0.1, zb, zb + 2.25, r["kapi"])
    cdortgen(g, on, cu - 0.5, cu + 0.5, 0.11, zb + 1.0, zb + 2.1, r["cam"])
    ckutu(g, on, cu - 1.3, cu + 1.3, 0, 1.5, zb + 2.6, zb + 2.8, r["suslu"])
    for k in range(3):
        ckutu(g, on, cu - 1.1 - 0.1 * k, cu + 1.1 + 0.1 * k, 0, 0.4 + 0.35 * k, z0, z0 + (zb - z0) * (3 - k) / 3, r["taban"], atla=("zn",))
    # Yan ve arka cepheler
    for f in range(kat):
        z = zb + f * KY + 0.75
        n = max(2, int((W - 1) / 2.2))
        for i in range(n):
            pencere(g, arka, W * (i + 0.5) / n, z, 0.85, 1.35, r, sade=True)
        for fc in (sol, sag):
            for u in (D * 0.25, D * 0.75):
                pencere(g, fc, u, z, 0.8, 1.35, r, sade=True)
    # Çatı
    duz_cati = rnd.random() >= 0.55
    if not duz_cati:
        kutu(g, x0 - 0.05, x1 + 0.05, fy - 0.05, by + 0.05, Ht - 0.2, Ht, r["suslu"], atla=("zn", "zp"))
        zr = besik_cati(g, "x", x0, x1, fy, by, Ht, r)
        cati_anteni(g, x0 + W * 0.4, fy + D / 2, zr + 0.2)
        for bx in (x0 + W * 0.2, x0 + W * 0.8):
            ev_baca(g, bx, fy + D / 2 + 0.6, cati_z(Ht, fy, by, fy + D / 2 + 0.6) - 0.3, zr + 0.45, r)
    else:
        duz_cati_ust(g, x0, x1, fy, by, Ht, {**r, "beton": r["t_beton"], "klima": r["t_metal"], "koyu": r["t_koyu"]},
                     rnd, r["suslu"], klima=2)
        kutu(g, x0 + cu - 1.4, x0 + cu + 1.4, fy + D / 2 - 1.2, fy + D / 2 + 1.2, Ht + 0.57, Ht + 2.3, r["duvar"])  # merdiven kulesi
        kutu(g, x0 + cu - 1.5, x0 + cu + 1.5, fy + D / 2 - 1.3, fy + D / 2 + 1.3, Ht + 2.3, Ht + 2.45, r["suslu"])
        for i in range(3):                                                    # güneş panelleri
            gunes_paneli(g, x0 + 1.6 + i * 1.9, fy + 1.6, Ht + 0.57, 1.6, 1.1)
        cati_anteni(g, x0 + cu + 0.8, fy + D / 2 + 0.6, Ht + 2.45)
    # Detaylar: yağmur boruları, klimalar, çanak antenler, giriş lambaları
    for xx in (x0 - 0.07, x1 + 0.07):
        oluk(g, xx, fy - 0.07, z0, Ht - 0.1)
    for f in range(kat):
        zk = zb + f * KY + 0.3
        for fc in (sol, sag):
            if rnd.random() < 0.45:
                klima_dis(g, fc, D * 0.5, zk)
        if f > 0 and rnd.random() < 0.35:
            canak_anten(g, on, yan[rnd.choice([1, 2])] + 1.0, 1.1, zb + f * KY + 0.6)
    for du in (-1.05, 1.05):
        kapi_lambasi(g, on, cu + du, zb + 2.0)
    # Bahçe: yol, ağaçlar, çalılar, bisiklet
    kutu(g, x0 + cu - 0.9, x0 + cu + 0.9, 2.0, fy - 0.7, z0, z0 + 0.04, r["yol"], atla=("zn",))
    agac(g, 0.9, 2.9, r, rnd)
    agac(g, PW - 0.9, 2.9, r, rnd)
    for xx0, xx1 in ((x0 + 0.3, x0 + cu - 1.6), (x0 + cu + 1.6, x1 - 0.3)):
        calilik(g, xx0, xx1, fy - 0.45, r, rnd)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ------------------------------------------------------------------ şehir parçaları: yollar, zemin, ağaçlar
# Bir kare = 10 m. Oyunda yönler: kuzey = Blender −Y, doğu = Blender −X (FBX Unity'ye bu yönlerle girer).
# Trafik: arabalar kare merkezinden 1,7 m sağda, yayalar 4,4 m'de → asfalt ±3,6 m, kaldırım 3,6–5 m.

YOL_SABIT = dict(asfalt="#5b5e63", asfalt_koyu="#53565b", serit="#f2f2ee", kaldirim="#d8d1c4", kaldirim2="#cdc5b7",
                 bordur="#b3ab9e", cim="#9cc46a", cim_koyu="#86b05a", toprak="#8a6a4a", koyu="#2b2d31", metal="#a7abb0",
                 isik="#f3e3a0", kirmizi="#d9473a", sari="#f2c14a", yesil="#4fb36a", mavi="#3f73e6", beyaz="#f6f3ec")

YK = 5.0      # karenin yarısı
YA = 3.6      # asfaltın yarı genişliği
Z_ASF = 0.10  # asfalt üstü
Z_KAL = 0.22  # kaldırım üstü


def yol_renkleri():
    r = {k: M("Y" + k.capitalize(), v) for k, v in YOL_SABIT.items()}
    r.update({"govde": M("AgacGovde", SABIT["govde"]), "agac": [M("Agac", c) for c in SABIT["agac"]],
              "cali": M("Cali", EV_SABIT["cali"]), "cicek": [M("Cicek", c) for c in EV_SABIT["cicek"]]})
    return r


def cizgisiz(g, bas):
    """bas'tan sonra eklenen yüzlerde çizgi kabuğu olmasın (zemin levhaları: kare sınırında siyah ızgara çıkmasın)."""
    for i in range(bas, len(g.f)):
        g.kabuksuz[i] = True


def zemin_levha(g, x0, x1, y0, y1, z1, mat, ust=None):
    b = len(g.f)
    kutu(g, x0, x1, y0, y1, 0.0, z1, mat, atla=("zn",), yuzler={"zp": ust} if ust else None)
    cizgisiz(g, b)


def boya(g, x0, x1, y0, y1, mat, z=Z_ASF):
    """Asfalt üstüne boya (şerit, yaya geçidi)."""
    b = len(g.v)
    g.v += [(x0, y0, z + 0.008), (x1, y0, z + 0.008), (x1, y1, z + 0.008), (x0, y1, z + 0.008)]
    g.yuz([b, b + 1, b + 2, b + 3], mat, kabuk=False)


def kaldirim(g, x0, x1, y0, y1, r, bordur_kenar=()):
    """Kaldırım bloğu; bordur_kenar: asfalta bakan kenarlar ("xn", "xp", "yn", "yp") açık renk bordür."""
    zemin_levha(g, x0, x1, y0, y1, Z_KAL, r["kaldirim"])
    t = 0.18
    b = len(g.f)
    for k in bordur_kenar:
        if k == "xn":
            kutu(g, x0, x0 + t, y0, y1, Z_KAL - 0.01, Z_KAL + 0.015, r["bordur"], atla=("zn",))
        elif k == "xp":
            kutu(g, x1 - t, x1, y0, y1, Z_KAL - 0.01, Z_KAL + 0.015, r["bordur"], atla=("zn",))
        elif k == "yn":
            kutu(g, x0, x1, y0, y0 + t, Z_KAL - 0.01, Z_KAL + 0.015, r["bordur"], atla=("zn",))
        elif k == "yp":
            kutu(g, x0, x1, y1 - t, y1, Z_KAL - 0.01, Z_KAL + 0.015, r["bordur"], atla=("zn",))
    cizgisiz(g, b)
    # Kaldırım taşı derzleri (her 2,5 m)
    uz_x = (x1 - x0) >= (y1 - y0)
    L0, L1 = (x0, x1) if uz_x else (y0, y1)
    k = int((L1 - L0) / 2.5)
    for i in range(1, k + 1):
        p = L0 + i * 2.5
        if p >= L1 - 0.05:
            break
        if uz_x:
            boya(g, p - 0.025, p + 0.025, y0 + 0.2, y1, r["kaldirim2"], z=Z_KAL)
        else:
            boya(g, x0 + 0.2, x1, p - 0.025, p + 0.025, r["kaldirim2"], z=Z_KAL)


def sokak_lambasi(g, x, y, kol_x, kol_y, r):
    """Kaldırımda lamba; (kol_x, kol_y) kolun uzandığı yön (yola doğru)."""
    z = Z_KAL
    silindir(g, x, y, z, 0.2, 0.07, 6, r["koyu"], kabuk=False)
    silindir(g, x, y, z + 0.2, 4.0, 0.035, 6, r["koyu"], kabuk=False)
    ux, uy = x + kol_x * 1.0, y + kol_y * 1.0
    cubuk(g, (x, y, z + 4.2), (ux, uy, z + 4.2), 0.045, r["koyu"], kabuk=False)
    cubuk(g, (x, y, z + 3.7), (x + kol_x * 0.5, y + kol_y * 0.5, z + 4.2), 0.03, r["koyu"], kabuk=False)
    kutu(g, ux - 0.18, ux + 0.18, uy - 0.16, uy + 0.16, z + 4.08, z + 4.24, r["koyu"])
    kutu(g, ux - 0.14, ux + 0.14, uy - 0.12, uy + 0.12, z + 4.03, z + 4.08, r["isik"])


def trafik_lambasi(g, x, y, yon_x, yon_y, r):
    """Kavşak köşesinde trafik ışığı; ışıklar (yon_x, yon_y) yönüne bakar."""
    z = Z_KAL
    silindir(g, x, y, z, 3.2, 0.04, 6, r["koyu"], kabuk=False)
    hx, hy = x, y
    kutu(g, hx - 0.14, hx + 0.14, hy - 0.14, hy + 0.14, z + 2.2, z + 3.3, r["koyu"])
    for i, renk in enumerate(("kirmizi", "sari", "yesil")):
        zz = z + 3.1 - i * 0.33
        cx, cy = hx + yon_x * 0.14, hy + yon_y * 0.14
        ex, ey = (0.03, 0.11) if yon_x else (0.11, 0.03)
        kutu(g, cx - ex, cx + ex, cy - ey, cy + ey, zz - 0.11, zz + 0.11, r[renk])
    # Yaya düğmesi
    kutu(g, x - 0.06, x + 0.06, y - 0.06, y + 0.06, z + 1.0, z + 1.2, r["sari"])


def yol_tabelasi(g, x, y, r, tur="dur"):
    """Direkli trafik tabelası: "dur" (kırmızı sekizgen), "dikkat" (üçgen), "yaya" (mavi kare)."""
    z = Z_KAL
    silindir(g, x, y, z, 2.3, 0.05, 6, r["metal"])
    zc = z + 2.55
    if tur == "dur":
        pts = []
        for i in range(8):
            a = 2 * math.pi * (i + 0.5) / 8
            pts.append((x + 0.38 * math.cos(a), y - 0.04, zc + 0.38 * math.sin(a)))
        pts += [(p[0], y + 0.04, p[2]) for p in pts]
        yuzler = [list(range(8)), list(range(8, 16))] + [[i, (i + 1) % 8, 8 + (i + 1) % 8, 8 + i] for i in range(8)]
        dis_bukey(g, pts, yuzler, r["kirmizi"])
        kutu(g, x - 0.24, x + 0.24, y - 0.06, y - 0.04, zc - 0.05, zc + 0.05, r["beyaz"])
    elif tur == "dikkat":
        tri = [(x - 0.42, zc - 0.3), (x + 0.42, zc - 0.3), (x, zc + 0.42)]
        pts = [(a, y - 0.04, b) for a, b in tri] + [(a, y + 0.04, b) for a, b in tri]
        dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["beyaz"])
        tri2 = [(x - 0.3, zc - 0.23), (x + 0.3, zc - 0.23), (x, zc + 0.28)]
        pts = [(a, y - 0.06, b) for a, b in tri2] + [(a, y - 0.04, b) for a, b in tri2]
        dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["kirmizi"], kabuk=False)
    else:
        kutu(g, x - 0.33, x + 0.33, y - 0.04, y + 0.04, zc - 0.33, zc + 0.33, r["mavi"])
        kutu(g, x - 0.05, x + 0.05, y - 0.06, y - 0.04, zc - 0.22, zc + 0.12, r["beyaz"])
        kutu(g, x - 0.1, x + 0.1, y - 0.06, y - 0.04, zc + 0.16, zc + 0.26, r["beyaz"])


def zebra(g, eksen, merkez, r, uzun=1.6):
    """Yaya geçidi: eksen "y" ise yol Y boyunca gider, çizgiler X'e dizilir; merkez = (x, y)."""
    cx, cy = merkez
    n = 7
    for i in range(n):
        u = -YA + 0.35 + i * (2 * YA - 0.7) / (n - 1)
        if eksen == "y":
            boya(g, cx + u - 0.22, cx + u + 0.22, cy - uzun / 2, cy + uzun / 2, r["serit"])
        else:
            boya(g, cx - uzun / 2, cx + uzun / 2, cy + u - 0.22, cy + u + 0.22, r["serit"])


def orta_serit(g, eksen, a, b, r):
    """Kesik orta çizgi; a..b aralığında, 2,5 m periyot (kareler arasında kesintisiz)."""
    p = a
    while p < b - 0.01:
        q0, q1 = p + 0.65, min(b, p + 1.85)
        if q0 < b:
            if eksen == "y":
                boya(g, -0.08, 0.08, q0, q1, r["serit"])
            else:
                boya(g, q0, q1, -0.08, 0.08, r["serit"])
        p += 2.5


def yol_duz(r):
    """Kanonik düz: kuzey-güney (Blender Y boyunca)."""
    g = Govde()
    zemin_levha(g, -YA, YA, -YK, YK, Z_ASF, r["asfalt"])
    kaldirim(g, YA, YK, -YK, YK, r, ("xn",))
    kaldirim(g, -YK, -YA, -YK, YK, r, ("xp",))
    orta_serit(g, "y", -YK, YK, r)
    sokak_lambasi(g, 4.55, 0.0, -1.0, 0.0, r)
    rogar(g, 1.7, 2.6, Z_ASF)
    izgara(g, 3.15, 3.5, -3.3, -2.6, Z_ASF)
    izgara(g, -3.5, -3.15, 2.0, 2.7, Z_ASF)
    if DETAY:
        silindir(g, -4.75, -3.0, Z_KAL, 0.8, 0.2, 8, M("CopKutusu", "#4fb36a"))
    return g


def yol_kose(r):
    """Kanonik köşe: kuzey (−Y) ve doğu (−X) açık; dış kaldırımlar batı (+X) ve güneyde (+Y)."""
    g = Govde()
    zemin_levha(g, -YK, YA, -YK, YA, Z_ASF, r["asfalt"])
    kaldirim(g, YA, YK, -YK, YK, r, ("xn",))
    kaldirim(g, -YK, YA, YA, YK, r, ("yn",))
    kaldirim(g, -YK, -YA, -YK, -YA, r, ("xp", "yp"))
    orta_serit(g, "y", -YK, -0.1, r)
    orta_serit(g, "x", -YK, -0.1, r)
    sokak_lambasi(g, 4.45, 4.45, -0.7, -0.7, r)
    rogar(g, -1.4, -1.6, Z_ASF)
    # Dış köşede küçük ağaç çukuru
    b = len(g.f)
    kutu(g, 3.85, 4.75, 3.85 - 2.2, 4.75 - 2.2, Z_KAL - 0.02, Z_KAL + 0.01, r["toprak"], atla=("zn",))
    cizgisiz(g, b)
    top(g, (4.3, 2.1, Z_KAL + 0.35), 0.45, 0.45, 0.38, r["cali"])
    return g


def yol_t(r):
    """Kanonik T: batı (+X), kuzey (−Y), doğu (−X) açık; güney (+Y) kapalı."""
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YA, Z_ASF, r["asfalt"])
    kaldirim(g, -YK, YK, YA, YK, r, ("yn",))
    kaldirim(g, YA, YK, -YK, -YA, r, ("xn", "yp"))
    kaldirim(g, -YK, -YA, -YK, -YA, r, ("xp", "yp"))
    zebra(g, "y", (0.0, -YA - 0.75), r)
    orta_serit(g, "x", -YK, -YA - 0.1, r)
    orta_serit(g, "x", YA + 0.1, YK, r)
    sokak_lambasi(g, 0.0, 4.55, 0.0, -1.0, r)
    rogar(g, -1.8, 1.4, Z_ASF)
    izgara(g, 2.0, 2.7, 3.15, 3.5, Z_ASF)
    yol_tabelasi(g, 4.3, -4.3, r, "dikkat")
    yol_tabelasi(g, -4.3, -4.3, r, "yaya")
    return g


def yol_kavsak(r):
    """Dörtlü kavşak: dört köşede kaldırım, dört kolda yaya geçidi, iki köşede trafik ışığı."""
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_ASF, r["asfalt"])
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
        x0, x1 = sorted((sx * YA, sx * YK))
        y0, y1 = sorted((sy * YA, sy * YK))
        kaldirim(g, x0, x1, y0, y1, r, ("xn" if sx > 0 else "xp", "yn" if sy > 0 else "yp"))
    zebra(g, "y", (0.0, -YA - 0.75), r)
    zebra(g, "y", (0.0, YA + 0.75), r)
    zebra(g, "x", (-YA - 0.75, 0.0), r)
    zebra(g, "x", (YA + 0.75, 0.0), r)
    trafik_lambasi(g, 4.3, -4.3, 0.0, 1.0, r)
    trafik_lambasi(g, -4.3, 4.3, 0.0, -1.0, r)
    trafik_lambasi(g, 4.3, 4.3, -1.0, 0.0, r)
    trafik_lambasi(g, -4.3, -4.3, 1.0, 0.0, r)
    rogar(g, 1.3, -1.1, Z_ASF)
    return g


def zemin_kaldirim(r):
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_KAL, r["kaldirim"])
    for i in range(1, 4):
        p = -YK + i * 2.5
        boya(g, p - 0.025, p + 0.025, -YK, YK, r["kaldirim2"], z=Z_KAL)
        boya(g, -YK, YK, p - 0.025, p + 0.025, r["kaldirim2"], z=Z_KAL)
    return g


def zemin_cim(r, rnd):
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, 0.12, r["cim"])
    for _ in range(9):
        x, y = rnd.uniform(-4.3, 4.3), rnd.uniform(-4.3, 4.3)
        pts = [(x - 0.25, y - 0.12, 0.12), (x + 0.25, y - 0.12, 0.12), (x, y, 0.42),
               (x - 0.25, y + 0.12, 0.12), (x + 0.25, y + 0.12, 0.12), (x, y + 0.05, 0.38)]
        dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["cim_koyu"], kabuk=False)
    for _ in range(4):
        x, y = rnd.uniform(-4.3, 4.3), rnd.uniform(-4.3, 4.3)
        top(g, (x, y, 0.2), 0.09, 0.09, 0.08, rnd.choice(r["cicek"]))
    return g


def koni(g, cx, cy, z0, h, rad, n, mat):
    pts = [(cx + rad * math.cos(2 * math.pi * i / n), cy + rad * math.sin(2 * math.pi * i / n), z0) for i in range(n)]
    pts.append((cx, cy, z0 + h))
    dis_bukey(g, pts, [list(range(n))[::-1]] + [[i, (i + 1) % n, n] for i in range(n)], mat)


def agac_modeli(r, rnd, tur, renk_i):
    g = Govde()
    renk = r["agac"][renk_i % len(r["agac"])]
    if tur == "cam":
        silindir(g, 0.0, 0.0, 0.0, 0.9, 0.16, 6, r["govde"])
        koyu = M("AgacCam", "#4f7f4a")
        for i, (z, rad, h) in enumerate(((0.7, 1.35, 1.9), (1.6, 1.05, 1.7), (2.45, 0.72, 1.5))):
            koni(g, 0.0, 0.0, z, h, rad, 7, koyu)
    elif tur == "cali":
        for _ in range(3):
            top(g, (rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), 0.4), rnd.uniform(0.45, 0.6), rnd.uniform(0.45, 0.6), 0.42, r["cali"])
        if rnd.random() < 0.6:
            for _ in range(3):
                top(g, (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, -0.2), rnd.uniform(0.5, 0.75)), 0.1, 0.1, 0.1, rnd.choice(r["cicek"]))
    else:
        boy = rnd.uniform(1.3, 1.8)
        silindir(g, 0.0, 0.0, 0.0, boy, 0.15, 6, r["govde"])
        cubuk(g, (0.0, 0.0, boy * 0.7), (0.45, 0.1, boy + 0.25), 0.1, r["govde"])
        rr = rnd.uniform(0.95, 1.2)
        top(g, (0.0, 0.0, boy + rr * 0.75), rr, rr, rr * 1.0, renk)
        top(g, (rr * 0.55, 0.15, boy + rr * 0.35), rr * 0.62, rr * 0.62, rr * 0.55, renk)
        if rnd.random() < 0.6:
            top(g, (-rr * 0.5, -0.1, boy + rr * 0.45), rr * 0.55, rr * 0.55, rr * 0.5, renk)
    return g


def sehir_parcalari():
    """Sabit adlı parçalar: oyun yolları adlarından tanır (City3D.LoadPack)."""
    r = yol_renkleri()
    rnd = random.Random(TOHUM * 777)
    liste = [("Yol_lane_01", yol_duz(r)), ("Yol_corner_01", yol_kose(r)), ("Yol_t_intersection_01", yol_t(r)),
             ("Yol_intersection", yol_kavsak(r)), ("Yol_sidewalk", zemin_kaldirim(r)), ("Yol_grass", zemin_cim(r, rnd))]
    for i in range(3):
        liste.append((f"Agac_yuvarlak_{i + 1}", agac_modeli(r, rnd, "yuvarlak", i)))
    for i in range(2):
        liste.append((f"Agac_cam_{i + 1}", agac_modeli(r, rnd, "cam", i)))
    for i in range(2):
        liste.append((f"Cali_{i + 1}", agac_modeli(r, rnd, "cali", i)))
    liste += park_parcalari()
    liste += avlu_parcalari()
    sonuc = []
    for ad, g in liste:
        if CIZGI:
            # Yollarda çizgi sabit kalınlıkta (kare 10 m); ağaç ve park eşyaları küçük olduğu için biraz incelir
            kabuk(g, CIZGI_KALINLIK * (1.0 if ad.startswith("Yol_") else 0.7 if ad.startswith(("Park_", "Meydan_", "Avlu_")) else 0.4 if ad.startswith("Kir_") else 0.6))
        sonuc.append((ad, g, ortala(g)))
    return sonuc


# ------------------------------------------------------------------ parklar ve meydan (kare 10 m, köşeler ağaçlara boş)

PARK_SABIT = dict(cakil="#dccfae", kum="#ead9a6", su="#5fa8d8", su_koyu="#4a8fc0", tas="#b9b2a6", tas_acik="#d6cfc2",
                  ahsap="#a7773f", kirmizi="#d9473a", sari="#f2c14a", mavi="#3f73e6", nilufer="#5f9a4a", ordek="#f6f3ec",
                  gaga="#f0a030", heykel="#8a9a8f")
ZC = 0.12   # çim üstü


def park_renkleri():
    r = yol_renkleri()
    r.update({k: M("P" + k.capitalize(), v) for k, v in PARK_SABIT.items()})
    return r


def cim_taban(g, r):
    zemin_levha(g, -YK, YK, -YK, YK, ZC, r["cim"])


def poligon(n, rx, ry, cx=0.0, cy=0.0, faz=0.0):
    return [(cx + rx * math.cos(faz + 2 * math.pi * i / n), cy + ry * math.sin(faz + 2 * math.pi * i / n)) for i in range(n)]


def duz_poligon(g, pts, z, mat):
    """Yukarı bakan düz çokgen (yol, su yüzeyi). Çizgi kabuğu yok."""
    b = len(g.v)
    g.v += [(x, y, z) for x, y in pts]
    idx = list(range(b, b + len(pts)))
    if newell([g.v[i] for i in idx])[2] < 0:
        idx = idx[::-1]
    g.yuz(idx, mat, kabuk=False)


def prizma_poligon(g, pts, z0, z1, mat, kabuk=True):
    n = len(pts)
    p3 = [(x, y, z0) for x, y in pts] + [(x, y, z1) for x, y in pts]
    yuzler = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    dis_bukey(g, p3, yuzler, mat, kabuk)


def bank(g, x, y, aci, r, z0=ZC):
    """Park bankı; aci: bankın baktığı yön (radyan)."""
    n = (math.cos(aci), math.sin(aci), 0.0)
    u = (-n[1], n[0], 0.0)
    yonlu_kutu(g, (x, y, z0 + 0.45), mul(u, 0.8), mul(n, 0.22), (0, 0, 0.035), r["ahsap"])
    yonlu_kutu(g, add((x, y, z0 + 0.78), mul(n, -0.21)), mul(u, 0.8), mul(n, 0.03), (0, 0, 0.2), r["ahsap"])
    for s in (-0.65, 0.65):
        yonlu_kutu(g, add((x, y, z0 + 0.22), mul(u, s)), mul(u, 0.04), mul(n, 0.2), (0, 0, 0.22), r["koyu"])


def park_lambasi(g, x, y, r, z0=ZC):
    silindir(g, x, y, z0, 0.15, 0.06, 6, r["koyu"], kabuk=False)
    silindir(g, x, y, z0 + 0.15, 2.85, 0.028, 6, r["koyu"], kabuk=False)
    top(g, (x, y, z0 + 3.12), 0.17, 0.17, 0.2, r["isik"])
    silindir(g, x, y, z0 + 3.3, 0.06, 0.1, 6, r["koyu"], kabuk=False)


def park_cop(g, x, y, r, z0=ZC):
    silindir(g, x, y, z0, 0.75, 0.22, 8, r["yesil"])
    silindir(g, x, y, z0 + 0.75, 0.05, 0.25, 8, r["koyu"])


def cicek_obegi(g, rnd, cx, cy, rad, n, r, z):
    for _ in range(n):
        a, d = rnd.uniform(0, 2 * math.pi), rad * math.sqrt(rnd.random())
        x, y = cx + d * math.cos(a), cy + d * math.sin(a)
        if rnd.random() < 0.35:
            top(g, (x, y, z + 0.12), 0.16, 0.16, 0.14, r["cali"])
        else:
            top(g, (x, y, z + 0.14), 0.11, 0.11, 0.1, rnd.choice(r["cicek"]))


def park_oyun(r, rnd):
    g = Govde()
    cim_taban(g, r)
    # Kum havuzu ve ahşap çerçeve
    b = len(g.f)
    kutu(g, -2.8, 2.8, -2.4, 2.4, 0.0, ZC + 0.03, r["kum"], atla=("zn",))
    cizgisiz(g, b)
    for (x0, x1, y0, y1) in ((-2.95, 2.95, -2.55, -2.4), (-2.95, 2.95, 2.4, 2.55), (-2.95, -2.8, -2.4, 2.4), (2.8, 2.95, -2.4, 2.4)):
        kutu(g, x0, x1, y0, y1, ZC, ZC + 0.22, r["ahsap"], atla=("zn",))
    z = ZC + 0.03
    # Kaydırak: merdiven, platform, eğik kayma yüzeyi
    px, py = -1.6, -1.0
    for dx in (-0.3, 0.3):
        cubuk(g, (px + dx, py - 0.3, z), (px + dx, py - 0.3, z + 1.6), 0.08, r["koyu"])
        cubuk(g, (px + dx, py + 0.3, z), (px + dx, py + 0.3, z + 1.6), 0.08, r["koyu"])
    kutu(g, px - 0.4, px + 0.4, py - 0.4, py + 0.4, z + 1.4, z + 1.5, r["sari"])
    for k in range(4):
        zz = z + 0.3 + k * 0.3
        cubuk(g, (px - 0.3, py - 0.75 + k * 0.08, zz), (px + 0.3, py - 0.75 + k * 0.08, zz), 0.05, r["koyu"])
    a, bb = (px, py + 0.4, z + 1.45), (px, py + 2.1, z + 0.15)
    yonlu_kutu(g, mul(add(a, bb), 0.5), (0.32, 0.0, 0.0), mul(sub(bb, a), 0.5), (0.0, 0.03, 0.04), r["kirmizi"])
    for dx in (-0.32, 0.32):
        cubuk(g, (px + dx, py + 0.4, z + 1.6), (px + dx, py + 2.1, z + 0.3), 0.05, r["kirmizi"])
    # Salıncak: A çerçeve, üst kiriş, iki oturak
    sx, sy = 1.5, -0.8
    for dy in (-0.9, 0.9):
        cubuk(g, (sx - 0.5, sy + dy, z), (sx, sy + dy, z + 2.0), 0.08, r["mavi"])
        cubuk(g, (sx + 0.5, sy + dy, z), (sx, sy + dy, z + 2.0), 0.08, r["mavi"])
    cubuk(g, (sx, sy - 0.95, z + 2.0), (sx, sy + 0.95, z + 2.0), 0.09, r["mavi"])
    for dy in (-0.4, 0.4):
        for e in (-0.14, 0.14):
            cubuk(g, (sx, sy + dy + e, z + 2.0), (sx, sy + dy + e, z + 0.5), 0.02, r["koyu"], kabuk=False)
        kutu(g, sx - 0.12, sx + 0.12, sy + dy - 0.2, sy + dy + 0.2, z + 0.45, z + 0.52, r["kirmizi"])
    # Tahterevalli
    tx, ty = 0.6, 1.5
    kutu(g, tx - 0.12, tx + 0.12, ty - 0.12, ty + 0.12, z, z + 0.4, r["sari"])
    yonlu_kutu(g, (tx, ty, z + 0.45), (1.1, 0.0, 0.18), (0.0, 0.12, 0.0), (0.0, 0.0, 0.03), r["kirmizi"])
    for s in (-1, 1):
        cubuk(g, (tx + s * 0.9, ty, z + 0.45 + s * 0.15), (tx + s * 0.9, ty, z + 0.7 + s * 0.15), 0.04, r["koyu"])
    # Kenarda banklar, çöp, çalılar
    bank(g, 3.6, 0.0, math.pi, r)
    bank(g, -3.6, 1.4, 0.0, r)
    park_cop(g, 3.6, 1.4, r)
    for (x, y) in ((-3.7, -1.5), (0.0, 3.6), (0.0, -3.6)):
        top(g, (x, y, ZC + 0.3), 0.45, 0.45, 0.36, r["cali"])
    return g


def park_cicek(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, poligon(16, 3.2, 3.2), ZC + 0.01, r["cakil"])
    for i in range(4):                                              # dört kola çakıl yol (kenara varmadan)
        a = i * math.pi / 2
        c, s = math.cos(a), math.sin(a)
        p = [(c * 3.0 - s * 0.6, s * 3.0 + c * 0.6), (c * 4.3 - s * 0.6, s * 4.3 + c * 0.6),
             (c * 4.3 + s * 0.6, s * 4.3 - c * 0.6), (c * 3.0 + s * 0.6, s * 3.0 - c * 0.6)]
        duz_poligon(g, p, ZC + 0.01, r["cakil"])
    prizma_poligon(g, poligon(12, 1.55, 1.55), ZC, ZC + 0.3, r["tas"])
    duz_poligon(g, poligon(12, 1.4, 1.4), ZC + 0.305, r["toprak"])
    cicek_obegi(g, rnd, 0.0, 0.0, 1.25, 26, r, ZC + 0.3)
    top(g, (0.0, 0.0, ZC + 0.9), 0.45, 0.45, 0.6, r["cali"])
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        bank(g, 2.45 * math.cos(a), 2.45 * math.sin(a), a + math.pi, r)
    park_lambasi(g, 2.9, 0.0, r)
    park_lambasi(g, -2.9, 0.0, r)
    park_cop(g, 0.0, 2.9, r)
    for i in range(4):                                              # kollar arasında küçük tarhlar
        a = i * math.pi / 2 + math.pi / 4
        cicek_obegi(g, rnd, 3.4 * math.cos(a), 3.4 * math.sin(a), 0.5, 7, r, ZC)
    return g


def park_gezinti(r, rnd):
    g = Govde()
    cim_taban(g, r)
    # Kıvrılan çakıl yol (kenarlara değmeden)
    yol = [(-3.8, -1.5), (-2.4, -2.4), (-0.6, -2.2), (0.6, -0.6), (1.8, 0.8), (3.2, 1.2), (3.8, 2.6)]
    for (x0, y0), (x1, y1) in zip(yol, yol[1:]):
        dx, dy = x1 - x0, y1 - y0
        L = math.hypot(dx, dy)
        nx, ny = -dy / L * 0.6, dx / L * 0.6
        duz_poligon(g, [(x0 - nx, y0 - ny), (x1 - nx, y1 - ny), (x1 + nx, y1 + ny), (x0 + nx, y0 + ny)], ZC + 0.01, r["cakil"])
        duz_poligon(g, poligon(8, 0.6, 0.6, x1, y1), ZC + 0.012, r["cakil"])
    # Çit sırası (budanmış çalı) ve banklar
    kutu(g, -3.2, -0.6, 1.0, 1.55, ZC, ZC + 0.75, r["cali"])
    kutu(g, 0.8, 3.4, -3.3, -2.75, ZC, ZC + 0.75, r["cali"])
    bank(g, -1.9, 0.6, -math.pi / 2, r)
    bank(g, 2.1, -2.3, math.pi / 2, r)
    park_lambasi(g, 0.0, 0.4, r)
    park_cop(g, -0.6, -1.2, r)
    # Piknik masası
    mx, my = -1.6, 3.0
    kutu(g, mx - 0.9, mx + 0.9, my - 0.4, my + 0.4, ZC + 0.72, ZC + 0.8, r["ahsap"])
    for s in (-1, 1):
        kutu(g, mx - 0.9, mx + 0.9, my + s * 0.75 - 0.15, my + s * 0.75 + 0.15, ZC + 0.42, ZC + 0.48, r["ahsap"])
        kutu(g, mx + s * 0.7 - 0.05, mx + s * 0.7 + 0.05, my - 0.9, my + 0.9, ZC, ZC + 0.42, r["koyu"])
        kutu(g, mx + s * 0.6 - 0.05, mx + s * 0.6 + 0.05, my - 0.05, my + 0.05, ZC, ZC + 0.72, r["koyu"])
    cicek_obegi(g, rnd, 2.6, 3.0, 0.7, 10, r, ZC)
    cicek_obegi(g, rnd, -3.0, -3.2, 0.6, 8, r, ZC)
    return g


def park_gol(r, rnd):
    g = Govde()
    cim_taban(g, r)
    dis = poligon(14, 4.0, 3.1, faz=0.2)
    ic = poligon(14, 3.65, 2.8, faz=0.2)
    prizma_poligon(g, dis, ZC - 0.02, ZC + 0.16, r["tas"])
    duz_poligon(g, ic, ZC + 0.165, r["su"])
    duz_poligon(g, poligon(10, 2.2, 1.6, 0.4, 0.2), ZC + 0.168, r["su_koyu"])
    for _ in range(7):                                                    # nilüferler
        x, y = rnd.uniform(-2.6, 2.6), rnd.uniform(-1.9, 1.9)
        duz_poligon(g, poligon(7, 0.28, 0.28, x, y), ZC + 0.172, r["nilufer"])
        if rnd.random() < 0.4:
            top(g, (x + 0.08, y, ZC + 0.22), 0.07, 0.07, 0.06, r["cicek"][1])
    for _ in range(10):                                                   # sazlar
        a = rnd.uniform(-0.6, 0.9)
        x, y = 3.35 * math.cos(a + math.pi), 2.55 * math.sin(a + math.pi)
        for k in range(3):
            cubuk(g, (x + k * 0.07, y, ZC + 0.16), (x + k * 0.09 - 0.05, y + 0.03, ZC + 0.9 + 0.15 * k), 0.035, r["cali"], kabuk=False)
    # İskele
    kutu(g, 1.6, 2.4, 1.3, 3.6, ZC + 0.28, ZC + 0.36, r["ahsap"])
    for (x, y) in ((1.65, 1.35), (2.35, 1.35), (1.65, 2.4), (2.35, 2.4)):
        kutu(g, x - 0.05, x + 0.05, y - 0.05, y + 0.05, ZC, ZC + 0.36, r["koyu"])
    # Ördekler
    for (x, y, a) in ((-0.8, -0.6, 0.3), (-0.2, -1.1, 0.9), (0.9, 0.6, 2.5)):
        top(g, (x, y, ZC + 0.26), 0.22, 0.14, 0.1, r["ordek"])
        top(g, (x + 0.16 * math.cos(a), y + 0.16 * math.sin(a), ZC + 0.4), 0.08, 0.08, 0.08, r["ordek"])
        top(g, (x + 0.25 * math.cos(a), y + 0.25 * math.sin(a), ZC + 0.39), 0.05, 0.03, 0.025, r["gaga"])
    bank(g, -2.0, 3.75, -math.pi / 2, r)
    bank(g, 0.6, -3.75, math.pi / 2, r)
    park_lambasi(g, -3.9, 3.6, r)
    return g


def park_anit(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, [(-3.4, -3.4), (3.4, -3.4), (3.4, 3.4), (-3.4, 3.4)], ZC + 0.01, r["tas_acik"])
    for i in range(1, 6):
        p = -3.4 + i * 6.8 / 6
        boya(g, p - 0.02, p + 0.02, -3.4, 3.4, r["tas"], z=ZC + 0.01)
        boya(g, -3.4, 3.4, p - 0.02, p + 0.02, r["tas"], z=ZC + 0.01)
    z = ZC
    for k, (w, h) in enumerate(((1.9, 0.25), (1.5, 0.25), (1.1, 0.9))):
        kutu(g, -w, w, -w, w, z, z + h, r["tas"] if k < 2 else r["tas_acik"], atla=("zn",))
        z += h
    kutu(g, -0.5, 0.5, -1.12, -1.1, z - 0.7, z - 0.2, r["heykel"])        # yazıt levhası (kuzey yüzü)
    # Dikilitaş: aşağıdan yukarı incelen dört yüzlü gövde ve piramit uç
    a0, a1, H = 0.62, 0.38, 6.5
    pts = [(-a0, -a0, z), (a0, -a0, z), (a0, a0, z), (-a0, a0, z), (-a1, -a1, z + H), (a1, -a1, z + H), (a1, a1, z + H), (-a1, a1, z + H)]
    dis_bukey(g, pts, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], r["tas_acik"])
    pts = [(-a1 - 0.05, -a1 - 0.05, z + H), (a1 + 0.05, -a1 - 0.05, z + H), (a1 + 0.05, a1 + 0.05, z + H), (-a1 - 0.05, a1 + 0.05, z + H), (0.0, 0.0, z + H + 0.9)]
    dis_bukey(g, pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], r["sari"])
    for (x, y) in ((-2.6, -2.6), (2.6, -2.6), (-2.6, 2.6), (2.6, 2.6)):
        prizma_poligon(g, poligon(8, 0.75, 0.75, x, y), ZC, ZC + 0.3, r["tas"])
        cicek_obegi(g, rnd, x, y, 0.6, 9, r, ZC + 0.3)
    for (x, y) in ((-2.6, 0.0), (2.6, 0.0)):
        park_lambasi(g, x, y, r)
    bank(g, 0.0, -2.9, math.pi / 2, r)
    bank(g, 0.0, 2.9, -math.pi / 2, r)
    return g


def meydan_cesme(r, rnd):
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_KAL, r["kaldirim"])
    duz_poligon(g, poligon(20, 4.2, 4.2), Z_KAL + 0.005, r["tas_acik"])
    duz_poligon(g, poligon(20, 3.4, 3.4), Z_KAL + 0.008, r["kaldirim2"])
    z = Z_KAL
    prizma_poligon(g, poligon(16, 2.7, 2.7), z, z + 0.6, r["tas"])
    duz_poligon(g, poligon(16, 2.45, 2.45), z + 0.605, r["su"])
    silindir(g, 0.0, 0.0, z + 0.6, 1.2, 0.3, 8, r["tas_acik"])
    prizma_poligon(g, poligon(12, 1.15, 1.15), z + 1.8, z + 2.0, r["tas"])
    duz_poligon(g, poligon(12, 1.0, 1.0), z + 2.005, r["su"])
    silindir(g, 0.0, 0.0, z + 2.0, 0.7, 0.16, 8, r["tas_acik"])
    top(g, (0.0, 0.0, z + 2.9), 0.28, 0.28, 0.32, r["su"])
    for i in range(8):                                                     # dökülen su
        a = 2 * math.pi * i / 8
        cubuk(g, (1.1 * math.cos(a), 1.1 * math.sin(a), z + 1.95), (1.9 * math.cos(a), 1.9 * math.sin(a), z + 0.62), 0.07, r["su"], kabuk=False)
    for i in range(4):
        a = i * math.pi / 2 + math.pi / 4
        bank(g, 3.8 * math.cos(a), 3.8 * math.sin(a), a + math.pi, r, z0=Z_KAL)
        park_lambasi(g, 4.5 * math.cos(a + math.pi / 4), 4.5 * math.sin(a + math.pi / 4), r, z0=Z_KAL)
    return g


def park_parcalari():
    r = park_renkleri()
    rnd = random.Random(TOHUM * 991)
    return [("Park_cim_oyun", park_oyun(r, rnd)), ("Park_cim_cicek", park_cicek(r, rnd)), ("Park_cim_gezinti", park_gezinti(r, rnd)),
            ("Park_gol", park_gol(r, rnd)), ("Park_anit", park_anit(r, rnd)), ("Meydan_cesme", meydan_cesme(r, rnd))]


# ------------------------------------------------------------------ blok içi avlular ve kır süsleri

def park_araba(g, x, y, aci, renk, r, z0):
    """Park etmiş küçük araba (alçak poligon)."""
    u = (math.cos(aci), math.sin(aci), 0.0)
    v = (-u[1], u[0], 0.0)
    yonlu_kutu(g, (x, y, z0 + 0.45), mul(u, 2.0), mul(v, 0.85), (0, 0, 0.3), renk)
    yonlu_kutu(g, add((x, y, z0 + 1.0), mul(u, -0.2)), mul(u, 1.1), mul(v, 0.75), (0, 0, 0.27), renk)
    for su, sv in ((1.25, 0.86), (1.25, -0.86), (-1.25, 0.86), (-1.25, -0.86)):
        c = add(add((x, y, z0 + 0.32), mul(u, su)), mul(v, sv))
        yonlu_kutu(g, c, mul(u, 0.32), mul(v, 0.08), (0, 0, 0.32), r["koyu"])


def avlu_otopark(r, rnd):
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_ASF, r["asfalt"])
    for sy in (-1, 1):
        y0 = sy * 0.6
        for i in range(5):
            x = -4.5 + i * 2.25
            boya(g, x - 0.06, x + 0.06, min(y0, y0 + sy * 4.0), max(y0, y0 + sy * 4.0), r["serit"])
    b = len(g.f)
    kutu(g, -4.8, 4.8, -0.45, 0.45, 0.0, Z_KAL, r["bordur"], atla=("zn",), yuzler={"zp": r["cim"]})   # orta yeşil şerit
    cizgisiz(g, b)
    for x in (-3.0, 1.2):
        top(g, (x, 0.0, Z_KAL + 0.3), 0.5, 0.4, 0.32, r["cali"])
    renkler = [M("Araba", c) for c in ("#d9473a", "#3f73e6", "#f6f3ec", "#2b2d31", "#f2c14a", "#4fb36a", "#a7abb0")]
    for sy in (-1, 1):
        for i in range(4):
            if rnd.random() < 0.3:
                continue
            x = -3.4 + i * 2.25
            park_araba(g, x, sy * 2.8, math.pi / 2, rnd.choice(renkler), r, Z_ASF)
    return g


def avlu_havuz(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, [(-3.9, -3.2), (3.9, -3.2), (3.9, 3.2), (-3.9, 3.2)], ZC + 0.01, r["tas_acik"])
    prizma_poligon(g, [(-3.0, -1.9), (3.0, -1.9), (3.0, 1.9), (-3.0, 1.9)], ZC, ZC + 0.22, r["beyaz"])
    duz_poligon(g, [(-2.75, -1.65), (2.75, -1.65), (2.75, 1.65), (-2.75, 1.65)], ZC + 0.225, r["su"])
    for k in range(1, 4):
        boya(g, -2.75, 2.75, -1.65 + k * 0.82 - 0.02, -1.65 + k * 0.82 + 0.02, r["su_koyu"], z=ZC + 0.226)
    for dx in (-0.25, 0.25):                                                   # merdiven
        cubuk(g, (2.85 + 0.0, dx, ZC + 0.2), (2.85, dx, ZC + 0.9), 0.05, r["metal"])
        cubuk(g, (2.85, dx, ZC + 0.9), (2.55, dx, ZC + 0.9), 0.05, r["metal"])
    sez = M("Sezlong", "#f6f3ec")
    for i in range(3):                                                         # şezlonglar
        x = -2.2 + i * 1.6
        yonlu_kutu(g, (x, -2.6, ZC + 0.3), (0.3, 0, 0), (0, 0.55, 0.0), (0, 0, 0.05), sez)
        yonlu_kutu(g, (x, -2.0, ZC + 0.45), (0.3, 0, 0), (0, 0.12, 0.18), (0, -0.05, 0.04), sez)
    tx, ty = 3.3, -2.6                                                         # şemsiye
    silindir(g, tx, ty, ZC, 1.9, 0.04, 6, r["metal"])
    pts = [(tx + 1.0 * math.cos(2 * math.pi * i / 8), ty + 1.0 * math.sin(2 * math.pi * i / 8), ZC + 1.8) for i in range(8)]
    pts.append((tx, ty, ZC + 2.2))
    dis_bukey(g, pts, [[i, (i + 1) % 8, 8] for i in range(8)] + [list(range(8))[::-1]], r["kirmizi"])
    for (x, y) in ((-3.6, 2.9), (3.6, 2.9), (0.0, 2.9)):
        top(g, (x, y, ZC + 0.32), 0.42, 0.3, 0.32, r["cali"])
    return g


def avlu_bahce(r, rnd):
    g = Govde()
    cim_taban(g, r)
    for i in range(3):                                                         # sebze tarhları
        y = -2.6 + i * 1.5
        kutu(g, -3.6, -0.6, y - 0.45, y + 0.45, ZC, ZC + 0.25, r["ahsap"])
        duz_poligon(g, [(-3.5, y - 0.35), (-0.7, y - 0.35), (-0.7, y + 0.35), (-3.5, y + 0.35)], ZC + 0.255, r["toprak"])
        for k in range(5):
            top(g, (-3.2 + k * 0.6, y, ZC + 0.42), 0.17, 0.17, 0.15, r["cali"] if i != 1 else rnd.choice(r["cicek"]))
    kutu(g, 1.6, 3.6, 1.4, 3.4, ZC, ZC + 1.9, r["ahsap"])                      # bahçe kulübesi
    pts = [(1.45, 1.25, ZC + 1.9), (3.75, 1.25, ZC + 1.9), (2.6, 1.25, ZC + 2.6),
           (1.45, 3.55, ZC + 1.9), (3.75, 3.55, ZC + 1.9), (2.6, 3.55, ZC + 2.6)]
    dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["kirmizi"])
    kutu(g, 2.3, 2.9, 1.38, 1.4, ZC, ZC + 1.5, r["koyu"])
    for x in (1.0, 3.8):                                                       # çamaşır ipi
        cubuk(g, (x, -2.6, ZC), (x, -2.6, ZC + 1.7), 0.06, r["metal"])
    cubuk(g, (1.0, -2.6, ZC + 1.6), (3.8, -2.6, ZC + 1.6), 0.02, r["beyaz"], kabuk=False)
    for i, c in enumerate(("kirmizi", "mavi", "beyaz", "sari")):
        x = 1.4 + i * 0.65
        kutu(g, x - 0.22, x + 0.22, -2.62, -2.58, ZC + 1.05, ZC + 1.58, r[c])
    bank(g, 2.6, -0.4, math.pi, r)
    return g


def kir_cicek(r, rnd):
    g = Govde()
    for _ in range(16):
        a, d = rnd.uniform(0, 2 * math.pi), 1.1 * math.sqrt(rnd.random())
        x, y = d * math.cos(a), d * math.sin(a)
        if rnd.random() < 0.3:
            top(g, (x, y, 0.15), 0.22, 0.22, 0.16, r["cali"])
        else:
            top(g, (x, y, 0.2), 0.14, 0.14, 0.12, rnd.choice(r["cicek"]))
    return g


def kir_kaya(r, rnd):
    g = Govde()
    kaya = M("Kaya", "#9a958c")
    for i in range(3):
        s = 0.7 - i * 0.18
        top(g, (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), s * 0.4), s, s * 0.85, s * 0.6, kaya)
    top(g, (0.6, 0.3, 0.12), 0.25, 0.25, 0.18, r["cali"])
    return g


def avlu_parcalari():
    r = park_renkleri()
    r["cam_koyu"] = M("CamKoyu", "#5f7f99")
    rnd = random.Random(TOHUM * 313)
    return [("Avlu_otopark", avlu_otopark(r, rnd)), ("Avlu_havuz", avlu_havuz(r, rnd)), ("Avlu_bahce", avlu_bahce(r, rnd)),
            ("Kir_cicek_1", kir_cicek(r, rnd)), ("Kir_cicek_2", kir_cicek(r, rnd)), ("Kir_kaya_1", kir_kaya(r, rnd))]


# ------------------------------------------------------------------ altı dükkanlı Avrupa binaları (oyundaki dükkanlar + genel dükkanlar)

def _hex(r, g, b):
    return "#%02x%02x%02x" % (int(r * 255), int(g * 255), int(b * 255))


# Oyundaki dükkanlar (Data/GameData.cs): ad, tabela rengi, tür (yemek / kafe / market / restoran)
DUKKANLAR = {
    "doner_kral":     ("Döner Kral", _hex(0.80, 0.20, 0.20), "yemek"),
    "starbocks":      ("Starbocks", _hex(0.10, 0.55, 0.35), "kafe"),
    "pizza_hat":      ("Pizza Hat", _hex(0.85, 0.10, 0.35), "yemek"),
    "kfg":            ("KFG", _hex(0.70, 0.15, 0.10), "yemek"),
    "sushi_samurai":  ("Sushi Samurai", "#2b2d31", "restoran"),
    "fish_ships":     ("Fish & Ships", _hex(0.20, 0.55, 0.75), "yemek"),
    "otogar_bufe":    ("Otogar Büfé", _hex(0.90, 0.62, 0.15), "yemek"),
    "villa_gourmet":  ("Villa Gourmet", _hex(0.55, 0.25, 0.45), "restoran"),
    "nonstop_gyros":  ("Non-Stop Gyros", _hex(0.90, 0.35, 0.25), "yemek"),
    "bioszpar":       ("BioSzpar", _hex(0.45, 0.70, 0.25), "market"),
    "abc_nonstop":    ("ABC Non-Stop", _hex(0.30, 0.55, 0.85), "market"),
    "sarok_kisbolt":  ("Sarok Kisbolt", _hex(0.55, 0.45, 0.80), "market"),
    "ejjel_nappali":  ("Éjjel-Nappali", _hex(0.25, 0.65, 0.70), "market"),
    "mini_abc":       ("Mini ABC", _hex(0.65, 0.55, 0.30), "market"),
    "kikoto_nonstop": ("Kikötő Non-Stop", _hex(0.20, 0.45, 0.65), "market"),
    "falu_abc":       ("Falu ABC", _hex(0.50, 0.65, 0.35), "market"),
    # Şehir dokusu için genel dükkanlar (Macar şehir havası)
    "genel_pekseg":   ("PÉKSÉG", "#b5703a", "kafe"),
    "genel_virag":    ("VIRÁG", "#d0608a", "cicek"),
    "genel_konyv":    ("KÖNYV", "#3f5f7a", "kitap"),
    "genel_patika":   ("PATIKA", "#3f9a5a", "eczane"),
    "genel_kavezo":   ("KÁVÉZÓ", "#7a4a2e", "kafe"),
    "genel_sajt":     ("SAJT", "#d9a83a", "kitap"),
}

DUKKAN_DUVAR = ["#f1e4c8", "#ecd3a4", "#f0cdb0", "#d9dfc6", "#cfdbe3", "#e8d6c8", "#f4f0e6", "#b8634a", "#9c4a35"]


def dukkan_renkleri(rnd, ana):
    duvar = rnd.choice(DUKKAN_DUVAR)
    tugla = duvar in ("#b8634a", "#9c4a35")
    cati = rnd.choice(["#5b5d63", "#5b5d63", "#c0573a", "#b04a33"])
    r = {
        "duvar": M("Duvar", duvar), "suslu": M("Suslu", "#f8f4ea"), "taban": M("Taban", "#b8b0a3"),
        "cati": M("Cati", cati), "cati_koyu": M("CatiKoyu", koyu_ton(cati)), "baca": M("Baca", "#c9b9a3" if not tugla else duvar),
        "ana": M("Marka", ana), "ikinci": M("Marka2", koyu_ton(ana, 0.7)), "yazi": M("Yazi", "#ffffff"),
        "cam": M("Cam", SABIT["cam"]), "cerceve": M("TCerceve", "#3a3d42"), "kapi": M("Kapi", rnd.choice(["#7a4a2e", "#3f5f7a", "#2b2d31"])),
        "demir": M("Demir", SABIT["demir"]), "saksi": M("Saksi", SABIT["saksi"]), "bitki": M("Bitki", SABIT["bitki"]),
        "cicek": [M("Cicek", c) for c in EV_SABIT["cicek"]], "cali": M("Cali", EV_SABIT["cali"]),
        "kaldirim": M("TKaldirim", TIC_SABIT["kaldirim"]), "bordur": M("TBordur", TIC_SABIT["bordur"]),
        "beton": M("TBeton", TIC_SABIT["beton"]), "klima": M("Klima", SABIT["klima"]), "koyu": M("TKoyu", TIC_SABIT["koyu"]),
        "metal": M("TMetal", TIC_SABIT["metal"]), "beyaz": M("TBeyaz", "#f6f3ec"), "ahsap": M("TAhsap", TIC_SABIT["ahsap"]),
        "urun": [M("Urun", c) for c in ("#e0525a", "#f2b84a", "#6fae4a", "#f2e3c0", "#8a5a9a")],
        "govde": M("AgacGovde", SABIT["govde"]), "agac": [M("Agac", c) for c in SABIT["agac"]],
        "kemer": "bloklu" if rnd.random() < 0.4 else "duz", "panjur": M("Panjur", rnd.choice(["#5f8a6a", "#4f6f8f", "#8a5a3a"])),
        "_tugla": tugla,
    }
    return r


def mansart_cati(g, x0, x1, y0, y1, zt, r, rnd, n_dormer):
    """Haussmann tarzı mansart çatı: dik alt eğim (arduvaz), üstte düz çinko, önde çatı pencereleri."""
    o, ic, h = 0.12, 0.85, 2.4
    kutu(g, x0 - 0.25, x1 + 0.25, y0 - 0.25, y1 + 0.25, zt - 0.25, zt, r["suslu"], atla=("zn",))         # korniş
    pts = [(x0 - o, y0 - o, zt), (x1 + o, y0 - o, zt), (x1 + o, y1 + o, zt), (x0 - o, y1 + o, zt),
           (x0 + ic, y0 + ic, zt + h), (x1 - ic, y0 + ic, zt + h), (x1 - ic, y1 - ic, zt + h), (x0 + ic, y1 - ic, zt + h)]
    dis_bukey(g, pts, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], r["cati"])
    kutu(g, x0 + ic - 0.05, x1 - ic + 0.05, y0 + ic - 0.05, y1 - ic + 0.05, zt + h, zt + h + 0.12, r["cati_koyu"])
    W = x1 - x0
    for i in range(n_dormer):                                                   # çatı pencereleri
        cx = x0 + W * (i + 0.5) / n_dormer
        yd = y0 + 0.05
        kutu(g, cx - 0.45, cx + 0.45, yd, yd + 1.2, zt + 0.2, zt + 1.55, r["suslu"], atla=("zn",))
        dfc = Cephe((cx - 0.45, yd, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        cdortgen(g, dfc, 0.15, 0.75, 0.01, zt + 0.4, zt + 1.3, r["cam"])
        cprizma(g, dfc, [(-0.05, zt + 1.55), (0.95, zt + 1.55), (0.45, zt + 1.95)], 0.0, 1.2, r["cati"])
    ym = (y0 + y1) / 2
    for bx in (x0 + 0.6, x1 - 0.6):                                             # bacalar (yan duvarlarda)
        kutu(g, bx - 0.35, bx + 0.35, ym - 0.7, ym + 0.7, zt, zt + h + 0.9, r["baca"])
        for k in range(3):
            silindir(g, bx, ym - 0.4 + k * 0.4, zt + h + 0.9, 0.3, 0.09, 6, r["saksi"])


def dukkan_on_kati(g, fc, W, zb, Hz, r, rnd, ad, tur):
    """Zemin kat dükkanı: vitrin, kapı, tente, tabela; türüne göre önünde masa, kasa, çiçek, kitap sergisi."""
    kapi_u = W * rnd.choice([0.25, 0.75]) if W > 7 else W / 2
    vitrin(g, fc, 0.4, kapi_u - 0.75, zb + 0.45, zb + Hz - 1.25, r, aralik=1.2, kasa=r["ikinci"], poster=0.3, rnd=rnd)
    vitrin(g, fc, kapi_u + 0.75, W - 0.4, zb + 0.45, zb + Hz - 1.25, r, aralik=1.2, kasa=r["ikinci"], poster=0.3, rnd=rnd)
    ckutu(g, fc, kapi_u - 0.65, kapi_u + 0.65, 0, 0.08, zb, zb + Hz - 1.15, r["ikinci"])
    cdortgen(g, fc, kapi_u - 0.55, kapi_u + 0.55, 0.09, zb + 0.05, zb + Hz - 1.25, r["cam"])
    ckutu(g, fc, -0.02, W + 0.02, 0, 0.12, zb, zb + 0.45, r["ikinci"])                     # vitrin altı
    # Tabela bandı ve yazı
    tabela(g, fc, W / 2, zb + Hz - 0.6, W - 0.3, 0.7, 0.14, r, ad, renk=r["ana"], yazi_renk=r["yazi"], oran=0.62)
    # Tente (çizgili), tabelanın altında
    tente(g, fc, 0.25, W - 0.25, zb + Hz - 1.55, 1.4, r, r["ana"], r["beyaz"])
    # Dik tabela (köşede, iki yüzü yazılı değil: renkli kutu ve beyaz çerçeve)
    bu = W - 0.1
    ckutu(g, fc, bu - 0.05, bu + 0.05, 0, 0.9, zb + Hz + 0.3, zb + Hz + 0.38, r["demir"])
    p1, p2 = fc.P(bu - 0.06, 0.25, 0.0), fc.P(bu + 0.06, 0.95, 0.0)
    kutu(g, min(p1[0], p2[0]), max(p1[0], p2[0]), min(p1[1], p2[1]), max(p1[1], p2[1]), zb + Hz - 0.6, zb + Hz + 0.3, r["ana"])
    # Önünde türüne göre eşya
    z = zb
    if tur in ("yemek", "kafe", "restoran"):
        for i, uu in enumerate((0.9, 2.6) if kapi_u > W / 2 else (W - 2.6, W - 0.9)):
            p = fc.P(uu, 2.0, z)
            silindir(g, p[0], p[1], z, 0.72, 0.05, 6, r["koyu"])
            silindir(g, p[0], p[1], z + 0.72, 0.04, 0.38, 8, r["beyaz"])
            for d in (-0.55, 0.55):
                q = fc.P(uu + d, 2.0, z)
                kutu(g, q[0] - 0.18, q[0] + 0.18, q[1] - 0.18, q[1] + 0.18, z, z + 0.45, r["ahsap"])
            if tur == "restoran":
                silindir(g, p[0], p[1], z + 0.76, 0.18, 0.06, 6, r["saksi"])
    elif tur == "market":
        for i in range(3):
            uu = (0.8 + i * 0.75) if kapi_u > W / 2 else (W - 0.8 - i * 0.75)
            p = fc.P(uu, 1.0, z)
            kutu(g, p[0] - 0.32, p[0] + 0.32, p[1] - 0.28, p[1] + 0.28, z, z + 0.55, r["ahsap"])
            kutu(g, p[0] - 0.28, p[0] + 0.28, p[1] - 0.24, p[1] + 0.24, z + 0.55, z + 0.62, rnd.choice(r["urun"]))
    elif tur == "cicek":
        for i in range(4):
            uu = 0.7 + i * 0.6 if kapi_u > W / 2 else W - 0.7 - i * 0.6
            p = fc.P(uu, 0.9, z)
            silindir(g, p[0], p[1], z, 0.35, 0.2, 7, r["saksi"])
            top(g, (p[0], p[1], z + 0.55), 0.24, 0.24, 0.24, rnd.choice(r["cicek"]))
    elif tur == "kitap":
        uu = 1.2 if kapi_u > W / 2 else W - 1.2
        p = fc.P(uu, 0.8, z)
        kutu(g, p[0] - 0.7, p[0] + 0.7, p[1] - 0.3, p[1] + 0.3, z, z + 0.8, r["ahsap"])
        for k in range(5):
            kutu(g, p[0] - 0.6 + k * 0.25, p[0] - 0.42 + k * 0.25, p[1] - 0.2, p[1] + 0.2, z + 0.8, z + 1.05, rnd.choice(r["urun"]))
    elif tur == "eczane":
        p = fc.P(W - 0.1, 0.55, zb + Hz + 0.1)
        kutu(g, p[0] - 0.08, p[0] + 0.08, p[1] - 0.35, p[1] + 0.35, p[2] - 0.35, p[2] + 0.35, r["ana"])
    kapi_lambasi(g, fc, kapi_u + 0.95, zb + Hz - 1.0)
    return kapi_u


def dukkanli_bina(rnd, anahtar):
    g = Govde()
    ad, ana, tur = DUKKANLAR[anahtar]
    r = dukkan_renkleri(rnd, ana)
    W = rnd.uniform(8.0, 9.6)
    D = rnd.uniform(8.0, 9.0)
    PW, PD = W + 1.6, D + 4.2
    x0, fy = 0.8, 3.2
    x1, by = x0 + W, fy + D
    z0 = EV_ZEMIN
    zb = z0 + 0.14
    Hz = 4.2                                                   # dükkan katı
    KY = 3.0
    kat = rnd.randint(2, 4)
    Ht = zb + Hz + kat * KY
    stil = rnd.choice(["mansart", "mansart", "kiremit", "duz"])

    levha(g, 0.0, PW, 0.0, PD, 0.0, z0, r["kaldirim"])
    levha(g, 0.0, PW, 0.0, fy, z0, zb, r["kaldirim"])
    on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    sol = Cephe((x0, by, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    sag = Cephe((x1, fy, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))
    arka = Cephe((x1, by, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
    kutu(g, x0, x1, fy, by, zb, Ht, r["duvar"], atla=("zn",))
    kutu(g, x0 - 0.06, x1 + 0.06, fy + 0.02, by + 0.06, zb, zb + Hz, r["taban"] if not r["_tugla"] else r["duvar"], atla=("zn", "zp", "yn"))
    ckutu(g, on, -0.06, W + 0.06, 0, 0.25, zb + Hz - 0.1, zb + Hz + 0.15, r["suslu"])           # kat bandı

    dukkan_on_kati(g, on, W, zb, Hz, r, rnd, ad, tur)

    # Üst katlar: pencereler, Fransız balkonları; ilk katta boydan boya balkon (Haussmann)
    n = max(3, int(W / 2.2))
    for f in range(kat):
        z = zb + Hz + f * KY + 0.55
        for i in range(n):
            u = W * (i + 0.5) / n
            pencere(g, on, u, z, 0.9, 1.75, r, sade=False)
            if stil == "mansart" and f != 1:
                for k in range(6):
                    uu = u - 0.5 + k * 0.2
                    ckutu(g, on, uu - 0.02, uu + 0.02, 0.18, 0.22, z - 0.05, z + 0.6, r["demir"])
                ckutu(g, on, u - 0.55, u + 0.55, 0.16, 0.24, z + 0.6, z + 0.66, r["demir"])
            elif rnd.random() < 0.3:
                p = on.P(u, 0.25, z - 0.08)
                kutu(g, p[0] - 0.45, p[0] + 0.45, p[1] - 0.12, p[1] + 0.12, z - 0.3, z - 0.08, r["saksi"])
                top(g, (p[0], p[1], z), 0.3, 0.12, 0.1, rnd.choice(r["cicek"]))
        if stil == "mansart" and f == 1:
            zz = z - 0.35
            ckutu(g, on, -0.1, W + 0.1, 0, 0.8, zz - 0.15, zz, r["suslu"])
            for k in range(int(W / 0.2)):
                uu = 0.0 + k * 0.2
                ckutu(g, on, uu - 0.02, uu + 0.02, 0.74, 0.78, zz, zz + 0.85, r["demir"])
            ckutu(g, on, -0.1, W + 0.1, 0.72, 0.8, zz + 0.85, zz + 0.92, r["demir"])
        if f < kat - 1:
            ckutu(g, on, -0.03, W + 0.03, 0, 0.08, zb + Hz + (f + 1) * KY - 0.05, zb + Hz + (f + 1) * KY + 0.06, r["suslu"])
        for fc, L in ((sol, D), (sag, D)):
            for u in (L * 0.3, L * 0.7):
                pencere(g, fc, u, z, 0.8, 1.5, r, sade=True)
        for i in range(n):
            pencere(g, arka, W * (i + 0.5) / n, z, 0.8, 1.4, r, sade=True)
        if rnd.random() < 0.35:
            klima_dis(g, rnd.choice([sol, sag]), D * 0.5, z - 0.3)

    # Çatı
    if stil == "mansart":
        mansart_cati(g, x0, x1, fy, by, Ht, r, rnd, n)
    elif stil == "kiremit":
        r2 = dict(r)
        r2["cati"], r2["cati_koyu"] = M("Cati", "#c0573a"), M("CatiKoyu", koyu_ton("#c0573a"))
        kutu(g, x0 - 0.05, x1 + 0.05, fy - 0.05, by + 0.05, Ht - 0.2, Ht, r["suslu"], atla=("zn", "zp"))
        zr = besik_cati(g, "x", x0, x1, fy, by, Ht, r2)
        ev_baca(g, x0 + W * 0.75, fy + D / 2 + 0.6, cati_z(Ht, fy, by, fy + D / 2 + 0.6) - 0.3, zr + 0.45, r2)
    else:
        kutu(g, x0 - 0.2, x1 + 0.2, fy - 0.2, by + 0.2, Ht, Ht + 0.35, r["suslu"], atla=("zn",))
        kutu(g, x0 - 0.1, x1 + 0.1, fy - 0.1, by + 0.1, Ht + 0.35, Ht + 0.85, r["duvar"], atla=("zn",), yuzler={"zp": r["beton"]})
        cati_anteni(g, x0 + W * 0.3, fy + D * 0.6, Ht + 0.85)
        gunes_paneli(g, x0 + W * 0.65, fy + D * 0.4, Ht + 0.85, 1.8, 1.1)

    for xx in (x0 - 0.07, x1 + 0.07):
        oluk(g, xx, fy - 0.07, zb, Ht - 0.1)
    # Kaldırım: ağaç ya da lamba, bisiklet
    if rnd.random() < 0.6:
        agac(g, PW - 0.6, 1.0, r, rnd, boy=1.4, rr=0.75)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


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


def karakter_blok(rnd, kurye):
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


# ---------- yuvarlak karakter (kapsül ve elipslerden, yumuşak gölgeli)

def _taban_eksen(d):
    """Eksene dik iki yön: u mümkünse X'e yakın (gövdede genişlik), v = d × u."""
    u0 = (1.0, 0.0, 0.0) if abs(d[0]) < 0.9 else (0.0, 1.0, 0.0)
    u = norm(sub(u0, mul(d, dot(u0, d))))
    return u, cross(d, u)


def kapsul(g, p0, p1, r0, r1, mat, seg=8, nr=2, kesit=(1.0, 1.0), uc=1.0, kabuk=True):
    """
    Uçları yuvarlak, kalınlığı p0'dan p1'e değişen kapsül (kol, bacak, gövde). kesit: enine kesitin (u, v) ölçeği
    (gövde için yassı elips), uc: yuvarlak uçların eksen boyunca basıklığı.
    """
    d = sub(p1, p0)
    L = math.sqrt(dot(d, d))
    d = norm(d) if L > 1e-6 else (0.0, 0.0, 1.0)
    u, v = _taban_eksen(d)
    a = math.asin(max(-0.9, min(0.9, (r0 - r1) / L))) if L > 1e-6 else 0.0
    halkalar = []
    for k in range(1, nr + 1):                                  # alt yarım küre: kutuptan teğet çizgisine
        f = -math.pi / 2 + (a + math.pi / 2) * k / nr
        halkalar.append((p0, r0, f))
    for k in range(nr):                                         # üst yarım küre: teğet çizgisinden kutba
        f = a + (math.pi / 2 - a) * k / nr
        halkalar.append((p1, r1, f))
    pts = [add(p0, mul(d, -r0 * uc))]
    for c, r, f in halkalar:
        m = add(c, mul(d, r * math.sin(f) * uc))
        rho = r * math.cos(f)
        for j in range(seg):
            t = 2 * math.pi * j / seg
            pts.append(add(m, add(mul(u, rho * kesit[0] * math.cos(t)), mul(v, rho * kesit[1] * math.sin(t)))))
    pts.append(add(p1, mul(d, r1 * uc)))
    n = len(halkalar)
    son = len(pts) - 1
    yuzler = [[0, 1 + (j + 1) % seg, 1 + j] for j in range(seg)]
    for h in range(n - 1):
        a0, a1 = 1 + h * seg, 1 + (h + 1) * seg
        for j in range(seg):
            jj = (j + 1) % seg
            yuzler.append([a0 + j, a0 + jj, a1 + jj, a1 + j])
    a0 = 1 + (n - 1) * seg
    yuzler += [[a0 + j, a0 + (j + 1) % seg, son] for j in range(seg)]
    dis_bukey(g, pts, yuzler, mat, kabuk, yumusak=True)


def elips(g, c, rx, ry, rz, mat, seg=10, nr=5, kabuk=True):
    """Elipsoit (baş, el, göz, saç topu): nr enlem bandı."""
    pts = [(c[0], c[1], c[2] - rz)]
    for k in range(1, nr):
        f = -math.pi / 2 + math.pi * k / nr
        for j in range(seg):
            t = 2 * math.pi * j / seg
            pts.append((c[0] + rx * math.cos(f) * math.cos(t), c[1] + ry * math.cos(f) * math.sin(t), c[2] + rz * math.sin(f)))
    pts.append((c[0], c[1], c[2] + rz))
    son = len(pts) - 1
    yuzler = [[0, 1 + (j + 1) % seg, 1 + j] for j in range(seg)]
    for h in range(nr - 2):
        a0, a1 = 1 + h * seg, 1 + (h + 1) * seg
        for j in range(seg):
            jj = (j + 1) % seg
            yuzler.append([a0 + j, a0 + jj, a1 + jj, a1 + j])
    a0 = 1 + (nr - 2) * seg
    yuzler += [[a0 + j, a0 + (j + 1) % seg, son] for j in range(seg)]
    dis_bukey(g, pts, yuzler, mat, kabuk, yumusak=True)


def karakter_yuvarlak(rnd, kurye):
    g = Govde()
    ten_hex = rnd.choice(TENLER)
    ten = M("Ten", ten_hex)
    koyu_ten = M("TenKoyu", "#%02x%02x%02x" % tuple(int(int(ten_hex[i:i + 2], 16) * 0.88) for i in (1, 3, 5)))
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
    w = rnd.uniform(0.95, 1.08)

    # Bacaklar: uyluk, baldır, yuvarlak ayakkabı
    for s, yan in ((1, "Left"), (-1, "Right")):
        x = 0.1 * s
        g.kemik = yan + "Leg"
        kapsul(g, (x, 0.035, 0.042), (x, -0.105, 0.04), 0.052, 0.05, ayak, seg=8, nr=2, kesit=(1.0, 0.8))
        kapsul(g, (x, 0.0, 0.46), (x, 0.0, 0.11), 0.06, 0.045, ten if sort else alt)
        g.kemik = yan + "UpLeg"
        kapsul(g, (x, 0.0, 0.83), (x, 0.0, 0.48), 0.082, 0.063, alt)
    g.kemik = "Hips"
    elips(g, (0.0, 0.0, 0.9), 0.165 * w, 0.105, 0.1, alt, seg=10, nr=4)
    if not kurye and rnd.random() < 0.25:
        pahli_kutu(g, (-0.215, 0.0, 0.93), (0.035, 0.1, 0.08), 0.02, M("Canta", rnd.choice(["#6b4a33", "#2f2f33", "#a33f3f"])))

    # Gövde (yassı, omuzlara doğru genişleyen) ve kollar
    g.kemik = "Spine"
    kapsul(g, (0.0, 0.0, 1.03), (0.0, 0.0, 1.28), 0.155 * w, 0.185 * w, ust, seg=12, nr=3, kesit=(1.0, 0.62), uc=0.65)
    for s, yan in ((1, "Left"), (-1, "Right")):
        x = 0.245 * s
        g.kemik = yan + "Arm"
        kapsul(g, (x, 0.0, 1.345), (x, 0.0, 1.12), 0.058, 0.048, ust)
        g.kemik = yan + "ForeArm"
        kapsul(g, (x, 0.0, 1.1), (x, 0.0, 0.9), 0.046, 0.038, ten if kisa_kol else ust)
        elips(g, (x, -0.005, 0.835), 0.036, 0.042, 0.055, ten, seg=8, nr=4)

    if kurye:
        g.kemik = "Spine"
        ikinci = M("SirketRengi2", SIRKET_RENGI2)
        pahli_kutu(g, (0.0, 0.24, 1.22), (0.18, 0.12, 0.2), 0.05, ust)
        pahli_kutu(g, (0.0, 0.24, 1.37), (0.19, 0.13, 0.035), 0.02, ikinci)
        on_dortgen(g, -0.08, 0.08, 0.3615, 1.14, 1.28, ikinci, arkaya=True)
        koyu = M("Aski", "#26262a")
        for s in (1, -1):
            kapsul(g, (0.095 * s, -0.1, 1.37), (0.1 * s, -0.113, 1.06), 0.017, 0.017, koyu, seg=6, nr=1, kabuk=False)

    # Boyun, baş, yüz
    g.kemik = "Head"
    kapsul(g, (0.0, 0.0, 1.37), (0.0, 0.0, 1.47), 0.046, 0.046, ten, seg=8, nr=1)
    elips(g, (0.0, 0.0, 1.6), 0.115, 0.125, 0.14, ten, seg=12, nr=6)
    for s in (1, -1):
        elips(g, (0.115 * s, 0.005, 1.595), 0.018, 0.03, 0.04, koyu_ten, seg=6, nr=3)                 # kulak
        elips(g, (0.042 * s, -0.111, 1.602), 0.017, 0.011, 0.023, goz, seg=6, nr=3, kabuk=False)    # göz
    elips(g, (0.0, -0.126, 1.57), 0.018, 0.02, 0.026, koyu_ten, seg=6, nr=3, kabuk=False)          # burun

    def kas():
        for s in (1, -1):
            kapsul(g, (0.022 * s, -0.112, 1.64), (0.062 * s, -0.104, 1.637), 0.008, 0.008, sac, seg=5, nr=1, kabuk=False)

    kisa_sac = lambda: elips(g, (0.0, 0.02, 1.665), 0.123, 0.133, 0.115, sac, seg=12, nr=6)
    if kurye:
        kas()
        if rnd.random() < 0.7:                                          # kask + vizör
            elips(g, (0.0, 0.005, 1.69), 0.138, 0.147, 0.11, ikinci, seg=12, nr=6)
            elips(g, (0.0, -0.128, 1.7), 0.09, 0.05, 0.013, M("Vizor", "#3a4450"), seg=10, nr=3)      # kısa siperlik
            for s in (1, -1):                                                                          # havalandırma çizgileri
                kapsul(g, (0.045 * s, -0.06, 1.79), (0.05 * s, 0.09, 1.785), 0.012, 0.012, ust, seg=5, nr=1, kabuk=False)
        else:                                                           # şirket şapkası
            elips(g, (0.0, 0.012, 1.68), 0.126, 0.136, 0.09, ust, seg=12, nr=5)
            elips(g, (0.0, -0.13, 1.65), 0.1, 0.075, 0.014, ust, seg=10, nr=3)
    else:
        model = rnd.randrange(7)
        if model != 6:
            kas()
        if model == 0:
            kisa_sac()
        elif model == 1:                                                # uzun
            kisa_sac()
            kapsul(g, (0.0, 0.075, 1.63), (0.0, 0.085, 1.42), 0.11, 0.095, sac, seg=10, nr=2, kesit=(1.0, 0.55))
        elif model == 2:                                                # topuz
            kisa_sac()
            elips(g, (0.0, 0.085, 1.79), 0.055, 0.055, 0.05, sac, seg=8, nr=4)
        elif model == 3:                                                # çok kısa
            elips(g, (0.0, 0.006, 1.648), 0.119, 0.13, 0.107, sac, seg=12, nr=6)
        elif model == 4:                                                # şapka
            kisa_sac()
            sapka = M("Sapka", rnd.choice(USTLER))
            elips(g, (0.0, 0.012, 1.685), 0.127, 0.137, 0.09, sapka, seg=12, nr=5)
            elips(g, (0.0, -0.13, 1.655), 0.1, 0.075, 0.014, sapka, seg=10, nr=3)
        elif model == 5:                                                # kıvırcık
            elips(g, (0.0, 0.02, 1.665), 0.122, 0.132, 0.11, sac, seg=12, nr=6)
            for k in range(9):
                t = 2 * math.pi * k / 9
                elips(g, (0.11 * math.cos(t), 0.03 + 0.11 * math.sin(t), 1.69 + 0.03 * math.sin(t)), 0.06, 0.06, 0.06, sac, seg=7, nr=4)
            elips(g, (0.0, 0.02, 1.76), 0.07, 0.07, 0.05, sac, seg=8, nr=4)
        else:                                                           # at kuyruğu
            kas()
            kisa_sac()
            elips(g, (0.0, 0.13, 1.66), 0.045, 0.045, 0.045, sac, seg=8, nr=4)
            kapsul(g, (0.0, 0.15, 1.64), (0.0, 0.17, 1.47), 0.04, 0.025, sac, seg=7, nr=2)
        if model in (0, 3, 4, 5) and rnd.random() < 0.18:               # sakal
            elips(g, (0.0, -0.06, 1.515), 0.1, 0.075, 0.065, sac, seg=10, nr=4)

    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * 0.5)
    return g, 0.6


def karakter(rnd, kurye):
    return karakter_blok(rnd, kurye) if KARAKTER_STIL == "blok" else karakter_yuvarlak(rnd, kurye)


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



# ------------------------------------------------------------------ katalog aileleri: konut, ofis (büro), sanayi, kamu, park

def ar(rnd, duvar, cati="#c0573a", ana="#3f73e6"):
    """Ailelerin ortak paleti: park/yol renkleri + bina renkleri."""
    r = park_renkleri()
    r.update({
        "duvar": M("Duvar", duvar), "suslu": M("Suslu", "#f8f4ea"), "taban": M("Taban", "#b8b0a3"),
        "cati": M("Cati", cati), "cati_koyu": M("CatiKoyu", koyu_ton(cati)), "baca": M("Baca", "#a8553f"),
        "cam": M("Cam", SABIT["cam"]), "kemer": "duz", "kapi": M("Kapi", rnd.choice(["#7a4a2e", "#3f5f7a", "#5f7f4a", "#2b2d31"])),
        "panjur": M("Panjur", rnd.choice(["#5f8a6a", "#4f6f8f", "#8a5a3a"])), "saksi": M("Saksi", SABIT["saksi"]),
        "bitki": M("Bitki", SABIT["bitki"]), "demir": M("Demir", SABIT["demir"]), "beton": M("TBeton", TIC_SABIT["beton"]),
        "klima": M("Klima", SABIT["klima"]), "ana": M("Marka", ana), "ikinci": M("Marka2", koyu_ton(ana, 0.7)),
        "yazi": M("Yazi", "#ffffff"), "cerceve": M("TCerceve", "#3a3d42"), "yol": M("Yol", EV_SABIT["yol"]),
        "urun": [M("Urun", c) for c in ("#e0525a", "#f2b84a", "#6fae4a", "#f2e3c0", "#8a5a9a")],
        "tugla": M("Tugla", "#a8503a"), "ahsap_koyu": M("AhsapKoyu", "#5a3b26"), "cinko": M("Cinko", "#8f949a"),
    })
    return r


def cepheler(x0, x1, y0, y1):
    return (Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0)), Cephe((x1, y0, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)),
            Cephe((x0, y1, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0)), Cephe((x1, y1, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0)))


def pencereler(g, fc, L, z0, kat, KY, r, w=0.9, h=1.5, aralik=2.0, sade=False, atla=(), bas=0):
    n = max(1, int((L - 0.4) / aralik))
    for f in range(bas, kat):
        z = z0 + f * KY + 0.8
        for i in range(n):
            u = L * (i + 0.5) / n
            if any(a <= u <= b for a, b in atla) and f == bas:
                continue
            pencere(g, fc, u, z, w, h, r, sade=sade)


def kalca_cati(g, x0, x1, y0, y1, zt, egim, mat, sacak=0.35):
    """Kırma (dört eğimli) çatı; kare tabanda piramit."""
    x0, x1, y0, y1 = x0 - sacak, x1 + sacak, y0 - sacak, y1 + sacak
    W, D = x1 - x0, y1 - y0
    zc = zt - sacak * egim
    if W >= D:
        h = D / 2 * egim
        xa, xb, ym = x0 + D / 2, x1 - D / 2, (y0 + y1) / 2
        xb = max(xb, xa + 0.02)
        pts = [(x0, y0, zc), (x1, y0, zc), (x1, y1, zc), (x0, y1, zc), (xa, ym, zc + h), (xb, ym, zc + h)]
        f = [[0, 1, 2, 3], [0, 1, 5, 4], [2, 3, 4, 5], [0, 4, 3], [1, 2, 5]]
    else:
        h = W / 2 * egim
        ya, yb, xm = y0 + W / 2, y1 - W / 2, (x0 + x1) / 2
        yb = max(yb, ya + 0.02)
        pts = [(x0, y0, zc), (x1, y0, zc), (x1, y1, zc), (x0, y1, zc), (xm, ya, zc + h), (xm, yb, zc + h)]
        f = [[0, 1, 2, 3], [1, 2, 5, 4], [3, 0, 4, 5], [0, 1, 4], [2, 3, 5]]
    dis_bukey(g, pts, f, mat)
    return zc + h


def saat(g, fc, cu, zc, rad, r, o=0.0):
    """Cephede saat kadranı (çerçeve, beyaz kadran, akrep ve yelkovan)."""
    for rr, oo, mat in ((rad + 0.1, 0.06, r["koyu"]), (rad, 0.1, r["beyaz"])):
        pts = [fc.P(cu + rr * math.cos(2 * math.pi * i / 12), o, zc + rr * math.sin(2 * math.pi * i / 12)) for i in range(12)]
        pts += [fc.P(cu + rr * math.cos(2 * math.pi * i / 12), o + oo, zc + rr * math.sin(2 * math.pi * i / 12)) for i in range(12)]
        yuz = [list(range(12)), list(range(12, 24))] + [[i, (i + 1) % 12, 12 + (i + 1) % 12, 12 + i] for i in range(12)]
        dis_bukey(g, pts, yuz, mat)
    ckutu(g, fc, cu - 0.03, cu + 0.03, o + 0.1, o + 0.13, zc, zc + rad * 0.75, r["koyu"])
    ckutu(g, fc, cu, cu + rad * 0.55, o + 0.1, o + 0.13, zc - 0.03, zc + 0.03, r["koyu"])


def kaldirim_arsa(g, PW, PD, r, on_d=2.2, zemin="kaldirim"):
    levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["cim"] if zemin == "cim" else r["kaldirim"])
    levha(g, 0.0, PW, 0.0, on_d, EV_ZEMIN, EV_ZEMIN + 0.12, r["kaldirim"])


# ---------- KONUT

def konut(rnd, tip):
    g = Govde()
    z0 = EV_ZEMIN
    if tip == "sira_evler":
        # Bitişik sıra evler: her biri farklı renk ve alınlık (kademeli / üçgen / düz korniş)
        n = rnd.choice([3, 4])
        PW, PD = n * 3.6 + 1.2, 12.0
        r = ar(rnd, "#f1e4c8")
        kaldirim_arsa(g, PW, PD, r)
        x, fy, D = 0.6, 3.0, 8.0
        renkler = ["#b8634a", "#e8c98f", "#f0cdb0", "#cfdbe3", "#d9dfc6", "#9c4a35", "#f4f0e6"]
        rnd.shuffle(renkler)
        for i in range(n):
            W = 3.6
            ri = dict(r)
            ri["duvar"] = M("Duvar", renkler[i % len(renkler)])
            kat = rnd.randint(3, 4)
            Ht = z0 + kat * 2.9
            kutu(g, x, x + W, fy, fy + D, z0, Ht, ri["duvar"], atla=("zn",))
            on, sag, sol, arka = cepheler(x, x + W, fy, fy + D)
            ckutu(g, on, 0.9, 1.9, 0, 0.06, z0, z0 + 2.3, ri["kapi"])
            ckutu(g, on, 0.8, 2.0, 0, 0.1, z0 + 2.3, z0 + 2.45, ri["suslu"])
            for k in range(2):
                ckutu(g, on, 0.7 - 0.06 * k, 2.1 + 0.06 * k, 0, 0.35 + 0.3 * k, z0 - 0.3 * k, z0 + 0.0 + (0.3 - 0.3 * k) * 0, ri["taban"]) if False else None
            pencere(g, on, 2.85, z0 + 0.9, 0.7, 1.4, ri)
            for f in range(1, kat):
                for u in (1.0, 2.6):
                    pencere(g, on, u, z0 + f * 2.9 + 0.8, 0.75, 1.45, ri)
            stil = rnd.choice(["kademe", "ucgen", "duz"])
            if stil == "kademe":
                z = Ht
                for wi, hi in ((W, 0.9), (W * 0.7, 0.8), (W * 0.4, 0.6)):
                    kutu(g, x + (W - wi) / 2, x + (W + wi) / 2, fy, fy + 0.3, z, z + hi, ri["duvar"])
                    kutu(g, x + (W - wi) / 2 - 0.05, x + (W + wi) / 2 + 0.05, fy - 0.05, fy + 0.35, z + hi, z + hi + 0.08, ri["suslu"])
                    z += hi
                kutu(g, x, x + W, fy + 0.3, fy + D, Ht, Ht + 0.3, ri["cati"])
            elif stil == "ucgen":
                besik_cati(g, "y", fy, fy + D, x, x + W, Ht, ri, sacak=0.15, yan_sacak=0.2)
                pencere(g, on, W / 2, Ht + 0.3, 0.6, 0.9, ri, sade=True)
            else:
                kutu(g, x - 0.05, x + W + 0.05, fy - 0.2, fy + D, Ht, Ht + 0.35, ri["suslu"])
            x += W
        oluk(g, 0.55, fy - 0.07, z0, z0 + 8.0)
        for i in range(n):
            if rnd.random() < 0.6:
                p = (0.6 + i * 3.6 + 3.0, fy - 0.6)
                silindir(g, p[0], p[1], z0 + 0.12, 0.35, 0.2, 7, r["saksi"])
                top(g, (p[0], p[1], z0 + 0.7), 0.3, 0.3, 0.3, r["cali"])
    elif tip == "tugla_apt":
        # Asansörsüz tuğla apartman: yangın merdiveni, çatıda su deposu
        r = ar(rnd, rnd.choice(["#a8503a", "#9c4a35", "#b8634a"]))
        PW, PD = 12.0, 13.0
        kaldirim_arsa(g, PW, PD, r)
        x0, x1, fy, by = 1.2, 10.8, 3.0, 11.0
        kat = rnd.randint(5, 6)
        KY = 3.0
        Ht = z0 + 0.6 + kat * KY
        kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        W = x1 - x0
        ckutu(g, on, -0.1, W + 0.1, 0, 0.25, Ht - 0.4, Ht, r["suslu"])
        ckutu(g, on, -0.05, W + 0.05, 0, 0.1, z0, z0 + 0.6, r["taban"])
        for f in range(kat):
            for i in range(4):
                u = W * (i + 0.5) / 4
                if f == 0 and i == 1:
                    ckutu(g, on, u - 0.6, u + 0.6, 0, 0.08, z0 + 0.6, z0 + 3.0, r["kapi"])
                    ckutu(g, on, u - 0.75, u + 0.75, 0, 0.6, z0 + 3.1, z0 + 3.25, r["suslu"])
                    continue
                pencere(g, on, u, z0 + 0.6 + f * KY + 0.7, 0.9, 1.6, r)
        # Yangın merdiveni (sağ yarıda, her katta sahanlık ve korkuluk)
        for f in range(1, kat):
            z = z0 + 0.6 + f * KY + 0.4
            ckutu(g, on, W * 0.55, W * 0.95, 0, 0.9, z - 0.06, z, r["demir"])
            for k in range(9):
                uu = W * 0.55 + k * (W * 0.4) / 8
                ckutu(g, on, uu - 0.015, uu + 0.015, 0.86, 0.9, z, z + 0.9, r["demir"])
            ckutu(g, on, W * 0.55, W * 0.95, 0.85, 0.9, z + 0.88, z + 0.94, r["demir"])
            if f < kat - 1:
                a = on.P(W * 0.6, 0.6, z)
                b = on.P(W * 0.9, 0.6, z + KY)
                cubuk(g, a, b, 0.12, r["demir"], kabuk=False)
        for fc, L in ((sol, by - fy), (sag, by - fy)):
            pencereler(g, fc, L, z0 + 0.6, kat, KY, r, w=0.8, h=1.4, aralik=2.6, sade=True)
        pencereler(g, arka, W, z0 + 0.6, kat, KY, r, w=0.8, h=1.4, aralik=2.4, sade=True)
        kutu(g, x0, x1, fy, by, Ht, Ht + 0.05, r["beton"])
        # Su deposu
        cx, cy = x0 + 2.4, by - 2.5
        for dx, dy in ((-0.8, -0.8), (0.8, -0.8), (-0.8, 0.8), (0.8, 0.8)):
            cubuk(g, (cx + dx, cy + dy, Ht), (cx + dx * 0.8, cy + dy * 0.8, Ht + 1.6), 0.1, r["ahsap_koyu"], kabuk=False)
        silindir(g, cx, cy, Ht + 1.6, 2.0, 1.1, 10, r["ahsap"])
        koni(g, cx, cy, Ht + 3.6, 0.8, 1.2, 10, r["cinko"])
        cati_anteni(g, x1 - 1.5, fy + 2.0, Ht)
    elif tip == "avlulu_blok":
        # U biçimli blok, ortada bahçeli avlu (önü sokağa açık)
        r = ar(rnd, rnd.choice(["#f1e4c8", "#ecd3a4", "#e8d6c8"]))
        PW, PD = 16.0, 16.0
        kaldirim_arsa(g, PW, PD, r)
        kat, KY = 4, 2.9
        Ht = z0 + 0.5 + kat * KY
        kanat = 4.2
        for (x0, x1, y0, y1) in ((1.0, 1.0 + kanat, 3.0, 15.0), (15.0 - kanat, 15.0, 3.0, 15.0), (1.0 + kanat, 15.0 - kanat, 15.0 - kanat, 15.0)):
            kutu(g, x0, x1, y0, y1, z0, Ht, r["duvar"], atla=("zn",))
            kalca_cati(g, x0, x1, y0, y1, Ht, 0.75, r["cati"], sacak=0.25)
            on, sag, sol, arka = cepheler(x0, x1, y0, y1)
            for fc, L in ((on, x1 - x0), (sag, y1 - y0), (sol, y1 - y0), (arka, x1 - x0)):
                pencereler(g, fc, L, z0 + 0.5, kat, KY, r, w=0.8, h=1.4, aralik=1.9, sade=fc is arka)
        # Avlu: çim, yol, ağaç, bank, çocuk salıncağı
        levha(g, 1.0 + kanat, 15.0 - kanat, 3.0, 15.0 - kanat, EV_ZEMIN, EV_ZEMIN + 0.04, r["cim"])
        kutu(g, 7.4, 8.6, 2.2, 10.8, EV_ZEMIN, EV_ZEMIN + 0.06, r["cakil"], atla=("zn",))
        agac(g, 6.4, 8.5, r, rnd)
        agac(g, 9.6, 6.0, r, rnd)
        bank(g, 9.4, 9.2, math.pi, r, z0=EV_ZEMIN)
        calilik(g, 5.4, 10.6, 3.6, r, rnd)
    elif tip in ("villa", "konak", "sale"):
        PW, PD = 14.0, 15.0
        if tip == "villa":
            r = ar(rnd, rnd.choice(["#f0cdb0", "#f4f0e6", "#ecd3a4"]), cati="#5b5d63")
        elif tip == "konak":
            r = ar(rnd, "#f4ecd8", cati=rnd.choice(["#c0573a", "#b04a33"]))
        else:
            r = ar(rnd, "#a7773f", cati="#6b4a33")
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["cim"])
        x0, x1, fy, by = 3.0, 11.0, 4.5, 11.5
        W, D = x1 - x0, by - fy
        zb = z0 + 0.5
        if tip == "villa":
            # Şehir villası: kırma çatı, köşede kule (sivri çatılı), bahçe çiti
            Ht = zb + 2 * 3.0
            kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
            kutu(g, x0 - 0.05, x1 + 0.05, fy - 0.05, by + 0.05, z0, zb, r["taban"], atla=("zn",))
            on, sag, sol, arka = cepheler(x0, x1, fy, by)
            pencereler(g, on, W, zb, 2, 3.0, r, w=0.9, h=1.6, aralik=2.2, atla=((3.0, 5.0),))
            ckutu(g, on, 3.4, 4.6, 0, 0.06, zb, zb + 2.4, r["kapi"])
            ckutu(g, on, 3.0, 5.0, 0, 1.2, zb + 2.6, zb + 2.75, r["suslu"])
            for fc in (sag, sol, arka):
                pencereler(g, fc, D if fc is not arka else W, zb, 2, 3.0, r, w=0.8, h=1.5, aralik=2.4, sade=True)
            zr = kalca_cati(g, x0, x1, fy, by, Ht, 0.8, r["cati"])
            tx, ty = x1 - 0.2, fy + 0.2
            prizma_poligon(g, poligon(8, 1.5, 1.5, tx, ty), z0, Ht + 1.4, r["duvar"])
            for f in range(3):
                for a in (-0.6, 0.0, 0.6):
                    p = (tx + 1.55 * math.cos(-math.pi / 2 + a), ty + 1.55 * math.sin(-math.pi / 2 + a))
                    kutu(g, p[0] - 0.25, p[0] + 0.25, p[1] - 0.1, p[1] + 0.05, zb + f * 2.6 + 0.9, zb + f * 2.6 + 2.0, r["cam"])
            koni(g, tx, ty, Ht + 1.4, 3.0, 1.85, 8, r["cati"])
            ev_baca(g, x0 + 1.5, fy + D / 2, zr - 1.0, zr + 0.6, r)
        elif tip == "konak":
            # Yarı ahşap konak: koyu kirişler, dik kiremit çatı, çıkma pencere
            Ht = zb + 2 * 2.9
            kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
            kutu(g, x0 - 0.05, x1 + 0.05, fy - 0.05, by + 0.05, z0, zb + 0.4, M("Tas", "#b9b2a6"), atla=("zn",))
            on, sag, sol, arka = cepheler(x0, x1, fy, by)
            for fc, L in ((on, W), (sag, D), (sol, D), (arka, W)):
                for k in range(int(L / 1.3) + 1):
                    u = k * L / int(L / 1.3)
                    ckutu(g, fc, u - 0.08, u + 0.08, 0, 0.06, zb + 0.4, Ht, r["ahsap_koyu"])
                for zz in (zb + 0.4, zb + 2.9, Ht - 0.12):
                    ckutu(g, fc, -0.02, L + 0.02, 0, 0.065, zz, zz + 0.14, r["ahsap_koyu"])
                for k in range(int(L / 2.6)):
                    a, b = fc.P(k * 2.6 + 0.1, 0.06, zb + 2.95), fc.P(k * 2.6 + 1.25, 0.06, Ht - 0.1)
                    cubuk(g, a, b, 0.12, r["ahsap_koyu"], kabuk=False)
            for f in range(2):
                for u in (W * 0.2, W * 0.8):
                    ev_pencere(g, on, u, zb + 0.4 + f * 2.5 + 0.7, 0.85, 1.25, r, rnd, panjur=False, saksi=True)
            ckutu(g, on, W / 2 - 0.55, W / 2 + 0.55, 0, 0.06, zb, zb + 2.3, r["kapi"])
            zr = besik_cati(g, "x", x0, x1, fy, by, Ht, r, sacak=0.5)
            besik_cati(g, "y", fy - 1.2, fy + D / 2, x0 + W * 0.55, x0 + W * 0.9, Ht, r, sacak=0.3, yan_sacak=0.2)
            kutu(g, x0 + W * 0.55, x0 + W * 0.9, fy - 1.2, fy, zb + 2.9, Ht, r["duvar"], atla=("yp",))
            ev_baca(g, x0 + W * 0.2, fy + D / 2 + 0.5, cati_z(Ht, fy, by, fy + D / 2 + 0.5) - 0.3, zr + 0.5, r)
        else:
            # Alp şalesi: taş zemin, ahşap üst kat, geniş saçaklı yayvan çatı, ahşap balkon
            Ht = zb + 2 * 2.8
            tas = M("Tas", "#b9b2a6")
            kutu(g, x0, x1, fy, by, z0, zb + 2.8, tas, atla=("zn",))
            kutu(g, x0, x1, fy, by, zb + 2.8, Ht, r["duvar"], atla=("zn",))
            on, sag, sol, arka = cepheler(x0, x1, fy, by)
            for k in range(int((Ht - zb - 2.8) / 0.35)):
                zz = zb + 2.8 + k * 0.35
                ckutu(g, on, -0.02, W + 0.02, 0, 0.04, zz, zz + 0.05, r["ahsap_koyu"])
            pencereler(g, on, W, zb, 2, 2.8, r, w=0.9, h=1.3, aralik=2.0, atla=((3.0, 5.0),))
            ckutu(g, on, 3.4, 4.6, 0, 0.06, z0, zb + 2.2, r["ahsap_koyu"])
            z = zb + 2.8
            ckutu(g, on, -0.4, W + 0.4, 0, 1.3, z - 0.15, z, r["ahsap"])
            for k in range(int((W + 0.8) / 0.25)):
                uu = -0.4 + k * 0.25
                ckutu(g, on, uu, uu + 0.12, 1.2, 1.28, z, z + 0.95, r["ahsap"])
            ckutu(g, on, -0.4, W + 0.4, 1.18, 1.3, z + 0.95, z + 1.05, r["ahsap_koyu"])
            for k in range(5):
                p = on.P(0.6 + k * (W - 1.2) / 4, 1.15, z + 1.0)
                kutu(g, p[0] - 0.25, p[0] + 0.25, p[1] - 0.1, p[1] + 0.1, z + 0.95, z + 1.1, r["saksi"])
                top(g, (p[0], p[1], z + 1.15), 0.22, 0.1, 0.1, rnd.choice(r["cicek"]))
            ri = dict(r)
            zr = besik_cati(g, "y", fy, by, x0, x1, Ht, ri, sacak=1.2, yan_sacak=1.0)
            pencere(g, on, W / 2, Ht + 0.3, 0.8, 0.9, r, sade=True)
            for k in range(6):                                                 # odun yığını
                kutu(g, x1 + 0.3, x1 + 1.0, fy + 1 + k * 0.4, fy + 1.35 + k * 0.4, z0, z0 + 1.2, r["ahsap_koyu"])
        # Bahçe: çit, yol, ağaçlar
        kutu(g, 6.4, 7.6, 0.0, fy, z0, z0 + 0.04, r["cakil"], atla=("zn",))
        for (a, b) in ((0.2, 6.2), (7.8, PW - 0.2)):
            k = int((b - a) / 0.45)
            for i in range(k + 1):
                xx = a + i * (b - a) / k
                kutu(g, xx - 0.05, xx + 0.05, 0.15, 0.25, z0, z0 + 0.8, r["beyaz"] if tip != "sale" else r["ahsap"])
            cubuk(g, (a, 0.2, z0 + 0.55), (b, 0.2, z0 + 0.55), 0.06, r["beyaz"] if tip != "sale" else r["ahsap"])
        agac(g, 1.4, PD - 1.6, r, rnd, boy=1.6, rr=1.0)
        agac(g, PW - 1.4, 2.4, r, rnd)
        calilik(g, x0, x1, fy - 0.6, r, rnd)
    else:   # balkonlu_blok: sosyal konut, renkli balkon panelleri
        r = ar(rnd, "#ece6da")
        PW, PD = 18.0, 13.0
        kaldirim_arsa(g, PW, PD, r, zemin="cim")
        x0, x1, fy, by = 1.0, 17.0, 3.0, 11.0
        W = x1 - x0
        kat, KY = rnd.randint(5, 7), 2.8
        Ht = z0 + 0.4 + kat * KY
        kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        paneller = [M("BalkonPanel", c) for c in ("#e0825a", "#f2c14a", "#6fae6a", "#5f8fd0")]
        n = 5
        for f in range(kat):
            z = z0 + 0.4 + f * KY
            for i in range(n):
                u = W * (i + 0.5) / n
                if f == 0:
                    if i == 2:
                        ckutu(g, on, u - 0.8, u + 0.8, 0, 0.06, z0, z0 + 2.4, r["cerceve"])
                        cdortgen(g, on, u - 0.7, u + 0.7, 0.07, z0 + 0.05, z0 + 2.3, r["cam"])
                        ckutu(g, on, u - 1.4, u + 1.4, 0, 1.4, z0 + 2.5, z0 + 2.7, r["beton"])
                    else:
                        pencere(g, on, u, z + 0.8, 1.2, 1.3, r, sade=True)
                    continue
                ckutu(g, on, u - 0.5, u + 0.5, 0, 0.05, z + 0.1, z + 2.3, r["cerceve"])
                cdortgen(g, on, u - 0.42, u + 0.42, 0.06, z + 0.15, z + 2.2, r["cam"])
                ckutu(g, on, u - 1.4, u + 1.4, 0, 1.2, z - 0.05, z + 0.1, r["beton"])
                ckutu(g, on, u - 1.4, u + 1.4, 1.1, 1.2, z + 0.1, z + 1.1, paneller[(i + f) % len(paneller)])
                if rnd.random() < 0.3:
                    p = on.P(u + 0.9, 0.6, z + 0.1)
                    silindir(g, p[0], p[1], z + 0.1, 0.3, 0.15, 6, r["saksi"])
                    top(g, (p[0], p[1], z + 0.55), 0.22, 0.22, 0.25, r["bitki"])
                if rnd.random() < 0.15:
                    canak_anten(g, on, u - 1.0, 1.15, z + 0.9)
        for fc, L in ((sol, by - fy), (sag, by - fy)):
            pencereler(g, fc, L, z0 + 0.4, kat, KY, r, w=0.8, h=1.2, aralik=2.6, sade=True)
        pencereler(g, arka, W, z0 + 0.4, kat, KY, r, w=0.9, h=1.2, aralik=2.0, sade=True)
        duz_cati_ust(g, x0, x1, fy, by, Ht, {**r, "koyu": r["koyu"]}, rnd, r["suslu"], klima=2)
        for i in range(4):
            gunes_paneli(g, x0 + 2.0 + i * 3.6, fy + 2.0, Ht + 0.57, 2.6, 1.2)
        agac(g, 0.8, PD - 1.0, r, rnd)
        agac(g, PW - 0.8, PD - 1.0, r, rnd)
        for i in range(5):                                                     # bisiklet parkı
            bx = 2.0 + i * 0.6
            cubuk(g, (bx, fy - 1.2, z0 + 0.12), (bx, fy - 1.2, z0 + 0.8), 0.05, r["metal"])
            cubuk(g, (bx, fy - 1.2, z0 + 0.8), (bx, fy - 0.7, z0 + 0.8), 0.05, r["metal"])
            cubuk(g, (bx, fy - 0.7, z0 + 0.8), (bx, fy - 0.7, z0 + 0.12), 0.05, r["metal"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------- BÜRO (ofis)

def buro(rnd, tip):
    g = Govde()
    z0 = EV_ZEMIN
    if tip == "tugla_cam":
        r = ar(rnd, rnd.choice(["#a8503a", "#8e4331", "#b8634a"]))
        PW, PD = 13.0, 13.0
        kaldirim_arsa(g, PW, PD, r)
        x0, x1, fy, by = 1.5, 11.5, 3.0, 11.5
        W, D = x1 - x0, by - fy
        kat, KY = 5, 3.4
        Ht = z0 + kat * KY
        kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        for fc, L in ((on, W), (sag, D), (sol, D), (arka, W)):
            n = int(L / 2.4)
            for f in range(kat):
                for i in range(n):
                    u0 = L * i / n + 0.3
                    u1 = L * (i + 1) / n - 0.3
                    zz = z0 + f * KY
                    cdortgen(g, fc, u0, u1, 0.01, zz + 0.4, zz + KY - 0.3, r["cam"])
                    ckutu(g, fc, (u0 + u1) / 2 - 0.04, (u0 + u1) / 2 + 0.04, 0, 0.06, zz + 0.4, zz + KY - 0.3, r["cerceve"])
                ckutu(g, fc, -0.03, L + 0.03, 0, 0.12, z0 + f * KY + KY - 0.3, z0 + f * KY + KY - 0.1, r["suslu"])
        ckutu(g, on, W / 2 - 1.2, W / 2 + 1.2, 0, 1.6, z0 + 3.0, z0 + 3.2, r["cerceve"])
        kutu(g, x0 - 0.1, x1 + 0.1, fy - 0.1, by + 0.1, Ht, Ht + 0.5, r["duvar"], yuzler={"zp": r["cim"]})
        for i in range(4):
            top(g, (x0 + 1.5 + i * 2.2, fy + 2.0, Ht + 0.8), 0.6, 0.6, 0.45, r["cali"])
        silindir(g, x1 - 1.6, by - 1.6, Ht + 0.5, 1.4, 0.7, 8, r["cerceve"])
    elif tip == "is_kulesi":
        r = ar(rnd, "#c9d6df")
        PW, PD = 14.0, 14.0
        kaldirim_arsa(g, PW, PD, r)
        cx, cy = 7.0, 8.0
        kat, KY = rnd.randint(13, 16), 3.4
        z = z0
        kutu(g, cx - 5.2, cx + 5.2, cy - 4.6, cy + 4.6, z0, z0 + 5.0, r["cerceve"], atla=("zn",))      # lobi kaidesi
        lfc = Cephe((cx - 5.2, cy - 4.6, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        cdortgen(g, lfc, 0.6, 9.8, 0.01, z0 + 0.3, z0 + 4.4, r["cam"])
        tabela(g, lfc, 5.2, z0 + 4.65, 4.0, 0.5, 0.1, r, "CITY TOWER", renk=r["koyu"], yazi_renk=r["beyaz"], oran=0.6)
        z = z0 + 5.0
        yari = 4.4
        for kis in range(3):
            nk = kat // 3
            h = nk * KY
            kutu(g, cx - yari, cx + yari, cy - yari * 0.9, cy + yari * 0.9, z, z + h, r["duvar"], atla=("zn",))
            fc = Cephe((cx - yari, cy - yari * 0.9, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
            for side, L in ((fc, 2 * yari), (Cephe((cx + yari, cy - yari * 0.9, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), 1.8 * yari)):
                for k in range(int(L / 1.2) + 1):
                    u = k * L / int(L / 1.2)
                    ckutu(g, side, u - 0.05, u + 0.05, 0, 0.12, z, z + h, r["cerceve"])
                for f in range(nk):
                    ckutu(g, side, -0.02, L + 0.02, 0, 0.08, z + f * KY, z + f * KY + 0.25, M("CamKoyu", "#7f9fb8"))
            z += h
            kutu(g, cx - yari - 0.1, cx + yari + 0.1, cy - yari * 0.9 - 0.1, cy + yari * 0.9 + 0.1, z, z + 0.3, r["beton"])
            z += 0.3
            yari -= 0.9
        kutu(g, cx - 1.0, cx + 1.0, cy - 1.0, cy + 1.0, z, z + 2.0, r["cerceve"])
        cubuk(g, (cx, cy, z + 2.0), (cx, cy, z + 8.0), 0.15, r["metal"])
        top(g, (cx, cy, z + 8.0), 0.15, 0.15, 0.15, r["kirmizi"])
    elif tip == "saat_kuleli":
        r = ar(rnd, "#a8503a", cati="#5b5d63")
        PW, PD = 15.0, 14.0
        kaldirim_arsa(g, PW, PD, r)
        x0, x1, fy, by = 1.5, 13.5, 3.2, 12.0
        W, D = x1 - x0, by - fy
        kat, KY = 3, 3.6
        Ht = z0 + 0.6 + kat * KY
        kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        kutu(g, x0 - 0.06, x1 + 0.06, fy - 0.06, by + 0.06, z0, z0 + 0.6, M("Tas", "#b9b2a6"), atla=("zn",))
        for fc, L in ((on, W), (sag, D), (sol, D), (arka, W)):
            pencereler(g, fc, L, z0 + 0.6, kat, KY, r, w=1.0, h=2.0, aralik=2.0, sade=fc is arka)
            ckutu(g, fc, -0.08, L + 0.08, 0, 0.3, Ht - 0.4, Ht, r["suslu"])
        zr = kalca_cati(g, x0, x1, fy, by, Ht, 0.6, r["cati"], sacak=0.3)
        tx = x0 + W / 2
        kutu(g, tx - 1.6, tx + 1.6, fy - 0.4, fy + 2.8, z0, Ht + 6.0, r["duvar"], atla=("zn",))
        tfc = Cephe((tx - 1.6, fy - 0.4, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        ckutu(g, tfc, 0.8, 2.4, 0, 0.06, z0 + 0.6, z0 + 3.2, r["kapi"])
        tabela(g, tfc, 1.6, z0 + 3.6, 2.8, 0.45, 0.08, r, "TŐZSDE", renk=r["koyu"], oran=0.62)
        saat(g, tfc, 1.6, Ht + 3.6, 1.0, r)
        for fc2 in (Cephe((tx + 1.6, fy - 0.4, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), Cephe((tx - 1.6, fy + 2.8, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))):
            saat(g, fc2, 1.6, Ht + 3.6, 1.0, r)
        kutu(g, tx - 1.8, tx + 1.8, fy - 0.6, fy + 3.0, Ht + 6.0, Ht + 6.3, r["suslu"])
        pts = [(tx - 1.7, fy - 0.5, Ht + 6.3), (tx + 1.7, fy - 0.5, Ht + 6.3), (tx + 1.7, fy + 2.9, Ht + 6.3), (tx - 1.7, fy + 2.9, Ht + 6.3), (tx, fy + 1.2, Ht + 9.5)]
        dis_bukey(g, pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], r["cati"])
        cubuk(g, (tx, fy + 1.2, Ht + 9.5), (tx, fy + 1.2, Ht + 10.6), 0.08, r["sari"])
    else:   # yetmisler: bant pencereli beton ofis, kolonlar üstünde
        r = ar(rnd, "#cfc8bc")
        PW, PD = 15.0, 13.0
        kaldirim_arsa(g, PW, PD, r)
        x0, x1, fy, by = 1.2, 13.8, 3.0, 11.6
        W, D = x1 - x0, by - fy
        kat, KY = 6, 3.2
        z1 = z0 + 4.0
        Ht = z1 + kat * KY
        kutu(g, x0 + 1.5, x1 - 1.5, fy + 1.5, by - 1.5, z0, z1, r["cerceve"], atla=("zn",))
        lfc = Cephe((x0 + 1.5, fy + 1.5, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        cdortgen(g, lfc, 0.3, W - 3.3, 0.01, z0 + 0.2, z1 - 0.4, r["cam"])
        for i in range(5):
            xx = x0 + 0.6 + i * (W - 1.2) / 4
            kutu(g, xx - 0.3, xx + 0.3, fy + 0.3, fy + 0.9, z0, z1, r["beton"])
        kutu(g, x0, x1, fy, by, z1, Ht, r["duvar"])
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        for fc, L in ((on, W), (sag, D), (sol, D), (arka, W)):
            for f in range(kat):
                zz = z1 + f * KY
                cdortgen(g, fc, 0.2, L - 0.2, 0.01, zz + 1.0, zz + 2.4, M("CamKoyu", "#7f9fb8"))
                ckutu(g, fc, -0.05, L + 0.05, 0, 0.35, zz + 0.85, zz + 1.0, r["beton"])
                for k in range(int(L / 1.5)):
                    u = (k + 0.5) * L / int(L / 1.5)
                    ckutu(g, fc, u - 0.04, u + 0.04, 0, 0.05, zz + 1.0, zz + 2.4, r["cerceve"])
        duz_cati_ust(g, x0, x1, fy, by, Ht, r, rnd, r["beton"], klima=3)
        tabela(g, on, W / 2, z1 - 0.2, 6.0, 0.55, 0.12, r, "IRODAHÁZ", renk=r["koyu"], oran=0.6)
    agac(g, 0.8, 1.2, r, rnd, boy=1.3, rr=0.7)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------- SANAYİ

def sanayi(rnd, tip):
    g = Govde()
    z0 = EV_ZEMIN
    if tip == "fabrika":
        r = ar(rnd, rnd.choice(["#a8503a", "#9c4a35"]), cati="#5b5d63")
        PW, PD = 18.0, 16.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["asfalt"])
        x0, x1, fy, by = 1.0, 15.0, 3.5, 14.5
        W, D = x1 - x0, by - fy
        H = z0 + 6.0
        kutu(g, x0, x1, fy, by, z0, H, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        for fc, L in ((on, W), (sag, D), (sol, D)):
            n = int(L / 2.4)
            for i in range(n):
                u = L * (i + 0.5) / n
                cdortgen(g, fc, u - 0.8, u + 0.8, 0.01, z0 + 1.6, z0 + 4.8, r["cam"])
                ckutu(g, fc, u - 0.04, u + 0.04, 0, 0.05, z0 + 1.6, z0 + 4.8, r["cerceve"])
                ckutu(g, fc, u - 0.9, u + 0.9, 0, 0.12, z0 + 4.8, z0 + 5.1, r["suslu"])
        ckutu(g, on, 2.0, 5.0, 0, 0.06, z0, z0 + 3.8, M("Kepenk", "#6f7a84"))
        tabela(g, on, W * 0.65, H - 0.5, 5.0, 0.6, 0.08, r, "GYÁR", renk=r["suslu"], yazi_renk=r["koyu"], oran=0.6)
        # Testere dişi çatı (kuzeye bakan camlı dişler)
        n = 4
        t = D / n
        for i in range(n):
            ya = fy + i * t
            pts2 = [(ya, H), (ya + t, H), (ya + t, H + 2.0)]
            pts = [(x0 - 0.1, y, z) for y, z in pts2] + [(x1 + 0.1, y, z) for y, z in pts2]
            dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["cati"])
            for xx in range(int(W / 1.4)):
                u = x0 + 0.4 + xx * 1.4
                kutu(g, u, u + 1.0, ya + t - 0.02, ya + t + 0.02, H + 0.3, H + 1.8, r["cam"])
        for k, cxx in enumerate((x1 - 2.0, x1 - 4.5)):                         # bacalar
            hh = 14.0 - k * 3.0
            silindir(g, cxx, by + 1.0, z0, hh, 0.75 - k * 0.1, 10, r["tugla"])
            for zz in (hh * 0.6, hh - 0.6):
                silindir(g, cxx, by + 1.0, z0 + zz, 0.35, 0.85 - k * 0.1, 10, r["suslu"])
        for i in range(3):                                                     # paletler, varil
            kutu(g, 7 + i * 1.4, 8.1 + i * 1.4, 1.2, 2.3, z0, z0 + 0.15, r["ahsap"])
            kutu(g, 7.1 + i * 1.4, 8.0 + i * 1.4, 1.3, 2.2, z0 + 0.15, z0 + 0.9, rnd.choice(r["urun"]))
        silindir(g, 12.5, 1.6, z0, 0.9, 0.3, 8, r["mavi"])
    elif tip == "depo":
        r = ar(rnd, rnd.choice(["#c9ccd0", "#b9c6c0", "#c8b8a0"]), cati="#8f949a")
        PW, PD = 20.0, 15.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["asfalt"])
        x0, x1, fy, by = 1.0, 19.0, 4.5, 14.0
        W, D = x1 - x0, by - fy
        H = z0 + 6.5
        kutu(g, x0, x1, fy, by, z0, H, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        for fc, L in ((on, W), (sag, D), (sol, D)):
            for k in range(1, int(L / 0.7)):
                u = k * 0.7
                ckutu(g, fc, u - 0.04, u + 0.04, 0, 0.05, z0 + 1.0, H, r["metal"])
        for i in range(4):                                                     # yükleme rampaları
            u = 2.0 + i * 4.2
            ckutu(g, on, u - 1.4, u + 1.4, 0, 0.06, z0 + 1.0, z0 + 4.6, M("Kepenk", "#6f7a84"))
            for k in range(1, 8):
                ckutu(g, on, u - 1.35, u + 1.35, 0.06, 0.08, z0 + 1.0 + k * 0.45, z0 + 1.04 + k * 0.45, r["koyu"])
            ckutu(g, on, u - 1.6, u + 1.6, 0, 1.2, z0, z0 + 1.0, r["beton"])
            ckutu(g, on, u - 1.6, u + 1.6, 0, 1.4, z0 + 4.8, z0 + 5.0, r["sari"])
        besik_cati(g, "x", x0, x1, fy, by, H, {**r, "duvar": r["duvar"]}, sacak=0.3, yan_sacak=0.2)
        tabela(g, on, W / 2, H - 0.5, 6.0, 0.7, 0.1, r, "RAKTÁR", renk=r["ana"], oran=0.6)
        for i in range(2):                                                     # tır dorsesi
            x = 3.0 + i * 8.4
            kutu(g, x, x + 2.4, 0.4, 3.9, z0 + 0.6, z0 + 3.6, rnd.choice([r["beyaz"], r["kirmizi"], r["mavi"]]))
            for dy in (0.9, 1.6):
                for dx in (0.3, 2.1):
                    silindir(g, x + dx, dy, z0, 0.6, 0.3, 8, r["koyu"]) if False else kutu(g, x + dx - 0.15, x + dx + 0.15, dy - 0.3, dy + 0.3, z0, z0 + 0.6, r["koyu"])
    elif tip == "sera":
        r = ar(rnd, "#f4f0e6")
        PW, PD = 16.0, 15.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["toprak"])
        cam = M("SeraCam", "#cfe6e0")
        for i in range(3):
            x0 = 1.0 + i * 4.8
            x1 = x0 + 4.2
            fy, by = 3.0, 13.0
            kutu(g, x0, x1, fy, by, z0, z0 + 2.4, cam, atla=("zn",))
            pts2 = [(x0, z0 + 2.4), (x1, z0 + 2.4), ((x0 + x1) / 2, z0 + 3.8)]
            pts = [(x, fy, z) for x, z in pts2] + [(x, by, z) for x, z in pts2]
            dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], cam)
            for k in range(int((by - fy) / 1.25) + 1):
                y = fy + k * 1.25
                cubuk(g, (x0, y, z0), (x0, y, z0 + 2.4), 0.06, r["beyaz"], kabuk=False)
                cubuk(g, (x1, y, z0), (x1, y, z0 + 2.4), 0.06, r["beyaz"], kabuk=False)
                cubuk(g, (x0, y, z0 + 2.4), ((x0 + x1) / 2, y, z0 + 3.8), 0.06, r["beyaz"], kabuk=False)
                cubuk(g, (x1, y, z0 + 2.4), ((x0 + x1) / 2, y, z0 + 3.8), 0.06, r["beyaz"], kabuk=False)
            for k in range(3):
                kutu(g, x0 + 0.4, x1 - 0.4, fy + 0.5 + k * 3.0, fy + 2.5 + k * 3.0, z0, z0 + 0.5, r["toprak"])
                for j in range(4):
                    top(g, (x0 + 0.8 + j * 0.9, fy + 1.5 + k * 3.0, z0 + 0.75), 0.3, 0.3, 0.3, r["cali"] if j % 2 else rnd.choice(r["cicek"]))
        for j in range(5):
            kutu(g, 1.0 + j * 2.8, 3.2 + j * 2.8, 0.6, 2.2, z0, z0 + 0.3, r["ahsap"])
            for k in range(3):
                top(g, (1.5 + j * 2.8 + k * 0.6, 1.4, z0 + 0.5), 0.22, 0.22, 0.2, rnd.choice(r["cicek"]))
    elif tip == "garaj":
        r = ar(rnd, "#e8e4dc", ana="#d9473a")
        PW, PD = 16.0, 14.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["asfalt"])
        x0, x1, fy, by = 1.0, 15.0, 5.0, 13.0
        W = x1 - x0
        H = z0 + 5.0
        kutu(g, x0, x1, fy, by, z0, H, r["duvar"], atla=("zn",))
        on, sag, sol, arka = cepheler(x0, x1, fy, by)
        ckutu(g, on, -0.05, W + 0.05, 0, 0.15, H - 1.0, H, r["ana"])
        tabela(g, on, W / 2, H - 0.5, 7.0, 0.8, 0.18, r, "AUTÓSZERVIZ", renk=r["ana"], oran=0.55)
        for i in range(3):
            u = 2.2 + i * 4.2
            ckutu(g, on, u - 1.6, u + 1.6, 0, 0.06, z0, z0 + 3.4, M("Kepenk", "#8f949a"))
            for k in range(1, 7):
                ckutu(g, on, u - 1.55, u + 1.55, 0.06, 0.08, z0 + k * 0.48, z0 + 0.04 + k * 0.48, r["koyu"])
        kutu(g, x0, x1, fy, by, H, H + 0.3, r["beton"])
        renkler = [M("Araba", c) for c in ("#d9473a", "#3f73e6", "#f6f3ec", "#2b2d31", "#f2c14a")]
        for i in range(3):
            park_araba(g, 2.5 + i * 4.5, 2.4, 0.0, rnd.choice(renkler), r, z0)
        for k in range(4):                                                     # lastik yığını
            silindir(g, 14.6, 1.2, z0 + k * 0.25, 0.25, 0.4, 8, r["koyu"])
    elif tip == "yel_degirmeni":
        r = ar(rnd, "#6b4a33", cati="#3a3d42")
        PW, PD = 14.0, 14.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["cim"])
        cx, cy = 7.0, 8.0
        n = 8
        alt = [(cx + 2.4 * math.cos(2 * math.pi * i / n), cy + 2.4 * math.sin(2 * math.pi * i / n), z0) for i in range(n)]
        ust = [(cx + 1.4 * math.cos(2 * math.pi * i / n), cy + 1.4 * math.sin(2 * math.pi * i / n), z0 + 9.0) for i in range(n)]
        dis_bukey(g, alt + ust, [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)], r["duvar"])
        kutu(g, cx - 2.9, cx + 2.9, cy - 2.9, cy + 2.9, z0 + 3.0, z0 + 3.2, r["ahsap"])            # galeri
        for i in range(16):
            a = 2 * math.pi * i / 16
            cubuk(g, (cx + 2.85 * math.cos(a), cy + 2.85 * math.sin(a), z0 + 3.2), (cx + 2.85 * math.cos(a), cy + 2.85 * math.sin(a), z0 + 4.0), 0.06, r["ahsap"], kabuk=False)
        top(g, (cx, cy, z0 + 9.3), 1.7, 1.9, 1.3, r["cati"])
        hx, hy, hz = cx, cy - 2.2, z0 + 9.2
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            d = (math.cos(a), 0.0, math.sin(a))
            c = (hx + d[0] * 3.4, hy, hz + d[2] * 3.4)
            yonlu_kutu(g, c, mul(d, 3.2), (-d[2] * 0.55, 0.0, d[0] * 0.55), (0.0, 0.05, 0.0), M("Kanat", "#e8e2d4"))
            for j in range(1, 6):
                p = (hx + d[0] * j * 1.1, hy - 0.06, hz + d[2] * j * 1.1)
                cubuk(g, add(p, (-d[2] * 0.55, 0, d[0] * 0.55)), add(p, (d[2] * 0.55, 0, -d[0] * 0.55)), 0.05, r["ahsap_koyu"], kabuk=False)
        top(g, (hx, hy - 0.1, hz), 0.35, 0.35, 0.35, r["ahsap_koyu"])
        kapi_fc = Cephe((cx - 0.6, cy - 2.3, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        ckutu(g, kapi_fc, 0.0, 1.2, 0.0, 0.1, z0, z0 + 2.2, r["ahsap_koyu"])
        renkler = ["#e0525a", "#f2c14a", "#f0a0c0", "#ffffff", "#8a5a9a"]           # lale tarlası
        for j in range(5):
            mat = M("Lale", renkler[j])
            kutu(g, 0.6 + j * 2.6, 2.6 + j * 2.6, 0.5, 3.6, z0, z0 + 0.25, mat)
    else:   # ahir: kırmızı ahır, mansart (kırık) çatı
        r = ar(rnd, "#a8352a", cati="#5b5d63")
        PW, PD = 14.0, 14.0
        levha(g, 0.0, PW, 0.0, PD, 0.0, EV_ZEMIN, r["cim"])
        x0, x1, fy, by = 2.5, 11.5, 3.5, 12.5
        W = x1 - x0
        H = z0 + 3.6
        kutu(g, x0, x1, fy, by, z0, H, r["duvar"], atla=("zn",))
        prof = [(x0 - 0.3, H), (x0 + 1.2, H + 2.4), (x0 + W / 2, H + 3.6), (x1 - 1.2, H + 2.4), (x1 + 0.3, H)]
        for (a, b) in zip(prof, prof[1:]):
            dx, dz = b[0] - a[0], b[1] - a[1]
            L = math.hypot(dx, dz)
            nn = (-dz / L, dx / L)
            yonlu_kutu(g, ((a[0] + b[0]) / 2 + nn[0] * 0.08, (fy + by) / 2, (a[1] + b[1]) / 2 + nn[1] * 0.08),
                       (dx / 2, 0, dz / 2), (0, (by - fy) / 2 + 0.3, 0), (nn[0] * 0.08, 0, nn[1] * 0.08), r["cati"])
        on = Cephe((x0, fy, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        cpoligon(g, on, [(0.0, H), (1.2, H + 2.4), (W / 2, H + 3.55), (W - 1.2, H + 2.4), (W, H)], r["duvar"]) if False else None
        pts = [(x0 + u, fy + 0.01, z) for u, z in ((0.0, H), (1.2, H + 2.4), (W / 2, H + 3.55), (W - 1.2, H + 2.4), (W, H))]
        pts += [(p[0], fy + 0.3, p[2]) for p in pts]
        dis_bukey(g, pts, [[0, 1, 2, 3, 4], [5, 6, 7, 8, 9]] + [[i, (i + 1) % 5, 5 + (i + 1) % 5, 5 + i] for i in range(5)], r["duvar"])
        ckutu(g, on, W / 2 - 1.6, W / 2 + 1.6, 0, 0.08, z0, z0 + 3.2, r["beyaz"])
        ckutu(g, on, W / 2 - 1.45, W / 2 + 1.45, 0.08, 0.1, z0, z0 + 3.05, r["duvar"])
        for k in (-1, 1):
            a, b = on.P(W / 2 - 1.4, 0.11, z0 + (0.1 if k > 0 else 3.0)), on.P(W / 2 + 1.4, 0.11, z0 + (3.0 if k > 0 else 0.1))
            cubuk(g, a, b, 0.14, r["beyaz"], kabuk=False)
        ckutu(g, on, W / 2 - 0.6, W / 2 + 0.6, 0, 0.08, H + 1.2, H + 2.2, r["beyaz"])
        for k in range(4):                                                     # saman balyaları
            silindir(g, 1.0 + k * 1.3, 1.6, z0, 1.0, 0.55, 10, M("Saman", "#e2c070"))
        for (a, b) in ((0.3, PW - 0.3),):
            k = int((b - a) / 1.6)
            for i in range(k + 1):
                xx = a + i * (b - a) / k
                kutu(g, xx - 0.06, xx + 0.06, 0.2, 0.32, z0, z0 + 1.0, r["ahsap"])
            for hz in (0.4, 0.85):
                cubuk(g, (a, 0.26, z0 + hz), (b, 0.26, z0 + hz), 0.07, r["ahsap"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------- KAMU

KAMU = {
    "polis": ("RENDŐRSÉG", "#f4f0e6", "#5b5d63", "#2f5fa8"),
    "itfaiye": ("TŰZOLTÓSÁG", "#a8503a", "#5b5d63", "#c8302a"),
    "hastane": ("KÓRHÁZ", "#f6f3ec", "#8f949a", "#c8302a"),
    "okul": ("ISKOLA", "#b8634a", "#c0573a", "#3f73e6"),
    "kutuphane": ("KÖNYVTÁR", "#ece6da", "#6f9a8a", "#3a3d42"),
    "belediye": ("VÁROSHÁZA", "#efdf9e", "#5b5d63", "#3a3d42"),
    "postane": ("POSTA", "#f2c14a", "#5b5d63", "#c8302a"),
}


def kamu(rnd, tip):
    g = Govde()
    ad, duvar, cati, ana = KAMU[tip]
    r = ar(rnd, duvar, cati=cati, ana=ana)
    z0 = EV_ZEMIN
    PW, PD = 17.0, 15.0
    kaldirim_arsa(g, PW, PD, r, zemin="cim" if tip in ("okul", "hastane", "kutuphane") else "kaldirim")
    x0, x1, fy, by = 1.5, 15.5, 3.5, 13.0
    W, D = x1 - x0, by - fy
    kat = {"polis": 3, "itfaiye": 2, "hastane": 5, "okul": 3, "kutuphane": 2, "belediye": 3, "postane": 3}[tip]
    KY = 3.4 if tip != "itfaiye" else 4.2
    zb = z0 + 0.5
    Ht = zb + kat * KY
    kutu(g, x0, x1, fy, by, z0, Ht, r["duvar"], atla=("zn",))
    kutu(g, x0 - 0.06, x1 + 0.06, fy - 0.06, by + 0.06, z0, zb, M("Tas", "#b9b2a6"), atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, fy, by)
    for fc, L in ((sag, D), (sol, D), (arka, W)):
        pencereler(g, fc, L, zb, kat, KY, r, w=0.9, h=1.6, aralik=2.2, sade=True)
    for fc, L in ((on, W), (sag, D), (sol, D), (arka, W)):
        ckutu(g, fc, -0.12, L + 0.12, 0, 0.3, Ht - 0.45, Ht, r["suslu"])
    kapi_u = W / 2
    if tip == "itfaiye":
        for i in range(3):
            u = 2.4 + i * 4.0
            ckutu(g, on, u - 1.6, u + 1.6, 0, 0.08, z0, z0 + 3.8, r["ana"])
            for k in range(1, 4):
                cdortgen(g, on, u - 1.4, u + 1.4, 0.09, z0 + k * 0.95 + 0.2, z0 + k * 0.95 + 0.6, r["cam"])
            kutu(g, x0 + u - 1.2, x0 + u + 1.2, fy - 4.2, fy - 0.3, z0, z0 + 2.6, r["ana"])                 # itfaiye aracı
            kutu(g, x0 + u - 1.1, x0 + u + 1.1, fy - 4.2, fy - 3.0, z0 + 2.6, z0 + 3.0, r["beyaz"])
            cdortgen(g, Cephe((x0 + u - 1.2, fy - 4.2, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0)), 0.2, 2.2, 0.01, z0 + 1.6, z0 + 2.4, r["cam"])
        pencereler(g, on, W, zb, kat, KY, r, w=0.9, h=1.6, aralik=2.0, bas=1)
        tx, ty = x1 - 2.0, by - 2.0                                               # gözetleme kulesi
        kutu(g, tx - 1.4, tx + 1.4, ty - 1.4, ty + 1.4, z0, Ht + 6.0, r["duvar"])
        kutu(g, tx - 1.6, tx + 1.6, ty - 1.6, ty + 1.6, Ht + 6.0, Ht + 6.2, r["suslu"])
        for k in range(4):
            a = k * math.pi / 2
            cdortgen(g, Cephe((tx - 1.4, ty - 1.4, 0), (1, 0, 0), (0, -1, 0)), 0.4, 2.4, 0.01, Ht + 4.4, Ht + 5.6, r["cam"]) if k == 0 else None
        pts = [(tx - 1.7, ty - 1.7, Ht + 6.2), (tx + 1.7, ty - 1.7, Ht + 6.2), (tx + 1.7, ty + 1.7, Ht + 6.2), (tx - 1.7, ty + 1.7, Ht + 6.2), (tx, ty, Ht + 8.6)]
        dis_bukey(g, pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], r["cati"])
    else:
        pencereler(g, on, W, zb, kat, KY, r, w=0.95, h=1.7, aralik=2.0, atla=((kapi_u - 2.0, kapi_u + 2.0),))
        ckutu(g, on, kapi_u - 1.0, kapi_u + 1.0, 0, 0.08, zb, zb + 2.8, r["kapi"])
        cdortgen(g, on, kapi_u - 0.85, kapi_u + 0.85, 0.09, zb + 0.1, zb + 2.6, r["cam"])
        for k in range(3):
            ckutu(g, on, kapi_u - 2.0 - k * 0.15, kapi_u + 2.0 + k * 0.15, 0, 0.5 + k * 0.35, z0, zb - k * 0.15, M("Tas", "#b9b2a6"))
    if tip in ("kutuphane", "belediye", "polis"):
        # Sütunlu giriş ve üçgen alınlık
        zt = zb + KY * (2 if tip != "kutuphane" else 2) - 0.3
        for i in range(6 if tip == "kutuphane" else 4):
            n = 6 if tip == "kutuphane" else 4
            u = kapi_u - 3.0 + i * 6.0 / (n - 1)
            p = on.P(u, 1.6, 0)
            silindir(g, p[0], p[1], zb, zt - zb, 0.28, 10, r["suslu"])
        ckutu(g, on, kapi_u - 3.6, kapi_u + 3.6, 0, 2.1, zt, zt + 0.5, r["suslu"])
        cprizma(g, on, [(kapi_u - 3.8, zt + 0.5), (kapi_u + 3.8, zt + 0.5), (kapi_u, zt + 2.2)], 0.0, 2.1, r["suslu"])
        tabela(g, on, kapi_u, zt + 0.25, 5.6, 0.36, 2.15, r, ad, renk=r["suslu"], yazi_renk=r["koyu"], oran=0.75)
    else:
        tabela(g, on, kapi_u, zb + 3.3 if tip != "itfaiye" else z0 + 4.4, min(W - 1.0, 7.5), 0.7, 0.12, r, ad, renk=r["ana"], oran=0.62)
    # Çatı ve özel parçalar
    if tip == "kutuphane":
        kutu(g, x0, x1, fy, by, Ht, Ht + 0.3, r["beton"])
        silindir(g, x0 + W / 2, fy + D / 2, Ht + 0.3, 1.2, 3.0, 16, r["duvar"])
        top(g, (x0 + W / 2, fy + D / 2, Ht + 1.5), 3.0, 3.0, 2.6, r["cati"])
        top(g, (x0 + W / 2, fy + D / 2, Ht + 4.2), 0.4, 0.4, 0.5, r["sari"])
    elif tip == "belediye":
        zr = kalca_cati(g, x0, x1, fy, by, Ht, 0.55, r["cati"])
        tx, ty = x0 + W / 2, fy + D / 2
        kutu(g, tx - 1.5, tx + 1.5, ty - 1.5, ty + 1.5, Ht, zr + 4.5, r["duvar"])
        tfc = Cephe((tx - 1.5, ty - 1.5, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        saat(g, tfc, 1.5, zr + 3.0, 0.95, r)
        pts = [(tx - 1.6, ty - 1.6, zr + 4.5), (tx + 1.6, ty - 1.6, zr + 4.5), (tx + 1.6, ty + 1.6, zr + 4.5), (tx - 1.6, ty + 1.6, zr + 4.5), (tx, ty, zr + 9.0)]
        dis_bukey(g, pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], r["cati"])
        for k, renk in enumerate(("#c8302a", "#f6f3ec", "#3f9a5a")):              # bayrak direkleri
            px = 4.0 + k * 0.9
            cubuk(g, (px, 2.6, z0), (px, 2.6, z0 + 6.0), 0.06, r["metal"], kabuk=False)
            kutu(g, px, px + 0.9, 2.58, 2.62, z0 + 5.0, z0 + 5.8, M("Bayrak", renk))
    elif tip == "postane":
        zr = kalca_cati(g, x0, x1, fy, by, Ht, 0.5, r["cati"])
        tx, ty = x0 + 1.6, fy + 1.6
        kutu(g, tx - 1.8, tx + 1.8, ty - 1.8, ty + 1.8, z0, Ht + 3.5, r["duvar"])
        saat(g, Cephe((tx - 1.8, ty - 1.8, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0)), 1.8, Ht + 2.0, 0.9, r)
        koni(g, tx, ty, Ht + 3.5, 3.0, 2.3, 8, r["cati"])
        p = on.P(W - 1.2, 1.2, z0)
        kutu(g, p[0] - 0.3, p[0] + 0.3, p[1] - 0.25, p[1] + 0.25, z0 + 0.6, z0 + 1.4, r["ana"])
        cubuk(g, (p[0], p[1], z0), (p[0], p[1], z0 + 0.6), 0.1, r["koyu"])
    elif tip == "hastane":
        duz_cati_ust(g, x0, x1, fy, by, Ht, r, rnd, r["suslu"], klima=3)
        cfc = on
        ckutu(g, cfc, W - 2.4, W - 1.6, 0, 0.12, Ht - 2.8, Ht - 0.8, r["ana"])
        ckutu(g, cfc, W - 3.0, W - 1.0, 0, 0.12, Ht - 2.2, Ht - 1.4, r["ana"])
        kutu(g, x0 + 1.0, x0 + 6.0, fy - 3.0, fy, zb + 3.2, zb + 3.5, r["beton"])                          # ambulans saçağı
        for xx in (x0 + 1.2, x0 + 5.8):
            cubuk(g, (xx, fy - 2.8, z0), (xx, fy - 2.8, zb + 3.2), 0.18, r["metal"])
        kutu(g, x0 + 2.0, x0 + 4.4, fy - 2.6, fy - 0.4, z0, z0 + 2.4, r["beyaz"])
        kutu(g, x0 + 2.0, x0 + 4.4, fy - 1.6, fy - 1.4, z0 + 1.2, z0 + 1.5, r["ana"])
        silindir(g, x1 - 2.0, by - 2.0, Ht + 0.57, 0.08, 2.0, 12, r["sari"])
    elif tip == "okul":
        r2 = dict(r)
        zr = besik_cati(g, "x", x0, x1, fy, by, Ht, r2)
        kutu(g, x0 + W / 2 - 1.0, x0 + W / 2 + 1.0, fy + D / 2 - 1.0, fy + D / 2 + 1.0, zr - 0.5, zr + 2.4, r["duvar"])
        koni(g, x0 + W / 2, fy + D / 2, zr + 2.4, 1.8, 1.4, 4, r["cati"])
        # Okul bahçesi: basket potası, işaret çizgileri
        for (a, b) in ((2.0, 6.0),):
            boya(g, a, b, 0.6, 2.8, r["serit"], z=EV_ZEMIN + 0.12)
        cubuk(g, (1.4, 1.7, z0 + 0.12), (1.4, 1.7, z0 + 3.0), 0.1, r["metal"])
        kutu(g, 1.3, 1.5, 1.1, 2.3, z0 + 2.7, z0 + 3.5, r["beyaz"])
    else:   # polis
        zr = kalca_cati(g, x0, x1, fy, by, Ht, 0.5, r["cati"])
        kutu(g, 12.0, 14.4, 0.6, 2.7, z0 + 0.12, z0 + 1.3, r["beyaz"])                   # polis aracı
        kutu(g, 12.0, 14.4, 1.4, 1.9, z0 + 0.7, z0 + 1.0, r["ana"])
        kutu(g, 12.7, 13.7, 1.2, 2.1, z0 + 1.3, z0 + 1.45, r["ana"])
    for xx in (x0 - 0.07, x1 + 0.07):
        oluk(g, xx, fy - 0.07, z0, Ht - 0.2)
    agac(g, 0.9, PD - 1.0, r, rnd, boy=1.6, rr=0.95)
    agac(g, PW - 0.9, PD - 1.0, r, rnd, boy=1.6, rr=0.95)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------- PARKLAR (park karesi havuzuna girer: Park_cim_...)

def park_duzenli(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, [(-4.4, -0.5), (4.4, -0.5), (4.4, 0.5), (-4.4, 0.5)], ZC + 0.01, r["cakil"])
    duz_poligon(g, [(-0.5, -4.4), (0.5, -4.4), (0.5, 4.4), (-0.5, 4.4)], ZC + 0.01, r["cakil"])
    duz_poligon(g, poligon(12, 1.9, 1.9), ZC + 0.012, r["cakil"])
    for sx in (-1, 1):
        for sy in (-1, 1):
            x0, x1 = sorted((sx * 1.0, sx * 4.0))
            y0, y1 = sorted((sy * 1.0, sy * 4.0))
            for (a0, a1, b0, b1) in ((x0, x1, y0, y0 + 0.35), (x0, x1, y1 - 0.35, y1), (x0, x0 + 0.35, y0, y1), (x1 - 0.35, x1, y0, y1)):
                kutu(g, a0, a1, b0, b1, ZC, ZC + 0.55, r["cali"])
            cicek_obegi(g, rnd, (x0 + x1) / 2, (y0 + y1) / 2, 0.9, 10, r, ZC)
            top(g, (sx * 4.2, sy * 4.2, ZC + 0.7), 0.4, 0.4, 0.6, r["cali"])
    prizma_poligon(g, poligon(10, 1.3, 1.3), ZC, ZC + 0.4, r["tas"])
    duz_poligon(g, poligon(10, 1.15, 1.15), ZC + 0.405, r["su"])
    silindir(g, 0.0, 0.0, ZC + 0.4, 0.8, 0.12, 6, r["tas_acik"])
    top(g, (0.0, 0.0, ZC + 1.3), 0.2, 0.2, 0.3, r["su"])
    for a in (0.0, math.pi):
        bank(g, 2.7 * math.cos(a), 0.9, -math.pi / 2, r)
    park_lambasi(g, 1.6, 1.6, r)
    park_lambasi(g, -1.6, -1.6, r)
    return g


def park_atlikarinca(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, poligon(16, 4.3, 4.3), ZC + 0.01, r["tas_acik"])
    prizma_poligon(g, poligon(16, 3.0, 3.0), ZC, ZC + 0.35, r["ahsap"])
    silindir(g, 0.0, 0.0, ZC + 0.35, 3.2, 0.5, 10, r["sari"])
    renkler = [M("Atli", c) for c in ("#e0525a", "#f6f3ec")]
    n = 12
    for i in range(n):                                                         # çizgili tente (dilimler)
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 1) / n
        pts = [(3.3 * math.cos(a0), 3.3 * math.sin(a0), ZC + 3.4), (3.3 * math.cos(a1), 3.3 * math.sin(a1), ZC + 3.4), (0.0, 0.0, ZC + 4.9),
               (3.3 * math.cos(a0), 3.3 * math.sin(a0), ZC + 3.3), (3.3 * math.cos(a1), 3.3 * math.sin(a1), ZC + 3.3), (0.0, 0.0, ZC + 4.8)]
        dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], renkler[i % 2])
        ax, ay = 3.25 * math.cos(a0), 3.25 * math.sin(a0)
        kutu(g, ax - 0.06, ax + 0.06, ay - 0.06, ay + 0.06, ZC + 3.0, ZC + 3.4, r["sari"])
    top(g, (0.0, 0.0, ZC + 5.0), 0.25, 0.25, 0.3, r["sari"])
    atlar = [M("Atli", c) for c in ("#f6f3ec", "#c8a26a", "#6b4a33")]
    for i in range(8):
        a = 2 * math.pi * i / 8
        px, py = 2.3 * math.cos(a), 2.3 * math.sin(a)
        cubuk(g, (px, py, ZC + 0.35), (px, py, ZC + 3.4), 0.05, r["sari"], kabuk=False)
        z = ZC + 1.2 + 0.3 * (i % 2)
        u = (-math.sin(a), math.cos(a), 0.0)
        yonlu_kutu(g, (px, py, z), mul(u, 0.45), (0.12 * math.cos(a), 0.12 * math.sin(a), 0), (0, 0, 0.2), atlar[i % 3])
        yonlu_kutu(g, add((px, py, z + 0.35), mul(u, 0.4)), mul(u, 0.12), (0.1 * math.cos(a), 0.1 * math.sin(a), 0), (0, 0, 0.25), atlar[i % 3])
    for k in range(2):
        a = math.pi / 4 + k * math.pi
        bank(g, 4.0 * math.cos(a), 4.0 * math.sin(a), a + math.pi, r)
    p = (3.8, -3.6)                                                            # bilet kulübesi
    kutu(g, p[0] - 0.5, p[0] + 0.5, p[1] - 0.5, p[1] + 0.5, ZC, ZC + 2.0, r["kirmizi"])
    kutu(g, p[0] - 0.6, p[0] + 0.6, p[1] - 0.6, p[1] + 0.6, ZC + 2.0, ZC + 2.15, r["beyaz"])
    return g


def park_stadyum(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, [(-4.4, -2.9), (4.4, -2.9), (4.4, 2.6), (-4.4, 2.6)], ZC + 0.01, M("Saha", "#6fae4a"))
    for x in (-4.3, 4.3):
        boya(g, x - 0.04, x + 0.04, -2.8, 2.5, r["beyaz"], z=ZC + 0.01)
    for y in (-2.8, 2.5):
        boya(g, -4.3, 4.3, y - 0.04, y + 0.04, r["beyaz"], z=ZC + 0.01)
    boya(g, -0.03, 0.03, -2.8, 2.5, r["beyaz"], z=ZC + 0.01)
    for x in (-4.3, 4.3):                                                      # kaleler
        s = 1 if x > 0 else -1
        cubuk(g, (x, -0.7, ZC), (x, -0.7, ZC + 0.9), 0.06, r["beyaz"], kabuk=False)
        cubuk(g, (x, 0.4, ZC), (x, 0.4, ZC + 0.9), 0.06, r["beyaz"], kabuk=False)
        cubuk(g, (x, -0.7, ZC + 0.9), (x, 0.4, ZC + 0.9), 0.06, r["beyaz"], kabuk=False)
    for k in range(3):                                                         # tribün
        kutu(g, -4.0, 4.0, 3.0 + k * 0.5, 3.5 + k * 0.5, ZC, ZC + 0.4 + k * 0.4, r["beton"])
        for i in range(10):
            kutu(g, -3.8 + i * 0.8, -3.3 + i * 0.8, 3.1 + k * 0.5, 3.4 + k * 0.5, ZC + 0.4 + k * 0.4, ZC + 0.5 + k * 0.4,
                 M("Koltuk", ["#3f73e6", "#d9473a"][(i + k) % 2]))
    kutu(g, -4.2, 4.2, 3.0, 4.8, ZC + 2.6, ZC + 2.75, r["cinko"])
    for x in (-4.0, 4.0):
        cubuk(g, (x, 4.6, ZC), (x, 4.6, ZC + 2.6), 0.08, r["metal"], kabuk=False)
    for (x, y) in ((-4.7, -3.4), (4.7, -3.4)):                                  # ışık direkleri
        cubuk(g, (x, y, ZC), (x, y, ZC + 5.0), 0.1, r["metal"], kabuk=False)
        kutu(g, x - 0.4, x + 0.4, y - 0.15, y + 0.15, ZC + 5.0, ZC + 5.5, r["koyu"])
        kutu(g, x - 0.35, x + 0.35, y + 0.1, y + 0.18, ZC + 5.05, ZC + 5.45, r["isik"])
    return g


def park_hobi(r, rnd):
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, [(-0.5, -4.4), (0.5, -4.4), (0.5, 4.4), (-0.5, 4.4)], ZC + 0.01, r["cakil"])
    kul_renk = [M("Kulube", c) for c in ("#5f8fd0", "#e0825a", "#6fae6a", "#f2c14a", "#a7773f")]
    for sx in (-1, 1):
        for k in range(3):
            x0, x1 = sorted((sx * 0.8, sx * 4.4))
            y0 = -4.3 + k * 2.9
            y1 = y0 + 2.6
            for j in range(3):
                yy = y0 + 0.3 + j * 0.7
                kutu(g, x0 + 0.3, x1 - 1.3, yy, yy + 0.4, ZC, ZC + 0.15, r["toprak"])
                for q in range(4):
                    top(g, (x0 + 0.6 + q * 0.5, yy + 0.2, ZC + 0.3), 0.15, 0.15, 0.15, r["cali"] if (q + j) % 3 else rnd.choice(r["cicek"]))
            kx = x1 - 0.7 if sx > 0 else x0 + 0.7
            kutu(g, kx - 0.5, kx + 0.5, y1 - 1.3, y1 - 0.3, ZC, ZC + 1.4, rnd.choice(kul_renk))
            pts = [(kx - 0.6, y1 - 1.4, ZC + 1.4), (kx + 0.6, y1 - 1.4, ZC + 1.4), (kx, y1 - 1.4, ZC + 1.9),
                   (kx - 0.6, y1 - 0.2, ZC + 1.4), (kx + 0.6, y1 - 0.2, ZC + 1.4), (kx, y1 - 0.2, ZC + 1.9)]
            dis_bukey(g, pts, [[0, 1, 2], [3, 4, 5], [0, 1, 4, 3], [1, 2, 5, 4], [2, 0, 3, 5]], r["kirmizi"])
            cubuk(g, (x0, y0 - 0.1, ZC + 0.5), (x1, y0 - 0.1, ZC + 0.5), 0.04, r["ahsap"], kabuk=False)
    return g


def park_pazar(r, rnd):
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_KAL, r["kaldirim"])
    for i in range(1, 4):
        p = -YK + i * 2.5
        boya(g, p - 0.025, p + 0.025, -YK, YK, r["kaldirim2"], z=Z_KAL)
        boya(g, -YK, YK, p - 0.025, p + 0.025, r["kaldirim2"], z=Z_KAL)
    z = Z_KAL
    tente_renk = [M("Tente", c) for c in ("#e0525a", "#3f73e6", "#6fae6a", "#f2c14a")]
    for i in range(6):                                                         # tezgâhlar
        x = -3.3 + (i % 3) * 3.3
        y = -2.6 if i < 3 else 2.4
        kutu(g, x - 1.1, x + 1.1, y - 0.5, y + 0.5, z, z + 0.85, r["ahsap"])
        for q in range(4):
            kutu(g, x - 1.0 + q * 0.52, x - 0.55 + q * 0.52, y - 0.4, y + 0.4, z + 0.85, z + 1.0, rnd.choice(r["urun"]))
        for dx in (-1.05, 1.05):
            for dy in (-0.45, 0.45):
                cubuk(g, (x + dx, y + dy, z), (x + dx, y + dy, z + 2.1), 0.05, r["metal"], kabuk=False)
        renk = tente_renk[i % 4]
        yonlu_kutu(g, (x, y, z + 2.25), (1.25, 0, 0), (0, 0.7, -0.15), (0, 0, 0.04), renk)
    prizma_poligon(g, poligon(10, 0.9, 0.9, 0.0, 0.0), z, z + 0.45, r["tas"])
    duz_poligon(g, poligon(10, 0.75, 0.75), z + 0.455, r["su"])
    silindir(g, 0.0, 0.0, z + 0.45, 0.7, 0.1, 6, r["tas_acik"])
    for (x, y) in ((-4.5, 0.0), (4.5, 0.0)):
        park_lambasi(g, x, y, r, z0=z)
    return g


def katalog_parklari():
    r = park_renkleri()
    r.update({"beton": M("PBeton", "#c9c4bb"), "cinko": M("Cinko", "#8f949a"), "urun": [M("Urun", c) for c in ("#e0525a", "#f2b84a", "#6fae4a", "#f2e3c0", "#8a5a9a")]})
    rnd = random.Random(TOHUM * 517)
    return [("Park_cim_duzenli", park_duzenli(r, rnd)), ("Park_cim_atlikarinca", park_atlikarinca(r, rnd)),
            ("Park_cim_stadyum", park_stadyum(r, rnd)), ("Park_cim_hobi", park_hobi(r, rnd)), ("Park_cim_pazar", park_pazar(r, rnd))]


KONUT_TIPLER = ["sira_evler", "tugla_apt", "avlulu_blok", "villa", "konak", "sale", "balkonlu_blok"]
BURO_TIPLER = ["tugla_cam", "is_kulesi", "saat_kuleli", "yetmisler"]
SANAYI_TIPLER = ["fabrika", "depo", "sera", "garaj", "yel_degirmeni", "ahir"]


def katalog():
    """Bütün katalog aileleri: konut ve büro 2'şer, sanayi ve kamu birer, parklar."""
    sonuc = []
    for aile, tipler, fn, adet in (("Konut", KONUT_TIPLER, konut, 2), ("Buro", BURO_TIPLER, buro, 2),
                                   ("Sanayi", SANAYI_TIPLER, sanayi, 1), ("Kamu", list(KAMU), kamu, 1)):
        for i, t in enumerate(tipler):
            for v in range(adet):
                rnd = random.Random(TOHUM * 7919 + i * 101 + v * 13)
                g, gen = fn(rnd, t)
                sonuc.append((f"{aile}_{t}_{v + 1}", g, gen))
    for ad, g in katalog_parklari():
        if CIZGI:
            kabuk(g, CIZGI_KALINLIK * 0.7)
        sonuc.append((ad, g, ortala(g)))
    return sonuc



def uret():
    if MODEL == "sehir":
        return sehir_parcalari()
    if MODEL == "katalog":
        return katalog()
    if MODEL == "dukkan":
        sonuc = []
        for i, k in enumerate(DUKKANLAR):
            rnd = random.Random(TOHUM * 1000 + i)
            g, gen = dukkanli_bina(rnd, k)
            sonuc.append((f"Dukkan_{k}", g, gen))
        return sonuc
    sonuc = []
    for i in range(max(1, VARYANT)):
        rnd = random.Random(TOHUM * 1000 + i)
        if MODEL == "ofis_kulesi":
            g, gen = ofis(rnd, OFIS_STIL)
            sonuc.append((f"OfisKulesi_{OFIS_STIL}_{TOHUM}_{i + 1}", g, gen))
        elif MODEL == "market":
            tip = MARKET_TIP if MARKET_TIP != "karisik" else ["szpar_express", "market_24", "interszpar"][i % 3]
            g, gen = market(rnd, tip)
            sonuc.append((f"Market_{tip}_{TOHUM}_{i + 1}", g, gen))
        elif MODEL == "fastfood":
            mk = FASTFOOD_MARKA if FASTFOOD_MARKA != "karisik" else ["burger_queen", "mcdanolds"][i % 2]
            g, gen = fastfood(rnd, mk)
            sonuc.append((f"FastFood_{mk}_{TOHUM}_{i + 1}", g, gen))
        elif MODEL == "apartman":
            g, gen = apartman(rnd)
            sonuc.append((f"Apartman_{TOHUM}_{i + 1}", g, gen))
        elif MODEL == "mustakil_ev":
            g, gen = mustakil_ev(rnd, EV_STIL)
            sonuc.append((f"MustakilEv_{EV_STIL}_{TOHUM}_{i + 1}", g, gen))
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
    if bpy.app.version < (5, 0, 0):
        m.use_nodes = True                   # Blender 5'te malzemeler hep düğümlü
    bsdf = m.node_tree.nodes.get("Principled BSDF") if m.node_tree is not None else None
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
    me.polygons.foreach_set("use_smooth", g.yum)
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


def lod_kaydet(k, klasor):
    """Aynı modelleri detaysız ve çizgisiz üretip ADI_Hafif.fbx olarak kaydeder (aynı tohum: geometri birebir aynı yerde).
    (Ad "_LOD1" değil: Unity o eki kendi otomatik LOD sistemine ait sanıp uyarı veriyor.)"""
    global DETAY, CIZGI
    eski = (DETAY, CIZGI)
    DETAY, CIZGI = False, False
    try:
        for ad, g, gen in uret():
            ob = blender_obje(g, ad + "_Hafif", k)
            fbx_kaydet(ob, os.path.join(klasor, ad + "_Hafif.fbx"))
            me = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if me is not None and me.users == 0:
                bpy.data.meshes.remove(me)
    finally:
        DETAY, CIZGI = eski


def main():
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    k = koleksiyon()
    klasor = os.path.expanduser(KLASOR)
    kok = klasor.split("Assets")[0] if "Assets" in klasor else os.path.dirname(klasor.rstrip("\\/"))
    if not os.path.isdir(kok):
        klasor = os.path.expanduser(YEDEK_KLASOR)
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
    if LOD and DISA_AKTAR and not karakter_mi:
        lod_kaydet(k, klasor)
        satir.append("Hafif (LOD) sürümler de kaydedildi.")
    gorunum()
    if karakter_mi:
        satir.append("Yürüme animasyonunu görmek için Space (oynat). Bekleme: aksiyonu \"idle\" yap.")
    if DISA_AKTAR:
        satir.append(f"FBX: {klasor}")
    print("\n".join(satir))
    mesaj(satir)


if IN_BLENDER and __name__ == "__main__":
    main()
