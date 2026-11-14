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
MODEL = "mustakil_ev"      # "sira_ev" · "ofis_kulesi" · "mustakil_ev" · "market" · "fastfood" · "apartman" · "dukkan" (altı dükkanlı binalar) · "sehir" (yollar, zemin, ağaçlar, parklar) · "katalog" (konut, büro, sanayi, kamu, yeni parklar) · "hava" (bulut, yağmur, kar, yaprak, şimşek) · "kir" (kır parselleri: tarla, bağ, meyve bahçesi, çayır, gölet, sera, koru) · "blok" (bitişik blok ve köşe binaları) · "kurye" · "yaya"
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

def cam_grubu(cam, cu, zb):
    """Pencere camı üç ışık grubundan birine düşer (Cam_, Cam2_, Cam3_; renk aynı).
    Unity'de gece her grup ayrı saatte yanıp söner: binada bazı pencereler yanar, bazıları karanlık kalır."""
    if not cam.startswith("Cam_"):
        return cam
    k = int(abs(cu * 7.31 + zb * 3.17 + cu * zb * 0.53) * 10.0) % 3
    if k == 0:
        return cam
    ad = f"Cam{k + 1}_{cam[4:]}"
    HEX[ad] = HEX[cam]
    return ad


def isik_havuzu(g, x, y, z, ic=1.2, dis=2.3):
    """Lambanın altında gece parlayan ışık halkası (gündüz görünmez: Unity'de saydam, gece yanar).
    Halka karenin (±YK) dışına taşmaz: taşarsa parçanın sınırı büyür, Unity yolu küçültür ve yollar arasında boşluk kalır."""
    # Sert sarı diskler kötü görünüyordu: lamba ışığı artık Unity'de gerçek ışık kaynağı (GeceIsiklari)
    return
    k = YK - 0.02

    def icinde(pts):
        return [(min(max(px, -k), k), min(max(py, -k), k)) for px, py in pts]
    duz_poligon(g, icinde(poligon(14, dis, dis, x, y)), z + 0.008, M("IsikHavuzDis", "#ffd98a"))
    duz_poligon(g, icinde(poligon(12, ic, ic, x, y)), z + 0.014, M("IsikHavuzIc", "#ffe7a8"))


def pencere(g, fc, cu, zb, w, h, r, sade=False):
    T = r["suslu"]
    cdortgen(g, fc, cu - w / 2, cu + w / 2, 0.012, zb, zb + h, cam_grubu(r["cam"], cu, zb))
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


def cam_kapi(g, fc, cu, z0, r, w=1.8, h=2.5, kasa=None, sacak=True):
    """Çift kanatlı camlı giriş kapısı: koyu kasa, iki cam kanat, orta kayıt, kollar, üstte saçak (ofis, apartman girişi)."""
    kasa = kasa or r["cerceve"]
    vcam = r["cam"]
    if vcam.startswith("Cam_"):
        vcam = "Vitrin_" + vcam[4:]
        HEX[vcam] = HEX[r["cam"]]
    ckutu(g, fc, cu - w / 2 - 0.12, cu + w / 2 + 0.12, 0, 0.1, z0, z0 + h + 0.15, kasa)
    for su in (-1, 1):
        a, b = (cu - w / 2 + 0.06, cu - 0.04) if su < 0 else (cu + 0.04, cu + w / 2 - 0.06)
        cdortgen(g, fc, a, b, 0.11, z0 + 0.08, z0 + h - 0.05, vcam)
        ckutu(g, fc, cu + su * 0.12 - 0.03, cu + su * 0.12 + 0.03, 0.1, 0.16, z0 + 0.9, z0 + 1.4, r["metal"] if "metal" in r else kasa)
    ckutu(g, fc, cu - 0.04, cu + 0.04, 0.1, 0.13, z0, z0 + h, kasa)
    if sacak:
        ckutu(g, fc, cu - w / 2 - 0.5, cu + w / 2 + 0.5, 0, 1.3, z0 + h + 0.25, z0 + h + 0.42, kasa)
    ckutu(g, fc, cu - w / 2 - 0.3, cu + w / 2 + 0.3, 0, 0.9, z0 - 0.12, z0 + 0.0, r.get("beton", kasa))     # basamak


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
    # Vitrin camı ayrı malzeme: gece dükkân kapanana kadar içi ışıklı görünür (Unity GeceIsiklari)
    vcam = r["cam"]
    if vcam.startswith("Cam_"):
        vcam = "Vitrin_" + vcam[4:]
        HEX[vcam] = HEX[r["cam"]]
    cdortgen(g, fc, u0, u1, 0.02, z0, z1, vcam)
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

YOL_SABIT = dict(asfalt="#6f7277", asfalt_koyu="#676a6f", serit="#f2f2ee", kaldirim="#ddd7cc", kaldirim2="#d1cabd",
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
    """Modern sokak lambası: ince yuvarlak direk, yola doğru hafif kavisli kol, yassı LED başlık.
    (kol_x, kol_y) kolun uzandığı yön (yola doğru). Başlığın altındaki YIsik malzemesi gece yanar."""
    z = Z_KAL
    gri = M("LambaGri", "#3d4247")
    silindir(g, x, y, z, 0.12, 0.06, 8, gri, kabuk=False)                         # taban flanşı
    silindir(g, x, y, z + 0.12, 4.4, 0.028, 8, gri, kabuk=False)                  # ince direk
    # Kavisli kol: direğin tepesinden üç kısa parçayla yola doğru kıvrılır
    pts = [(0.0, 4.5), (0.25, 4.66), (0.6, 4.74), (1.05, 4.76)]
    for (a0, h0), (a1, h1) in zip(pts, pts[1:]):
        cubuk(g, (x + kol_x * a0, y + kol_y * a0, z + h0), (x + kol_x * a1, y + kol_y * a1, z + h1), 0.022, gri, kabuk=False)
    # Yassı LED başlık (kol yönünde uzun, ince)
    ux, uy = x + kol_x * 1.2, y + kol_y * 1.2
    ex, ey = (0.34, 0.1) if abs(kol_x) >= abs(kol_y) else (0.1, 0.34)
    if abs(kol_x) > 0.1 and abs(kol_y) > 0.1:
        ex, ey = 0.22, 0.22
    kutu(g, ux - ex, ux + ex, uy - ey, uy + ey, z + 4.7, z + 4.79, gri)
    kutu(g, ux - ex + 0.04, ux + ex - 0.04, uy - ey + 0.03, uy + ey - 0.03, z + 4.68, z + 4.7, r["isik"])
    if DETAY:
        isik_havuzu(g, ux, uy, z)


def trafik_lambasi(g, x, y, yon_x, yon_y, r):
    """Kavşak köşesinde trafik ışığı: ince direk, dar ve kısa ışık kutusu; ışıklar (yon_x, yon_y) yönüne bakar."""
    z = Z_KAL
    silindir(g, x, y, z, 2.95, 0.03, 6, r["koyu"], kabuk=False)
    hx, hy = x, y
    w = 0.085                                    # kutu yarı genişliği (eskiden 0.14: çok kalındı)
    kutu(g, hx - w, hx + w, hy - w, hy + w, z + 2.25, z + 2.95, r["koyu"])
    kutu(g, hx - w - 0.015, hx + w + 0.015, hy - w - 0.015, hy + w + 0.015, z + 2.95, z + 2.98, r["koyu"])   # ince şapka
    for i, renk in enumerate(("kirmizi", "sari", "yesil")):
        zz = z + 2.83 - i * 0.22
        cx, cy = hx + yon_x * w, hy + yon_y * w
        ex, ey = (0.02, 0.06) if yon_x else (0.06, 0.02)
        kutu(g, cx - ex, cx + ex, cy - ey, cy + ey, zz - 0.06, zz + 0.06, r[renk])
    # Yaya düğmesi
    kutu(g, x - 0.04, x + 0.04, y - 0.04, y + 0.04, z + 1.0, z + 1.14, r["sari"])


def yuvarlak_kose(g, sx, sy, r, rad=0.85, ax=YA, ay=YA):
    """Kavşak köşesindeki kaldırım: asfalta bakan köşesi yuvarlatılmış (referanstaki gibi yumuşak bordür).
    sx, sy: köşenin yönü (±1). Kare (ax..YK) × (ay..YK), (ax, ay) köşesi rad yarıçapla yuvarlanır.
    ax, ay: dikey ve yatay yolun asfalt yarı genişliği (cadde daha geniş)."""
    rad = min(rad, (YK - max(ax, ay)) * 0.8)
    cx, cy = sx * (ax + rad), sy * (ay + rad)
    pts = [(sx * YK, sy * YK), (sx * ax, sy * YK)]
    n = 6
    for i in range(n + 1):
        a = math.pi * 0.5 * i / n
        # (ax, ay+rad) noktasından (ax+rad, ay) noktasına çeyrek yay
        pts.append((cx - sx * rad * math.cos(a), cy - sy * rad * math.sin(a)))
    pts.append((sx * YK, sy * ay))
    # Saat yönünün tersine sırala (yüz normali yukarı)
    alan = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    if alan < 0:
        pts.reverse()
    b = len(g.f)
    prizma_poligon(g, pts, 0.0, Z_KAL, r["kaldirim"], kabuk=False)
    cizgisiz(g, b)


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


def zebra(g, eksen, merkez, r, uzun=1.6, yari=YA):
    """Yaya geçidi: eksen "y" ise yol Y boyunca gider, çizgiler X'e dizilir; merkez = (x, y). yari: asfaltın yarı genişliği."""
    cx, cy = merkez
    n = 7 if yari <= YA + 0.05 else 9
    for i in range(n):
        u = -yari + 0.35 + i * (2 * yari - 0.7) / (n - 1)
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


def yol_duz_agacli(r, rnd):
    """Ağaçlı düz yol: iki kaldırımda ağaç çukurları ve yuvarlak ağaçlar (referanstaki ağaçlı caddeler)."""
    g = yol_duz(r)
    for sx in (-1, 1):
        for y in (-2.6, 2.6):
            x = sx * 4.3
            b = len(g.f)
            kutu(g, x - 0.45, x + 0.45, y - 0.45, y + 0.45, Z_KAL - 0.02, Z_KAL + 0.01, r["toprak"], atla=("zn",))
            cizgisiz(g, b)
            boy = rnd.uniform(1.2, 1.6)
            silindir(g, x, y, Z_KAL, boy, 0.11, 6, r["govde"])
            rr = rnd.uniform(0.75, 0.95)
            renk = rnd.choice(r["agac"])
            top(g, (x, y, Z_KAL + boy + rr * 0.7), rr, rr, rr * 0.95, renk)
            top(g, (x + 0.3 * sx, y + 0.2, Z_KAL + boy + rr * 0.3), rr * 0.6, rr * 0.6, rr * 0.55, renk)
    return g


def yol_kose(r):
    """Kanonik köşe: kuzey (−Y) ve doğu (−X) açık; dış kaldırımlar batı (+X) ve güneyde (+Y)."""
    g = Govde()
    zemin_levha(g, -YK, YA, -YK, YA, Z_ASF, r["asfalt"])
    kaldirim(g, YA, YK, -YK, YK, r, ("xn",))
    kaldirim(g, -YK, YA, YA, YK, r, ("yn",))
    yuvarlak_kose(g, -1, -1, r)
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
    yuvarlak_kose(g, 1, -1, r)
    yuvarlak_kose(g, -1, -1, r)
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
        yuvarlak_kose(g, sx, sy, r)
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


# ---------- geniş caddeler (iki şeritli gidiş, iki şeritli dönüş) ve kavşak varyantları
# Ana caddeler (oyunda her ikinci blok yolu) daha geniş: asfalt ±YC, kaldırım daha dar (binaların önünde ayrıca kaldırım var).
YC = 4.3


def kesik_cizgi(g, eksen, a, b, off, r, w=0.07):
    """off kadar kaydırılmış kesik şerit çizgisi (a..b, 2,5 m periyot)."""
    p = a
    while p < b - 0.01:
        q0, q1 = p + 0.65, min(b, p + 1.85)
        if q0 < b:
            if eksen == "y":
                boya(g, off - w, off + w, q0, q1, r["serit"])
            else:
                boya(g, q0, q1, off - w, off + w, r["serit"])
        p += 2.5


def serit_kol(g, eksen, a, b, h, r):
    """Kolun yol çizgileri: sokakta kesik orta çizgi; caddede çift düz orta çizgi + iki kesik şerit çizgisi."""
    if b - a < 0.3:
        return
    if h <= YA + 0.05:
        orta_serit(g, eksen, a, b, r)
        return
    for off in (-0.13, 0.13):
        if eksen == "y":
            boya(g, off - 0.05, off + 0.05, a, b, r["sari"])
        else:
            boya(g, a, b, off - 0.05, off + 0.05, r["sari"])
    kesik_cizgi(g, eksen, a, b, h / 2 + 0.05, r)
    kesik_cizgi(g, eksen, a, b, -h / 2 - 0.05, r)


def yol_cadde(r, rnd=None, agacli=False):
    """Kanonik düz cadde: kuzey-güney (Blender Y boyunca), dört şerit, çift sarı orta çizgi."""
    g = Govde()
    zemin_levha(g, -YC, YC, -YK, YK, Z_ASF, r["asfalt"])
    kaldirim(g, YC, YK, -YK, YK, r, ("xn",))
    kaldirim(g, -YK, -YC, -YK, YK, r, ("xp",))
    serit_kol(g, "y", -YK, YK, YC, r)
    sokak_lambasi(g, 4.7, 0.0, -1.0, 0.0, r)
    rogar(g, 1.0, 2.6, Z_ASF)
    izgara(g, 3.85, 4.2, -3.3, -2.6, Z_ASF)
    izgara(g, -4.2, -3.85, 2.0, 2.7, Z_ASF)
    if agacli and rnd is not None:
        for sx in (-1, 1):
            for y in ((-2.6, 2.6) if sx > 0 else (-3.4, 0.0, 3.4)):
                x = sx * 4.66
                b = len(g.f)
                kutu(g, x - 0.28, x + 0.28, y - 0.4, y + 0.4, Z_KAL - 0.02, Z_KAL + 0.01, r["toprak"], atla=("zn",))
                cizgisiz(g, b)
                boy = rnd.uniform(1.4, 1.8)
                silindir(g, x, y, Z_KAL, boy, 0.1, 6, r["govde"])
                rr = rnd.uniform(0.7, 0.85)
                renk = rnd.choice(r["agac"])
                top(g, (x - sx * 0.15, y, Z_KAL + boy + rr * 0.7), rr, rr, rr * 0.95, renk)
    return g


def yol_kavsak_g(r, kollar, hx, hy):
    """Genel kavşak/köşe parçası. kollar: "N" (−Y), "S" (+Y), "E" (−X), "W" (+X) — Unity'deki kanonik yönler.
    hx: kuzey-güney yolun asfalt yarı genişliği, hy: doğu-batı yolun (sokak YA, cadde YC)."""
    g = Govde()
    N, S, E, W = ("N" in kollar), ("S" in kollar), ("E" in kollar), ("W" in kollar)
    n = N + S + E + W
    x_lo, x_hi = (-YK if E else -hx), (YK if W else hx)
    y_lo, y_hi = (-YK if N else -hy), (YK if S else hy)
    zemin_levha(g, x_lo, x_hi, y_lo, y_hi, Z_ASF, r["asfalt"])
    # Kapalı kenarlarda düz kaldırım (önce batı/doğu tam boy, sonra kuzey/güney aradaki boşluk)
    if not W:
        kaldirim(g, hx, YK, -YK, YK, r, ("xn",))
    if not E:
        kaldirim(g, -YK, -hx, -YK, YK, r, ("xp",))
    sx0, sx1 = (-hx if not E else -YK), (hx if not W else YK)
    if not S:
        kaldirim(g, sx0, sx1, hy, YK, r, ("yn",))
    if not N:
        kaldirim(g, sx0, sx1, -YK, -hy, r, ("yp",))
    # İki kolun arasındaki köşeler: yuvarlatılmış kaldırım
    for sx, kx in ((1, W), (-1, E)):
        for sy, ky in ((1, S), (-1, N)):
            if kx and ky:
                yuvarlak_kose(g, sx, sy, r, ax=hx, ay=hy)
    # Yaya geçitleri ve şerit çizgileri
    zeb = n >= 3
    d_y = hy + (1.6 if zeb else 0.1)       # kuzey/güney kolunda çizgi bu mesafeden başlar
    d_x = hx + (1.6 if zeb else 0.1)
    if n == 2:
        d_y = d_x = 0.1
    # Yaya geçidi karenin içinde kalsın (caddede kol kısa: geçit biraz daha dar ve kavşağa yakın)
    def zb(h):
        uz = max(0.9, min(1.6, YK - h - 0.1))
        return YK - uz / 2 - 0.05, uz
    (zy, uy), (zx, ux) = zb(hy), zb(hx)
    if zeb:
        d_y, d_x = zy + uy / 2 + 0.1, zx + ux / 2 + 0.1
    if N:
        if zeb: zebra(g, "y", (0.0, -zy), r, uzun=uy, yari=hx)
        serit_kol(g, "y", -YK, -d_y, hx, r)
    if S:
        if zeb: zebra(g, "y", (0.0, zy), r, uzun=uy, yari=hx)
        serit_kol(g, "y", d_y, YK, hx, r)
    if E:
        if zeb: zebra(g, "x", (-zx, 0.0), r, uzun=ux, yari=hy)
        serit_kol(g, "x", -YK, -d_x, hy, r)
    if W:
        if zeb: zebra(g, "x", (zx, 0.0), r, uzun=ux, yari=hy)
        serit_kol(g, "x", d_x, YK, hy, r)
    # Köşe noktaları (kaldırım ortası)
    def kp(sx, sy):
        return sx * (hx + (YK - hx) * 0.5), sy * (hy + (YK - hy) * 0.5)
    if n == 4:
        for (sx, sy, yx, yy) in ((1, -1, 0.0, 1.0), (-1, 1, 0.0, -1.0), (1, 1, -1.0, 0.0), (-1, -1, 1.0, 0.0)):
            px, py = kp(sx, sy)
            trafik_lambasi(g, px, py, yx, yy, r)
    elif n == 3:
        kapali = "S" if not S else "N" if not N else "W" if not W else "E"
        if kapali == "S":
            sokak_lambasi(g, 0.0, hy + (YK - hy) * 0.6, 0.0, -1.0, r)
            for sx, tur in ((1, "dikkat"), (-1, "yaya")):
                px, py = kp(sx, -1)
                yol_tabelasi(g, px, py, r, tur)
    elif n == 2 and not (N and S) and not (E and W):
        # Köşe: dış köşede lamba ve küçük ağaç çukuru
        sx = 1 if not W else -1
        sy = 1 if not S else -1
        px, py = kp(sx, sy)
        sokak_lambasi(g, sx * (YK - 0.5), sy * (YK - 0.5), -sx * 0.7, -sy * 0.7, r)
        b = len(g.f)
        tx, ty = sx * (YK - 0.65), sy * (hy + (YK - hy) * 0.2)
        kutu(g, tx - 0.3, tx + 0.3, ty - 0.3, ty + 0.3, Z_KAL - 0.02, Z_KAL + 0.01, r["toprak"], atla=("zn",))
        cizgisiz(g, b)
        top(g, (tx, ty, Z_KAL + 0.3), 0.36, 0.36, 0.3, r["cali"])
    rogar(g, 1.3, -1.1, Z_ASF)
    return g


def cadde_parcalari(r, rnd):
    """Geniş cadde ve sokakla birleştiği kavşaklar. Ad kuralı (Unity City3D.AddRoad bunu arar):
    Yol_kavsak_ca: kuzey-güney cadde × doğu-batı sokak · Yol_kavsak_cc: iki cadde
    Yol_t_XY: X = yatay kol (batı-doğu), Y = gövde (kuzey); c = cadde, s = sokak
    Yol_kose_XY: X = kuzey kolu, Y = doğu kolu."""
    w = {"c": YC, "s": YA}
    liste = [("Yol_cadde_01", yol_cadde(r)), ("Yol_cadde_agacli", yol_cadde(r, rnd, True)),
             ("Yol_kavsak_ca", yol_kavsak_g(r, "NSEW", YC, YA)), ("Yol_kavsak_cc", yol_kavsak_g(r, "NSEW", YC, YC))]
    for bar, govde in (("c", "s"), ("s", "c"), ("c", "c")):
        liste.append((f"Yol_t_{bar}{govde}", yol_kavsak_g(r, "WNE", w[govde], w[bar])))
    for kuzey, dogu in (("c", "s"), ("s", "c"), ("c", "c")):
        liste.append((f"Yol_kose_{kuzey}{dogu}", yol_kavsak_g(r, "NE", w[kuzey], w[dogu])))
    return liste


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
    liste = [("Yol_lane_01", yol_duz(r)), ("Yol_lane_agacli", yol_duz_agacli(r, rnd)), ("Yol_corner_01", yol_kose(r)), ("Yol_t_intersection_01", yol_t(r)),
             ("Yol_intersection", yol_kavsak(r)), ("Yol_sidewalk", zemin_kaldirim(r)), ("Yol_grass", zemin_cim(r, rnd))]
    for i in range(3):
        liste.append((f"Agac_yuvarlak_{i + 1}", agac_modeli(r, rnd, "yuvarlak", i)))
    for i in range(2):
        liste.append((f"Agac_cam_{i + 1}", agac_modeli(r, rnd, "cam", i)))
    for i in range(2):
        liste.append((f"Cali_{i + 1}", agac_modeli(r, rnd, "cali", i)))
    liste += cadde_parcalari(r, rnd)
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
    if DETAY:
        isik_havuzu(g, x, y, z0, ic=0.9, dis=1.7)


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
            if rnd.random() < 0.12:
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


def cit_hat(g, x0, y0, x1, y1, z0, mat, h=0.85, aralik=1.0):
    """Alçak çit: dikmeler ve iki yatay lata (x0,y0) → (x1,y1)."""
    L = math.hypot(x1 - x0, y1 - y0)
    n = max(1, int(L / aralik))
    for i in range(n + 1):
        t = i / n
        x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        kutu(g, x - 0.04, x + 0.04, y - 0.04, y + 0.04, z0, z0 + h, mat)
    for zz in (0.35, h - 0.08):
        cubuk(g, (x0, y0, z0 + zz), (x1, y1, z0 + zz), 0.05, mat, kabuk=False)


def avlu_agac(g, x, y, z0, r, rnd, boy=1.3, rr=0.75):
    silindir(g, x, y, z0, boy, 0.09, 6, r["govde"])
    renk = rnd.choice(r["agac"])
    top(g, (x, y, z0 + boy + rr * 0.7), rr, rr, rr * 0.95, renk)


ARABA_RENK = ("#d9473a", "#3f73e6", "#f6f3ec", "#2b2d31", "#f2c14a", "#4fb36a", "#a7abb0", "#8a5a9a", "#e08a3a")


def avlu_otopark_agacli(r, rnd):
    """Dolu otopark: iki sıra park yeri (çoğu dolu), uçlarda ağaçlı adacıklar, aydınlatma direği."""
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_ASF, r["asfalt"])
    renkler = [M("Araba", c) for c in ARABA_RENK]
    for sy in (-1, 1):
        for i in range(5):
            x = -3.75 + i * 2.15
            boya(g, x - 0.05, x + 0.05, min(sy * 1.3, sy * 4.9), max(sy * 1.3, sy * 4.9), r["serit"])
        for i in range(4):
            x = -2.67 + i * 2.15
            if rnd.random() < 0.85:
                park_araba(g, x, sy * 3.05, math.pi / 2, rnd.choice(renkler), r, Z_ASF)
        for sx in (-1, 1):
            b = len(g.f)
            kutu(g, sx * 4.0, sx * 4.95, min(sy * 1.3, sy * 4.95), max(sy * 1.3, sy * 4.95), 0.0, Z_KAL, r["bordur"],
                 atla=("zn",), yuzler={"zp": r["cim"]})
            cizgisiz(g, b)
            avlu_agac(g, sx * 4.47, sy * 3.1, Z_KAL, r, rnd)
    for x in (-1.2, 1.6):                                                      # yön okları
        boya(g, x - 0.5, x + 0.5, -0.06, 0.06, r["serit"])
    park_lambasi(g, 0.0, 4.6, r, Z_ASF)
    return g


def avlu_karma(r, rnd):
    """Yarısı otopark, yarısı bahçe: ahşap çit, ağaçlar, bank, çalılar, çöp konteynerleri."""
    g = Govde()
    zemin_levha(g, -YK, 0.0, -YK, YK, Z_ASF, r["asfalt"])
    zemin_levha(g, 0.0, YK, -YK, YK, ZC, r["cim"])
    renkler = [M("Araba", c) for c in ARABA_RENK]
    for i in range(4):
        y = -3.6 + i * 2.4
        boya(g, -4.9, -0.6, y - 1.2 - 0.05, y - 1.2 + 0.05, r["serit"])
        if rnd.random() < 0.8:
            park_araba(g, -2.6, y, 0.0, rnd.choice(renkler), r, Z_ASF)
    cit_hat(g, 0.15, -4.8, 0.15, -0.8, ZC, r["ahsap"])
    cit_hat(g, 0.15, 0.8, 0.15, 4.8, ZC, r["ahsap"])
    for (x, y) in ((2.2, -3.4), (3.9, 3.3), (1.6, 3.9)):
        avlu_agac(g, x, y, ZC, r, rnd)
    bank(g, 3.0, 0.0, math.pi / 2, r)
    for i in range(5):
        top(g, (4.5, -2.0 + i * 0.6, ZC + 0.3), 0.36, 0.32, 0.3, r["cali"])
    cop = M("Konteyner", "#4f7a4a")
    for i in range(2):
        x = -4.4 + i * 1.3
        kutu(g, x - 0.5, x + 0.5, 4.2, 4.85, Z_ASF, Z_ASF + 1.0, cop)
        kutu(g, x - 0.52, x + 0.52, 4.18, 4.87, Z_ASF + 1.0, Z_ASF + 1.08, r["koyu"])
    return g


def avlu_oyun(r, rnd):
    """Çocuk oyun alanı: kum havuzu, kaydırak, salıncak, ağaçlar, banklar, çevresinde alçak çit."""
    g = Govde()
    cim_taban(g, r)
    duz_poligon(g, [(-3.6, -3.0), (3.6, -3.0), (3.6, 3.0), (-3.6, 3.0)], ZC + 0.01, r["kum"])
    kutu(g, -3.2, -1.2, -2.6, -0.8, ZC, ZC + 0.25, r["ahsap"])                 # kum havuzu
    duz_poligon(g, [(-3.05, -2.45), (-1.35, -2.45), (-1.35, -0.95), (-3.05, -0.95)], ZC + 0.255, r["kum"])
    kutu(g, 0.6, 1.6, 0.6, 1.6, ZC, ZC + 1.6, r["kirmizi"])                    # kaydırak kulesi
    kutu(g, 0.5, 1.7, 0.5, 1.7, ZC + 1.6, ZC + 1.75, r["sari"])
    yonlu_kutu(g, (1.1, -0.6, ZC + 0.85), (0.35, 0, 0), (0, 1.2, -0.8), (0, 0, 0.04), r["mavi"])
    for x in (-1.0, 2.6):                                                      # salıncak
        cubuk(g, (x, 1.6, ZC), (x, 2.2, ZC + 2.0), 0.07, r["metal"])
        cubuk(g, (x, 2.8, ZC), (x, 2.2, ZC + 2.0), 0.07, r["metal"])
    cubuk(g, (-1.0, 2.2, ZC + 2.0), (2.6, 2.2, ZC + 2.0), 0.07, r["metal"])
    for x in (-0.1, 1.7):
        for dx in (-0.25, 0.25):
            cubuk(g, (x + dx, 2.2, ZC + 2.0), (x + dx, 2.2, ZC + 0.5), 0.02, r["koyu"], kabuk=False)
        kutu(g, x - 0.3, x + 0.3, 2.05, 2.35, ZC + 0.45, ZC + 0.52, r["kirmizi"])
    for (x, y) in ((-4.3, -4.3), (4.3, -4.3), (-4.3, 4.3), (4.3, 4.3)):
        avlu_agac(g, x, y, ZC, r, rnd, boy=1.5, rr=0.8)
    bank(g, 0.0, -3.9, 0.0, r)
    bank(g, -3.9, 1.0, math.pi / 2, r)
    m = r["metal"]
    for a, b in (((-4.9, -4.9), (-0.8, -4.9)), ((0.8, -4.9), (4.9, -4.9)), ((-4.9, 4.9), (-0.8, 4.9)), ((0.8, 4.9), (4.9, 4.9)),
                 ((-4.9, -4.9), (-4.9, -0.8)), ((-4.9, 0.8), (-4.9, 4.9)), ((4.9, -4.9), (4.9, -0.8)), ((4.9, 0.8), (4.9, 4.9))):
        cit_hat(g, a[0], a[1], b[0], b[1], ZC, m, h=0.7)
    return g


def avlu_parcalari():
    r = park_renkleri()
    r["cam_koyu"] = M("CamKoyu", "#5f7f99")
    rnd = random.Random(TOHUM * 313)
    return [("Avlu_otopark", avlu_otopark(r, rnd)), ("Avlu_havuz", avlu_havuz(r, rnd)), ("Avlu_bahce", avlu_bahce(r, rnd)),
            ("Avlu_otopark_agacli", avlu_otopark_agacli(r, rnd)), ("Avlu_karma", avlu_karma(r, rnd)), ("Avlu_oyun", avlu_oyun(r, rnd)),
            ("Kir_cicek_1", kir_cicek(r, rnd)), ("Kir_cicek_2", kir_cicek(r, rnd)), ("Kir_kaya_1", kir_kaya(r, rnd))]



# ------------------------------------------------------------------ kır alanları (parseller: 50 × 40 m, oyunda tarla boyutuna uyar)
# Malzeme adları "Kir..." ile başlar: oyun (City3D.KirMevsim) mevsime ve havaya göre renklerini değiştirir
# (ilkbahar taze yeşil, yaz sonu kurak/sarı, sonbahar ıslak/kahve, kış karlı). Sabit renkli olanlar (ahşap, inek, sera) değişmez.

KIR_RENK = dict(cim="#6f9a4a", cim_koyu="#5a8540", toprak="#7e5f42", toprak_koyu="#6a4e36", ekin="#7fa64a", ekin_uc="#97bb5a",
                bugday="#cfae55", bugday_uc="#e0c46c", kolza="#e6cf3e", kolza_uc="#f2de5a", saman="#c9b07a", balya="#d8bf72",
                asma="#5f8a3e", ahsap="#8a6a4a", yaprak="#6b9a45", yaprak_koyu="#557f3a", meyve="#d9473a", govde="#6b4a33",
                cali="#5f8f4a", su="#5f9fc8", kamis="#8fa25a", sera="#dfe9ee", sera_kenar="#9aa8b0", inek="#f2efe8",
                inek_koyu="#2d2a28", tas="#a49e94", cam="#4f7f4a")
KW, KD = 25.0, 20.0     # parselin yarı boyu (x, y)
KZ = 0.1                # taban üstü


def kir_renkleri():
    ad = {"cim": "KirCim", "cim_koyu": "KirCimKoyu", "toprak": "KirToprak", "toprak_koyu": "KirToprakKoyu", "ekin": "KirEkin",
          "ekin_uc": "KirEkinUc", "bugday": "KirBugday", "bugday_uc": "KirBugdayUc", "kolza": "KirKolza", "kolza_uc": "KirKolzaUc",
          "saman": "KirSaman", "balya": "KirBalya", "asma": "KirAsma", "ahsap": "KirAhsap", "yaprak": "KirYaprak",
          "yaprak_koyu": "KirYaprakKoyu", "meyve": "KirMeyve", "govde": "KirGovde", "cali": "KirCali", "su": "KirSu",
          "kamis": "KirKamis", "sera": "KirSera", "sera_kenar": "KirSeraKenar", "inek": "KirInek", "inek_koyu": "KirInekKoyu",
          "tas": "KirTas", "cam": "KirCam"}
    r = {k: M(ad[k], v) for k, v in KIR_RENK.items()}
    r["cicek"] = [M("KirCicek", c) for c in ("#e0525a", "#f0a0c0", "#f2d04a", "#ffffff", "#9a7ae0")]
    return r


def kir_taban(g, mat, x0=-KW, x1=KW, y0=-KD, y1=KD):
    levha(g, x0, x1, y0, y1, 0.0, KZ, mat)


