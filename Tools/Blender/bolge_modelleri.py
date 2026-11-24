# -*- coding: utf-8 -*-
"""
KURYE TYCOON · BÖLGE MODELLERİ VE SOKAK DONATILARI (Blender scripti)

Her bölgenin kendine özgü binaları (Orta Avrupa / Orta Avrupa kasabası konsepti) ve yol kenarı donatıları.
Geometri saf Python ile kurulur (bpy işlemi yok, sadece mesh + FBX dışa aktarma): Blender arka planda güvenle çalışır,
Blender dışında da önizleme resmi çizilebilir.

  Eski şehir   Bolge_eski_*    barok sıra evler, kapı altlı konak, kuleli köşe evi, tek katlı kasaba evi, kilise
  Çarşı        Bolge_karma_*   dükkânlı apartman, köşe kafe, eczane/fırın, 70'ler bloğu, mahalle marketi
  Kule         Bolge_kule_*    panel blok, cam ofis kulesi, balkonlu konut kulesi, otel, ikiz kule
  Evler        Bolge_ev_*      kocka ház, aile evi (garajlı), bungalov, modern küp ev, ikiz ev
  Banliyö      Bolge_banliyo_* uzun köy evi (tornác, gémeskút), çiftlik (ahır, góré), bağ evi
  Kampüs       Bolge_kampus_*  fakülte (revaklı), kütüphane (kubbeli), yurt, laboratuvar, spor salonu
  Sanayi       Bolge_sanayi_*  testere çatılı fabrika, lojistik depo, silolar, atölyeler, su kulesi
  AVM          Bolge_avm_*     hipermarket, mağaza sırası
  Donatılar    Donati_*        lambalar, bank, çöp kutusu, durak, ilan sütunu, büfe, direk, saman balyası ...

Çalıştırma:
  • Unity: Kurye Tycoon > Blender > Bölge modellerini ve donatıları üret
  • Terminal: blender -b --factory-startup -P Tools/Blender/bolge_modelleri.py
  • Blender'sız: python3 bolge_modelleri.py --onizleme klasor   → her modelin önizleme resmi (PNG)

Kurallar (kurye_modeller.py ile aynı; Unity tarafı bunlara göre kurulu):
  • Bina arsası 14 × 14 m, ortada; önü (sokak) −Y; arsa levhası z = 0…0,08; ölçü metre.
  • Donatılar: tabanı z = 0, önü (yola bakan yüz) −Y.
  • Malzeme adı "Etiket_rrggbb": Unity rengi addan okur. Cam_/Cam2_/Cam3_ pencere, Vitrin_ dükkân camı,
    OfisCam_ ofis camı, YIsik_ sokak lambası başı, Isik_ diğer lambalar geceleri yanar.
  • Her modelin ADI_Hafif.fbx sürümü de kaydedilir (detaysız): Unity uzakta onu gösterir.
"""

import math
import os
import random
import sys

try:
    import bpy
    IN_BLENDER = True
except ImportError:
    IN_BLENDER = False

# ================================== AYARLAR ==================================
CIKTI = ""            # boşsa Assets/KuryeTycoon/Modeller/Binalar
TOHUM = 11
LOD = True
DETAY = True          # Hafif sürüm üretilirken kapanır
SADECE = None         # örn. ("eski_barok_sira",) yalnızca bunlar

LOT = 7.0             # arsanın yarı genişliği
ZL = 0.08             # arsa levhasının üstü

# ================================== ÇEKİRDEK ==================================
HEX = {}


def M(etiket, hexv):
    hexv = hexv.lower().lstrip("#")
    ad = "%s_%s" % (etiket, hexv)
    HEX[ad] = "#" + hexv
    return ad


def add(a, b): return (a[0] + b[0], a[1] + b[1], a[2] + b[2])
def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def mul(a, s): return (a[0] * s, a[1] * s, a[2] * s)
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def norm(a):
    l = math.sqrt(dot(a, a))
    return (a[0] / l, a[1] / l, a[2] / l) if l > 1e-12 else (0.0, 0.0, 1.0)


def newell(p):
    n = [0.0, 0.0, 0.0]
    for i in range(len(p)):
        a, b = p[i], p[(i + 1) % len(p)]
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    return tuple(n)


def ort(p):
    k = 1.0 / len(p)
    return (sum(q[0] for q in p) * k, sum(q[1] for q in p) * k, sum(q[2] for q in p) * k)


class G:
    """Model geometrisi: her yüz kendi köşeleriyle (düz gölgeleme), yüz başına malzeme."""

    def __init__(self, ad):
        self.ad = ad
        self.v, self.f, self.fm, self.mats = [], [], [], []
        self.fs = []          # yüzün ait olduğu katı (hafif sürüm küçük katıları atar)
        self.sid = 0
        self._mi = {}

    def mi(self, m):
        i = self._mi.get(m)
        if i is None:
            i = self._mi[m] = len(self.mats)
            self.mats.append(m)
        return i

    def yuz(self, pts, m):
        if m is None:
            return
        b = len(self.v)
        self.v.extend(pts)
        self.f.append(list(range(b, b + len(pts))))
        self.fm.append(self.mi(m))
        self.fs.append(self.sid)

    @property
    def yum(self):
        return [False] * len(self.f)

    def ucgen(self):
        return sum(len(f) - 2 for f in self.f)


def kati(g, pts, yuzler, mat, ic=None):
    """Dışbükey katı: her yüz merkeze göre dışa döndürülür. mat: tek ad ya da yüz başına liste (None: yüz atlanır)."""
    c = ic if ic is not None else ort(pts)
    g.sid += 1
    for k, fi in enumerate(yuzler):
        m = mat[k] if isinstance(mat, (list, tuple)) else mat
        if m is None:
            continue
        P = [pts[i] for i in fi]
        if dot(newell(P), sub(ort(P), c)) < 0:
            P.reverse()
        g.yuz(P, m)


_OB = [("0-", (0, 2, 3, 1)), ("0+", (4, 5, 7, 6)), ("1-", (0, 1, 5, 4)), ("1+", (2, 6, 7, 3)), ("2-", (0, 4, 6, 2)), ("2+", (1, 3, 7, 5))]


def obox(g, c, a, b, h, mat, atla=()):
    """Yönlü kutu: merkez c, yarı eksen vektörleri a, b, h. Yüz anahtarları 0-/0+ (a), 1-/1+ (b), 2-/2+ (h)."""
    pts = []
    for sa in (-1, 1):
        for sb in (-1, 1):
            for sh in (-1, 1):
                pts.append(add(add(add(c, mul(a, sa)), mul(b, sb)), mul(h, sh)))
    # indeks: sa*4 + sb*2 + sh  (0/1)
    yuzler, mats = [], []
    for anahtar, fi in _OB:
        if anahtar in atla:
            continue
        m = mat.get(anahtar, mat.get("*")) if isinstance(mat, dict) else mat
        yuzler.append(fi)
        mats.append(m)
    kati(g, pts, yuzler, mats, ic=c)


_KUTU_ANAHTAR = {"xn": "0-", "xp": "0+", "yn": "1-", "yp": "1+", "zn": "2-", "zp": "2+"}


def kutu(g, x0, x1, y0, y1, z0, z1, mat, atla=("zn",)):
    if x1 < x0: x0, x1 = x1, x0
    if y1 < y0: y0, y1 = y1, y0
    if z1 < z0: z0, z1 = z1, z0
    c = ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)
    if isinstance(mat, dict):
        mat = {_KUTU_ANAHTAR.get(k, k): v for k, v in mat.items()}
    obox(g, c, ((x1 - x0) / 2, 0, 0), (0, (y1 - y0) / 2, 0), (0, 0, (z1 - z0) / 2), mat,
         tuple(_KUTU_ANAHTAR[k] for k in atla))


def cubuk(g, p0, p1, t, mat, t2=None):
    """İki nokta arasında kare kesitli çubuk (t: kalınlık)."""
    d = sub(p1, p0)
    L = math.sqrt(dot(d, d))
    if L < 1e-6:
        return
    ax = mul(d, 0.5 / L * L)
    u = norm(d)
    ref = (0, 0, 1) if abs(u[2]) < 0.9 else (1, 0, 0)
    b = norm(cross(u, ref))
    h = norm(cross(u, b))
    obox(g, mul(add(p0, p1), 0.5), mul(d, 0.5), mul(b, t / 2), mul(h, (t2 or t) / 2), mat)


def silindir(g, p0, p1, r0, r1, n, mat, kapak=(True, True), kapak_mat=None):
    d = norm(sub(p1, p0))
    ref = (1, 0, 0) if abs(d[0]) < 0.9 else (0, 1, 0)
    a = norm(cross(d, ref))
    b = cross(d, a)
    alt, ust = [], []
    for i in range(n):
        t = 2 * math.pi * (i + 0.5) / n
        ca, sa = math.cos(t), math.sin(t)
        alt.append(add(p0, add(mul(a, ca * r0), mul(b, sa * r0))))
        ust.append(add(p1, add(mul(a, ca * r1), mul(b, sa * r1))))
    pts = alt + ust
    yuzler, mats = [], []
    for i in range(n):
        j = (i + 1) % n
        yuzler.append([i, j, n + j, n + i])
        mats.append(mat)
    if kapak[0]:
        yuzler.append(list(range(n)))
        mats.append(kapak_mat or mat)
    if kapak[1] and r1 > 1e-6:
        yuzler.append(list(range(n, 2 * n)))
        mats.append(kapak_mat or mat)
    elif r1 <= 1e-6:
        # koni: üst noktalar çakışık; yan yüzler üçgen
        pts = alt + [p1]
        yuzler = [[i, (i + 1) % n, n] for i in range(n)]
        mats = [mat] * n
        if kapak[0]:
            yuzler.append(list(range(n)))
            mats.append(kapak_mat or mat)
    kati(g, pts, yuzler, mats, ic=mul(add(p0, p1), 0.5))


def dsil(g, x, y, z0, z1, r, n, mat, r1=None, kapak=(False, True), kapak_mat=None):
    """Dik silindir / kesik koni."""
    silindir(g, (x, y, z0), (x, y, z1), r, r if r1 is None else r1, n, mat, kapak, kapak_mat)


_ICO = None


def _ico():
    global _ICO
    if _ICO:
        return _ICO
    t = (1 + 5 ** 0.5) / 2
    v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    v = [norm(p) for p in v]
    f = [[0, 11, 5], [0, 5, 1], [0, 1, 7], [0, 7, 10], [0, 10, 11], [1, 5, 9], [5, 11, 4], [11, 10, 2], [10, 7, 6],
         [7, 1, 8], [3, 9, 4], [3, 4, 2], [3, 2, 6], [3, 6, 8], [3, 8, 9], [4, 9, 5], [2, 4, 11], [6, 2, 10], [8, 6, 7], [9, 8, 1]]
    # bir kez böl (80 üçgen)
    kenar = {}

    def orta(i, j):
        k = (min(i, j), max(i, j))
        if k not in kenar:
            kenar[k] = len(v)
            v.append(norm(mul(add(v[i], v[j]), 0.5)))
        return kenar[k]
    f2 = []
    for a, b, c in f:
        ab, bc, ca = orta(a, b), orta(b, c), orta(c, a)
        f2 += [[a, ab, ca], [b, bc, ab], [c, ca, bc], [ab, bc, ca]]
    _ICO = (v, f2)
    return _ICO


def top(g, c, rx, ry, rz, mat, rnd=None, puruz=0.0):
    v, f = _ico()
    pts = []
    for p in v:
        k = 1.0 + (rnd.uniform(-puruz, puruz) if rnd and puruz else 0.0)
        pts.append((c[0] + p[0] * rx * k, c[1] + p[1] * ry * k, c[2] + p[2] * rz * k))
    kati(g, pts, f, mat, ic=c)


def prizma(g, taban, d, mat, kapak=True):
    """Dışbükey çokgeni (3B noktalar) d vektörü boyunca uzatır."""
    n = len(taban)
    ust = [add(p, d) for p in taban]
    pts = list(taban) + ust
    yuzler = [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    mats = [mat] * n
    if kapak:
        yuzler += [list(range(n)), list(range(n, 2 * n))]
        mats += [mat if not isinstance(kapak, str) else kapak] * 2
    kati(g, pts, yuzler, mats)


# ------------------------------------------------------------------ cephe çerçevesi

class C:
    """Cephe: o (x, y) duvar üzerinde sol uç, u duvar boyunca (soldan sağa, dışarıdan bakınca), n dışa normal."""

    def __init__(self, o, u, n, L):
        self.o, self.u, self.n, self.L = o, u, n, L

    def p(self, u, o, z):
        return (self.o[0] + self.u[0] * u + self.n[0] * o, self.o[1] + self.u[1] * u + self.n[1] * o, z)

    def kutu(self, g, u0, u1, o0, o1, z0, z1, mat, arka=False):
        if u1 < u0: u0, u1 = u1, u0
        if z1 < z0: z0, z1 = z1, z0
        c = self.p((u0 + u1) / 2, (o0 + o1) / 2, (z0 + z1) / 2)
        a = (self.u[0] * (u1 - u0) / 2, self.u[1] * (u1 - u0) / 2, 0)
        b = (self.n[0] * (o1 - o0) / 2, self.n[1] * (o1 - o0) / 2, 0)
        obox(g, c, a, b, (0, 0, (z1 - z0) / 2), mat, () if arka else ("1-",))

    def prizma_uc(self, g, u0, u1, o0, o1, zb, h, mat):
        """Cepheye dik, üçgen alınlık (u0–u1 tabanı, h yüksekliği), o0–o1 kalınlığı."""
        taban = [self.p(u0, o0, zb), self.p(u1, o0, zb), self.p((u0 + u1) / 2, o0, zb + h)]
        prizma(g, taban, (self.n[0] * (o1 - o0), self.n[1] * (o1 - o0), 0), mat)

    def yarim_disk(self, g, uc, zc, r, o0, o1, mat, n=8):
        taban = [self.p(uc + r * math.cos(math.pi * i / n), o0, zc + r * math.sin(math.pi * i / n)) for i in range(n + 1)]
        prizma(g, taban, (self.n[0] * (o1 - o0), self.n[1] * (o1 - o0), 0), mat)


def govde(g, x0, x1, y0, y1, z0, z1, mat, atla=("zn",)):
    """Dikdörtgen gövde ve dört cephesi: on (−Y), sag (+X), arka (+Y), sol (−X)."""
    kutu(g, x0, x1, y0, y1, z0, z1, mat, atla)
    return dict(
        on=C((x0, y0), (1, 0), (0, -1), x1 - x0),
        sag=C((x1, y0), (0, 1), (1, 0), y1 - y0),
        arka=C((x1, y1), (-1, 0), (0, 1), x1 - x0),
        sol=C((x0, y1), (0, -1), (-1, 0), y1 - y0),
    )


# ------------------------------------------------------------------ renkler

def koyu(hexv, f=0.8):
    h = hexv.lstrip("#")
    r, gg, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return "#%02x%02x%02x" % (int(r * f), int(gg * f), int(b * f))


def acik(hexv, f=0.25):
    h = hexv.lstrip("#")
    r, gg, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return "#%02x%02x%02x" % (int(r + (255 - r) * f), int(gg + (255 - gg) * f), int(b + (255 - b) * f))


def cam(rnd, renk="#a9c9de"):
    return M(rnd.choice(("Cam", "Cam2", "Cam3")), renk)


SABIT = dict(
    demir="#2b2d31", beyaz="#f4f0e6", krem="#efe3c9", tas="#c9c1b3", tas_koyu="#a59d90", beton="#bdb8ae",
    kiremit="#b5553a", kiremit2="#a0482f", kiremit3="#c46a43", arduvaz="#59606b", bakir="#6fa58a", cinko="#9aa3a8",
    ahsap="#8a5a3a", ahsap_acik="#b58a5e", cim="#8fbf5f", cim2="#7fb356", toprak="#8a6a4a", kaldirim="#d8d1c4",
    asfalt="#5b5e63", yaprak=("#5f9a4f", "#77ad57", "#4d8a48", "#8cbf62"), govde="#7a5238", su="#5aa6d6",
)


# ------------------------------------------------------------------ küçük parçalar

def agac(g, x, y, z, rnd, boy=None, tip=None):
    """Boyalı yapraklı ağaç ya da çam."""
    tip = tip or rnd.choice(("yuvarlak", "yuvarlak", "yuvarlak", "cam", "kavak"))
    boy = boy or rnd.uniform(4.5, 7.0)
    gm = M("AgacGovde", SABIT["govde"])
    if tip == "cam":
        dsil(g, x, y, z, z + boy * 0.25, 0.18, 6, gm, r1=0.14)
        ton = rnd.choice(("#3f7a48", "#4a8650", "#356d42"))
        for k in range(3):
            r = boy * (0.34 - k * 0.08)
            z0 = z + boy * (0.18 + k * 0.24)
            dsil(g, x, y, z0, z0 + boy * 0.42, r, 8, M("Yaprak", ton if k != 1 else acik(ton, 0.12)), r1=0.0, kapak=(True, False))
        return
    if tip == "kavak":
        dsil(g, x, y, z, z + boy * 0.2, 0.16, 6, gm, r1=0.12)
        top(g, (x, y, z + boy * 0.58), boy * 0.17, boy * 0.17, boy * 0.42, M("Yaprak", rnd.choice(SABIT["yaprak"])), rnd, 0.08)
        return
    dsil(g, x, y, z, z + boy * 0.45, 0.2, 6, gm, r1=0.13)
    R = boy * rnd.uniform(0.28, 0.34)
    zc = z + boy * 0.62
    tonlar = list(SABIT["yaprak"])
    rnd.shuffle(tonlar)
    top(g, (x, y, zc), R, R, R * 0.85, M("Yaprak", tonlar[0]), rnd, 0.1)
    if DETAY:
        for k in range(3):
            a = rnd.uniform(0, 2 * math.pi)
            rr = R * rnd.uniform(0.45, 0.6)
            top(g, (x + math.cos(a) * R * 0.6, y + math.sin(a) * R * 0.6, zc + rnd.uniform(-0.2, 0.4) * R), rr, rr, rr * 0.85,
                M("Yaprak", tonlar[1 + k % 3]), rnd, 0.1)


def cali(g, x, y, z, r, rnd, ton=None):
    ton = ton or rnd.choice(("#5f9a4f", "#6ea552", "#4f8c4a"))
    top(g, (x, y, z + r * 0.6), r, r, r * 0.7, M("Cali", ton), rnd, 0.12)


def cit_cali(g, x0, x1, y0, y1, z, h, rnd):
    """Budanmış çit (kutu)."""
    kutu(g, x0, x1, y0, y1, z, z + h, M("Cali", rnd.choice(("#5a924c", "#4f8a47", "#64995a"))))


def cit_parmak(g, p0, p1, z, h, renk, aralik=0.14, kal=0.05):
    """Parmaklık / bahçe çiti: üst ve alt kuşak + dikmeler."""
    m = M("Cit", renk)
    d = sub(p1, p0)
    L = math.sqrt(dot(d, d))
    if L < 0.1:
        return
    cubuk(g, (p0[0], p0[1], z + h * 0.85), (p1[0], p1[1], z + h * 0.85), kal, m)
    cubuk(g, (p0[0], p0[1], z + h * 0.25), (p1[0], p1[1], z + h * 0.25), kal, m)
    if not DETAY:
        return
    n = max(1, int(L / aralik))
    for i in range(n + 1):
        t = i / n
        p = (p0[0] + d[0] * t, p0[1] + d[1] * t)
        cubuk(g, (p[0], p[1], z), (p[0], p[1], z + h), kal * 0.8, m)


def cit_tahta(g, p0, p1, z, h, renk, aralik=0.18):
    """Ahşap çakma çit (sivri uçlu tahtalar)."""
    m = M("Cit", renk)
    d = sub(p1, p0)
    L = math.sqrt(dot(d, d))
    if L < 0.1:
        return
    cubuk(g, (p0[0], p0[1], z + h * 0.7), (p1[0], p1[1], z + h * 0.7), 0.05, m, 0.08)
    cubuk(g, (p0[0], p0[1], z + h * 0.25), (p1[0], p1[1], z + h * 0.25), 0.05, m, 0.08)
    if not DETAY:
        return
    u = norm((d[0], d[1], 0))
    nn = (-u[1], u[0], 0)
    n = max(1, int(L / aralik))
    for i in range(n):
        t = (i + 0.5) / n
        p = (p0[0] + d[0] * t, p0[1] + d[1] * t, z)
        a, b = mul(u, 0.055), mul(nn, 0.015)
        taban = [add(add(p, a), b), add(sub(p, b), a), sub(sub(p, a), b), add(sub(p, a), b)]
        # dikdörtgen tahta + sivri uç
        ust = (p[0], p[1], z + h)
        pts = [add(q, (0, 0, h * 0.9)) for q in taban]
        kati(g, taban + pts, [[0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], m)
        kati(g, pts + [add(ust, (0, 0, 0.0))], [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], m)


ZEMIN = False         # arsa levhası: kapalı (bina şehrin kendi zeminine oturur; kare çim/taş plakalar zeminle uyuşmuyordu)
# Zemin örtüsü sayılan yamalar (çim, avlu taşı, asfalt, kaldırım) levha kapalıyken çizilmez; işlevli yüzeyler kalır
ORTU = ("Cim_", "TasAvlu_", "Asfalt_", "Kaldirim_", "Beton_a9a49a", "Beton_b3ada2")


def zemin(g, mat, kenar=None):
    """14 × 14 arsa levhası (ZEMIN açıksa). Kapalıyken modelin ölçüsü yine 14 m arsaya göredir (Unity sabit ölçek kullanır)."""
    if ZEMIN:
        kutu(g, -LOT, LOT, -LOT, LOT, 0, ZL, mat, atla=("zn",))


def yama(g, x0, x1, y0, y1, mat, z=ZL, k=0.03):
    """Arsanın üstünde ince boyalı alan (yol, çakıl, çiçeklik, teras, saha)."""
    if not ZEMIN and mat.startswith(ORTU):
        return
    kutu(g, x0, x1, y0, y1, z, z + k, mat)


def saksi_cicek(g, x, y, z, rnd, r=0.35):
    dsil(g, x, y, z, z + r * 1.1, r, 8, M("Saksi", "#b5553a"), r1=r * 1.2, kapak=(False, True), kapak_mat=M("Toprak", SABIT["toprak"]))
    cali(g, x, y, z + r * 1.0, r * 1.1, rnd)


def cicek_tarhi(g, x0, x1, y0, y1, rnd, n=None):
    yama(g, x0, x1, y0, y1, M("Toprak", "#7a5a3e"), k=0.05)
    if not DETAY:
        return
    n = n or int((x1 - x0) * (y1 - y0) * 3)
    renkler = ("#e0525a", "#f0a0c0", "#f2d04a", "#ffffff", "#b07ad8", "#ff8a3c")
    for i in range(n):
        x, y = rnd.uniform(x0 + 0.1, x1 - 0.1), rnd.uniform(y0 + 0.1, y1 - 0.1)
        top(g, (x, y, ZL + 0.15), 0.12, 0.12, 0.1, M("Cicek", rnd.choice(renkler)))


# ================================== CEPHE ÖĞELERİ ==================================

def pencere(g, c, u, zb, w, h, st, rnd):
    """Pencere: cam (Cam_/Cam2_/Cam3_), kasa, kayıt, denizlik, lento/alınlık, panjur, çiçeklik.
    st: tip (klasik, modern, kemer, kare), cerceve, denizlik, lento (duz, alinlik, kilit, None), lento_mat, panjur, saksi (olasılık)."""
    tip = st.get("tip", "klasik")
    cm = cam(rnd, st.get("cam", "#a9c9de"))
    fr = st.get("cerceve") or M("Cerceve", "#f4f0e6")
    u0, u1 = u - w / 2, u + w / 2
    hr = h - w / 2 if tip == "kemer" else h
    c.kutu(g, u0, u1, 0, 0.03, zb, zb + hr, cm)
    if tip == "kemer":
        c.yarim_disk(g, u, zb + hr, w / 2, 0, 0.03, cm, n=8)
    if DETAY:
        t = 0.07
        c.kutu(g, u0 - t, u0, 0, 0.08, zb, zb + hr, fr)
        c.kutu(g, u1, u1 + t, 0, 0.08, zb, zb + hr, fr)
        c.kutu(g, u0, u1, 0, 0.08, zb, zb + 0.06, fr)
        if tip == "kemer":
            n = 7
            R = w / 2 + t / 2
            for i in range(n):
                a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
                cubuk(g, c.p(u + R * math.cos(a0), 0.04, zb + hr + R * math.sin(a0)),
                      c.p(u + R * math.cos(a1), 0.04, zb + hr + R * math.sin(a1)), t, fr, 0.08)
            c.kutu(g, u0, u1, 0, 0.07, zb + hr - 0.03, zb + hr + 0.03, fr)
        else:
            c.kutu(g, u0 - t, u1 + t, 0, 0.08, zb + h, zb + h + t, fr)
        if tip == "klasik":
            if w > 0.65:
                c.kutu(g, u - 0.03, u + 0.03, 0, 0.06, zb, zb + h, fr)
            c.kutu(g, u0, u1, 0, 0.06, zb + h * 0.7, zb + h * 0.7 + 0.06, fr)
        elif tip == "modern" and w > 1.5:
            c.kutu(g, u - 0.03, u + 0.03, 0, 0.06, zb, zb + h, fr)
        elif tip == "kare":
            c.kutu(g, u - 0.025, u + 0.025, 0, 0.05, zb, zb + h, fr)
            c.kutu(g, u0, u1, 0, 0.05, zb + h / 2 - 0.025, zb + h / 2 + 0.025, fr)
    ds = st.get("denizlik", M("Tas", SABIT["tas"]))
    if ds:
        c.kutu(g, u0 - 0.1, u1 + 0.1, 0, 0.15, zb - 0.09, zb, ds)
    lt = st.get("lento")
    lm = st.get("lento_mat") or ds or fr
    ust = zb + h + (0.07 if DETAY else 0)
    if lt == "duz":
        c.kutu(g, u0 - 0.13, u1 + 0.13, 0, 0.1, ust, ust + 0.18, lm)
    elif lt == "alinlik":
        c.kutu(g, u0 - 0.15, u1 + 0.15, 0, 0.12, ust, ust + 0.12, lm)
        c.prizma_uc(g, u0 - 0.2, u1 + 0.2, 0, 0.12, ust + 0.12, 0.32, lm)
    elif lt == "kemerli":
        c.kutu(g, u0 - 0.13, u1 + 0.13, 0, 0.1, ust, ust + 0.12, lm)
        c.yarim_disk(g, u, ust + 0.12, w / 2 + 0.12, 0, 0.1, lm, n=6)
    elif lt == "kilit" and DETAY:
        c.kutu(g, u - 0.12, u + 0.12, 0, 0.1, ust - 0.08, ust + 0.22, lm)
    pj = st.get("panjur")
    if pj:
        pw = min(0.6, w * 0.5)
        for a, b in ((u0 - pw - 0.08, u0 - 0.08), (u1 + 0.08, u1 + pw + 0.08)):
            c.kutu(g, a, b, 0, 0.05, zb, zb + hr, pj)
            if DETAY:
                k = max(3, int(hr / 0.22))
                for i in range(1, k):
                    z = zb + hr * i / k
                    c.kutu(g, a + 0.03, b - 0.03, 0.05, 0.07, z - 0.02, z + 0.02, M("Panjur", koyu(HEX[pj], 0.82)))
    if DETAY and rnd.random() < st.get("saksi", 0.0):
        c.kutu(g, u0, u1, 0.06, 0.3, zb - 0.32, zb - 0.1, M("Saksi", "#9a4a32"))
        for i in range(max(2, int(w / 0.22))):
            uu = u0 + 0.1 + (w - 0.2) * (i + 0.5) / max(2, int(w / 0.22))
            p = c.p(uu, 0.2, zb - 0.02)
            top(g, p, 0.13, 0.13, 0.11, M("Cicek", rnd.choice(("#e0525a", "#f06a8a", "#ffffff", "#f2a03a"))))


def pencere_dizisi(g, c, zb, w, h, st, rnd, kenar=0.8, aralik=None, atla=(), u0=0.0, u1=None):
    """Cephe boyunca eşit aralıklı pencereler; atla: (a, b) u aralıkları (kapı, kapı altı) boş kalır. Pencere u'larını döndürür."""
    u1 = c.L if u1 is None else u1
    L = u1 - u0 - 2 * kenar
    if L < w:
        return []
    aralik = aralik or max(w + 0.6, 1.9)
    n = max(1, int((L + aralik - w) / aralik))
    adim = (L - w) / max(1, n - 1) if n > 1 else 0
    us = []
    for i in range(n):
        u = u0 + kenar + w / 2 + adim * i if n > 1 else (u0 + u1) / 2
        if any(a - w / 2 < u < b + w / 2 for a, b in atla):
            continue
        pencere(g, c, u, zb, w, h, st, rnd)
        us.append(u)
    return us


def kapi(g, c, u, zb, w, h, st, rnd, z0=ZL, sacak=False):
    km = st.get("kapi") or M("Kapi", rnd.choice(("#5a3a2a", "#2f4a3a", "#3b4c6b", "#7a2f2f", "#4a4a4a")))
    fr = st.get("cerceve") or M("Cerceve", "#f4f0e6")
    c.kutu(g, u - w / 2, u + w / 2, 0, 0.04, zb, zb + h, km)
    if DETAY:
        c.kutu(g, u - w / 2 - 0.08, u - w / 2, 0, 0.1, zb, zb + h + 0.08, fr)
        c.kutu(g, u + w / 2, u + w / 2 + 0.08, 0, 0.1, zb, zb + h + 0.08, fr)
        c.kutu(g, u - w / 2 - 0.08, u + w / 2 + 0.08, 0, 0.1, zb + h, zb + h + 0.08, fr)
        pk = M("Kapi", koyu(HEX[km], 0.8))
        if w > 1.3:   # çift kanat
            for s in (-1, 1):
                a = u + s * w / 4
                c.kutu(g, a - w / 4 + 0.1, a + w / 4 - 0.1, 0.04, 0.06, zb + 0.15, zb + h * 0.45, pk)
                c.kutu(g, a - w / 4 + 0.1, a + w / 4 - 0.1, 0.04, 0.06, zb + h * 0.52, zb + h * 0.72, pk)
            c.kutu(g, u - 0.02, u + 0.02, 0.04, 0.07, zb, zb + h * 0.75, pk)
        else:
            c.kutu(g, u - w / 2 + 0.12, u + w / 2 - 0.12, 0.04, 0.06, zb + 0.15, zb + h * 0.45, pk)
            c.kutu(g, u - w / 2 + 0.12, u + w / 2 - 0.12, 0.04, 0.06, zb + h * 0.52, zb + h * 0.72, pk)
        c.kutu(g, u - w / 2 + 0.08, u + w / 2 - 0.08, 0, 0.05, zb + h * 0.77, zb + h - 0.06, cam(rnd))
        c.kutu(g, u + w / 2 - 0.2, u + w / 2 - 0.12, 0.04, 0.1, zb + h * 0.42, zb + h * 0.47, M("Pirinc", "#c9a24a"))
    yuk = zb - z0
    if yuk > 0.05:
        n = max(1, int(math.ceil(yuk / 0.17)))
        r = yuk / n
        tm = st.get("basamak") or M("Tas", SABIT["tas"])
        for i in range(n):
            c.kutu(g, u - w / 2 - 0.25, u + w / 2 + 0.25, 0, 0.32 * (n - i), z0 + i * r, z0 + (i + 1) * r, tm)
    if sacak:
        sm = st.get("sacak_mat") or M("Saçak".replace("ç", "c"), "#3a3d42")
        c.kutu(g, u - w / 2 - 0.35, u + w / 2 + 0.35, 0, 0.9, zb + h + 0.2, zb + h + 0.3, sm)
        if DETAY:
            for s in (-1, 1):
                cubuk(g, c.p(u + s * (w / 2 + 0.25), 0.85, zb + h + 0.2), c.p(u + s * (w / 2 + 0.25), 0.05, zb + h - 0.35), 0.04, sm)


def balkon(g, c, u0, u1, z, d, st, rnd, korkuluk=None):
    """Balkon: döşeme, konsollar, korkuluk (demir, cam, dolu)."""
    sm = st.get("balkon_mat") or M("Beton", "#e8e3d8")
    c.kutu(g, u0, u1, 0, d, z - 0.16, z, sm)
    tip = korkuluk or st.get("korkuluk", "demir")
    km = st.get("korkuluk_mat") or M("Demir", SABIT["demir"])
    H = 1.0
    if tip == "dolu":
        c.kutu(g, u0, u1, d - 0.1, d, z, z + H, sm)
        c.kutu(g, u0, u0 + 0.1, 0, d - 0.1, z, z + H, sm)
        c.kutu(g, u1 - 0.1, u1, 0, d - 0.1, z, z + H, sm)
        return
    if tip == "cam":
        gm = M("Korkuluk", "#bcd6de")
        c.kutu(g, u0 + 0.03, u1 - 0.03, d - 0.05, d - 0.02, z, z + H, gm)
        c.kutu(g, u0 + 0.03, u0 + 0.06, 0.05, d - 0.05, z, z + H, gm)
        c.kutu(g, u1 - 0.06, u1 - 0.03, 0.05, d - 0.05, z, z + H, gm)
        if DETAY:
            c.kutu(g, u0, u1, d - 0.07, d, z + H, z + H + 0.05, km)
        return
    # demir parmaklık
    c.kutu(g, u0, u1, d - 0.06, d, z + H - 0.05, z + H, km)
    c.kutu(g, u0, u0 + 0.05, 0, d, z + H - 0.05, z + H, km)
    c.kutu(g, u1 - 0.05, u1, 0, d, z + H - 0.05, z + H, km)
    if DETAY:
        c.kutu(g, u0, u1, d - 0.05, d, z + 0.08, z + 0.12, km)
        n = max(2, int((u1 - u0) / 0.14))
        for i in range(n + 1):
            uu = u0 + 0.03 + (u1 - u0 - 0.06) * i / n
            c.kutu(g, uu - 0.015, uu + 0.015, d - 0.045, d - 0.015, z, z + H, km)
        for k in range(1, max(2, int(d / 0.14))):
            oo = d * k / max(2, int(d / 0.14))
            for uu in (u0 + 0.025, u1 - 0.025):
                c.kutu(g, uu - 0.015, uu + 0.015, oo - 0.015, oo + 0.015, z, z + H, km)
        if st.get("konsol", True):
            for uu in (u0 + 0.2, u1 - 0.2):
                c.kutu(g, uu - 0.08, uu + 0.08, 0, d * 0.7, z - 0.45, z - 0.16, sm)


def korniz(g, c, z, h, d, mat, u0=None, u1=None, tasma=0.0, profil=True):
    u0 = 0.0 if u0 is None else u0
    u1 = c.L if u1 is None else u1
    if profil and DETAY:
        c.kutu(g, u0 - tasma * 0.5, u1 + tasma * 0.5, 0, d * 0.45, z, z + h * 0.3, mat)
        c.kutu(g, u0 - tasma * 0.8, u1 + tasma * 0.8, 0, d * 0.75, z + h * 0.3, z + h * 0.65, mat)
        c.kutu(g, u0 - tasma, u1 + tasma, 0, d, z + h * 0.65, z + h, mat)
    else:
        c.kutu(g, u0 - tasma, u1 + tasma, 0, d, z, z + h, mat)


def tente(g, c, u0, u1, z, cikma, r1, r2, dusus=0.55):
    """Çizgili tente: eğimli şeritler, saçak eteği, yan üçgenler."""
    n = max(2, int((u1 - u0) / 0.32))
    m1, m2 = M("Tente", r1), M("Tente", r2)
    for i in range(n):
        a, b = u0 + (u1 - u0) * i / n, u0 + (u1 - u0) * (i + 1) / n
        m = m1 if i % 2 == 0 else m2
        if not DETAY:
            m = m1
        p = [c.p(a, 0, z), c.p(b, 0, z), c.p(b, cikma, z - dusus), c.p(a, cikma, z - dusus)]
        q = [add(x, (0, 0, -0.04)) for x in p]
        kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], m)
        c.kutu(g, a, b, cikma - 0.02, cikma, z - dusus - 0.22, z - dusus, m)
    if DETAY:
        for uu in (u0, u1 - 0.03):
            taban = [c.p(uu, 0, z), c.p(uu, cikma, z - dusus), c.p(uu, 0, z - dusus)]
            prizma(g, taban, (c.u[0] * 0.03, c.u[1] * 0.03, 0), m1)