def kir_sira(g, x0, x1, y, gen, boy, mat, mat_ust, z0=KZ):
    """Ekin sırası: üstü dar yamuk kesitli uzun tümsek (x boyunca)."""
    h, u = gen / 2, gen * 0.22
    pts = [(x0, y - h, z0), (x1, y - h, z0), (x1, y + h, z0), (x0, y + h, z0),
           (x0, y - u, z0 + boy), (x1, y - u, z0 + boy), (x1, y + u, z0 + boy), (x0, y + u, z0 + boy)]
    b = len(g.v)
    g.v += pts
    g.yuz([b + 0, b + 1, b + 5, b + 4], mat, kabuk=False)     # güney yamaç
    g.yuz([b + 2, b + 3, b + 7, b + 6], mat, kabuk=False)     # kuzey yamaç
    g.yuz([b + 4, b + 5, b + 6, b + 7], mat_ust, kabuk=False)  # tepe
    g.yuz([b + 0, b + 4, b + 7, b + 3], mat, kabuk=False)     # uçlar
    g.yuz([b + 1, b + 2, b + 6, b + 5], mat, kabuk=False)


def yatik_silindir(g, c, r, L, aci, n, mat, mat_yuz=None):
    """Yere yatırılmış silindir (rulo saman balyası): ekseni yatay, aci yönünde."""
    ax = (math.cos(aci) * L / 2, math.sin(aci) * L / 2, 0.0)
    yx, yy = -math.sin(aci), math.cos(aci)
    pts = []
    for s in (-1, 1):
        for i in range(n):
            a = 2 * math.pi * i / n
            pts.append((c[0] + s * ax[0] + yx * r * math.cos(a), c[1] + s * ax[1] + yy * r * math.cos(a), c[2] + r * math.sin(a)))
    yuzler = [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)]
    b = len(g.f)
    dis_bukey(g, pts, yuzler, mat)
    if mat_yuz:
        g.fm[b] = g.mat(mat_yuz)
        g.fm[b + 1] = g.mat(mat_yuz)


def kir_agac(g, x, y, r, rnd, boy=None, rr=None, mat=None, cam=False):
    boy = boy or rnd.uniform(2.6, 3.6)
    if cam:
        silindir(g, x, y, KZ, boy * 0.35, 0.22, 6, r["govde"])
        for i, (z, rad, h) in enumerate(((0.3, 1.9, 2.6), (1.5, 1.45, 2.3), (2.6, 1.0, 2.0))):
            koni(g, x, y, KZ + boy * 0.35 + z, h, rad, 7, r["cam"])
        return
    rr = rr or rnd.uniform(1.5, 2.1)
    silindir(g, x, y, KZ, boy, 0.22, 6, r["govde"])
    top(g, (x, y, KZ + boy + rr * 0.6), rr, rr, rr * 0.9, mat or r["yaprak"])
    if rnd.random() < 0.6:
        top(g, (x + rr * 0.5, y + 0.2, KZ + boy + rr * 0.25), rr * 0.6, rr * 0.6, rr * 0.5, r["yaprak_koyu"])


def kir_cit(g, x0, y0, x1, y1, r):
    cit_hat(g, x0, y0, x1, y1, KZ, r["ahsap"], h=1.1, aralik=2.5)


def kir_inek(g, x, y, aci, r, rnd):
    ca, sa = math.cos(aci), math.sin(aci)
    ax, ay, az = (ca * 1.0, sa * 1.0, 0.0), (-sa * 0.42, ca * 0.42, 0.0), (0.0, 0.0, 0.42)
    zc = KZ + 1.15
    yonlu_kutu(g, (x, y, zc), ax, ay, az, r["inek"])
    # lekeler
    for _ in range(2):
        d = rnd.uniform(-0.5, 0.5)
        yonlu_kutu(g, (x + ca * d, y + sa * d, zc + 0.3), (ca * 0.28, sa * 0.28, 0.0), (-sa * 0.44, ca * 0.44, 0.0), (0, 0, 0.14), r["inek_koyu"])
    # baş
    hx, hy = x + ca * 1.25, y + sa * 1.25
    yonlu_kutu(g, (hx, hy, zc + 0.25), (ca * 0.3, sa * 0.3, 0.0), (-sa * 0.26, ca * 0.26, 0.0), (0, 0, 0.26), r["inek"])
    # bacaklar
    for du in (-0.7, 0.7):
        for dv in (-0.28, 0.28):
            px, py = x + ca * du - sa * dv, y + sa * du + ca * dv
            cubuk(g, (px, py, KZ), (px, py, zc - 0.35), 0.16, r["inek_koyu"], kabuk=False)


def kir_ekin(r, rnd, tur):
    """Ekili tarla: x boyunca sıralar. tur: genc (alçak yeşil), bugday (uzun, sarı), kolza (sarı çiçekli)."""
    g = Govde()
    kir_taban(g, r["toprak"])
    mat, ust = {"genc": (r["ekin"], r["ekin_uc"]), "bugday": (r["bugday"], r["bugday_uc"]), "kolza": (r["kolza"], r["kolza_uc"])}[tur]
    boy = {"genc": 0.35, "bugday": 0.95, "kolza": 0.75}[tur]
    aralik = {"genc": 1.3, "bugday": 1.1, "kolza": 1.4}[tur]
    y = -KD + 1.2
    while y < KD - 1.0:
        kir_sira(g, -KW + 1.0, KW - 1.0, y, aralik * 0.78, boy * rnd.uniform(0.9, 1.08), mat, ust)
        y += aralik
    if tur == "bugday" and rnd.random() < 0.7:
        # biçerdöver izi: tarlanın bir ucu biçilmiş
        kutu(g, KW - 9.0, KW - 1.0, -KD + 1.0, KD - 1.0, KZ, KZ + 0.15, r["saman"])
    return g


def kir_surulmus(r, rnd):
    """Sürülmüş toprak: ince karık sırtları."""
    g = Govde()
    kir_taban(g, r["toprak_koyu"])
    y = -KD + 0.8
    while y < KD - 0.6:
        kir_sira(g, -KW + 0.6, KW - 0.6, y, 0.7, 0.28, r["toprak"], r["toprak"])
        y += 0.85
    return g


def kir_saman(r, rnd):
    """Biçilmiş tarla: anız, rulo balyalar, bir köşede balya yığını."""
    g = Govde()
    kir_taban(g, r["saman"])
    y = -KD + 1.0
    while y < KD - 1.0:
        kir_sira(g, -KW + 1.0, KW - 1.0, y, 0.5, 0.12, r["saman"], r["balya"])
        y += 1.6
    for _ in range(rnd.randint(7, 12)):
        x, yy = rnd.uniform(-KW + 3, KW - 3), rnd.uniform(-KD + 3, KD - 3)
        yatik_silindir(g, (x, yy, KZ + 0.75), 0.75, 1.25, rnd.uniform(0, math.pi), 10, r["balya"], r["saman"])
    # köşede kare balya yığını
    bx, by = KW - 6.0, KD - 5.0
    for i in range(3):
        for j in range(2):
            kutu(g, bx + i * 1.3, bx + i * 1.3 + 1.2, by + j * 0.9, by + j * 0.9 + 0.8, KZ, KZ + 0.6, r["balya"])
    for i in range(2):
        kutu(g, bx + 0.6 + i * 1.3, bx + 0.6 + i * 1.3 + 1.2, by + 0.4, by + 1.2, KZ + 0.6, KZ + 1.2, r["balya"])
    return g


def kir_bag(r, rnd):
    """Üzüm bağı: direkli, telli asma sıraları; sıra aralarında çimen."""
    g = Govde()
    kir_taban(g, r["cim_koyu"])
    y = -KD + 2.0
    while y < KD - 1.5:
        x0, x1 = -KW + 2.0, KW - 2.0
        kutu(g, x0 + 0.5, x1 - 0.5, y - 0.35, y + 0.35, KZ + 0.5, KZ + 1.6, r["asma"])
        x = x0
        while x <= x1 + 0.01:
            kutu(g, x - 0.08, x + 0.08, y - 0.08, y + 0.08, KZ, KZ + 1.9, r["ahsap"])
            x += 6.0
        kutu(g, x0, x1, y - 0.6, y + 0.6, KZ, KZ + 0.05, r["toprak"])
        y += 2.8
    return g


def kir_meyve(r, rnd, sik):
    """Meyve bahçesi: çimen üstünde düzenli ağaç sıraları, dallarda meyve."""
    g = Govde()
    kir_taban(g, r["cim"])
    nx, ny = (6, 4) if sik else (5, 3)
    for i in range(nx):
        for j in range(ny):
            x = -KW + 4.0 + i * (2 * KW - 8.0) / (nx - 1)
            y = -KD + 4.0 + j * (2 * KD - 8.0) / (ny - 1)
            kir_agac(g, x + rnd.uniform(-0.3, 0.3), y + rnd.uniform(-0.3, 0.3), r, rnd, boy=rnd.uniform(1.6, 2.1), rr=rnd.uniform(1.6, 1.9))
            b = len(g.f)
            for _ in range(3):
                a = rnd.uniform(0, 2 * math.pi)
                top(g, (x + 1.5 * math.cos(a), y + 1.5 * math.sin(a), KZ + 3.0 + rnd.uniform(-0.4, 0.5)), 0.24, 0.24, 0.24, r["meyve"])
            cizgisiz(g, b)
    kir_cit(g, -KW + 0.5, -KD + 0.5, KW - 0.5, -KD + 0.5, r)
    return g


def kir_cayir(r, rnd, inekli):
    """Çayır: tümsekli çimen, çalılar, çiçekler, bir kenarında ahşap çit; bazen inekler."""
    g = Govde()
    kir_taban(g, r["cim"])
    b = len(g.f)
    for _ in range(12):
        x, y = rnd.uniform(-KW + 3, KW - 3), rnd.uniform(-KD + 3, KD - 3)
        top(g, (x, y, KZ), rnd.uniform(2.5, 5.0), rnd.uniform(2.0, 4.0), rnd.uniform(0.15, 0.35), r["cim_koyu"])
    cizgisiz(g, b)   # tümsekler çizgisiz: çimenin parçası gibi dursun
    for _ in range(9):
        x, y = rnd.uniform(-KW + 2, KW - 2), rnd.uniform(-KD + 2, KD - 2)
        top(g, (x, y, KZ + 0.5), rnd.uniform(0.8, 1.3), rnd.uniform(0.8, 1.3), 0.7, r["cali"])
    b = len(g.f)
    for _ in range(24):
        x, y = rnd.uniform(-KW + 1, KW - 1), rnd.uniform(-KD + 1, KD - 1)
        top(g, (x, y, KZ + 0.15), 0.22, 0.22, 0.16, rnd.choice(r["cicek"]))
    cizgisiz(g, b)
    kir_cit(g, -KW + 0.5, -KD + 0.5, -KW + 0.5, KD - 0.5, r)
    kir_cit(g, -KW + 0.5, KD - 0.5, KW - 0.5, KD - 0.5, r)
    if inekli:
        for _ in range(rnd.randint(3, 5)):
            kir_inek(g, rnd.uniform(-KW + 6, KW - 6), rnd.uniform(-KD + 5, KD - 5), rnd.uniform(0, 2 * math.pi), r, rnd)
        # su yalağı
        kutu(g, 8.0, 11.0, -3.0, -2.0, KZ, KZ + 0.6, r["ahsap"])
        kutu(g, 8.15, 10.85, -2.85, -2.15, KZ + 0.6, KZ + 0.62, r["su"])
    else:
        kir_agac(g, rnd.uniform(-10, 10), rnd.uniform(-8, 8), r, rnd, boy=3.4, rr=2.6)
    return g


def kir_gol(r, rnd):
    """Gölet: düzensiz su yüzeyi, kıyıda kamış ve çalılar, küçük ahşap iskele."""
    g = Govde()
    kir_taban(g, r["cim"])
    pts = []
    n = 16
    for i in range(n):
        a = 2 * math.pi * i / n
        rr = rnd.uniform(0.75, 1.0)
        pts.append((14.0 * rr * math.cos(a), 10.5 * rr * math.sin(a)))
    # kıyı (koyu çimen halkası) ve su
    duz_poligon(g, [(x * 1.12, y * 1.15) for x, y in pts], KZ + 0.02, r["cim_koyu"])
    duz_poligon(g, pts, KZ + 0.05, r["su"])
    for i in range(n):
        if rnd.random() < 0.55:
            x, y = pts[i]
            for _ in range(rnd.randint(3, 6)):
                kx, ky = x * rnd.uniform(0.95, 1.1) + rnd.uniform(-0.6, 0.6), y * rnd.uniform(0.95, 1.1) + rnd.uniform(-0.6, 0.6)
                h = rnd.uniform(0.8, 1.6)
                cubuk(g, (kx, ky, KZ), (kx + rnd.uniform(-0.15, 0.15), ky + rnd.uniform(-0.15, 0.15), KZ + h), 0.09, r["kamis"], kabuk=False)
    for _ in range(6):
        a = rnd.uniform(0, 2 * math.pi)
        x, y = 17.5 * math.cos(a), 13.5 * math.sin(a)
        top(g, (x, y, KZ + 0.6), rnd.uniform(1.0, 1.5), rnd.uniform(1.0, 1.5), 0.8, r["cali"])
    kir_agac(g, -20.0, 14.0, r, rnd, boy=3.6, rr=2.4)
    kir_agac(g, 19.0, -14.0, r, rnd, boy=3.2, rr=2.2)
    # iskele
    kutu(g, -1.0, 1.0, -12.5, -6.0, KZ + 0.35, KZ + 0.5, r["ahsap"])
    for yy in (-12.0, -9.0, -6.5):
        for xx in (-0.85, 0.85):
            kutu(g, xx - 0.1, xx + 0.1, yy - 0.1, yy + 0.1, KZ - 0.2, KZ + 0.4, r["ahsap"])
    return g


def kir_sera(r, rnd):
    """Seralar: toprak üstünde yarım silindir tüneller, su deposu."""
    g = Govde()
    kir_taban(g, r["toprak"])
    for i in range(3):
        y = -12.0 + i * 12.0
        n = 8
        R = 3.6
        pts = []
        for x in (-18.0, 14.0):
            for k in range(n + 1):
                a = math.pi * k / n
                pts.append((x, y + R * math.cos(a), KZ + R * math.sin(a)))
        # yan yüzler
        m = n + 1
        b = len(g.v)
        g.v += pts
        for k in range(n):
            g.yuz([b + k, b + k + 1, b + m + k + 1, b + m + k], r["sera"], kabuk=False)
        g.yuz([b + k for k in range(m)][::-1], r["sera_kenar"], kabuk=False)
        g.yuz([b + m + k for k in range(m)], r["sera_kenar"], kabuk=False)
        for x in (-18.0, -10.0, -2.0, 6.0, 14.0):
            for k in range(n):
                a0, a1 = math.pi * k / n, math.pi * (k + 1) / n
                cubuk(g, (x, y + R * math.cos(a0), KZ + R * math.sin(a0)), (x, y + R * math.cos(a1), KZ + R * math.sin(a1)), 0.1, r["sera_kenar"], kabuk=False)
    silindir(g, 19.0, 10.0, KZ, 3.2, 1.6, 10, r["sera_kenar"])
    kutu(g, 16.5, 21.5, -16.0, -12.0, KZ, KZ + 2.4, r["ahsap"])
    return g


def kir_koru(r, rnd):
    """Koru: çimen üstünde sık ağaç kümesi (yapraklı ve iğne yapraklı), çalılar."""
    g = Govde()
    kir_taban(g, r["cim_koyu"])
    for _ in range(22):
        a, d = rnd.uniform(0, 2 * math.pi), math.sqrt(rnd.random())
        x, y = (KW - 4) * d * math.cos(a), (KD - 4) * d * math.sin(a)
        kir_agac(g, x, y, r, rnd, cam=rnd.random() < 0.35, mat=rnd.choice((r["yaprak"], r["yaprak_koyu"])))
    for _ in range(10):
        x, y = rnd.uniform(-KW + 2, KW - 2), rnd.uniform(-KD + 2, KD - 2)
        top(g, (x, y, KZ + 0.5), rnd.uniform(0.9, 1.4), rnd.uniform(0.9, 1.4), 0.7, r["cali"])
    return g


# Çayır parçaları (20 × 20 m, tabansız): kır zemininin üstüne serpilir; ot kümeleri, çalı, kır çiçeği, kaya, sazlık.
# Ot rengi "KirOt": mevsime göre değişir (ilkbahar taze, yaz sonu sarı, sonbahar zeytin, kış soluk), kar yağınca beyazlar.
CW = 10.0   # yarı boy


def ot_kumesi(g, x, y, rx, ry, h, mat):
    top(g, (x, y, 0.0), rx, ry, h, mat)


def kir_cimen(r, rnd, tur):
    g = Govde()
    ot = M("KirOt", "#7aa64e")
    ot_koyu = M("KirOtKoyu", "#62903f")
    ot_acik = M("KirOtAcik", "#94b95c")
    b = len(g.f)
    n = {"sade": 9, "cicekli": 7, "calili": 6, "kayalik": 6, "sazlik": 5}[tur]
    for _ in range(n):
        x, y = rnd.uniform(-CW + 1.5, CW - 1.5), rnd.uniform(-CW + 1.5, CW - 1.5)
        ot_kumesi(g, x, y, rnd.uniform(1.4, 3.2), rnd.uniform(1.2, 2.8), rnd.uniform(0.25, 0.6), rnd.choice((ot, ot_koyu, ot_acik)))
    if tur == "cicekli":
        for _ in range(4):
            cx, cy = rnd.uniform(-CW + 2, CW - 2), rnd.uniform(-CW + 2, CW - 2)
            renk = rnd.choice(r["cicek"])
            for _ in range(rnd.randint(5, 9)):
                top(g, (cx + rnd.uniform(-1.4, 1.4), cy + rnd.uniform(-1.4, 1.4), 0.35), 0.28, 0.28, 0.2, renk)
    if tur in ("calili", "sade"):
        for _ in range(3 if tur == "calili" else 1):
            x, y = rnd.uniform(-CW + 2, CW - 2), rnd.uniform(-CW + 2, CW - 2)
            top(g, (x, y, 0.55), rnd.uniform(0.9, 1.5), rnd.uniform(0.9, 1.4), 0.8, r["cali"])
            if rnd.random() < 0.5:
                top(g, (x + 0.9, y + 0.3, 0.4), 0.7, 0.6, 0.55, r["cali"])
    if tur == "kayalik":
        for _ in range(rnd.randint(2, 4)):
            x, y = rnd.uniform(-CW + 2, CW - 2), rnd.uniform(-CW + 2, CW - 2)
            s = rnd.uniform(0.6, 1.3)
            top(g, (x, y, s * 0.25), s, s * 0.8, s * 0.55, r["tas"])
            top(g, (x + s * 0.9, y - 0.3, s * 0.15), s * 0.5, s * 0.45, s * 0.35, r["tas"])
    if tur == "sazlik":
        pts = poligon(9, rnd.uniform(2.5, 3.5), rnd.uniform(1.8, 2.6), 0.0, 0.0, rnd.uniform(0, 1))
        duz_poligon(g, [(x * 1.25, y * 1.25) for x, y in pts], 0.03, ot_koyu)
        duz_poligon(g, pts, 0.05, r["su"])
        for x, y in pts:
            for _ in range(3):
                kx, ky = x * rnd.uniform(1.0, 1.25) + rnd.uniform(-0.4, 0.4), y * rnd.uniform(1.0, 1.25) + rnd.uniform(-0.4, 0.4)
                cubuk(g, (kx, ky, 0.0), (kx + rnd.uniform(-0.1, 0.1), ky + rnd.uniform(-0.1, 0.1), rnd.uniform(0.8, 1.5)), 0.08, r["kamis"], kabuk=False)
    cizgisiz(g, b)   # çayır çizgisiz: zeminin bir parçası gibi
    return g


def kir_alanlari():
    r = kir_renkleri()
    rnd = random.Random(TOHUM * 911)
    liste = [("Kir_alan_ekin_1", kir_ekin(r, rnd, "genc")), ("Kir_alan_ekin_2", kir_ekin(r, rnd, "bugday")),
             ("Kir_alan_ekin_3", kir_ekin(r, rnd, "kolza")), ("Kir_alan_ekin_4", kir_ekin(r, rnd, "bugday")),
             ("Kir_alan_surulmus_1", kir_surulmus(r, rnd)), ("Kir_alan_surulmus_2", kir_surulmus(r, rnd)),
             ("Kir_alan_saman_1", kir_saman(r, rnd)), ("Kir_alan_saman_2", kir_saman(r, rnd)),
             ("Kir_alan_bag_1", kir_bag(r, rnd)),
             ("Kir_alan_meyve_1", kir_meyve(r, rnd, True)), ("Kir_alan_meyve_2", kir_meyve(r, rnd, False)),
             ("Kir_alan_cayir_1", kir_cayir(r, rnd, True)), ("Kir_alan_cayir_2", kir_cayir(r, rnd, False)),
             ("Kir_alan_gol_1", kir_gol(r, rnd)), ("Kir_alan_sera_1", kir_sera(r, rnd)), ("Kir_alan_koru_1", kir_koru(r, rnd))]
    for i, tur in enumerate(("sade", "sade", "cicekli", "cicekli", "calili", "kayalik", "sazlik")):
        liste.append((f"Kir_cimen_{tur}_{i + 1}", kir_cimen(r, rnd, tur)))
    sonuc = []
    for ad, g in liste:
        if CIZGI and not ad.startswith("Kir_cimen_"):
            kabuk(g, CIZGI_KALINLIK * 1.6)   # parsel 50 m: oyunda ~5 kareye küçülür, çizgi orantılı kalınlaşır
        sonuc.append((ad, g, ortala(g)))
    return sonuc

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