def yazi_seridi(g, c, u0, u1, zc, boy, renk, rnd, o=0.14):
    """Tabelada harfleri andıran parlak kutucuklar (Yazi_: geceleri yanar)."""
    if not DETAY:
        return
    m = M("Yazi", renk)
    L = u1 - u0
    harf = boy * 0.62
    n = max(3, min(int(L * 0.75 / (harf * 0.8)), 14))
    w = L * 0.75
    bas = u0 + (L - w) / 2
    aralik = w / n
    for i in range(n):
        if rnd.random() < 0.12 and 0 < i < n - 1:
            continue
        a = bas + aralik * i + aralik * 0.12
        hh = boy * (0.75 if rnd.random() < 0.8 else 1.0)
        c.kutu(g, a, a + aralik * 0.72, o, o + 0.04, zc - hh / 2, zc + hh / 2, m)


def vitrin(g, c, u0, u1, z0, h, st, rnd, kapi_u=None):
    """Dükkân önü: vitrin camı (Vitrin_), kaide, kayıtlar, cam kapı, tabela bandı ve yazısı."""
    vm = M("Vitrin", st.get("vitrin", "#cfe3ea"))
    km = st.get("vitrin_kasa") or M("Kasa", "#33363b")
    taban = 0.35
    c.kutu(g, u0, u1, 0, 0.03, z0 + taban, z0 + h, vm)
    c.kutu(g, u0, u1, 0, 0.07, z0, z0 + taban, st.get("kaide") or M("Tas", SABIT["tas_koyu"]))
    if kapi_u is None:
        kapi_u = u0 + (u1 - u0) * rnd.choice((0.2, 0.5, 0.8))
    if DETAY:
        n = max(1, int((u1 - u0) / 1.6))
        for i in range(n + 1):
            uu = u0 + (u1 - u0) * i / n
            c.kutu(g, uu - 0.05, uu + 0.05, 0, 0.08, z0, z0 + h, km)
        c.kutu(g, u0, u1, 0, 0.08, z0 + h - 0.06, z0 + h, km)
        c.kutu(g, u0, u1, 0, 0.08, z0 + h * 0.78, z0 + h * 0.78 + 0.05, km)
        # cam kapı
        c.kutu(g, kapi_u - 0.55, kapi_u + 0.55, 0, 0.1, z0, z0 + 0.06, km)
        c.kutu(g, kapi_u - 0.55, kapi_u - 0.48, 0, 0.1, z0, z0 + h * 0.78, km)
        c.kutu(g, kapi_u + 0.48, kapi_u + 0.55, 0, 0.1, z0, z0 + h * 0.78, km)
        c.kutu(g, kapi_u - 0.48, kapi_u + 0.48, 0, 0.05, z0, z0 + taban, vm)
        c.kutu(g, kapi_u + 0.25, kapi_u + 0.3, 0.08, 0.14, z0 + 0.8, z0 + 1.5, M("Metal", "#c7c9cc"))
    tm = st.get("tabela") or M("Tabela", rnd.choice(("#2f5d50", "#7a2f2f", "#2f3f6b", "#c46a2b", "#3a3a3a", "#5a2f5a")))
    c.kutu(g, u0 - 0.05, u1 + 0.05, 0, 0.14, z0 + h + 0.05, z0 + h + 0.7, tm)
    yazi_seridi(g, c, u0, u1, z0 + h + 0.375, 0.42, st.get("yazi", "#fff2c8"), rnd)


def bayrak_tabela(g, c, u, z, rnd, renk=None, ikon=None):
    """Cepheye dik tabela (eczane haçı, fincan, simit...)."""
    tm = M("Tabela", renk or rnd.choice(("#2f5d50", "#7a2f2f", "#2f3f6b", "#c46a2b")))
    c.kutu(g, u - 0.04, u + 0.04, 0, 0.25, z + 0.25, z + 0.33, M("Demir", SABIT["demir"]))
    c.kutu(g, u - 0.05, u + 0.05, 0.2, 0.85, z, z + 0.65, tm)
    if not DETAY or not ikon:
        return
    im = M("Yazi", "#f4f4ec" if ikon != "hac" else "#4ad07a")
    oc, zc = 0.525, z + 0.325
    for s in (-1, 1):
        uu = u + s * 0.07
        if ikon == "hac":
            c.kutu(g, uu - 0.02, uu + 0.02, oc - 0.06, oc + 0.06, zc - 0.22, zc + 0.22, im)
            c.kutu(g, uu - 0.02, uu + 0.02, oc - 0.22, oc + 0.22, zc - 0.06, zc + 0.06, im)
        else:
            c.kutu(g, uu - 0.02, uu + 0.02, oc - 0.18, oc + 0.18, zc - 0.18, zc + 0.18, im)


def klima(g, c, u, z, o=0.0):
    m = M("Metal", "#d9dcdf")
    c.kutu(g, u - 0.4, u + 0.4, o, o + 0.32, z, z + 0.55, m)
    if DETAY:
        c.kutu(g, u - 0.3, u + 0.12, o + 0.32, o + 0.34, z + 0.1, z + 0.45, M("Metal", "#8f9499"))


def canak(g, x, y, z, yon=(0, -1)):
    m = M("Metal", "#e6e8ea")
    if not DETAY:
        return
    cubuk(g, (x, y, z), (x, y, z + 0.5), 0.04, m)
    c = (x + yon[0] * 0.1, y + yon[1] * 0.1, z + 0.62)
    silindir(g, c, add(c, (yon[0] * 0.12, yon[1] * 0.12, 0.08)), 0.32, 0.05, 10, m)


# ================================== ÇATILAR ==================================

def besik_cati(g, x0, x1, y0, y1, z, egim, cm, dm, sacak=0.35, yan=0.25, kalin=0.14, siralar=True):
    """Beşik çatı (mahya X boyunca). cm: çatı, dm: alınlık duvarı. Çatı yüksekliği fonksiyonunu döndürür."""
    ym = (y0 + y1) / 2
    hh = (y1 - y0) / 2 * egim
    prizma(g, [(x0, y0, z), (x0, y1, z), (x0, ym, z + hh)], (x1 - x0, 0, 0), dm, kapak=True)
    L = math.hypot((y1 - y0) / 2 + sacak, hh + sacak * egim)
    mahya = M("Mahya", koyu(HEX[cm], 0.75))
    for s in (-1, 1):
        ye = (y0 - sacak) if s < 0 else (y1 + sacak)
        e = (0, ye, z - sacak * egim)
        r = (0, ym, z + hh)
        dv = sub(r, e)
        n = norm((0, -s * abs(dv[2]) * (1 if s < 0 else 1), abs(dv[1])))
        n = norm((0, (-1 if s < 0 else 1) * abs(dv[2]), abs(dv[1])))
        cx = (x0 + x1) / 2
        mid = add((cx, (e[1] + r[1]) / 2, (e[2] + r[2]) / 2), mul(n, kalin / 2))
        obox(g, mid, ((x1 - x0) / 2 + yan, 0, 0), mul(dv, 0.5), mul(n, kalin / 2), {"*": cm, "2-": M("Sacakalti", "#d9d2c4")})
        if siralar and DETAY:
            k = max(3, int(L / 0.5))
            for i in range(1, k):
                t = i / k
                p = add(add((cx, e[1], e[2]), mul(dv, t)), mul(n, kalin + 0.025))
                obox(g, p, ((x1 - x0) / 2 + yan, 0, 0), mul(norm(dv), 0.06), mul(n, 0.025), M("Cati", koyu(HEX[cm], 0.9)))
    cubuk(g, (x0 - yan, ym, z + hh + kalin * 0.8), (x1 + yan, ym, z + hh + kalin * 0.8), 0.24, mahya, 0.2)

    def zr(y):
        return z + max(0.0, min(y - y0, y1 - y)) * egim
    return zr


def besik_cati_y(g, x0, x1, y0, y1, z, egim, cm, dm, sacak=0.35, yan=0.25, kalin=0.14, siralar=True):
    """Beşik çatı, mahya Y boyunca (alınlık sokağa bakar). Koordinatları çevirip besik_cati'yi kullanır."""
    g2 = G("tmp")
    besik_cati(g2, y0, y1, -x1, -x0, z, egim, cm, dm, sacak, yan, kalin, siralar)
    for f, m in zip(g2.f, g2.fm):
        pts = [(-g2.v[i][1], g2.v[i][0], g2.v[i][2]) for i in f]
        # (x, y) → (−y, x) dönüşü 90°: yüz yönü korunur
        g.yuz(pts, g2.mats[m])

    def zr(x):
        return z + max(0.0, min(x - x0, x1 - x)) * egim
    return zr


def kirma_cati(g, x0, x1, y0, y1, z, egim, cm, sacak=0.4, sirt=True):
    """Kırma (dört yana eğimli) çatı; kareyse piramit."""
    x0s, x1s, y0s, y1s = x0 - sacak, x1 + sacak, y0 - sacak, y1 + sacak
    zb = z - sacak * egim
    W, D = x1s - x0s, y1s - y0s
    hh = min(W, D) / 2 * egim
    zt = zb + hh
    if abs(W - D) < 0.05:
        ap = ((x0s + x1s) / 2, (y0s + y1s) / 2, zt)
        pts = [(x0s, y0s, zb), (x1s, y0s, zb), (x1s, y1s, zb), (x0s, y1s, zb), ap]
        kati(g, pts, [[0, 1, 2, 3], [0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4]], [M("Sacakalti", "#d9d2c4"), cm, cm, cm, cm])
        sirtlar = [(pts[i], ap) for i in range(4)]
    elif W > D:
        a, b = (x0s + D / 2, (y0s + y1s) / 2, zt), (x1s - D / 2, (y0s + y1s) / 2, zt)
        pts = [(x0s, y0s, zb), (x1s, y0s, zb), (x1s, y1s, zb), (x0s, y1s, zb), a, b]
        kati(g, pts, [[0, 1, 2, 3], [0, 1, 5, 4], [2, 3, 4, 5], [1, 2, 5], [3, 0, 4]], [M("Sacakalti", "#d9d2c4"), cm, cm, cm, cm])
        sirtlar = [(pts[0], a), (pts[3], a), (pts[1], b), (pts[2], b), (a, b)]
    else:
        a, b = ((x0s + x1s) / 2, y0s + W / 2, zt), ((x0s + x1s) / 2, y1s - W / 2, zt)
        pts = [(x0s, y0s, zb), (x1s, y0s, zb), (x1s, y1s, zb), (x0s, y1s, zb), a, b]
        kati(g, pts, [[0, 1, 2, 3], [1, 2, 5, 4], [3, 0, 4, 5], [0, 1, 4], [2, 3, 5]], [M("Sacakalti", "#d9d2c4"), cm, cm, cm, cm])
        sirtlar = [(pts[0], a), (pts[1], a), (pts[2], b), (pts[3], b), (a, b)]
    if sirt and DETAY:
        mm = M("Mahya", koyu(HEX[cm], 0.75))
        for p, q in sirtlar:
            cubuk(g, add(p, (0, 0, 0.06)), add(q, (0, 0, 0.06)), 0.18, mm, 0.12)
    return zt


def mansard(g, x0, x1, y0, y1, z, h1, ic, cm_alt, cm_ust, egim2=0.3):
    """Mansard: dik alt etek + basık üst kırma çatı. Alt eteğin iç dikdörtgenini döndürür."""
    a = [(x0 - 0.15, y0 - 0.15, z), (x1 + 0.15, y0 - 0.15, z), (x1 + 0.15, y1 + 0.15, z), (x0 - 0.15, y1 + 0.15, z)]
    b = [(x0 + ic, y0 + ic, z + h1), (x1 - ic, y0 + ic, z + h1), (x1 - ic, y1 - ic, z + h1), (x0 + ic, y1 - ic, z + h1)]
    pts = a + b
    kati(g, pts, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]],
         [None, None, cm_alt, cm_alt, cm_alt, cm_alt])
    kirma_cati(g, x0 + ic, x1 - ic, y0 + ic, y1 - ic, z + h1, egim2, cm_ust, sacak=0.12)
    if DETAY:
        c = M("Korniz", "#f1ece2")
        kutu(g, x0 - 0.25, x1 + 0.25, y0 - 0.25, y1 + 0.25, z - 0.05, z + 0.1, c)
    return (x0 + ic, x1 - ic, y0 + ic, y1 - ic)


def duz_cati(g, x0, x1, y0, y1, z, ph, dm, cm, kal=0.25, kenar=None):
    """Düz çatı: su yalıtımı yüzeyi + parapet + harpuşta."""
    kutu(g, x0 + kal, x1 - kal, y0 + kal, y1 - kal, z, z + 0.04, cm)
    if ph <= 0:
        return
    for a, b, c_, d in ((x0, x1, y0, y0 + kal), (x0, x1, y1 - kal, y1), (x0, x0 + kal, y0 + kal, y1 - kal), (x1 - kal, x1, y0 + kal, y1 - kal)):
        kutu(g, a, b, c_, d, z, z + ph, dm)
    if DETAY:
        hm = kenar or M("Harpusta", "#e9e5dc")
        kutu(g, x0 - 0.05, x1 + 0.05, y0 - 0.05, y0 + kal + 0.03, z + ph, z + ph + 0.07, hm)
        kutu(g, x0 - 0.05, x1 + 0.05, y1 - kal - 0.03, y1 + 0.05, z + ph, z + ph + 0.07, hm)
        kutu(g, x0 - 0.05, x0 + kal + 0.03, y0 + kal + 0.03, y1 - kal - 0.03, z + ph, z + ph + 0.07, hm)
        kutu(g, x1 - kal - 0.03, x1 + 0.05, y0 + kal + 0.03, y1 - kal - 0.03, z + ph, z + ph + 0.07, hm)


def cati_penceresi(g, xc, w, y_on, ym, zr, cm, dm, rnd, st):
    """Beşik çatının ön eteğinde çatı penceresi (mahya X boyunca)."""
    zf = zr(y_on)
    h = 1.25
    kutu(g, xc - w / 2, xc + w / 2, y_on, ym, zf - 0.35, zf + h, dm, atla=("zn", "yp"))
    prizma(g, [(xc - w / 2 - 0.15, y_on - 0.15, zf + h), (xc + w / 2 + 0.15, y_on - 0.15, zf + h), (xc, y_on - 0.15, zf + h + w * 0.45)],
           (0, ym - y_on + 0.15, 0), cm)
    c = C((xc - w / 2, y_on), (1, 0), (0, -1), w)
    pencere(g, c, w / 2, zf + 0.1, w * 0.55, 0.85, dict(st, panjur=None, lento=None, saksi=0, denizlik=None), rnd)


def baca(g, x, y, z0, z1, mat, w=0.55, d=0.45):
    kutu(g, x - w / 2, x + w / 2, y - d / 2, y + d / 2, z0, z1, mat)
    if DETAY:
        kutu(g, x - w / 2 - 0.06, x + w / 2 + 0.06, y - d / 2 - 0.06, y + d / 2 + 0.06, z1, z1 + 0.1, M("Tas", SABIT["tas_koyu"]))
        kutu(g, x - w / 2 + 0.08, x + w / 2 - 0.08, y - d / 2 + 0.08, y + d / 2 - 0.08, z1 + 0.1, z1 + 0.16, M("Demir", "#3a3a3a"))


def gunes_paneli(g, x0, x1, y0, y1, zr_y, egim_ust=0.04):
    """Çatı eteğine (ön, mahya X) yatık güneş paneli; zr_y: y → çatı yüksekliği."""
    if not DETAY:
        return
    m = M("Panel", "#2c3e5c")
    p = [(x0, y0, zr_y(y0) + 0.18), (x1, y0, zr_y(y0) + 0.18), (x1, y1, zr_y(y1) + 0.18), (x0, y1, zr_y(y1) + 0.18)]
    q = [add(a, (0, 0, -0.06)) for a in p]
    kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]],
         [m, None, M("Metal", "#c7c9cc"), M("Metal", "#c7c9cc"), M("Metal", "#c7c9cc"), M("Metal", "#c7c9cc")])


# ================================== ORTAK CEPHE DÜZENLERİ ==================================

def eksenler(L, adim_min=1.55, kenar=0.0):
    n = max(1, int((L - 2 * kenar) / adim_min))
    a = (L - 2 * kenar) / n
    return [kenar + a * (i + 0.5) for i in range(n)], a


def rustik(g, c, z0, z1, mat, aralik=0.42, u0=0.0, u1=None):
    """Zemin katta yatay taş derzleri."""
    if not DETAY:
        return
    u1 = c.L if u1 is None else u1
    z = z0 + aralik
    while z < z1 - 0.1:
        c.kutu(g, u0, u1, 0, 0.035, z - 0.03, z + 0.03, mat)
        z += aralik


def klasik_cephe(g, c, rnd, z0, kat, kh, st, zemin="konut", us=None, balkon_kat=None, pilaster=True,
                 lento_sira=("alinlik", "duz", "kilit", "duz"), pw=0.95, ph=1.65, kapi_i=None, korniz_d=0.42):
    """Eski şehir cephesi: zemin kat (dükkân, konut ya da kapı altı), kuşak silmesi, pencere eksenleri, plasterler, ana korniş."""
    L = c.L
    if us is None:
        us, adim = eksenler(L, 1.6)
    else:
        adim = (us[1] - us[0]) if len(us) > 1 else L
    trim = st["trim"]
    if zemin == "dukkan":
        vitrin(g, c, 0.3, L - 0.3, z0, kh - 1.05, st, rnd)
        if rnd.random() < 0.6:
            r1 = rnd.choice(("#c0392b", "#2e7d5b", "#2f4f8a", "#d98a1f", "#6b4a8a"))
            tente(g, c, 0.25, L - 0.25, z0 + kh - 0.35, 1.3, r1, "#f4efe4")
    elif zemin == "kapi":
        rustik(g, c, z0, z0 + kh - 0.2, M("Siva", koyu(st["duvar"], 0.9)))
        ku = L / 2
        kw = 2.6
        dk = M("Kapi", "#3a2a22")
        c.kutu(g, ku - kw / 2, ku + kw / 2, 0, 0.02, z0, z0 + kh - 0.5 - kw / 2 + 0.2, dk)
        c.yarim_disk(g, ku, z0 + kh - 0.5 - kw / 2 + 0.2, kw / 2, 0, 0.02, dk, n=8)
        if DETAY:
            for s in (-1, 1):
                c.kutu(g, ku + s * (kw / 2 + 0.15) - 0.15, ku + s * (kw / 2 + 0.15) + 0.15, 0, 0.12, z0, z0 + kh - 0.5 - kw / 2 + 0.2, trim)
            c.kutu(g, ku - 0.22, ku + 0.22, 0, 0.14, z0 + kh - 0.65, z0 + kh - 0.2, trim)
            for i in range(9):
                a0, a1 = math.pi * i / 9, math.pi * (i + 1) / 9
                R = kw / 2 + 0.12
                zc = z0 + kh - 0.5 - kw / 2 + 0.2
                cubuk(g, c.p(ku + R * math.cos(a0), 0.06, zc + R * math.sin(a0)), c.p(ku + R * math.cos(a1), 0.06, zc + R * math.sin(a1)), 0.22, trim, 0.12)
        for u in us:
            if abs(u - ku) < kw / 2 + 0.6:
                continue
            pencere(g, c, u, z0 + 0.95, pw, ph * 0.9, dict(st, lento=None, panjur=None), rnd)
    else:
        rustik(g, c, z0, z0 + kh - 0.2, M("Siva", koyu(st["duvar"], 0.9)))
        ki = kapi_i if kapi_i is not None else rnd.randrange(len(us))
        for i, u in enumerate(us):
            if i == ki:
                kapi(g, c, u, z0 + 0.34, 1.15, 2.45, st, rnd, z0=z0)
                if DETAY:
                    c.yarim_disk(g, u, z0 + 2.95, 0.6, 0, 0.05, cam(rnd), n=6)
            else:
                pencere(g, c, u, z0 + 0.9, pw, ph, dict(st, tip=st.get("zemin_tip", "kemer"), lento="kilit", panjur=None), rnd)
    korniz(g, c, z0 + kh - 0.12, 0.24, 0.14, trim, profil=False)
    for k in range(1, kat):
        zb = z0 + k * kh + 0.8
        lt = lento_sira[(k - 1) % len(lento_sira)]
        bk = balkon_kat == k
        for i, u in enumerate(us):
            orta = abs(u - L / 2) < adim * 0.6
            if bk and orta:
                balkon(g, c, u - adim * 0.45, u + adim * 0.45, zb - 0.8 + 0.02, 0.85, st, rnd)
                kapi(g, c, u, zb - 0.78, 1.0, 2.3, dict(st, kapi=cam(rnd)), rnd, z0=zb - 0.78)
                c.kutu(g, u - 0.65, u + 0.65, 0, 0.1, zb + 1.6, zb + 1.78, trim)
            else:
                pencere(g, c, u, zb, pw, ph, dict(st, lento=lt), rnd)
        if DETAY and k < kat - 1:
            c.kutu(g, 0, L, 0, 0.06, z0 + (k + 1) * kh - 0.05, z0 + (k + 1) * kh + 0.08, trim)
    if pilaster and DETAY and kat > 1:
        for i in range(len(us) + 1):
            u = 0.12 if i == 0 else (L - 0.12 if i == len(us) else (us[i - 1] + us[i]) / 2)
            c.kutu(g, u - 0.16, u + 0.16, 0, 0.09, z0 + kh, z0 + kat * kh - 0.3, trim)
    korniz(g, c, z0 + kat * kh - 0.42, 0.42, korniz_d, trim, tasma=0.05)
    return us


def yan_cephe(g, c, rnd, z0, kat, kh, st, olas=0.5, w=0.85, h=1.4):
    """Yan/arka cephe: sade pencereler (bazı katlar boş)."""
    if c.L < 2.0:
        return
    for k in range(kat):
        if rnd.random() > olas:
            continue
        pencere_dizisi(g, c, z0 + k * kh + 0.9, w, h, dict(st, lento=None, panjur=None, saksi=0), rnd, kenar=0.9, aralik=2.4)


def avlu(g, x0, x1, y0, y1, rnd, agac_olas=0.8):
    """Arka avlu: taş döşeme, ağaç, çöp konteyneri, bisiklet."""
    yama(g, x0, x1, y0, y1, M("TasAvlu", "#cfc7b8"))
    if DETAY and rnd.random() < agac_olas and (x1 - x0) > 2.5 and (y1 - y0) > 2.5:
        agac(g, rnd.uniform(x0 + 1.2, x1 - 1.2), rnd.uniform(y0 + 1.2, y1 - 1.2), ZL, rnd, boy=rnd.uniform(4.5, 6.5), tip="yuvarlak")
    if DETAY and rnd.random() < 0.6 and (x1 - x0) > 1.5:
        x = x0 + 0.5
        y = y1 - 0.6
        kutu(g, x, x + 1.1, y - 0.5, y + 0.5, ZL, ZL + 1.1, M("Konteyner", rnd.choice(("#3f6b45", "#555a60", "#2f4f7a"))))


# ================================== ESKİ ŞEHİR ==================================

ESKI_RENK = ["#f2c75c", "#e8b48a", "#dba79b", "#bcd4c0", "#c4d5e6", "#efe0b8", "#e6c79a", "#d3b8cf", "#f3d7a7", "#cfe0b0", "#e9cdb5"]
TRIM = ["#f6f1e5", "#efe6d2", "#fbf7ee"]


def eski_st(rnd, onceki=None):
    renk = rnd.choice([r for r in ESKI_RENK if r != onceki])
    return dict(duvar=renk, dm=M("Siva", renk), trim=M("Korniz", rnd.choice(TRIM)), cerceve=M("Cerceve", rnd.choice(("#f4f0e6", "#5a3a2a", "#2f4a3a"))),
                denizlik=M("Tas", SABIT["tas"]), panjur=(M("Panjur", rnd.choice(("#3e6b4f", "#6b4a32", "#4f6b8a"))) if rnd.random() < 0.35 else None),
                saksi=0.35, cam="#a9c9de", korkuluk="demir", zemin_tip=rnd.choice(("kemer", "klasik")))


def eski_barok_sira(rnd):
    """2–3 bitişik barok/eklektik sıra ev: farklı renk, kat ve çatı; arkada taş avlu."""
    g = G("eski_barok_sira")
    zemin(g, M("TasAvlu", "#cfc7b8"))
    n = rnd.choice((2, 3, 3))
    gen = [rnd.uniform(0.8, 1.2) for _ in range(n)]
    s = sum(gen)
    gen = [2 * LOT * w / s for w in gen]
    x = -LOT
    y0 = -LOT
    onceki = None
    for i, w in enumerate(gen):
        st = eski_st(rnd, onceki)
        onceki = st["duvar"]
        kat = rnd.choice((2, 3, 3, 4))
        kh = 3.4
        D = rnd.uniform(8.0, 10.0)
        H = kat * kh
        x0, x1 = x, x + w
        f = govde(g, x0, x1, y0, y0 + D, ZL, ZL + H, st["dm"])
        zemin_tip = rnd.choice(("dukkan", "konut", "dukkan"))
        us, _ = eksenler(w, 1.55, 0.35)
        klasik_cephe(g, f["on"], rnd, ZL, kat, kh, st, zemin=zemin_tip, us=us, balkon_kat=(1 if rnd.random() < 0.4 else None))
        yan_cephe(g, f["arka"], rnd, ZL, kat, kh, st, 0.8)
        if i == 0:
            yan_cephe(g, f["sol"], rnd, ZL, kat, kh, st, 0.35)
        if i == n - 1:
            yan_cephe(g, f["sag"], rnd, ZL, kat, kh, st, 0.35)
        zt = ZL + H
        cati = rnd.choice(("besik", "besik", "mansard", "kirma"))
        cm = M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit2"], SABIT["kiremit3"]))) if cati != "mansard" else M("Arduvaz", SABIT["arduvaz"])
        if cati == "besik":
            zr = besik_cati(g, x0, x1, y0, y0 + D, zt, rnd.uniform(0.7, 0.9), cm, st["dm"], sacak=0.3, yan=0.05)
            if w > 4.0 and rnd.random() < 0.8:
                for xc in ([x0 + w / 2] if w < 5.5 else [x0 + w * 0.3, x0 + w * 0.7]):
                    cati_penceresi(g, xc, 1.2, y0 + D * 0.18, y0 + D / 2, zr, cm, st["dm"], rnd, st)
            baca(g, x0 + w * rnd.uniform(0.2, 0.8), y0 + D * 0.62, zt, zr(y0 + D * 0.62) + 1.0, M("Tugla", "#a65a44"))
        elif cati == "mansard":
            ic = mansard(g, x0, x1, y0, y0 + D, zt, 2.4, 0.9, cm, M("Cinko", SABIT["cinko"]))
            if DETAY:
                for xc in eksenler(w, 1.8, 0.6)[0]:
                    xc += x0
                    kutu(g, xc - 0.5, xc + 0.5, y0 + 0.25, y0 + 1.2, zt + 0.3, zt + 1.9, st["trim"], atla=("zn", "yp"))
                    c = C((xc - 0.5, y0 + 0.25), (1, 0), (0, -1), 1.0)
                    pencere(g, c, 0.5, zt + 0.5, 0.6, 1.1, dict(st, panjur=None, lento=None, saksi=0, denizlik=None), rnd)
                    c.yarim_disk(g, 0.5, zt + 1.9, 0.5, 0, 0.95, cm, n=6)
            baca(g, x0 + w * 0.3, y0 + D * 0.6, zt + 2.0, zt + 3.6, M("Tugla", "#a65a44"))
        else:
            kirma_cati(g, x0, x1, y0, y0 + D, zt, rnd.uniform(0.6, 0.8), cm, sacak=0.3)
            baca(g, x0 + w * 0.5, y0 + D * 0.5, zt, zt + 2.6, M("Tugla", "#a65a44"))
        # ön alınlık (barok tepe)
        if cati != "mansard" and rnd.random() < 0.45 and w > 4:
            c = f["on"]
            zb = zt
            bw = min(3.2, w * 0.55)
            c.kutu(g, w / 2 - bw / 2, w / 2 + bw / 2, 0.0, 0.35, zb, zb + 1.3, st["dm"], arka=True)
            c.yarim_disk(g, w / 2, zb + 1.3, bw / 2, 0.0, 0.35, st["dm"], n=10)
            if DETAY:
                c.kutu(g, w / 2 - bw / 2 - 0.1, w / 2 + bw / 2 + 0.1, 0.35, 0.45, zb + 1.25, zb + 1.38, st["trim"])
                for s in (-1, 1):
                    top(g, c.p(w / 2 + s * (bw / 2 + 0.25), 0.2, zb + 0.35), 0.3, 0.3, 0.3, st["trim"])
                top(g, c.p(w / 2, 0.25, zb + 1.3 + bw / 2 + 0.2), 0.18, 0.18, 0.25, st["trim"])
                silindir(g, c.p(w / 2, 0.35, zb + 1.05), c.p(w / 2, 0.4, zb + 1.05), 0.42, 0.42, 12, cam(rnd))
                silindir(g, c.p(w / 2, 0.35, zb + 1.05), c.p(w / 2, 0.42, zb + 1.05), 0.52, 0.52, 12, st["trim"], kapak=(False, False))
        x = x1
    avlu(g, -LOT, LOT, y0 + 10.0, LOT, rnd)
    return g


def eski_kapialti(rnd):
    """Kapı altlı (kapualj) tek katlı ya da iki katlı eklektik konak: ortada kemerli araba kapısı, üstünde balkon."""
    g = G("eski_kapialti")
    zemin(g, M("TasAvlu", "#cfc7b8"))
    st = eski_st(rnd)
    kat = rnd.choice((2, 2, 3))
    kh = 3.6
    D = rnd.uniform(8.5, 10.5)
    y0 = -LOT
    f = govde(g, -LOT, LOT, y0, y0 + D, ZL, ZL + kat * kh, st["dm"])
    us, _ = eksenler(2 * LOT, 1.6, 0.6)
    klasik_cephe(g, f["on"], rnd, ZL, kat, kh, st, zemin="kapi", us=us, balkon_kat=1, pw=1.0, ph=1.8)
    yan_cephe(g, f["arka"], rnd, ZL, kat, kh, st, 0.9)
    for k in ("sol", "sag"):
        yan_cephe(g, f[k], rnd, ZL, kat, kh, st, 0.4)
    zt = ZL + kat * kh
    if rnd.random() < 0.5:
        ic = mansard(g, -LOT, LOT, y0, y0 + D, zt, 2.3, 0.85, M("Arduvaz", SABIT["arduvaz"]), M("Cinko", SABIT["cinko"]))
        if DETAY:
            for xc in eksenler(2 * LOT, 2.6, 1.2)[0]:
                xc -= LOT
                kutu(g, xc - 0.5, xc + 0.5, y0 + 0.25, y0 + 1.2, zt + 0.3, zt + 1.9, st["trim"], atla=("zn", "yp"))
                c = C((xc - 0.5, y0 + 0.25), (1, 0), (0, -1), 1.0)
                pencere(g, c, 0.5, zt + 0.5, 0.6, 1.1, dict(st, panjur=None, lento=None, saksi=0, denizlik=None), rnd)
                c.prizma_uc(g, 0.0, 1.0, 0.0, 0.95, zt + 1.9, 0.45, M("Arduvaz", SABIT["arduvaz"]))
    else:
        cm = M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit2"])))
        kirma_cati(g, -LOT, LOT, y0, y0 + D, zt, 0.7, cm, sacak=0.35)
        if DETAY:
            # ortada tepe alınlığı
            c = f["on"]
            c.kutu(g, LOT - 2.2, LOT + 2.2, 0, 0.4, zt, zt + 1.1, st["dm"], arka=True)
            c.prizma_uc(g, LOT - 2.5, LOT + 2.5, 0, 0.5, zt + 1.1, 1.2, st["trim"])
    for xc in (-LOT + 2.5, LOT - 2.5):
        baca(g, xc, y0 + D * 0.55, zt, zt + 3.2, M("Tugla", "#a65a44"))
    # avlu: araba yolu, bahçe, ağaç
    yama(g, -1.3, 1.3, y0 + D, LOT, M("TasYol", "#bdb4a4"))
    yama(g, -LOT + 0.3, -1.6, y0 + D + 0.4, LOT - 0.3, M("Cim", SABIT["cim"]))
    if DETAY:
        agac(g, -LOT + 2.5, LOT - 2.2, ZL, rnd, boy=6.5)
        cicek_tarhi(g, 2.0, LOT - 0.5, LOT - 1.6, LOT - 0.4, rnd)
    return g


def eski_kuleli(rnd):
    """Köşesinde silindir kuleli (sivri bakır külahlı) eklektik bina."""
    g = G("eski_kuleli")
    zemin(g, M("TasAvlu", "#cfc7b8"))
    st = eski_st(rnd)
    kat = rnd.choice((3, 4))
    kh = 3.4
    D = 10.0
    y0 = -LOT
    sag = rnd.random() < 0.5
    x0, x1 = -LOT, LOT
    f = govde(g, x0, x1, y0, y0 + D, ZL, ZL + kat * kh, st["dm"])
    us, _ = eksenler(2 * LOT, 1.6, 0.4)
    kx = x1 - 1.6 if sag else x0 + 1.6
    us = [u for u in us if abs((u + x0) - kx) > 2.0]
    klasik_cephe(g, f["on"], rnd, ZL, kat, kh, st, zemin="dukkan" if rnd.random() < 0.6 else "konut", us=us, balkon_kat=2)
    yan_cephe(g, f["arka"], rnd, ZL, kat, kh, st, 0.8)
    yan_cephe(g, f["sag" if not sag else "sol"], rnd, ZL, kat, kh, st, 0.6)
    zt = ZL + kat * kh
    cm = M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit3"])))
    zr = besik_cati(g, x0, x1, y0, y0 + D, zt, 0.75, cm, st["dm"], sacak=0.3, yan=0.1)
    for xc in eksenler(2 * LOT, 3.0, 1.5)[0]:
        if abs(xc + x0 - kx) > 2.4:
            cati_penceresi(g, xc + x0, 1.1, y0 + D * 0.2, y0 + D / 2, zr, cm, st["dm"], rnd, st)
    # kule
    R = 1.75
    ky = y0 + 0.9
    n = 12
    dsil(g, kx, ky, ZL, zt + 2.6, R, n, st["dm"])
    if DETAY:
        for k in range(1, kat + 1):
            dsil(g, kx, ky, ZL + k * kh - 0.3, ZL + k * kh, R + 0.12, n, st["trim"])
    zp = zt + 2.6
    dsil(g, kx, ky, zp, zp + 0.35, R + 0.25, n, st["trim"])
    dsil(g, kx, ky, zp + 0.35, zp + 4.8, R + 0.15, n, M("Cati", SABIT["bakir"]), r1=0.0, kapak=(True, False))
    if DETAY:
        cubuk(g, (kx, ky, zp + 4.6), (kx, ky, zp + 5.8), 0.07, M("Pirinc", "#c9a24a"))
        top(g, (kx, ky, zp + 5.3), 0.16, 0.16, 0.16, M("Pirinc", "#c9a24a"))
    # kule pencereleri (dışa bakan üç yön)
    for k in range(1, kat + 1):
        for a in (-90, -45 if not sag else -135, 0 if sag else 180):
            if a in (0, 180) and False:
                continue
            ar = math.radians(a)
            nx, ny = math.cos(ar), math.sin(ar)
            o = (kx + nx * R, ky + ny * R)
            c = C((o[0] - (-ny) * 0.5, o[1] - nx * 0.5), (-ny, nx), (nx, ny), 1.0)
            pencere(g, c, 0.5, ZL + (k - 1) * kh + (1.0 if k > 1 else 0.9), 0.8, 1.6, dict(st, panjur=None, lento="duz", saksi=0), rnd)
        if k == kat:
            for a in range(0, 360, 45):
                ar = math.radians(a)
                nx, ny = math.cos(ar), math.sin(ar)
                o = (kx + nx * R, ky + ny * R)
                c = C((o[0] - (-ny) * 0.35, o[1] - nx * 0.35), (-ny, nx), (nx, ny), 0.7)
                pencere(g, c, 0.35, zt + 0.8, 0.5, 1.2, dict(st, panjur=None, lento=None, saksi=0, tip="kemer", denizlik=None), rnd)
    baca(g, (x0 + x1) / 2 + (-3 if sag else 3), y0 + 6.5, zt, zt + 3.4, M("Tugla", "#a65a44"))
    avlu(g, -LOT, LOT, y0 + D, LOT, rnd)
    return g