def kaldirim_arsa(g, PW, PD, r, on_d=2.2, zemin="cim"):
    """Arsa: önde kaldırım şeridi, gerisi çim (şehir griye boğulmasın)."""
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
            yan_kanat = y1 - y0 > 6.0
            for fc, L in ((on, x1 - x0), (sag, y1 - y0), (sol, y1 - y0), (arka, x1 - x0)):
                atla = ((L / 2 - 1.3, L / 2 + 1.3),) if (fc is on and yan_kanat) else ()
                pencereler(g, fc, L, z0 + 0.5, kat, KY, r, w=0.8, h=1.4, aralik=1.9, sade=fc is arka, atla=atla)
            if yan_kanat:
                # Sokağa bakan kanat ucunda apartman girişi; avluya bakan yüzde ikinci kapı
                ckutu(g, on, (x1 - x0) / 2 - 0.65, (x1 - x0) / 2 + 0.65, 0, 0.06, z0, z0 + 2.4, r["kapi"])
                cdortgen(g, on, (x1 - x0) / 2 - 0.45, (x1 - x0) / 2 + 0.45, 0.07, z0 + 1.6, z0 + 2.25, r["cam"])
                ckutu(g, on, (x1 - x0) / 2 - 1.0, (x1 - x0) / 2 + 1.0, 0, 0.9, z0 + 2.5, z0 + 2.65, r["suslu"])
                ic = sag if x0 < 8.0 else sol
                ckutu(g, ic, (y1 - y0) * 0.35 - 0.6, (y1 - y0) * 0.35 + 0.6, 0, 0.06, z0, z0 + 2.3, r["kapi"])
            else:
                ckutu(g, on, (x1 - x0) / 2 - 0.7, (x1 - x0) / 2 + 0.7, 0, 0.06, z0, z0 + 2.4, r["kapi"])
                ckutu(g, on, (x1 - x0) / 2 - 1.0, (x1 - x0) / 2 + 1.0, 0, 0.8, z0 + 2.5, z0 + 2.65, r["suslu"])
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
                    if f == 0 and fc is on and u0 < L / 2 + 1.3 and u1 > L / 2 - 1.3:
                        continue                                               # kapının yeri
                    zz = z0 + f * KY
                    cdortgen(g, fc, u0, u1, 0.01, zz + 0.4, zz + KY - 0.3, r["cam"])
                    ckutu(g, fc, (u0 + u1) / 2 - 0.04, (u0 + u1) / 2 + 0.04, 0, 0.06, zz + 0.4, zz + KY - 0.3, r["cerceve"])
                ckutu(g, fc, -0.03, L + 0.03, 0, 0.12, z0 + f * KY + KY - 0.3, z0 + f * KY + KY - 0.1, r["suslu"])
        # Giriş: saçağın altında çift kanatlı cam kapı (zemindeki pencerenin önünde, duvara gömülü kasa)
        cam_kapi(g, on, W / 2, z0, r, w=1.9, h=2.6, sacak=False)
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
        for k in range(1, 8):                                                  # lobi camı kayıtları
            ckutu(g, lfc, 0.6 + k * 1.15 - 0.04, 0.6 + k * 1.15 + 0.04, 0, 0.08, z0 + 0.3, z0 + 4.4, r["koyu"])
        cam_kapi(g, lfc, 3.6, z0, r, w=1.7, h=2.6, kasa=r["koyu"])
        cam_kapi(g, lfc, 6.8, z0, r, w=1.7, h=2.6, kasa=r["koyu"])
        tabela(g, lfc, 5.2, z0 + 4.65, 4.0, 0.5, 0.1, r, "CITY TOWER", renk=r["koyu"], yazi_renk=r["beyaz"], oran=0.6)
        z = z0 + 5.0
        yari = 4.4
        for kis in range(3):
            nk = kat // 3
            h = nk * KY
            kutu(g, cx - yari, cx + yari, cy - yari * 0.9, cy + yari * 0.9, z, z + h, r["duvar"], atla=("zn",))
            fc = Cephe((cx - yari, cy - yari * 0.9, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
            yanlar = ((fc, 2 * yari), (Cephe((cx + yari, cy - yari * 0.9, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0)), 1.8 * yari),
                      (Cephe((cx - yari, cy + yari * 0.9, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0)), 1.8 * yari),
                      (Cephe((cx + yari, cy + yari * 0.9, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0)), 2 * yari))
            for side, L in yanlar:
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
        cam_kapi(g, lfc, (W - 3.0) / 2, z0, r, w=2.0, h=2.7, sacak=False)
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
        for kfc in cepheler(tx - 1.4, tx + 1.4, ty - 1.4, ty + 1.4):
            cdortgen(g, kfc, 0.4, 2.4, 0.01, Ht + 4.4, Ht + 5.6, r["cam"])
        ckutu(g, on, W - 1.6, W - 0.6, 0, 0.06, z0, z0 + 2.3, r["kapi"])                # personel kapısı
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
    for aile, tipler, fn, adet in (("Konut", KONUT_TIPLER, konut, 3), ("Buro", BURO_TIPLER, buro, 3),
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



# ------------------------------------------------------------------ bitişik blok binaları (referanstaki kapalı bloklar)
# Her model tam 10 m × 10 m kare: bina kaldırıma yaslanır, iki yanı komşusuna değer (aralarında boşluk kalmaz),
# arkası bloğun avlusuna bakar. Unity bunları ölçeklemeden (1 m = 0,1 kare) yerleştirir.
#   Blok_duz_Nk_V  : sokağa (−Y) bakan ara bina, N kat
#   Blok_kose_Nk_V : köşe binası, iki sokağa (−Y ve −X) bakan L biçimli
BLOK_FY = 1.4       # önde kalan kaldırım şeridi
BLOK_BY = 8.2       # binanın arka duvarı (arkada 1,8 m avlu şeridi)
BLOK_ZEMIN = 3.4    # zemin kat yüksekliği (dükkân / giriş)
BLOK_KAT = 3.0      # üst kat yüksekliği
BLOK_KATLAR = (2, 3, 4, 5, 6, 7, 9)
BLOK_STIL_RENK = {
    "siva": ["#f1e4c8", "#ecd3a4", "#f0cdb0", "#d9dfc6", "#cfdbe3", "#e8d6c8", "#f4f0e6", "#e9c9b5", "#dfe6d0"],
    "tugla": ["#a8503a", "#9c4a35", "#b8634a", "#8f4a3a"],
    "modern": ["#e9e7e2", "#cfd3d6", "#d8d2c6", "#b9c0c6", "#f2efe8"],
    "klasik": ["#e6c98f", "#d9b98a", "#e8d6b0", "#cbb79a", "#e2c4a8", "#c9d3c4"],
    "cumbali": ["#e9d9a6", "#d6e0c8", "#f0d6c0", "#d8dde6", "#f2e6cf", "#e7c9c3"],
    # Referans (CityRT Avrupa kataloğu) stilleri
    "haussmann": ["#e9dcc3", "#ecdfc8", "#e2d3b8", "#efe5d2"],                 # krem taş, çinko mansart
    "kanal": ["#7a3b2e", "#8e4a36", "#5e3328", "#a0583e", "#6b3a2e"],          # Hollanda kanal evleri (basamaklı alınlık)
    "barok": ["#cfe0c0", "#f2d68a", "#f0c2b8", "#c8d8e8", "#e8b8c8", "#f4e3b0"],  # pastel barok alınlıklı evler
    "loft": ["#8e4331", "#7a3b2e", "#9c4a35"],                                 # antrepo lofu (tuğla depo)
    "modern_tugla": ["#5a3a2e", "#6b4636", "#4e3a33", "#7a5040"],              # koyu tuğla, büyük pencere, yeşil çatı
    "galerili": ["#e9e7e2", "#d8d2c6", "#cfd8dc", "#efe3cf"],                  # dış galerili (koridor balkonlu) blok
    "teras": ["#f2efe8", "#e6e2da", "#dfe4e6"],                                # teraslı modern ev
}
BLOK_CINKO = "#6f7a86"
DUKKAN_TEMA_RENK = {"firin": "#8a5a32", "eczane": "#2f8f5a", "pub": "#1f4a36", "kitap": "#2b3f6b",
                    "cicek": "#c9577a", "bisiklet": "#e08a3a"}
BLOK_CATI = ["#c0573a", "#b04a32", "#9a5a3f", "#6f6a66", "#5a6470"]
BLOK_DUKKAN_RENK = ["#d9473a", "#3f73e6", "#2f8f5a", "#e08a3a", "#8a5a9a", "#2b2d31", "#c9a03a", "#3aa0b8"]


def blok_renk(rnd, stil):
    r = ar(rnd, rnd.choice(BLOK_STIL_RENK[stil]), cati=rnd.choice(BLOK_CATI), ana=rnd.choice(BLOK_DUKKAN_RENK))
    if stil == "modern":
        r["suslu"] = M("Suslu", rnd.choice(["#3a3d42", "#5a6068", "#f6f3ec"]))
    elif stil in ("tugla", "kanal", "loft"):
        r["suslu"] = M("Suslu", rnd.choice(["#efe6d4", "#e6dccb"]))
    elif stil == "modern_tugla":
        r["suslu"] = M("Suslu", "#2e2b2a")
        r["cerceve"] = M("TCerceve", "#2b2d31")
    elif stil in ("galerili", "teras"):
        r["suslu"] = M("Suslu", rnd.choice(["#3a3d42", "#f6f3ec"]))
    if stil == "haussmann":
        r["cati"] = M("Cati", BLOK_CINKO)
        r["cati_koyu"] = M("CatiKoyu", koyu_ton(BLOK_CINKO))
        r["suslu"] = M("Suslu", "#f4ecdc")
    r["kemer"] = "bloklu" if stil in ("klasik", "cumbali") else "duz"
    r["avlu"] = M("AvluTas", "#d6cfc2")
    return r


def cikma_tabela(g, fc, u, z, r, mat, ikon=None):
    """Cepheden dik çıkan asma tabela (fırın simidi, eczane haçı, pub tabelası)."""
    cubuk(g, fc.P(u, 0.0, z + 0.25), fc.P(u, 1.0, z + 0.25), 0.04, r["demir"], kabuk=False)
    ckutu(g, fc, u - 0.035, u + 0.035, 0.35, 0.95, z - 0.45, z + 0.15, mat)
    if ikon == "ekmek":
        p = fc.P(u, 0.65, z - 0.6)
        top(g, p, 0.3, 0.3, 0.16, M("Ekmek", "#d9a24a"))
    elif ikon == "hac":
        ckutu(g, fc, u - 0.04, u + 0.04, 0.45, 0.85, z - 0.3, z + 0.0, mat)


def dukkan_tema(g, fc, L, kapi_u, z0, H0, r, rnd, tema):
    """Dükkânın türüne göre süs: fırın, eczane, pub, kitapçı, çiçekçi, bisikletçi (referanstaki mahalle dükkânları)."""
    if not DETAY or tema in ("genel", "kafe"):
        return
    ku = 0.35 if kapi_u > L / 2 else L - 0.35
    yazi_m = M("Yazi", {"eczane": "#4fe08a", "pub": "#e8c35a"}.get(tema, "#f6f3ec"))
    if tema == "firin":
        cikma_tabela(g, fc, ku, H0 - 0.2, r, r["ana"], "ekmek")
        for i in range(3):                                                     # vitrinin önünde ekmek sepetleri
            p = fc.P(1.0 + i * 0.6 if kapi_u > L / 2 else L - 1.0 - i * 0.6, 0.35, z0)
            silindir(g, p[0], p[1], z0, 0.35, 0.22, 8, r["ahsap"])
            top(g, (p[0], p[1], z0 + 0.42), 0.18, 0.18, 0.1, M("Ekmek", "#d9a24a"))
    elif tema == "eczane":
        # Yeşil haç (gece yanar)
        ckutu(g, fc, ku - 0.04, ku + 0.04, 0.3, 1.1, H0 - 0.52, H0 - 0.28, yazi_m)
        ckutu(g, fc, ku - 0.04, ku + 0.04, 0.58, 0.82, H0 - 0.8, H0 + 0.0, yazi_m)
        cubuk(g, fc.P(ku, 0.0, H0 - 0.4), fc.P(ku, 0.3, H0 - 0.4), 0.04, r["demir"], kabuk=False)
    elif tema == "pub":
        ckutu(g, fc, 0.3, L - 0.3, 0.16, 0.19, H0 - 0.65, H0 - 0.45, yazi_m)                  # altın yazı şeridi
        cikma_tabela(g, fc, ku, H0 - 0.1, r, r["ana"])
        for u in (0.6, L - 0.6):                                               # asma çiçek sepetleri
            cubuk(g, fc.P(u, 0.0, H0 - 0.15), fc.P(u, 0.5, H0 - 0.15), 0.03, r["demir"], kabuk=False)
            top(g, fc.P(u, 0.5, H0 - 0.55), 0.3, 0.3, 0.25, r["cali"])
            for k in range(3):
                top(g, fc.P(u + (k - 1) * 0.18, 0.62, H0 - 0.7), 0.09, 0.09, 0.09, rnd.choice(r["cicek"]))
        for u in [x for x in (1.2, L - 1.2) if abs(x - kapi_u) > 1.2]:          # fıçı masa
            p = fc.P(u, 0.6, 0.0)
            silindir(g, p[0], p[1], z0, 1.0, 0.3, 8, M("Fici", "#6b4a2f"))
    elif tema == "kitap":
        for u in [x for x in (1.3, L - 1.3) if abs(x - kapi_u) > 1.3]:           # kitap tezgâhı
            a, b = u - 0.7, u + 0.7
            ckutu(g, fc, a, b, 0.2, 0.8, z0, z0 + 0.75, r["ahsap"])
            for k in range(6):
                uu = a + 0.1 + k * 0.22
                ckutu(g, fc, uu, uu + 0.16, 0.3, 0.7, z0 + 0.75, z0 + 0.75 + rnd.uniform(0.08, 0.2), rnd.choice(r["urun"]))
    elif tema == "cicek":
        for u in [x for x in (1.2, L - 1.2) if abs(x - kapi_u) > 1.3]:           # basamaklı çiçek sehpası
            for k in range(3):
                ckutu(g, fc, u - 0.8, u + 0.8, 0.15 + k * 0.25, 0.4 + k * 0.25, z0, z0 + 0.8 - k * 0.25, r["ahsap"])
                for j in range(4):
                    p = fc.P(u - 0.6 + j * 0.4, 0.27 + k * 0.25, 0.0)
                    zz = z0 + 0.8 - k * 0.25
                    silindir(g, p[0], p[1], zz, 0.22, 0.1, 6, r["saksi"])
                    top(g, (p[0], p[1], zz + 0.32), 0.15, 0.15, 0.13, rnd.choice(r["cicek"] + [r["cali"]]))
    elif tema == "bisiklet":
        for i, u in enumerate([x for x in (0.9, 2.0, L - 2.0, L - 0.9) if abs(x - kapi_u) > 1.1][:3]):
            renk = M("Bisiklet", rnd.choice(["#d9473a", "#3f73e6", "#2f8f5a", "#f2c14a"]))
            for d in (-0.5, 0.5):                                              # tekerlekler (yan yatmış disk)
                c = fc.P(u, 0.7 + d, z0 + 0.36)
                top(g, c, 0.04 if abs(fc.out[0]) < 0.5 else 0.34, 0.34 if abs(fc.out[0]) < 0.5 else 0.04, 0.34, r["koyu"])
            cubuk(g, fc.P(u, 0.2, z0 + 0.36), fc.P(u, 0.7, z0 + 0.8), 0.035, renk, kabuk=False)
            cubuk(g, fc.P(u, 0.7, z0 + 0.8), fc.P(u, 1.2, z0 + 0.36), 0.035, renk, kabuk=False)
            cubuk(g, fc.P(u, 0.25, z0 + 0.95), fc.P(u, 0.25, z0 + 0.65), 0.03, r["koyu"], kabuk=False)


def korkuluk(g, fc, u0, u1, o, z, r, h=0.95, aralik=0.2, mat=None):
    """Ferforje korkuluk: üst ve alt lama, aralarda ince dikmeler (dolu siyah bant gibi görünmesin)."""
    mat = mat or r["demir"]
    if not DETAY:                                                              # hafif sürüm: sadece üst ve alt lama
        ckutu(g, fc, u0, u1, o - 0.05, o, z + h - 0.06, z + h, mat)
        ckutu(g, fc, u0, u1, o - 0.05, o, z + 0.08, z + 0.14, mat)
        return
    aralik = max(aralik, 0.28)
    ckutu(g, fc, u0, u1, o - 0.05, o, z + h - 0.06, z + h, mat)
    ckutu(g, fc, u0, u1, o - 0.05, o, z + 0.08, z + 0.12, mat)
    k = max(1, int((u1 - u0) / aralik))
    for i in range(k + 1):
        uu = u0 + (u1 - u0) * i / k
        ckutu(g, fc, uu - 0.015, uu + 0.015, o - 0.04, o - 0.01, z, z + h, mat)


def blok_balkon(g, fc, u, z, r, stil, gen=1.7):
    ckutu(g, fc, u - gen / 2, u + gen / 2, 0, 0.95, z - 0.15, z, r["beton"])
    if stil == "modern":
        cam = M("BalkonCam", "#bcd3df")
        ckutu(g, fc, u - gen / 2, u + gen / 2, 0.9, 0.95, z, z + 1.0, cam)
    else:
        ckutu(g, fc, u - gen / 2, u + gen / 2, 0.9, 0.95, z + 0.92, z + 1.0, r["demir"])
        k = int(gen / 0.18)
        for i in range(k + 1):
            uu = u - gen / 2 + gen * i / k
            ckutu(g, fc, uu - 0.015, uu + 0.015, 0.9, 0.93, z, z + 0.92, r["demir"])
        for su in (-1, 1):
            ckutu(g, fc, u + su * gen / 2 - 0.03, u + su * gen / 2 + 0.03, 0.05, 0.93, z + 0.92, z + 1.0, r["demir"])


def blok_cephe(g, fc, L, kat, r, rnd, dukkan, stil, z0):
    """Sokağa bakan cephe: zemin kat dükkân ya da giriş, üst katlarda pencere ve balkon, tepede korniş."""
    H0 = z0 + BLOK_ZEMIN
    if dukkan:
        kapi_u = L * rnd.choice([0.2, 0.5, 0.8])
        ckutu(g, fc, 0.1, L - 0.1, 0, 0.16, H0 - 0.85, H0 - 0.25, r["ana"])                  # tabela bandı
        ckutu(g, fc, 0.0, L, 0, 0.08, z0, z0 + 0.2, r["taban"])
        for a, b in ((0.45, kapi_u - 0.75), (kapi_u + 0.75, L - 0.45)):
            if b - a > 0.8:
                vitrin(g, fc, a, b, z0 + 0.3, H0 - 1.05, r, aralik=1.6, poster=0.3, rnd=rnd)
        # Dükkân kapısı: renkli kasalı camlı kapı (vitrinden ayrı seçilsin), önünde basamak
        cam_kapi(g, fc, kapi_u, z0, r, w=1.2, h=2.35, kasa=r["ana"], sacak=False)
        dukkan_tema(g, fc, L, kapi_u, z0, H0, r, rnd, getattr(g, "tema", "genel"))
        ckutu(g, fc, kapi_u - 0.75, kapi_u + 0.75, 0, 0.5, z0 + 2.55, z0 + 2.68, r["cerceve"])     # kapı saçağı
        if rnd.random() < 0.6:
            # Tente kapının iki yanında (kapı ve saçağı görünür kalır)
            renk2 = r["beyaz"] if rnd.random() < 0.5 else None
            for a, b in ((0.3, kapi_u - 0.85), (kapi_u + 0.85, L - 0.3)):
                if b - a > 1.0:
                    tente(g, fc, a, b, H0 - 1.0, 1.0, r, r["ana"], renk2)
    else:
        kapi_u = L * rnd.choice([0.25, 0.5, 0.75])
        ckutu(g, fc, 0.0, L, 0, 0.1, z0, z0 + 0.8, r["taban"])
        ckutu(g, fc, kapi_u - 0.65, kapi_u + 0.65, 0, 0.05, z0, z0 + 2.6, r["kapi"])
        cdortgen(g, fc, kapi_u - 0.45, kapi_u + 0.45, 0.06, z0 + 2.1, z0 + 2.5, r["cam"])
        ckutu(g, fc, kapi_u - 0.8, kapi_u + 0.8, 0, 0.12, z0 + 2.6, z0 + 2.8, r["suslu"])
        ckutu(g, fc, kapi_u - 0.9, kapi_u + 0.9, 0, 0.7, z0 + 2.85, z0 + 2.97, r["suslu"])       # giriş saçağı
        n0 = max(1, int(round(L / 2.4)))
        for i in range(n0):
            u = L * (i + 0.5) / n0
            if abs(u - kapi_u) < 1.2:
                continue
            pencere(g, fc, u, z0 + 1.1, 0.9, 1.5, r, sade=True)
    # Üst katlar
    n = max(2, int(round(L / 2.45)))
    balkon_sutun = set(i for i in range(n) if rnd.random() < (0.45 if stil == "siva" else 0.25))
    if stil in ("modern", "haussmann", "modern_tugla", "galerili", "loft"):
        balkon_sutun = set()
    # Cumbalı: ortada iki pencere genişliğinde çıkma (üst katlarda), üstü teras
    cumba = None
    if stil == "cumbali" and kat >= 3:
        cw = min(L * 0.42, 4.2)
        cumba = (L / 2 - cw / 2, L / 2 + cw / 2)
        ckutu(g, fc, cumba[0], cumba[1], 0, 0.85, H0 + 0.1, H0 + (kat - 1) * BLOK_KAT - 0.5, r["duvar"])
        ckutu(g, fc, cumba[0] - 0.1, cumba[1] + 0.1, 0, 0.95, H0 - 0.05, H0 + 0.12, r["suslu"])
        ckutu(g, fc, cumba[0] - 0.1, cumba[1] + 0.1, 0, 0.95, H0 + (kat - 1) * BLOK_KAT - 0.5, H0 + (kat - 1) * BLOK_KAT - 0.3, r["suslu"])
        cfc = Cephe(fc.P(cumba[0], 0.85, 0.0), fc.u, fc.out)
    for f in range(kat - 1):
        z = H0 + f * BLOK_KAT
        if stil in ("klasik", "siva") and f == 0:
            ckutu(g, fc, 0.0, L, 0, 0.14, z - 0.12, z + 0.05, r["suslu"])                   # kat silmesi
        if cumba is not None:
            for cu in (0.3, 0.7):
                pencere(g, cfc, (cumba[1] - cumba[0]) * cu, z + 0.8, 0.85, 1.5, r, sade=True)
        for i in range(n):
            u = L * (i + 0.5) / n
            if cumba is not None and cumba[0] - 0.3 < u < cumba[1] + 0.3:
                continue
            if stil == "haussmann":
                # Uzun Fransız pencereleri; 2. ve son katta boydan boya ferforje balkon, diğerlerinde pencere önü korkuluğu
                pencere(g, fc, u, z + 0.45, 1.0, 2.0, r)
                if not (f == 1 or f == kat - 2):
                    ckutu(g, fc, u - 0.6, u + 0.6, 0, 0.25, z + 0.38, z + 0.46, r["suslu"])
                    korkuluk(g, fc, u - 0.55, u + 0.55, 0.24, z + 0.46, r, h=0.6, aralik=0.18)
                continue
            if stil == "modern_tugla":
                w, h = L / n * 0.62, 1.9
                ckutu(g, fc, u - w / 2 - 0.12, u + w / 2 + 0.12, 0, 0.18, z + 0.45, z + 0.55, r["cerceve"])
                ckutu(g, fc, u - w / 2 - 0.12, u + w / 2 + 0.12, 0, 0.18, z + 0.55 + h, z + 0.65 + h, r["cerceve"])
                ckutu(g, fc, u - w / 2 - 0.12, u - w / 2, 0, 0.18, z + 0.55, z + 0.55 + h, r["cerceve"])
                ckutu(g, fc, u + w / 2, u + w / 2 + 0.12, 0, 0.18, z + 0.55, z + 0.55 + h, r["cerceve"])
                cdortgen(g, fc, u - w / 2, u + w / 2, 0.01, z + 0.55, z + 0.55 + h, cam_grubu(r["cam"], u, z))
                continue
            if stil == "galerili":
                # Dış galeri: her katta koridor balkonu, kapı + pencere sırası
                if i == 0:
                    ckutu(g, fc, 0.0, L, 0, 1.25, z - 0.05, z + 0.12, r["beton"])
                    if rnd.random() < 0.5:
                        korkuluk(g, fc, 0.0, L, 1.25, z + 0.12, r, h=1.0, aralik=0.25)
                    else:
                        ckutu(g, fc, 0.0, L, 1.2, 1.25, z + 0.12, z + 1.0, M("BalkonPanel", rnd.choice(["#e0825a", "#6fae6a", "#5f8fd0", "#f2c14a"])))
                if i % 2 == 0:
                    ckutu(g, fc, u - 0.45, u + 0.45, 0, 0.05, z + 0.12, z + 2.25, M("Kapi", rnd.choice(["#d9473a", "#3f73e6", "#2f8f5a", "#f2c14a", "#2b2d31"])))
                else:
                    pencere(g, fc, u, z + 0.9, 1.1, 1.3, r, sade=True)
                continue
            if stil == "loft":
                if abs(u - L / 2) < L / n * 0.5:
                    # Ortada yükleme kapıları (her katta), önünde küçük korkuluk
                    ckutu(g, fc, u - 0.65, u + 0.65, 0, 0.06, z + 0.2, z + 2.4, r["ahsap_koyu"])
                    ckutu(g, fc, u - 0.7, u + 0.7, 0, 0.5, z + 0.15, z + 0.25, r["beton"])
                    ckutu(g, fc, u - 0.7, u + 0.7, 0.45, 0.5, z + 0.25, z + 1.15, r["demir"])
                else:
                    pencere(g, fc, u, z + 0.5, 0.95, 1.9, r, sade=True)
                    for k, (a, b) in enumerate(((0.62, 0.62), (0.5, 0.5), (0.3, 0.3))):     # tuğla kemer
                        ckutu(g, fc, u - a, u + b, 0, 0.1, z + 2.45 + k * 0.12, z + 2.57 + k * 0.12, r["suslu"])
                continue
            if stil == "modern":
                w = L / n * 0.72
                cdortgen(g, fc, u - w / 2, u + w / 2, 0.012, z + 0.55, z + 2.45, cam_grubu(r["cam"], u, z))
                ckutu(g, fc, u - w / 2 - 0.06, u + w / 2 + 0.06, 0, 0.06, z + 0.5, z + 0.56, r["suslu"])
                ckutu(g, fc, u - w / 2 - 0.06, u + w / 2 + 0.06, 0, 0.06, z + 2.45, z + 2.5, r["suslu"])
                if i == 0:
                    ckutu(g, fc, 0.0, L, 0, 0.45, z + 0.32, z + 0.5, r["beton"])          # kat bandı (saçak)
            else:
                pencere(g, fc, u, z + 0.8, 0.95 if stil != "tugla" else 0.85, 1.55, r)
            if i in balkon_sutun and f >= 0 and (f % 2 == 0 or stil == "modern"):
                blok_balkon(g, fc, u, z + 0.62, r, stil, gen=min(1.8, L / n * 0.85))
        if DETAY and stil != "klasik" and rnd.random() < 0.25:
            klima_dis(g, fc, min(L - 0.5, L * (rnd.randrange(n) + 0.5) / n + 0.75), z + 0.4)
    Ht = H0 + (kat - 1) * BLOK_KAT
    if stil == "haussmann":
        for f in sorted(set((1, kat - 2))):
            if 0 <= f < kat - 1:
                z = H0 + f * BLOK_KAT + 0.45
                ckutu(g, fc, 0.0, L, 0, 0.8, z - 0.12, z + 0.02, r["beton"])                       # sürekli balkon
                korkuluk(g, fc, 0.0, L, 0.8, z + 0.02, r, h=1.0, aralik=0.22)
        for k in range(1, 6):                                                   # rustik taş zemin (yatay derzler)
            zz = z0 + k * 0.55
            if zz < H0 - 0.9:
                ckutu(g, fc, 0.0, 0.35, 0, 0.04, zz, zz + 0.05, r["suslu"])
                ckutu(g, fc, L - 0.35, L, 0, 0.04, zz, zz + 0.05, r["suslu"])
    if stil == "loft":
        cubuk(g, fc.P(L / 2, 0.0, Ht - 0.15), fc.P(L / 2, 1.0, Ht - 0.15), 0.12, r["ahsap_koyu"])  # vinç kirişi
        cubuk(g, fc.P(L / 2, 0.95, Ht - 0.2), fc.P(L / 2, 0.95, Ht - 1.3), 0.02, r["koyu"], kabuk=False)
    ckutu(g, fc, 0.0, L, 0, 0.32 if stil in ("klasik", "haussmann") else 0.2, Ht - 0.4, Ht, r["suslu"])        # korniş
    g.kapi_u = kapi_u
    return Ht


def blok_duz_cati(g, x0, x1, y0, y1, H, r, rnd, parapet):
    """Düz çatı: parapet, klima, bazen su deposu ve merdiven kulübesi (kare sınırını aşmaz)."""
    kutu(g, x0, x1, y0, y1, H, H + 0.5, parapet, atla=("zn",), yuzler={"zp": r["beton"]})
    for _ in range(rnd.randint(1, 2)):
        kx, ky = rnd.uniform(x0 + 1.0, x1 - 1.0), rnd.uniform(y0 + 1.2, y1 - 1.2)
        kutu(g, kx - 0.5, kx + 0.5, ky - 0.38, ky + 0.38, H + 0.5, H + 1.05, r["klima"])
    if rnd.random() < 0.4:
        kx, ky = rnd.uniform(x0 + 1.5, x1 - 1.5), rnd.uniform(y0 + 1.5, y1 - 1.5)
        kutu(g, kx - 0.9, kx + 0.9, ky - 0.9, ky + 0.9, H + 0.5, H + 2.7, r["duvar"])
        kutu(g, kx - 1.0, kx + 1.0, ky - 1.0, ky + 1.0, H + 2.7, H + 2.85, r["beton"])
    elif rnd.random() < 0.4:
        kx, ky = rnd.uniform(x0 + 1.5, x1 - 1.5), rnd.uniform(y0 + 1.5, y1 - 1.5)
        for dx, dy in ((-0.55, -0.55), (0.55, -0.55), (-0.55, 0.55), (0.55, 0.55)):
            cubuk(g, (kx + dx, ky + dy, H + 0.5), (kx + dx, ky + dy, H + 1.6), 0.08, r["demir"])
        silindir(g, kx, ky, H + 1.6, 1.3, 0.75, 8, M("Depo", "#8a6a4a"))
    if DETAY and rnd.random() < 0.5:
        cati_anteni(g, rnd.uniform(x0 + 0.8, x1 - 0.8), rnd.uniform(y0 + 0.8, y1 - 0.8), H + 0.5)


def blok_mansart(g, x0, x1, y0, y1, zt, r, rnd):
    """Mansart çatı (yanlarda dik: komşu binayla birleşir), önde çatı pencereleri."""
    kutu(g, x0, x1, y0 - 0.2, y1 + 0.2, zt - 0.22, zt, r["suslu"], atla=("zn",))
    h, ic = 2.3, 0.9
    pts = [(x0, y0 - 0.08, zt), (x1, y0 - 0.08, zt), (x1, y1 + 0.08, zt), (x0, y1 + 0.08, zt),
           (x0, y0 + ic, zt + h), (x1, y0 + ic, zt + h), (x1, y1 - ic, zt + h), (x0, y1 - ic, zt + h)]
    dis_bukey(g, pts, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], r["cati"])
    kutu(g, x0, x1, y0 + ic, y1 - ic, zt + h, zt + h + 0.1, r["cati_koyu"])
    n = 3
    W = x1 - x0
    for i in range(n):
        cx = x0 + W * (i + 0.5) / n
        yd = y0 + 0.1
        kutu(g, cx - 0.45, cx + 0.45, yd, yd + 1.1, zt + 0.15, zt + 1.5, r["suslu"], atla=("zn",))
        dfc = Cephe((cx - 0.45, yd, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        cdortgen(g, dfc, 0.15, 0.75, 0.01, zt + 0.35, zt + 1.3, cam_grubu(r["cam"], cx, zt))
        cprizma(g, dfc, [(-0.05, zt + 1.5), (0.95, zt + 1.5), (0.45, zt + 1.9)], 0.0, 1.1, r["cati"])
    ym = (y0 + y1) / 2
    bx = rnd.choice([x0 + 0.6, x1 - 0.6])
    kutu(g, bx - 0.35, bx + 0.35, ym - 0.6, ym + 0.6, zt, zt + h + 0.8, r["baca"])


def blok_arka(g, r, rnd, x0, x1, y0, y1):
    """Avluya bakan arka şerit: park etmiş araba, çöp kutuları, çalı, küçük ağaç, bisiklet."""
    z0 = EV_ZEMIN
    renkler = [M("Araba", c) for c in ARABA_RENK]
    x = x0 + 0.4
    while x < x1 - 0.6:
        k = rnd.random()
        kalan = x1 - x
        yc = (y0 + y1) / 2
        if k < 0.3 and kalan > 4.6 and y1 - y0 >= 1.75:
            park_araba(g, x + 2.2, yc, 0.0, rnd.choice(renkler), r, z0)
            x += 4.7
        elif k < 0.5:
            for i in range(rnd.randint(1, 3)):
                cop_kutusu(g, x + 0.3 + i * 0.6, y1 - 0.4, r, rnd.choice([r["koyu"], M("CopYesil", "#4f7a4a"), M("CopMavi", "#3f5f9a")]))
            x += 2.0
        elif k < 0.7 and kalan > 1.4:
            agac(g, x + 0.6, yc, r, rnd, boy=1.1, rr=0.62)
            x += 1.6
        elif k < 0.85:
            calilik(g, x, min(x1 - 0.2, x + 2.4), y1 - 0.45, r, rnd)
            x += 2.6
        else:
            for i in range(3):
                bx = x + 0.3 + i * 0.55
                cubuk(g, (bx, y0 + 0.4, z0), (bx, y0 + 0.4, z0 + 0.7), 0.04, r["metal"])
            x += 1.8


def blok_cati(g, x0, x1, y0, y1, Ht, r, rnd, stil):
    t = rnd.random()
    if stil == "haussmann":
        blok_mansart(g, x0, x1, y0, y1, Ht, r, rnd)
        for bx in (x0 + 0.45, x1 - 0.45):                                        # yan duvarlarda baca kümeleri
            kutu(g, bx - 0.3, bx + 0.3, y0 + 1.2, y1 - 1.2, Ht, Ht + 3.2, r["duvar"])
            for k in range(4):
                silindir(g, bx, y0 + 1.6 + k * ((y1 - y0 - 3.2) / 3), Ht + 3.2, 0.35, 0.09, 6, r["saksi"])
        return
    if stil == "modern_tugla":
        kutu(g, x0, x1, y0, y1, Ht, Ht + 0.45, r["suslu"], atla=("zn",), yuzler={"zp": r["cim"]})
        for k in range(5):
            top(g, (rnd.uniform(x0 + 1, x1 - 1), rnd.uniform(y0 + 1, y1 - 1), Ht + 0.6), 0.45, 0.45, 0.3, r["cali"])
        kutu(g, x1 - 2.4, x1 - 0.6, y1 - 2.6, y1 - 0.8, Ht + 0.45, Ht + 2.6, r["cerceve"])
        return
    if stil == "loft":
        zr = besik_cati(g, "y", y0, y1, x0, x1, Ht, r, sacak=0.0, yan_sacak=0.2)
        return
    if stil in ("galerili", "teras"):
        blok_duz_cati(g, x0, x1, y0, y1, Ht, r, rnd, r["suslu"] if stil == "galerili" else r["duvar"])
        if stil == "teras":
            for k in range(4):
                top(g, (x0 + 1.2 + k * 2.4, y0 + 0.9, Ht + 0.75), 0.42, 0.32, 0.32, r["cali"])
        return
    if stil == "modern" or (stil == "tugla" and t < 0.6):
        blok_duz_cati(g, x0, x1, y0, y1, Ht, r, rnd, r["suslu"] if stil == "tugla" else r["duvar"])
    elif stil in ("klasik", "cumbali") and t < (0.7 if stil == "klasik" else 0.45):
        blok_mansart(g, x0, x1, y0, y1, Ht, r, rnd)
    else:
        zr = besik_cati(g, "x", x0, x1, y0, y1, Ht, r, sacak=0.32, yan_sacak=0.0)
        ev_baca(g, rnd.uniform(x0 + 1.5, x1 - 1.5), (y0 + y1) / 2 + 0.9, Ht, zr + 0.3, r)


def cephe_poligon(g, fc, pts_uz, o0, o1, mat):
    """Cephede dışbükey çokgen kabartma (barok alınlık)."""
    n = len(pts_uz)
    pts = [fc.P(u, o0, z) for u, z in pts_uz] + [fc.P(u, o1, z) for u, z in pts_uz]
    dis_bukey(g, pts, [list(range(n)), list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)], mat)


def blok_dar_evler(rnd, stil, kat):
    """İki dar ev (5 m): kanal evleri (basamaklı tuğla alınlık) ya da barok evler (kavisli pastel alınlık).
    Sokağa bakan alınlıklar çatının önünü kapatır, her evin rengi farklı (referanstaki Amsterdam / Brüksel sokakları)."""
    g = Govde()
    z0, FY, BY = EV_ZEMIN, BLOK_FY, BLOK_BY
    r0 = blok_renk(rnd, stil)
    levha(g, 0.0, 10.0, 0.0, FY, 0.0, z0, r0["kaldirim"])
    levha(g, 0.0, 10.0, FY, 10.0, 0.0, z0, r0["avlu"])
    H0 = z0 + BLOK_ZEMIN
    Ht = H0 + (kat - 1) * BLOK_KAT
    renkler = list(BLOK_STIL_RENK[stil])
    rnd.shuffle(renkler)
    for i, (a, b) in enumerate(((0.0, 5.0), (5.0, 10.0))):
        ri = dict(r0)
        ri["duvar"] = M("Duvar", renkler[i % len(renkler)])
        ri["ana"] = M("Marka", rnd.choice(BLOK_DUKKAN_RENK))
        h_ek = rnd.choice([0.0, 0.0, BLOK_KAT * 0.5]) if kat >= 3 else 0.0
        Hi = Ht + h_ek
        kutu(g, a, b, FY, BY, z0, Hi, ri["duvar"], atla=("zn",))
        fc = Cephe((a, FY, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        g.tema = rnd.choice(["genel", "firin", "kitap", "cicek", "pub", "kafe"])
        blok_cephe(g, fc, 5.0, kat, ri, rnd, rnd.random() < 0.55, "tugla" if stil == "kanal" else "siva", z0)
        if h_ek:
            pencere(g, fc, 2.5, Ht + 0.2, 0.8, 1.0, ri, sade=True)
        arka = Cephe((b, BY, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
        pencereler(g, arka, 5.0, H0, kat - 1, BLOK_KAT, ri, w=0.8, h=1.3, aralik=2.4, sade=True)
        besik_cati(g, "y", FY + 0.3, BY, a, b, Hi, ri, sacak=0.0, yan_sacak=0.15)
        if stil == "kanal":
            # Basamaklı alınlık: üst üste daralan tuğla basamaklar, beyaz kapak taşları, tepede vinç kirişi
            for k in range(4):
                u0, u1 = 0.25 + k * 0.55, 4.75 - k * 0.55
                ckutu(g, fc, u0, u1, 0, 0.3, Hi + k * 0.75, Hi + (k + 1) * 0.75, ri["duvar"])
                ckutu(g, fc, u0 - 0.05, u0 + 0.5, 0, 0.36, Hi + (k + 1) * 0.75, Hi + (k + 1) * 0.75 + 0.08, ri["suslu"])
                ckutu(g, fc, u1 - 0.5, u1 + 0.05, 0, 0.36, Hi + (k + 1) * 0.75, Hi + (k + 1) * 0.75 + 0.08, ri["suslu"])
            pencere(g, fc, 2.5, Hi + 0.6, 0.8, 1.1, ri, sade=True)
            cubuk(g, fc.P(2.5, 0.3, Hi + 2.7), fc.P(2.5, 1.0, Hi + 2.7), 0.1, ri["ahsap_koyu"])
        else:
            # Kavisli (çan biçimli) alınlık, omuzlarda süs topları, tepede küçük alınlık
            pts = [(0.3, Hi), (4.7, Hi), (4.7, Hi + 0.9), (4.3, Hi + 1.9), (3.5, Hi + 2.6), (2.5, Hi + 2.9),
                   (1.5, Hi + 2.6), (0.7, Hi + 1.9), (0.3, Hi + 0.9)]
            cephe_poligon(g, fc, pts, 0.0, 0.3, ri["duvar"])
            for u in (0.4, 4.6):
                top(g, fc.P(u, 0.2, Hi + 1.15), 0.2, 0.2, 0.2, ri["suslu"])
            cephe_poligon(g, fc, [(1.9, Hi + 2.8), (3.1, Hi + 2.8), (2.5, Hi + 3.3)], 0.0, 0.36, ri["suslu"])
            ckutu(g, fc, 0.25, 4.75, 0, 0.36, Hi - 0.1, Hi + 0.05, ri["suslu"])
            pencere(g, fc, 2.5, Hi + 0.7, 0.9, 1.2, ri, sade=True)
    blok_arka(g, r0, rnd, 0.0, 10.0, BY, 10.0)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK)
    return g, ortala(g)


def blok_duz(rnd, stil, kat):
    if stil in ("kanal", "barok"):
        return blok_dar_evler(rnd, stil, kat)
    g = Govde()
    r = blok_renk(rnd, stil)
    z0, FY, BY = EV_ZEMIN, BLOK_FY, BLOK_BY
    levha(g, 0.0, 10.0, 0.0, FY, 0.0, z0, r["kaldirim"])
    levha(g, 0.0, 10.0, FY, 10.0, 0.0, z0, r["avlu"] if rnd.random() < 0.6 else r["cim"])
    H0 = z0 + BLOK_ZEMIN
    Ht = H0 + (kat - 1) * BLOK_KAT
    teras = stil == "teras" and kat >= 3
    kutu(g, 0.0, 10.0, FY, BY, z0, Ht - (BLOK_KAT if teras else 0.0), r["duvar"], atla=("zn",))
    on = Cephe((0.0, FY, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    arka = Cephe((10.0, BY, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
    dukkan = rnd.random() < (0.6 if kat >= 3 else 0.35)
    g.tema = rnd.choice(["genel", "firin", "eczane", "pub", "kitap", "cicek", "bisiklet", "kafe", "kafe"])
    if g.tema in DUKKAN_TEMA_RENK:
        r["ana"] = M("Marka", DUKKAN_TEMA_RENK[g.tema])
    if teras:
        # Teraslı: en üst kat 1,8 m geride, önünde cam korkuluklu teras ve saksılar
        Hm = Ht - BLOK_KAT
        blok_cephe(g, on, 10.0, kat - 1, r, rnd, dukkan, "modern", z0)
        kutu(g, 0.0, 10.0, FY, FY + 1.8, Hm - 0.4, Hm, r["beton"], atla=("zn",))
        ust = Cephe((0.0, FY + 1.8, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        kutu(g, 0.0, 10.0, FY + 1.8, BY, Hm, Ht, r["duvar"], atla=("zn",))
        for k in range(3):
            u = 10.0 * (k + 0.5) / 3
            cdortgen(g, ust, u - 1.3, u + 1.3, 0.01, Hm + 0.1, Hm + 2.6, cam_grubu(r["cam"], u, Hm))
        ckutu(g, on, 0.0, 10.0, 0, 0.06, Hm, Hm + 1.0, M("BalkonCam", "#bcd3df"))
        for k in range(4):
            p = on.P(1.2 + k * 2.5, -0.5, Hm)
            silindir(g, p[0], p[1], Hm, 0.4, 0.3, 8, r["saksi"])
            top(g, (p[0], p[1], Hm + 0.8), 0.4, 0.4, 0.45, r["cali"])
    else:
        blok_cephe(g, on, 10.0, kat, r, rnd, dukkan, stil, z0)
    pencereler(g, arka, 10.0, H0, kat - 1, BLOK_KAT, r, w=0.8, h=1.3, aralik=2.6, sade=True)
    ckutu(g, arka, 4.4, 5.6, 0, 0.05, z0, z0 + 2.3, r["kapi"])
    blok_cati(g, 0.0, 10.0, FY + (1.8 if teras else 0.0), BY, Ht, r, rnd, stil)
    blok_arka(g, r, rnd, 0.0, 10.0, BY, 10.0)
    if DETAY and dukkan and getattr(g, "tema", "") == "kafe":
        sm = M("Semsiye", rnd.choice(["#f6f3ec", "#d9473a", "#2f8f5a"]))
        ku = getattr(g, "kapi_u", 5.0)
        for x in [x for x in (1.6, 3.6, 6.4, 8.4) if abs(x - ku) > 1.6][:2]:   # kaldırımda kafe masası (kapının önü boş)
            silindir(g, x, 0.75, z0, 0.75, 0.3, 8, r["beyaz"])
            silindir(g, x, 0.75, z0, 1.9, 0.03, 6, r["metal"], kabuk=False)
            koni(g, x, 0.75, z0 + 1.85, 0.45, 0.65, 8, sm)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK)
    return g, ortala(g)


def blok_kose(rnd, stil, kat):
    if stil in ("kanal", "barok", "teras"):
        stil = {"kanal": "tugla", "barok": "cumbali", "teras": "modern"}[stil]
    g = Govde()
    r = blok_renk(rnd, stil)
    z0, FY, BY = EV_ZEMIN, BLOK_FY, BLOK_BY
    levha(g, 0.0, 10.0, 0.0, FY, 0.0, z0, r["kaldirim"])
    levha(g, 0.0, FY, FY, 10.0, 0.0, z0, r["kaldirim"])
    levha(g, FY, 10.0, FY, 10.0, 0.0, z0, r["avlu"])
    H0 = z0 + BLOK_ZEMIN
    Ht = H0 + (kat - 1) * BLOK_KAT
    kutu(g, FY, 10.0, FY, BY, z0, Ht, r["duvar"], atla=("zn",))
    kutu(g, FY, BY, BY, 10.0, z0, Ht, r["duvar"], atla=("zn", "yn"))
    L = 10.0 - FY
    on = Cephe((FY, FY, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    yan = Cephe((FY, 10.0, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    blok_cephe(g, on, L, kat, r, rnd, rnd.random() < 0.75, stil, z0)
    blok_cephe(g, yan, L, kat, r, rnd, rnd.random() < 0.4, stil, z0)
    for fc in (Cephe((10.0, BY, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0)), Cephe((BY, BY, 0.0), (0.0, 1.0, 0.0), (1.0, 0.0, 0.0))):
        pencereler(g, fc, 10.0 - BY, H0, kat - 1, BLOK_KAT, r, w=0.7, h=1.2, aralik=1.6, sade=True)
    parapet = r["suslu"] if stil == "tugla" else r["duvar"]
    if stil == "haussmann" or (stil == "klasik" and rnd.random() < 0.6):
        blok_mansart(g, FY, 10.0, FY, BY, Ht, r, rnd)
        kutu(g, FY, BY, BY, 10.0, Ht, Ht + 0.5, parapet, atla=("zn",), yuzler={"zp": r["beton"]})
    else:
        blok_duz_cati(g, FY, 10.0, FY, BY, Ht, r, rnd, parapet)
        kutu(g, FY, BY, BY, 10.0, Ht, Ht + 0.5, parapet, atla=("zn",), yuzler={"zp": r["beton"]})
    # Köşe kulesi: köşede yuvarlak cumba, sivri külah (eski şehir binalarında)
    if stil in ("siva", "klasik", "cumbali", "haussmann") and rnd.random() < (0.85 if stil == "haussmann" else 0.6) and kat >= 3:
        cx = cy = FY + 0.55
        R = 1.25
        silindir(g, cx, cy, H0, Ht - H0 + 1.0, R, 10, r["duvar"])
        for f in range(kat - 1):
            z = H0 + f * BLOK_KAT
            for a in (math.pi * 1.0, math.pi * 1.25, math.pi * 1.5):
                d = (math.cos(a), math.sin(a), 0.0)
                t = (-d[1], d[0], 0.0)
                c = (cx + d[0] * (R + 0.01), cy + d[1] * (R + 0.01), z + 1.55)
                yonlu_kutu(g, c, mul(t, 0.32), mul(d, 0.02), (0, 0, 0.7), cam_grubu(r["cam"], a, z), kabuk=False)
            silindir(g, cx, cy, z - 0.1, 0.16, R + 0.08, 10, r["suslu"])
        if stil in ("haussmann", "klasik") and rnd.random() < 0.7:
            # Kubbeli köşe (Belle Époque): çinko kubbe ve fener
            top(g, (cx, cy, Ht + 1.0), R + 0.12, R + 0.12, 1.7, M("Kubbe", BLOK_CINKO))
            silindir(g, cx, cy, Ht + 2.55, 0.6, 0.28, 8, r["suslu"])
            koni(g, cx, cy, Ht + 3.15, 0.6, 0.34, 8, M("Kubbe", BLOK_CINKO))
        else:
            koni(g, cx, cy, Ht + 1.0, 2.4, R + 0.15, 10, r["cati"])
    blok_arka(g, r, rnd, BY, 10.0, BY, 10.0)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK)
    return g, ortala(g)


BLOK_STILLER = {
    2: ["siva", "tugla", "klasik", "cumbali", "kanal", "barok"],
    3: ["siva", "tugla", "klasik", "cumbali", "kanal", "barok", "loft"],
    4: ["haussmann", "siva", "kanal", "barok", "loft", "modern_tugla", "galerili", "cumbali"],
    5: ["haussmann", "siva", "modern", "cumbali", "loft", "modern_tugla", "galerili", "teras"],
    6: ["haussmann", "modern", "tugla", "cumbali", "modern_tugla", "galerili", "teras"],
    7: ["haussmann", "modern", "siva", "modern_tugla", "teras", "galerili"],
    9: ["modern", "tugla", "modern_tugla", "teras", "galerili", "haussmann"],
}
BLOK_KOSE_STILLER = {
    2: ["siva", "tugla", "klasik"], 3: ["siva", "tugla", "cumbali", "klasik"], 4: ["haussmann", "siva", "modern_tugla", "cumbali"],
    5: ["haussmann", "modern", "cumbali", "modern_tugla"], 6: ["haussmann", "modern", "tugla", "galerili"],
    7: ["haussmann", "modern", "modern_tugla"], 9: ["modern", "tugla", "modern_tugla"],
}


def bloklar():
    sonuc = []
    for ki, kat in enumerate(BLOK_KATLAR):
        for v, stil in enumerate(BLOK_STILLER[kat]):
            rnd = random.Random(TOHUM * 9001 + ki * 131 + v * 17)
            g, gen = blok_duz(rnd, stil, kat)
            sonuc.append((f"Blok_duz_{kat}k_{v + 1}", g, gen))
        for v, stil in enumerate(BLOK_KOSE_STILLER[kat]):
            rnd = random.Random(TOHUM * 7001 + ki * 113 + v * 29)
            g, gen = blok_kose(rnd, stil, kat)
            sonuc.append((f"Blok_kose_{kat}k_{v + 1}", g, gen))
    return sonuc


# ------------------------------------------------------------------ REFERANS (CityRT Avrupa kataloğu) — tek tek binalar
# Konut_ref_*: şehir binaları · Konut_villa_* / Konut_konak_*: bahçeli evler · Dukkan_cadde_*: cadde dükkânları
# Buro_klinker: büro · Sanayi_*: atölye ve fabrikalar · Simge_*: bir bloğu kaplayan simge yapılar (opera, gar, pazar …)
# Park_cim_* / Avlu_*: park ve avlu kareleri (10 m)

def araba_renk(rnd):
    return M("Araba", rnd.choice(ARABA_RENK))


def tonoz_y(g, x0, x1, y0, y1, z, h, mat, n=10, kabuk=True):
    """Y boyunca beşik tonoz (yarım silindir): x0..x1 açıklık, z üzengi, h yükseklik."""
    xc, R = (x0 + x1) / 2, (x1 - x0) / 2
    arc = [(xc + R * math.cos(math.pi * i / n), z + h * math.sin(math.pi * i / n)) for i in range(n + 1)]
    m = n + 1
    pts = [(x, y0, zz) for x, zz in arc] + [(x, y1, zz) for x, zz in arc]
    yz = [list(range(m)), list(range(m, 2 * m))] + [[i, i + 1, m + i + 1, m + i] for i in range(n)] + [[0, n, m + n, m]]
    dis_bukey(g, pts, yz, mat, kabuk)


def tonoz_kaburga(g, x0, x1, y, z, h, mat, n=10, t=0.12):
    xc, R = (x0 + x1) / 2, (x1 - x0) / 2 + 0.06
    arc = [(xc + R * math.cos(math.pi * i / n), z + (h + 0.06) * math.sin(math.pi * i / n)) for i in range(n + 1)]
    for (a, b), (c, d) in zip(arc, arc[1:]):
        cubuk(g, (a, y, b), (c, y, d), t, mat, kabuk=False)


def kemer(g, fc, u, z0, w, h, o0, o1, mat, n=8):
    """Kemerli açıklık (kapı, pencere): dikdörtgen + yarım daire tepe."""
    R = w / 2
    zc = z0 + h - R
    pts = [(u - R, z0), (u + R, z0)] + [(u + R * math.cos(math.pi * i / n), zc + R * math.sin(math.pi * i / n)) for i in range(n + 1)]
    cephe_poligon(g, fc, pts, o0, o1, mat)


def yuvarlak_pencere(g, fc, u, zc, R, o, mat, n=12):
    cephe_poligon(g, fc, [(u + R * math.cos(2 * math.pi * i / n), zc + R * math.sin(2 * math.pi * i / n)) for i in range(n)], o, o + 0.04, mat)


def mansart(g, x0, x1, y0, y1, zt, r, rnd, n=None, oval=False, arka=True, h=2.6, ic=0.9, yan_egik=True, korn=True):
    """Bağımsız bina için çinko / kiremit mansart: dört yan eğik, önde (ve arkada) çatı pencereleri. Dönüş: üst kotu."""
    if korn:
        kutu(g, x0 - 0.2, x1 + 0.2, y0 - 0.2, y1 + 0.2, zt - 0.25, zt, r["suslu"], atla=("zn",))
    xi0, xi1 = (x0 + ic, x1 - ic) if yan_egik else (x0, x1)
    pts = [(x0, y0, zt), (x1, y0, zt), (x1, y1, zt), (x0, y1, zt),
           (xi0, y0 + ic, zt + h), (xi1, y0 + ic, zt + h), (xi1, y1 - ic, zt + h), (xi0, y1 - ic, zt + h)]
    dis_bukey(g, pts, [[0, 1, 2, 3], [4, 5, 6, 7], [0, 1, 5, 4], [1, 2, 6, 5], [2, 3, 7, 6], [3, 0, 4, 7]], r["cati"])
    kutu(g, xi0, xi1, y0 + ic, y1 - ic, zt + h, zt + h + 0.1, r["cati_koyu"])
    W = x1 - x0
    n = n or max(2, int((W - 1.0) / 2.5))
    for yuz in ((0, 1) if arka else (0,)):
        for i in range(n):
            cx = x0 + 0.5 + (W - 1.0) * (i + 0.5) / n
            if yuz == 0:
                yd = y0 + 0.1
                kutu(g, cx - 0.45, cx + 0.45, yd, yd + 1.1, zt + 0.1, zt + 1.5, r["suslu"], atla=("zn",))
                fc = Cephe((cx - 0.45, yd, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
            else:
                yd = y1 - 0.1
                kutu(g, cx - 0.45, cx + 0.45, yd - 1.1, yd, zt + 0.1, zt + 1.5, r["suslu"], atla=("zn",))
                fc = Cephe((cx + 0.45, yd, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0))
            if oval:
                yuvarlak_pencere(g, fc, 0.45, zt + 0.85, 0.3, 0.005, cam_grubu(r["cam"], cx, zt + yuz))
                cephe_poligon(g, fc, [(0.45 + 0.52 * math.cos(math.pi * k / 8), zt + 1.45 + 0.4 * math.sin(math.pi * k / 8)) for k in range(9)],
                              -1.1, 0.04, r["cati"])
            else:
                cdortgen(g, fc, 0.15, 0.75, 0.01, zt + 0.35, zt + 1.3, cam_grubu(r["cam"], cx, zt + yuz))
                cprizma(g, fc, [(-0.05, zt + 1.5), (0.95, zt + 1.5), (0.45, zt + 1.95)], -1.1, 0.04, r["cati"])
    return zt + h


def baca_sira(g, x, y, z, r, n=4, h=1.5, yon="y"):
    """Paris bacası: sıva kütle, üstünde sıra sıra kiremit baca kapakları."""
    L = 0.45 * n
    if yon == "y":
        kutu(g, x - 0.35, x + 0.35, y - L / 2, y + L / 2, z, z + h, r["duvar"])
        kutu(g, x - 0.42, x + 0.42, y - L / 2 - 0.07, y + L / 2 + 0.07, z + h, z + h + 0.1, r["suslu"])
        pts = [(x, y - L / 2 + 0.225 + i * 0.45) for i in range(n)]
    else:
        kutu(g, x - L / 2, x + L / 2, y - 0.35, y + 0.35, z, z + h, r["duvar"])
        kutu(g, x - L / 2 - 0.07, x + L / 2 + 0.07, y - 0.42, y + 0.42, z + h, z + h + 0.1, r["suslu"])
        pts = [(x - L / 2 + 0.225 + i * 0.45, y) for i in range(n)]
    for px, py in pts:
        silindir(g, px, py, z + h + 0.1, 0.38, 0.11, 6, r["saksi"])


def paris(g, rnd, r, x0, x1, y0, y1, kat, stil="haussmann", yuzler=("on",), dukkan=True, tema="genel", sagir=()):
    """Bağımsız Avrupa binası gövdesi: seçilen cephelerde blok cephesi (dükkân, pencere, balkon, korniş), diğerlerinde pencere."""
    z0 = EV_ZEMIN
    H0 = z0 + BLOK_ZEMIN
    Ht = H0 + (kat - 1) * BLOK_KAT
    kutu(g, x0, x1, y0, y1, z0, Ht, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    g.tema = tema
    for ad, fc, L in (("on", on, x1 - x0), ("sag", sag, y1 - y0), ("sol", sol, y1 - y0), ("arka", arka, x1 - x0)):
        if ad in yuzler:
            blok_cephe(g, fc, L, kat, r, rnd, dukkan if ad == "on" else (dukkan and rnd.random() < 0.5), stil, z0)
        elif ad not in sagir:
            pencereler(g, fc, L, H0, kat - 1, BLOK_KAT, r, w=0.8, h=1.4, aralik=2.4, sade=True)
    return Ht, on, sag, sol, arka


def sagir_duvar(g, x, y0, y1, z1, r, t=0.3):
    """Komşu binaya yaslanan yüksek sağır yan duvar (çatının üstüne taşan)."""
    kutu(g, x - t / 2, x + t / 2, y0, y1, EV_ZEMIN, z1, r["duvar"])
    kutu(g, x - t / 2 - 0.05, x + t / 2 + 0.05, y0, y1, z1, z1 + 0.12, r["suslu"])


def ferforje_cit(g, x0, x1, y, r, z0=None, kapi=None, aralik=2.4):
    """Taş dikmeli ferforje bahçe çiti; kapi = (a, b) aralığı boş kalır (ya da kapı kanadı)."""
    z0 = EV_ZEMIN if z0 is None else z0
    n = max(1, int((x1 - x0) / aralik))
    xs = [x0 + (x1 - x0) * i / n for i in range(n + 1)]
    for x in xs:
        kutu(g, x - 0.2, x + 0.2, y - 0.2, y + 0.2, z0, z0 + 1.35, r["suslu"])
        kutu(g, x - 0.25, x + 0.25, y - 0.25, y + 0.25, z0 + 1.35, z0 + 1.45, r["suslu"])
    for a, b in zip(xs, xs[1:]):
        if kapi and a >= kapi[0] - 0.01 and b <= kapi[1] + 0.01:
            continue
        kutu(g, a + 0.2, b - 0.2, y - 0.12, y + 0.12, z0, z0 + 0.35, r["suslu"])
        cubuk(g, (a + 0.2, y, z0 + 1.05), (b - 0.2, y, z0 + 1.05), 0.05, r["demir"], kabuk=False)
        k = max(1, int((b - a) / 0.22)) if DETAY else 2
        for i in range(1, k):
            xx = a + 0.2 + (b - a - 0.4) * i / k
            cubuk(g, (xx, y, z0 + 0.35), (xx, y, z0 + 1.15), 0.03, r["demir"], kabuk=False)


def cicek_kutusu(g, fc, u, z, r, rnd, w=0.9):
    ckutu(g, fc, u - w / 2, u + w / 2, 0, 0.25, z - 0.22, z, r["saksi"] if rnd.random() < 0.5 else r["ahsap"])
    if DETAY:
        for k in range(3):
            p = fc.P(u - w / 2 + 0.15 + k * (w - 0.3) / 2, 0.15, z + 0.08)
            top(g, p, 0.13, 0.13, 0.11, rnd.choice(r["cicek"] + [r["cali"]]))


def lamba_klasik(g, x, y, r, h=3.6, z0=None):
    """Dökme demir klasik sokak feneri (koyu yeşil)."""
    z0 = EV_ZEMIN if z0 is None else z0
    yesil = M("Fener", "#2f4a3a")
    silindir(g, x, y, z0, 0.35, 0.12, 6, yesil)
    silindir(g, x, y, z0 + 0.35, h - 0.35, 0.05, 6, yesil, kabuk=False)
    kutu(g, x - 0.18, x + 0.18, y - 0.18, y + 0.18, z0 + h, z0 + h + 0.45, r["isik"])
    koni(g, x, y, z0 + h + 0.45, 0.3, 0.26, 4, yesil)


def masa_semsiye(g, x, y, r, renk, z0=None):
    z0 = EV_ZEMIN if z0 is None else z0
    silindir(g, x, y, z0, 0.75, 0.32, 8, r["beyaz"])
    silindir(g, x, y, z0, 2.0, 0.03, 6, r["metal"], kabuk=False)
    koni(g, x, y, z0 + 1.95, 0.45, 0.95, 8, renk)
    for a in (0.0, math.pi):
        silindir(g, x + 0.6 * math.cos(a), y + 0.6 * math.sin(a), z0, 0.45, 0.17, 6, r["koyu"])


def fici(g, x, y, r, z0=None):
    z0 = EV_ZEMIN if z0 is None else z0
    silindir(g, x, y, z0, 0.9, 0.32, 8, M("Fici", "#6b4a2f"))
    silindir(g, x, y, z0 + 0.42, 0.06, 0.34, 8, r["koyu"], kabuk=False)


def bisiklet(g, x, y, aci, r, rnd, z0=None):
    z0 = EV_ZEMIN if z0 is None else z0
    d = (math.cos(aci), math.sin(aci), 0.0)
    renk = M("Bisiklet", rnd.choice(["#d9473a", "#3f73e6", "#2f8f5a", "#f2c14a", "#2b2d31"]))
    for s in (-0.5, 0.5):
        c = (x + d[0] * s, y + d[1] * s, z0 + 0.34)
        top(g, c, 0.34 if abs(d[0]) > 0.5 else 0.04, 0.34 if abs(d[1]) > 0.5 else 0.04, 0.34, r["koyu"])
    cubuk(g, (x - d[0] * 0.5, y - d[1] * 0.5, z0 + 0.34), (x, y, z0 + 0.8), 0.04, renk, kabuk=False)
    cubuk(g, (x, y, z0 + 0.8), (x + d[0] * 0.5, y + d[1] * 0.5, z0 + 0.34), 0.04, renk, kabuk=False)


def kamyon(g, x, y, aci, r, renk, z0=None):
    z0 = EV_ZEMIN if z0 is None else z0
    u = (math.cos(aci), math.sin(aci), 0.0)
    v = (-u[1], u[0], 0.0)
    yonlu_kutu(g, add((x, y, z0 + 1.6), mul(u, -0.9)), mul(u, 2.4), mul(v, 1.1), (0, 0, 1.25), renk)
    yonlu_kutu(g, add((x, y, z0 + 1.2), mul(u, 2.3)), mul(u, 0.8), mul(v, 1.05), (0, 0, 0.85), r["beyaz"])
    cdortgen(g, Cephe(add((x, y, 0.0), add(mul(u, 3.12), mul(v, 0.9))), mul(v, -1.0), u), 0.2, 1.6, 0.0, z0 + 1.35, z0 + 1.9, r["cam"])
    for su in (-2.2, 0.4, 2.3):
        for sv in (-1.12, 1.12):
            c = add(add((x, y, z0 + 0.4), mul(u, su)), mul(v, sv))
            yonlu_kutu(g, c, mul(u, 0.4), mul(v, 0.1), (0, 0, 0.4), r["koyu"])


def serit_tente_tezgah(g, x, y, w, d, r, renk, z0=None, aci=0.0):
    """Pazar tezgâhı: ahşap tezgâh, dört direk, çizgili tente, üstünde ürünler."""
    z0 = EV_ZEMIN if z0 is None else z0
    kutu(g, x - w / 2, x + w / 2, y - d / 2, y + d / 2, z0, z0 + 0.85, r["ahsap"])
    for k in range(int(w / 0.35)):
        top(g, (x - w / 2 + 0.2 + k * 0.35, y, z0 + 0.95), 0.14, 0.14, 0.12, r["urun"][k % len(r["urun"])])
    for sx in (-1, 1):
        for sy in (-1, 1):
            cubuk(g, (x + sx * w / 2, y + sy * d / 2, z0), (x + sx * w / 2, y + sy * d / 2, z0 + 2.2), 0.06, r["koyu"], kabuk=False)
    n = 6
    for i in range(n):
        a, b = x - w / 2 - 0.1 + (w + 0.2) * i / n, x - w / 2 - 0.1 + (w + 0.2) * (i + 1) / n
        yonlu_kutu(g, ((a + b) / 2, y, z0 + 2.3), ((b - a) / 2, 0, 0), (0, d / 2 + 0.25, -0.18), (0, 0, 0.03),
                   renk if i % 2 == 0 else r["beyaz"])


# ---------------- KONUT

def ref_kira(rnd):
    """Kira apartmanı: bosajlı taş zemin, 2. ve 5. katta boydan boya balkonlar, çinko mansart, köşede rotunda."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 18.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    x0, x1, y0, y1 = 1.4, 17.0, 1.4, 12.8
    Ht, on, sag, sol, arka = paris(g, rnd, r, x0, x1, y0, y1, 6, "haussmann", ("on", "sol", "sag"), dukkan=False)
    # Köşe rotundası (sol ön köşe): yuvarlak cumba, kubbe ve fener
    cx, cy, R = x0 + 0.4, y0 + 0.4, 1.5
    silindir(g, cx, cy, EV_ZEMIN, Ht + 0.6, R, 12, r["duvar"])
    for f in range(5):
        z = EV_ZEMIN + BLOK_ZEMIN + f * BLOK_KAT
        silindir(g, cx, cy, z - 0.12, 0.16, R + 0.08, 12, r["suslu"])
        for a in (math.pi * 1.05, math.pi * 1.25, math.pi * 1.45):
            d = (math.cos(a), math.sin(a), 0.0)
            t = (-d[1], d[0], 0.0)
            yonlu_kutu(g, (cx + d[0] * (R + 0.01), cy + d[1] * (R + 0.01), z + 1.3), mul(t, 0.36), mul(d, 0.02), (0, 0, 0.85),
                       cam_grubu(r["cam"], a, z), kabuk=False)
    top(g, (cx, cy, Ht + 0.6), R + 0.1, R + 0.1, 1.9, M("Kubbe", BLOK_CINKO))
    silindir(g, cx, cy, Ht + 2.3, 0.6, 0.3, 8, r["suslu"])
    # Ana giriş: ortada camlı saçak
    L = x1 - x0
    cam_kapi(g, on, L / 2, EV_ZEMIN, r, w=1.8, h=2.8, kasa=r["koyu"], sacak=False)
    ckutu(g, on, L / 2 - 1.6, L / 2 + 1.6, 0, 1.5, EV_ZEMIN + 3.0, EV_ZEMIN + 3.12, M("SacakCam", "#9fb8c6"))
    ckutu(g, on, L / 2 - 1.65, L / 2 + 1.65, 0, 1.55, EV_ZEMIN + 3.12, EV_ZEMIN + 3.2, r["koyu"])
    mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=6, oval=rnd.random() < 0.5)
    baca_sira(g, x0 + 4.0, (y0 + y1) / 2, Ht + 2.6, r, 4)
    baca_sira(g, x1 - 3.0, (y0 + y1) / 2, Ht + 2.6, r, 3)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_kubbeli(rnd):
    """Kubbeli apartman: dar, yüksek Haussmann; oval çatı pencereleri, iki yanda çatıyı aşan sağır duvarlar, çatı ışıklıkları."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 11.0, 12.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    x0, x1, y0, y1 = 0.5, 10.5, 1.4, 11.4
    Ht, on, *_ = paris(g, rnd, r, x0, x1, y0, y1, 7, "haussmann", ("on",), dukkan=True,
                       tema=rnd.choice(["firin", "kafe", "genel", "kitap"]), sagir=("sag", "sol"))
    zt = mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=4, oval=True, yan_egik=False)
    for x in (x0, x1):
        sagir_duvar(g, x, y0 + 0.4, y1 - 0.4, zt + 0.2, r)
        baca_sira(g, x, (y0 + y1) / 2, zt + 0.32, r, 5)
    for k in range(2):
        kutu(g, 3.5 + k * 3, 4.3 + k * 3, y0 + 4.0, y0 + 5.0, zt, zt + 0.2, M("Isiklik", "#bcd3df"))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_faubourg(rnd):
    """Faubourg evi: kiremit mansart, sıra sıra lukarn, yeşil panjurlar ve pencere önü çiçekleri, yanda camlı ek."""
    g = Govde()
    r = blok_renk(rnd, "klasik")
    r["cati"], r["cati_koyu"] = M("Cati", "#b0583a"), M("CatiKoyu", koyu_ton("#b0583a"))
    r["duvar"] = M("Duvar", rnd.choice(["#e8c98f", "#ecd3a4", "#e6c08a"]))
    PW, PD = 16.0, 12.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    x0, x1, y0, y1 = 2.2, 15.6, 1.4, 11.0
    kat = 6
    Ht, on, sag, sol, arka = paris(g, rnd, r, x0, x1, y0, y1, kat, "klasik", ("on",), dukkan=rnd.random() < 0.5, sagir=("sol",))
    L = x1 - x0
    n = max(2, int(round(L / 2.45)))
    pj = M("Panjur", rnd.choice(["#5f8a6a", "#4f7a5a", "#6f9a7a"]))
    for f in range(kat - 1):
        z = EV_ZEMIN + BLOK_ZEMIN + f * BLOK_KAT
        for i in range(n):
            u = L * (i + 0.5) / n
            for s in (-1, 1):
                ckutu(g, on, u + s * 0.62 - 0.17, u + s * 0.62 + 0.17, 0, 0.05, z + 0.8, z + 2.35, pj)
            cicek_kutusu(g, on, u, z + 0.72, r, rnd)
    mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=7, yan_egik=False)
    sagir_duvar(g, x0, y0, y1, Ht + 2.0, r)
    baca_sira(g, x0 + 0.2, (y0 + y1) / 2, Ht + 2.1, r, 3)
    baca_sira(g, x1 - 0.6, (y0 + y1) / 2, Ht + 2.6, r, 3)
    cam = M("CamEk", "#bcd3df")
    kutu(g, 0.3, x0, 5.5, 10.0, EV_ZEMIN, EV_ZEMIN + 3.2, cam)                    # camlı ek (veranda)
    kutu(g, 0.2, x0, 5.4, 10.1, EV_ZEMIN + 3.2, EV_ZEMIN + 3.35, r["koyu"])
    for yy in (5.5, 7.75, 10.0):
        kutu(g, 0.25, 0.35, yy - 0.05, yy + 0.05, EV_ZEMIN, EV_ZEMIN + 3.2, r["koyu"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_altbau(rnd):
    """Avlulu kira apartmanı (Altbau): pastel sıva, kemerli geçit, geçidin üstünde cumba, altta dükkânlar, yan duvarda reklam."""
    g = Govde()
    r = blok_renk(rnd, "siva")
    r["duvar"] = M("Duvar", rnd.choice(["#ecc9c4", "#e9d2b4", "#d6e0cc", "#e2cfe0"]))
    r["cati"], r["cati_koyu"] = M("Cati", "#8a4a32"), M("CatiKoyu", koyu_ton("#8a4a32"))
    PW, PD = 18.0, 12.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    x0, x1, y0, y1 = 1.0, 17.0, 1.4, 11.0
    kat = 6
    Ht, on, sag, sol, arka = paris(g, rnd, r, x0, x1, y0, y1, kat, "siva", ("on",), dukkan=True, tema=rnd.choice(["kafe", "genel", "cicek"]),
                                   sagir=("sol",))
    L = x1 - x0
    # Kemerli geçit (ortada) ve üstünde üç katlık cumba
    kemer(g, on, L / 2, EV_ZEMIN, 2.8, 3.3, 0.0, 0.2, r["suslu"])
    kemer(g, on, L / 2, EV_ZEMIN, 2.3, 3.0, 0.2, 0.22, M("Gecit", "#3a3633"))
    z1 = EV_ZEMIN + BLOK_ZEMIN
    ckutu(g, on, L / 2 - 1.8, L / 2 + 1.8, 0, 0.9, z1 + 0.1, z1 + 3 * BLOK_KAT, r["duvar"])
    cfc = Cephe(on.P(L / 2 - 1.8, 0.9, 0.0), on.u, on.out)
    for f in range(3):
        for cu in (0.9, 2.7):
            pencere(g, cfc, cu, z1 + f * BLOK_KAT + 0.8, 0.8, 1.45, r, sade=True)
    ckutu(g, on, L / 2 - 1.9, L / 2 + 1.9, 0, 1.0, z1 + 3 * BLOK_KAT, z1 + 3 * BLOK_KAT + 0.15, r["suslu"])
    korkuluk(g, on, L / 2 - 1.8, L / 2 + 1.8, 0.95, z1 + 3 * BLOK_KAT + 0.15, r)
    zt = mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=7, yan_egik=False)
    sagir_duvar(g, x0, y0, y1, zt - 0.3, r)
    ckutu(g, sol, 2.0, 7.5, 0, 0.03, Ht - 6.0, Ht - 2.0, M("Reklam", "#efe2c4"))           # eski boya reklam
    ckutu(g, sol, 2.4, 7.1, 0.03, 0.05, Ht - 3.4, Ht - 2.8, r["kirmizi"])
    for x in (x0 + 2.5, x1 - 2.5, (x0 + x1) / 2):
        ev_baca(g, x, y0 + 4.5, zt - 1.0, zt + 0.8, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_pasajli(rnd):
    """Pasajlı bina: üç kat yüksekliğinde kemer, ardında dökme demir ve camdan tonozlu pasaj, kemerin üstünde saat."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 16.0, 16.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    x0, x1, y0, y1 = 0.8, 15.2, 1.4, 8.6
    kat = 6
    Ht, on, *_ = paris(g, rnd, r, x0, x1, y0, y1, kat, "haussmann", ("on",), dukkan=True, tema="genel", sagir=("sag", "sol"))
    L = x1 - x0
    z0 = EV_ZEMIN
    kemer(g, on, L / 2, z0, 3.8, 9.2, 0.0, 0.25, r["suslu"])
    kemer(g, on, L / 2, z0, 3.2, 8.8, 0.25, 0.27, M("PasajCam", "#a9c9de"))
    for k in range(1, 4):
        ckutu(g, on, L / 2 - 1.6, L / 2 + 1.6, 0.25, 0.3, z0 + k * 2.2, z0 + k * 2.2 + 0.08, r["koyu"])
    ckutu(g, on, L / 2 - 0.06, L / 2 + 0.06, 0.25, 0.3, z0, z0 + 8.6, r["koyu"])
    saat(g, on, L / 2, z0 + 9.9, 0.55, r, o=0.1)
    # Pasaj: iki yanda alçak kanatlar, ortada cam tonoz
    for a, b in ((x0, 6.4), (9.6, x1)):
        kutu(g, a, b, y1, 15.4, z0, z0 + 10.0, r["duvar"], atla=("zn",))
        kutu(g, a - 0.1, b + 0.1, y1, 15.5, z0 + 10.0, z0 + 10.35, r["suslu"])
    kutu(g, 6.4, 9.6, y1, 15.4, z0, z0 + 0.1, r["tas_acik"])
    tonoz_y(g, 6.4, 9.6, y1, 15.4, z0 + 7.0, 1.6, M("Vitrin_a9c9de", "#a9c9de"), kabuk=False)
    for yy in range(int(y1) + 1, 16, 1):
        tonoz_kaburga(g, 6.4, 9.6, yy, z0 + 7.0, 1.6, r["koyu"], t=0.08)
    mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=6, yan_egik=False)
    cephe_poligon(g, on, [(L / 2 - 2.2, Ht), (L / 2 + 2.2, Ht), (L / 2, Ht + 1.4)], 0.0, 0.4, r["suslu"])
    for x in (x0, x1):
        sagir_duvar(g, x, y0, y1, Ht + 2.4, r)
        baca_sira(g, x, (y0 + y1) / 2, Ht + 2.5, r, 4)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_eski_sehir(rnd):
    """Eski şehir evleri: iki bitişik mansartlı ev, oval çatı pencereleri, ön bahçede alçak çit, çöp kutuları ve basamaklar."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 16.0, 13.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["cim"])
    levha(g, 0, PW, 0, 1.4, EV_ZEMIN, EV_ZEMIN + 0.12, r["kaldirim"])
    for i, (a, b) in enumerate(((0.6, 8.0), (8.0, 15.4))):
        ri = dict(r)
        if i == 1:
            ri["duvar"] = M("Duvar", rnd.choice(["#efe5d2", "#e2d3b8"]))
        Ht, on, sag, sol, arka = paris(g, rnd, ri, a, b, 3.8, 11.6, 3, "klasik", ("on",), dukkan=False)
        mansart(g, a, b, 3.8, 11.6, Ht, ri, rnd, n=2, oval=True, ic=1.0)
        baca_sira(g, (a + b) / 2, 7.7, Ht + 2.6, ri, 3, yon="x")
        L = b - a
        for k in range(3):                                                     # giriş basamakları
            ckutu(g, on, L / 2 - 0.9, L / 2 + 0.9, 0, 0.9 - k * 0.3, EV_ZEMIN + k * 0.15, EV_ZEMIN + (k + 1) * 0.15, r["beton"])
        cit_hat(g, a + 0.2, 1.7, a + L / 2 - 1.1, 1.7, EV_ZEMIN, M("Cit", "#7a3b2e"), h=0.8, aralik=0.5)
        cit_hat(g, a + L / 2 + 1.1, 1.7, b - 0.2, 1.7, EV_ZEMIN, M("Cit", "#7a3b2e"), h=0.8, aralik=0.5)
        for k in range(2):
            cop_kutusu(g, a + 0.8 + k * 0.6, 3.0, r, M("CopMavi", "#3f5f9a") if k else r["koyu"])
        for k in range(3):
            top(g, (b - 1.0 - k * 0.6, 2.6, EV_ZEMIN + 0.3), 0.25, 0.25, 0.22, rnd.choice(r["cicek"] + [r["cali"]]))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_mews(rnd):
    """Ahır sokağı (mews): beş pastel iki katlı ev, araba kapıları (bazıları camlanmış), çiçek kutuları, taş döşeli avlu, babalar."""
    g = Govde()
    r = ar(rnd, "#e8e4dc", cati="#4f5560")
    PW, PD = 20.0, 10.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("ArnavutTasi", "#c9a88a"))
    renkler = ["#dfe6ea", "#efe2c8", "#f0cfc6", "#e8e2d4", "#dfe8d8", "#e6d8ea"]
    rnd.shuffle(renkler)
    z0, H = EV_ZEMIN, EV_ZEMIN + 5.8
    for i in range(5):
        a, b = 0.2 + i * 3.92, 0.2 + (i + 1) * 3.92
        duv = M("Duvar", renkler[i])
        kutu(g, a, b, 3.0, 9.6, z0, H, duv, atla=("zn",))
        fc = Cephe((a, 3.0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        kapi_renk = M("Kapi", rnd.choice(["#2f4a6a", "#2f5a3a", "#3a3d42", "#6a2f3a"]))
        if rnd.random() < 0.35:
            cdortgen(g, fc, 0.4, 2.8, 0.02, z0 + 0.1, z0 + 2.4, cam_grubu(r["cam"], a, z0))
            ckutu(g, fc, 0.3, 2.9, 0, 0.06, z0 + 2.4, z0 + 2.55, r["koyu"])
        else:
            ckutu(g, fc, 0.4, 2.8, 0, 0.06, z0, z0 + 2.45, kapi_renk)
            for k in range(1, 4):
                ckutu(g, fc, 0.4 + k * 0.6, 0.4 + k * 0.6 + 0.04, 0.06, 0.08, z0, z0 + 2.45, r["koyu"])
        ckutu(g, fc, 3.0, 3.75, 0, 0.05, z0, z0 + 2.2, kapi_renk)
        lamba_klasik(g, a + 3.45, 2.75, r, h=0.2, z0=z0 + 2.35) if DETAY else None
        for u in (1.0, 2.8):
            pencere(g, fc, u, z0 + 3.4, 0.85, 1.3, {**r, "duvar": duv}, sade=True)
            cicek_kutusu(g, fc, u, z0 + 3.32, r, rnd)
        if rnd.random() < 0.4 and DETAY:                                       # mor salkım
            for k in range(5):
                top(g, fc.P(0.6 + k * 0.6, 0.15, z0 + 2.9), 0.25, 0.12, 0.22, M("Salkim", "#9a7ac8"))
        for k in range(2):
            silindir(g, a + 0.5 + k * 2.9, 2.6, z0, 0.5, 0.25, 8, r["saksi"])
            top(g, (a + 0.5 + k * 2.9, 2.6, z0 + 1.0), 0.32, 0.32, 0.45, r["cali"])
    besik_cati(g, "x", 0.2, 19.8, 3.0, 9.6, H, r, sacak=0.25, yan_sacak=0.15)
    for i in range(5):
        cx = 0.2 + i * 3.92 + 1.96
        kutu(g, cx - 0.5, cx + 0.5, 3.6, 4.6, H, H + 1.3, r["suslu"])
        cdortgen(g, Cephe((cx - 0.5, 3.6, 0.0), (1, 0, 0), (0, -1, 0)), 0.2, 0.8, 0.01, H + 0.2, H + 1.0, r["cam"])
        kutu(g, cx - 0.62, cx + 0.62, 3.5, 4.7, H + 1.3, H + 1.42, r["cati_koyu"])
        if i % 2 == 0:
            ev_baca(g, cx + 1.2, 6.3, H + 1.0, H + 3.3, r)
    for k in range(8):                                                         # babalar
        silindir(g, 1.0 + k * 2.5, 0.6, z0, 0.75, 0.12, 6, r["koyu"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_kosk(rnd):
    """Ön avlulu köşk: dar, yüksek taş ev; simetrik cephe, alınlıkta yuvarlak pencere, mansart, uzun bacalar, ferforje çit."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 12.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["cim"])
    x0, x1, y0, y1 = 1.6, 10.4, 4.5, 12.5
    Ht, on, sag, sol, arka = paris(g, rnd, r, x0, x1, y0, y1, 4, "haussmann", ("on",), dukkan=False)
    L = x1 - x0
    ckutu(g, on, L / 2 - 1.6, L / 2 + 1.6, 0, 0.35, EV_ZEMIN, Ht, r["duvar"])           # öne çıkan orta aks
    cephe_poligon(g, on, [(L / 2 - 1.9, Ht), (L / 2 + 1.9, Ht), (L / 2, Ht + 1.6)], 0.0, 0.45, r["suslu"])
    yuvarlak_pencere(g, on, L / 2, Ht + 0.6, 0.38, 0.46, r["cam"])
    zt = mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=2)
    for x in (x0 + 0.8, x1 - 0.8):
        baca_sira(g, x, (y0 + y1) / 2, zt - 0.6, r, 3, h=2.4)
    levha(g, L / 2 + x0 - 0.8, L / 2 + x0 + 0.8, 0, y0, EV_ZEMIN, EV_ZEMIN + 0.05, r["tas_acik"])
    ferforje_cit(g, 0.3, PW - 0.3, 0.6, r, kapi=(4.0, 8.0), aralik=2.85)
    for x in (2.0, 10.0):
        top(g, (x, 2.0, EV_ZEMIN + 0.6), 0.55, 0.55, 0.55, r["cali"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_artnouveau(rnd):
    """Art Nouveau villa: turuncu tuğla, sivri külahlı yuvarlak kule, alınlıkta yuvarlak pencere, mor şerit, balkon, ferforje çit."""
    g = Govde()
    r = ar(rnd, rnd.choice(["#d98a5a", "#cf7a4e", "#e09a6a"]), cati="#5a6470")
    r["suslu"] = M("Suslu", "#f2e6cf")
    PW, PD = 12.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["cim"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 3.0, 10.6, 4.4, 11.8
    KY, kat = 3.1, 3
    Ht = z0 + 0.6 + kat * KY
    kutu(g, x0, x1, y0, y1, z0, Ht, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    L = x1 - x0
    ckutu(g, on, -0.05, L + 0.05, 0, 0.08, z0, z0 + 0.6, r["suslu"])
    pencereler(g, on, L, z0 + 0.6, kat, KY, r, w=1.1, h=1.7, aralik=2.6, atla=((2.0, 4.2),))
    ckutu(g, on, 2.6, 3.7, 0, 0.06, z0 + 0.6, z0 + 2.9, r["kapi"])
    ckutu(g, on, 2.3, 4.0, 0, 1.0, z0 + 3.0, z0 + 3.12, M("Sacak", "#7f9fb8"))
    for k in range(3):
        ckutu(g, on, 2.4, 3.9, 0, 1.0 - k * 0.3, z0 + 0.15 * k, z0 + 0.15 * (k + 1), r["suslu"])
    ckutu(g, on, 4.6, 7.0, 0, 0.9, z0 + 0.6 + KY - 0.1, z0 + 0.6 + KY + 0.05, r["beton"])   # balkon
    korkuluk(g, on, 4.6, 7.0, 0.9, z0 + 0.6 + KY + 0.05, r)
    for fc, LL in ((sag, y1 - y0), (sol, y1 - y0), (arka, L)):
        pencereler(g, fc, LL, z0 + 0.6, kat, KY, r, w=0.9, h=1.5, aralik=2.4, sade=True)
    mor = M("Serit", rnd.choice(["#b89ad0", "#9ab8d0", "#d0b89a"]))
    for fc, LL in ((on, L), (sag, y1 - y0), (sol, y1 - y0)):
        ckutu(g, fc, 0, LL, 0, 0.05, Ht - 0.9, Ht - 0.5, mor)
    zr = kalca_cati(g, x0, x1, y0, y1, Ht, 0.8, r["cati"], sacak=0.35)
    # Önde alınlık ve yuvarlak pencere
    cephe_poligon(g, on, [(L * 0.45, Ht), (L - 0.2, Ht), ((L * 0.45 + L - 0.2) / 2, Ht + 2.6)], -0.2, 0.25, r["duvar"])
    yuvarlak_pencere(g, on, (L * 0.45 + L - 0.2) / 2, Ht + 0.9, 0.42, 0.26, r["cam"])
    # Köşe kulesi
    cx, cy, R = x0 + 0.2, y0 + 0.3, 1.5
    silindir(g, cx, cy, z0, Ht + 0.8, R, 12, r["duvar"])
    silindir(g, cx, cy, Ht - 0.9, 0.4, R + 0.03, 12, mor)
    for f in range(kat):
        z = z0 + 0.6 + f * KY
        for a in (math.pi * 1.1, math.pi * 1.4):
            d = (math.cos(a), math.sin(a), 0.0)
            t = (-d[1], d[0], 0.0)
            yonlu_kutu(g, (cx + d[0] * (R + 0.01), cy + d[1] * (R + 0.01), z + 1.4), mul(t, 0.32), mul(d, 0.02), (0, 0, 0.75),
                       cam_grubu(r["cam"], a, z), kabuk=False)
    koni(g, cx, cy, Ht + 0.8, 3.6, R + 0.25, 12, r["cati"])
    cubuk(g, (cx, cy, Ht + 4.3), (cx, cy, Ht + 5.2), 0.06, r["sari"])
    ev_baca(g, x1 - 1.5, y1 - 2.5, zr - 1.5, zr + 0.9, r)
    ferforje_cit(g, 0.3, PW - 0.3, 0.6, r, kapi=(4.53, 7.47), aralik=2.94)
    calilik(g, 7.5, 11.3, 1.4, r, rnd)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_konak_bahceli(rnd):
    """Bahçeli konak: ahşap karkas üst kat, dik kiremit çatı ve çatı pencereleri, garaj kanadı, çitli bahçe ve patika."""
    g = Govde()
    r = ar(rnd, rnd.choice(["#f2e6cf", "#efe2c8"]), cati=rnd.choice(["#c0573a", "#b04a32"]))
    PW, PD = 16.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["cim"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 6.0, 14.5, 4.5, 11.5
    zb = z0 + 0.4
    Ht = zb + 2 * 2.8
    tugla = M("Tugla", "#b8634a")
    kutu(g, x0, x1, y0, y1, z0, zb + 2.8, tugla, atla=("zn",))
    kutu(g, x0 - 0.05, x1 + 0.05, y0 - 0.05, y1 + 0.05, zb + 2.8, Ht, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    W, D = x1 - x0, y1 - y0
    for fc, L in ((on, W), (sag, D), (sol, D), (arka, W)):
        n = int(L / 1.4)
        for k in range(n + 1):
            u = k * L / n
            ckutu(g, fc, u - 0.08, u + 0.08, -0.05, 0.03, zb + 2.8, Ht, r["ahsap_koyu"])
        for zz in (zb + 2.8, Ht - 0.14):
            ckutu(g, fc, -0.05, L + 0.05, -0.05, 0.035, zz, zz + 0.14, r["ahsap_koyu"])
    for u in (1.4, W - 1.4):
        ev_pencere(g, on, u, zb + 0.6, 0.95, 1.4, r, rnd, panjur=True, saksi=True)
        ev_pencere(g, on, u, zb + 3.4, 0.85, 1.2, r, rnd, panjur=False, saksi=True)
    ckutu(g, on, W / 2 - 0.55, W / 2 + 0.55, 0, 0.06, zb, zb + 2.3, r["kapi"])
    ckutu(g, on, W / 2 - 0.9, W / 2 + 0.9, 0, 0.9, zb + 2.4, zb + 2.52, r["ahsap_koyu"])
    zr = besik_cati(g, "x", x0, x1, y0, y1, Ht, r, sacak=0.5)
    for cx in (x0 + 2.3, x1 - 2.3):
        kutu(g, cx - 0.55, cx + 0.55, y0 + 0.2, y0 + 1.6, Ht - 0.1, Ht + 1.3, r["duvar"], atla=("zn",))
        dfc = Cephe((cx - 0.55, y0 + 0.2, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        cdortgen(g, dfc, 0.2, 0.9, 0.01, Ht + 0.1, Ht + 1.05, r["cam"])
        cprizma(g, dfc, [(-0.1, Ht + 1.3), (1.2, Ht + 1.3), (0.55, Ht + 1.9)], -1.5, 0.15, r["cati"])
    ev_baca(g, x0 + 1.2, (y0 + y1) / 2 + 0.6, zr - 1.2, zr + 0.8, r)
    # Garaj kanadı
    gx0, gx1 = 1.2, x0
    kutu(g, gx0, gx1, y0 + 1.0, y1, z0, z0 + 3.0, r["duvar"], atla=("zn",))
    gfc = Cephe((gx0, y0 + 1.0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    ckutu(g, gfc, 0.6, 4.2, 0, 0.06, z0, z0 + 2.4, M("GarajKapi", "#7a4a2e"))
    for k in range(1, 5):
        ckutu(g, gfc, 0.6, 4.2, 0.06, 0.08, z0 + k * 0.48, z0 + k * 0.48 + 0.04, r["ahsap_koyu"])
    besik_cati(g, "y", y0 + 1.0, y1, gx0, gx1, z0 + 3.0, r, sacak=0.3, yan_sacak=0.2)
    levha(g, 1.8, 4.6, 0, y0 + 1.0, z0, z0 + 0.04, M("Patika", "#cfc6b6"))
    levha(g, x0 + W / 2 - 0.6, x0 + W / 2 + 0.6, 0, y0, z0, z0 + 0.04, M("Patika", "#cfc6b6"))
    cit = M("Cit", "#5a3b26")
    cit_hat(g, 4.9, 0.4, 9.6, 0.4, z0, cit, h=0.8, aralik=0.6)
    cit_hat(g, 11.2, 0.4, 15.6, 0.4, z0, cit, h=0.8, aralik=0.6)
    cit_hat(g, 15.6, 0.4, 15.6, 13.6, z0, cit, h=0.8, aralik=0.6)
    bank(g, 12.5, 2.6, math.pi / 2, r, z0)
    kutu(g, 2.0, 3.0, y1 + 0.3, y1 + 1.0, z0, z0 + 1.0, M("Cop", "#4f7a4a"))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------------- CADDE DÜKKÂNLARI

def ref_firin(rnd, bistro=False):
    """Ekmek fırını / teraslı bistro: dar Haussmann, zeminde koyu kırmızı dükkân cephesi; bistroda yeşil tente ve teras."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    r["ana"] = M("Marka", "#8e1f2a" if not bistro else "#7a1f24")
    PW, PD = 9.0, 12.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    x0, x1, y0, y1 = 0.5, 8.5, 2.4 if bistro else 1.4, 11.4
    Ht, on, *_ = paris(g, rnd, r, x0, x1, y0, y1, 5, "haussmann", ("on",), dukkan=True,
                       tema="kafe" if bistro else "firin", sagir=("sag", "sol"))
    L = x1 - x0
    z0 = EV_ZEMIN
    tabela(g, on, L / 2, z0 + BLOK_ZEMIN - 0.55, L - 1.0, 0.45, 0.18, r, "BRASSERIE" if bistro else "BOULANGERIE",
           renk=r["ana"], yazi_renk=M("Yazi", "#f2d58a"), oran=0.6)
    zt = mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=3, oval=not bistro, yan_egik=False)
    for x in (x0, x1):
        sagir_duvar(g, x, y0 + 0.3, y1 - 0.3, zt + 0.3, r)
        baca_sira(g, x, (y0 + y1) / 2, zt + 0.42, r, 5)
    if bistro:
        yesil = M("Tente", "#2f6a46")
        tente(g, on, -0.2, L + 0.2, z0 + 2.9, 2.2, r, yesil)
        for x in (1.8, 4.5, 7.0):
            silindir(g, x, 1.2, z0, 0.75, 0.3, 8, r["beyaz"])
            for a in (0.0, math.pi):
                silindir(g, x + 0.55 * math.cos(a), 1.2, z0, 0.45, 0.16, 6, r["koyu"])
        for x in (0.6, 8.4):
            kutu(g, x - 0.35, x + 0.35, 0.6, 1.3, z0, z0 + 0.6, r["ana"])
            top(g, (x, 0.95, z0 + 1.0), 0.35, 0.35, 0.45, r["cali"])
    else:
        bank(g, 6.8, 0.8, -math.pi / 2, r, z0)
        bisiklet(g, 1.5, 0.7, 0.0, r, rnd)
        silindir(g, 0.8, 1.0, z0, 0.4, 0.22, 6, r["saksi"])
        top(g, (0.8, 1.0, z0 + 0.65), 0.28, 0.28, 0.3, r["cali"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_pub(rnd):
    """Köşedeki pub: tuğla bina, koyu lacivert/yeşil zemin kat ve altın yazı, köşede pahlı giriş ve dökme demir sütun,
    asma tabela, çiçek kutuları, fıçılar, şemsiyeli masalar."""
    g = Govde()
    r = ar(rnd, rnd.choice(["#9c4a35", "#a8503a"]), cati="#5a4a44", ana=rnd.choice(["#1f2f4a", "#1f4a36"]))
    r["suslu"] = M("Suslu", "#efe6d4")
    PW, PD = 14.0, 12.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 1.6, 13.0, 1.6, 10.8
    H0, KY = z0 + 3.6, 3.0
    Ht = H0 + 2 * KY
    pah = 1.4
    pts = [(x0 + pah, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0 + pah)]
    prizma_poligon(g, pts, z0, Ht, r["duvar"])
    prizma_poligon(g, [(x - 0.12 if x <= x0 + 0.01 else x + 0.12 if x >= x1 - 0.01 else x,
                        y - 0.12 if y <= y0 + 0.01 else y + 0.12 if y >= y1 - 0.01 else y) for x, y in pts], z0, H0, r["ana"])
    on = Cephe((x0 + pah, y0 - 0.12, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    sol = Cephe((x0 - 0.12, y1, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    altin = M("Yazi", "#e8c35a")
    for fc, L in ((on, x1 - x0 - pah), (sol, y1 - y0 - pah)):
        ckutu(g, fc, 0.2, L - 0.2, 0, 0.05, H0 - 0.75, H0 - 0.35, altin)
        vitrin(g, fc, 0.6, L - 0.6, z0 + 0.5, H0 - 1.0, r, aralik=1.3, kasa=r["ana"])
    p0 = (x0 - 0.12, y0 + pah)
    p1 = (x0 + pah, y0 - 0.12)
    # Pahlı köşedeki camlı kapı (çapraz yüz): cepheye düz bir dörtgen
    d = norm((p1[0] - p0[0], p1[1] - p0[1], 0.0))
    o = (-0.03, -0.03)
    a = (p0[0] + d[0] * 0.3 + o[0], p0[1] + d[1] * 0.3 + o[1])
    b = (p1[0] - d[0] * 0.3 + o[0], p1[1] - d[1] * 0.3 + o[1])
    bv = len(g.v)
    g.v += [(a[0], a[1], z0), (b[0], b[1], z0), (b[0], b[1], z0 + 2.4), (a[0], a[1], z0 + 2.4)]
    idx = [bv, bv + 1, bv + 2, bv + 3]
    if dot(newell([g.v[i] for i in idx]), (-1.0, -1.0, 0.0)) < 0:
        idx = idx[::-1]
    HEX["Vitrin_8a9aa8"] = "#8a9aa8"
    g.yuz(idx, "Vitrin_8a9aa8", kabuk=False)
    silindir(g, x0 + 0.1, y0 + 0.1, z0, H0 - z0, 0.12, 8, r["ana"])
    on2 = Cephe((x0 + pah, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    sol2 = Cephe((x0, y1, 0.0), (0.0, -1.0, 0.0), (-1.0, 0.0, 0.0))
    for fc, L in ((on2, x1 - x0 - pah), (sol2, y1 - y0 - pah)):
        n = max(2, int(L / 2.3))
        for f in range(2):
            for i in range(n):
                u = L * (i + 0.5) / n
                pencere(g, fc, u, H0 + f * KY + 0.7, 0.95, 1.5, r)
                cicek_kutusu(g, fc, u, H0 + f * KY + 0.62, r, rnd)
        for zz in range(int((Ht - H0) / 0.5)):                                 # köşe taşları
            ckutu(g, fc, L - 0.5 if zz % 2 else L - 0.35, L, 0, 0.04, H0 + zz * 0.5, H0 + zz * 0.5 + 0.4, r["suslu"])
    zr = kalca_cati(g, x0, x1, y0, y1, Ht, 0.55, r["cati"], sacak=0.3)
    for x in (x0 + 2.0, x1 - 1.6):
        ev_baca(g, x, y1 - 1.6, zr - 1.4, zr + 0.6, r)
    cikma_tabela(g, sol2, y1 - y0 - pah - 0.8, H0 + 0.3, r, r["ana"])
    for (x, y) in ((4.0, 0.8), (9.5, 0.8)):
        masa_semsiye(g, x, y, r, M("Semsiye", rnd.choice(["#2f6a46", "#f6f3ec"])))
    for (x, y) in ((0.7, 4.5), (0.7, 5.4), (0.7, 7.5)):
        fici(g, x, y, r)
    lamba_klasik(g, 0.6, 0.6, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_eski_dukkanlar(rnd):
    """Eski şehir dükkânları: solda mansartlı taş bina (kırmızı tenteli brasserie), sağda basamaklı alınlıklı tuğla ev (peynirci)."""
    g = Govde()
    PW, PD = 14.0, 12.0
    r = blok_renk(rnd, "haussmann")
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    r["ana"] = M("Marka", "#8e1f2a")
    Ht, on, *_ = paris(g, rnd, r, 0.5, 7.4, 1.6, 11.0, 3, "haussmann", ("on", "sol"), dukkan=True, tema="kafe", sagir=("sag",))
    tente(g, on, 0.3, 6.6, z0 + 2.9, 1.6, r, M("Tente", "#a0242c"))
    mansart(g, 0.5, 7.4, 1.6, 11.0, Ht, r, rnd, n=3, oval=True)
    baca_sira(g, 4.0, 6.3, Ht + 2.6, r, 3, yon="x")
    for x in (1.5, 4.0):
        masa_semsiye(g, x, 0.8, r, M("Semsiye", "#f6f3ec")) if False else silindir(g, x, 0.8, z0, 0.75, 0.3, 8, r["beyaz"])
    r2 = blok_renk(rnd, "kanal")
    r2["ana"] = M("Marka", "#1f4a36")
    kat = 4
    H0 = z0 + BLOK_ZEMIN
    Ht2 = H0 + (kat - 1) * BLOK_KAT
    x0, x1, y0, y1 = 7.4, 13.5, 1.6, 11.0
    kutu(g, x0, x1, y0, y1, z0, Ht2, r2["duvar"], atla=("zn",))
    fc = Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    g.tema = "genel"
    blok_cephe(g, fc, x1 - x0, kat, r2, rnd, True, "tugla", z0)
    tente(g, fc, 0.3, x1 - x0 - 0.3, z0 + 2.9, 1.4, r2, M("Tente", "#1f4a36"), r2["beyaz"])
    besik_cati(g, "y", y0 + 0.3, y1, x0, x1, Ht2, {**r2, "cati": M("Cati", "#4a3f3a"), "cati_koyu": M("CatiKoyu", "#3a302c")},
               sacak=0.0, yan_sacak=0.15)
    L = x1 - x0
    for k in range(5):                                                         # basamaklı alınlık
        u0, u1 = 0.2 + k * 0.55, L - 0.2 - k * 0.55
        if u1 - u0 < 0.6:
            break
        ckutu(g, fc, u0, u1, 0, 0.3, Ht2 + k * 0.7, Ht2 + (k + 1) * 0.7, r2["duvar"])
        ckutu(g, fc, u0 - 0.05, u0 + 0.45, 0, 0.36, Ht2 + (k + 1) * 0.7, Ht2 + (k + 1) * 0.7 + 0.08, r2["suslu"])
        ckutu(g, fc, u1 - 0.45, u1 + 0.05, 0, 0.36, Ht2 + (k + 1) * 0.7, Ht2 + (k + 1) * 0.7 + 0.08, r2["suslu"])
    pencere(g, fc, L / 2, Ht2 + 0.6, 0.8, 1.2, r2, sade=True)
    cikma_tabela(g, fc, L - 0.4, H0 - 0.2, r2, r2["ana"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_bufe(rnd):
    """Durak büfesi: gazete ve dondurma büfesi (afişli, mavi çatılı), tek ayaklı masa, banklı tramvay durağı, posta kutusu."""
    g = Govde()
    r = ar(rnd, "#2f5a9a", ana="#2f5a9a")
    PW, PD = 14.0, 9.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Kaldirim", "#ddd7cc"))
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 0.8, 7.6, 2.0, 6.5
    kutu(g, x0, x1, y0, y1, z0, z0 + 2.9, r["duvar"], atla=("zn",))
    kutu(g, x0 - 0.25, x1 + 0.25, y0 - 0.4, y1 + 0.25, z0 + 2.9, z0 + 3.25, r["duvar"])
    on = Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    for a, b, metin in ((0.3, 3.2, "KIOSQUE"), (3.6, 6.5, "PRESSE")):
        ckutu(g, on, a, b, 0, 0.05, z0 + 0.9, z0 + 2.4, M("Pano", "#f2e6cf"))
        for i in range(8):
            u = a + 0.25 + (i % 4) * 0.68
            zz = z0 + 1.05 + (i // 4) * 0.6
            ckutu(g, on, u, u + 0.5, 0.05, 0.07, zz, zz + 0.45, rnd.choice(r["urun"]))
        tabela(g, on, (a + b) / 2, z0 + 2.65, b - a - 0.4, 0.35, 0.08, r, metin, renk=r["beyaz"], yazi_renk=r["duvar"], oran=0.7)
    ckutu(g, on, 0.3, 6.5, 0, 0.6, z0 + 0.85, z0 + 0.95, r["ahsap"])
    kutu(g, 0.2, 0.75, 4.0, 5.2, z0, z0 + 1.0, M("Cop", "#e0b24a"))
    silindir(g, 2.0, 0.9, z0, 1.05, 0.05, 6, r["metal"])
    silindir(g, 2.0, 0.9, z0 + 1.05, 0.05, 0.4, 8, r["beyaz"])
    for x in (4.2, 5.0):
        kutu(g, x - 0.3, x + 0.3, 1.2, 1.5, z0, z0 + 1.2, r["metal"])        # gazete standı
    yesil = M("Cim", "#9cc46a")
    levha(g, 8.2, 11.0, 4.8, 8.6, z0, z0 + 0.06, yesil)
    agac(g, 9.6, 6.8, r, rnd, boy=1.6, rr=0.95)
    kutu(g, 8.6, 9.2, 2.0, 2.5, z0, z0 + 1.1, M("PostaKutusu", "#f2c14a"))
    # Tramvay durağı
    sx0, sx1, sy0, sy1 = 10.5, 13.7, 2.4, 4.0
    for x in (sx0, sx1):
        for y in (sy0, sy1):
            cubuk(g, (x, y, z0), (x, y, z0 + 2.6), 0.08, r["koyu"], kabuk=False)
    kutu(g, sx0 - 0.2, sx1 + 0.2, sy0 - 0.4, sy1 + 0.2, z0 + 2.6, z0 + 2.75, M("SacakCam", "#9fb8c6"))
    kutu(g, sx0, sx1, sy1 - 0.05, sy1, z0 + 0.3, z0 + 2.5, M("DurakCam", "#bcd3df"))
    kutu(g, sx0 + 0.3, sx1 - 0.3, sy1 - 0.5, sy1 - 0.1, z0 + 0.45, z0 + 0.55, r["ahsap"])
    kutu(g, sx1 - 0.05, sx1, sy0, sy1, z0 + 0.6, z0 + 2.2, M("Yazi", "#e8f0c8"))   # ışıklı hat haritası
    cubuk(g, (10.0, 1.4, z0), (10.0, 1.4, z0 + 2.8), 0.07, r["metal"])
    silindir(g, 10.0, 1.4, z0 + 2.6, 0.06, 0.38, 10, M("DurakTabela", "#3f9a5a"))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_benzinlik(rnd):
    """Marketli benzinlik: pompaların üstünde yeşil kenarlı saçak, küçük market, yıkama bölümü, yol kenarında fiyat direği."""
    g = Govde()
    r = ar(rnd, "#d8d2c6", ana="#2f6a46")
    PW, PD = 16.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["asfalt"])
    z0 = EV_ZEMIN
    kutu(g, 1.5, 12.0, 9.0, 13.5, z0, z0 + 3.6, r["duvar"], atla=("zn",))
    mfc = Cephe((1.5, 9.0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    vitrin(g, mfc, 0.6, 6.0, z0 + 0.1, z0 + 2.6, r, aralik=1.4, poster=0.4, rnd=rnd)
    cam_kapi(g, mfc, 7.2, z0, r, w=1.4, h=2.4, sacak=False)
    duz_cati_ust(g, 1.5, 12.0, 9.0, 13.5, z0 + 3.6, r, rnd, r["duvar"], klima=2)
    kutu(g, 12.0, 15.5, 8.5, 13.5, z0, z0 + 3.2, M("Yikama", "#e9e7e2"), atla=("zn",))   # yıkama bölümü
    ckutu(g, Cephe((12.0, 8.5, 0.0), (1.0, 0, 0), (0, -1.0, 0)), 0.4, 3.1, 0, 0.05, z0, z0 + 2.8, r["koyu"])
    for (x, y) in ((4.0, 3.0), (11.0, 3.0), (4.0, 6.4), (11.0, 6.4)):
        kutu(g, x - 0.3, x + 0.3, y - 0.3, y + 0.3, z0, z0 + 5.0, r["beyaz"])
    kutu(g, 2.0, 13.5, 1.6, 7.8, z0 + 5.0, z0 + 5.8, r["beyaz"], atla=("zn",))
    for fc, L in ((Cephe((2.0, 1.6, 0.0), (1, 0, 0), (0, -1, 0)), 11.5), (Cephe((13.5, 1.6, 0.0), (0, 1, 0), (1, 0, 0)), 6.2),
                  (Cephe((2.0, 7.8, 0.0), (0, -1, 0), (-1, 0, 0)), 6.2)):
        ckutu(g, fc, 0, L, 0, 0.05, z0 + 5.1, z0 + 5.6, r["ana"])
        ckutu(g, fc, 0, L, 0.05, 0.07, z0 + 5.0, z0 + 5.08, M("Serit", "#e08a3a"))
    tabela(g, Cephe((2.0, 1.6, 0.0), (1, 0, 0), (0, -1, 0)), 7.0, z0 + 5.35, 4.0, 0.35, 0.08, r, "STATION-SERVICE",
           renk=r["ana"], yazi_renk=r["beyaz"], oran=0.7)
    for (x, y) in ((6.5, 3.4), (6.5, 6.0), (9.0, 3.4), (9.0, 6.0)):
        kutu(g, x - 0.6, x + 0.6, y - 0.35, y + 0.35, z0, z0 + 0.2, r["beton"])
        kutu(g, x - 0.35, x + 0.35, y - 0.22, y + 0.22, z0 + 0.2, z0 + 1.7, r["ana"])
        kutu(g, x - 0.3, x + 0.3, y - 0.24, y + 0.24, z0 + 1.1, z0 + 1.5, r["beyaz"])
    park_araba(g, 4.5, 4.7, 0.0, araba_renk(rnd), r, z0)
    park_araba(g, 10.8, 4.7, math.pi, araba_renk(rnd), r, z0)
    # Fiyat direği
    kutu(g, 0.6, 1.4, 0.5, 1.0, z0, z0 + 6.0, r["beyaz"])
    kutu(g, 0.55, 1.45, 0.45, 1.05, z0 + 4.6, z0 + 6.0, M("Turuncu", "#e0602a"))
    kutu(g, 0.55, 1.45, 0.45, 1.05, z0 + 3.0, z0 + 4.5, r["ana"])
    kutu(g, 14.0, 14.6, 1.0, 1.6, z0, z0 + 1.3, M("Hava", "#3f73e6"))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_market(rnd):
    """Mahalle marketi: tek katlı indirim marketi, kırmızı şerit, saçak altında camlı giriş, otopark, cam atık kumbaraları,
    bisiklet park yeri, fiyat direği."""
    g = Govde()
    r = ar(rnd, "#efece6", ana=rnd.choice(["#c9302c", "#2f6ab0", "#e0a020"]))
    PW, PD = 18.0, 16.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["asfalt"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 0.8, 12.5, 3.0, 15.2
    kutu(g, x0, x1, y0, y1, z0, z0 + 4.2, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    for fc, L in ((on, x1 - x0), (sag, y1 - y0)):
        ckutu(g, fc, -0.05, L + 0.05, 0, 0.12, z0 + 3.1, z0 + 4.2, r["ana"])
    tabela(g, sag, 2.5, z0 + 3.65, 4.5, 0.6, 0.15, r, "SUPERMARCHE", renk=r["ana"], yazi_renk=r["beyaz"], oran=0.6)
    cdortgen(g, sag, 6.0, 10.5, 0.01, z0 + 0.1, z0 + 2.8, M("Vitrin_bcd3df", "#bcd3df"))
    cam_kapi(g, sag, 8.2, z0, r, w=1.8, h=2.6, sacak=False)
    ckutu(g, sag, 5.5, 11.5, 0, 2.0, z0 + 2.9, z0 + 3.1, r["ana"])
    vitrin(g, on, 1.0, 10.5, z0 + 0.2, z0 + 2.6, r, aralik=2.0, poster=0.6, rnd=rnd)
    duz_cati_ust(g, x0, x1, y0, y1, z0 + 4.2, r, rnd, r["duvar"], klima=3)
    for k in range(2):
        kutu(g, 4.0 + k * 4, 5.2 + k * 4, 8.0, 9.0, z0 + 4.77, z0 + 5.0, M("Isiklik", "#bcd3df"))
    for i in range(3):
        boya(g, 13.2, 17.6, 4.0 + i * 2.6, 4.06 + i * 2.6, r["serit"], z=z0 + 0.008)
    park_araba(g, 15.5, 5.3, 0.0, araba_renk(rnd), r, z0)
    for k in range(4):
        cubuk(g, (2.0 + k * 0.6, 1.8, z0), (2.0 + k * 0.6, 1.8, z0 + 0.7), 0.05, r["metal"])
    for k, c in enumerate(("#4f7a4a", "#8a5a3a")):
        kutu(g, 14.0 + k * 1.3, 15.1 + k * 1.3, 13.5, 14.6, z0, z0 + 1.6, M("CamKumbara", c))
    kutu(g, 16.6, 17.2, 1.0, 1.4, z0, z0 + 3.2, r["beyaz"])
    kutu(g, 16.5, 17.3, 0.95, 1.45, z0 + 2.0, z0 + 3.0, r["ana"])
    top(g, (14.3, 2.2, z0 + 0.5), 0.8, 0.5, 0.45, r["cali"])
    park_lambasi(g, 13.0, 15.0, r, z0)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_autohaus(rnd):
    """Autohaus: küp logolu grafit renkli cam showroom, camın ardında arabalar, önünde fiyat etiketli satış alanı ve direk."""
    g = Govde()
    r = ar(rnd, "#3a3d42", ana="#e6e2da")
    PW, PD = 18.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Kaldirim", "#d8d2c6"))
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 1.0, 17.0, 6.5, 13.4
    kutu(g, x0, x1, y0 + 0.4, y1, z0, z0 + 4.6, r["duvar"], atla=("zn",))
    fc = Cephe((x0, y0 + 0.4, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    cdortgen(g, fc, 0.2, x1 - x0 - 0.2, 0.01, z0, z0 + 4.2, M("Vitrin_9fb8c6", "#9fb8c6"))
    for k in range(9):
        ckutu(g, fc, 0.1 + k * 1.97, 0.1 + k * 1.97 + 0.1, 0, 0.08, z0, z0 + 4.3, r["koyu"])
    kutu(g, x0 - 0.3, x1 + 0.3, y0 - 0.6, y1 + 0.3, z0 + 4.6, z0 + 5.2, r["duvar"])
    for k in range(3):
        park_araba(g, 3.5 + k * 5.0, 10.2, 0.0, araba_renk(rnd), r, z0)
    kutu(g, 13.5, 15.5, 9.5, 11.5, z0 + 5.2, z0 + 7.2, r["koyu"])            # küp logo
    for fc2 in cepheler(13.5, 15.5, 9.5, 11.5):
        cephe_poligon(g, fc2, [(0.5, z0 + 6.6), (1.5, z0 + 6.6), (1.5, z0 + 6.0), (1.0, z0 + 5.6), (0.5, z0 + 6.0)], 0.0, 0.04, r["ana"])
    yesil = M("Cim", "#9cc46a")
    levha(g, 0, PW, 0, 0.8, z0, z0 + 0.1, yesil)
    for k in range(4):
        x = 2.0 + k * 4.0
        park_araba(g, x, 3.6, math.pi / 2 + (0.3 if k % 2 else -0.3), araba_renk(rnd), r, z0)
        kutu(g, x - 0.5, x + 0.5, 1.4, 1.5, z0 + 0.6, z0 + 1.2, r["beyaz"])
        cubuk(g, (x, 1.45, z0), (x, 1.45, z0 + 0.6), 0.04, r["metal"])
    for k in range(5):
        kutu(g, 0.6 + k * 3.6, 1.6 + k * 3.6, 5.6, 6.3, z0, z0 + 0.6, r["cali"])
    kutu(g, 16.6, 17.4, 1.2, 1.6, z0, z0 + 5.0, r["koyu"])
    kutu(g, 16.5, 17.5, 1.15, 1.65, z0 + 4.0, z0 + 4.9, r["ana"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_fitness(rnd):
    """Fitness stüdyosu: siyah kutu, cam arkasında koşu bantları ve bisikletler, neon dambıl ve 24/7, otopark."""
    g = Govde()
    r = ar(rnd, "#2b2d31", ana="#ff4fa0")
    PW, PD = 18.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["asfalt"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 0.6, 17.4, 6.2, 13.6
    kutu(g, x0, x1, y0, y1, z0, z0 + 5.2, r["duvar"], atla=("zn",))
    fc = Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    cdortgen(g, fc, 0.3, x1 - x0 - 0.3, 0.01, z0 + 0.1, z0 + 3.6, M("Vitrin_d8e4ea", "#d8e4ea"))
    neon = M("Yazi", "#ff4fa0")
    ckutu(g, fc, 0.2, x1 - x0 - 0.2, 0, 0.05, z0 + 3.75, z0 + 3.85, neon)
    tabela(g, fc, (x1 - x0) / 2 + 1.0, z0 + 4.45, 2.2, 0.6, 0.06, r, "24/7", renk=r["duvar"], yazi_renk=M("Yazi", "#4fe0e0"), oran=0.8)
    for u in (4.0, (x1 - x0) / 2 - 1.0):
        ckutu(g, fc, u - 0.5, u + 0.5, 0, 0.06, z0 + 4.4, z0 + 4.5, neon)
        ckutu(g, fc, u - 0.55, u - 0.4, 0, 0.06, z0 + 4.25, z0 + 4.65, neon)
        ckutu(g, fc, u + 0.4, u + 0.55, 0, 0.06, z0 + 4.25, z0 + 4.65, neon)
    for k in range(12):                                                        # koşu bantları
        x = x0 + 1.0 + k * 1.3
        kutu(g, x - 0.35, x + 0.35, y0 + 0.6, y0 + 2.2, z0, z0 + 0.25, r["koyu"])
        kutu(g, x - 0.35, x + 0.35, y0 + 0.6, y0 + 0.8, z0 + 0.25, z0 + 1.3, r["metal"])
    cam_kapi(g, fc, 9.5, z0, r, w=1.8, h=2.8, kasa=r["koyu"])
    duz_cati_ust(g, x0, x1, y0, y1, z0 + 5.2, r, rnd, r["duvar"], klima=3)
    for i in range(6):
        boya(g, 1.0 + i * 2.6, 1.06 + i * 2.6, 0.6, 4.6, r["serit"], z=z0 + 0.008)
    for k in range(4):
        park_araba(g, 2.3 + k * 2.6 + (2.6 if k > 1 else 0), 2.6, math.pi / 2, araba_renk(rnd), r, z0)
    for k in range(5):
        bisiklet(g, 11.5 + k * 0.5, 5.4, math.pi / 2, r, rnd)
    kutu(g, 16.8, 17.4, 0.6, 1.2, z0, z0 + 3.4, r["koyu"])
    kutu(g, 16.75, 17.45, 0.55, 1.25, z0 + 2.4, z0 + 3.3, neon)
    kutu(g, 0.6, 17.4, 0.1, 0.4, z0, z0 + 0.5, r["cali"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------------- BÜRO

def ref_buro_klinker(rnd):
    """Büro binası: koyu klinker, sivri başlıklı sık tuğla payandalar, teraslı basamaklı üst katlar, altın sivri kemerli portal
    ve döner kapı, önünde bisiklet park yeri."""
    g = Govde()
    r = ar(rnd, rnd.choice(["#7a3a2a", "#6e3426", "#843f2c"]), ana="#2f5a3a")
    r["suslu"] = M("Suslu", "#efe2c8")
    PW, PD = 16.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    KY = 3.3
    kademeler = [(1.0, 15.0, 1.6, 13.0, 7), (1.9, 14.1, 2.5, 12.1, 1), (2.8, 13.2, 3.4, 11.2, 1)]
    z = z0
    altin = M("Altin", "#d9a83a")
    for ki, (x0, x1, y0, y1, nk) in enumerate(kademeler):
        h = (BLOK_ZEMIN if ki == 0 else 0.0) + nk * KY
        kutu(g, x0, x1, y0, y1, z, z + h, r["duvar"], atla=("zn",))
        for fc, L in zip(cepheler(x0, x1, y0, y1), (x1 - x0, y1 - y0, y1 - y0, x1 - x0)):
            n = int(L / 1.25)
            zb = z + (BLOK_ZEMIN if ki == 0 else 0.0)
            for k in range(n + 1):
                u = k * L / n
                ckutu(g, fc, u - 0.12, u + 0.12, 0, 0.22, zb, z + h + 0.5, r["duvar"])
                if DETAY:
                    p = fc.P(u, 0.11, 0.0)
                    koni(g, p[0], p[1], z + h + 0.5, 0.5, 0.17, 4, r["duvar"])
            for f in range(nk):
                zz = zb + f * KY
                for k in range(n):
                    u = (k + 0.5) * L / n
                    cdortgen(g, fc, u - 0.38, u + 0.38, 0.01, zz + 0.7, zz + 2.6, cam_grubu(r["cam"], u, zz))
                ckutu(g, fc, -0.05, L + 0.05, 0, 0.12, zz, zz + 0.15, r["suslu"])
        z += h
        if ki < 2:
            kutu(g, x0, x1, y0, y1, z, z + 0.25, r["beton"])
            for k in range(5):
                top(g, (x0 + 1.5 + k * (x1 - x0 - 3) / 4, y0 + 0.5, z + 0.5), 0.35, 0.3, 0.3, r["cali"])
    duz_cati_ust(g, 2.8, 13.2, 3.4, 11.2, z, r, rnd, r["duvar"], klima=1)
    kutu(g, 6.0, 9.0, 6.0, 8.5, z + 0.5, z + 2.3, r["duvar"])
    cubuk(g, (12.0, 9.0, z + 0.5), (12.0, 9.0, z + 5.0), 0.1, r["metal"])
    # Zemin kat: koyu yeşil vitrinler, ortada altın sivri kemerli portal ve döner kapı
    on = Cephe((1.0, 1.6, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    L = 14.0
    for a, b in ((0.4, 5.4), (8.6, 13.6)):
        vitrin(g, on, a, b, z0 + 0.3, z0 + 2.7, r, aralik=1.25, kasa=r["ana"])
        tente(g, on, a, b, z0 + 2.75, 0.9, r, r["ana"], r["beyaz"])
    cephe_poligon(g, on, [(5.5, z0), (8.5, z0), (8.5, z0 + 4.2), (7.0, z0 + 6.0), (5.5, z0 + 4.2)], 0.0, 0.3, altin)
    cephe_poligon(g, on, [(6.0, z0), (8.0, z0), (8.0, z0 + 3.9), (7.0, z0 + 5.2), (6.0, z0 + 3.9)], 0.3, 0.32, M("Vitrin_8fa8b8", "#8fa8b8"))
    silindir(g, 8.0, 0.7, z0, 2.6, 0.9, 10, M("DonerKapi", "#bcd3df"))
    silindir(g, 8.0, 0.7, z0 + 2.6, 0.2, 0.95, 10, altin)
    for k in range(8):
        bisiklet(g, 1.6 + k * 0.6 if k < 4 else 9.8 + (k - 4) * 0.6, 0.6, math.pi / 2, r, rnd)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------------- SANAYİ

def testere_cati(g, x0, x1, y0, y1, z, n, h, r, cati, cam):
    """Testere dişi çatı: dişler y boyunca dizilir, dik yüzleri camlı (kuzeye)."""
    D = (y1 - y0) / n
    for i in range(n):
        a, b = y0 + i * D, y0 + (i + 1) * D
        pts = [(x0, a, z), (x1, a, z), (x1, b, z), (x0, b, z), (x0, b, z + h), (x1, b, z + h)]
        dis_bukey(g, pts, [[0, 1, 2, 3], [0, 1, 5, 4], [2, 3, 4, 5], [0, 4, 3], [1, 2, 5]], cati)
        cdortgen(g, Cephe((x1, b, 0.0), (-1.0, 0.0, 0.0), (0.0, 1.0, 0.0)), 0.3, x1 - x0 - 0.3, 0.02, z + 0.2, z + h - 0.15, cam)


def ref_matbaa(rnd):
    """Matbaa: üç katlı tuğla, büyük kafesli pencereler, testere dişi çatı, uzun baca, saatli alınlık, yükleme kapısı, kamyon."""
    g = Govde()
    r = ar(rnd, rnd.choice(["#a8503a", "#9c4a35", "#b05a40"]), cati="#8f949a")
    r["suslu"] = M("Suslu", "#e8d6c8")
    PW, PD = 14.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 1.0, 10.5, 1.6, 12.6
    KY = 3.8
    Ht = z0 + 3 * KY
    kutu(g, x0, x1, y0, y1, z0, Ht, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    for fc, L in ((on, x1 - x0), (sag, y1 - y0), (sol, y1 - y0)):
        n = int(L / 2.2)
        for f in range(3):
            for i in range(n):
                u = L * (i + 0.5) / n
                if fc is sag and f == 0 and abs(u - L * 0.7) < 1.5:
                    continue
                w, h = 1.5, 2.6
                zz = z0 + f * KY + 0.6
                cdortgen(g, fc, u - w / 2, u + w / 2, 0.01, zz, zz + h, cam_grubu(r["cam"], u, zz))
                for k in (1, 2):
                    ckutu(g, fc, u - w / 2 + k * w / 3 - 0.03, u - w / 2 + k * w / 3 + 0.03, 0, 0.05, zz, zz + h, r["cerceve"])
                for k in (1, 2, 3):
                    ckutu(g, fc, u - w / 2, u + w / 2, 0, 0.05, zz + k * h / 4 - 0.03, zz + k * h / 4 + 0.03, r["cerceve"])
                ckutu(g, fc, u - w / 2 - 0.1, u + w / 2 + 0.1, 0, 0.1, zz + h, zz + h + 0.25, r["suslu"])
            ckutu(g, fc, -0.05, L + 0.05, 0, 0.12, z0 + (f + 1) * KY - 0.15, z0 + (f + 1) * KY, r["suslu"])
    ckutu(g, sag, (y1 - y0) * 0.7 - 1.3, (y1 - y0) * 0.7 + 1.3, 0, 0.06, z0, z0 + 3.4, M("Kepenk", "#6f7a84"))
    testere_cati(g, x0, x1, y0, y1, Ht, 3, 2.4, r, r["cati"], M("Cam_bcd3df", "#bcd3df"))
    L = x1 - x0
    cephe_poligon(g, on, [(L / 2 - 2.2, Ht), (L / 2 + 2.2, Ht), (L / 2 + 2.2, Ht + 1.0), (L / 2 + 1.2, Ht + 1.0),
                          (L / 2 + 1.2, Ht + 1.7), (L / 2 - 1.2, Ht + 1.7), (L / 2 - 1.2, Ht + 1.0), (L / 2 - 2.2, Ht + 1.0)], 0.0, 0.3, r["duvar"])
    saat(g, on, L / 2, Ht + 0.95, 0.5, r, o=0.3)
    silindir(g, x0 + 1.2, y1 - 1.0, z0, 20.0, 0.65, 10, r["duvar"])
    for zz in (8.0, 14.0, 19.0):
        silindir(g, x0 + 1.2, y1 - 1.0, z0 + zz, 0.35, 0.75, 10, r["suslu"])
    kamyon(g, 12.4, 7.0, math.pi / 2, r, M("Kamyon", "#7f9aa8"))
    cikma_tabela(g, on, 0.6, z0 + 3.0, r, r["koyu"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_lastik(rnd):
    """Lastik fabrikası: gri kule ve tabela, silolar ve konveyör, uzun vulkanizasyon atölyesi, koyu ofis, dev lastik, otopark."""
    g = Govde()
    r = ar(rnd, "#d6d6d2", cati="#5a6068")
    PW, PD = 18.0, 16.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["asfalt"])
    z0 = EV_ZEMIN
    kutu(g, 0.8, 7.0, 1.0, 7.4, z0, z0 + 15.0, r["duvar"], atla=("zn",))
    for fc, L in zip(cepheler(0.8, 7.0, 1.0, 7.4), (6.2, 6.4, 6.4, 6.2)):
        for k in range(4):
            cdortgen(g, fc, 0.4, L - 0.4, 0.01, z0 + 2.0 + k * 3.3, z0 + 2.9 + k * 3.3, M("CamKoyu", "#5f7f99"))
    tabela(g, Cephe((0.8, 1.0, 0.0), (1, 0, 0), (0, -1, 0)), 3.1, z0 + 13.6, 4.8, 0.8, 0.1, r, "MÉLANGES", renk=r["koyu"],
           yazi_renk=r["beyaz"], oran=0.6)
    for k, c in enumerate(("#efece6", "#2b2d31", "#efece6")):
        x = 8.4 + k * 2.2
        silindir(g, x, 3.0, z0 + 1.2, 7.0, 0.95, 10, M("Silo", c))
        koni(g, x, 3.0, z0 + 8.2, 0.6, 0.95, 10, M("Silo", c))
        koni(g, x, 3.0, z0 + 1.2, -1.0, 0.95, 10, M("Silo", c)) if False else None
        for sx in (-0.6, 0.6):
            cubuk(g, (x + sx, 3.0, z0), (x + sx, 3.0, z0 + 1.3), 0.12, r["metal"])
    cubuk(g, (13.5, 3.0, z0 + 6.5), (16.5, 8.0, z0 + 3.5), 0.6, r["metal"])         # konveyör
    kutu(g, 8.0, 17.4, 8.0, 15.2, z0, z0 + 5.0, r["duvar"], atla=("zn",))
    duz_cati_ust(g, 8.0, 17.4, 8.0, 15.2, z0 + 5.0, r, rnd, r["duvar"], klima=0)
    for k in range(3):
        silindir(g, 10.0 + k * 3.0, 13.5, z0 + 5.5, 1.6, 0.18, 8, r["metal"])
        kutu(g, 9.5 + k * 3.0, 11.0 + k * 3.0, 9.5, 10.5, z0 + 5.55, z0 + 5.8, M("Isiklik", "#bcd3df"))
    for fc, L in zip(cepheler(8.0, 17.4, 8.0, 15.2), (9.4, 7.2, 7.2, 9.4)):
        cdortgen(g, fc, 0.6, L - 0.6, 0.01, z0 + 3.3, z0 + 4.4, M("CamKoyu", "#5f7f99"))
    kutu(g, 0.8, 7.0, 9.0, 15.2, z0, z0 + 6.6, M("Ofis", "#2e3a4a"), atla=("zn",))
    for fc, L in zip(cepheler(0.8, 7.0, 9.0, 15.2), (6.2, 6.2, 6.2, 6.2)):
        for f in range(2):
            for k in range(3):
                u = L * (k + 0.5) / 3
                cdortgen(g, fc, u - 0.6, u + 0.6, 0.01, z0 + 0.9 + f * 3.0, z0 + 2.2 + f * 3.0, cam_grubu(r["cam"], u, f))
    duz_cati_ust(g, 0.8, 7.0, 9.0, 15.2, z0 + 6.6, r, rnd, M("Ofis", "#2e3a4a"), klima=1)
    pts = [(9.0 + 1.6 * math.cos(2 * math.pi * i / 12), 6.4, z0 + 1.6 + 1.6 * math.sin(2 * math.pi * i / 12)) for i in range(12)]
    pts += [(p[0], 7.4, p[2]) for p in pts]
    dis_bukey(g, pts, [list(range(12)), list(range(12, 24))] + [[i, (i + 1) % 12, 12 + (i + 1) % 12, 12 + i] for i in range(12)], r["koyu"])
    for k in range(3):
        park_araba(g, 14.0 + k * 1.3 - 4.0 if False else 10.5 + k * 2.3, 6.0 if False else 5.8, math.pi / 2, araba_renk(rnd), r, z0) if False else None
    for k in range(3):
        park_araba(g, 12.3 + k * 2.3, 6.3, math.pi / 2, araba_renk(rnd), r, z0)
    for x in (8.0, 17.6):
        cit_hat(g, x, 0.3, x, 7.6, z0, r["metal"], h=1.6, aralik=1.5)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_insaat_deposu(rnd):
    """İnşaat malzemeleri ve kereste deposu: kapıda büro/showroom, tahta raflı açık sundurma, tuğla paletleri, kum ve çakıl
    bölmeleri, forklift, telli çit."""
    g = Govde()
    r = ar(rnd, "#efe2c8", cati="#7f9aa0", ana="#2f6a46")
    PW, PD = 18.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Beton", "#c9c4bb"))
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 0.8, 7.6, 1.4, 8.4
    kutu(g, x0, x1, y0, y1, z0, z0 + 6.4, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    vitrin(g, on, 0.4, 4.6, z0 + 0.2, z0 + 2.7, r, aralik=1.4)
    ckutu(g, on, 5.2, 6.2, 0, 0.06, z0, z0 + 2.4, r["ana"])
    for fc, L in ((on, 6.8), (sol, 7.0), (sag, 7.0)):
        for k in range(3):
            pencere(g, fc, L * (k + 0.5) / 3, z0 + 3.6, 1.0, 1.5, r, sade=True)
    tabela(g, on, 3.4, z0 + 3.15, 5.0, 0.6, 0.15, r, "MATERIAUX", renk=r["ana"], yazi_renk=r["beyaz"], oran=0.6)
    duz_cati_ust(g, x0, x1, y0, y1, z0 + 6.4, r, rnd, r["duvar"], klima=0)
    # Açık sundurma
    sx0, sx1, sy0, sy1 = 8.4, 17.6, 9.0, 13.6
    for x in (sx0, (sx0 + sx1) / 2, sx1):
        for y in (sy0, sy1):
            kutu(g, x - 0.12, x + 0.12, y - 0.12, y + 0.12, z0, z0 + 4.6, r["metal"])
    yonlu_kutu(g, ((sx0 + sx1) / 2, (sy0 + sy1) / 2 - 0.4, z0 + 4.8), ((sx1 - sx0) / 2 + 0.3, 0, 0), (0, (sy1 - sy0) / 2 + 0.8, -0.5),
               (0, 0, 0.06), r["cati"])
    ah = M("Kereste", "#c99a5a")
    for k in range(3):
        for j in range(4):
            kutu(g, sx0 + 0.4 + j * 2.2, sx0 + 2.3 + j * 2.2, sy1 - 1.4, sy1 - 0.4, z0 + 0.3 + k * 1.2, z0 + 0.6 + k * 1.2, ah)
    for k in range(3):                                                         # kum ve çakıl bölmeleri
        x = 9.0 + k * 2.8
        kutu(g, x - 0.1, x, 5.6, 8.0, z0, z0 + 1.2, r["beton"])
        kutu(g, x + 2.4, x + 2.5, 5.6, 8.0, z0, z0 + 1.2, r["beton"])
        kutu(g, x, x + 2.4, 7.9, 8.0, z0, z0 + 1.2, r["beton"])
        top(g, (x + 1.2, 6.9, z0 + 0.3), 1.1, 1.0, 0.85, M("Kum", ["#e0c890", "#a9a49a", "#c8a070"][k]))
    for k in range(3):                                                         # tuğla paletleri
        kutu(g, 9.0 + k * 1.5, 10.2 + k * 1.5, 2.0, 3.2, z0, z0 + 0.15, r["ahsap"])
        kutu(g, 9.05 + k * 1.5, 10.15 + k * 1.5, 2.05, 3.15, z0 + 0.15, z0 + 1.0, M("Tugla", "#b8634a"))
    kutu(g, 14.5, 16.0, 2.2, 3.6, z0, z0 + 1.4, M("Forklift", "#f2c14a"))
    cubuk(g, (14.6, 2.2, z0 + 1.4), (14.6, 2.2, z0 + 2.6), 0.1, r["koyu"])
    cubuk(g, (15.9, 2.2, z0 + 1.4), (15.9, 2.2, z0 + 2.6), 0.1, r["koyu"])
    kutu(g, 14.6, 15.9, 1.4, 2.2, z0 + 0.2, z0 + 0.3, r["koyu"])
    for (a, b) in (((8.2, 0.3), (17.7, 0.3)), ((17.7, 0.3), (17.7, 8.6))):
        cit_hat(g, a[0], a[1], b[0], b[1], z0, r["metal"], h=1.8, aralik=1.6)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_tamirhane(rnd):
    """Garaj ve oto tamirhanesi: seksiyonel kapılı tuğla atölye, açık bölmede liftte araba, kırmızı tabela, eski benzin pompası,
    lastik yığınları, yağ varilleri, avluda arabalar, telli çit."""
    g = Govde()
    r = ar(rnd, rnd.choice(["#a8503a", "#9c4a35"]), ana="#c9302c")
    PW, PD = 16.0, 14.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["asfalt"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 0.8, 13.0, 7.2, 13.6
    kutu(g, x0, x1, y0, y1, z0, z0 + 4.6, r["duvar"], atla=("zn",))
    fc = Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    kep = M("Kepenk", "#9aa0a6")
    ckutu(g, fc, 0.6, 3.6, 0, 0.06, z0, z0 + 3.2, kep)
    for k in range(1, 7):
        ckutu(g, fc, 0.6, 3.6, 0.06, 0.08, z0 + k * 0.45, z0 + k * 0.45 + 0.04, r["koyu"])
    ckutu(g, fc, 4.2, 7.2, 0, 0.02, z0, z0 + 3.2, M("Ic", "#2b2622"))          # açık bölme
    kutu(g, x0 + 4.6, x0 + 6.8, y0 + 0.4, y0 + 2.4, z0 + 1.2, z0 + 1.3, r["metal"])
    park_araba(g, x0 + 5.7, y0 + 1.3, math.pi / 2, araba_renk(rnd), r, z0 + 1.3)
    cdortgen(g, fc, 8.2, 11.6, 0.01, z0 + 0.9, z0 + 2.6, cam_grubu(r["cam"], 9.0, 0.0))
    ckutu(g, fc, 7.6, 8.4, 0, 0.05, z0, z0 + 2.3, r["kapi"])
    tabela(g, fc, 5.2, z0 + 3.9, 7.0, 0.9, 0.2, r, "GARAGE", renk=r["ana"], yazi_renk=r["beyaz"], oran=0.6)
    duz_cati_ust(g, x0, x1, y0, y1, z0 + 4.6, r, rnd, r["duvar"], klima=1)
    kutu(g, 13.4, 13.9, 7.6, 8.1, z0, z0 + 1.8, M("Pompa", "#3f73e6"))
    top(g, (13.65, 7.85, z0 + 2.0), 0.22, 0.22, 0.22, r["beyaz"])
    for (x, y, a) in ((2.4, 3.0, math.pi / 2), (5.4, 4.0, math.pi / 2 + 0.3), (9.2, 4.6, 0.2), (12.5, 3.0, math.pi / 2)):
        park_araba(g, x, y, a, araba_renk(rnd), r, z0)
    for k in range(4):
        for j in range(3):
            silindir(g, 14.7, 10.0 + k * 0.9, z0 + j * 0.25, 0.24, 0.4, 10, r["koyu"])
    for k in range(3):
        silindir(g, 14.6 + (k % 2) * 0.7, 12.6 + (k // 2) * 0.7, z0, 0.9, 0.3, 8, M("Varil", "#2f5a9a"))
    for (a, b) in (((0.3, 0.4), (15.7, 0.4)), ((0.3, 0.4), (0.3, 7.0)), ((15.7, 0.4), (15.7, 9.0))):
        cit_hat(g, a[0], a[1], b[0], b[1], z0, r["metal"], h=1.8, aralik=1.5)
    park_lambasi(g, 15.0, 1.2, r, z0)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_atolye(rnd):
    """Tuğla atölye: ikiz alınlıklı (biri krem sıvalı, biri tuğla) atölyeler, kemerli yeşil kapılar, kiremit çatı, çatı
    sırtının altında vinç kirişi, önde taş avlu ve alçak çit."""
    g = Govde()
    PW, PD = 14.0, 13.0
    r = ar(rnd, "#efe2c8", cati="#b0583a")
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    yesil = M("Kapi", "#2f5a3a")
    for i, (a, b, duv) in enumerate(((0.8, 6.8, "#efe2c8"), (6.8, 13.2, "#a8503a"))):
        ri = dict(r)
        ri["duvar"] = M("Duvar", duv)
        y0, y1 = 4.6, 12.4
        H = z0 + 4.2
        kutu(g, a, b, y0, y1, z0, H, ri["duvar"], atla=("zn",))
        fc = Cephe((a, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
        L = b - a
        kemer(g, fc, L * 0.32, z0, 1.6, 2.9, 0.0, 0.06, yesil)
        ckutu(g, fc, L * 0.32 - 0.95, L * 0.32 + 0.95, 0, 0.12, z0 + 2.9, z0 + 3.1, ri["suslu"])
        pencere(g, fc, L * 0.75, z0 + 1.0, 0.9, 1.4, ri, sade=True)
        for s in (-1, 1):
            ckutu(g, fc, L * 0.75 + s * 0.65 - 0.17, L * 0.75 + s * 0.65 + 0.17, 0, 0.05, z0 + 1.0, z0 + 2.4, yesil)
        zr = besik_cati(g, "y", y0, y1, a, b, H, ri, sacak=0.25, yan_sacak=0.25)
        pencere(g, fc, L / 2, H + 0.4, 0.8, 1.2, ri, sade=True)
        for s in (-1, 1):
            ckutu(g, fc, L / 2 + s * 0.6 - 0.16, L / 2 + s * 0.6 + 0.16, 0, 0.05, H + 0.4, H + 1.6, yesil)
        ckutu(g, fc, L / 2 - 0.35, L / 2 + 0.35, -0.3, 0.3, zr + 0.1, zr + 0.6, ri["suslu"])     # alınlık kapak taşı
        for s in (0.0, L):
            ckutu(g, fc, s - 0.3 if s else 0.0, s if s else 0.3, -0.3, 0.3, H - 0.1, H + 0.4, ri["suslu"])
        cubuk(g, fc.P(L / 2, 0.0, zr - 0.6), fc.P(L / 2, 0.9, zr - 0.6), 0.12, r["ahsap_koyu"])
        levha(g, a + 0.3, b - 0.3, 0.6, y0, z0, z0 + 0.04, M("TasAvlu", "#d6cfc2"))
        if i == 0:
            ev_baca(g, a + 1.2, y0 + 3.0, zr - 1.4, zr + 0.4, ri)
    cit = M("Cit", "#9c4a35")
    for (a, b) in ((0.5, 5.6), (8.4, 13.5)):
        cit_hat(g, a, 0.4, b, 0.4, z0, cit, h=0.7, aralik=1.0)
    agac(g, 13.3, 3.2, r, rnd, boy=1.6, rr=0.9)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------------- SİMGE YAPILAR (bir bloğu kaplar: ~28 m)

def ref_opera(rnd):
    """Opera binası: görkemli merdiven, çift sütunlu açık galeri, yaldızlı kanatlı figürler, salonun üstünde bakır kubbe,
    sahne kulesi, köşelerde küçük kubbeler."""
    g = Govde()
    r = ar(rnd, "#ecdfc8", cati="#7fa88f", ana="#c9a03a")
    r["suslu"] = M("Suslu", "#f4ecdc")
    bakir, altin = M("Bakir", "#7fa88f"), M("Altin", "#d9a83a")
    PW, PD = 28.0, 28.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Meydan", "#d6cfc2"))
    z0 = EV_ZEMIN
    # Ön pavyon
    x0, x1, y0, y1 = 3.0, 25.0, 6.5, 14.0
    zb = z0 + 1.6
    Ht = zb + 9.5
    kutu(g, x0, x1, y0, y1, z0, Ht, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    L = x1 - x0
    for k in range(6):                                                         # zemin: kemerli kapılar
        u = 2.5 + k * 3.4
        kemer(g, on, u, zb, 1.8, 3.4, 0.0, 0.04, M("Kapi", "#2f4a3a"))
        kemer(g, on, u, zb, 2.2, 3.7, -0.02, 0.0, r["suslu"]) if False else None
    ckutu(g, on, -0.1, L + 0.1, 0, 0.6, zb + 4.0, zb + 4.4, r["suslu"])
    for k in range(7):                                                         # galeri: çift sütunlar
        u = 0.8 + k * 3.4
        for du in (-0.35, 0.35):
            p = on.P(u + du, 0.8, 0.0)
            silindir(g, p[0], p[1], zb + 4.4, 4.4, 0.24, 8, r["suslu"])
    for k in range(6):
        u = 2.5 + k * 3.4
        kemer(g, on, u, zb + 4.6, 1.8, 3.8, 0.0, 0.04, cam_grubu(r["cam"], u, 3.0))
    ckutu(g, on, -0.2, L + 0.2, 0, 1.2, zb + 8.8, zb + 9.5, r["suslu"])
    kutu(g, x0 + 7.0, x1 - 7.0, y0 + 0.5, y1, Ht, Ht + 1.6, r["duvar"])     # atik
    cephe_poligon(g, on, [(7.0, Ht), (L - 7.0, Ht), (L - 7.0, Ht + 1.6), (7.0, Ht + 1.6)], -0.5, 0.0, r["duvar"])
    tabela(g, on, L / 2, Ht + 0.8, 4.0, 0.6, 0.0, r, "OPÉRA", renk=r["duvar"], yazi_renk=altin, oran=0.7)
    for x in (x0 + 7.2, x1 - 7.2):                                             # kanatlı figürler
        top(g, (x, y0 + 0.9, Ht + 2.3), 0.35, 0.35, 0.7, altin)
        for s in (-1, 1):
            yonlu_kutu(g, (x + s * 0.55, y0 + 0.9, Ht + 2.8), (0.45, 0, 0.0), (0, 0.06, 0), (0, 0, 0.55), altin)
    for fc, LL in ((sag, y1 - y0), (sol, y1 - y0)):
        for k in range(2):
            kemer(g, fc, LL * (k + 0.5) / 2, zb + 4.6, 1.6, 3.4, 0.0, 0.04, cam_grubu(r["cam"], k, 3.0))
    # Köşe pavyonları ve küçük kubbeler
    for cx in (x0 + 1.6, x1 - 1.6):
        kutu(g, cx - 1.8, cx + 1.8, y0 - 0.4, y0 + 3.2, z0, Ht + 0.6, r["duvar"])
        top(g, (cx, y0 + 1.4, Ht + 0.6), 1.7, 1.7, 1.6, bakir)
        top(g, (cx, y0 + 1.4, Ht + 2.2), 0.3, 0.3, 0.5, altin)
    # Salon ve kubbe
    kutu(g, 5.0, 23.0, y1, 22.0, z0, z0 + 14.0, r["duvar"], atla=("zn",))
    for fc, LL in zip(cepheler(5.0, 23.0, y1, 22.0)[1:3], (8.0, 8.0)):
        pencereler(g, fc, LL, z0 + 2.0, 3, 3.6, r, w=1.2, h=2.2, aralik=2.7, sade=True)
    silindir(g, 14.0, 18.0, z0 + 14.0, 1.6, 6.0, 16, r["duvar"])
    for k in range(4):
        a = 2 * math.pi * k / 4
        yuvarlak_pencere(g, Cephe((14.0 + 6.02 * math.cos(a), 18.0 + 6.02 * math.sin(a), 0.0), (-math.sin(a), math.cos(a), 0.0),
                                  (math.cos(a), math.sin(a), 0.0)), 0.0, z0 + 14.8, 0.45, 0.0, r["cam"])
    top(g, (14.0, 18.0, z0 + 15.6), 6.1, 6.1, 4.8, bakir)
    silindir(g, 14.0, 18.0, z0 + 20.2, 1.2, 0.9, 8, r["suslu"])
    top(g, (14.0, 18.0, z0 + 21.4), 1.0, 1.0, 0.9, bakir)
    top(g, (14.0, 18.0, z0 + 22.4), 0.3, 0.3, 0.5, altin)
    # Sahne kulesi
    kutu(g, 7.0, 21.0, 22.0, 27.0, z0, z0 + 19.0, r["duvar"], atla=("zn",))
    besik_cati(g, "x", 7.0, 21.0, 22.0, 27.0, z0 + 19.0, {**r, "cati": bakir, "cati_koyu": M("BakirKoyu", "#6a8f78")}, sacak=0.2, yan_sacak=0.2)
    for x in (7.3, 20.7):
        top(g, (x, 24.5, z0 + 20.5), 0.4, 0.4, 0.8, altin)
    # Merdiven
    for k in range(8):
        kutu(g, x0 + 2.0, x1 - 2.0, y0 - 0.55 * (k + 1), y0 - 0.55 * k, z0, zb - k * 0.2, r["suslu"])
    for x in (x0 + 1.6, x1 - 1.6, x0 + 6.0, x1 - 6.0):
        lamba_klasik(g, x, 1.6, r, h=3.0)
    silindir(g, 1.6, 1.6, z0, 2.4, 0.6, 10, M("Morris", "#2f4a3a"))
    top(g, (1.6, 1.6, z0 + 2.5), 0.65, 0.65, 0.5, M("Morris", "#2f4a3a"))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_gar(rnd):
    """Cam peron çatılı tren garı: saat kuleli taş bina, dev yarım daire pencere, mansartlı yan pavyonlar; arkada demir-cam
    peron tonozu, peronlar ve lokomotif; önde tramvay ve taksi."""
    g = Govde()
    r = ar(rnd, "#e6d6b8", cati="#5a6470")
    r["suslu"] = M("Suslu", "#f4ecdc")
    PW, PD = 30.0, 30.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Meydan", "#d6cfc2"))
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 3.0, 27.0, 5.0, 12.0
    Ht = z0 + 11.0
    kutu(g, x0, x1, y0, y1, z0, Ht, r["duvar"], atla=("zn",))
    on, sag, sol, arka = cepheler(x0, x1, y0, y1)
    L = x1 - x0
    kemer(g, on, L / 2, z0 + 1.5, 9.0, 9.3, 0.0, 0.4, r["suslu"])
    kemer(g, on, L / 2, z0 + 1.6, 8.2, 8.8, 0.4, 0.42, M("Vitrin_a9c9de", "#a9c9de"))
    for k in range(7):                                                         # yelpaze kayıtlar
        a = math.pi * (k + 1) / 8
        zc = z0 + 1.6 + 8.8 - 4.1
        cubuk(g, on.P(L / 2, 0.45, zc), on.P(L / 2 + 4.0 * math.cos(a), 0.45, zc + 4.0 * math.sin(a)), 0.1, r["koyu"], kabuk=False)
    ckutu(g, on, L / 2 - 4.1, L / 2 + 4.1, 0.4, 0.45, zc - 0.05, zc + 0.05, r["koyu"])
    for k in range(3):
        kemer(g, on, L / 2 - 2.6 + k * 2.6, z0, 1.6, 2.8, 0.42, 0.46, M("Kapi", "#2f4a3a"))
    for u in (2.2, 4.6, L - 4.6, L - 2.2):
        for f in range(2):
            pencere(g, on, u, z0 + 1.2 + f * 4.6, 1.2, 2.6, r)
    ckutu(g, on, -0.1, L + 0.1, 0, 0.5, Ht - 0.6, Ht, r["suslu"])
    kutu(g, x0 + 0.5, x1 - 0.5, y0 + 0.5, y1 - 0.5, Ht, Ht + 1.0, r["cati"])
    for cx in (x0 + 2.2, x1 - 2.2):                                            # yan pavyonlar
        kutu(g, cx - 2.6, cx + 2.6, y0 - 0.5, y1, z0, Ht + 1.6, r["duvar"])
        mansart(g, cx - 2.6, cx + 2.6, y0 - 0.5, y1, Ht + 1.6, r, rnd, n=2)
    # Saat kulesi
    tx, ty = x0 + 7.0, y0 + 1.2
    kutu(g, tx - 1.6, tx + 1.6, ty - 1.6, ty + 1.6, z0, z0 + 24.0, r["duvar"])
    for fc in cepheler(tx - 1.6, tx + 1.6, ty - 1.6, ty + 1.6):
        saat(g, fc, 1.6, z0 + 20.5, 1.1, r)
        for f in range(4):
            pencere(g, fc, 1.6, z0 + 6.0 + f * 3.2, 0.7, 1.6, r, sade=True)
    kutu(g, tx - 1.8, tx + 1.8, ty - 1.8, ty + 1.8, z0 + 24.0, z0 + 24.4, r["suslu"])
    top(g, (tx, ty, z0 + 24.4), 1.7, 1.7, 2.0, M("Kubbe", BLOK_CINKO))
    cubuk(g, (tx, ty, z0 + 26.2), (tx, ty, z0 + 28.0), 0.1, r["koyu"])
    # Peron tonozu
    cam = M("Vitrin_bcd3df", "#bcd3df")
    tonoz_y(g, 3.0, 27.0, y1, 29.5, z0 + 4.0, 7.0, cam, n=12, kabuk=False)
    for k in range(7):
        tonoz_kaburga(g, 3.0, 27.0, y1 + 0.5 + k * 2.8, z0 + 4.0, 7.0, r["koyu"], n=12, t=0.18)
    for x in (3.0, 27.0):
        for k in range(7):
            cubuk(g, (x, y1 + 0.5 + k * 2.8, z0), (x, y1 + 0.5 + k * 2.8, z0 + 4.0), 0.2, r["koyu"])
    for x in (8.0, 15.0, 22.0):
        kutu(g, x - 1.0, x + 1.0, y1, 29.0, z0, z0 + 0.9, r["beton"])
    kutu(g, 10.5, 13.0, 18.0, 27.0, z0 + 0.6, z0 + 3.2, M("Lokomotif", "#2b2d31"))
    silindir(g, 11.75, 19.0, z0 + 3.2, 1.2, 0.3, 8, r["koyu"])
    top(g, (11.75, 19.0, z0 + 5.0), 0.8, 0.8, 0.7, M("Buhar", "#f2f2f2"))
    kutu(g, 17.5, 20.0, 15.0, 27.0, z0 + 0.6, z0 + 3.2, M("Vagon", "#3f5f4a"))
    # Önde tramvay ve taksiler
    kutu(g, 4.0, 15.0, 1.0, 3.2, z0 + 0.4, z0 + 3.2, M("Tramvay", "#c9302c"))
    cdortgen(g, Cephe((4.0, 1.0, 0.0), (1, 0, 0), (0, -1, 0)), 0.5, 10.5, 0.01, z0 + 1.7, z0 + 2.7, r["cam"])
    kutu(g, 4.0, 15.0, 1.0, 3.2, z0 + 3.2, z0 + 3.4, r["beyaz"])
    for k in range(2):
        park_araba(g, 19.0 + k * 4.5, 2.2, 0.0, M("Taksi", "#2b2d31"), r, z0)
    for x in (1.0, 29.0):
        lamba_klasik(g, x, 4.0, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_kapali_pazar(rnd):
    """Kapalı pazar: saatli yuvarlak pencereli tuğla alınlıklar, cam fenerli ana nef, yan nefler ve çizgili tenteli tezgâhlar."""
    g = Govde()
    r = ar(rnd, "#a8503a", cati="#5a6470")
    r["suslu"] = M("Suslu", "#efe2c8")
    r["urun"] = [M("Urun", c) for c in ("#f2d04a", "#e0525a", "#f0a0c0", "#6fae4a", "#e8e4dc")]
    PW, PD = 26.0, 28.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Meydan", "#d6cfc2"))
    z0 = EV_ZEMIN
    nx0, nx1, y0, y1 = 8.0, 18.0, 3.0, 26.0
    Hn = z0 + 10.0
    kutu(g, nx0, nx1, y0, y1, z0, Hn, r["duvar"], atla=("zn",))
    zr = besik_cati(g, "y", y0, y1, nx0, nx1, Hn, r, sacak=0.3, yan_sacak=0.1)
    kutu(g, 12.2, 13.8, y0 + 2.0, y1 - 2.0, zr - 0.3, zr + 1.6, M("Vitrin_e8eef2", "#e8eef2"))   # cam fener
    kutu(g, 12.0, 14.0, y0 + 1.9, y1 - 1.9, zr + 1.6, zr + 1.8, r["cati"])
    for x0, x1, sx in ((3.0, nx0, -1), (nx1, 23.0, 1)):                        # yan nefler
        kutu(g, x0, x1, y0 + 1.0, y1 - 1.0, z0, z0 + 5.0, r["duvar"], atla=("zn",))
        xm = (x0 + x1) / 2
        yonlu_kutu(g, (xm, (y0 + y1) / 2, z0 + 5.8), ((x1 - x0) / 2 + 0.4, 0, -sx * 0.8 * 0 + sx * 0.0), (0, (y1 - y0) / 2 - 0.6, 0), (0, 0, 0.08), r["cati"])
        fc = cepheler(nx0, nx1, y0, y1)[1 if sx > 0 else 2]
        cdortgen(g, fc, 1.0, y1 - y0 - 1.0, 0.01, z0 + 6.4, z0 + 9.2, cam_grubu(r["cam"], sx, 6.0))
        sfc = cepheler(x0, x1, y0 + 1.0, y1 - 1.0)[1 if sx > 0 else 2]
        for k in range(6):
            kemer(g, sfc, 1.8 + k * 3.6, z0 + 0.5, 1.6, 3.4, 0.0, 0.04, cam_grubu(r["cam"], k, sx))
    on = Cephe((nx0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    L = nx1 - nx0
    cephe_poligon(g, on, [(0.0, Hn), (L, Hn), (L / 2, zr + 0.4)], -0.3, 0.3, r["duvar"])
    saat(g, on, L / 2, z0 + 7.6, 2.0, r, o=0.1)
    kemer(g, on, L / 2, z0, 3.0, 4.6, 0.0, 0.05, cam_grubu(r["cam"], 0.0, 0.0))
    for u in (0.0, L):
        p = on.P(u, 0.2, 0.0)
        silindir(g, p[0], p[1], z0, Hn + 0.4, 0.4, 8, r["suslu"])
        koni(g, p[0], p[1], Hn + 0.4, 1.4, 0.45, 8, r["cati"])
    renkler = [M("Tente", c) for c in ("#c9302c", "#2f6a46", "#2f5a9a", "#e0a020")]
    for k in range(6):
        serit_tente_tezgah(g, 1.5, y0 + 3.0 + k * 3.7, 2.2, 1.6, r, renkler[k % 4]) if False else None
    for k in range(5):
        x = 2.0
        y = y0 + 3.0 + k * 4.2
        serit_tente_tezgah(g, x, y, 1.6, 2.6, r, renkler[k % 4])
        serit_tente_tezgah(g, PW - x, y, 1.6, 2.6, r, renkler[(k + 2) % 4])
    for x in (4.0, 22.0):
        lamba_klasik(g, x, 1.2, r)
    for k in range(5):
        bisiklet(g, 14.5 + k * 0.6, 1.3, math.pi / 2, r, rnd)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_pazar_pavyonu(rnd):
    """Camlı pazar pavyonu: dökme demir ve cam, beşik tonoz, saatli lunet (yelpaze pencere), girişte çizgili tenteli tezgâhlar."""
    g = Govde()
    r = ar(rnd, "#5a6470", cati="#7f8a96")
    r["urun"] = [M("Urun", c) for c in ("#f2d04a", "#e0525a", "#6fae4a", "#f0a0c0", "#e08a3a")]
    PW, PD = 26.0, 26.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Meydan", "#d6cfc2"))
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 4.0, 22.0, 6.0, 24.0
    H = z0 + 5.0
    kutu(g, x0, x1, y0, y1, z0, z0 + 0.6, M("Tugla", "#a8503a"), atla=("zn",))
    cam = M("Vitrin_bcd3df", "#bcd3df")
    for fc, L in zip(cepheler(x0, x1, y0, y1), (x1 - x0, y1 - y0, y1 - y0, x1 - x0)):
        cdortgen(g, fc, 0.0, L, 0.01, z0 + 0.6, H, cam)
        n = int(L / 1.8)
        for k in range(n + 1):
            ckutu(g, fc, k * L / n - 0.07, k * L / n + 0.07, 0, 0.1, z0, H, r["duvar"])
        ckutu(g, fc, -0.1, L + 0.1, 0, 0.3, H - 0.2, H + 0.2, r["duvar"])
    kutu(g, x0, x1, y0, y1, z0, z0 + 0.05, M("Ic", "#c9b89a"))
    tonoz_y(g, x0, x1, y0, y1, H, 6.5, r["cati"], n=12)
    for k in range(10):
        tonoz_kaburga(g, x0, x1, y0 + 0.2 + k * 1.95, H, 6.5, r["duvar"], n=12, t=0.14)
    kutu(g, 12.0, 14.0, y0 + 1.0, y1 - 1.0, H + 6.3, H + 6.9, cam)
    on = Cephe((x0, y0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    L = x1 - x0
    cephe_poligon(g, on, [(L / 2 + 9.0 * math.cos(math.pi * i / 12), H + 6.5 * math.sin(math.pi * i / 12)) for i in range(13)], -0.05, 0.05, cam)
    for k in range(9):
        a = math.pi * (k + 1) / 10
        cubuk(g, on.P(L / 2, 0.08, H), on.P(L / 2 + 9.0 * math.cos(a), 0.08, H + 6.5 * math.sin(a)), 0.12, r["duvar"], kabuk=False)
    saat(g, on, L / 2, H + 0.9, 0.6, {**r, "koyu": r["duvar"]}, o=0.1)
    ckutu(g, on, L / 2 - 2.0, L / 2 + 2.0, 0, 0.06, z0, z0 + 3.6, M("Ic", "#3a3633"))
    renkler = [M("Tente", c) for c in ("#c9302c", "#e0a020", "#2f6a46", "#2f5a9a")]
    for k in range(8):
        x = 2.0 + k * 2.9
        if 11.0 < x < 15.5:
            continue
        serit_tente_tezgah(g, x, 3.4, 2.4, 1.5, r, renkler[k % 4])
    for x in (2.0, 24.0):
        lamba_klasik(g, x, 1.0, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_avm(rnd):
    """Şehir içi alışveriş merkezi: önde korunmuş eski cepheler (taş ve tuğla), ortada tam yükseklikte cam giriş, üstte delikli
    metal kaplı otopark katı ve çatıda arabalar, arkada spiral rampa, reklam panosu."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 28.0, 28.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    Ht, on1, *_ = paris(g, rnd, r, 1.0, 10.0, 1.4, 9.0, 4, "haussmann", ("on", "sol"), dukkan=True, tema="kafe", sagir=())
    mansart(g, 1.0, 10.0, 1.4, 9.0, Ht, r, rnd, n=3)
    tente(g, on1, 0.3, 8.7, z0 + 2.9, 1.4, r, M("Tente", "#e0a020"))
    r2 = blok_renk(rnd, "tugla")
    r2["duvar"] = M("Duvar", "#b0483a")
    Ht2, on2, *_ = paris(g, rnd, r2, 18.0, 27.0, 1.4, 9.0, 4, "tugla", ("on", "sag"), dukkan=True, tema="genel", sagir=())
    tente(g, on2, 0.3, 8.7, z0 + 2.9, 1.2, r2, M("Tente", "#c9302c"), r2["beyaz"])
    kutu(g, 18.0, 27.0, 1.4, 9.0, Ht2, Ht2 + 0.5, r2["suslu"])
    # Cam giriş
    gx0, gx1 = 10.0, 18.0
    kutu(g, gx0, gx1, 1.4, 9.0, z0, z0 + 17.0, r["koyu"], atla=("zn",))
    gfc = Cephe((gx0, 1.4, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    cdortgen(g, gfc, 0.4, 7.6, 0.01, z0, z0 + 16.0, M("Vitrin_9fb8c6", "#9fb8c6"))
    for k in range(1, 5):
        ckutu(g, gfc, 0.4 + k * 1.44 - 0.05, 0.4 + k * 1.44 + 0.05, 0, 0.08, z0, z0 + 16.0, r["koyu"])
    for k in range(1, 5):
        ckutu(g, gfc, 0.4, 7.6, 0, 0.08, z0 + k * 3.4, z0 + k * 3.4 + 0.1, r["koyu"])
    tabela(g, gfc, 4.0, z0 + 16.5, 5.6, 0.7, 0.1, r, "GALERIES CITYRT", renk=r["koyu"], yazi_renk=M("Yazi", "#e8c35a"), oran=0.55)
    cam_kapi(g, gfc, 4.0, z0, r, w=2.4, h=3.0, kasa=r["koyu"], sacak=False)
    # Arkadaki büyük hacim: delikli metal kaplama ve çatı otoparkı
    kh = M("DelikliMetal", "#a0703a")
    bx0, bx1, by0, by1 = 1.0, 27.0, 9.0, 27.0
    Hb = z0 + 15.0
    kutu(g, bx0, bx1, by0, by1, z0, Hb, kh, atla=("zn",))
    for fc, L in zip(cepheler(bx0, bx1, by0, by1), (26.0, 18.0, 18.0, 26.0)):
        for k in range(int(L / 1.1)):
            u = 0.3 + k * 1.1
            ckutu(g, fc, u, u + 0.5, 0, 0.06, z0 + 0.5 + (k % 3) * 0.3, Hb - 0.4 - (k % 2) * 0.4, M("DelikliMetal2", "#8a5a2e"))
    kutu(g, 1.0, 10.0, 1.4, 9.0, Ht + 2.6, Hb, kh) if Ht + 2.6 < Hb else None
    kutu(g, 18.0, 27.0, 1.4, 9.0, Ht2 + 0.5, Hb, kh)
    kutu(g, bx0, bx1, 1.4, by1, Hb, Hb + 0.4, r["beton"])
    for k in range(5):
        boya(g, 2.0, 26.0, 4.0 + k * 4.4, 4.06 + k * 4.4, r["serit"], z=Hb + 0.41)
    for k in range(14):
        x = 2.8 + (k % 7) * 3.4
        y = 5.6 + (k // 7) * 8.8 + (0 if k % 2 else 2.0)
        if rnd.random() < 0.8:
            park_araba(g, x, y, math.pi / 2, araba_renk(rnd), r, Hb + 0.4)
    kutu(g, 9.0, 19.0, 11.0, 15.0, Hb + 0.4, Hb + 1.4, M("Vitrin_bcd3df", "#bcd3df"))
    for fc in cepheler(bx0, bx1, 1.4, by1):
        pass
    silindir(g, 22.5, 22.5, z0, Hb + 1.2, 4.0, 16, r["beton"])                # spiral rampa
    for k in range(5):
        silindir(g, 22.5, 22.5, z0 + 2.0 + k * 3.0, 0.3, 4.1, 16, r["suslu"])
    kutu(g, 26.9, 27.2, 14.0, 22.0, z0 + 7.0, z0 + 13.0, M("Pano", "#3f9a5a"))
    kutu(g, 0.8, 1.1, 14.0, 20.0, z0 + 6.0, z0 + 11.0, M("Yazi", "#ff4fa0"))
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_buyuk_magaza(rnd):
    """Büyük mağaza: kemerli vitrinler, köşelerde küçük kubbeler, atriyumun üstünde vitray kubbe çatı, mansart."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 26.0, 24.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 1.0, 25.0, 1.4, 22.6
    Ht, on, sag, sol, arka = paris(g, rnd, r, x0, x1, y0, y1, 5, "haussmann", ("on", "sag", "sol", "arka"), dukkan=False)
    for fc, L in ((on, x1 - x0), (sag, y1 - y0), (sol, y1 - y0)):
        n = int(L / 3.6)
        for k in range(n):
            u = L * (k + 0.5) / n
            kemer(g, fc, u, z0 + 0.2, 2.6, 3.0, 0.12, 0.16, M("Vitrin_8a9aa8", "#8a9aa8"))
            kemer(g, fc, u, z0 + 0.1, 2.9, 3.2, 0.0, 0.12, r["koyu"])
    tabela(g, on, (x1 - x0) / 2, z0 + 3.5, 8.0, 0.5, 0.12, r, "GRANDS MAGASINS", renk=r["koyu"], yazi_renk=M("Yazi", "#e8c35a"), oran=0.6)
    zt = mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=8)
    for cx, cy in ((x0 + 1.0, y0 + 1.0), (x1 - 1.0, y0 + 1.0)):
        top(g, (cx, cy, Ht + 0.4), 1.6, 1.6, 2.2, M("Kubbe", BLOK_CINKO))
        top(g, (cx, cy, Ht + 2.6), 0.25, 0.25, 0.5, M("Altin", "#d9a83a"))
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    silindir(g, cx, cy, zt - 0.4, 1.4, 5.4, 18, r["duvar"])
    renkler = ["#f2d08a", "#c8b8e0", "#a8d0d8", "#f0b8a0", "#d8e0a0"]
    for k, c in enumerate(renkler):
        rr = 5.5 - k * 1.05
        top(g, (cx, cy, zt + 1.0 + k * 0.55), rr, rr, 1.0 + (4 - k) * 0.35, M("Vitray", c))
    for k in range(4):
        a = math.pi / 2 * k
        kutu(g, cx + 5.6 * math.cos(a) - 0.6, cx + 5.6 * math.cos(a) + 0.6, cy + 5.6 * math.sin(a) - 0.6, cy + 5.6 * math.sin(a) + 0.6,
             zt - 0.4, zt + 1.4, r["suslu"])
    for x in (0.6, 25.4):
        lamba_klasik(g, x, 0.7, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_grand_otel(rnd):
    """Grand otel: ana girişin üstünde camlı saçak, bayraklar, kırmızı halı, boydan boya balkonlar, çatı pencereli mansart."""
    g = Govde()
    r = blok_renk(rnd, "haussmann")
    PW, PD = 22.0, 20.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, r["kaldirim"])
    z0 = EV_ZEMIN
    x0, x1, y0, y1 = 1.0, 21.0, 2.0, 18.6
    Ht, on, sag, sol, arka = paris(g, rnd, r, x0, x1, y0, y1, 8, "haussmann", ("on", "sag", "sol", "arka"), dukkan=False)
    L = x1 - x0
    cam_kapi(g, on, L / 2, z0, r, w=2.4, h=3.0, kasa=M("Altin", "#b8902a"), sacak=False)
    ckutu(g, on, L / 2 - 2.8, L / 2 + 2.8, 0, 1.9, z0 + 3.2, z0 + 3.35, M("SacakCam", "#9fb8c6"))
    ckutu(g, on, L / 2 - 2.85, L / 2 + 2.85, 0, 1.95, z0 + 3.35, z0 + 3.5, r["koyu"])
    for s in (-1, 1):
        p = on.P(L / 2 + s * 2.6, 1.8, 0.0)
        cubuk(g, (p[0], p[1], z0), (p[0], p[1], z0 + 3.2), 0.08, r["koyu"], kabuk=False)
    levha(g, x0 + L / 2 - 1.0, x0 + L / 2 + 1.0, 0.0, y0, z0, z0 + 0.03, M("Hali", "#a0242c"))
    tabela(g, on, L / 2, z0 + 4.2, 4.0, 0.6, 0.1, r, "GRAND HÔTEL", renk=r["koyu"], yazi_renk=M("Yazi", "#e8c35a"), oran=0.6)
    for k, c in enumerate(("#c9302c", "#2f5a9a", "#f6f3ec", "#2f6a46")):     # bayraklar
        u = 3.0 + k * 4.6
        if abs(u - L / 2) < 2.0:
            u += 2.2
        a, b = on.P(u, 0.0, z0 + 7.0), on.P(u, 1.6, z0 + 7.8)
        cubuk(g, a, b, 0.07, r["koyu"], kabuk=False)
        yonlu_kutu(g, add(b, (0.0, 0.0, -0.75)), (0.03, 0, 0), (0, 0.04, 0), (0, 0, 0.0) if False else (0, 0, 0.75), M("Bayrak", c))
    mansart(g, x0, x1, y0, y1, Ht, r, rnd, n=7)
    baca_sira(g, x0 + 5.0, (y0 + y1) / 2, Ht + 2.6, r, 4)
    baca_sira(g, x1 - 5.0, (y0 + y1) / 2, Ht + 2.6, r, 4)
    for x in (0.5, 21.5):
        lamba_klasik(g, x, 0.8, r)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_stadyum(rnd):
    """Mahalle stadyumu: çizgili çim saha, kaleler, reklam panoları, kapalı küçük tribün, skorbord, dört projektör direği,
    teraslı tuğla kulüp evi, otopark."""
    g = Govde()
    r = ar(rnd, "#a8503a", cati="#5a4a44")
    PW, PD = 30.0, 26.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Cim", "#86b05a"))
    z0 = EV_ZEMIN
    px0, px1, py0, py1 = 2.0, 23.0, 2.5, 16.5
    for k in range(7):
        x = px0 + k * 3.0
        levha(g, x, x + 1.5, py0, py1, z0, z0 + 0.03, M("Saha", "#9cc46a"))
    b = M("Cizgi", "#f6f3ec")
    zz = z0 + 0.04
    for (a0, a1, c0, c1) in ((px0, px1, py0, py0 + 0.1), (px0, px1, py1 - 0.1, py1), (px0, px0 + 0.1, py0, py1), (px1 - 0.1, px1, py0, py1),
                             ((px0 + px1) / 2 - 0.05, (px0 + px1) / 2 + 0.05, py0, py1)):
        boya(g, a0, a1, c0, c1, b, z=zz)
    for s in (px0, px1):
        d = 1 if s == px0 else -1
        boya(g, min(s, s + d * 3.0), max(s, s + d * 3.0), 6.0, 6.1, b, z=zz)
        boya(g, min(s, s + d * 3.0), max(s, s + d * 3.0), 12.9, 13.0, b, z=zz)
        boya(g, s + d * 3.0 - 0.05, s + d * 3.0 + 0.05, 6.0, 13.0, b, z=zz)
        for y in (8.0, 11.0):
            cubuk(g, (s, y, z0), (s, y, z0 + 2.0), 0.1, b)
        cubuk(g, (s, 8.0, z0 + 2.0), (s, 11.0, z0 + 2.0), 0.1, b)
    renkler = [M("Reklam", c) for c in ("#c9302c", "#2f5a9a", "#e0a020", "#2f6a46")]
    for k in range(7):
        kutu(g, px0 + k * 3.0, px0 + k * 3.0 + 2.8, py0 - 0.6, py0 - 0.45, z0, z0 + 0.8, renkler[k % 4])
    for (a, c) in (((0.6, 0.6), (24.4, 0.6)), ((0.6, 0.6), (0.6, 18.0)), ((24.4, 0.6), (24.4, 18.0))):
        cit_hat(g, a[0], a[1], c[0], c[1], z0, r["koyu"], h=2.2, aralik=2.0)
    # Tribün
    for k in range(4):
        kutu(g, 6.0, 20.0, 17.2 + k * 0.6, 17.8 + k * 0.6, z0, z0 + 0.5 + k * 0.5, r["beton"])
    for x in (6.0, 13.0, 20.0):
        cubuk(g, (x, 19.6, z0), (x, 19.6, z0 + 4.0), 0.18, r["metal"])
    yonlu_kutu(g, (13.0, 18.4, z0 + 4.0), (7.4, 0, 0), (0, 1.8, 0.4), (0, 0, 0.08), M("Cati", "#5a6470"))
    for k in range(10):
        top(g, (7.0 + k * 1.3, 17.8 + (k % 3) * 0.6, z0 + 1.2 + (k % 3) * 0.5), 0.2, 0.2, 0.35, renkler[k % 4])
    kutu(g, 21.0, 23.5, 18.0, 18.3, z0 + 2.0, z0 + 3.4, r["koyu"])           # skorbord
    for (x, y) in ((1.2, 1.2), (23.8, 1.2), (1.2, 17.4), (23.8, 17.4)):
        cubuk(g, (x, y, z0), (x, y, z0 + 13.0), 0.25, r["metal"])
        kutu(g, x - 0.8, x + 0.8, y - 0.2, y + 0.2, z0 + 13.0, z0 + 14.0, M("Projektor", "#f3e3a0"))
    # Kulüp evi
    kx0, kx1, ky0, ky1 = 2.0, 15.0, 20.4, 25.4
    kutu(g, kx0, kx1, ky0, ky1, z0, z0 + 3.6, r["duvar"], atla=("zn",))
    kfc = Cephe((kx0, ky0, 0.0), (1.0, 0.0, 0.0), (0.0, -1.0, 0.0))
    for k in range(5):
        pencere(g, kfc, 1.4 + k * 2.6, z0 + 0.9, 1.1, 1.5, r, sade=True)
    kalca_cati(g, kx0, kx1, ky0, ky1, z0 + 3.6, 0.5, r["cati"], sacak=0.4)
    for k in range(3):
        masa_semsiye(g, 16.5 + k * 1.8 if False else 4.0 + k * 4.0, 19.4 if False else 26.0 - 0.0, r, M("Semsiye", "#f6f3ec")) if False else None
    # Otopark
    levha(g, 25.0, 30.0, 0.0, 18.0, z0, z0 + 0.04, r["asfalt"])
    for k in range(5):
        boya(g, 25.2, 29.8, 2.0 + k * 3.2, 2.06 + k * 3.2, r["serit"], z=z0 + 0.05)
        if rnd.random() < 0.8:
            park_araba(g, 27.5, 3.6 + k * 3.2, 0.0, araba_renk(rnd), r, z0)
    for k in range(5):
        bisiklet(g, 17.0 + k * 0.6, 21.0, math.pi / 2, r, rnd)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


def ref_bahce_merkezi(rnd):
    """Bahçe merkezi: beyaz Venlo serası (gece parlar), file altında çiçekli saksı rafları, küvetlerde küçük ağaçlar, toprak
    çuvalları, kapıda kasa kulübesi ve çiçek logolu tabela."""
    g = Govde()
    r = ar(rnd, "#f6f3ec", ana="#2f6a46")
    PW, PD = 28.0, 24.0
    levha(g, 0, PW, 0, PD, 0, EV_ZEMIN, M("Kaldirim", "#d8d2c6"))
    z0 = EV_ZEMIN
    gx0, gx1, gy0, gy1 = 11.0, 27.0, 6.0, 22.0
    cam = M("Vitrin_e8eef2", "#e8eef2")
    kutu(g, gx0, gx1, gy0, gy1, z0, z0 + 4.0, cam, atla=("zn",))
    for k in range(4):
        a, b = gx0 + k * 4.0, gx0 + (k + 1) * 4.0
        pts = [(a, gy0, z0 + 4.0), (b, gy0, z0 + 4.0), (b, gy1, z0 + 4.0), (a, gy1, z0 + 4.0), ((a + b) / 2, gy0, z0 + 5.8), ((a + b) / 2, gy1, z0 + 5.8)]
        dis_bukey(g, pts, [[0, 1, 2, 3], [0, 1, 4], [3, 2, 5], [0, 4, 5, 3], [1, 2, 5, 4]], cam, kabuk=False)
        cubuk(g, ((a + b) / 2, gy0, z0 + 5.8), ((a + b) / 2, gy1, z0 + 5.8), 0.08, r["beyaz"], kabuk=False)
    for fc, L in zip(cepheler(gx0, gx1, gy0, gy1), (16.0, 16.0, 16.0, 16.0)):
        for k in range(9):
            ckutu(g, fc, k * 2.0 - 0.05, k * 2.0 + 0.05, 0, 0.06, z0, z0 + 4.0, r["beyaz"])
    ckutu(g, Cephe((gx0, gy0, 0.0), (1, 0, 0), (0, -1, 0)), 6.5, 9.0, 0, 0.05, z0, z0 + 2.8, r["beyaz"])
    # File altı raflar
    for x in (1.0, 9.0):
        for y in (3.0, 9.0, 15.0):
            cubuk(g, (x, y, z0), (x, y, z0 + 3.2), 0.12, r["koyu"], kabuk=False)
    for k in range(6):
        yonlu_kutu(g, (5.0, 3.0 + k * 2.4, z0 + 3.3), (4.2, 0, 0), (0, 0.5, 0), (0, 0, 0.04), M("File", "#3f7a4a"))
    for j in range(3):
        y = 4.5 + j * 4.0
        kutu(g, 1.5, 8.5, y - 0.5, y + 0.5, z0 + 0.6, z0 + 0.8, r["ahsap"])
        for k in range(12):
            top(g, (1.8 + k * 0.58, y, z0 + 1.0), 0.2, 0.2, 0.18, rnd.choice(r["cicek"] + [r["cali"]]))
    for k in range(9):                                                         # küvetlerde ağaçlar
        x = 11.5 + k * 1.6
        kutu(g, x - 0.35, x + 0.35, 3.6, 4.3, z0, z0 + 0.6, r["ahsap"])
        top(g, (x, 3.95, z0 + 1.3), 0.5, 0.5, 0.7, r["agac"][k % len(r["agac"])])
    kutu(g, 10.0, 26.0, 2.2, 2.8, z0, z0 + 0.8, r["cali"])
    for k in range(4):
        kutu(g, 2.0 + k * 0.8, 2.7 + k * 0.8, 19.5, 20.5, z0, z0 + 0.4, M("Cuval", "#8a6a4a"))
    kutu(g, 6.0, 8.0, 19.0, 21.0, z0, z0 + 2.4, r["ana"])                    # kasa kulübesi
    kutu(g, 5.8, 8.2, 18.8, 21.2, z0 + 2.4, z0 + 2.6, r["beyaz"])
    kutu(g, 1.0, 1.4, 1.0, 1.4, z0, z0 + 2.6, r["ana"])
    kutu(g, 0.4, 2.0, 1.0, 1.4, z0 + 2.6, z0 + 4.0, r["ana"])
    top(g, (1.2, 0.95, z0 + 3.3), 0.45, 0.06, 0.45, r["beyaz"])
    top(g, (1.2, 0.9, z0 + 3.3), 0.18, 0.06, 0.18, r["sari"])
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g))
    return g, ortala(g)


# ---------------- PARK VE AVLU KARELERİ (10 m)

def ref_park_metro(r, rnd):
    """Metro önü parkı: Art Nouveau metro girişi, Morris ilan sütunu, Wallace çeşmesi, fenerler, ağaç, bank, saksı."""
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, ZC, r["tas_acik"])
    yesil = M("Fener", "#2f4a3a")
    kutu(g, -1.8, 1.8, -2.6, 0.6, ZC - 0.6, ZC + 0.01, M("Merdiven", "#5a4a40"))
    for k in range(5):
        kutu(g, -1.6, 1.6, -2.4 + k * 0.6, -1.8 + k * 0.6, ZC - 0.6 + k * 0.12, ZC - 0.48 + k * 0.12, r["koyu"])
    for (a, b) in (((-1.9, -2.7), (-1.9, 0.7)), ((1.9, -2.7), (1.9, 0.7)), ((-1.9, 0.7), (1.9, 0.7))):
        cit_hat(g, a[0], a[1], b[0], b[1], ZC, yesil, h=1.0, aralik=0.3)
    for x in (-1.9, 1.9):
        cubuk(g, (x, -2.7, ZC), (x, -2.7, ZC + 2.6), 0.1, yesil)
        top(g, (x, -2.7, ZC + 2.7), 0.25, 0.25, 0.35, M("MetroLamba", "#f0a030"))
    yonlu_kutu(g, (0.0, -2.7, ZC + 2.3), (1.9, 0, 0), (0, 0.06, 0), (0, 0, 0.2), M("MetroTabela", "#e8c35a"))
    levha(g, -1.5, 2.0, 1.5, 4.5, ZC, ZC + 0.05, r["cim"])
    silindir(g, 0.2, 3.0, ZC, 2.6, 0.13, 6, r["govde"])
    for (dx, dy) in ((0, 0), (0.6, 0.3), (-0.5, 0.4), (0.1, -0.5)):
        top(g, (0.2 + dx, 3.0 + dy, ZC + 3.0), 0.75, 0.75, 0.6, r["agac"][1])
    silindir(g, 3.4, 1.8, ZC, 2.8, 0.7, 10, yesil)                             # Morris sütunu
    silindir(g, 3.4, 1.8, ZC + 0.6, 1.6, 0.72, 10, M("Afis", "#f2d08a"))
    top(g, (3.4, 1.8, ZC + 2.9), 0.78, 0.78, 0.6, yesil)
    for (dx, dy) in ((-0.3, -0.3), (0.3, -0.3), (-0.3, 0.3), (0.3, 0.3)):     # Wallace çeşmesi
        cubuk(g, (-3.4 + dx, 2.0 + dy, ZC + 0.4), (-3.4 + dx, 2.0 + dy, ZC + 1.6), 0.06, yesil)
    silindir(g, -3.4, 2.0, ZC, 0.4, 0.5, 8, yesil)
    top(g, (-3.4, 2.0, ZC + 1.8), 0.45, 0.45, 0.45, yesil)
    lamba_klasik(g, 3.6, -2.6, r, h=3.4, z0=ZC)
    bank(g, 2.4, 3.8, math.pi, r)
    silindir(g, -2.6, -4.0, ZC, 0.45, 0.3, 8, r["saksi"])
    top(g, (-2.6, -4.0, ZC + 0.7), 0.35, 0.35, 0.35, r["cali"])
    return g


def ref_otopark_cinar(r, rnd):
    """Çınar altı otopark: parke taşı, çit çalılar, kulübeli ve bariyerli giriş, fenerler, park etmiş arabalar."""
    g = Govde()
    zemin_levha(g, -YK, YK, -YK, YK, Z_ASF, M("ParkeTas", "#cfc9be"))
    for i in range(5):
        x = -4.0 + i * 2.15
        boya(g, x - 0.04, x + 0.04, 1.0, 4.4, r["serit"])
        boya(g, x - 0.04, x + 0.04, -2.4, 1.0 - 4.4 + 1.0 if False else -2.4 + 0.01, r["serit"]) if False else None
    for i in range(4):
        x = -2.9 + i * 2.15
        if rnd.random() < 0.85:
            park_araba(g, x, 2.7, math.pi / 2, araba_renk(rnd), r, Z_ASF)
    for i in range(3):
        x = -0.8 + i * 2.15
        boya(g, x - 1.07, x - 1.0, -4.4, -1.4, r["serit"])
        if rnd.random() < 0.8:
            park_araba(g, x, -2.9, math.pi / 2, araba_renk(rnd), r, Z_ASF)
    boya(g, -1.2, 0.4, -0.5, -0.4, r["serit"])
    boya(g, 1.0, 2.6, 0.4, 0.5, r["serit"])
    for (a0, a1, b0, b1) in ((-4.9, 4.9, 4.5, 4.95), (4.5, 4.95, -4.9, 4.5), (-4.9, -4.45, -4.9, -2.0), (-4.9, -4.45, 0.5, 4.5)):
        kutu(g, a0, a1, b0, b1, Z_ASF, Z_ASF + 0.7, r["cali"])
    kutu(g, -4.4, -3.0, -1.6, -0.2, Z_ASF, Z_ASF + 2.2, M("Kulube", "#3f73e6"))
    kutu(g, -4.5, -2.9, -1.7, -0.1, Z_ASF + 2.2, Z_ASF + 2.4, r["koyu"])
    cubuk(g, (-2.9, -0.4, Z_ASF + 0.9), (0.6, -0.4, Z_ASF + 0.9), 0.1, r["kirmizi"])
    for (x, y) in ((-4.6, 4.6), (4.6, -4.6)):
        lamba_klasik(g, x, y, r, h=3.6, z0=Z_ASF)
    for (x, y) in ((4.6, 4.6), (-4.6, -4.6)):
        top(g, (x, y, Z_ASF + 1.0), 0.6, 0.6, 0.7, r["agac"][0])
    return g


def ref_park_kanal(r, rnd):
    """Kanal köprüsü: tuğla rıhtım, su, ferforje korkuluklu köprü, bisikletler, yüzen ev, fenerler, basamaklar, ağaçlar."""
    g = Govde()
    tugla = M("TuglaZemin", "#b8634a")
    zemin_levha(g, -YK, YK, -YK, YK, ZC, tugla)
    su = M("Su", "#5fa8d8")
    kutu(g, -YK, YK, -1.6, 1.6, ZC - 0.6, ZC + 0.02, su, atla=("zn",))
    for y in (-1.6, 1.6):
        kutu(g, -YK, YK, y - 0.12, y + 0.12, ZC, ZC + 0.18, r["tas"])
    kutu(g, -1.6, 1.6, -2.0, 2.0, ZC + 0.18, ZC + 0.5, M("Kopru", "#a0482e"))
    for x in (-1.5, 1.5):
        cit_hat(g, x, -2.0, x, 2.0, ZC + 0.5, r["metal"], h=0.9, aralik=0.25)
    kutu(g, 2.0, 4.8, -1.2, 1.2, ZC - 0.2, ZC + 0.5, r["beyaz"])            # yüzen ev
    kutu(g, 2.4, 4.4, -1.0, 1.0, ZC + 0.5, ZC + 2.0, M("Duvar", "#7fae8f"))
    kutu(g, 2.3, 4.5, -1.1, 1.1, ZC + 2.0, ZC + 2.15, r["koyu"])
    cdortgen(g, Cephe((2.4, -1.0, 0.0), (1, 0, 0), (0, -1, 0)), 0.4, 1.6, 0.01, ZC + 0.9, ZC + 1.6, r["cam"])
    for k in range(4):
        bisiklet(g, -3.8 + k * 0.6, 3.0, math.pi / 2, r, rnd, z0=ZC)
    for k in range(3):
        bisiklet(g, -0.8 + k * 0.6, -3.4, math.pi / 2, r, rnd, z0=ZC)
    for (x, y) in ((-4.2, 2.4), (3.6, -2.4)):
        lamba_klasik(g, x, y, r, h=3.0, z0=ZC)
    for (x, y) in ((-3.8, -3.6), (3.8, 3.6)):
        silindir(g, x, y, ZC, 1.4, 0.1, 6, r["govde"])
        top(g, (x, y, ZC + 2.0), 0.8, 0.8, 0.7, r["agac"][0])
    for k in range(4):
        kutu(g, 2.0, YK, 3.6 + k * 0.35, 3.95 + k * 0.35, ZC, ZC + 0.12 + k * 0.12, r["tas"])
    return g


def referans():
    sonuc = []
    tekil = [("Konut_ref_kira", ref_kira), ("Konut_ref_kubbeli", ref_kubbeli), ("Konut_ref_faubourg", ref_faubourg),
             ("Konut_ref_altbau", ref_altbau), ("Konut_ref_pasajli", ref_pasajli), ("Konut_ref_eski_sehir", ref_eski_sehir),
             ("Konut_ref_mews", ref_mews), ("Konut_villa_kosk", ref_kosk), ("Konut_villa_artnouveau", ref_artnouveau),
             ("Konut_konak_bahceli", ref_konak_bahceli),
             ("Dukkan_cadde_firin", ref_firin), ("Dukkan_cadde_bistro", lambda rnd: ref_firin(rnd, bistro=True)),
             ("Dukkan_cadde_pub", ref_pub), ("Dukkan_cadde_eski", ref_eski_dukkanlar), ("Dukkan_cadde_bufe", ref_bufe),
             ("Dukkan_cadde_benzinlik", ref_benzinlik), ("Dukkan_cadde_market", ref_market), ("Dukkan_cadde_autohaus", ref_autohaus),
             ("Dukkan_cadde_fitness", ref_fitness), ("Buro_klinker_1", ref_buro_klinker),
             ("Sanayi_fabrika_matbaa", ref_matbaa), ("Sanayi_fabrika_lastik", ref_lastik), ("Sanayi_depo_insaat", ref_insaat_deposu),
             ("Sanayi_garaj_tamirhane", ref_tamirhane), ("Sanayi_garaj_atolye", ref_atolye),
             ("Simge_opera", ref_opera), ("Simge_gar", ref_gar), ("Simge_kapali_pazar", ref_kapali_pazar),
             ("Simge_pazar_pavyonu", ref_pazar_pavyonu), ("Simge_avm", ref_avm), ("Simge_buyuk_magaza", ref_buyuk_magaza),
             ("Simge_grand_otel", ref_grand_otel), ("Simge_stadyum", ref_stadyum), ("Simge_bahce_merkezi", ref_bahce_merkezi)]
    for i, (ad, fn) in enumerate(tekil):
        rnd = random.Random(TOHUM * 4243 + i * 97)
        g, gen = fn(rnd)
        sonuc.append((ad, g, gen))
    r = ar(random.Random(TOHUM), "#e8e4dc")
    rnd = random.Random(TOHUM * 919)
    for ad, fn in (("Park_cim_metro", ref_park_metro), ("Avlu_otopark_cinar", ref_otopark_cinar), ("Park_cim_kanal", ref_park_kanal)):
        g = fn(r, rnd)
        if CIZGI:
            kabuk(g, CIZGI_KALINLIK * 0.7)
        sonuc.append((ad, g, ortala(g)))
    return sonuc


# ------------------------------------------------------------------ hava: bulut, yağmur, kar, yaprak, şimşek (Unity'de Hava3D kullanır)

def bulut(rnd, tip, koyu):
    """Alçak poligon bulut. tip: "kume" (kabarık, yüksek), "yassi" (uzun, alçak), "kucuk" (tek iki top),
    "firtina" (koyu, büyük, tepesi örs gibi yayvan). Ölçü metre; Unity kendi boyuna ölçekler ve her birini ayrıca esnetir."""
    g = Govde()
    renk = M("BulutKoyu", "#8f959e") if koyu else M("Bulut", "#f6f7f9")
    golge = M("BulutAlt", "#6f757e") if koyu else M("BulutAlti", "#dfe4ea")
    if tip == "yassi":
        L, oran, n, yuk = rnd.uniform(30.0, 40.0), rnd.uniform(0.32, 0.42), rnd.randint(7, 9), 0.5
    elif tip == "kucuk":
        L, oran, n, yuk = rnd.uniform(9.0, 13.0), rnd.uniform(0.55, 0.7), rnd.randint(2, 3), 0.85
    elif tip == "firtina":
        L, oran, n, yuk = rnd.uniform(28.0, 36.0), rnd.uniform(0.55, 0.65), rnd.randint(6, 8), 1.0
    elif tip == "kule":
        L, oran, n, yuk = rnd.uniform(14.0, 18.0), rnd.uniform(0.65, 0.78), rnd.randint(3, 4), 1.3
    elif tip == "dagnik":
        # Dağınık küçük topaklar: tek gövde değil, aralıklı 4–6 küçük bulutçuk
        g2 = Govde()
        for _ in range(rnd.randint(4, 6)):
            x, y = rnd.uniform(-14.0, 14.0), rnd.uniform(-6.0, 6.0)
            rr = rnd.uniform(2.2, 3.8)
            top(g2, (x, y, rr * 0.5), rr * 1.4, rr * 1.1, rr * 0.6, renk)
            top(g2, (x + rr * 0.6, y + 0.3, rr * 0.75), rr * 0.8, rr * 0.7, rr * 0.55, renk)
        if CIZGI:
            kabuk(g2, CIZGI_KALINLIK * olcek(g2) * 0.8)
        return g2, ortala(g2)
    elif tip == "ince":
        L, oran, n, yuk = rnd.uniform(36.0, 46.0), rnd.uniform(0.18, 0.24), rnd.randint(8, 10), 0.35
    else:  # kume
        L, oran, n, yuk = rnd.uniform(18.0, 26.0), rnd.uniform(0.5, 0.62), rnd.randint(4, 6), 1.15
    D = L * oran
    # Taban: yassı geniş top (alt yüzü hafif koyu: güneşte gölgeli taraf)
    top(g, (0.0, 0.0, D * 0.1), L * 0.5, D * 0.48, D * 0.13 * (0.7 if tip == "yassi" else 1.0), golge)
    for i in range(n):
        u = (i + 0.5) / n - 0.5
        x = u * L * 0.82 + rnd.uniform(-1.0, 1.0)
        y = rnd.uniform(-D * 0.2, D * 0.2)
        r = (D * 0.42) * (1.0 - abs(u) * 0.85) * rnd.uniform(0.75, 1.2)
        top(g, (x, y, D * 0.1 + r * 0.4 * yuk), r * 1.2, r, r * 0.68 * yuk, renk)
    # Ortada kabarık tepeler (kümede yüksek, yassıda yok)
    tepe = {"kume": 2, "firtina": 2, "kucuk": 1, "yassi": 0, "kule": 3, "ince": 0}[tip]
    for _ in range(tepe):
        x = rnd.uniform(-L * 0.15, L * 0.15)
        r = D * rnd.uniform(0.36, 0.46)
        top(g, (x, rnd.uniform(-1.0, 1.0), D * 0.22 * yuk + r * 0.45 * yuk), r * 1.2, r * 0.95, r * 0.8 * yuk, renk)
    if tip == "firtina":
        # Örs: tepede yayvan, düz üstlü koyu katman
        zt = D * 0.22 + D * 0.5
        top(g, (rnd.uniform(-2.0, 2.0), 0.0, zt), L * 0.42, D * 0.42, D * 0.12, renk)
    if CIZGI:
        kabuk(g, CIZGI_KALINLIK * olcek(g) * 0.8)
    return g, ortala(g)


def damla():
    """Yağmur damlası çizgisi: ince, uzun, 6 yüzlü (1 m boy; Unity ölçekler)."""
    g = Govde()
    mat = M("Yagmur", "#b9d4f0")
    n = 6
    pts = []
    for z, rr in ((0.0, 0.0), (0.12, 0.045), (0.75, 0.03), (1.0, 0.0)):
        if rr == 0.0:
            pts.append((0.0, 0.0, z))
        else:
            pts += [(rr * math.cos(2 * math.pi * i / n), rr * math.sin(2 * math.pi * i / n), z) for i in range(n)]
    # 0: alt uç, 1..6: alt halka, 7..12: üst halka, 13: üst uç
    f = [[0, 1 + (i + 1) % n, 1 + i] for i in range(n)]
    f += [[1 + i, 1 + (i + 1) % n, 7 + (i + 1) % n, 7 + i] for i in range(n)]
    f += [[7 + i, 7 + (i + 1) % n, 13] for i in range(n)]
    dis_bukey(g, pts, f, mat, kabuk=False)
    return g, ortala(g)


def sicrama():
    """Yere düşen damlanın sıçrama halkası (taç): alçak, açık mavi."""
    g = Govde()
    mat = M("Sicrama", "#d6e6f8")
    n = 10
    for i in range(n):
        a0, a1 = 2 * math.pi * i / n, 2 * math.pi * (i + 0.55) / n
        p = [(0.5 * math.cos(a0), 0.5 * math.sin(a0), 0.0), (0.5 * math.cos(a1), 0.5 * math.sin(a1), 0.0),
             (0.62 * math.cos(a1), 0.62 * math.sin(a1), 0.18), (0.62 * math.cos(a0), 0.62 * math.sin(a0), 0.22)]
        yonlu = [p[0], p[1], p[2], p[3]]
        ic = [(q[0] * 0.85, q[1] * 0.85, q[2]) for q in yonlu]
        dis_bukey(g, yonlu + ic, [[0, 1, 2, 3], [7, 6, 5, 4], [0, 4, 5, 1], [1, 5, 6, 2], [2, 6, 7, 3], [3, 7, 4, 0]], mat, kabuk=False)
    return g, ortala(g)


def kar_tanesi():
    """Altı kollu yassı kar tanesi (1 m çap)."""
    g = Govde()
    mat = M("Kar", "#ffffff")
    for k in range(3):
        a = k * math.pi / 3
        d = (math.cos(a), math.sin(a), 0.0)
        yonlu_kutu(g, (0.0, 0.0, 0.0), mul(d, 0.5), (-d[1] * 0.06, d[0] * 0.06, 0.0), (0.0, 0.0, 0.03), mat, kabuk=False)
        for s in (-1, 1):
            m = mul(d, 0.3 * s)
            for e in (-1, 1):
                b = (math.cos(a + e * 0.6), math.sin(a + e * 0.6), 0.0)
                yonlu_kutu(g, add(m, mul(b, 0.08 * s)), mul(b, 0.1 * s), (-b[1] * 0.04, b[0] * 0.04, 0.0), (0.0, 0.0, 0.025), mat, kabuk=False)
    return g, ortala(g)


def yaprak(renk):
    """Rüzgârda savrulan yaprak: hafif kıvrık, ortası damarlı."""
    g = Govde()
    mat = M("Yaprak", renk)
    damar = M("YaprakDamar", koyu_ton(renk, 0.7))
    pts = [(0.0, -0.5, 0.0), (0.22, -0.25, 0.04), (0.26, 0.05, 0.06), (0.15, 0.32, 0.04), (0.0, 0.5, 0.0),
           (-0.15, 0.32, 0.04), (-0.26, 0.05, 0.06), (-0.22, -0.25, 0.04)]
    ust = [(p[0], p[1], p[2] + 0.02) for p in pts]
    n = len(pts)
    dis_bukey(g, pts + ust, [list(range(n))[::-1], list(range(n, 2 * n))] + [[i, (i + 1) % n, n + (i + 1) % n, n + i] for i in range(n)], mat, kabuk=False)
    yonlu_kutu(g, (0.0, -0.05, 0.05), (0.0, 0.5, 0.0), (0.015, 0.0, 0.0), (0.0, 0.0, 0.012), damar, kabuk=False)
    return g, ortala(g)


def simsek():
    """Şimşek: zikzak, parlak sarı-beyaz yassı prizma (10 m boy; Unity ölçekler)."""
    g = Govde()
    mat = M("Simsek", "#fff6c2")
    cizgi = [(0.0, 10.0), (0.9, 7.6), (-0.2, 6.9), (1.1, 4.4), (0.1, 3.9), (1.0, 1.4), (0.5, 0.0)]
    for (x0, z0), (x1, z1) in zip(cizgi, cizgi[1:]):
        c = ((x0 + x1) / 2, 0.0, (z0 + z1) / 2)
        ax = ((x1 - x0) / 2, 0.0, (z1 - z0) / 2)
        yonlu_kutu(g, c, ax, (0.18, 0.0, 0.0), (0.0, 0.12, 0.0), mat, kabuk=False)
    return g, ortala(g)


def hava_parcalari():
    sonuc = []
    beyaz = ["kume", "kume", "kume", "yassi", "yassi", "kucuk", "kucuk", "kule", "kule", "dagnik", "dagnik", "ince"]
    for i, tip in enumerate(beyaz):
        rnd = random.Random(TOHUM * 31 + i)
        g, gen = bulut(rnd, tip, koyu=False)
        sonuc.append((f"Hava_bulut_{i + 1}", g, gen))
    for i, tip in enumerate(["firtina", "firtina", "kume"]):
        rnd = random.Random(TOHUM * 57 + i)
        g, gen = bulut(rnd, tip, koyu=True)
        sonuc.append((f"Hava_firtina_{i + 1}", g, gen))
    sonuc.append(("Hava_damla",) + damla())
    sonuc.append(("Hava_sicrama",) + sicrama())
    sonuc.append(("Hava_kar",) + kar_tanesi())
    for i, renk in enumerate(("#7aa043", "#d9952e", "#b5552f")):
        sonuc.append((f"Hava_yaprak_{i + 1}",) + yaprak(renk))
    sonuc.append(("Hava_simsek",) + simsek())
    return sonuc


def uret():
    if MODEL == "sehir":
        return sehir_parcalari()
    if MODEL == "katalog":
        return katalog()
    if MODEL == "hava":
        return hava_parcalari()
    if MODEL == "kir":
        return kir_alanlari()
    if MODEL == "blok":
        return bloklar()
    if MODEL == "referans":
        return referans()
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