def eski_kasaba_evi(rnd):
    """Tek katlı kasaba evi: sokağa paralel uzun cephe, yüksek pencereler, yan araba kapısı, kırma çatı, arka bahçe."""
    g = G("eski_kasaba_evi")
    zemin(g, M("Cim", SABIT["cim2"]))
    st = eski_st(rnd)
    kh = 4.2
    y0 = -LOT
    W = rnd.uniform(10.0, 11.5)
    x0, x1 = -LOT, -LOT + W
    D = rnd.uniform(6.5, 8.0)
    f = govde(g, x0, x1, y0, y0 + D, ZL, ZL + kh, st["dm"])
    c = f["on"]
    rustik(g, c, ZL, ZL + 0.8, M("Siva", koyu(st["duvar"], 0.85)), aralik=0.2)
    c.kutu(g, 0, W, 0, 0.06, ZL, ZL + 0.75, M("Siva", koyu(st["duvar"], 0.85)))
    us, _ = eksenler(W, 2.3, 0.6)
    for u in us:
        pencere(g, c, u, ZL + 1.1, 1.05, 1.95, dict(st, lento=rnd.choice(("duz", "alinlik", "kemerli")), tip="klasik"), rnd)
        if DETAY:
            c.kutu(g, u - 0.75, u + 0.75, 0, 0.05, ZL + 0.85, ZL + 1.0, st["trim"])
    korniz(g, c, ZL + kh - 0.5, 0.5, 0.35, st["trim"])
    for k in ("sol", "arka"):
        yan_cephe(g, f[k], rnd, ZL, 1, kh, st, 1.0, w=0.9, h=1.6)
    kirma_cati(g, x0, x1, y0, y0 + D, ZL + kh, 0.75, M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit2"]))), sacak=0.35)
    baca(g, x0 + W * 0.35, y0 + D * 0.5, ZL + kh, ZL + kh + 2.8, M("Tugla", "#a65a44"))
    # yan duvar ve araba kapısı
    dx0 = x1
    kutu(g, dx0, LOT, y0, y0 + 0.4, ZL, ZL + 2.8, st["dm"])
    cc = C((dx0, y0), (1, 0), (0, -1), LOT - dx0)
    km = M("Kapi", rnd.choice(("#5a3a2a", "#2f4a3a", "#6b4a32")))
    cc.kutu(g, 0.3, LOT - dx0 - 0.3, 0, 0.05, ZL, ZL + 2.5, km)
    if DETAY:
        for i in range(int((LOT - dx0 - 0.6) / 0.16)):
            uu = 0.38 + i * 0.16
            cc.kutu(g, uu, uu + 0.06, 0.05, 0.08, ZL + 0.1, ZL + 2.4, M("Kapi", koyu(HEX[km], 0.8)))
        cc.kutu(g, 0, LOT - dx0, 0, 0.12, ZL + 2.8, ZL + 3.0, st["trim"])
    # arka bahçe
    yama(g, dx0 + 0.3, LOT - 0.3, y0 + 0.6, LOT - 0.3, M("TasYol", "#c2b8a6"))
    if DETAY:
        agac(g, -2.0, LOT - 2.0, ZL, rnd, boy=6.0)
        agac(g, 3.0, LOT - 1.8, ZL, rnd, boy=5.0, tip="yuvarlak")
        cicek_tarhi(g, x0 + 0.6, x0 + 4.0, y0 + D + 0.6, y0 + D + 1.6, rnd)
        cit_parmak(g, (-LOT, LOT - 0.1, 0), (LOT, LOT - 0.1, 0), ZL, 1.3, "#5a5e63")
    return g


def eski_kilise(rnd):
    """Barok kilise: tek kuleli cephe, soğan kubbe, nefli gövde."""
    g = G("eski_kilise")
    zemin(g, M("TasAvlu", "#d2cabb"))
    renk = rnd.choice(("#f2c75c", "#efe0b8", "#f6f1e5", "#e8d2a8"))
    dm = M("Siva", renk)
    trim = M("Korniz", "#fbf7ee")
    st = dict(duvar=renk, dm=dm, trim=trim, cerceve=M("Cerceve", "#5a3a2a"), denizlik=M("Tas", SABIT["tas"]), cam="#8fb6cf", saksi=0)
    # nef
    nx0, nx1 = -3.6, 3.6
    ny0, ny1 = -3.2, 6.6
    H = 9.5
    f = govde(g, nx0, nx1, ny0, ny1, ZL, ZL + H, dm)
    for k in ("sol", "sag"):
        for u in eksenler(f[k].L, 2.4, 0.8)[0]:
            pencere(g, f[k], u, ZL + 3.0, 1.3, 4.2, dict(st, tip="kemer", lento=None, panjur=None), rnd)
            if DETAY:
                f[k].kutu(g, u + 1.0, u + 1.45, 0, 0.35, ZL, ZL + H - 0.5, trim)
    korniz(g, f["sol"], ZL + H - 0.6, 0.6, 0.4, trim)
    korniz(g, f["sag"], ZL + H - 0.6, 0.6, 0.4, trim)
    besik_cati_y(g, nx0, nx1, ny0, ny1, ZL + H, 0.95, M("Kiremit", "#8f3f2c"), dm, sacak=0.4, yan=0.2)
    # apsis
    dsil(g, 0, ny1, ZL, ZL + H - 1.0, 3.0, 12, dm)
    dsil(g, 0, ny1, ZL + H - 1.0, ZL + H + 1.6, 3.15, 12, M("Kiremit", "#8f3f2c"), r1=0.5)
    # kule (ön)
    tx0, tx1, ty0, ty1 = -2.4, 2.4, -6.6, -2.4
    TH = 17.0
    f2 = govde(g, tx0, tx1, ty0, ty1, ZL, ZL + TH, dm)
    c = f2["on"]
    kapi(g, c, 2.4, ZL + 0.5, 1.8, 3.4, dict(st, kapi=M("Kapi", "#4a2c1e")), rnd)
    if DETAY:
        c.yarim_disk(g, 2.4, ZL + 4.1, 1.05, 0, 0.12, trim, n=10)
        for u in (0.35, 4.45):
            c.kutu(g, u - 0.3, u + 0.3, 0, 0.3, ZL, ZL + TH - 0.4, trim)
        for z in (ZL + 6.0, ZL + 11.5):
            korniz(g, c, z, 0.4, 0.3, trim)
    pencere(g, c, 2.4, ZL + 7.2, 1.1, 2.8, dict(st, tip="kemer", lento="duz", panjur=None), rnd)
    for k in ("on", "sol", "sag", "arka"):
        cc = f2[k]
        pencere(g, cc, cc.L / 2, ZL + 12.6, 1.0, 2.6, dict(st, tip="kemer", lento=None, panjur=None, cam="#3c4a52"), rnd)
        if DETAY:
            silindir(g, cc.p(cc.L / 2, 0.0, ZL + TH - 1.4), cc.p(cc.L / 2, 0.15, ZL + TH - 1.4), 0.75, 0.75, 14, M("Saat", "#f4efe2"))
            cc.kutu(g, cc.L / 2 - 0.04, cc.L / 2 + 0.04, 0.15, 0.2, ZL + TH - 1.45, ZL + TH - 0.85, M("Demir", "#202020"))
    korniz(g, f2["on"], ZL + TH - 0.4, 0.45, 0.4, trim, tasma=0.4)
    korniz(g, f2["arka"], ZL + TH - 0.4, 0.45, 0.4, trim, tasma=0.4)
    korniz(g, f2["sol"], ZL + TH - 0.4, 0.45, 0.4, trim, tasma=0.4)
    korniz(g, f2["sag"], ZL + TH - 0.4, 0.45, 0.4, trim, tasma=0.4)
    # soğan kubbe
    cx, cy, z = 0.0, (ty0 + ty1) / 2, ZL + TH + 0.05
    bk = M("Cati", "#5e9a80")
    dsil(g, cx, cy, z, z + 1.2, 2.0, 12, bk, r1=1.4)
    dsil(g, cx, cy, z + 1.2, z + 2.0, 1.0, 10, dm, kapak=(True, True))
    top(g, (cx, cy, z + 3.0), 1.35, 1.35, 1.25, bk)
    dsil(g, cx, cy, z + 4.0, z + 5.2, 0.6, 10, bk, r1=0.0, kapak=(True, False))
    if DETAY:
        m = M("Pirinc", "#d8b24a")
        cubuk(g, (cx, cy, z + 5.0), (cx, cy, z + 6.6), 0.08, m)
        cubuk(g, (cx - 0.45, cy, z + 6.1), (cx + 0.45, cy, z + 6.1), 0.08, m)
        top(g, (cx, cy, z + 5.3), 0.2, 0.2, 0.2, m)
    # bahçe ağaçları
    if DETAY:
        for x, y in ((-5.5, -4.5), (5.5, -4.5), (-5.6, 4.0), (5.6, 4.0)):
            agac(g, x, y, ZL, rnd, boy=rnd.uniform(5.5, 7.5), tip=rnd.choice(("yuvarlak", "kavak")))
        cit_parmak(g, (-LOT, -LOT + 0.1, 0), (-2.0, -LOT + 0.1, 0), ZL, 1.1, "#2b2d31")
        cit_parmak(g, (2.0, -LOT + 0.1, 0), (LOT, -LOT + 0.1, 0), ZL, 1.1, "#2b2d31")
    return g


# ================================== ÇARŞI (KARMA) ==================================

KARMA_RENK = ["#e9d8b8", "#d8e2e4", "#f0c9a0", "#c9dcc6", "#e6c0b0", "#f4e7c9", "#cfd3dc", "#e2d2e0", "#f1dc9a", "#b9d3d8"]
VURGU = ["#d9822b", "#2f7d8c", "#c0473a", "#5a8f4a", "#3d5a8a", "#e0b23a", "#8a4a6b"]


def karma_st(rnd):
    renk = rnd.choice(KARMA_RENK)
    vurgu = rnd.choice(VURGU)
    return dict(duvar=renk, dm=M("Siva", renk), vurgu=vurgu, vm=M("Siva", vurgu), trim=M("Korniz", "#f3efe6"),
                cerceve=M("Cerceve", rnd.choice(("#f4f4f2", "#3a3d42", "#6b6f75"))), denizlik=M("Tas", "#d6d2c8"),
                panjur=None, saksi=0.25, tip="kare", korkuluk=rnd.choice(("demir", "cam", "dolu")),
                balkon_mat=M("Beton", "#ece8df"))


def cati_ustu(g, x0, x1, y0, y1, z, rnd, n_klima=3, asansor=True):
    """Düz çatı üstü: asansör kulübesi, klimalar, çanak anten, su deposu."""
    if asansor:
        ax = rnd.uniform(x0 + 2, x1 - 4)
        ay = rnd.uniform(y0 + 2, y1 - 3.5)
        kutu(g, ax, ax + 2.4, ay, ay + 2.0, z, z + 2.4, M("Beton", "#d9d4ca"))
        if DETAY:
            kutu(g, ax - 0.1, ax + 2.5, ay - 0.1, ay + 2.1, z + 2.4, z + 2.55, M("Harpusta", "#e9e5dc"))
            kutu(g, ax + 0.6, ax + 1.5, ay - 0.04, ay, z, z + 2.0, M("Kapi", "#5a5e63"))
    if not DETAY:
        return
    for _ in range(n_klima):
        x, y = rnd.uniform(x0 + 1, x1 - 1.5), rnd.uniform(y0 + 1, y1 - 1.2)
        kutu(g, x, x + 1.0, y, y + 0.7, z, z + 0.6, M("Metal", "#d9dcdf"))
        dsil(g, x + 0.5, y + 0.35, z + 0.6, z + 0.62, 0.28, 10, M("Metal", "#6f757b"))
    for _ in range(rnd.randint(1, 3)):
        canak(g, rnd.uniform(x0 + 1, x1 - 1), rnd.uniform(y0 + 0.8, y0 + 3), z, (0, -1))


def modern_cephe(g, c, rnd, z0, kat, kh, st, us=None, balkon_olas=0.5, pw=1.3, ph=1.45, balkon_tip=None, kenar=0.6):
    """Modern apartman cephesi (zemin hariç): pencereler, balkonlar, renkli dikey şerit."""
    L = c.L
    if us is None:
        us, adim = eksenler(L, 2.3, kenar)
    else:
        adim = us[1] - us[0] if len(us) > 1 else L
    balkonlu = [rnd.random() < balkon_olas for _ in us]
    serit = rnd.random() < 0.5 and len(us) >= 3
    si = rnd.randrange(len(us)) if serit else -1
    for k in range(1, kat):
        zb = z0 + k * kh + 0.85
        for i, u in enumerate(us):
            if i == si:
                c.kutu(g, u - adim * 0.48, u + adim * 0.48, 0, 0.06, z0 + k * kh, z0 + (k + 1) * kh, st["vm"])
            if balkonlu[i]:
                bw = min(adim * 0.9, 3.2)
                balkon(g, c, u - bw / 2, u + bw / 2, z0 + k * kh + 0.02, 1.1, st, rnd, korkuluk=balkon_tip)
                kapi(g, c, u - bw * 0.22, z0 + k * kh + 0.03, 0.85, 2.2, dict(st, kapi=cam(rnd)), rnd, z0=z0 + k * kh)
                pencere(g, c, u + bw * 0.2, zb, min(1.0, bw * 0.35), ph, dict(st, lento=None, panjur=None), rnd)
            else:
                pencere(g, c, u, zb, pw, ph, dict(st, lento=None), rnd)
    return us


def dukkan_zemin(g, c, rnd, z0, kh, st, n=None, giris=True):
    """Zemin katta 1–3 dükkân (her biri ayrı tabela/tente) ve apartman girişi."""
    L = c.L
    n = n or rnd.choice((1, 2, 2, 3))
    gw = 1.8 if giris else 0.0
    gu = rnd.choice((0.0, L - gw)) if giris else None
    a0 = gw if gu == 0.0 else 0.0
    a1 = L - gw if gu is not None and gu > 0 else L
    w = (a1 - a0) / n
    for i in range(n):
        u0 = a0 + i * w + 0.15
        u1 = a0 + (i + 1) * w - 0.15
        sti = dict(st, tabela=M("Tabela", rnd.choice(("#2f5d50", "#7a2f2f", "#2f3f6b", "#c46a2b", "#3a3a3a", "#5a2f5a", "#1f6f8b", "#a33a3a"))))
        vitrin(g, c, u0, u1, z0, kh - 1.1, sti, rnd)
        if rnd.random() < 0.45:
            tente(g, c, u0, u1, z0 + kh - 0.45, 1.2, rnd.choice(("#c0392b", "#2e7d5b", "#2f4f8a", "#d98a1f", "#6b4a8a", "#3a3a3a")), "#f4efe4")
        elif rnd.random() < 0.4:
            bayrak_tabela(g, c, u1 - 0.3, z0 + kh + 0.3, rnd, ikon=rnd.choice(("hac", "kare", None)))
    if gu is not None:
        kapi(g, c, gu + gw / 2, z0 + 0.15, 1.2, 2.4, dict(st, kapi=cam(rnd)), rnd, z0=z0, sacak=True)
        if DETAY:
            c.kutu(g, gu + gw / 2 + 0.75, gu + gw / 2 + 0.95, 0, 0.06, z0 + 1.1, z0 + 1.6, M("Metal", "#c7c9cc"))
    c.kutu(g, 0, L, 0, 0.18, z0 + kh - 0.25, z0 + kh, st["trim"])


def karma_dukkanli_blok(rnd):
    """4–5 katlı dükkânlı apartman: zemin dükkânlar, üstte balkonlu daireler, düz çatı."""
    g = G("karma_dukkanli_blok")
    zemin(g, M("TasAvlu", "#cfc7b8"))
    st = karma_st(rnd)
    kat = rnd.choice((4, 4, 5))
    kh = 3.05
    D = rnd.uniform(10.0, 11.5)
    y0 = -LOT
    H = kat * kh
    f = govde(g, -LOT, LOT, y0, y0 + D, ZL, ZL + H, st["dm"])
    dukkan_zemin(g, f["on"], rnd, ZL, kh + 0.6, st)
    modern_cephe(g, f["on"], rnd, ZL + 0.6, kat, kh, st, balkon_olas=rnd.uniform(0.3, 0.7))
    for k in ("sol", "sag"):
        yan_cephe(g, f[k], rnd, ZL + 0.6, kat, kh, st, 0.3)
    yan_cephe(g, f["arka"], rnd, ZL + 0.6, kat, kh, st, 0.95, w=1.2)
    if rnd.random() < 0.5:
        duz_cati(g, -LOT, LOT, y0, y0 + D, ZL + H + 0.6, 0.8, st["dm"], M("DuzCati", "#8f8a84"))
        cati_ustu(g, -LOT, LOT, y0, y0 + D, ZL + H + 0.6, rnd)
    else:
        kirma_cati(g, -LOT, LOT, y0, y0 + D, ZL + H + 0.6, 0.36, M("Kiremit", rnd.choice((SABIT["kiremit"], "#7a4a3a", "#5d6066"))), sacak=0.5)
        if DETAY:
            for _ in range(2):
                canak(g, rnd.uniform(-5, 5), y0 + 1.5, ZL + H + 1.3, (0, -1))
    avlu(g, -LOT, LOT, y0 + D, LOT, rnd)
    return g


def karma_kose_kafe(rnd):
    """Önü teraslı kafe, üstünde iki-üç kat daire; terasta masalar, şemsiyeler, saksılar."""
    g = G("karma_kose_kafe")
    zemin(g, M("TasAvlu", "#d6cdbc"))
    st = karma_st(rnd)
    kat = rnd.choice((3, 3, 4))
    kh = 3.1
    y0 = -LOT + 3.4
    D = 9.5
    H = kat * kh
    f = govde(g, -LOT, LOT, y0, y0 + D, ZL, ZL + H, st["dm"])
    c = f["on"]
    vitrin(g, c, 0.4, c.L - 0.4, ZL, kh - 0.9, dict(st, tabela=M("Tabela", rnd.choice(("#3a2a22", "#2f5d50", "#1f3a5a")))), rnd)
    tente(g, c, 0.3, c.L - 0.3, ZL + kh - 0.25, 2.2, rnd.choice(("#2e5d4b", "#7a2f2f", "#d98a1f", "#2f4f8a")), "#f4efe4", dusus=0.45)
    modern_cephe(g, c, rnd, ZL, kat, kh, dict(st, tip=rnd.choice(("kare", "klasik"))), balkon_olas=0.35, balkon_tip="demir")
    for k in ("sol", "sag", "arka"):
        yan_cephe(g, f[k], rnd, ZL, kat, kh, st, 0.5)
    korniz(g, c, ZL + H - 0.35, 0.35, 0.3, st["trim"])
    kirma_cati(g, -LOT, LOT, y0, y0 + D, ZL + H, 0.6, M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit3"]))), sacak=0.35)
    baca(g, -3.5, y0 + 5, ZL + H, ZL + H + 3.0, M("Tugla", "#a65a44"))
    # teras
    yama(g, -LOT + 0.3, LOT - 0.3, -LOT + 0.2, y0 - 0.1, M("Deck", "#b58a5e"), k=0.06)
    if DETAY:
        for i in range(4):
            x = -5.2 + i * 3.45
            y = -LOT + 1.6
            dsil(g, x, y, ZL + 0.06, ZL + 0.78, 0.05, 6, M("Metal", "#3a3d42"))
            dsil(g, x, y, ZL + 0.78, ZL + 0.82, 0.42, 10, M("Masa", "#f2efe8"))
            for s in (-1, 1):
                kutu(g, x + s * 0.65 - 0.2, x + s * 0.65 + 0.2, y - 0.2, y + 0.2, ZL + 0.06, ZL + 0.5, M("Sandalye", "#6b4a32"))
                kutu(g, x + s * 0.83 - 0.03, x + s * 0.83 + 0.03, y - 0.2, y + 0.2, ZL + 0.5, ZL + 0.95, M("Sandalye", "#6b4a32"))
            if i % 2 == 0:
                cubuk(g, (x, y, ZL + 0.82), (x, y, ZL + 2.4), 0.05, M("Metal", "#d9dcdf"))
                dsil(g, x, y, ZL + 2.1, ZL + 2.55, 1.4, 8, M("Tente", rnd.choice(("#f4efe4", "#c0392b", "#2e5d4b"))), r1=0.05, kapak=(True, False))
        for x in (-LOT + 0.6, LOT - 0.6):
            saksi_cicek(g, x, -LOT + 0.6, ZL + 0.06, rnd, r=0.35)
        for i in range(5):
            x = -5.5 + i * 2.75
            kutu(g, x - 0.6, x + 0.6, -LOT + 0.15, -LOT + 0.45, ZL + 0.06, ZL + 0.55, M("Saksi", "#8a8f96"))
            cali(g, x, -LOT + 0.3, ZL + 0.5, 0.35, rnd)
    avlu(g, -LOT, LOT, y0 + D, LOT, rnd, 0.6)
    return g


def karma_mahalle_dukkani(rnd):
    """İki katlı mahalle binası: eczane, fırın ya da kasap; bayrak tabelası, kırma çatı."""
    g = G("karma_mahalle_dukkani")
    zemin(g, M("TasAvlu", "#cfc7b8"))
    st = karma_st(rnd)
    st.update(tip="klasik", panjur=(M("Panjur", "#4f6b8a") if rnd.random() < 0.3 else None))
    tur = rnd.choice(("eczane", "firin", "kasap", "cicekci"))
    renk = dict(eczane="#2f7d4f", firin="#a0622b", kasap="#8a2f2f", cicekci="#6b4a8a")[tur]
    kh = 3.3
    kat = 2
    y0 = -LOT
    D = 10
    W = rnd.uniform(10.5, 14.0)
    x0 = -LOT
    f = govde(g, x0, x0 + W, y0, y0 + D, ZL, ZL + kat * kh, st["dm"])
    c = f["on"]
    vitrin(g, c, 0.4, W * 0.7, ZL, kh - 1.0, dict(st, tabela=M("Tabela", renk)), rnd)
    kapi(g, c, W * 0.85, ZL + 0.15, 1.1, 2.4, st, rnd)
    bayrak_tabela(g, c, W * 0.72, ZL + kh - 0.4, rnd, renk=renk, ikon="hac" if tur == "eczane" else "kare")
    if tur in ("firin", "cicekci"):
        tente(g, c, 0.3, W * 0.7 + 0.1, ZL + kh - 0.3, 1.3, renk, "#f4efe4")
    korniz(g, c, ZL + kh - 0.2, 0.2, 0.12, st["trim"])
    for u in eksenler(W, 2.0, 0.6)[0]:
        pencere(g, c, u, ZL + kh + 0.9, 1.0, 1.5, dict(st, lento="duz"), rnd)
    korniz(g, c, ZL + kat * kh - 0.35, 0.35, 0.3, st["trim"])
    for k in ("sol", "sag", "arka"):
        yan_cephe(g, f[k], rnd, ZL, kat, kh, st, 0.6)
    kirma_cati(g, x0, x0 + W, y0, y0 + D, ZL + kat * kh, 0.7, M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit2"]))), sacak=0.35)
    baca(g, x0 + W * 0.3, y0 + 5, ZL + kat * kh, ZL + kat * kh + 2.6, M("Tugla", "#a65a44"))
    if DETAY:
        if tur == "cicekci":
            c = f["on"]
            for i in range(4):
                p = c.p(0.9 + i * 1.6, 0.45, ZL)
                saksi_cicek(g, p[0], p[1], ZL, rnd, r=0.25)
        if W < 13.5:
            yama(g, x0 + W + 0.2, LOT - 0.2, y0 + 0.3, LOT - 0.3, M("Cim", SABIT["cim"]))
            agac(g, (x0 + W + LOT) / 2, 0, ZL, rnd, boy=5.5)
    avlu(g, -LOT, LOT, y0 + D, LOT, rnd, 0.7)
    return g


def karma_70ler(rnd):
    """70'ler bloğu: yatay şerit pencereler, dolu balkon bantları, zemin dükkânlar."""
    g = G("karma_70ler")
    zemin(g, M("TasAvlu", "#cfc7b8"))
    st = karma_st(rnd)
    renk = rnd.choice(("#d8d4cc", "#cfc9bd", "#e2ddd2"))
    st.update(duvar=renk, dm=M("Beton", renk))
    panel = M("Panel", rnd.choice(("#d9822b", "#2f7d8c", "#c0473a", "#e0b23a", "#5a8f4a")))
    kat = rnd.choice((4, 5))
    kh = 2.9
    y0 = -LOT
    D = 11.0
    H = kat * kh + 0.6
    f = govde(g, -LOT, LOT, y0, y0 + D, ZL, ZL + H, st["dm"])
    c = f["on"]
    dukkan_zemin(g, c, rnd, ZL, kh + 0.6, st, n=rnd.choice((2, 3)))
    for k in range(1, kat):
        z = ZL + 0.6 + k * kh
        # sürekli balkon bandı (dolu) + renkli panel
        c.kutu(g, 0.2, c.L - 0.2, 0, 1.2, z - 0.15, z + 0.02, M("Beton", "#ece8df"))
        c.kutu(g, 0.2, c.L - 0.2, 1.1, 1.22, z, z + 1.0, panel if k % 2 == 1 else M("Beton", "#ece8df"))
        if DETAY:
            for u in eksenler(c.L, 3.5, 0.2)[0][1:]:
                c.kutu(g, u - 1.75 - 0.05, u - 1.75 + 0.05, 0, 1.2, z, z + kh - 0.17, M("Beton", "#ece8df"))
        c.kutu(g, 0.4, c.L - 0.4, 0, 0.03, z + 0.9, z + 2.3, cam(rnd))
        if DETAY:
            for u in eksenler(c.L - 0.8, 1.2)[0]:
                c.kutu(g, 0.4 + u - 0.6 - 0.04, 0.4 + u - 0.6 + 0.04, 0, 0.06, z + 0.9, z + 2.3, M("Cerceve", "#6b6f75"))
    for k in ("sol", "sag"):
        yan_cephe(g, f[k], rnd, ZL + 0.6, kat, kh, st, 0.2)
    cc = f["arka"]
    for k in range(1, kat):
        z = ZL + 0.6 + k * kh
        cc.kutu(g, 0.4, cc.L - 0.4, 0, 0.03, z + 0.9, z + 2.0, cam(rnd))
    duz_cati(g, -LOT, LOT, y0, y0 + D, ZL + H, 0.6, st["dm"], M("DuzCati", "#7f7a74"))
    cati_ustu(g, -LOT, LOT, y0, y0 + D, ZL + H, rnd, n_klima=4)
    avlu(g, -LOT, LOT, y0 + D, LOT, rnd)
    return g


def karma_market(rnd):
    """Mahalle marketi: tek katlı geniş cam cepheli kutu, önünde park yerleri, alışveriş arabası durağı."""
    g = G("karma_market")
    zemin(g, M("Asfalt", "#6a6d72"))
    renk = rnd.choice(("#c0392b", "#2e7d5b", "#2f4f8a", "#e0b23a", "#d9822b"))
    dm = M("Panel", rnd.choice(("#e8e6e1", "#d9d6cf")))
    vm = M("Panel", renk)
    y0 = -1.5
    H = 4.6
    f = govde(g, -LOT + 0.3, LOT - 0.3, y0, LOT - 0.5, ZL, ZL + H, dm)
    c = f["on"]
    st = dict(tabela=vm, yazi="#ffffff", vitrin_kasa=M("Kasa", "#3a3d42"))
    vitrin(g, c, 0.6, c.L * 0.62, ZL, 3.0, st, rnd, kapi_u=c.L * 0.5)
    c.kutu(g, 0, c.L, 0, 0.15, ZL + H - 1.0, ZL + H + 0.4, vm)
    yazi_seridi(g, c, c.L * 0.15, c.L * 0.85, ZL + H - 0.3, 0.7, "#ffffff", rnd, o=0.15)
    c.kutu(g, c.L * 0.45, c.L * 0.75, 0, 2.2, ZL + 3.2, ZL + 3.45, M("Metal", "#c7c9cc"))
    if DETAY:
        for u in (c.L * 0.46, c.L * 0.74):
            cubuk(g, c.p(u, 2.0, ZL), c.p(u, 2.0, ZL + 3.2), 0.1, M("Metal", "#9aa0a6"))
        c.kutu(g, c.L * 0.68, c.L * 0.68 + 0.8, 0, 0.35, ZL + 0.8, ZL + 2.0, M("Metal", "#5a5e63"))  # ATM
        c.kutu(g, c.L * 0.68 + 0.15, c.L * 0.68 + 0.65, 0.35, 0.37, ZL + 1.4, ZL + 1.8, M("Ekran", "#2f4f5c"))
        # araba durağı
        for i in range(3):
            x = 3.0 + i * 0.35
            kutu(g, x, x + 0.25, -3.5, -2.5, ZL, ZL + 0.9, M("Metal", "#b8bcc0"))
        kutu(g, 2.8, 4.3, -3.6, -2.4, ZL + 1.8, ZL + 1.9, vm)
        for x in (2.85, 4.25):
            cubuk(g, (x, -3.5, ZL), (x, -3.5, ZL + 1.8), 0.06, M("Metal", "#8f9499"))
    # park çizgileri
    for i in range(6):
        x = -6.4 + i * 2.5
        yama(g, x, x + 0.1, -LOT + 0.4, -3.0, M("Serit", "#f2f2ee"), z=ZL, k=0.01)
    yama(g, -LOT + 0.3, LOT - 0.3, -2.4, y0 - 0.05, M("Kaldirim", "#d8d1c4"), k=0.12)
    if DETAY:
        for x in (-LOT + 0.6, LOT - 0.6):
            cali(g, x, -LOT + 0.6, ZL, 0.5, rnd)
    duz_cati(g, -LOT + 0.3, LOT - 0.3, y0, LOT - 0.5, ZL + H, 0.4, dm, M("DuzCati", "#8f8a84"))
    cati_ustu(g, -LOT + 0.3, LOT - 0.3, y0, LOT - 0.5, ZL + H, rnd, n_klima=5, asansor=False)
    return g


# ================================== KULE BLOKLAR ==================================

def bahce_zemini(g, rnd, y_bas, agac_n=2, oyun=False):
    """Bina arkasında çimen, yürüyüş yolu, ağaçlar, bank; isteğe bağlı küçük oyun alanı."""
    yama(g, -LOT + 0.2, LOT - 0.2, y_bas, LOT - 0.2, M("Cim", SABIT["cim"]))
    yama(g, -0.8, 0.8, y_bas, LOT - 0.2, M("TasYol", "#cfc6b5"), z=ZL + 0.012)
    if not DETAY:
        return
    for i in range(agac_n):
        x = rnd.choice((-1, 1)) * rnd.uniform(2.5, 5.5)
        y = rnd.uniform(y_bas + 1.2, LOT - 1.2)
        agac(g, x, y, ZL, rnd)
    if oyun and LOT - y_bas > 3.5:
        x0 = 2.0
        y = (y_bas + LOT) / 2
        yama(g, x0, x0 + 3.5, y - 1.5, y + 1.5, M("Kum", "#e8d6a8"), z=ZL + 0.012, k=0.03)
        m = M("Oyun", rnd.choice(("#d94f3a", "#2f7dbf", "#e0b23a")))
        for x in (x0 + 0.6, x0 + 2.6):
            cubuk(g, (x, y - 0.6, ZL), (x, y, ZL + 2.0), 0.08, m)
            cubuk(g, (x, y + 0.6, ZL), (x, y, ZL + 2.0), 0.08, m)
        cubuk(g, (x0 + 0.6, y, ZL + 2.0), (x0 + 2.6, y, ZL + 2.0), 0.08, m)
        for x in (x0 + 1.2, x0 + 2.0):
            cubuk(g, (x, y, ZL + 2.0), (x, y, ZL + 0.55), 0.02, M("Metal", "#9aa0a6"))
            kutu(g, x - 0.22, x + 0.22, y - 0.1, y + 0.1, ZL + 0.5, ZL + 0.56, M("Ahsap", SABIT["ahsap"]))


def kule_panel(rnd):
    """Panel blok (panelház): prefabrik panel derzleri, aynı pencereler, renkli yenilenmiş cephe şeritleri, loggialar."""
    g = G("kule_panel")
    zemin(g, M("Cim", SABIT["cim"]))
    kat = rnd.choice((8, 10, 10, 11))
    kh = 2.8
    y0 = -LOT + 1.5
    D = 11.0
    H = kat * kh + 0.4
    taban = rnd.choice(("#e8e4da", "#dcd8ce", "#efe9dc"))
    renk1 = rnd.choice(("#e07a3a", "#3f8fb0", "#d9b43a", "#7fae4f", "#c75a5a", "#6b7fc0"))
    renk2 = acik(renk1, 0.45)
    dm = M("Panel", taban)
    f = govde(g, -LOT, LOT, y0, y0 + D, ZL, ZL + H, dm)
    st = dict(cerceve=M("Cerceve", "#f4f4f2"), denizlik=None, tip="kare", cam="#9fc1d6", balkon_mat=M("Beton", "#ece8df"))
    desen = rnd.choice(("serit", "dama", "kenar"))
    for ad in ("on", "arka"):
        c = f[ad]
        L = c.L
        sutun = [0.0, 3.5, 7.0, 10.5, 14.0]
        for k in range(kat):
            z = ZL + 0.4 + k * kh
            for j in range(4):
                u0, u1 = sutun[j], sutun[j + 1]
                boya = (desen == "serit" and j == (1 if ad == "on" else 2)) or (desen == "dama" and (j + k) % 2 == 0 and k > 0) or \
                       (desen == "kenar" and j in (0, 3))
                if boya:
                    c.kutu(g, u0 + 0.02, u1 - 0.02, 0, 0.05, z + 0.02, z + kh - 0.02, M("Panel", renk1 if k % 2 == 0 else renk2))
                if k == 0 and ad == "on" and j == 1:
                    kapi(g, c, (u0 + u1) / 2, ZL + 0.45, 1.6, 2.3, dict(st, kapi=cam(rnd)), rnd, z0=ZL, sacak=True)
                    continue
                loggia = ad == "on" and j == 2 and k > 0
                if loggia:
                    balkon(g, c, u0 + 0.2, u1 - 0.2, z + 0.02, 1.0, st, rnd, korkuluk="dolu")
                    c.kutu(g, u0 + 0.25, u1 - 0.25, 0.9, 1.02, z + 0.02, z + 1.0, M("Panel", renk1))
                    kapi(g, c, u0 + 1.0, z + 0.03, 0.8, 2.15, dict(st, kapi=cam(rnd)), rnd, z0=z)
                    pencere(g, c, u0 + 2.4, z + 0.9, 1.1, 1.3, st, rnd)
                else:
                    pencere(g, c, (u0 + u1) / 2, z + 0.9, 1.5, 1.35, st, rnd)
            if DETAY:
                c.kutu(g, 0, L, 0, 0.03, z + kh - 0.03, z + kh + 0.03, M("Derz", koyu(taban, 0.85)))
        if DETAY:
            for u in sutun[1:-1]:
                c.kutu(g, u - 0.03, u + 0.03, 0, 0.03, ZL, ZL + H, M("Derz", koyu(taban, 0.85)))
    # merdiven kulesi camı (arka orta) ve yan cepheler
    for ad in ("sol", "sag"):
        c = f[ad]
        if rnd.random() < 0.6:
            c.kutu(g, 0.3, c.L - 0.3, 0, 0.05, ZL + 0.4, ZL + H - 0.2, M("Panel", renk1))
        for k in range(1, kat):
            pencere(g, c, c.L / 2, ZL + 0.4 + k * kh + 0.9, 0.9, 1.2, st, rnd)
    duz_cati(g, -LOT, LOT, y0, y0 + D, ZL + H, 0.7, dm, M("DuzCati", "#7f7a74"))
    cati_ustu(g, -LOT, LOT, y0, y0 + D, ZL + H, rnd, n_klima=2)
    if DETAY:
        for x in (-4.5, 4.5):
            cubuk(g, (x, y0 + 3, ZL + H), (x, y0 + 3, ZL + H + 3.5), 0.06, M("Metal", "#9aa0a6"))
            cubuk(g, (x - 0.6, y0 + 3, ZL + H + 3.0), (x + 0.6, y0 + 3, ZL + H + 3.0), 0.04, M("Metal", "#9aa0a6"))
    yama(g, -LOT + 0.2, LOT - 0.2, -LOT + 0.2, y0 - 0.05, M("Cim", SABIT["cim"]))
    yama(g, -3.0, -1.3, -LOT, y0, M("TasYol", "#cfc6b5"), z=ZL + 0.012)
    if DETAY:
        cali(g, -5.5, -LOT + 0.8, ZL, 0.6, rnd)
        cali(g, 5.5, -LOT + 0.8, ZL, 0.6, rnd)
    return g


def perde_duvar(g, c, z0, z1, rnd, ton="#9fbfd4", aralik=1.5, kat_h=3.4, fin=0.25, fin_mat=None):
    """Cam giydirme cephe: OfisCam_ + dikey kayıtlar + kat bantları."""
    c.kutu(g, 0.0, c.L, 0, 0.04, z0, z1, M("OfisCam", ton))
    if not DETAY:
        return
    fm = fin_mat or M("Kasa", "#4a5058")
    n = max(1, int(c.L / aralik))
    for i in range(n + 1):
        u = c.L * i / n
        c.kutu(g, u - 0.05, u + 0.05, 0, fin, z0, z1, fm)
    z = z0
    while z <= z1 + 0.01:
        c.kutu(g, 0, c.L, 0, 0.1, z - 0.08, z + 0.08, fm)
        z += kat_h


def kule_ofis_cam(rnd):
    """Cam ofis kulesi: iki katlı lobi podyumu, geri çekilen üst katlar, çatı tacı."""
    g = G("kule_ofis_cam")
    zemin(g, M("TasAvlu", "#d4cec3"))
    kat = rnd.randint(10, 14)
    kh = 3.4
    ton = rnd.choice(("#9fbfd4", "#8fb0c8", "#a8c8c8", "#b4c4d4", "#8aa6bf"))
    pod = M("Tas", rnd.choice(("#cfc8bb", "#b8b2a6", "#dcd6ca")))
    # podyum
    py0 = -LOT + 1.0
    f = govde(g, -LOT + 0.5, LOT - 0.5, py0, LOT - 0.5, ZL, ZL + 2 * kh, pod)
    c = f["on"]
    c.kutu(g, 1.0, c.L - 1.0, 0, 0.04, ZL, ZL + 2 * kh - 0.4, M("OfisCam", ton))
    if DETAY:
        for u in eksenler(c.L - 2, 1.5)[0]:
            c.kutu(g, 1.0 + u - 0.75 - 0.06, 1.0 + u - 0.75 + 0.06, 0, 0.12, ZL, ZL + 2 * kh - 0.4, M("Kasa", "#3a3f46"))
        c.kutu(g, 1.0, c.L - 1.0, 0, 0.12, ZL + kh - 0.08, ZL + kh + 0.08, M("Kasa", "#3a3f46"))
    c.kutu(g, c.L / 2 - 2.5, c.L / 2 + 2.5, 0, 2.5, ZL + 3.4, ZL + 3.65, M("Metal", "#c7c9cc"))
    kapi(g, c, c.L / 2, ZL + 0.05, 2.0, 2.8, dict(kapi=M("Vitrin", "#cfe3ea"), cerceve=M("Kasa", "#3a3f46")), rnd)
    for k in ("sol", "sag", "arka"):
        perde_duvar(g, f[k], ZL + 0.5, ZL + 2 * kh - 0.4, rnd, ton, kat_h=kh)
    duz_cati(g, -LOT + 0.5, LOT - 0.5, py0, LOT - 0.5, ZL + 2 * kh, 0.5, pod, M("DuzCati", "#8f8a84"))
    # kule
    tw = rnd.uniform(9.0, 10.5)
    td = rnd.uniform(8.5, 10.0)
    tx0, ty0 = -tw / 2, py0 + 1.2
    z = ZL + 2 * kh
    katlar = kat - 2
    geri = rnd.choice((0, 2, 3))
    alt = katlar - geri
    f2 = govde(g, tx0, tx0 + tw, ty0, ty0 + td, z, z + alt * kh, M("Kasa", "#4a5058"))
    for k in f2:
        perde_duvar(g, f2[k], z, z + alt * kh, rnd, ton, kat_h=kh)
    z2 = z + alt * kh
    if geri:
        duz_cati(g, tx0, tx0 + tw, ty0, ty0 + td, z2, 0.0, None, M("DuzCati", "#8f8a84"))
        f3 = govde(g, tx0 + 1.2, tx0 + tw - 1.2, ty0 + 1.2, ty0 + td - 1.2, z2, z2 + geri * kh, M("Kasa", "#4a5058"))
        for k in f3:
            perde_duvar(g, f3[k], z2, z2 + geri * kh, rnd, ton, kat_h=kh)
        z2 += geri * kh
        tx0, tw, ty0, td = tx0 + 1.2, tw - 2.4, ty0 + 1.2, td - 2.4
    duz_cati(g, tx0, tx0 + tw, ty0, ty0 + td, z2, 1.2, M("Kasa", "#5a6068"), M("DuzCati", "#8f8a84"))
    if DETAY:
        kutu(g, tx0 + 1.5, tx0 + tw - 1.5, ty0 + 1.5, ty0 + td - 1.5, z2, z2 + 2.4, M("Metal", "#c7c9cc"))
        for i in range(int(tw / 0.6)):
            x = tx0 + 0.3 + i * 0.6
            kutu(g, x - 0.04, x + 0.04, ty0 - 0.05, ty0 + 0.25, z2 + 1.2, z2 + 3.0, M("Kasa", "#5a6068"))
        cubuk(g, (tx0 + tw - 1.0, ty0 + td - 1.0, z2 + 2.4), (tx0 + tw - 1.0, ty0 + td - 1.0, z2 + 7.0), 0.1, M("Metal", "#c7c9cc"))
        top(g, (tx0 + tw - 1.0, ty0 + td - 1.0, z2 + 7.0), 0.15, 0.15, 0.15, M("Isik", "#ff5a4a"))
        # podyum çatısında teras
        for x in (-5.0, 5.0):
            saksi_cicek(g, x, py0 + 0.8, ZL + 2 * kh + 0.04, rnd, r=0.4)
    # meydancık
    yama(g, -LOT + 0.2, LOT - 0.2, -LOT + 0.2, py0 - 0.05, M("TasAvlu", "#e0d9cc"))
    if DETAY:
        for x in (-5.5, 5.5):
            agac(g, x, -LOT + 0.6, ZL, rnd, boy=5.0, tip="kavak")
        for x in (-2.8, 2.8):
            kutu(g, x - 1.0, x + 1.0, -LOT + 0.3, -LOT + 0.75, ZL, ZL + 0.45, M("Beton", "#d8d3c8"))
    return g


def kule_konut(rnd):
    """Modern konut kulesi: her katta cam korkuluklu balkonlar (sağa sola kayan), ahşap paneller, çatı terası."""
    g = G("kule_konut")
    zemin(g, M("Cim", SABIT["cim"]))
    kat = rnd.randint(9, 13)
    kh = 3.0
    W, D = rnd.uniform(10.5, 12.0), rnd.uniform(9.5, 10.5)
    x0, y0 = -W / 2, -LOT + 2.0
    H = kat * kh
    dm = M("Siva", rnd.choice(("#f2f0ea", "#e8e6e0", "#efe9df")))
    ah = M("Ahsap", rnd.choice(("#b07a4a", "#9a6a44", "#c49060")))
    gri = M("Panel", rnd.choice(("#5a5f66", "#6b7078", "#4a4e55")))
    f = govde(g, x0, x0 + W, y0, y0 + D, ZL, ZL + H, dm)
    st = dict(cerceve=M("Cerceve", "#3a3d42"), denizlik=None, tip="modern", cam="#a8c4d6", balkon_mat=M("Beton", "#f4f2ec"))
    for ad in ("on", "arka", "sol", "sag"):
        c = f[ad]
        L = c.L
        for k in range(kat):
            z = ZL + k * kh
            if k == 0:
                if ad == "on":
                    c.kutu(g, 0.6, L - 0.6, 0, 0.04, ZL + 0.1, ZL + kh - 0.3, M("Vitrin", "#cfe3ea"))
                    c.kutu(g, L / 2 - 2.0, L / 2 + 2.0, 0, 1.8, ZL + kh - 0.2, ZL + kh, M("Beton", "#f4f2ec"))
                continue
            kay = (k % 2) * 2 - 1
            bu = L / 2 + kay * L * 0.18
            if ad in ("on", "arka"):
                c.kutu(g, bu - L * 0.3, bu + L * 0.3, 0, 0.04, z + 0.1, z + kh - 0.25, cam(rnd, "#a8c4d6"))
                if DETAY:
                    for u in eksenler(L * 0.6, 1.3)[0]:
                        c.kutu(g, bu - L * 0.3 + u - 0.65 - 0.03, bu - L * 0.3 + u - 0.65 + 0.03, 0, 0.06, z + 0.1, z + kh - 0.25, st["cerceve"])
                balkon(g, c, bu - L * 0.3, bu + L * 0.3, z + 0.02, 1.5, st, rnd, korkuluk="cam")
                pan = (0.2, bu - L * 0.3 - 0.1) if kay > 0 else (bu + L * 0.3 + 0.1, L - 0.2)
                c.kutu(g, pan[0], pan[1], 0, 0.06, z, z + kh, ah if (k // 2) % 2 == 0 else gri)
            else:
                pencere(g, c, L * 0.3, z + 0.9, 1.2, 1.5, st, rnd)
                pencere(g, c, L * 0.7, z + 0.9, 1.2, 1.5, st, rnd)
    duz_cati(g, x0, x0 + W, y0, y0 + D, ZL + H, 1.1, dm, M("DuzCati", "#8f8a84"))
    if DETAY:
        zt = ZL + H
        kutu(g, x0 + 1.0, x0 + 4.0, y0 + 1.0, y0 + D - 1.0, zt, zt + 0.05, M("Deck", "#b58a5e"))
        for i in range(5):
            x = x0 + 1.1 + i * 0.7
            kutu(g, x - 0.05, x + 0.05, y0 + 1.0, y0 + D - 1.0, zt + 2.4, zt + 2.55, ah)
        for x, y in ((x0 + 1.1, y0 + 1.1), (x0 + 3.9, y0 + 1.1), (x0 + 1.1, y0 + D - 1.1), (x0 + 3.9, y0 + D - 1.1)):
            cubuk(g, (x, y, zt), (x, y, zt + 2.45), 0.12, ah)
        for i in range(3):
            saksi_cicek(g, x0 + W - 1.2, y0 + 1.5 + i * 2.5, zt, rnd, r=0.4)
        kutu(g, x0 + W - 4.5, x0 + W - 2.0, y0 + D - 3.5, y0 + D - 1.2, zt, zt + 2.6, dm)
    yama(g, -LOT + 0.2, LOT - 0.2, -LOT + 0.2, y0 - 0.05, M("TasAvlu", "#ddd5c6"))
    bahce_zemini(g, rnd, y0 + D + 0.1, agac_n=2, oyun=True)
    return g


def kule_otel(rnd):
    """Otel: dikey kanatlı (fin) cephe, çatıda ışıklı tabela, giriş saçağı ve bayrak direkleri."""
    g = G("kule_otel")
    zemin(g, M("TasAvlu", "#d9d1c2"))
    kat = rnd.randint(8, 11)
    kh = 3.1
    y0 = -LOT + 2.5
    D = 8.5
    W = 2 * LOT - 0.6
    x0 = -W / 2
    H = kat * kh + 1.0
    renk = rnd.choice(("#efe7d6", "#e4dccb", "#d8dde2", "#ece2d8"))
    dm = M("Siva", renk)
    fin = M("Panel", rnd.choice(("#c7a46a", "#8a6a4a", "#5a6068", "#b07a5a")))
    f = govde(g, x0, x0 + W, y0, y0 + D, ZL, ZL + H, dm)
    c = f["on"]
    c.kutu(g, 0.5, c.L - 0.5, 0, 0.04, ZL + 0.1, ZL + 3.8, M("Vitrin", "#d8e8ec"))
    if DETAY:
        for u in eksenler(c.L - 1, 1.6)[0]:
            c.kutu(g, 0.5 + u - 0.8 - 0.05, 0.5 + u - 0.8 + 0.05, 0, 0.1, ZL + 0.1, ZL + 3.8, M("Kasa", "#3a3d42"))
    c.kutu(g, c.L / 2 - 3.5, c.L / 2 + 3.5, 0, 2.4, ZL + 3.6, ZL + 4.0, M("Panel", koyu(renk, 0.6)))
    if DETAY:
        for s in (-1, 1):
            cubuk(g, c.p(c.L / 2 + s * 3.2, 2.2, ZL), c.p(c.L / 2 + s * 3.2, 2.2, ZL + 3.6), 0.12, M("Metal", "#c7c9cc"))
        yazi_seridi(g, c, c.L / 2 - 3.0, c.L / 2 + 3.0, ZL + 3.8, 0.28, "#ffe6a8", rnd, o=2.4)
    for ad in ("on", "arka"):
        c = f[ad]
        us, adim = eksenler(c.L, 1.75, 0.3)
        for k in range(1, kat):
            z = ZL + 1.0 + k * kh
            for u in us:
                c.kutu(g, u - 0.6, u + 0.6, 0, 0.04, z + 0.5, z + kh - 0.3, cam(rnd, "#a6c2d2"))
                if DETAY:
                    c.kutu(g, u - 0.7, u + 0.7, 0, 0.1, z + 0.4, z + 0.5, M("Cerceve", "#f4f4f2"))
        if DETAY:
            for i in range(len(us) + 1):
                u = 0.15 if i == 0 else (c.L - 0.15 if i == len(us) else (us[i - 1] + us[i]) / 2)
                c.kutu(g, u - 0.12, u + 0.12, 0, 0.55, ZL + 4.0, ZL + H - 0.3, fin)
    for ad in ("sol", "sag"):
        c = f[ad]
        c.kutu(g, 0.4, c.L - 0.4, 0, 0.06, ZL + 4.0, ZL + H - 0.3, fin)
    duz_cati(g, x0, x0 + W, y0, y0 + D, ZL + H, 0.8, dm, M("DuzCati", "#8f8a84"))
    zt = ZL + H
    sw = 7.0
    kutu(g, -sw / 2, sw / 2, y0 + 0.6, y0 + 0.9, zt + 0.8, zt + 2.6, M("Tabela", rnd.choice(("#2a2f3a", "#7a2f2f", "#2f4f4a"))))
    for x in (-sw / 2 + 0.5, sw / 2 - 0.5):
        cubuk(g, (x, y0 + 1.0, zt), (x, y0 + 1.0, zt + 1.0), 0.1, M("Metal", "#9aa0a6"))
    c2 = C((-sw / 2, y0 + 0.6), (1, 0), (0, -1), sw)
    yazi_seridi(g, c2, 0, sw, zt + 1.7, 1.0, "#fff2c8", rnd, o=0.0)
    cati_ustu(g, x0, x0 + W, y0 + 1.5, y0 + D, zt, rnd, n_klima=4)
    # giriş
    yama(g, -LOT + 0.2, LOT - 0.2, -LOT + 0.2, y0 - 0.05, M("TasAvlu", "#e3dccd"))
    if DETAY:
        for i, x in enumerate((-5.6, -4.8, -4.0)):
            cubuk(g, (x, -LOT + 0.6, ZL), (x, -LOT + 0.6, ZL + 6.0), 0.07, M("Metal", "#d9dcdf"))
            kutu(g, x + 0.04, x + 1.0, -LOT + 0.58, -LOT + 0.62, ZL + 5.0, ZL + 5.8, M("Bayrak", ("#c0392b", "#2f7d4f", "#2f4f8a")[i]))
        for x in (3.5, 5.5):
            saksi_cicek(g, x, -LOT + 0.8, ZL, rnd, r=0.45)
    bahce_zemini(g, rnd, y0 + D + 0.1, agac_n=2)
    return g


def kule_ikiz(rnd):
    """İkiz kule: ortak podyum, iki ince kule ve aralarında cam köprü."""
    g = G("kule_ikiz")
    zemin(g, M("TasAvlu", "#d4cec3"))
    kh = 3.2
    ton = rnd.choice(("#9fbfd4", "#a8c8c8", "#b4c4d4"))
    dm = M("Panel", rnd.choice(("#e8e4dc", "#d9d4ca", "#c9c4ba")))
    py0 = -LOT + 1.0
    f = govde(g, -LOT + 0.4, LOT - 0.4, py0, LOT - 0.4, ZL, ZL + kh + 0.8, dm)
    vitrin(g, f["on"], 0.6, f["on"].L - 0.6, ZL, kh - 0.4, dict(tabela=M("Tabela", "#2a2f3a")), rnd)
    duz_cati(g, -LOT + 0.4, LOT - 0.4, py0, LOT - 0.4, ZL + kh + 0.8, 0.5, dm, M("DuzCati", "#8f8a84"))
    z = ZL + kh + 0.8
    k1, k2 = rnd.randint(8, 10), rnd.randint(9, 12)
    for (x0, kat) in ((-6.2, k1), (0.8, k2)):
        w, d = 5.4, 7.5
        y0 = py0 + 2.0
        f2 = govde(g, x0, x0 + w, y0, y0 + d, z, z + kat * kh, dm)
        for ad, c in f2.items():
            for k in range(kat):
                zz = z + k * kh
                c.kutu(g, 0.35, c.L - 0.35, 0, 0.04, zz + 0.4, zz + kh - 0.2, M("OfisCam", ton))
                if DETAY:
                    for u in eksenler(c.L - 0.7, 1.2)[0]:
                        c.kutu(g, 0.35 + u - 0.6 - 0.04, 0.35 + u - 0.6 + 0.04, 0, 0.08, zz + 0.4, zz + kh - 0.2, M("Kasa", "#4a5058"))
        duz_cati(g, x0, x0 + w, y0, y0 + d, z + kat * kh, 0.9, dm, M("DuzCati", "#8f8a84"))
        if DETAY:
            kutu(g, x0 + 1.2, x0 + w - 1.2, y0 + 1.5, y0 + d - 1.5, z + kat * kh, z + kat * kh + 1.8, M("Metal", "#c7c9cc"))
    zk = z + 6 * kh
    kutu(g, -0.8, 0.8, py0 + 3.5, py0 + 6.5, zk, zk + kh, M("OfisCam", acik(ton, 0.2)))
    kutu(g, -0.8, 0.8, py0 + 3.5, py0 + 6.5, zk - 0.3, zk, dm)
    kutu(g, -0.8, 0.8, py0 + 3.5, py0 + 6.5, zk + kh, zk + kh + 0.25, dm)
    yama(g, -LOT + 0.2, LOT - 0.2, -LOT + 0.2, py0 - 0.05, M("TasAvlu", "#e0d9cc"))
    if DETAY:
        for x in (-5.0, 0.0, 5.0):
            agac(g, x, -LOT + 0.6, ZL, rnd, boy=4.5, tip="kavak")
    return g


# ================================== EVLER ==================================

EV_RENK = ["#f2e6c8", "#efd6b0", "#e6e0d0", "#f4ead8", "#dfe6d8", "#e9d0c0", "#d8dfe6", "#f1dcae", "#e4d6e4", "#f6f0e2"]


def ev_st(rnd):
    renk = rnd.choice(EV_RENK)
    return dict(duvar=renk, dm=M("Siva", renk), trim=M("Korniz", "#f7f3ea"), cerceve=M("Cerceve", rnd.choice(("#f4f4f2", "#6b4a32", "#3a3d42"))),
                denizlik=M("Tas", "#d6d2c8"), panjur=(M("Panjur", rnd.choice(("#3e6b4f", "#6b4a32", "#4f6b8a", "#8a3a32"))) if rnd.random() < 0.35 else None),
                saksi=0.3, tip=rnd.choice(("klasik", "kare")), cam="#a9c9de", korkuluk="demir")


def on_cit(g, rnd, y, kapi_x=None, tip=None, renk=None, x0=-LOT, x1=LOT, kapi_w=1.2):
    """Sokak çiti: kaideli demir, ahşap çakma, çit bitkisi ya da alçak taş duvar. Kapı aralığı bırakır."""
    tip = tip or rnd.choice(("demir", "tahta", "cali", "tas"))
    renk = renk or rnd.choice(("#2b2d31", "#4a5a4a", "#f4f4f2", "#6b4a32"))
    parcalar = [(x0, x1)] if kapi_x is None else [(x0, kapi_x - kapi_w / 2), (kapi_x + kapi_w / 2, x1)]
    for a, b in parcalar:
        if b - a < 0.2:
            continue
        if tip == "demir":
            kutu(g, a, b, y - 0.12, y + 0.12, ZL, ZL + 0.5, M("Tas", "#c9c1b3"))
            cit_parmak(g, (a, y, 0), (b, y, 0), ZL + 0.5, 0.9, renk, aralik=0.15)
            if DETAY:
                for x in (a, b):
                    kutu(g, x - 0.18, x + 0.18, y - 0.18, y + 0.18, ZL, ZL + 1.55, M("Tas", "#c9c1b3"))
        elif tip == "tahta":
            cit_tahta(g, (a, y, 0), (b, y, 0), ZL, 1.15, renk if renk != "#2b2d31" else "#8a5a3a")
        elif tip == "cali":
            cit_cali(g, a, b, y - 0.3, y + 0.3, ZL, 1.2, rnd)
        else:
            kutu(g, a, b, y - 0.15, y + 0.15, ZL, ZL + 0.75, M("Tas", rnd.choice(("#c9c1b3", "#b9ad9a", "#d6cdb8"))))
            if DETAY:
                kutu(g, a - 0.03, b + 0.03, y - 0.18, y + 0.18, ZL + 0.75, ZL + 0.82, M("Tas", "#e1dbcf"))
    if kapi_x is not None and DETAY:
        km = M("Cit", renk)
        cit_parmak(g, (kapi_x - kapi_w / 2 + 0.05, y, 0), (kapi_x + kapi_w / 2 - 0.05, y, 0), ZL + 0.05, 1.1, renk, aralik=0.12)


def sebze_tarhi(g, x0, x1, y0, y1, rnd):
    yama(g, x0, x1, y0, y1, M("Toprak", "#7a5a3e"), k=0.04)
    if not DETAY:
        return
    n = int((y1 - y0) / 0.6)
    for i in range(n):
        y = y0 + 0.3 + i * 0.6
        ton = rnd.choice(("#6aa84f", "#8cbf62", "#4f8a47"))
        for j in range(int((x1 - x0) / 0.45)):
            x = x0 + 0.25 + j * 0.45
            top(g, (x, y, ZL + 0.12), 0.17, 0.17, 0.12, M("Yaprak", ton))


def kulube(g, x, y, w, d, h, rnd, aci=0):
    """Bahçe kulübesi (ahşap, tek eğimli çatı)."""
    m = M("Ahsap", rnd.choice(("#8a5a3a", "#6b4a32", "#9a7a5a", "#5a6b5a")))
    kutu(g, x - w / 2, x + w / 2, y - d / 2, y + d / 2, ZL, ZL + h, m)
    cm = M("Cati", "#4a4e55")
    p = [(x - w / 2 - 0.2, y - d / 2 - 0.2, ZL + h + 0.3), (x + w / 2 + 0.2, y - d / 2 - 0.2, ZL + h + 0.3),
         (x + w / 2 + 0.2, y + d / 2 + 0.2, ZL + h - 0.1), (x - w / 2 - 0.2, y + d / 2 + 0.2, ZL + h - 0.1)]
    q = [add(a, (0, 0, 0.08)) for a in p]
    kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], cm)
    prizma(g, [(x - w / 2, y - d / 2, ZL + h), (x - w / 2, y + d / 2, ZL + h), (x - w / 2, y - d / 2, ZL + h + 0.3)], (w, 0, 0), m)
    if DETAY:
        c = C((x - w / 2, y - d / 2), (1, 0), (0, -1), w)
        c.kutu(g, w / 2 - 0.45, w / 2 + 0.45, 0, 0.04, ZL, ZL + 1.9, M("Kapi", koyu(HEX[m], 0.75)))
        for i in range(int(w / 0.3)):
            c.kutu(g, 0.15 + i * 0.3, 0.19 + i * 0.3, 0, 0.03, ZL, ZL + h, M("Ahsap", koyu(HEX[m], 0.85)))


def trambolin(g, x, y):
    if not DETAY:
        return
    dsil(g, x, y, ZL + 0.75, ZL + 0.8, 1.5, 14, M("Trambolin", "#2b2d31"))
    dsil(g, x, y, ZL + 0.8, ZL + 0.82, 1.6, 14, M("Oyun", "#2f7dbf"), kapak=(False, False))
    for i in range(4):
        a = math.pi / 2 * i + 0.4
        cubuk(g, (x + math.cos(a) * 1.5, y + math.sin(a) * 1.5, ZL), (x + math.cos(a) * 1.5, y + math.sin(a) * 1.5, ZL + 0.8), 0.06, M("Metal", "#9aa0a6"))


def havuz(g, x0, x1, y0, y1):
    kutu(g, x0 - 0.4, x1 + 0.4, y0 - 0.4, y1 + 0.4, ZL, ZL + 0.1, M("Tas", "#ece6da"))
    kutu(g, x0, x1, y0, y1, ZL + 0.1, ZL + 0.13, M("Su", "#5ab6d6"))


def ev_kocka(rnd):
    """Kocka ház: kare planlı, piramit çatılı kasaba evi; süslü pencere bantları, yan girişli sundurma, kaideli demir çit, meyve ağaçları."""
    g = G("ev_kocka")
    zemin(g, M("Cim", rnd.choice((SABIT["cim"], SABIT["cim2"]))))
    st = ev_st(rnd)
    s = rnd.uniform(8.6, 9.6)
    x0 = -LOT + rnd.uniform(1.0, 2.0)
    y0 = -LOT + rnd.uniform(3.0, 4.0)
    H = 3.3
    f = govde(g, x0, x0 + s, y0, y0 + s, ZL, ZL + 0.6 + H, st["dm"])
    vurgu = M("Siva", rnd.choice(("#c9b48a", "#b8c9a0", "#d8a890", "#a8b8c8", koyu(st["duvar"], 0.85))))
    for ad in ("on", "sol", "sag", "arka"):
        c = f[ad]
        c.kutu(g, 0, c.L, 0, 0.05, ZL, ZL + 0.6, M("Siva", koyu(st["duvar"], 0.78)))
        n = 3 if ad == "on" else 2
        for i in range(n):
            u = c.L * (i + 0.5) / n
            if ad == "sag" and i == 0:
                continue
            pencere(g, c, u, ZL + 1.4, 0.95, 1.45, dict(st, lento=None), rnd)
            if DETAY:
                c.kutu(g, u - 0.75, u + 0.75, 0, 0.04, ZL + 1.15, ZL + 3.25, vurgu)
                c.kutu(g, u - 0.6, u + 0.6, 0.04, 0.06, ZL + 3.0, ZL + 3.18, st["trim"])
        korniz(g, c, ZL + 0.6 + H - 0.3, 0.3, 0.2, st["trim"], profil=False)
    zt = kirma_cati(g, x0, x0 + s, y0, y0 + s, ZL + 0.6 + H, rnd.uniform(0.85, 1.0),
                    M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit2"], "#7a4a3a", "#5d6066"))), sacak=0.45)
    baca(g, x0 + s * 0.5 + 0.8, y0 + s * 0.5, ZL + H, zt + 0.4, M("Tugla", "#a65a44"))
    # yan sundurma ve giriş
    c = f["sag"]
    gx = x0 + s
    kutu(g, gx, gx + 1.6, y0 + 0.6, y0 + 3.0, ZL, ZL + 0.45, M("Tas", "#cfc7b8"))
    kapi(g, c, 1.8, ZL + 0.6, 1.0, 2.3, st, rnd, z0=ZL + 0.45)
    p = [(gx, y0 + 0.4, ZL + 3.2), (gx + 1.9, y0 + 0.4, ZL + 2.7), (gx + 1.9, y0 + 3.2, ZL + 2.7), (gx, y0 + 3.2, ZL + 3.2)]
    q = [add(a, (0, 0, 0.1)) for a in p]
    kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], M("Cati", "#4a4e55"))
    if DETAY:
        for yy in (y0 + 0.6, y0 + 3.0):
            cubuk(g, (gx + 1.75, yy, ZL + 0.45), (gx + 1.75, yy, ZL + 2.7), 0.1, M("Demir", SABIT["demir"]))
    # bahçe
    kx = x0 + s + 1.0
    on_cit(g, rnd, -LOT + 0.25, kapi_x=min(LOT - 1.5, kx + 0.8))
    yama(g, kx, kx + 1.6, -LOT + 0.25, y0 + 0.6, M("TasYol", "#c9c0ae"))
    if DETAY:
        for i in range(rnd.randint(2, 3)):
            agac(g, rnd.uniform(-LOT + 1.2, x0 + s - 1.0) if i < 2 else rnd.uniform(kx + 1.5, LOT - 1.0),
                 rnd.uniform(-LOT + 1.3, y0 - 1.0) if i == 0 else rnd.uniform(y0 + s + 1.0, LOT - 1.2), ZL, rnd, boy=rnd.uniform(3.8, 5.5), tip="yuvarlak")
        sebze_tarhi(g, x0 + 0.5, x0 + 5.0, y0 + s + 0.8, LOT - 0.5, rnd)
        cicek_tarhi(g, x0, x0 + s, y0 - 0.9, y0 - 0.2, rnd)
        if rnd.random() < 0.6:
            kulube(g, LOT - 1.6, LOT - 1.6, 2.0, 1.8, 2.0, rnd)
    return g


def garaj(g, x0, x1, y0, y1, rnd, dm, renk=None):
    H = 2.8
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, dm)
    c = f["on"]
    km = M("Garaj", renk or rnd.choice(("#e8e8e4", "#5a5e63", "#8a5a3a", "#3a4a5a")))
    c.kutu(g, 0.35, c.L - 0.35, 0, 0.04, ZL, ZL + 2.3, km)
    if DETAY:
        for i in range(1, 6):
            z = ZL + 2.3 * i / 6
            c.kutu(g, 0.35, c.L - 0.35, 0.04, 0.06, z - 0.02, z + 0.02, M("Garaj", koyu(HEX[km], 0.85)))
    duz_cati(g, x0, x1, y0, y1, ZL + H, 0.25, dm, M("DuzCati", "#8f8a84"))


def ev_aile(rnd):
    """İki katlı aile evi: beşik çatı, çatı penceresi, yanında garaj ve giriş yolu, çit bitkisi, güneş paneli."""
    g = G("ev_aile")
    zemin(g, M("Cim", rnd.choice((SABIT["cim"], SABIT["cim2"]))))
    st = ev_st(rnd)
    W = rnd.uniform(8.0, 9.0)
    D = rnd.uniform(7.5, 8.5)
    sag = rnd.random() < 0.5
    gw = 3.6
    x0 = (-LOT + 0.8) if not sag else (LOT - 0.8 - W)
    gx0 = x0 + W if not sag else x0 - gw
    y0 = -LOT + rnd.uniform(4.0, 5.0)
    kh = 2.8
    H = 2 * kh + 0.4
    f = govde(g, x0, x0 + W, y0, y0 + D, ZL, ZL + H, st["dm"])
    c = f["on"]
    c.kutu(g, 0, c.L, 0, 0.05, ZL, ZL + 0.4, M("Siva", koyu(st["duvar"], 0.75)))
    ku = W * (0.25 if not sag else 0.75)
    kapi(g, c, ku, ZL + 0.4, 1.0, 2.25, st, rnd, sacak=True)
    for u in (W * 0.62, W * 0.88) if not sag else (W * 0.12, W * 0.38):
        pencere(g, c, u, ZL + 1.3, 1.2, 1.4, dict(st, lento=None), rnd)
    for u in (W * 0.2, W * 0.5, W * 0.8):
        pencere(g, c, u, ZL + 0.4 + kh + 0.8, 1.0, 1.3, dict(st, lento=None), rnd)
    if rnd.random() < 0.5:
        balkon(g, c, W * 0.35, W * 0.65, ZL + 0.4 + kh, 1.0, st, rnd, korkuluk=rnd.choice(("demir", "cam")))
    for ad in ("sol", "sag", "arka"):
        yan_cephe(g, f[ad], rnd, ZL + 0.4, 2, kh, st, 0.8, w=1.0, h=1.3)
    cm = M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit3"], "#5d6066", "#7a4a3a")))
    egim = rnd.uniform(0.7, 0.9)
    zr = besik_cati(g, x0, x0 + W, y0, y0 + D, ZL + H, egim, cm, st["dm"], sacak=0.45, yan=0.35)
    if rnd.random() < 0.5:
        cati_penceresi(g, x0 + W / 2, 1.4, y0 + D * 0.2, y0 + D / 2, zr, cm, st["dm"], rnd, st)
    else:
        gunes_paneli(g, x0 + 0.6, x0 + W - 0.6, y0 + 0.3, y0 + D * 0.42, zr)
    baca(g, x0 + W * 0.7, y0 + D * 0.65, ZL + H, zr(y0 + D * 0.65) + 0.9, M("Tugla", "#a65a44"))
    garaj(g, gx0, gx0 + gw, y0 + 0.5, y0 + 6.5, rnd, st["dm"])
    yama(g, gx0 + 0.3, gx0 + gw - 0.3, -LOT, y0 + 0.5, M("TasYol", "#bdb5a6"))
    yama(g, x0 + ku - 0.6, x0 + ku + 0.6, -LOT + 0.3, y0, M("TasYol", "#c9c0ae"))
    tip = rnd.choice(("cali", "tahta", "demir"))
    if tip == "cali":
        for a, b in ((-LOT, min(gx0, x0 + ku - 0.6)), (max(x0 + ku + 0.6, gx0 + gw) if not sag else x0 + ku + 0.6, LOT)):
            if b - a > 0.5:
                cit_cali(g, a, b, -LOT + 0.1, -LOT + 0.6, ZL, 1.1, rnd)
    else:
        on_cit(g, rnd, -LOT + 0.25, kapi_x=x0 + ku, tip=tip, x0=-LOT if not sag else gx0 + gw, x1=gx0 if not sag else LOT)
    if DETAY:
        if rnd.random() < 0.5:
            trambolin(g, rnd.uniform(-3, 3), LOT - 2.2)
        elif LOT - (y0 + D) > 3.5:
            havuz(g, -3.0, 2.0, y0 + D + 1.2, LOT - 1.0)
        agac(g, LOT - 1.5 if not sag else -LOT + 1.5, LOT - 1.5, ZL, rnd)
        agac(g, -LOT + 1.5 if not sag else LOT - 1.5, -LOT + 2.0, ZL, rnd, boy=4.5)
        for i in range(3):
            cali(g, x0 + 1.0 + i * 1.2, y0 - 0.6, ZL, 0.45, rnd)
    return g


def ev_bungalov(rnd):
    """Tek katlı L planlı bungalov: iki kanat, ahşap teras ve pergola, havuz ya da bahçe."""
    g = G("ev_bungalov")
    zemin(g, M("Cim", rnd.choice((SABIT["cim"], SABIT["cim2"]))))
    st = ev_st(rnd)
    st["tip"] = "modern"
    y0 = -LOT + 3.5
    H = 3.0
    cm = M("Kiremit", rnd.choice(("#5d6066", "#7a4a3a", SABIT["kiremit"], "#4a4e55")))
    # ana kanat (sokağa paralel)
    f = govde(g, -6.0, 3.0, y0, y0 + 6.0, ZL, ZL + 0.3 + H, st["dm"])
    c = f["on"]
    kapi(g, c, 2.0, ZL + 0.3, 1.0, 2.2, st, rnd, sacak=True)
    for u in (4.2, 6.8):
        pencere(g, c, u, ZL + 1.1, 1.6, 1.4, dict(st, lento=None), rnd)
    kirma_cati(g, -6.0, 3.0, y0, y0 + 6.0, ZL + 0.3 + H, 0.55, cm, sacak=0.6)
    # yan kanat (arkaya uzanan)
    f2 = govde(g, 3.0, 6.5, y0, y0 + 9.5, ZL, ZL + 0.3 + H, st["dm"])
    pencere(g, f2["on"], 1.75, ZL + 1.1, 1.6, 1.4, dict(st, lento=None), rnd)
    for u in (7.5, 8.6):
        pencere(g, f2["sag"], u, ZL + 1.1, 0.9, 1.4, dict(st, lento=None), rnd)
    kirma_cati(g, 3.0, 6.5, y0, y0 + 9.5, ZL + 0.3 + H, 0.55, cm, sacak=0.6)
    for ad in ("sol", "arka"):
        yan_cephe(g, f[ad], rnd, ZL + 0.3, 1, H, st, 1.0, w=1.3, h=1.3)
    # teras + pergola
    yama(g, -6.0, 3.0, y0 + 6.0, y0 + 9.0, M("Deck", "#b58a5e"), k=0.08)
    if DETAY:
        ah = M("Ahsap", "#8a5a3a")
        for x in (-5.8, -2.0, 2.0):
            cubuk(g, (x, y0 + 8.8, ZL + 0.08), (x, y0 + 8.8, ZL + 2.6), 0.14, ah)
        for i in range(9):
            x = -5.8 + i * 0.95
            kutu(g, x - 0.05, x + 0.05, y0 + 6.0, y0 + 9.0, ZL + 2.6, ZL + 2.75, ah)
        kutu(g, -5.9, 2.1, y0 + 8.75, y0 + 8.9, ZL + 2.45, ZL + 2.6, ah)
        dsil(g, -3.5, y0 + 7.4, ZL + 0.08, ZL + 0.78, 0.06, 6, M("Metal", "#3a3d42"))
        dsil(g, -3.5, y0 + 7.4, ZL + 0.78, ZL + 0.82, 0.6, 10, M("Masa", "#f2efe8"))
        for s in (-1, 1):
            kutu(g, -3.5 + s * 0.9 - 0.22, -3.5 + s * 0.9 + 0.22, y0 + 7.2, y0 + 7.6, ZL + 0.08, ZL + 0.5, M("Sandalye", "#3a3d42"))
        if rnd.random() < 0.5 and LOT - (y0 + 9.0) > 2.2:
            havuz(g, -5.5, -0.5, y0 + 9.6, LOT - 0.6)
        else:
            agac(g, -4.0, LOT - 1.6, ZL, rnd)
            cicek_tarhi(g, -1.5, 2.5, LOT - 1.4, LOT - 0.4, rnd)
        for x in (-5.5, -3.5):
            agac(g, x, -LOT + 1.8, ZL, rnd, boy=4.5, tip=rnd.choice(("yuvarlak", "cam")))
    yama(g, 1.5, 2.5, -LOT + 0.3, y0, M("TasYol", "#c9c0ae"))
    on_cit(g, rnd, -LOT + 0.25, kapi_x=2.0, tip=rnd.choice(("cali", "tas", "demir")))
    return g


def ev_modern_kup(rnd):
    """Modern küp ev: üst üste kaydırılmış iki kutu, ahşap kaplama, büyük camlar, düz çatı, kapalı otopark."""
    g = G("ev_modern_kup")
    zemin(g, M("Cim", SABIT["cim"]))
    beyaz = M("Siva", rnd.choice(("#f4f2ec", "#ebe8e1", "#e2e0da")))
    koyu_m = M("Panel", rnd.choice(("#3d4148", "#4a4e55", "#5a554e")))
    ah = M("Ahsap", rnd.choice(("#b07a4a", "#9a6a44", "#c49060")))
    st = dict(cerceve=M("Cerceve", "#2b2d31"), denizlik=None, tip="modern", cam="#a8c4d6")
    y0 = -LOT + 4.5
    f = govde(g, -5.5, 3.0, y0, y0 + 7.0, ZL, ZL + 3.1, beyaz)
    c = f["on"]
    c.kutu(g, 0.6, 5.0, 0, 0.04, ZL + 0.1, ZL + 2.9, M("Vitrin", "#cfe3ea"))
    if DETAY:
        for u in (1.7, 2.8, 3.9):
            c.kutu(g, u - 0.03, u + 0.03, 0, 0.06, ZL + 0.1, ZL + 2.9, st["cerceve"])
    kapi(g, c, 6.8, ZL + 0.1, 1.1, 2.4, dict(st, kapi=M("Kapi", "#2b2d31")), rnd)
    for ad in ("sol", "arka"):
        yan_cephe(g, f[ad], rnd, ZL, 1, 3.1, st, 1.0, w=1.6, h=1.6)
    duz_cati(g, -5.5, 3.0, y0, y0 + 7.0, ZL + 3.1, 0.2, beyaz, M("DuzCati", "#8f8a84"))
    # üst kutu (öne taşan)
    ux0, uy0 = -3.5, y0 - 1.2
    f2 = govde(g, ux0, ux0 + 8.0, uy0, uy0 + 6.5, ZL + 3.1, ZL + 6.0, koyu_m if rnd.random() < 0.5 else ah)
    c2 = f2["on"]
    c2.kutu(g, 0.5, 4.5, 0, 0.04, ZL + 3.5, ZL + 5.6, cam(rnd, "#a8c4d6"))
    if DETAY:
        for u in (1.5, 2.5, 3.5):
            c2.kutu(g, u - 0.03, u + 0.03, 0, 0.06, ZL + 3.5, ZL + 5.6, st["cerceve"])
        for i in range(int(3.0 / 0.18)):
            u = 5.0 + i * 0.18
            c2.kutu(g, u, u + 0.1, 0, 0.04, ZL + 3.1, ZL + 6.0, ah)
    for ad in ("sag", "sol", "arka"):
        c3 = f2[ad]
        c3.kutu(g, c3.L * 0.3, c3.L * 0.7, 0, 0.04, ZL + 3.6, ZL + 5.5, cam(rnd))
    duz_cati(g, ux0, ux0 + 8.0, uy0, uy0 + 6.5, ZL + 6.0, 0.25, beyaz, M("DuzCati", "#8f8a84"))
    # otopark sundurması
    kutu(g, 3.2, 6.8, -LOT + 1.0, y0 + 3.0, ZL + 2.6, ZL + 2.85, beyaz)
    if DETAY:
        for x in (3.4, 6.6):
            for y in (-LOT + 1.2, y0 + 2.8):
                cubuk(g, (x, y, ZL), (x, y, ZL + 2.6), 0.12, M("Metal", "#3a3d42"))
    yama(g, 3.2, 6.8, -LOT, y0 + 3.0, M("Cakil", "#d8d2c4"))
    yama(g, -0.5, 0.7, -LOT + 0.3, y0, M("TasYol", "#bdb5a6"))
    if DETAY:
        for i in range(4):
            cali(g, -5.0 + i * 1.1, -LOT + 0.8, ZL, 0.35, rnd, ton="#8a9a5a")
        agac(g, -5.5, LOT - 1.6, ZL, rnd, tip="kavak")
        agac(g, 1.5, LOT - 1.4, ZL, rnd)
        cit_cali(g, -LOT, LOT, LOT - 0.4, LOT - 0.1, ZL, 1.4, rnd)
    return g


def ev_ikiz(rnd):
    """İkiz ev: sokağa alınlığı bakan, ortadan bölünmüş iki ev (iki renk), arada çit."""
    g = G("ev_ikiz")
    zemin(g, M("Cim", rnd.choice((SABIT["cim"], SABIT["cim2"]))))
    y0 = -LOT + 3.0
    D = 9.0
    kh = 2.8
    H = 2 * kh + 0.3
    cm = M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit3"], "#5d6066")))
    st0 = ev_st(rnd)
    renk2 = rnd.choice([r for r in EV_RENK if r != st0["duvar"]])
    for i, s in enumerate((-1, 1)):
        st = dict(st0, duvar=renk2 if i else st0["duvar"], dm=M("Siva", renk2 if i else st0["duvar"]))
        x0 = -5.2 if s < 0 else 0.0
        f = govde(g, x0, x0 + 5.2, y0, y0 + D, ZL, ZL + H, st["dm"])
        c = f["on"]
        ku = 1.0 if s < 0 else 4.2
        kapi(g, c, ku, ZL + 0.3, 0.95, 2.2, st, rnd, sacak=True)
        pencere(g, c, 3.2 if s < 0 else 2.0, ZL + 1.2, 1.4, 1.35, dict(st, lento=None), rnd)
        for u in (1.4, 3.8):
            pencere(g, c, u, ZL + 0.3 + kh + 0.8, 1.0, 1.3, dict(st, lento=None), rnd)
        yan_cephe(g, f["arka"], rnd, ZL + 0.3, 2, kh, st, 0.9)
        yan_cephe(g, f["sol" if s < 0 else "sag"], rnd, ZL + 0.3, 2, kh, st, 0.7)
        yama(g, x0 + ku - 0.55, x0 + ku + 0.55, -LOT + 0.3, y0, M("TasYol", "#c9c0ae"))
        if DETAY:
            agac(g, x0 + (1.2 if s < 0 else 4.0), LOT - 1.5, ZL, rnd, boy=rnd.uniform(4, 6))
            cicek_tarhi(g, x0 + 0.3, x0 + 2.5, y0 - 1.1, y0 - 0.3, rnd) if s > 0 else cicek_tarhi(g, x0 + 2.7, x0 + 4.9, y0 - 1.1, y0 - 0.3, rnd)
    zr = besik_cati_y(g, -5.2, 5.2, y0, y0 + D, ZL + H, 0.8, cm, st0["dm"], sacak=0.4, yan=0.35)
    if DETAY:
        c = C((-5.2, y0), (1, 0), (0, -1), 10.4)
        for u in (3.0, 7.4):
            pencere(g, c, u, ZL + H + 0.6, 0.8, 1.0, dict(st0, lento=None, panjur=None, saksi=0), rnd)
        baca(g, 0, y0 + D * 0.5, ZL + H, ZL + H + 4.3, M("Tugla", "#a65a44"), w=0.7)
    cit_cali(g, -0.15, 0.15, y0 + D, LOT - 0.2, ZL, 1.2, rnd)
    cit_cali(g, -0.15, 0.15, -LOT + 0.6, y0, ZL, 0.9, rnd)
    on_cit(g, rnd, -LOT + 0.25, tip=rnd.choice(("tahta", "demir", "tas")), kapi_x=None, x0=-LOT, x1=-5.2 + 0.45)
    on_cit(g, rnd, -LOT + 0.25, tip="tahta", x0=-5.2 + 1.55, x1=4.2 - 0.55)
    on_cit(g, rnd, -LOT + 0.25, tip="tahta", x0=4.2 + 0.55, x1=LOT)
    return g


# ================================== BANLİYÖ / KÖY ==================================

def gemeskut(g, x, y, rnd):
    """Gémeskút: kuyu bilezikli, çatallı direk ve uzun kaldıraç (ova simgesi)."""
    ah = M("Ahsap", "#7a5a3a")
    dsil(g, x, y, ZL, ZL + 0.8, 0.6, 10, M("Tas", "#bdb4a4"), kapak=(False, False))
    dsil(g, x, y, ZL + 0.75, ZL + 0.78, 0.48, 10, M("Su", "#3a6a8a"))
    px, py = x + 2.2, y
    cubuk(g, (px, py, ZL), (px, py, ZL + 3.2), 0.22, ah)
    if not DETAY:
        return
    cubuk(g, (px, py, ZL + 3.2), (px - 0.2, py, ZL + 3.7), 0.1, ah)
    cubuk(g, (px, py, ZL + 3.2), (px + 0.2, py, ZL + 3.7), 0.1, ah)
    a, b = (x - 0.1, y, ZL + 5.0), (px + 2.4, py, ZL + 1.4)
    cubuk(g, a, b, 0.12, ah)
    kutu(g, px + 2.1, px + 2.7, py - 0.2, py + 0.2, ZL + 1.1, ZL + 1.6, M("Tas", "#8f8a84"))
    cubuk(g, a, (x, y, ZL + 1.2), 0.03, ah)
    dsil(g, x, y, ZL + 0.9, ZL + 1.25, 0.18, 8, M("Ahsap", "#5a4030"))


def gore(g, x, y, rnd):
    """Góré: çıtalı, yukarı genişleyen mısır ambarı, ayaklar üstünde, kiremit çatılı."""
    ah = M("Ahsap", "#9a7a52")
    L, w0, w1, z0, h = 4.0, 0.9, 1.4, ZL + 0.6, 2.4
    for xx in (x - L / 2 + 0.2, x + L / 2 - 0.2):
        for s in (-1, 1):
            kutu(g, xx - 0.15, xx + 0.15, y + s * w0 / 2 - 0.15, y + s * w0 / 2 + 0.15, ZL, z0, M("Tas", "#bdb4a4"))
    pts = [(x - L / 2, y - w0 / 2, z0), (x - L / 2, y + w0 / 2, z0), (x - L / 2, y + w1 / 2, z0 + h), (x - L / 2, y - w1 / 2, z0 + h)]
    prizma(g, pts, (L, 0, 0), M("Misir", "#e2b84a"))
    if DETAY:
        for i in range(int(h / 0.18)):
            z = z0 + 0.09 + i * 0.18
            ww = w0 + (w1 - w0) * (z - z0) / h
            for s in (-1, 1):
                kutu(g, x - L / 2, x + L / 2, y + s * (ww / 2 + 0.02) - 0.03, y + s * (ww / 2 + 0.02) + 0.03, z - 0.04, z + 0.04, ah)
    besik_cati(g, x - L / 2, x + L / 2, y - w1 / 2, y + w1 / 2, z0 + h, 0.8, M("Kiremit", SABIT["kiremit2"]), ah, sacak=0.3, yan=0.3, siralar=False)


def saman(g, x, y, r=0.75, yatik=True, aci=0.0):
    m = M("Saman", "#d9b860")
    if yatik:
        dx, dy = math.cos(aci) * 0.6, math.sin(aci) * 0.6
        silindir(g, (x - dx, y - dy, ZL + r), (x + dx, y + dy, ZL + r), r, r, 12, m, kapak_mat=M("Saman", "#c9a850"))
    else:
        dsil(g, x, y, ZL, ZL + 2.2, 1.4, 10, m)
        top(g, (x, y, ZL + 2.2), 1.4, 1.4, 1.1, m)


def banliyo_uzun_ev(rnd):
    """Uzun köy evi (parasztház): alınlığı sokağa bakan dar uzun ev, yan boyunca sütunlu tornác, avluda gémeskút, góré, bahçe."""
    g = G("banliyo_uzun_ev")
    zemin(g, M("Cim", rnd.choice((SABIT["cim"], SABIT["cim2"], "#9cc46a"))))
    renk = rnd.choice(("#f6f1e4", "#f3ecd6", "#eef0e8", "#f2e2c4", "#e8eef2"))
    dm = M("Siva", renk)
    st = dict(duvar=renk, dm=dm, trim=M("Korniz", "#ffffff"), cerceve=M("Cerceve", rnd.choice(("#3e6b4f", "#6b4a32", "#4f6b8a"))),
              denizlik=None, panjur=M("Panjur", rnd.choice(("#3e6b4f", "#6b4a32", "#4f6b8a"))), saksi=0.6, tip="klasik")
    W, L = 5.6, 13.0
    x0 = -LOT + 1.0
    y0 = -LOT + 0.6
    H = 3.0
    f = govde(g, x0, x0 + W, y0, y0 + L, ZL, ZL + H, dm)
    c = f["on"]
    c.kutu(g, 0, W, 0, 0.05, ZL, ZL + 0.55, M("Siva", rnd.choice(("#6b7a8a", "#8a6a5a", "#5a6b5a"))))
    for u in (1.5, 4.1):
        pencere(g, c, u, ZL + 1.0, 0.85, 1.25, st, rnd)
    cm = M("Kiremit", rnd.choice((SABIT["kiremit"], SABIT["kiremit2"], "#8a4a32")))
    # tornác: sağ yanda sütunlar, çatı uzar
    tw = 1.8
    zr = besik_cati_y(g, x0, x0 + W + tw, y0, y0 + L, ZL + H, 0.95, cm, dm, sacak=0.35, yan=0.4)
    if DETAY:
        c.kutu(g, W, W + tw, 0, 0.05, ZL + H, ZL + H + 0.1, st["trim"])
        cc = C((x0 + W / 2 + tw / 2 - (W + tw) / 2, y0), (1, 0), (0, -1), W + tw)
        cc.kutu(g, (W + tw) / 2 - 0.25, (W + tw) / 2 + 0.25, 0, 0.05, ZL + H + 1.4, ZL + H + 1.8, M("Ahsap", "#6b4a32"))
    kutu(g, x0 + W, x0 + W + tw, y0 + 0.5, y0 + L, ZL, ZL + 0.35, M("Tas", "#cfc7b8"))
    kutu(g, x0 + W + 0.1, x0 + W + tw, y0, y0 + L, ZL + H - 0.35, ZL + H, dm, atla=("zn", "xn"))
    for i in range(6):
        y = y0 + 0.6 + i * (L - 1.2) / 5
        kutu(g, x0 + W + tw - 0.45, x0 + W + tw - 0.1, y - 0.18, y + 0.18, ZL + 0.35, ZL + H - 0.35, st["trim"])
    sc = f["sag"]
    for k, u in enumerate((2.0, 4.5, 7.0, 9.5, 11.5)):
        if k == 1:
            kapi(g, sc, u, ZL + 0.35, 1.0, 2.2, st, rnd, z0=ZL + 0.35)
        else:
            pencere(g, sc, u, ZL + 1.0, 0.85, 1.25, st, rnd)
    yan_cephe(g, f["sol"], rnd, ZL, 1, H, st, 0.6)
    if DETAY:
        c.kutu(g, 2.4, 3.2, 0, 0.05, ZL + H + 1.2, ZL + H + 1.9, M("Ahsap", "#5a4030"))
        for i in range(4):
            c.kutu(g, 2.45, 3.15, 0.05, 0.07, ZL + H + 1.25 + i * 0.17, ZL + H + 1.3 + i * 0.17, M("Ahsap", "#3a2a20"))
        baca(g, x0 + W / 2, y0 + L * 0.45, ZL + H, zr(x0 + W / 2) + 0.6, M("Siva", "#f4f0e6"))
    # avlu
    ax = x0 + W + tw + 0.3
    yama(g, ax, LOT - 0.3, -LOT + 0.3, -LOT + 7.0, M("Toprak", "#b8a078"), k=0.01)
    cit_tahta(g, (-LOT, -LOT + 0.15, 0), (x0, -LOT + 0.15, 0), ZL, 1.2, "#8a6a4a")
    cit_tahta(g, (ax, -LOT + 0.15, 0), (LOT - 3.0, -LOT + 0.15, 0), ZL, 1.2, "#8a6a4a")
    if DETAY:
        km = M("Kapi", rnd.choice(("#3e6b4f", "#6b4a32")))
        kutu(g, LOT - 3.0, LOT - 0.3, -LOT + 0.1, -LOT + 0.2, ZL, ZL + 1.8, km)
        kutu(g, x0 + W + 0.2, ax, -LOT + 0.1, -LOT + 0.2, ZL, ZL + 1.6, km)
        gemeskut(g, LOT - 4.5, -LOT + 4.2, rnd)
        gore(g, LOT - 2.6, 1.5, rnd) if rnd.random() < 0.7 else saman(g, LOT - 2.5, 1.5, yatik=False)
        sebze_tarhi(g, ax + 0.3, LOT - 4.5, 3.5, LOT - 0.4, rnd)
        for y in (-LOT + 2.5, 0.0):
            agac(g, ax + 1.0, y, ZL, rnd, boy=rnd.uniform(4, 5.5), tip="yuvarlak")
        for i in range(4):
            top(g, (rnd.uniform(ax + 2, LOT - 1), rnd.uniform(-LOT + 1, -LOT + 6), ZL + 0.2), 0.18, 0.18, 0.2, M("Tavuk", rnd.choice(("#f4f0e6", "#c98a4a", "#3a2a20"))))
    return g


def ahir(g, x0, x1, y0, y1, rnd, renk=None):
    renk = renk or rnd.choice(("#8a3a2a", "#6b4a32", "#7a5a3a", "#5a4a3a"))
    m = M("Ahsap", renk)
    H = 4.0
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, m)
    c = f["on"]
    c.kutu(g, c.L / 2 - 1.6, c.L / 2 + 1.6, 0, 0.05, ZL, ZL + 3.4, M("Kapi", koyu(renk, 0.7)))
    if DETAY:
        for s in (-1, 1):
            cubuk(g, c.p(c.L / 2 + s * 1.6, 0.08, ZL), c.p(c.L / 2 - s * 0.0, 0.08, ZL + 3.4), 0.12, M("Korniz", "#f4f0e6"))
        c.kutu(g, c.L / 2 - 1.75, c.L / 2 + 1.75, 0, 0.09, ZL + 3.4, ZL + 3.55, M("Korniz", "#f4f0e6"))
        for ad in f:
            cc = f[ad]
            for i in range(int(cc.L / 0.35)):
                u = 0.15 + i * 0.35
                cc.kutu(g, u, u + 0.06, 0, 0.03, ZL, ZL + H, M("Ahsap", koyu(renk, 0.85)))
    besik_cati_y(g, x0, x1, y0, y1, ZL + H, 0.85, M("Cati", rnd.choice(("#5d6066", "#8a4a32", "#6b7078"))), m, sacak=0.4, yan=0.3)
    if DETAY:
        c.kutu(g, c.L / 2 - 0.5, c.L / 2 + 0.5, 0, 0.05, ZL + H + 0.6, ZL + H + 1.4, M("Kapi", koyu(renk, 0.7)))


def traktor(g, x, y, rnd, aci=0.0):
    if not DETAY:
        return
    rk = M("Arac", rnd.choice(("#c0392b", "#2e7d3a", "#2f5a9a")))
    lm = M("Lastik", "#2b2d31")
    kutu(g, x - 1.0, x + 1.2, y - 0.5, y + 0.5, ZL + 0.6, ZL + 1.4, rk)
    kutu(g, x - 1.1, x - 0.1, y - 0.55, y + 0.55, ZL + 1.4, ZL + 2.6, M("Cam", "#a9c9de"))
    kutu(g, x - 1.15, x - 0.05, y - 0.6, y + 0.6, ZL + 2.6, ZL + 2.7, rk)
    for yy in (y - 0.7, y + 0.5):
        silindir(g, (x - 0.6, yy, ZL + 0.75), (x - 0.6, yy + 0.2, ZL + 0.75), 0.75, 0.75, 12, lm)
        silindir(g, (x + 0.85, yy + 0.05, ZL + 0.45), (x + 0.85, yy + 0.15, ZL + 0.45), 0.45, 0.45, 10, lm)
    cubuk(g, (x + 0.8, y + 0.3, ZL + 1.4), (x + 0.8, y + 0.3, ZL + 2.3), 0.08, M("Metal", "#3a3a3a"))


def banliyo_ciftlik(rnd):
    """Çiftlik: kırmızı ahşap ahır, küçük çiftlik evi, saman balyaları, traktör, góré, çit."""
    g = G("banliyo_ciftlik")
    zemin(g, M("Cim", rnd.choice(("#9cc46a", SABIT["cim2"]))))
    ahir(g, -LOT + 0.8, -LOT + 7.0, -1.0, LOT - 0.8, rnd)
    st = ev_st(rnd)
    f = govde(g, 1.0, 6.6, -LOT + 1.5, -LOT + 7.0, ZL, ZL + 3.0, st["dm"])
    kapi(g, f["on"], 1.5, ZL + 0.3, 0.95, 2.2, st, rnd, sacak=True)
    pencere(g, f["on"], 4.0, ZL + 1.0, 1.0, 1.3, dict(st, lento=None), rnd)
    yan_cephe(g, f["sol"], rnd, ZL, 1, 3.0, st, 1.0)
    kirma_cati(g, 1.0, 6.6, -LOT + 1.5, -LOT + 7.0, ZL + 3.0, 0.85, M("Kiremit", SABIT["kiremit"]), sacak=0.4)
    baca(g, 3.8, -LOT + 4.2, ZL + 3.0, ZL + 6.2, M("Tugla", "#a65a44"))
    yama(g, -LOT + 0.8, LOT - 0.5, -LOT + 7.4, -1.2, M("Toprak", "#b8a078"), k=0.01)
    if DETAY:
        for i in range(rnd.randint(3, 5)):
            saman(g, rnd.uniform(1.5, LOT - 1.0), rnd.uniform(0.5, LOT - 1.0), aci=rnd.uniform(0, 3))
        traktor(g, -1.5, -3.5, rnd)
        cit_tahta(g, (-LOT, -LOT + 0.15, 0), (LOT, -LOT + 0.15, 0), ZL, 1.1, "#8a6a4a")
        agac(g, LOT - 1.2, -LOT + 2.0, ZL, rnd)
        if rnd.random() < 0.5:
            gore(g, 3.5, LOT - 1.5, rnd)
    return g


def banliyo_bag_evi(rnd):
    """Bağ evi (présház): beyaz küçük ev, mahzen kapısı, sıra sıra asma kütükleri."""
    g = G("banliyo_bag_evi")
    zemin(g, M("Cim", "#a8c46a"))
    dm = M("Siva", rnd.choice(("#f6f1e4", "#f3ecd6", "#efe6d2")))
    st = dict(duvar="#f6f1e4", dm=dm, trim=M("Korniz", "#ffffff"), cerceve=M("Cerceve", "#6b4a32"), denizlik=None,
              panjur=M("Panjur", "#6b4a32"), saksi=0.3, tip="klasik")
    f = govde(g, -LOT + 1.0, -LOT + 5.5, -LOT + 1.0, -LOT + 7.5, ZL, ZL + 2.8, dm)
    c = f["on"]
    kapi(g, c, 2.25, ZL, 1.4, 2.2, dict(st, kapi=M("Kapi", "#5a3a2a")), rnd)
    if DETAY:
        c.yarim_disk(g, 2.25, ZL + 2.2, 0.7, 0, 0.05, M("Kapi", "#5a3a2a"), n=6)
    pencere(g, f["sag"], 2.0, ZL + 1.0, 0.7, 0.9, st, rnd)
    pencere(g, f["sag"], 4.5, ZL + 1.0, 0.7, 0.9, st, rnd)
    besik_cati_y(g, -LOT + 1.0, -LOT + 5.5, -LOT + 1.0, -LOT + 7.5, ZL + 2.8, 1.0, M("Kiremit", SABIT["kiremit2"]), dm, sacak=0.35, yan=0.3)
    # asma sıraları
    yama(g, -0.5, LOT - 0.4, -LOT + 0.6, LOT - 0.4, M("Toprak", "#9a7a52"), k=0.01)
    n = 6
    for i in range(n):
        x = 0.2 + i * (LOT - 0.9) / (n - 1)
        if DETAY:
            for y in (-LOT + 0.8, 0.0, LOT - 0.8):
                cubuk(g, (x, y, ZL), (x, y, ZL + 1.7), 0.08, M("Ahsap", "#6b4a32"))
            cubuk(g, (x, -LOT + 0.8, ZL + 1.5), (x, LOT - 0.8, ZL + 1.5), 0.02, M("Metal", "#9aa0a6"))
            for k in range(14):
                y = -LOT + 1.2 + k * (2 * LOT - 2.4) / 13
                top(g, (x, y, ZL + 1.2), 0.38, 0.45, 0.45, M("Yaprak", rnd.choice(("#6a9a3f", "#7fae4f", "#5a8a3a"))), rnd, 0.15)
        else:
            kutu(g, x - 0.35, x + 0.35, -LOT + 0.8, LOT - 0.8, ZL, ZL + 1.6, M("Yaprak", "#6a9a3f"))
    if DETAY:
        agac(g, -LOT + 2.0, LOT - 2.0, ZL, rnd, boy=6.0)
        agac(g, -LOT + 4.0, 3.0, ZL, rnd, boy=4.5)
        kutu(g, -LOT + 1.5, -LOT + 3.5, 1.0, 1.5, ZL, ZL + 0.45, M("Ahsap", "#8a6a4a"))
    return g


# ================================== KAMPÜS ==================================

def bayrak_direkleri(g, x0, y, n, rnd, aralik=0.9, h=7.0):
    if not DETAY:
        return
    renkler = [("#cd2a3e", "#ffffff", "#436f4d"), ("#2f4f8a", "#f2c94c"), ("#ffffff", "#2f4f8a")]
    for i in range(n):
        x = x0 + i * aralik
        cubuk(g, (x, y, ZL), (x, y, ZL + h), 0.07, M("Metal", "#d9dcdf"))
        rr = renkler[i % len(renkler)]
        for k, r in enumerate(rr):
            kutu(g, x + 0.04, x + 1.1, y - 0.02, y + 0.02, ZL + h - 0.3 - (k + 1) * 0.8 / len(rr), ZL + h - 0.3 - k * 0.8 / len(rr), M("Bayrak", r))


def bank(g, x, y, rnd, aci=0.0, renk=None):
    """Park bankı (yerel): ayaklar + oturma + sırt çıtaları."""
    m = M("Bank", renk or "#3f6b45")
    ca, sa = math.cos(aci), math.sin(aci)

    def P(u, v, z):
        return (x + u * ca - v * sa, y + u * sa + v * ca, z)
    for u in (-0.75, 0.75):
        obox(g, P(u, 0, ZL + 0.22), (0.03 * ca, 0.03 * sa, 0), (-0.22 * sa, 0.22 * ca, 0), (0, 0, 0.22), M("Demir", SABIT["demir"]))
    for k in range(3):
        obox(g, P(0, -0.15 + k * 0.15, ZL + 0.46), (0.9 * ca, 0.9 * sa, 0), (-0.06 * sa, 0.06 * ca, 0), (0, 0, 0.025), m)
    if DETAY:
        for k in range(2):
            obox(g, P(0, 0.25, ZL + 0.65 + k * 0.17), (0.9 * ca, 0.9 * sa, 0), (-0.02 * sa, 0.02 * ca, 0), (0, 0, 0.06), m)


def kampus_fakulte(rnd):
    """Fakülte binası: revaklı (sütunlu) giriş, merdivenler, alınlık, üç kat, bayrak direkleri ve çimen."""
    g = G("kampus_fakulte")
    zemin(g, M("Cim", SABIT["cim"]))
    renk = rnd.choice(("#e8dcc4", "#d9c8a8", "#c97a5a", "#e2d6c2"))
    tugla = renk == "#c97a5a"
    dm = M("Tugla" if tugla else "Siva", renk)
    trim = M("Korniz", "#f4efe4")
    st = dict(duvar=renk, dm=dm, trim=trim, cerceve=M("Cerceve", "#f4f4f2"), denizlik=M("Tas", "#e1dbcf"), panjur=None, saksi=0,
              tip="klasik", cam="#a9c9de")
    kat = 3
    kh = 3.6
    y0 = -LOT + 4.0
    D = 9.5
    H = kat * kh
    f = govde(g, -LOT + 0.3, LOT - 0.3, y0, y0 + D, ZL, ZL + 0.6 + H, dm)
    c = f["on"]
    c.kutu(g, 0, c.L, 0, 0.06, ZL, ZL + 0.6, M("Tas", "#cfc7b8"))
    us, _ = eksenler(c.L, 1.7, 0.4)
    for k in range(kat):
        for u in us:
            if k == 0 and abs(u - c.L / 2) < 2.6:
                continue
            pencere(g, c, u, ZL + 0.6 + k * kh + 0.9, 1.1, 2.0, dict(st, lento="duz" if k else "kilit"), rnd)
    korniz(g, c, ZL + 0.6 + H - 0.5, 0.5, 0.45, trim, tasma=0.1)
    for ad in ("sol", "sag", "arka"):
        yan_cephe(g, f[ad], rnd, ZL + 0.6, kat, kh, st, 0.95, w=1.1, h=1.9)
    kirma_cati(g, -LOT + 0.3, LOT - 0.3, y0, y0 + D, ZL + 0.6 + H, 0.35, M("Cinko", rnd.choice(("#7f8a8f", "#6fa58a", "#8a4a32"))), sacak=0.2)
    # revak
    rw = 5.6
    rx0 = -rw / 2
    for i in range(4):
        kutu(g, rx0 - 0.3 * i - 0.3, rw / 2 + 0.3 * i + 0.3, y0 - 0.4 - 0.4 * (4 - i), y0, ZL + i * 0.15, ZL + (i + 1) * 0.15, M("Tas", "#e1dbcf"))
    zr = ZL + 0.6
    for i in range(5):
        x = rx0 + i * rw / 4
        dsil(g, x, y0 - 1.6, zr, zr + 2 * kh - 0.5, 0.3, 10, trim)
        if DETAY:
            kutu(g, x - 0.38, x + 0.38, y0 - 1.98, y0 - 1.22, zr, zr + 0.3, trim)
            kutu(g, x - 0.38, x + 0.38, y0 - 1.98, y0 - 1.22, zr + 2 * kh - 0.6, zr + 2 * kh - 0.35, trim)
    zt = zr + 2 * kh - 0.35
    kutu(g, rx0 - 0.6, rw / 2 + 0.6, y0 - 2.2, y0, zt, zt + 0.7, trim, atla=("zn", "yp"))
    cc = C((rx0 - 0.6, y0 - 2.2), (1, 0), (0, -1), rw + 1.2)
    prizma(g, [cc.p(0, 0, zt + 0.7), cc.p(rw + 1.2, 0, zt + 0.7), cc.p((rw + 1.2) / 2, 0, zt + 2.2)], (0, 2.2, 0), trim)
    if DETAY:
        yazi_seridi(g, cc, 0.8, rw + 0.4, zt + 0.35, 0.35, "#c9a24a", rnd, o=0.0)
    kapi(g, c, c.L / 2, ZL + 0.6, 2.0, 3.0, dict(st, kapi=M("Kapi", "#4a2c1e")), rnd, z0=ZL + 0.6)
    # bahçe
    yama(g, -2.0, 2.0, -LOT + 0.2, y0 - 2.0, M("TasYol", "#d6cdbb"))
    if DETAY:
        bayrak_direkleri(g, 3.2, -LOT + 1.0, 3, rnd)
        for x in (-5.5, -3.8):
            agac(g, x, -LOT + 1.8, ZL, rnd, boy=6.0)
        bank(g, 3.0, -LOT + 2.6, rnd)
        bank(g, -3.0, LOT - 1.0, rnd)
        cicek_tarhi(g, -LOT + 0.5, -2.3, y0 - 1.0, y0 - 0.3, rnd)
        cicek_tarhi(g, 2.3, LOT - 0.5, y0 - 1.0, y0 - 0.3, rnd)
    return g


def kampus_kutuphane(rnd):
    """Kütüphane: cam kütle, ahşap kanatlar, çatıda kubbe ya da konsol saçak; önünde okuma çimeni ve banklar."""
    g = G("kampus_kutuphane")
    zemin(g, M("Cim", SABIT["cim"]))
    y0 = -LOT + 3.5
    W, D = 12.0, 9.0
    x0 = -W / 2
    H = 7.6
    beton = M("Beton", rnd.choice(("#e6e2da", "#d9d4ca")))
    ah = M("Ahsap", rnd.choice(("#b07a4a", "#c49060")))
    f = govde(g, x0, x0 + W, y0, y0 + D, ZL, ZL + H, beton)
    for ad in ("on", "sol", "sag"):
        c = f[ad]
        c.kutu(g, 0.6, c.L - 0.6, 0, 0.04, ZL + 0.3, ZL + H - 0.8, M("Vitrin", "#c8dfe6"))
        if DETAY:
            for i in range(int((c.L - 1.2) / 0.55) + 1):
                u = 0.6 + i * 0.55
                c.kutu(g, u - 0.07, u + 0.07, 0, 0.45, ZL + 0.3, ZL + H - 0.8, ah)
            c.kutu(g, 0.6, c.L - 0.6, 0, 0.2, ZL + H / 2 - 0.1, ZL + H / 2 + 0.1, beton)
    kapi(g, f["on"], W / 2, ZL, 2.2, 2.8, dict(kapi=M("Vitrin", "#c8dfe6"), cerceve=M("Kasa", "#3a3d42")), rnd)
    zt = ZL + H
    if rnd.random() < 0.5:
        duz_cati(g, x0, x0 + W, y0, y0 + D, zt, 0.3, beton, M("DuzCati", "#8f8a84"))
        dsil(g, 0, y0 + D / 2, zt, zt + 1.0, 3.0, 16, beton)
        top(g, (0, y0 + D / 2, zt + 1.0), 3.0, 3.0, 2.2, M("OfisCam", "#b9d4dc"))
        if DETAY:
            for i in range(8):
                a = i * math.pi / 4
                cubuk(g, (math.cos(a) * 3.0, y0 + D / 2 + math.sin(a) * 3.0, zt + 1.0), (0, y0 + D / 2, zt + 3.2), 0.08, M("Kasa", "#5a6068"))
    else:
        kutu(g, x0 - 1.5, x0 + W + 1.5, y0 - 1.8, y0 + D + 0.6, zt, zt + 0.5, beton)
        if DETAY:
            for x in (x0 - 1.0, x0 + W + 1.0):
                cubuk(g, (x, y0 - 1.3, ZL), (x, y0 - 1.3, zt), 0.25, beton)
    yama(g, -1.5, 1.5, -LOT + 0.2, y0, M("TasYol", "#d6cdbb"))
    if DETAY:
        for x in (-5.0, 5.0):
            bank(g, x, -LOT + 1.8, rnd, renk="#8a5a3a")
            agac(g, x * 1.25, LOT - 1.5, ZL, rnd)
        for x in (-3.0, 3.0):
            saksi_cicek(g, x, -LOT + 0.8, ZL, rnd, r=0.45)
        # bisiklet park
        for i in range(5):
            x = 2.5 + i * 0.6
            cubuk(g, (x, y0 - 1.0, ZL), (x, y0 - 1.0, ZL + 0.8), 0.05, M("Metal", "#9aa0a6"))
            cubuk(g, (x, y0 - 0.5, ZL), (x, y0 - 0.5, ZL + 0.8), 0.05, M("Metal", "#9aa0a6"))
            cubuk(g, (x, y0 - 1.0, ZL + 0.8), (x, y0 - 0.5, ZL + 0.8), 0.05, M("Metal", "#9aa0a6"))
    return g


def kampus_yurt(rnd):
    """Öğrenci yurdu: tuğla, uzun pencere dizileri, renkli giriş kutusu, bisiklet parkı."""
    g = G("kampus_yurt")
    zemin(g, M("Cim", SABIT["cim2"]))
    tugla = M("Tugla", rnd.choice(("#b5654a", "#a65a44", "#c47a5a", "#9a5040")))
    vurgu = M("Panel", rnd.choice(("#e0b23a", "#2f7d8c", "#d9822b", "#5a8f4a")))
    st = dict(cerceve=M("Cerceve", "#f4f4f2"), denizlik=M("Beton", "#e8e3d8"), tip="kare", cam="#a9c9de")
    kat = rnd.choice((5, 6))
    kh = 3.0
    y0 = -LOT + 2.5
    D = 10.0
    f = govde(g, -LOT + 0.4, LOT - 0.4, y0, y0 + D, ZL, ZL + kat * kh + 0.4, tugla)
    for ad in ("on", "arka"):
        c = f[ad]
        us, _ = eksenler(c.L, 1.6, 0.4)
        for k in range(kat):
            for i, u in enumerate(us):
                if ad == "on" and k == 0 and i in (len(us) // 2 - 1, len(us) // 2):
                    continue
                pencere(g, c, u, ZL + 0.4 + k * kh + 0.85, 1.0, 1.5, st, rnd)
            if DETAY:
                c.kutu(g, 0, c.L, 0, 0.05, ZL + 0.4 + k * kh - 0.08, ZL + 0.4 + k * kh + 0.08, M("Beton", "#e8e3d8"))
    c = f["on"]
    c.kutu(g, c.L / 2 - 2.2, c.L / 2 + 2.2, 0, 1.6, ZL, ZL + 3.6, vurgu)
    cc = C(c.p(c.L / 2 - 2.2, 1.6, 0)[:2], (1, 0), (0, -1), 4.4)
    cc.kutu(g, 0.5, 3.9, 0, 0.04, ZL + 0.1, ZL + 2.8, M("Vitrin", "#cfe3ea"))
    yazi_seridi(g, cc, 0.4, 4.0, ZL + 3.2, 0.4, "#ffffff", rnd, o=0.0)
    for ad in ("sol", "sag"):
        yan_cephe(g, f[ad], rnd, ZL + 0.4, kat, kh, st, 0.8, w=1.0, h=1.5)
    duz_cati(g, -LOT + 0.4, LOT - 0.4, y0, y0 + D, ZL + kat * kh + 0.4, 0.7, tugla, M("DuzCati", "#7f7a74"))
    cati_ustu(g, -LOT + 0.4, LOT - 0.4, y0, y0 + D, ZL + kat * kh + 0.4, rnd)
    yama(g, -2.0, 2.0, -LOT + 0.2, y0 - 1.6, M("TasYol", "#d6cdbb"))
    if DETAY:
        m = M("Metal", "#9aa0a6")
        for i in range(7):
            x = -6.0 + i * 0.55
            cubuk(g, (x, -LOT + 0.8, ZL), (x, -LOT + 0.8, ZL + 0.75), 0.05, m)
            cubuk(g, (x, -LOT + 1.4, ZL), (x, -LOT + 1.4, ZL + 0.75), 0.05, m)
            cubuk(g, (x, -LOT + 0.8, ZL + 0.75), (x, -LOT + 1.4, ZL + 0.75), 0.05, m)
        kutu(g, -6.3, -2.4, -LOT + 0.5, -LOT + 1.7, ZL + 2.2, ZL + 2.3, M("Panel", "#6b7078"))
        for x in (-6.2, -2.5):
            cubuk(g, (x, -LOT + 1.6, ZL), (x, -LOT + 1.6, ZL + 2.2), 0.08, m)
        agac(g, 4.5, -LOT + 1.6, ZL, rnd)
        agac(g, 0, LOT - 1.3, ZL, rnd)
    return g


def kampus_lab(rnd):
    """Laboratuvar: testere dişi tepe pencereli çatı, havalandırma bacaları, yanında sera."""
    g = G("kampus_lab")
    zemin(g, M("TasAvlu", "#d6cfc2"))
    dm = M("Panel", rnd.choice(("#e4e2dc", "#d6d8da", "#e8e0d0")))
    vurgu = M("Panel", rnd.choice(("#2f7d8c", "#5a8f4a", "#c0473a", "#3d5a8a")))
    y0 = -LOT + 2.0
    W, D, H = 9.0, 10.0, 6.5
    x0 = -LOT + 0.5
    f = govde(g, x0, x0 + W, y0, y0 + D, ZL, ZL + H, dm)
    c = f["on"]
    c.kutu(g, 0, c.L, 0, 0.05, ZL + H - 1.2, ZL + H, vurgu)
    for k in range(2):
        c.kutu(g, 0.5, c.L - 0.5, 0, 0.04, ZL + 0.8 + k * 3.0, ZL + 2.4 + k * 3.0, cam(rnd))
        if DETAY:
            for u in eksenler(c.L - 1.0, 1.2)[0]:
                c.kutu(g, 0.5 + u - 0.6 - 0.04, 0.5 + u - 0.6 + 0.04, 0, 0.07, ZL + 0.8 + k * 3.0, ZL + 2.4 + k * 3.0, M("Kasa", "#3a3d42"))
    kapi(g, c, 1.5, ZL, 1.4, 2.4, dict(kapi=cam(rnd), cerceve=M("Kasa", "#3a3d42")), rnd)
    for ad in ("sol", "arka"):
        yan_cephe(g, f[ad], rnd, ZL, 2, 3.0, dict(cerceve=M("Cerceve", "#3a3d42"), denizlik=None, tip="modern"), 1.0, w=1.6, h=1.3)
    # testere dişi çatı
    zt = ZL + H
    n = 4
    for i in range(n):
        ya, yb = y0 + D * i / n, y0 + D * (i + 1) / n
        pts = [(x0, ya, zt), (x0, yb, zt), (x0, yb, zt + 1.6)]
        prizma(g, pts, (W, 0, 0), M("Cati", "#6b7078"))
        kutu(g, x0 + 0.2, x0 + W - 0.2, yb - 0.05, yb + 0.01, zt + 0.15, zt + 1.5, M("OfisCam", "#b9d4dc"))
    if DETAY:
        for x in (x0 + 2.0, x0 + 5.0, x0 + 7.5):
            dsil(g, x, y0 + D * 0.3, zt + 1.0, zt + 3.4, 0.3, 10, M("Metal", "#c7c9cc"))
            dsil(g, x, y0 + D * 0.3, zt + 3.4, zt + 3.6, 0.45, 10, M("Metal", "#8f9499"))
    # sera
    sx0, sx1 = x0 + W + 0.6, LOT - 0.4
    sy0, sy1 = y0 + 1.0, y0 + 8.0
    gm = M("Sera", "#d4ecea")
    kutu(g, sx0, sx1, sy0, sy1, ZL, ZL + 2.6, gm)
    besik_cati_y(g, sx0, sx1, sy0, sy1, ZL + 2.6, 0.6, gm, gm, sacak=0.05, yan=0.05, siralar=False)
    if DETAY:
        km = M("Kasa", "#f4f4f2")
        for y in [sy0 + i * (sy1 - sy0) / 5 for i in range(6)]:
            kutu(g, sx0 - 0.04, sx1 + 0.04, y - 0.04, y + 0.04, ZL, ZL + 2.62, km)
        for i in range(4):
            top(g, (rnd.uniform(sx0 + 0.6, sx1 - 0.6), rnd.uniform(sy0 + 0.6, sy1 - 0.6), ZL + 0.5), 0.45, 0.45, 0.45, M("Yaprak", "#5f9a4f"))
        agac(g, LOT - 1.5, -LOT + 1.2, ZL, rnd, boy=4.5)
    return g


def kampus_spor(rnd):
    """Spor salonu: tonoz çatılı salon, yanında çizgili basketbol sahası."""
    g = G("kampus_spor")
    zemin(g, M("Cim", SABIT["cim"]))
    dm = M("Panel", rnd.choice(("#d9dcdf", "#e4ddd0", "#c9d4d8")))
    vurgu = M("Panel", rnd.choice(("#c0473a", "#2f7d8c", "#e0b23a")))
    x0, x1, y0, y1 = -LOT + 0.4, LOT - 0.4, 0.5, LOT - 0.4
    H = 5.0
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, dm)
    c = f["on"]
    c.kutu(g, 0.4, c.L - 0.4, 0, 0.04, ZL + 3.4, ZL + H - 0.3, M("OfisCam", "#b9d4dc"))
    c.kutu(g, 0, c.L, 0, 0.05, ZL, ZL + 1.0, vurgu)
    kapi(g, c, c.L / 2, ZL, 2.0, 2.6, dict(kapi=cam(rnd), cerceve=M("Kasa", "#3a3d42")), rnd, sacak=True)
    # tonoz
    n = 10
    R = (x1 - x0) / 2
    ym = (y0 + y1) / 2
    hh = 2.4
    tm = M("Cati", rnd.choice(("#7f8a8f", "#9aa3a8", "#6b7078")))
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        p0 = ((x0 + x1) / 2 - R * math.cos(a0), ZL + H + hh * math.sin(a0))
        p1 = ((x0 + x1) / 2 - R * math.cos(a1), ZL + H + hh * math.sin(a1))
        pts = [(p0[0], y0 - 0.3, p0[1]), (p1[0], y0 - 0.3, p1[1]), (p1[0], y1 + 0.3, p1[1]), (p0[0], y1 + 0.3, p0[1])]
        q = [add(p, (0, 0, -0.15)) for p in pts]
        kati(g, pts + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], tm)
    for yy in (y0, y1):
        taban = [((x0 + x1) / 2 - R * math.cos(math.pi * i / n), yy, ZL + H + hh * math.sin(math.pi * i / n)) for i in range(n + 1)]
        prizma(g, taban, (0, 0.01 if yy == y0 else -0.01, 0), dm)
    # saha
    yama(g, -LOT + 0.5, LOT - 0.5, -LOT + 0.5, -0.4, M("Saha", "#c8643a"), k=0.02)
    if DETAY:
        w = M("Serit", "#f4f4ec")
        for a, b, c_, d in ((-6.0, 6.0, -LOT + 0.8, -LOT + 0.9), (-6.0, 6.0, -0.8, -0.7), (-6.0, -5.9, -LOT + 0.8, -0.7),
                            (5.9, 6.0, -LOT + 0.8, -0.7), (-0.05, 0.05, -LOT + 0.8, -0.7)):
            yama(g, a, b, c_, d, w, z=ZL + 0.02, k=0.008)
        for x in (-5.4, 5.4):
            s = 1 if x > 0 else -1
            cubuk(g, (x + s * 0.6, -3.7, ZL), (x + s * 0.6, -3.7, ZL + 3.0), 0.1, M("Metal", "#5a5e63"))
            kutu(g, x - 0.05 * s, x + 0.05 * s, -4.3, -3.1, ZL + 2.7, ZL + 3.5, M("Panel", "#f4f4f2"))
            dsil(g, x - s * 0.25, -3.7, ZL + 2.95, ZL + 3.0, 0.23, 10, M("Pota", "#e05a2a"), kapak=(False, False))
        cit_parmak(g, (-LOT + 0.3, -LOT + 0.3, 0), (LOT - 0.3, -LOT + 0.3, 0), ZL, 2.2, "#5a5e63", aralik=0.3)
    return g


# ================================== SANAYİ ==================================

def sanayi_testere(rnd):
    """Tuğla fabrika: testere dişi çatı, yüksek tuğla baca, kemerli pencereler, yükleme kapısı."""
    g = G("sanayi_testere")
    zemin(g, M("Beton", "#a9a49a"))
    tugla = M("Tugla", rnd.choice(("#a65a44", "#b5654a", "#8f4a3a", "#9a5a48")))
    trim = M("Tas", "#d8d0c0")
    st = dict(cerceve=M("Cerceve", "#3a4a3a"), denizlik=trim, tip="kare", cam="#9fb8c8")
    x0, x1, y0, y1 = -LOT + 0.4, LOT - 0.4, -LOT + 2.0, LOT - 0.4
    H = 6.0
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, tugla)
    for ad in ("on", "sol", "sag"):
        c = f[ad]
        us, _ = eksenler(c.L, 2.2, 0.6)
        for u in us:
            pencere(g, c, u, ZL + 1.6, 1.4, 3.2, dict(st, tip="kemer", lento=None), rnd)
        if DETAY:
            for i in range(len(us) + 1):
                u = 0.3 if i == 0 else (c.L - 0.3 if i == len(us) else (us[i - 1] + us[i]) / 2)
                c.kutu(g, u - 0.25, u + 0.25, 0, 0.18, ZL, ZL + H, tugla)
            korniz(g, c, ZL + H - 0.4, 0.4, 0.2, trim, profil=False)
    c = f["on"]
    c.kutu(g, c.L / 2 - 1.8, c.L / 2 + 1.8, 0, 0.05, ZL, ZL + 4.0, M("Garaj", rnd.choice(("#3e5a4a", "#5a5e63", "#6b4a32"))))
    if DETAY:
        for i in range(1, 10):
            c.kutu(g, c.L / 2 - 1.8, c.L / 2 + 1.8, 0.05, 0.07, ZL + 0.4 * i - 0.02, ZL + 0.4 * i + 0.02, M("Garaj", "#2f4a3a"))
    n = 5
    zt = ZL + H
    for i in range(n):
        ya, yb = y0 + (y1 - y0) * i / n, y0 + (y1 - y0) * (i + 1) / n
        prizma(g, [(x0, ya, zt), (x0, yb, zt), (x0, ya, zt + 2.2)], (x1 - x0, 0, 0), M("Cati", "#6b7078"))
        kutu(g, x0 + 0.3, x1 - 0.3, ya - 0.01, ya + 0.05, zt + 0.2, zt + 2.0, M("OfisCam", "#a9c4d0"))
        if DETAY:
            prizma(g, [(x0 - 0.05, ya, zt), (x0 - 0.05, yb, zt), (x0 - 0.05, ya, zt + 2.2)], (0.06, 0, 0), tugla)
    # baca
    bx, by = x1 - 2.0, y1 - 2.0
    dsil(g, bx, by, ZL, ZL + 2.0, 1.1, 8, tugla, kapak=(False, False))
    dsil(g, bx, by, ZL + 2.0, ZL + 19.0, 0.9, 12, tugla, r1=0.6)
    if DETAY:
        for z in (ZL + 8.0, ZL + 14.0, ZL + 18.2):
            r = 0.9 - (z - ZL - 2.0) / 17 * 0.3
            dsil(g, bx, by, z, z + 0.4, r + 0.12, 12, trim)
    yama(g, -LOT + 0.2, LOT - 0.2, -LOT + 0.2, y0 - 0.05, M("Asfalt", "#6a6d72"))
    if DETAY:
        for i in range(3):
            x = -5.5 + i * 1.4
            kutu(g, x, x + 1.2, -LOT + 0.5, -LOT + 1.5, ZL, ZL + 0.15, M("Ahsap", "#b58a5e"))
            kutu(g, x + 0.05, x + 1.15, -LOT + 0.55, -LOT + 1.45, ZL + 0.15, ZL + 0.15 + rnd.uniform(0.4, 1.0), M("Koli", rnd.choice(("#c9a878", "#b5654a", "#5a8f4a"))))
    return g


def konteyner(g, x, y, z, rnd, aci=0, renk=None):
    renk = renk or rnd.choice(("#c0392b", "#2f5a9a", "#2e7d5b", "#d98a1f", "#6b7078", "#8a3a5a"))
    m = M("Konteyner", renk)
    L, w, h = 6.0, 2.4, 2.6
    if aci:
        kutu(g, x - w / 2, x + w / 2, y - L / 2, y + L / 2, z, z + h, m)
        if DETAY:
            for i in range(int(L / 0.3)):
                yy = y - L / 2 + 0.15 + i * 0.3
                for s in (-1, 1):
                    kutu(g, x + s * w / 2 - 0.03, x + s * w / 2 + 0.03, yy - 0.06, yy + 0.06, z + 0.1, z + h - 0.1, M("Konteyner", koyu(renk, 0.8)))
    else:
        kutu(g, x - L / 2, x + L / 2, y - w / 2, y + w / 2, z, z + h, m)
        if DETAY:
            for i in range(int(L / 0.3)):
                xx = x - L / 2 + 0.15 + i * 0.3
                for s in (-1, 1):
                    kutu(g, xx - 0.06, xx + 0.06, y + s * w / 2 - 0.03, y + s * w / 2 + 0.03, z + 0.1, z + h - 0.1, M("Konteyner", koyu(renk, 0.8)))


def tir_dorsesi(g, x, y, rnd):
    if not DETAY:
        return
    m = M("Arac", rnd.choice(("#f4f4f2", "#c0392b", "#2f5a9a", "#d9dcdf")))
    kutu(g, x - 1.25, x + 1.25, y - 0.5, y + 10.0, ZL + 1.1, ZL + 4.0, m)
    for yy in (y + 7.0, y + 8.4):
        for s in (-1, 1):
            silindir(g, (x + s * 1.05, yy, ZL + 0.5), (x + s * 1.3, yy, ZL + 0.5), 0.5, 0.5, 10, M("Lastik", "#2b2d31"))
    kutu(g, x - 1.2, x + 1.2, y + 9.8, y + 10.0, ZL + 0.6, ZL + 1.1, M("Metal", "#3a3a3a"))


def sanayi_lojistik(rnd):
    """Lojistik depo: trapez sac kaplama, yükleme rampaları ve kapıları, ofis eki, tır dorseleri."""
    g = G("sanayi_lojistik")
    zemin(g, M("Asfalt", "#6a6d72"))
    sac = rnd.choice(("#c9cdd2", "#b8c4cc", "#d8d4c8", "#9aa8b4"))
    dm = M("Panel", sac)
    vurgu = M("Panel", rnd.choice(("#2f5a9a", "#c0392b", "#2e7d5b", "#d98a1f")))
    x0, x1, y0, y1 = -LOT + 0.3, LOT - 0.3, -1.5, LOT - 0.3
    H = 7.5
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, dm)
    for ad in f:
        c = f[ad]
        if DETAY:
            for i in range(int(c.L / 0.5)):
                u = 0.25 + i * 0.5
                c.kutu(g, u - 0.07, u + 0.07, 0, 0.06, ZL + 0.3, ZL + H - 0.1, M("Panel", koyu(sac, 0.88)))
        c.kutu(g, 0, c.L, 0, 0.08, ZL + H - 1.0, ZL + H, vurgu)
    c = f["on"]
    n = 4
    for i in range(n):
        u = c.L * (i + 0.5) / n
        c.kutu(g, u - 1.4, u + 1.4, 0, 0.1, ZL, ZL + 1.2, M("Beton", "#bdb8ae"))
        c.kutu(g, u - 1.2, u + 1.2, 0, 0.05, ZL + 1.2, ZL + 4.2, M("Garaj", "#e8e8e4"))
        if DETAY:
            for k in range(1, 8):
                c.kutu(g, u - 1.2, u + 1.2, 0.05, 0.07, ZL + 1.2 + 0.375 * k - 0.02, ZL + 1.2 + 0.375 * k + 0.02, M("Garaj", "#c9c9c4"))
            for s in (-1, 1):
                c.kutu(g, u + s * 1.25 - 0.12, u + s * 1.25 + 0.12, 0.1, 0.3, ZL + 0.6, ZL + 1.1, M("Lastik", "#2b2d31"))
            c.kutu(g, u - 1.5, u + 1.5, 0, 0.5, ZL + 4.4, ZL + 4.5, M("Metal", "#8f9499"))
            top(g, c.p(u + 1.6, 0.15, ZL + 4.6), 0.12, 0.12, 0.12, M("Isik", "#ffd27a"))
    duz_cati(g, x0, x1, y0, y1, ZL + H, 0.4, dm, M("DuzCati", "#8f8a84"))
    if DETAY:
        for i in range(3):
            x = x0 + 2.5 + i * 4.0
            kutu(g, x, x + 1.6, y0 + 2.0, y1 - 2.0, ZL + H, ZL + H + 0.35, M("OfisCam", "#c9dde4"))
    # ofis eki
    kutu(g, x0, x0 + 4.0, y0 - 1.0, y0, ZL, ZL + 3.4, M("Panel", "#e8e6e1"))
    cc = C((x0, y0 - 1.0), (1, 0), (0, -1), 4.0)
    cc.kutu(g, 0.3, 3.7, 0, 0.04, ZL + 0.9, ZL + 2.9, cam(rnd))
    # avlu
    if DETAY:
        for i in range(6):
            x = -6.5 + i * 2.6
            yama(g, x, x + 0.1, -LOT + 0.3, -3.5, M("Serit", "#f2e04a"), z=ZL, k=0.008)
        for i in range(rnd.randint(2, 4)):
            x = rnd.uniform(-5.5, 5.5)
            kutu(g, x, x + 1.2, -LOT + 0.6, -LOT + 1.6, ZL, ZL + 0.15, M("Ahsap", "#b58a5e"))
            kutu(g, x + 0.05, x + 1.15, -LOT + 0.65, -LOT + 1.55, ZL + 0.15, ZL + 1.0, M("Koli", "#c9a878"))
        konteyner(g, 3.5, -LOT + 2.0, ZL, rnd)
    return g


def sanayi_silo(rnd):
    """Tahıl siloları: dört beton/metal silo, elevatör kulesi, konveyör köprüsü, kamyon kantarı."""
    g = G("sanayi_silo")
    zemin(g, M("Beton", "#b3ada2"))
    metal = rnd.random() < 0.5
    sm = M("Metal" if metal else "Beton", "#d4d6d8" if metal else rnd.choice(("#e0dbd0", "#d6d0c4")))
    R = 2.1
    pos = [(-4.4, -0.5), (0.0, -0.5), (-4.4, 4.0), (0.0, 4.0)]
    H = rnd.uniform(13.0, 16.0)
    for x, y in pos:
        dsil(g, x, y, ZL, ZL + H, R, 16, sm, kapak=(False, False))
        if metal:
            dsil(g, x, y, ZL + H, ZL + H + 1.4, R + 0.05, 16, sm, r1=0.4)
            if DETAY:
                for z in range(2, int(H), 2):
                    dsil(g, x, y, ZL + z, ZL + z + 0.12, R + 0.04, 16, M("Metal", "#a8abae"), kapak=(False, False))
        else:
            dsil(g, x, y, ZL + H, ZL + H + 0.2, R + 0.1, 16, M("Beton", "#c9c3b6"))
    # elevatör kulesi
    tx0, tx1, ty0, ty1 = 3.0, 5.5, -1.2, 1.4
    tm = M("Panel", rnd.choice(("#c0392b", "#d9dcdf", "#2f5a9a", "#e0b23a")))
    kutu(g, tx0, tx1, ty0, ty1, ZL, ZL + H + 4.0, tm)
    kirma_cati(g, tx0, tx1, ty0, ty1, ZL + H + 4.0, 0.4, M("Cati", "#6b7078"), sacak=0.2)
    if DETAY:
        c = C((tx0, ty0), (1, 0), (0, -1), tx1 - tx0)
        for k in range(4):
            pencere(g, c, 1.25, ZL + 2.0 + k * 4.0, 0.8, 1.0, dict(tip="kare", cerceve=M("Cerceve", "#3a3d42"), denizlik=None), rnd)
        cubuk(g, (tx0, 0.0, ZL + H + 1.5), (0.0, 0.0, ZL + H + 1.5), 0.8, M("Panel", "#a8abae"), 1.0)
        cubuk(g, (tx0, 1.0, ZL + 3.0), (6.8, -5.8, ZL + 0.5), 0.4, M("Metal", "#8f9499"))
    # kantar ve yol
    yama(g, 2.0, 6.8, -LOT + 0.2, -2.0, M("Asfalt", "#6a6d72"))
    if DETAY:
        kutu(g, 2.8, 6.0, -5.5, -2.5, ZL, ZL + 0.08, M("Metal", "#8f9499"))
        kutu(g, -6.8, -2.5, -LOT + 0.5, -4.0, ZL, ZL + 2.8, M("Panel", "#e8e6e1"))
        if rnd.random() < 0.3:
            saman(g, -1.0, -5.0)
    return g


def sanayi_atolye(rnd):
    """Atölye sırası: kepenkli üç atölye, tabelalar, önlerinde paletler, yağ varilleri; arkada konteynerler."""
    g = G("sanayi_atolye")
    zemin(g, M("Beton", "#a9a49a"))
    y0 = -LOT + 3.0
    D = 6.5
    H = 4.5
    for i in range(3):
        x0 = -LOT + 0.3 + i * (2 * LOT - 0.6) / 3
        w = (2 * LOT - 0.6) / 3
        renk = rnd.choice(("#d8d4c8", "#c9cdd2", "#e0d6c0", "#b8c4cc", "#d9c9b0"))
        f = govde(g, x0, x0 + w, y0, y0 + D, ZL, ZL + H, M("Panel", renk))
        c = f["on"]
        c.kutu(g, 0.4, w - 1.4, 0, 0.05, ZL, ZL + 3.4, M("Garaj", rnd.choice(("#9aa0a6", "#c7c9cc", "#5a6b7a"))))
        if DETAY:
            for k in range(1, 12):
                c.kutu(g, 0.4, w - 1.4, 0.05, 0.07, ZL + 0.28 * k - 0.015, ZL + 0.28 * k + 0.015, M("Garaj", "#7f858b"))
        kapi(g, c, w - 0.75, ZL, 0.9, 2.2, dict(kapi=M("Kapi", "#4a4e55"), cerceve=M("Kasa", "#3a3d42")), rnd)
        tm = M("Tabela", rnd.choice(("#c0392b", "#2f5a9a", "#e0b23a", "#2e7d5b", "#3a3a3a")))
        c.kutu(g, 0.3, w - 0.3, 0, 0.12, ZL + 3.6, ZL + 4.3, tm)
        yazi_seridi(g, c, 0.3, w - 0.3, ZL + 3.95, 0.4, "#ffffff", rnd, o=0.12)
        p = [(x0 - 0.1, y0 - 0.3, ZL + H + 0.5), (x0 + w + 0.1, y0 - 0.3, ZL + H + 0.5), (x0 + w + 0.1, y0 + D + 0.3, ZL + H),
             (x0 - 0.1, y0 + D + 0.3, ZL + H)]
        q = [add(a, (0, 0, -0.15)) for a in p]
        kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], M("Cati", "#7f858b"))
        prizma(g, [(x0, y0, ZL + H), (x0, y0 + D, ZL + H), (x0, y0, ZL + H + 0.45)], (w, 0, 0), M("Panel", renk))
    if DETAY:
        for _ in range(4):
            x = rnd.uniform(-6.0, 5.5)
            dsil(g, x, -LOT + rnd.uniform(0.6, 2.2), ZL, ZL + 0.9, 0.3, 10, M("Varil", rnd.choice(("#2f5a9a", "#c0392b", "#3e6b4f"))))
        for _ in range(3):
            x = rnd.uniform(-6.0, 5.0)
            kutu(g, x, x + 1.2, -LOT + 0.4, -LOT + 1.4, ZL, ZL + 0.15 * rnd.randint(1, 4), M("Ahsap", "#b58a5e"))
        konteyner(g, -3.2, LOT - 1.6, ZL, rnd)
        konteyner(g, 3.4, LOT - 1.6, ZL, rnd)
        if rnd.random() < 0.5:
            konteyner(g, -3.2, LOT - 1.6, ZL + 2.6, rnd)
    return g


def sanayi_su_kulesi(rnd):
    """Su kulesi (víztorony): beton ayak, mantar biçimli hazne; altta pompa evi, çit."""
    g = G("sanayi_su_kulesi")
    zemin(g, M("Cim", SABIT["cim2"]))
    bm = M("Beton", rnd.choice(("#e0dbd0", "#d6d0c4", "#cfd4d8")))
    vurgu = M("Panel", rnd.choice(("#2f5a9a", "#c0392b", "#2e7d5b", "#e0b23a")))
    H = rnd.uniform(16.0, 20.0)
    dsil(g, 0, 1.0, ZL, ZL + H, 1.5, 14, bm, kapak=(False, False))
    dsil(g, 0, 1.0, ZL + H, ZL + H + 2.6, 1.5, 16, bm, r1=4.2, kapak=(False, False))
    dsil(g, 0, 1.0, ZL + H + 2.6, ZL + H + 5.0, 4.2, 16, vurgu, kapak=(False, False))
    dsil(g, 0, 1.0, ZL + H + 5.0, ZL + H + 6.5, 4.2, 16, M("Cati", "#6b7078"), r1=1.0)
    if DETAY:
        dsil(g, 0, 1.0, ZL + H + 6.5, ZL + H + 7.4, 0.9, 10, bm)
        c = C((0 - 0.5, 1.0 - 1.5), (1, 0), (0, -1), 1.0)
        for k in range(4):
            c.kutu(g, 0.3, 0.7, 0, 0.04, ZL + 3.0 + k * 3.5, ZL + 4.2 + k * 3.5, cam(rnd))
        kapi(g, c, 0.5, ZL, 0.9, 2.2, dict(kapi=M("Kapi", "#4a4e55")), rnd)
        cubuk(g, (0, 1.0, ZL + H + 7.4), (0, 1.0, ZL + H + 9.0), 0.08, M("Metal", "#c7c9cc"))
        top(g, (0, 1.0, ZL + H + 9.0), 0.15, 0.15, 0.15, M("Isik", "#ff5a4a"))
    f = govde(g, 2.8, 6.4, -LOT + 1.0, -LOT + 4.5, ZL, ZL + 3.0, M("Tugla", "#b5654a"))
    kapi(g, f["on"], 1.8, ZL, 1.0, 2.2, dict(kapi=M("Kapi", "#3e5a4a")), rnd)
    kirma_cati(g, 2.8, 6.4, -LOT + 1.0, -LOT + 4.5, ZL + 3.0, 0.6, M("Kiremit", SABIT["kiremit"]), sacak=0.3)
    if DETAY:
        cit_parmak(g, (-LOT + 0.3, -LOT + 0.3, 0), (LOT - 0.3, -LOT + 0.3, 0), ZL, 1.8, "#5a5e63", aralik=0.25)
        for x, y in ((-5.0, -4.0), (-5.0, 4.5), (5.0, 4.5)):
            agac(g, x, y, ZL, rnd)
    return g


# ================================== AVM ==================================

def avm_hipermarket(rnd):
    """Hipermarket: geniş kutu, cam giriş kulesi, renkli tabela bandı, sundurma, otopark çizgileri."""
    g = G("avm_hipermarket")
    zemin(g, M("Asfalt", "#6a6d72"))
    renk = rnd.choice(("#c0392b", "#2e7d5b", "#2f4f8a", "#e0b23a", "#d9822b", "#6b3a8a"))
    dm = M("Panel", rnd.choice(("#e8e6e1", "#d9d6cf", "#ece4d6")))
    vm = M("Panel", renk)
    x0, x1, y0, y1 = -LOT + 0.3, LOT - 0.3, -0.5, LOT - 0.3
    H = 7.0
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, dm)
    c = f["on"]
    c.kutu(g, 0, c.L, 0, 0.1, ZL + H - 1.6, ZL + H, vm)
    yazi_seridi(g, c, c.L * 0.1, c.L * 0.9, ZL + H - 0.8, 1.1, "#ffffff", rnd, o=0.1)
    # giriş kulesi
    ex = c.L * rnd.choice((0.3, 0.5, 0.7))
    c.kutu(g, ex - 2.0, ex + 2.0, 0, 1.2, ZL, ZL + H + 2.2, M("OfisCam", "#b9d4dc"))
    if DETAY:
        for u in (ex - 2.0, ex - 0.7, ex + 0.7, ex + 2.0):
            c.kutu(g, u - 0.06, u + 0.06, 1.2, 1.28, ZL, ZL + H + 2.2, M("Kasa", "#3a3d42"))
        c.kutu(g, ex - 2.2, ex + 2.2, 0, 1.4, ZL + H + 2.2, ZL + H + 2.5, vm)
    c.kutu(g, 0.5, c.L - 0.5, 0, 2.5, ZL + 3.2, ZL + 3.45, M("Metal", "#c7c9cc"))
    if DETAY:
        for u in eksenler(c.L - 1.0, 3.0)[0]:
            cubuk(g, c.p(0.5 + u, 2.3, ZL), c.p(0.5 + u, 2.3, ZL + 3.2), 0.1, M("Metal", "#9aa0a6"))
        c.kutu(g, 1.0, c.L - 1.0, 0, 0.04, ZL + 0.2, ZL + 2.9, M("Vitrin", "#cfe3ea"))
    duz_cati(g, x0, x1, y0, y1, ZL + H, 0.6, dm, M("DuzCati", "#8f8a84"))
    cati_ustu(g, x0, x1, y0 + 1.0, y1, ZL + H, rnd, n_klima=8, asansor=False)
    for i in range(7):
        x = -6.5 + i * 2.2
        yama(g, x, x + 0.1, -LOT + 0.3, -3.0, M("Serit", "#f2f2ee"), z=ZL, k=0.008)
    yama(g, -LOT + 0.3, LOT - 0.3, -2.5, y0 - 0.05, M("Kaldirim", "#d8d1c4"), k=0.12)
    if DETAY:
        for x in (-LOT + 0.8, LOT - 0.8):
            agac(g, x, -LOT + 0.8, ZL, rnd, boy=4.5, tip="kavak")
    return g


def avm_magaza_sirasi(rnd):
    """Mağaza sırası (retail park): üç-dört geniş mağaza, farklı tabela renkleri, ortak saçak."""
    g = G("avm_magaza_sirasi")
    zemin(g, M("Asfalt", "#6a6d72"))
    x0, x1, y0, y1 = -LOT + 0.3, LOT - 0.3, 0.0, LOT - 0.3
    H = 6.0
    dm = M("Panel", rnd.choice(("#e8e6e1", "#d9d6cf", "#c9c4bc")))
    f = govde(g, x0, x1, y0, y1, ZL, ZL + H, dm)
    c = f["on"]
    n = rnd.choice((3, 4))
    for i in range(n):
        u0, u1 = c.L * i / n + 0.2, c.L * (i + 1) / n - 0.2
        tm = M("Tabela", rnd.choice(("#c0392b", "#2e7d5b", "#2f4f8a", "#e0b23a", "#d9822b", "#6b3a8a", "#1f6f8b", "#3a3a3a")))
        vitrin(g, c, u0, u1, ZL, 3.4, dict(tabela=tm, vitrin_kasa=M("Kasa", "#3a3d42")), rnd)
        c.kutu(g, u0, u1, 0, 0.12, ZL + 4.4, ZL + H - 0.2, tm)
        yazi_seridi(g, c, u0, u1, ZL + 5.1, 0.7, "#ffffff", rnd, o=0.12)
        if DETAY:
            c.kutu(g, u1 + 0.05, u1 + 0.35, 0, 0.3, ZL, ZL + H, dm)
    c.kutu(g, 0, c.L, 0, 2.0, ZL + 3.9, ZL + 4.1, M("Metal", "#c7c9cc"))
    duz_cati(g, x0, x1, y0, y1, ZL + H, 0.5, dm, M("DuzCati", "#8f8a84"))
    cati_ustu(g, x0, x1, y0, y1, ZL + H, rnd, n_klima=6, asansor=False)
    for i in range(7):
        x = -6.5 + i * 2.2
        yama(g, x, x + 0.1, -LOT + 0.3, -2.5, M("Serit", "#f2f2ee"), z=ZL, k=0.008)
    yama(g, -LOT + 0.3, LOT - 0.3, -2.0, -0.05, M("Kaldirim", "#d8d1c4"), k=0.12)
    return g


# ================================== SOKAK DONATILARI ==================================
# Tabanı z = 0, önü (yola bakan yüz) −Y. Arsa levhası yok.

def don_lamba_klasik(rnd):
    """Döküm demir klasik sokak lambası (eski şehir, meydan)."""
    g = G("lamba_klasik")
    m = M("Demir", rnd.choice(("#2b2d31", "#2f3f35", "#3a2f2a")))
    dsil(g, 0, 0, 0, 0.5, 0.2, 10, m, r1=0.16)
    dsil(g, 0, 0, 0.5, 0.62, 0.13, 10, m)
    dsil(g, 0, 0, 0.62, 3.5, 0.075, 8, m, r1=0.055)
    for z in (1.4, 3.2):
        dsil(g, 0, 0, z, z + 0.08, 0.1, 8, m)
    dsil(g, 0, 0, 3.5, 3.62, 0.16, 8, m)
    # fener: aşağı daralan dört yüzlü cam
    pts = [(-0.14, -0.14, 3.62), (0.14, -0.14, 3.62), (0.14, 0.14, 3.62), (-0.14, 0.14, 3.62),
           (-0.24, -0.24, 4.2), (0.24, -0.24, 4.2), (0.24, 0.24, 4.2), (-0.24, 0.24, 4.2)]
    kati(g, pts, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], M("YIsik", "#ffd27a"))
    for x, y in ((-0.24, -0.24), (0.24, -0.24), (0.24, 0.24), (-0.24, 0.24)):
        cubuk(g, (x * 0.6, y * 0.6, 3.62), (x, y, 4.2), 0.03, m)
    dsil(g, 0, 0, 4.2, 4.5, 0.34, 4, m, r1=0.05)
    top(g, (0, 0, 4.55), 0.06, 0.06, 0.08, m)
    return g


def don_lamba_modern(rnd):
    """Modern sokak lambası: tek direk, yola uzanan kol, yassı LED başlık."""
    g = G("lamba_modern")
    m = M("Metal", rnd.choice(("#6b7078", "#8f9499", "#3a3d42")))
    dsil(g, 0, 0, 0, 0.35, 0.16, 8, M("Beton", "#bdb8ae"))
    dsil(g, 0, 0, 0.35, 6.2, 0.09, 8, m, r1=0.06)
    cubuk(g, (0, 0, 6.1), (0, -1.3, 6.35), 0.07, m)
    kutu(g, -0.18, 0.18, -1.9, -1.1, 6.25, 6.42, m)
    kutu(g, -0.14, 0.14, -1.85, -1.15, 6.21, 6.25, M("YIsik", "#ffe2a8"))
    return g


def don_lamba_cift(rnd):
    """Çift kollu cadde lambası (iki yana uzanan kollar)."""
    g = G("lamba_cift")
    m = M("Metal", "#6b7078")
    dsil(g, 0, 0, 0, 0.4, 0.18, 8, M("Beton", "#bdb8ae"))
    dsil(g, 0, 0, 0.4, 7.4, 0.1, 8, m, r1=0.07)
    for s in (-1, 1):
        cubuk(g, (0, 0, 7.2), (0, s * 1.5, 7.5), 0.07, m)
        kutu(g, -0.18, 0.18, s * 1.5 - 0.45, s * 1.5 + 0.45, 7.42, 7.6, m)
        kutu(g, -0.14, 0.14, s * 1.5 - 0.4, s * 1.5 + 0.4, 7.38, 7.42, M("YIsik", "#ffe2a8"))
    return g


def don_bank(rnd):
    g = G("bank")
    global ZL
    eski, ZL = ZL, 0.0
    bank(g, 0, 0, rnd, renk=rnd.choice(("#3f6b45", "#8a5a3a", "#2f4f6b")))
    ZL = eski
    return g


def don_cop_kutusu(rnd):
    g = G("cop_kutusu")
    m = M("Metal", rnd.choice(("#3f6b45", "#5a5e63", "#2f4f6b")))
    cubuk(g, (0, 0.12, 0), (0, 0.12, 1.1), 0.08, M("Demir", SABIT["demir"]))
    dsil(g, 0, -0.12, 0.3, 1.0, 0.26, 12, m, kapak=(True, False))
    dsil(g, 0, -0.12, 0.97, 1.02, 0.29, 12, M("Metal", "#2b2d31"))
    return g


def don_geri_donusum(rnd):
    """Geri dönüşüm kumbaraları: sarı, mavi, yeşil."""
    g = G("geri_donusum")
    for i, r in enumerate(("#e0b23a", "#2f5a9a", "#2e7d3a")):
        x = -1.3 + i * 1.3
        kutu(g, x - 0.55, x + 0.55, -0.55, 0.55, 0, 1.3, M("Plastik", r))
        kirma_cati(g, x - 0.55, x + 0.55, -0.55, 0.55, 1.3, 0.35, M("Plastik", koyu(r, 0.85)), sacak=0.03, sirt=False)
        kutu(g, x - 0.2, x + 0.2, -0.57, -0.55, 1.0, 1.15, M("Metal", "#2b2d31"))
    return g


def don_durak(rnd):
    """Otobüs durağı: cam arka ve yan paneller, saçak, bank, ışıklı reklam panosu, durak direği."""
    g = G("durak")
    m = M("Metal", rnd.choice(("#5a6068", "#2f4f6b", "#3e5a4a")))
    gm = M("Korkuluk", "#bcd6de")
    W, D, H = 3.6, 1.5, 2.5
    for x in (-W / 2, W / 2):
        for y in (-D / 2, D / 2):
            cubuk(g, (x, y, 0), (x, y, H), 0.08, m)
    kutu(g, -W / 2, W / 2, D / 2 - 0.03, D / 2, 0.15, H - 0.1, gm)
    kutu(g, -W / 2, -W / 2 + 0.03, -D / 2 + 0.2, D / 2, 0.15, H - 0.1, gm)
    kutu(g, W / 2 - 1.2, W / 2 - 0.05, D / 2 - 0.08, D / 2 - 0.03, 0.4, 2.1, M("Vitrin", "#f2ead0"))
    kutu(g, W / 2 - 1.25, W / 2, D / 2 - 0.1, D / 2 - 0.02, 0.35, 2.15, m)
    kutu(g, -W / 2 - 0.15, W / 2 + 0.15, -D / 2 - 0.25, D / 2 + 0.1, H, H + 0.15, m)
    kutu(g, -W / 2 + 0.2, W / 2 - 1.4, D / 2 - 0.45, D / 2 - 0.1, 0.45, 0.5, M("Ahsap", "#b58a5e"))
    for x in (-W / 2 + 0.4, W / 2 - 1.6):
        kutu(g, x - 0.04, x + 0.04, D / 2 - 0.4, D / 2 - 0.15, 0, 0.45, m)
    cubuk(g, (-W / 2 - 0.5, -D / 2, 0), (-W / 2 - 0.5, -D / 2, 3.0), 0.07, M("Metal", "#9aa0a6"))
    kutu(g, -W / 2 - 0.8, -W / 2 - 0.2, -D / 2 - 0.03, -D / 2 + 0.03, 2.4, 3.0, M("Tabela", "#f2c94c"))
    kutu(g, -W / 2 - 0.75, -W / 2 - 0.25, -D / 2 - 0.05, -D / 2 - 0.03, 2.55, 2.85, M("Tabela", "#2f4f8a"))
    return g


def don_posta_kutusu(rnd):
    g = G("posta_kutusu")
    kirmizi = M("Metal", "#c8321f")
    cubuk(g, (0, 0.1, 0), (0, 0.1, 1.0), 0.07, M("Demir", SABIT["demir"]))
    kutu(g, -0.25, 0.25, -0.15, 0.15, 0.95, 1.6, kirmizi)
    prizma(g, [(-0.25, -0.15, 1.6), (-0.25, 0.15, 1.6), (-0.25, 0.0, 1.7)], (0.5, 0, 0), kirmizi)
    kutu(g, -0.15, 0.15, -0.17, -0.15, 1.38, 1.44, M("Metal", "#2b2d31"))
    kutu(g, -0.08, 0.08, -0.17, -0.15, 1.12, 1.26, M("Tabela", "#f2c94c"))
    return g


def don_yangin_musluk(rnd):
    g = G("yangin_musluk")
    k = M("Metal", "#c8321f")
    dsil(g, 0, 0, 0, 0.1, 0.17, 10, k)
    dsil(g, 0, 0, 0.1, 0.75, 0.12, 10, k)
    dsil(g, 0, 0, 0.75, 0.82, 0.16, 10, k)
    top(g, (0, 0, 0.88), 0.12, 0.12, 0.1, k)
    for s in (-1, 1):
        silindir(g, (s * 0.1, 0, 0.55), (s * 0.22, 0, 0.55), 0.06, 0.06, 8, M("Metal", "#d9dcdf"))
    silindir(g, (0, -0.1, 0.5), (0, -0.22, 0.5), 0.08, 0.08, 8, M("Metal", "#d9dcdf"))
    return g


def don_baba(rnd):
    """Üç bollard (baba) dizisi."""
    g = G("baba")
    m = M("Metal", rnd.choice(("#2b2d31", "#5a5e63")))
    for x in (-1.2, 0.0, 1.2):
        dsil(g, x, 0, 0, 0.85, 0.11, 10, m)
        top(g, (x, 0, 0.85), 0.11, 0.11, 0.07, m)
        dsil(g, x, 0, 0.62, 0.7, 0.115, 10, M("Serit", "#f2f2ee"), kapak=(False, False))
    return g


def isaret_levha(g, tip, z):
    if tip == "dur":
        silindir(g, (0, -0.06, z), (0, -0.1, z), 0.38, 0.38, 8, M("Tabela", "#c8321f"))
        silindir(g, (0, -0.1, z), (0, -0.11, z), 0.3, 0.3, 8, M("Tabela", "#f4f4ec"))
    elif tip == "yol_ver":
        taban = [(-0.42, -0.06, z + 0.3), (0.42, -0.06, z + 0.3), (0.0, -0.06, z - 0.42)]
        prizma(g, taban, (0, -0.04, 0), M("Tabela", "#c8321f"))
        taban = [(-0.3, -0.1, z + 0.23), (0.3, -0.1, z + 0.23), (0.0, -0.1, z - 0.28)]
        prizma(g, taban, (0, -0.01, 0), M("Tabela", "#f4f4ec"))
    elif tip == "park":
        kutu(g, -0.32, 0.32, -0.1, -0.06, z - 0.32, z + 0.32, M("Tabela", "#2f5a9a"))
        kutu(g, -0.1, -0.03, -0.11, -0.1, z - 0.2, z + 0.2, M("Tabela", "#f4f4ec"))
        kutu(g, -0.03, 0.12, -0.11, -0.1, z + 0.05, z + 0.2, M("Tabela", "#f4f4ec"))
    else:  # yaya geçidi
        kutu(g, -0.32, 0.32, -0.1, -0.06, z - 0.32, z + 0.32, M("Tabela", "#2f5a9a"))
        taban = [(-0.24, -0.1, z - 0.2), (0.24, -0.1, z - 0.2), (0.0, -0.1, z + 0.24)]
        prizma(g, taban, (0, -0.01, 0), M("Tabela", "#f4f4ec"))


def don_isaret(rnd, tip=None):
    tip = tip or rnd.choice(("dur", "yol_ver", "park", "yaya"))
    g = G("isaret_" + tip)
    cubuk(g, (0, 0, 0), (0, 0, 2.6), 0.07, M("Metal", "#9aa0a6"))
    isaret_levha(g, tip, 2.3)
    return g


def don_trafik_lambasi(rnd):
    g = G("trafik_lambasi")
    m = M("Metal", "#3a3d42")
    dsil(g, 0, 0, 0, 3.0, 0.08, 8, m)
    kutu(g, -0.18, 0.18, -0.26, 0.0, 2.0, 3.0, m)
    for i, r in enumerate(("#e0402a", "#f2b02a", "#3ac060")):
        z = 2.82 - i * 0.31
        silindir(g, (0, -0.26, z), (0, -0.29, z), 0.1, 0.1, 10, M("Isik", r))
        kutu(g, -0.14, 0.14, -0.4, -0.26, z + 0.1, z + 0.13, m)
    kutu(g, -0.12, 0.12, -0.22, -0.04, 1.0, 1.25, M("Tabela", "#f2c94c"))
    return g


def bisiklet(g, x, y, aci, renk):
    m = M("Bisiklet", renk)
    lm = M("Lastik", "#2b2d31")
    ca, sa = math.cos(aci), math.sin(aci)
    for d in (-0.5, 0.5):
        cx, cy = x + d * ca, y + d * sa
        silindir(g, (cx - 0.02 * sa, cy + 0.02 * ca, 0.34), (cx + 0.02 * sa, cy - 0.02 * ca, 0.34), 0.34, 0.34, 14, lm)
    cubuk(g, (x - 0.5 * ca, y - 0.5 * sa, 0.34), (x, y, 0.62), 0.04, m)
    cubuk(g, (x + 0.5 * ca, y + 0.5 * sa, 0.34), (x + 0.3 * ca, y + 0.3 * sa, 0.85), 0.04, m)
    cubuk(g, (x - 0.1 * ca, y - 0.1 * sa, 0.8), (x + 0.3 * ca, y + 0.3 * sa, 0.82), 0.04, m)
    cubuk(g, (x - 0.05 * ca, y - 0.05 * sa, 0.34), (x - 0.12 * ca, y - 0.12 * sa, 0.82), 0.04, m)
    kutu(g, x - 0.2 * ca - 0.08, x - 0.2 * ca + 0.08, y - 0.2 * sa - 0.05, y - 0.2 * sa + 0.05, 0.84, 0.88, M("Sele", "#3a2a20"))


def don_bisiklet_park(rnd):
    g = G("bisiklet_park")
    m = M("Metal", "#9aa0a6")
    for i in range(5):
        x = -1.6 + i * 0.8
        cubuk(g, (x, -0.35, 0), (x, -0.35, 0.8), 0.05, m)
        cubuk(g, (x, 0.35, 0), (x, 0.35, 0.8), 0.05, m)
        cubuk(g, (x, -0.35, 0.8), (x, 0.35, 0.8), 0.05, m)
    for i in rnd.sample(range(5), 2):
        bisiklet(g, -1.6 + i * 0.8 + 0.15, 0.0, math.pi / 2, rnd.choice(("#c0392b", "#2f5a9a", "#2e7d3a", "#f2f2ee", "#2b2d31")))
    return g


def don_saksi(rnd):
    g = G("saksi")
    m = M("Beton", rnd.choice(("#bdb8ae", "#d6d0c4", "#8a8f96")))
    if rnd.random() < 0.5:
        kutu(g, -0.6, 0.6, -0.6, 0.6, 0, 0.6, m)
        yama_g = M("Toprak", SABIT["toprak"])
        kutu(g, -0.52, 0.52, -0.52, 0.52, 0.6, 0.62, yama_g)
        top(g, (0, 0, 0.95), 0.55, 0.55, 0.45, M("Cali", rnd.choice(("#5f9a4f", "#6ea552"))), rnd, 0.1)
    else:
        dsil(g, 0, 0, 0, 0.55, 0.5, 12, m, r1=0.6, kapak_mat=M("Toprak", SABIT["toprak"]))
        for i in range(9):
            a = i * 0.7
            r = 0.15 + 0.25 * (i % 3) / 2
            top(g, (math.cos(a) * r, math.sin(a) * r, 0.68), 0.12, 0.12, 0.1,
                M("Cicek", rnd.choice(("#e0525a", "#f0a0c0", "#f2d04a", "#ffffff", "#b07ad8"))))
        top(g, (0, 0, 0.6), 0.45, 0.45, 0.12, M("Yaprak", "#5f9a4f"))
    return g


def don_ilan_sutunu(rnd):
    """İlan sütunu (hirdetőoszlop): yeşil döküm kaide ve kubbeli şapka, etrafında renkli afişler."""
    g = G("ilan_sutunu")
    yesil = M("Demir", rnd.choice(("#2f4f3a", "#3a3d42")))
    dsil(g, 0, 0, 0, 0.35, 0.72, 14, yesil)
    dsil(g, 0, 0, 0.35, 2.75, 0.6, 14, M("Afis", "#efe6d2"), kapak=(False, False))
    n = 7
    for i in range(n):
        a = 2 * math.pi * i / n
        nx, ny = math.cos(a), math.sin(a)
        c = C((nx * 0.6 + ny * 0.22, ny * 0.6 - nx * 0.22), (-ny, nx), (nx, ny), 0.44)
        c.kutu(g, 0.0, 0.44, 0, 0.03, 0.55 + 0.15 * (i % 2), 2.35 + 0.1 * (i % 3) / 2,
               M("Afis", rnd.choice(("#c0392b", "#2f5a9a", "#e0b23a", "#2e7d5b", "#d98a1f", "#6b3a8a", "#f4f4ec"))))
    dsil(g, 0, 0, 2.75, 2.9, 0.75, 14, yesil)
    top(g, (0, 0, 2.9), 0.68, 0.68, 0.45, yesil)
    top(g, (0, 0, 3.4), 0.1, 0.1, 0.12, M("Pirinc", "#c9a24a"))
    return g


def don_bufe(rnd):
    """Gazete/çiçek büfesi: küçük kulübe, açılır kanat saçak, vitrinler."""
    g = G("bufe")
    renk = rnd.choice(("#2f5d50", "#7a2f2f", "#2f3f6b", "#c46a2b", "#e0b23a"))
    m = M("Panel", renk)
    W, D, H = 2.4, 1.8, 2.5
    f = govde(g, -W / 2, W / 2, -D / 2, D / 2, 0, H, m)
    c = f["on"]
    c.kutu(g, 0.15, W - 0.15, 0, 0.03, 0.9, 2.0, M("Vitrin", "#f2ead0"))
    c.kutu(g, 0.1, W - 0.1, 0, 0.35, 0.85, 0.92, M("Ahsap", "#b58a5e"))
    p = [c.p(-0.1, 0, H - 0.1), c.p(W + 0.1, 0, H - 0.1), c.p(W + 0.1, 0.9, H - 0.45), c.p(-0.1, 0.9, H - 0.45)]
    q = [add(a, (0, 0, 0.05)) for a in p]
    kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], M("Tente", "#f4efe4"))
    kirma_cati(g, -W / 2, W / 2, -D / 2, D / 2, H, 0.4, m, sacak=0.15, sirt=False)
    for i in range(6):
        u = 0.25 + i * 0.35
        c.kutu(g, u, u + 0.25, 0.36, 0.38, 0.92, 1.2, M("Afis", rnd.choice(("#c0392b", "#2f5a9a", "#e0b23a", "#f4f4ec"))))
    kutu(g, -W / 2 + 0.1, W / 2 - 0.1, -D / 2 - 0.02, -D / 2, H - 0.45, H - 0.15, M("Yazi", "#fff2c8"))
    return g


def don_elektrik_diregi(rnd):
    """Ahşap elektrik direği: travers, izolatörler (kır yolları)."""
    g = G("elektrik_diregi")
    ah = M("Ahsap", rnd.choice(("#6b4a32", "#7a5a3a")))
    dsil(g, 0, 0, 0, 8.0, 0.14, 8, ah, r1=0.11)
    kutu(g, -1.0, 1.0, -0.06, 0.06, 7.2, 7.35, ah)
    for x in (-0.85, 0.0, 0.85):
        z = 7.35 if x else 8.0
        dsil(g, x, 0, z, z + 0.22, 0.05, 8, M("Seramik", "#e8e4da"))
    cubuk(g, (0, 0, 6.4), (0.6, 0, 7.2), 0.05, ah)
    cubuk(g, (0, 0, 6.4), (-0.6, 0, 7.2), 0.05, ah)
    return g


def don_saman(rnd):
    g = G("saman_balyasi")
    global ZL
    eski, ZL = ZL, 0.0
    saman(g, 0, 0, r=0.75)
    if rnd.random() < 0.6:
        saman(g, 1.6, 0.3, r=0.75, aci=0.3)
    ZL = eski
    return g


def don_cesme(rnd):
    """İçme suyu çeşmesi (döküm sütun)."""
    g = G("cesme")
    m = M("Demir", "#2f4f3a")
    dsil(g, 0, 0, 0, 0.12, 0.45, 12, M("Tas", "#bdb4a4"))
    dsil(g, 0, 0, 0.12, 0.22, 0.38, 12, M("Su", "#5ab6d6"))
    dsil(g, 0, 0, 0.12, 1.2, 0.12, 10, m, r1=0.09)
    top(g, (0, 0, 1.25), 0.15, 0.15, 0.15, m)
    cubuk(g, (0, -0.05, 1.05), (0, -0.3, 1.0), 0.05, M("Pirinc", "#c9a24a"))
    return g


def don_heykel(rnd):
    """Kaide üstünde bronz heykel (soyut figür)."""
    g = G("heykel")
    tas = M("Tas", rnd.choice(("#d6cdbb", "#c9c1b3", "#bdb4a4")))
    br = M("Bronz", rnd.choice(("#5e7a6a", "#7a6a4a", "#6a5a3a")))
    kutu(g, -1.0, 1.0, -1.0, 1.0, 0, 0.3, tas)
    kutu(g, -0.6, 0.6, -0.6, 0.6, 0.3, 2.0, tas)
    kutu(g, -0.7, 0.7, -0.7, 0.7, 2.0, 2.15, tas)
    kutu(g, -0.15, 0.15, -0.62, -0.6, 1.0, 1.4, M("Pirinc", "#c9a24a"))
    for x in (-0.12, 0.12):
        cubuk(g, (x, 0, 2.15), (x, 0, 3.0), 0.16, br)
    kutu(g, -0.25, 0.25, -0.15, 0.15, 3.0, 3.8, br)
    top(g, (0, 0, 4.05), 0.16, 0.16, 0.2, br)
    cubuk(g, (0.25, 0, 3.75), (0.55, -0.2, 4.4), 0.1, br)
    cubuk(g, (-0.25, 0, 3.75), (-0.35, 0, 3.1), 0.1, br)
    return g


def don_trafo(rnd):
    g = G("trafo")
    m = M("Metal", rnd.choice(("#9aa0a6", "#8a9a8a", "#b8bcc0")))
    kutu(g, -0.7, 0.7, -0.3, 0.3, 0, 1.4, m)
    kutu(g, -0.75, 0.75, -0.35, 0.35, 1.4, 1.48, M("Metal", koyu(HEX[m], 0.8)))
    kutu(g, -0.65, -0.03, -0.32, -0.3, 0.1, 1.3, M("Metal", koyu(HEX[m], 0.9)))
    kutu(g, 0.03, 0.65, -0.32, -0.3, 0.1, 1.3, M("Metal", koyu(HEX[m], 0.9)))
    taban = [(-0.15, -0.33, 0.9), (0.15, -0.33, 0.9), (0.0, -0.33, 1.15)]
    prizma(g, taban, (0, -0.01, 0), M("Tabela", "#f2c94c"))
    return g


def don_sarj(rnd):
    """Elektrikli araç şarj istasyonu."""
    g = G("sarj")
    kutu(g, -0.25, 0.25, -0.15, 0.15, 0, 1.6, M("Panel", "#f4f4f2"))
    kutu(g, -0.25, 0.25, -0.16, -0.15, 1.2, 1.6, M("Panel", rnd.choice(("#2e7d5b", "#2f5a9a"))))
    kutu(g, -0.15, 0.15, -0.17, -0.16, 0.85, 1.1, M("Ekran", "#2f4f5c"))
    cubuk(g, (0.25, -0.05, 1.0), (0.38, -0.1, 0.4), 0.04, M("Lastik", "#2b2d31"))
    kutu(g, -0.6, 0.6, -0.5, 0.5, 0, 0.05, M("Beton", "#bdb8ae"))
    return g


def don_reklam_panosu(rnd):
    """İki ayaklı ışıklı reklam panosu."""
    g = G("reklam_panosu")
    m = M("Metal", "#5a5e63")
    for x in (-1.4, 1.4):
        cubuk(g, (x, 0, 0), (x, 0, 2.4), 0.14, m)
    kutu(g, -2.0, 2.0, -0.1, 0.1, 2.4, 4.6, m)
    kutu(g, -1.9, 1.9, -0.13, -0.1, 2.5, 4.5, M("Vitrin", rnd.choice(("#f2ead0", "#e8f0f4"))))
    for i in range(3):
        kutu(g, -1.6 + i * 1.1, -0.8 + i * 1.1, -0.14, -0.13, 2.8 + 0.4 * (i % 2), 3.9, M("Afis", rnd.choice(("#c0392b", "#2f5a9a", "#e0b23a", "#2e7d5b"))))
    return g


def don_piknik(rnd):
    g = G("piknik")
    ah = M("Ahsap", "#9a6a44")
    kutu(g, -1.0, 1.0, -0.4, 0.4, 0.72, 0.78, ah)
    for s in (-1, 1):
        kutu(g, -1.0, 1.0, s * 0.75 - 0.15, s * 0.75 + 0.15, 0.42, 0.47, ah)
        for x in (-0.8, 0.8):
            cubuk(g, (x, s * 0.85, 0), (x, -s * 0.1, 0.75), 0.07, ah)
    return g


def don_oyun_parki(rnd):
    """Küçük oyun parkı: kaydıraklı kule, salıncak, kum havuzu."""
    g = G("oyun_parki")
    renk = rnd.choice(("#d94f3a", "#2f7dbf", "#e0b23a", "#4fa84a"))
    m = M("Oyun", renk)
    ah = M("Ahsap", "#9a6a44")
    kutu(g, -3.0, 3.0, -2.0, 2.0, 0, 0.04, M("Kum", "#e8d6a8"))
    for x in (-2.4, -1.2):
        for y in (-0.6, 0.6):
            cubuk(g, (x, y, 0), (x, y, 2.4), 0.12, ah)
    kutu(g, -2.5, -1.1, -0.7, 0.7, 1.2, 1.3, ah)
    besik_cati(g, -2.5, -1.1, -0.7, 0.7, 2.4, 0.6, m, ah, sacak=0.1, yan=0.1, siralar=False)
    p = [(-1.1, -0.35, 1.3), (-1.1, 0.35, 1.3), (0.8, 0.35, 0.05), (0.8, -0.35, 0.05)]
    q = [add(a, (0, 0, -0.06)) for a in p]
    kati(g, p + q, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], m)
    for x in (1.4, 2.8):
        cubuk(g, (x, -0.7, 0), (x, 0, 2.0), 0.07, M("Metal", "#9aa0a6"))
        cubuk(g, (x, 0.7, 0), (x, 0, 2.0), 0.07, M("Metal", "#9aa0a6"))
    cubuk(g, (1.4, 0, 2.0), (2.8, 0, 2.0), 0.07, M("Metal", "#9aa0a6"))
    for x in (1.8, 2.4):
        cubuk(g, (x, 0, 2.0), (x, 0, 0.5), 0.02, M("Metal", "#9aa0a6"))
        kutu(g, x - 0.2, x + 0.2, -0.1, 0.1, 0.45, 0.5, m)
    return g


def don_cit_ahsap(rnd):
    g = G("cit_ahsap")
    global ZL
    eski, ZL = ZL, 0.0
    cit_tahta(g, (-2.0, 0, 0), (2.0, 0, 0), 0.0, 1.1, rnd.choice(("#8a6a4a", "#9a7a52", "#f4f0e6")))
    ZL = eski
    return g


def don_gemeskut(rnd):
    g = G("gemeskut")
    global ZL
    eski, ZL = ZL, 0.0
    gemeskut(g, 0, 0, rnd)
    ZL = eski
    return g


def don_cicek_havuzu(rnd):
    """Meydan için yuvarlak çiçek havuzu (taş bilezikli)."""
    g = G("cicek_havuzu")
    dsil(g, 0, 0, 0, 0.45, 1.6, 16, M("Tas", "#d6cdbb"), kapak=(False, True), kapak_mat=M("Toprak", "#7a5a3e"))
    for i in range(26):
        a = i * 2.4
        r = 0.2 + 1.15 * ((i * 7) % 26) / 26
        top(g, (math.cos(a) * r, math.sin(a) * r, 0.58), 0.16, 0.16, 0.13,
            M("Cicek", rnd.choice(("#e0525a", "#f0a0c0", "#f2d04a", "#ffffff", "#b07ad8", "#ff8a3c"))))
    top(g, (0, 0, 0.5), 1.4, 1.4, 0.2, M("Yaprak", "#5f9a4f"))
    top(g, (0, 0, 1.1), 0.5, 0.5, 0.7, M("Cali", "#4f8a47"), rnd, 0.1)
    return g


# ================================== KATALOG ==================================
# (ad, üretici, adet). Ad oyunda bölge havuzunu belirler: Bolge_<bölge>_<tür>_<n>.

KATALOG = [
    ("eski_barok_sira", eski_barok_sira, 6), ("eski_kapialti", eski_kapialti, 4), ("eski_kuleli", eski_kuleli, 3),
    ("eski_kasaba_evi", eski_kasaba_evi, 4), ("eski_kilise", eski_kilise, 2),
    ("karma_dukkanli_blok", karma_dukkanli_blok, 6), ("karma_kose_kafe", karma_kose_kafe, 3),
    ("karma_mahalle_dukkani", karma_mahalle_dukkani, 4), ("karma_70ler", karma_70ler, 3), ("karma_market", karma_market, 2),
    ("kule_panel", kule_panel, 4), ("kule_ofis_cam", kule_ofis_cam, 3), ("kule_konut", kule_konut, 3),
    ("kule_otel", kule_otel, 2), ("kule_ikiz", kule_ikiz, 2),
    ("ev_kocka", ev_kocka, 6), ("ev_aile", ev_aile, 6), ("ev_bungalov", ev_bungalov, 4), ("ev_modern_kup", ev_modern_kup, 4),
    ("ev_ikiz", ev_ikiz, 4),
    ("banliyo_uzun_ev", banliyo_uzun_ev, 5), ("banliyo_ciftlik", banliyo_ciftlik, 3), ("banliyo_bag_evi", banliyo_bag_evi, 2),
    ("kampus_fakulte", kampus_fakulte, 2), ("kampus_kutuphane", kampus_kutuphane, 2), ("kampus_yurt", kampus_yurt, 3),
    ("kampus_lab", kampus_lab, 2), ("kampus_spor", kampus_spor, 2),
    ("sanayi_testere", sanayi_testere, 2), ("sanayi_lojistik", sanayi_lojistik, 3), ("sanayi_silo", sanayi_silo, 2),
    ("sanayi_atolye", sanayi_atolye, 3), ("sanayi_su_kulesi", sanayi_su_kulesi, 1),
    ("avm_hipermarket", avm_hipermarket, 2), ("avm_magaza_sirasi", avm_magaza_sirasi, 2),
]

DONATILAR = [
    ("lamba_klasik", don_lamba_klasik, 1), ("lamba_modern", don_lamba_modern, 1), ("lamba_cift", don_lamba_cift, 1),
    ("bank", don_bank, 3), ("cop_kutusu", don_cop_kutusu, 2), ("geri_donusum", don_geri_donusum, 1), ("durak", don_durak, 2),
    ("posta_kutusu", don_posta_kutusu, 1), ("yangin_musluk", don_yangin_musluk, 1), ("baba", don_baba, 1),
    ("isaret_dur", lambda r: don_isaret(r, "dur"), 1), ("isaret_yol_ver", lambda r: don_isaret(r, "yol_ver"), 1),
    ("isaret_park", lambda r: don_isaret(r, "park"), 1), ("isaret_yaya", lambda r: don_isaret(r, "yaya"), 1),
    ("trafik_lambasi", don_trafik_lambasi, 1), ("bisiklet_park", don_bisiklet_park, 2), ("saksi", don_saksi, 3),
    ("ilan_sutunu", don_ilan_sutunu, 2), ("bufe", don_bufe, 3), ("elektrik_diregi", don_elektrik_diregi, 1),
    ("saman_balyasi", don_saman, 2), ("cesme", don_cesme, 1), ("heykel", don_heykel, 2), ("trafo", don_trafo, 1),
    ("sarj", don_sarj, 1), ("reklam_panosu", don_reklam_panosu, 2), ("piknik", don_piknik, 1), ("oyun_parki", don_oyun_parki, 2),
    ("cit_ahsap", don_cit_ahsap, 2), ("gemeskut", don_gemeskut, 1), ("cicek_havuzu", don_cicek_havuzu, 1),
]


def tohum(ad, i):
    h = 0
    for ch in ad:
        h = (h * 131 + ord(ch)) & 0xFFFFFF
    return TOHUM * 100003 + h * 17 + i * 7919


def ayna(g):
    """X'te ayna: aynı türün sağa/sola açılan sürümü (ön yine −Y)."""
    g.v = [(-p[0], p[1], p[2]) for p in g.v]
    g.f = [list(reversed(f)) for f in g.f]


def hafif(g, esik=0.32):
    """Hafif (uzak) sürüm: ikinci büyük ölçüsü eşiğin altındaki katılar (kasa, kayıt, korkuluk çubuğu, kiremit sırası, çiçek) atılır."""
    kutular = {}
    for f, s in zip(g.f, g.fs):
        b = kutular.get(s)
        for i in f:
            p = g.v[i]
            if b is None:
                b = kutular[s] = [list(p), list(p)]
            else:
                for k in range(3):
                    if p[k] < b[0][k]: b[0][k] = p[k]
                    if p[k] > b[1][k]: b[1][k] = p[k]
    tut = set()
    for s, (a, b) in kutular.items():
        e = sorted((b[0] - a[0], b[1] - a[1], b[2] - a[2]))
        if e[1] >= esik:
            tut.add(s)
    h = G(g.ad + "_Hafif")
    for f, m, s in zip(g.f, g.fm, g.fs):
        if s in tut:
            h.sid = s
            h.yuz([g.v[i] for i in f], g.mats[m])
    return h


def modeller(sadece=None):
    """(dosya adı, geometri) üretir."""
    for liste, onek in ((KATALOG, "Bolge_"), (DONATILAR, "Donati_")):
        for ad, fn, adet in liste:
            if sadece and ad not in sadece:
                continue
            for i in range(adet):
                rnd = random.Random(tohum(ad, i))
                g = fn(rnd)
                if onek == "Bolge_" and i % 2 == 1:
                    ayna(g)
                yield "%s%s_%d" % (onek, ad, i + 1), g


# ================================== BLENDER ==================================

def srgb_lineer(hexv):
    out = []
    for i in (1, 3, 5):
        c = int(hexv[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return out


def malzeme(ad):
    m = bpy.data.materials.get(ad) or bpy.data.materials.new(ad)
    r, gg, b = srgb_lineer(HEX[ad])
    m.diffuse_color = (r, gg, b, 1.0)
    if bpy.app.version < (5, 0, 0):
        m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF") if m.node_tree is not None else None
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (r, gg, b, 1.0)
    return m


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
    bpy.ops.export_scene.fbx(filepath=yol, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='FACE',
                             use_mesh_modifiers=False, add_leaf_bones=False)


def cikti_klasoru():
    if CIKTI:
        return CIKTI
    kok = os.environ.get("KURYE_TYCOON_KOK")
    if kok:
        return os.path.join(kok, "Modeller", "Binalar")
    try:
        bu = os.path.dirname(os.path.abspath(__file__))
        return os.path.join(bu, "..", "..", "Modeller", "Binalar")
    except NameError:
        return os.path.expanduser("~/Desktop/KuryeBolgeModelleri")


def main():
    klasor = os.path.abspath(cikti_klasoru())
    os.makedirs(klasor, exist_ok=True)
    k = bpy.data.collections.get("KuryeBolge") or bpy.data.collections.new("KuryeBolge")
    if k.name not in bpy.context.scene.collection.children:
        bpy.context.scene.collection.children.link(k)
    n = 0
    for ad, g in modeller(SADECE):
        # Donatılar küçük ve ince (direk, çubuk): hafif sürümleri boş kalıyordu, sadece binalara LOD
        parcalar = [(ad, g)] + ([(ad + "_Hafif", hafif(g))] if LOD and ad.startswith("Bolge_") else [])
        for dad, gg in parcalar:
            ob = blender_obje(gg, dad, k)
            fbx_kaydet(ob, os.path.join(klasor, dad + ".fbx"))
            me = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            bpy.data.meshes.remove(me)
        n += 1
        print("KAYDEDILDI", ad, g.ucgen(), "ucgen")
    print("BOLGE MODELLERI BITTI: %d model -> %s" % (n, klasor))


def onizleme(klasor):
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from bolge_onizleme import render
    os.makedirs(klasor, exist_ok=True)
    for ad, g in modeller(SADECE):
        render(g, HEX, size=420, yol=os.path.join(klasor, ad + ".png"))
        print(ad, g.ucgen())


if IN_BLENDER:
    main()
elif __name__ == "__main__" and "--onizleme" in sys.argv:
    onizleme(sys.argv[sys.argv.index("--onizleme") + 1])
