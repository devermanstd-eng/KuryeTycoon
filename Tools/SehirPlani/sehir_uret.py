"""
Kurye Tycoon — prosedürel, tohumlu "Londra tipi" şehir planı üretici (plan sürümü 2).

Bir görüntüye bağlı değildir (eski üretici: plan_uret.py). Ürettiği dünya:
  • Swan River: şehrin içinden kıvrılarak geçer (batıdan doğuya genişler), şehir dışında da akar; 7 köprü.
  • Büyük şehir (u65: 144 × 134 kare, ~10 500 kentsel kare): köylerden birleşmiş. Ana ışınsalların üstünde mahalle merkezleri
    (her birinin çarşı caddesi, kilise sokağı, küçük taş meydanı, sıkışık eski dokusu); şehir lekesi buralarda dışarı taşar.
    Kuzey yaka: bahçeli meydanlı teraslar, set, büyük park (City Park, çevresinde Park Lane), doğuda Docklands ve kuleler.
    Güney yaka: boydan boya rıhtım / ambar şeridi (Bankside), sık işçi sokakları, doğuda sanayi.
    Demiryolu: kuzeydoğudaki terminalden (Swan Cross) kıra uzanan hat ve bir banliyö durağı (yol değil: 'D' şeridi + çizgi).
    Bir caddeye yaslanan parklar, iki mezarlık ('G'), okullar ve üniversite kampüsü ('C').
  • Yol hiyerarşisi: merkezden ışınsal çıkan eğri ana yollar (eski köy yolları), iç halka, ara yay (Crescent), dış halka
    (Circular), çevre caddesi (Orbital), nehir kıyısı caddeleri;
    aralarındaki adalarda mahalle sokakları (kılavuz caddeye paralel sıralar + şaşırtmalı kesen sokaklar: uzun teras blokları),
    merkezde küçük bloklar ve dar ara sokaklar (mews), bahçeli meydanlar (square), dış halkada kıvrımlı sokaklar, en dışta
    çıkmazlı banliyö. Sınıflar: 1 ana · 2 cadde · 3 sokak · 4 ara.
  • Bölgeler: merkez (eski şehir + çarşı caddeleri + birkaç kule), iç halka teraslar, dış halka bahçeli evler, banliyö,
    nehir boyunca liman / sanayi, kampüs, büyük park ve meydan bahçeleri; şehir lekesinin dışı kır (plan dikdörtgeninin içinde de).
  • Kırsal: ana yolların devamı ve köy yolları, düzensiz (Voronoi) tarla çokgenleri, çiftlikler, köyler, yol boyu evler.

Çıktılar:
  Resources/Sehir/plan.json        oyunun okuduğu plan (SehirPlani.cs). Eski alanların hepsi durur; yeni alanlar aşağıda.
  Tools/SehirPlani/onizleme.png    tepeden önizleme (bölgeler, yol sınıfları, nehir, parklar, tarlalar).

plan.json alanları (1 kare = 1 dünya birimi ≈ 10 m; x doğuya, y kuzeye artar; kare (x, y)'nin merkezi tam sayıda):
  surum, tohum, W, H, merkez, meydan, belediye, otogar
  yollar[]      {genis, sinif (1 ana, 2 cadde, 3 sokak, 4 ara), ad, x[], y[]}      şehir içi yol çizgileri
  su[], park[]  {x[], y[]}                                                          nehir ve park çokgenleri
  kare[]        satır başına metin: Y yol · K köprü · S su · P park · . yapı arsası / kır
  zone[]        O eski şehir (teraslar) · M çarşı · H kule · E bahçeli ev · B banliyö evi · C kampüs / okul · W sanayi/liman ·
                A AVM/otogar · P park · Z meydan · R nehir kıyısı · G mezarlık · D demiryolu şeridi · . kır
  bolge[]       semt 1–5 (kır kareleri 1: kilitlenmez);  yogunluk[]  0–9 yapı yoğunluğu
  kirYollari[]  {genis, sinif, x[], y[]}        şehir dışındaki yollar (kare değil: kurye gitmez)
  kirNehir[]    {x[], y[], g[]}                 nehrin şehir dışındaki kolları (orta çizgi + genişlik)
  tarlalar[]    {x[], y[], mx, my, gen, boy, aci, tur}   dışbükey tarla çokgeni (saat yönünün tersi) ve içine sığan
                                                          eğik dikdörtgen (merkez, boyut, açı °: 3D parsel modeli buraya oturur)
  ciftlikler    {x[], y[]};   kirEvleri {x[], y[], aci[], tur[]}  (tur 0 yol boyu ev, 1 köy evi, 2 köy kilisesi)
  demiryolu[]   {x[], y[]}                      hat (terminalden kıra); yol değil, kurye gitmez
  mezarlik[]    {x[], y[]}                      mezarlık çokgenleri
  yerler[]      {ad, x, y, tur, aci}            adı yazan yerler: tur 0 mahalle merkezi, 1 istasyon (aci: hattın yönü °), 2 mezarlık,
                                                3 park, 4 okul / kampüs

Kullanım:  python3 sehir_uret.py [tohum=20261127] [--yazma]     (--yazma: plan.json'a dokunmaz, yalnız önizleme /tmp'ye)
Gerekenler: numpy, scipy, pillow.  Ölçüm: python3 plan_olc.py
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from scipy.spatial import cKDTree, Voronoi

BURA = os.path.dirname(os.path.abspath(__file__))
ARG = [a for a in sys.argv[1:] if not a.startswith("--")]
TOHUM = int(ARG[0]) if ARG else 20261127
YAZMA = "--yazma" in sys.argv
CIKTI = os.path.join(BURA, "..", "..", "Resources", "Sehir", "plan.json")
ONIZLEME = os.path.join(BURA, "onizleme.png") if not YAZMA else "/tmp/onizleme_deneme.png"

rs = np.random.RandomState(TOHUM)
W, H = 144, 134                # plan dikdörtgeni (kare)
R1, RM, R2, R3 = 16.5, 25.0, 33.5, 49.0     # halkalar (kare): iç halka, ara yay, dış halka (Circular), çevre caddesi (Orbital)
PAD, F = 50, 4                 # çalışma alanı: dikdörtgenin PAD kare ötesine kadar; ince raster: kare başına F piksel
GW, GH = (W + 2 * PAD) * F, (H + 2 * PAD) * F
ANA, CADDE, SOKAK, ARA = 1, 2, 3, 4


# ====================================================================== yardımcılar
def gur(n=4, olcek=1.0):
    """Tohumlu yumuşak 1B gürültü (sinüs toplamı), yaklaşık −1 … 1."""
    a = rs.uniform(0.5, 1.0, n) / (np.arange(n) + 1)
    f = rs.uniform(0.7, 1.3, n) * (np.arange(n) + 1) * olcek
    ph = rs.uniform(0, 2 * math.pi, n)
    s = a.sum()
    return lambda t: sum(a[i] * np.sin(f[i] * t + ph[i]) for i in range(n)) / s


def gur_aci(n=4):
    """Açıya göre (2π dönemli) yumuşak gürültü."""
    a = rs.uniform(0.5, 1.0, n) / (np.arange(n) + 1)
    k = np.arange(n) + 1 + rs.randint(0, 2, n)
    ph = rs.uniform(0, 2 * math.pi, n)
    s = a.sum()
    return lambda t: sum(a[i] * np.sin(k[i] * t + ph[i]) for i in range(n)) / s


def uzunluk(p):
    p = np.asarray(p, float)
    return float(np.hypot(*np.diff(p, axis=0).T).sum()) if len(p) > 1 else 0.0


def yeniden(p, adim):
    """Çizgiyi eşit aralıklı noktalarla yeniden örnekler."""
    p = np.asarray(p, float)
    if len(p) < 2:
        return p
    d = np.hypot(*np.diff(p, axis=0).T)
    s = np.concatenate([[0], np.cumsum(d)])
    if s[-1] < 1e-6:
        return p[:1]
    n = max(2, int(round(s[-1] / adim)) + 1)
    t = np.linspace(0, s[-1], n)
    return np.stack([np.interp(t, s, p[:, 0]), np.interp(t, s, p[:, 1])], 1)


def catmull(p, k=12):
    p = np.asarray(p, float)
    q = np.vstack([2 * p[0] - p[1], p, 2 * p[-1] - p[-2]])
    out = []
    for i in range(1, len(q) - 2):
        p0, p1, p2, p3 = q[i - 1], q[i], q[i + 1], q[i + 2]
        for t in np.linspace(0, 1, k, endpoint=False):
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    out.append(p[-1])
    return np.array(out)


def yumusat(p, tur=2):
    """Chaikin köşe yumuşatma (uçlar sabit)."""
    p = np.asarray(p, float)
    for _ in range(tur):
        if len(p) < 3:
            break
        q = [p[0]]
        for a, b in zip(p[:-1], p[1:]):
            q.append(a * 0.75 + b * 0.25)
            q.append(a * 0.25 + b * 0.75)
        q.append(p[-1])
        p = np.array(q)
    return p


def teget_normal(p, pencere=6):
    """Noktalardaki birim teğet ve sol normal (pencere: yumuşatma, nokta sayısı)."""
    p = np.asarray(p, float)
    n = len(p)
    i0 = np.clip(np.arange(n) - pencere, 0, n - 1)
    i1 = np.clip(np.arange(n) + pencere, 0, n - 1)
    t = p[i1] - p[i0]
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-9)[:, None]
    return t, np.stack([-t[:, 1], t[:, 0]], 1)


def rdp(p, eps):
    p = np.asarray(p, float)
    if len(p) < 3:
        return p
    tut = np.zeros(len(p), bool)
    tut[0] = tut[-1] = True
    yigin = [(0, len(p) - 1)]
    while yigin:
        a, b = yigin.pop()
        if b <= a + 1:
            continue
        v = p[b] - p[a]
        L = math.hypot(*v) + 1e-12
        d = np.abs(v[0] * (p[a + 1:b, 1] - p[a, 1]) - v[1] * (p[a + 1:b, 0] - p[a, 0])) / L
        i = int(np.argmax(d))
        if d[i] > eps:
            k = a + 1 + i
            tut[k] = True
            yigin += [(a, k), (k, b)]
    return p[tut]


def rdp_kapali(p, eps):
    """Kapalı çokgeni (ilk nokta tekrarlanmadan) sadeleştirir: en uzak iki köşeden ikiye bölüp her yarıyı ayrı."""
    p = np.asarray(p, float)
    if len(p) < 4:
        return p
    k = int(np.argmax(np.hypot(p[:, 0] - p[0, 0], p[:, 1] - p[0, 1])))
    a = rdp(p[:k + 1], eps)
    b = rdp(np.vstack([p[k:], p[:1]]), eps)
    return np.vstack([a[:-1], b[:-1]])


def fpx(p):
    """Dünya → ince raster (sütun, satır) tam sayı; sınırda kırpılır."""
    p = np.asarray(p, float)
    c = np.clip(np.rint((p[..., 0] + PAD) * F).astype(int), 0, GW - 1)
    r = np.clip(np.rint((p[..., 1] + PAD) * F).astype(int), 0, GH - 1)
    return c, r


def ornek(alan, p):
    """İnce rasterdeki alanın dünya noktalarındaki değeri."""
    c, r = fpx(p)
    return alan[r, c]


def ciz(pts, kalin=1, deger=255, im=None):
    """Çizgiyi ince rastere çizer (PIL)."""
    yeni = im is None
    if yeni:
        im = Image.new("I", (GW, GH), 0)
    if len(pts) >= 2:
        ImageDraw.Draw(im).line([((x + PAD) * F, (y + PAD) * F) for x, y in pts], fill=int(deger), width=kalin)
    return im


def ic_nokta(poly, p):
    """Nokta dışbükey (saat yönünün tersi) çokgenin içinde mi; dönen: kenarlara en küçük içeri uzaklık (eksi = dışarıda)."""
    poly = np.asarray(poly, float)
    a, b = poly, np.roll(poly, -1, axis=0)
    e = b - a
    L = np.maximum(np.hypot(e[:, 0], e[:, 1]), 1e-9)
    d = (e[:, 0] * (p[1] - a[:, 1]) - e[:, 1] * (p[0] - a[:, 0])) / L
    return float(d.min())


def alan_poly(poly):
    poly = np.asarray(poly, float)
    x, y = poly[:, 0], poly[:, 1]
    return 0.5 * float(np.sum(x * np.roll(y, -1) - np.roll(x, -1) * y))


def yari_duzlem_kes(poly, a, n, d):
    """Dışbükey çokgeni n·(p − a) ≥ d yarı düzlemiyle keser (Sutherland–Hodgman)."""
    out = []
    m = len(poly)
    for i in range(m):
        p, q = poly[i], poly[(i + 1) % m]
        fp = (p[0] - a[0]) * n[0] + (p[1] - a[1]) * n[1] - d
        fq = (q[0] - a[0]) * n[0] + (q[1] - a[1]) * n[1] - d
        if fp >= 0:
            out.append(p)
        if (fp >= 0) != (fq >= 0):
            t = fp / (fp - fq)
            out.append((p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t))
    return out


def kosular(gecerli, enaz=10, bosluk=2):
    """Mantıksal dizideki True koşuları [(bas, son)] (kısa boşluklar kapatılır)."""
    g = np.array(gecerli, bool)
    n = len(g)
    i = 0
    while i < n:
        if not g[i]:
            j = i
            while j < n and not g[j]:
                j += 1
            if 0 < i and j < n and j - i <= bosluk:
                g[i:j] = True
            i = j
        else:
            i += 1
    out, bas = [], None
    for i in range(n + 1):
        if i < n and g[i]:
            if bas is None:
                bas = i
        elif bas is not None:
            if i - bas >= enaz:
                out.append((bas, i - 1))
            bas = None
    return out


class Yol:
    def __init__(self, pts, sinif, tur="", ad=""):
        self.pts = np.asarray(pts, float)
        self.sinif, self.tur, self.ad = sinif, tur, ad
        self.cikmaz = False


# ====================================================================== 1) nehir
def nehir_uret():
    j = lambda a: rs.uniform(-a, a)
    kp = [(-PAD + 2, .56 + j(.05)), (-30, .49 + j(.05)), (-13, .57 + j(.04)), (0.02 * W, .525 + j(.02)), (.17 * W, .47 + j(.02)),
          (.31 * W, .39 + j(.012)), (.46 * W, .375 + j(.008)), (.60 * W, .425 + j(.012)), (.74 * W, .40 + j(.02)),
          (.88 * W, .315 + j(.02)), (1.0 * W, .30 + j(.02)), (W + 14, .36 + j(.04)), (W + 30, .27 + j(.05)), (W + PAD - 2, .33 + j(.05))]
    c = yeniden(catmull([(x, (y + 0.07) * H) for x, y in kp], 16), 0.25)
    g = 3.6 + 1.7 * np.clip(c[:, 0] / W, 0, 1) + 0.9 * np.clip(np.maximum(-c[:, 0], c[:, 0] - W) / 30.0, 0, 1)
    return c, g


NEHIR, NEHIR_G = nehir_uret()
NT, NN = teget_normal(NEHIR, 8)
_nk = cKDTree(NEHIR)
YY, XX = np.mgrid[0:GH, 0:GW]
WX, WY = XX / F - PAD, YY / F - PAD
_d, _i = _nk.query(np.stack([WX[::2, ::2].ravel(), WY[::2, ::2].ravel()], 1))
sh2 = WX[::2, ::2].shape
NEHIR_UZAK = np.kron(_d.reshape(sh2), np.ones((2, 2)))[:GH, :GW]            # orta çizgiye uzaklık (kare)
_gen = np.kron(NEHIR_G[_i].reshape(sh2), np.ones((2, 2)))[:GH, :GW]
KIYI_UZAK = NEHIR_UZAK - _gen / 2                                            # kıyıya uzaklık (su içinde eksi)
# hangi yaka: +1 kuzey (orta çizginin solu), −1 güney
_yan = ((WX[::2, ::2].ravel() - NEHIR[_i, 0]) * NN[_i, 0] + (WY[::2, ::2].ravel() - NEHIR[_i, 1]) * NN[_i, 1])
YAKA = np.kron(np.sign(_yan).reshape(sh2), np.ones((2, 2)))[:GH, :GW]
SU_F = KIYI_UZAK < 0


def nehir_noktasi(x):
    """x'e en yakın orta çizgi noktasının indisi."""
    return int(np.argmin(np.abs(NEHIR[:, 0] - x)))


# ====================================================================== 2) merkez, iskelet
MX = int(round(W * 0.48))
_mi = nehir_noktasi(MX)
MY = int(round(NEHIR[_mi, 1] + NEHIR_G[_mi] / 2 + 6.5))
MERKEZ = np.array([MX, MY], float)             # Clock Tower Square
NM = NEHIR[_mi].copy()                          # halkaların merkezi: nehrin merkez hizasındaki noktası
iskelet = []                                    # Yol listesi (şehir içi + dışarı uzayan ham çizgiler)
HALKA_TAM = {}                                  # halka adı → kırpılmamış çizgi (halkanın içi maskesi için)


def kopru(i, pay=1.3):
    """Orta çizginin i. noktasından nehri dik geçen köprü: (güney ucu, kuzey ucu)."""
    yari = NEHIR_G[i] / 2 + pay
    return NEHIR[i] - NN[i] * yari, NEHIR[i] + NN[i] * yari


def isinsal(bas, aci, sinif, tur, menzil=140.0, sapma=0.5, merkez=None):
    """bas noktasından aci (rad) yönünde dışarı doğru kıvrılarak giden yol (eski köy yolu gibi)."""
    merkez = MERKEZ if merkez is None else merkez
    g = gur(3, 1.0)
    p = np.array(bas, float)
    h = aci
    pts = [p.copy()]
    s = 0.0
    while s < menzil:
        dis = math.atan2(p[1] - merkez[1], p[0] - merkez[0])
        r = math.hypot(*(p - merkez))
        hedef = dis + sapma * g(s * 0.055) * min(1.0, r / 14.0)
        fark = (hedef - h + math.pi) % (2 * math.pi) - math.pi
        h += np.clip(fark, -0.05, 0.05) * (0.5 if r > 6 else 0.15)
        p = p + np.array([math.cos(h), math.sin(h)]) * 0.5
        s += 0.5
        # nehre yaklaşma: kıyıdan uzak dur (dışarıda nehri geçmesin)
        ky = ornek(KIYI_UZAK, p)
        if ky < 3.0 and s > 4:
            yn = ornek(YAKA, p)
            i = _nk.query(p)[1]
            h_kacis = math.atan2(NN[i, 1] * yn, NN[i, 0] * yn)
            fark = (h_kacis - h + math.pi) % (2 * math.pi) - math.pi
            h += np.clip(fark, -0.12, 0.12)
        if not (-PAD + 2 < p[0] < W + PAD - 2 and -PAD + 2 < p[1] < H + PAD - 2):
            break
        pts.append(p.copy())
    return Yol(yeniden(pts, 0.25), sinif, tur)


def halka(r0, a0, a1, sinif, tur, oynama=0.11, merkez=None):
    """merkez çevresinde a0→a1 (rad) yayı; yarıçap açıya göre hafif oynar (tam daire olmasın)."""
    merkez = NM if merkez is None else merkez
    g = gur_aci(3)
    aci = np.linspace(a0, a1, max(8, int(abs(a1 - a0) * r0 / 0.5)))
    r = r0 * (1 + oynama * g(aci))
    return Yol(yeniden(np.stack([merkez[0] + r * np.cos(aci), merkez[1] + r * np.sin(aci)], 1), 0.25), sinif, tur)


def iskelet_kur():
    # --- merkez meydanı: 3×3 meydan, çevresinde halka yol
    k = 2
    kare = [(MX - k, MY - k), (MX + k, MY - k), (MX + k, MY + k), (MX - k, MY + k), (MX - k, MY - k)]
    iskelet.append(Yol(yeniden(kare, 0.25), CADDE, "meydan", "Clock Tower Square"))

    # --- köprüler: merkez + halkaların nehri kestiği yerler
    ki = {"m": nehir_noktasi(MX)}
    for ad, dx in (("b1", -R1), ("d1", R1), ("b2", -R2), ("d2", R2), ("b3", -R3), ("d3", R3)):
        ki[ad] = nehir_noktasi(MX + dx)
    kp = {a: kopru(i) for a, i in ki.items()}
    adlar = {"m": "Clock Tower Bridge", "b1": "Abbey Bridge", "d1": "Tower Bridge", "b2": "West Harbour Bridge", "d2": "Docklands Bridge",
             "b3": "Kew Bridge", "d3": "Woolwich Bridge"}
    for a, (g_, k_) in kp.items():
        iskelet.append(Yol(yeniden([g_, k_], 0.25), ANA if a in ("m", "b2", "d2") else CADDE, "kopru", adlar[a]))

    # --- merkezden köprüye ve güney kavşağına
    gm, km = kp["m"]
    iskelet.append(Yol(yeniden(yumusat([(MX, MY - k), (MX, MY - k - 1.5), km + NN[ki["m"]] * 1.0, km], 2), 0.25), ANA, "isin", "Bridge Street"))
    kavsak = gm - NN[ki["m"]] * 4.2
    iskelet.append(Yol(yeniden([gm, kavsak], 0.25), ANA, "isin", "Borough High Street"))

    # --- ışınsal ana yollar (kuzey yaka: meydandan; güney yaka: köprü başı kavşağından)
    d2r = math.pi / 180
    kuzey = [((MX + k, MY), 8, "Docklands Road"), ((MX + k, MY + k), 47, "Northgate Road"), ((MX, MY + k), 92, "Green Hill Road"),
             ((MX - k, MY + k), 137, "Abbey Road"), ((MX - k, MY), 176, "West End Road")]
    for bas, a, ad in kuzey:
        y = isinsal(bas, (a + rs.uniform(-5, 5)) * d2r, ANA, "isin")
        y.ad = ad
        iskelet.append(y)
    for a, ad in ((222, "Harbour Road"), (268, "Old Kent Road"), (318, "Greenwich Road")):
        y = isinsal(kavsak, (a + rs.uniform(-5, 5)) * d2r, ANA, "isin", merkez=kavsak + np.array([0, 3.0]))
        y.ad = ad
        iskelet.append(y)

    HM1 = np.array([MX, (MY + NM[1]) / 2 + 0.5])           # iç halkanın merkezi: meydanla nehir arası
    # --- halkalar: tam daire değil; yarıçapı açıya göre belirgin oynayan, yol noktalarından geçen eğri (köprülerde nehri dik geçer)
    def yay(r, ustte, sinif, ad, oyna, basik=1.0):
        b, d = {R1: ("b1", "d1"), R2: ("b2", "d2"), R3: ("b3", "d3")}[r]
        ib, id_ = ki[b], ki[d]
        yon = 1.0 if ustte else -1.0
        ub, ud = (kp[b][1], kp[d][1]) if ustte else (kp[b][0], kp[d][0])
        hm = HM1 if r == R1 else NM
        ab = math.atan2(ub[1] - hm[1], ub[0] - hm[0])
        ad_ = math.atan2(ud[1] - hm[1], ud[0] - hm[0])
        if ustte:                       # batı (≈ +π) → kuzeyden → doğu (≈ 0)
            ab = ab + 2 * math.pi if ab < 0 else ab
            ad_ = ad_ + 2 * math.pi if ad_ < -math.pi / 2 else ad_
        else:                           # batı (≈ −π) → güneyden → doğu (≈ 0)
            ab = ab - 2 * math.pi if ab > 0 else ab
            ad_ = ad_ - 2 * math.pi if ad_ > math.pi / 2 else ad_
        n = max(5, int(abs(ad_ - ab) * r / 7.5))
        eps = rs.uniform(-oyna, oyna, n)
        eps = (eps + np.roll(eps, 1) * 0.5 + np.roll(eps, -1) * 0.5) / 1.6
        eps[0] *= 0.3
        eps[-1] *= 0.3
        nok = [ub, ub + NN[ib] * yon * 2.2]
        for j in range(1, n):
            a = ab + (ad_ - ab) * j / n
            rr = r * (1 + eps[j]) / math.sqrt(math.cos(a) ** 2 + (math.sin(a) / basik) ** 2)
            nok.append(hm + np.array([math.cos(a), math.sin(a)]) * rr)
        nok += [ud + NN[id_] * yon * 2.2, ud]
        y = Yol(yeniden(catmull(nok, 14), 0.25), sinif, "halka", ad)
        HALKA_TAM[ad] = y.pts.copy()
        return y

    iskelet.append(yay(R1, True, CADDE, "Inner Ring North", 0.20))
    iskelet.append(yay(R1, False, CADDE, "Inner Ring South", 0.16))
    iskelet.append(yay(R2, True, ANA, "North Circular", 0.17))
    iskelet.append(yay(R2, False, ANA, "South Circular", 0.13))
    iskelet.append(yay(R3, True, CADDE, "Orbital Avenue North", 0.11, 0.88))
    iskelet.append(yay(R3, False, CADDE, "Orbital Avenue South", 0.10, 0.86))

    # --- kıyı caddeleri: kuzey ve güney yakada, dış halka köprüleri arasında (liman kesiminde kıyıdan içeride)
    i0, i1 = ki["b3"], ki["d3"]
    for yaka, ad in ((1, "Victoria Embankment"), (-1, "Bankside")):
        ii = np.arange(i0, i1 + 1)
        t = (NEHIR[ii, 0] - MX) / R2
        # kuzey yaka: doğuda (akış aşağı) liman, yol kıyıdan 3 kare içeride; merkezde ve batıda kıyının hemen yanında (set)
        ofs = NEHIR_G[ii] / 2 + 1.3
        if yaka > 0:
            ofs = ofs + 2.2 * np.clip((t - 0.85) / 0.10, 0, 1)
        else:
            ofs = ofs + 2.2 * np.clip((np.abs(t) - 0.50) / 0.10, 0, 1)        # güney yaka: çekirdeğin dışında rıhtım ve ambarlar
        p = NEHIR[ii] + NN[ii] * (yaka * ofs)[:, None]
        iskelet.append(Yol(yeniden(p, 0.25), CADDE, "kiyi", ad))


iskelet_kur()

# Mahalle merkezleri: şehir, büyürken yuttuğu köylerden oluşur. Her biri bir ana ışınsalın üstünde; kendi çarşı caddesi
# (ışınsalın o kesimi + onu kesen cadde), küçük meydanı ve sıkışık eski dokusu olur; şehir lekesi buralarda dışarı taşar.
MAHALLE = []          # dict(ad, c, yon, yol_ad, r)
for _yol_ad, _ad, _r in (("Northgate Road", "Northgate", 47.0), ("Green Hill Road", "Green Hill Village", 49.0),
                         ("Abbey Road", "Abbey Wood", 47.0), ("West End Road", "Westbourne", 50.0),
                         ("Harbour Road", "Harbour Green", 45.0), ("Old Kent Road", "Kentish Green", 41.0),
                         ("Greenwich Road", "Greenwich", 47.0)):
    _y = [y for y in iskelet if y.ad == _yol_ad]
    if not _y:
        continue
    _p = _y[0].pts
    _uz = np.hypot(_p[:, 0] - NM[0], _p[:, 1] - NM[1])
    _k = np.nonzero(_uz >= _r)[0]
    if len(_k) == 0 or _k[0] < 8 or _k[0] > len(_p) - 9:
        continue
    _i = int(_k[0])
    if any(math.hypot(*(_p[_i] - m_["c"])) < 22 for m_ in MAHALLE):       # iki köy üst üste binmesin
        continue
    _t = _p[_i + 6] - _p[_i - 6]
    MAHALLE.append(dict(ad=_ad, c=_p[_i].copy(), yon=_t / math.hypot(*_t), yol_ad=_yol_ad, r=_r, g=gur_aci(3)))

# ====================================================================== 3) şehir lekesi (kentsel alan)
BM = np.array([MX + 1.0, (MY + NM[1]) / 2 + 1.0])
_ra = np.hypot((WX - BM[0]) / 1.10, (WY - BM[1]) / 0.93)
_th = np.arctan2(WY - BM[1], WX - BM[0])
_g1, _g2, _g3 = gur_aci(4), gur_aci(3), gur_aci(5)
_isin = np.zeros((GH, GW), bool)
for y in iskelet:
    if y.tur == "isin":
        _isin |= np.array(ciz(y.pts)) > 0
ISIN_UZAK = ndi.distance_transform_edt(~_isin) / F
RMIN = 55.5 * (1 + 0.10 * _g1(_th))
RMAX = 64.5 * (1 + 0.05 * _g2(_th))
KENT_F = (_ra < RMIN) | ((_ra < RMAX) & (ISIN_UZAK < 5.0 + 2.0 * _g3(_th)))
for _m in MAHALLE:
    _mth = np.arctan2(WY - _m["c"][1], WX - _m["c"][0])
    KENT_F |= np.hypot(WX - _m["c"][0], WY - _m["c"][1]) < 9.0 * (1 + 0.22 * _m["g"](_mth))
KENT_F &= (WX > 1.6) & (WX < W - 2.6) & (WY > 1.6) & (WY < H - 2.6)
KENT_F = ndi.binary_closing(KENT_F, iterations=6)
KENT_F = ndi.binary_opening(KENT_F, iterations=8)
_e, _n = ndi.label(KENT_F | SU_F)
if _n > 1:      # en büyük parça (nehirle birlikte tek leke)
    boy = ndi.sum(KENT_F, _e, range(1, _n + 1))
    KENT_F &= _e == (int(np.argmax(boy)) + 1)
KENT_UZAK = ndi.distance_transform_edt(KENT_F) / F            # leke kenarına içeriden uzaklık
KENT_DIS = ndi.distance_transform_edt(~KENT_F) / F           # lekeye dışarıdan uzaklık


def kentte(p):
    return ornek(KENT_F, p)


# İkincil ışınsallar ve dış yay parçaları (lekeyi bilince)
def ikincil_kur():
    d2r = math.pi / 180
    # ana ışınsalların arasına cadde: iç halkanın hemen dışından başlar
    for a, r0, ad in ((28, R1 + 2.0, "Mile End Road"), (70, R1 + 1.5, "Camden Road"), (114, R1 + 2.0, "Highgate Road"), (157, R1 + 1.5, "Kilburn Lane"),
                      (196, R2 + 2.0, "Riverside West"), (-14, R2 + 1.5, "Wapping Lane")):
        aci = a * d2r
        bas = MERKEZ + np.array([math.cos(aci), math.sin(aci)]) * r0
        if ornek(KIYI_UZAK, bas) < 2.5 or ornek(YAKA, bas) < 0:
            continue
        y = isinsal(bas, aci + rs.uniform(-0.08, 0.08), CADDE, "isin2", sapma=0.7)
        y.ad = ad
        iskelet.append(y)
    gk = kopru(nehir_noktasi(MX))[0] - NN[nehir_noktasi(MX)] * 4.2
    for a, r0, ad in ((245, 10.5, "Camberwell Road"), (293, 10.5, "Deptford Road")):
        aci = a * d2r
        bas = gk + np.array([math.cos(aci), math.sin(aci)]) * r0
        y = isinsal(bas, aci + rs.uniform(-0.08, 0.08), CADDE, "isin2", sapma=0.7, merkez=gk + np.array([0, 3.0]))
        y.ad = ad
        iskelet.append(y)
    # güney yakanın iki ucunda dış halkadan başlayan caddeler
    for a, ad in ((203, "Wandsworth Road"), (338, "Woolwich Road")):
        aci = a * d2r
        bas = NM + np.array([math.cos(aci), math.sin(aci)]) * (R2 + 2.0)
        if ornek(KIYI_UZAK, bas) < 3.0 or ornek(YAKA, bas) > 0 or not kentte(bas):
            continue
        y = isinsal(bas, aci + rs.uniform(-0.06, 0.06), CADDE, "isin2", sapma=0.6, merkez=NM)
        y.ad = ad
        iskelet.append(y)
    # ara yay (RM): iç halkayla dış halka arasında; lekenin içinde ve kıyıdan uzak kesimler (Crescent)
    g = gur_aci(3)
    aci = np.linspace(-math.pi, math.pi, 1200)
    r = RM * (1 + 0.07 * g(aci))
    p = np.stack([NM[0] + r * np.cos(aci), NM[1] + 2.0 + r * np.sin(aci)], 1)
    ic = (ornek(KENT_UZAK, p) > 2.4) & (ornek(KIYI_UZAK, p) > 2.5)
    bas = None
    for i in range(len(p) + 1):
        if i < len(p) and ic[i]:
            if bas is None:
                bas = i
        elif bas is not None:
            if i - bas > 60:
                kuzeyde = ornek(YAKA, p[(bas + i) // 2]) > 0
                iskelet.append(Yol(yeniden(p[bas:i], 0.25), CADDE, "yay", "Queens Crescent" if kuzeyde else "Kings Crescent"))
            bas = None
    # mahalle merkezlerinin kesen caddesi (köyün kilise sokağı): ışınsala dik, iki yana 8–13 kare, hafif eğri
    for m in MAHALLE:
        c, yon = m["c"], m["yon"]
        if not kentte(c):
            continue
        nrm = np.array([-yon[1], yon[0]])
        L1, L2 = rs.uniform(8, 13), rs.uniform(8, 13)
        egri = rs.uniform(-0.012, 0.012)
        t = np.arange(-L1, L2 + 0.01, 0.25)
        p = c[None] + nrm[None] * t[:, None] + yon[None] * (egri * t * t)[:, None]
        gec = kentte(p) & (ornek(KIYI_UZAK, p) > 2.5) & (ornek(KENT_UZAK, p) > 1.2)
        orta = int(np.argmin(np.abs(t)))
        ks = [k_ for k_ in kosular(gec, enaz=16, bosluk=0) if k_[0] <= orta <= k_[1]]
        if not ks:
            continue
        iskelet.append(Yol(p[ks[0][0]:ks[0][1] + 1], CADDE, "koy", m["ad"].split()[0] + " Church Street"))
        m["kesen"] = iskelet[-1]


ikincil_kur()


def iskelet_kirp():
    """Şehir içi iskelet: lekenin içindeki kesimler. Dışarıda kalan ışınsal uzantıları kır yolu olur. İkincil yollar ve yaylar
    başka bir iskelet yoluna değdikleri yerde başlar / biter (sarkan uç kalmaz)."""
    sehir, kirlar = [], []
    for y in iskelet:
        ic = kentte(y.pts)
        if y.tur in ("isin", "isin2"):
            # merkezden dışarı: lekeden ilk çıkışa kadar şehir, sonrası kır
            cik = np.nonzero(~ic)[0]
            # leke içinde başlamalı; başlamıyorsa (ikincil başlangıcı dışarıda) atla
            if not ic[0]:
                gir = np.nonzero(ic)[0]
                if len(gir) == 0:
                    continue
                y.pts = y.pts[gir[0]:]
                ic = ic[gir[0]:]
                cik = np.nonzero(~ic)[0]
            k = cik[0] if len(cik) else len(y.pts)
            if k >= 12:
                s = Yol(y.pts[:k], y.sinif, y.tur, y.ad)
                s.cikis = k < len(y.pts)
                sehir.append(s)
            if k < len(y.pts) - 8:
                kirlar.append(Yol(y.pts[max(0, k - 1):], y.sinif, "kir_" + y.tur, y.ad))
        else:
            bas = None
            for i in range(len(ic) + 1):
                if i < len(ic) and ic[i]:
                    if bas is None:
                        bas = i
                elif bas is not None:
                    if i - bas >= 10:
                        sehir.append(Yol(y.pts[bas:i], y.sinif, y.tur, y.ad))
                    bas = None
    return sehir, kirlar


PARK_F = np.zeros((GH, GW), bool)         # büyük parkın içi (ince raster)


def buyuk_park():
    """City Park: kuzeybatıda iç halkayla dış halka arasında büyük park. Çevresini bir cadde dolanır (Park Lane); ışınsal ve
    yay caddeleri parkın kenarında biter (parkın içinden araç yolu geçmez)."""
    global PARK_F
    aci = math.radians(119 + rs.uniform(-4, 4))
    yon = np.array([math.cos(aci), math.sin(aci)])
    ic = HALKA_TAM["Inner Ring North"]
    dis = HALKA_TAM["North Circular"]

    def kesisim(pts):
        d = np.abs((pts[:, 0] - NM[0]) * (-yon[1]) + (pts[:, 1] - NM[1]) * yon[0])
        d[((pts - NM) @ yon) < 0] = 1e9
        return float(np.hypot(*(pts[int(np.argmin(d))] - NM)))

    r_ic, r_dis = kesisim(ic), kesisim(dis)
    b = float(np.clip((r_dis - r_ic) / 2 - 1.5, 5.0, 8.5))        # ışınsal yarı eksen
    a = float(np.clip(205.0 / (math.pi * b), 7.5, 12.5))          # teğet yarı eksen (alan ≈ 205 kare)
    c = NM + yon * (r_ic + r_dis) / 2
    tg = np.array([-yon[1], yon[0]])
    g = gur_aci(4)
    fi = np.linspace(0, 2 * math.pi, 400)
    rr = 1 + 0.09 * g(fi)
    p = c[None] + tg[None] * (a * rr * np.cos(fi))[:, None] + yon[None] * (b * rr * np.sin(fi))[:, None]
    p[-1] = p[0]
    im = Image.new("L", (GW, GH), 0)
    ImageDraw.Draw(im).polygon([((x + PAD) * F, (y_ + PAD) * F) for x, y_ in p], fill=255)
    PARK_F = np.array(im) > 0
    # parkın içinden geçen ışınsal / yay / köy caddeleri kenarda kesilir (iki örnek içeri taşar: çevre caddesine kare kare değsin)
    yeni = []
    for y in iskelet:
        if y.tur not in ("isin", "isin2", "yay", "koy"):
            yeni.append(y)
            continue
        icte = ornek(PARK_F, y.pts)
        if not icte.any():
            yeni.append(y)
            continue
        for (i0, i1) in kosular(~icte, enaz=12, bosluk=0):
            z = Yol(y.pts[max(0, i0 - 2):min(len(y.pts), i1 + 3)], y.sinif, y.tur, y.ad)
            yeni.append(z)
    iskelet[:] = yeni
    iskelet.append(Yol(yeniden(p, 0.25), CADDE, "park_cevre", "Park Lane"))
    return c


PARK_MERKEZ = buyuk_park()
SEHIR_ISK, KIR_ANA = iskelet_kirp()


def uclari_oturt(yollar):
    """İkincil ışınsalların başını ve yay / kıyı caddelerinin uçlarını en yakın başka iskelet yoluna bağlar."""
    for y in yollar:
        if y.tur not in ("isin2", "yay", "kiyi"):
            continue
        digerleri = np.vstack([o.pts for o in yollar if o is not y])
        kd = cKDTree(digerleri)
        for uc in ((0,) if y.tur == "isin2" else (0, -1)):
            p = y.pts[uc]
            d, i = kd.query(p)
            if d < 0.3:
                continue
            if d < 7.0:
                ek = yeniden([digerleri[i], p], 0.25)
                y.pts = np.vstack([ek, y.pts[1:]]) if uc == 0 else np.vstack([y.pts[:-1], ek[::-1]])
        # uçtan taşan kısa sarkıntıyı kes: başka yola değdiği son noktadan ötesi
        d, _ = kd.query(y.pts)
        deg = np.nonzero(d < 0.3)[0]
        if y.tur in ("yay", "kiyi") and len(deg) >= 2 and deg[-1] - deg[0] > 20:
            y.pts = y.pts[deg[0]:deg[-1] + 1]


uclari_oturt(SEHIR_ISK)

# ====================================================================== 3b) demiryolu
RAY = None             # hattın tamamı (terminalden kıra), 0,25 kare aralıklı noktalar
ISTASYON = []          # dict(ad, p, yon, terminal)


def demiryolu_kur():
    """Demiryolu: kuzeydoğuda ara yayın hemen dışındaki terminalden (Swan Cross) çıkar, iki ışınsal caddenin arasından şehri
    terk edip kıra uzanır. Yol değildir (kurye gitmez): planda ayrı çizgi, karelerde 'D' bölgesi (yapısız şerit). Mahalle
    sokakları hattı geçmez; yalnız iskelet caddeleri keser (hemzemin geçit)."""
    global RAY
    a = [y for y in SEHIR_ISK if y.ad == "Northgate Road"]
    b = [y for y in SEHIR_ISK if y.ad == "Camden Road"]
    if not a or not b:
        return

    def nokta(y, r):
        uz = np.hypot(y.pts[:, 0] - NM[0], y.pts[:, 1] - NM[1])
        k = np.nonzero(uz >= r)[0]
        return y.pts[int(k[0])] if len(k) else y.pts[-1]

    r0 = RM * 1.07 + 3.6
    orta = (nokta(a[0], r0) + nokta(b[0], r0)) / 2
    yon = (orta - NM) / math.hypot(*(orta - NM))
    bas = NM + yon * r0
    y = isinsal(bas, math.atan2(yon[1], yon[0]), 9, "ray", menzil=170.0, sapma=0.16, merkez=NM)
    p = y.pts
    son = np.nonzero(ornek(DIS_KENAR, p) > 43)[0]        # oyundaki kır zemini dikdörtgenin 44 kare ötesinde biter
    RAY = p[:int(son[0])] if len(son) else p
    t, _ = teget_normal(RAY, 8)
    ISTASYON.append(dict(ad="Swan Cross Station", p=RAY[0].copy(), yon=t[0].copy(), terminal=True))
    # banliyö durağı: hattın çevre caddesini (Orbital Avenue) geçtiği yerin hemen dışı
    orb = HALKA_TAM.get("Orbital Avenue North")
    if orb is not None:
        d, _ = cKDTree(orb).query(RAY)
        i = int(np.argmin(d))
        if d[i] < 1.0 and i + 14 < len(RAY) and kentte(RAY[i + 14]):
            ISTASYON.append(dict(ad="Northgate Halt", p=RAY[i + 14].copy(), yon=t[i + 14].copy(), terminal=False))


DIS_KENAR = np.maximum(np.maximum(-0.5 - WX, WX - (W - 0.5)), np.maximum(-0.5 - WY, WY - (H - 0.5)))      # dikdörtgenin dışına uzaklık
demiryolu_kur()
if RAY is not None:
    RAY_UZAK = ndi.distance_transform_edt(np.array(ciz(RAY)) == 0) / F
else:
    RAY_UZAK = np.full((GH, GW), 99.0)
# istasyon alanları (gar binası, peronlar): mahalle sokakları ve oyuk alanlar buraya girmez
ISTASYON_F = np.zeros((GH, GW), bool)
for _i in ISTASYON:
    _u = (WX - _i["p"][0]) * _i["yon"][0] + (WY - _i["p"][1]) * _i["yon"][1]
    _v = np.abs(-(WX - _i["p"][0]) * _i["yon"][1] + (WY - _i["p"][1]) * _i["yon"][0])
    ISTASYON_F |= ((_u > -1.3) & (_u < 5.2) & (_v < 1.1)) if _i["terminal"] else ((np.abs(_u) < 2.4) & (_v < 0.95))
RAY_UZAK = np.minimum(RAY_UZAK, ndi.distance_transform_edt(~ISTASYON_F) / F + 0.5) if ISTASYON_F.any() else RAY_UZAK

# iskelet rasterleri
ISK_NO = Image.new("I", (GW, GH), 0)
for no, y in enumerate(SEHIR_ISK):
    ciz(y.pts, 3, no + 1, ISK_NO)
ISK_NO = np.array(ISK_NO)
ISK_INCE = Image.new("I", (GW, GH), 0)
for no, y in enumerate(SEHIR_ISK):
    ciz(y.pts, 1, no + 1, ISK_INCE)
ISK_INCE = np.array(ISK_INCE)
ISK_UZAK = ndi.distance_transform_edt(ISK_INCE == 0) / F

# iç ve dış halkanın içi (ince raster maskeleri)
def halka_ici(anahtar):
    k = [y for y in SEHIR_ISK if y.tur == "halka" and anahtar in y.ad]
    if len(k) < 2:
        return _ra < (15 if anahtar == "Inner" else 28)
    kuz = [y for y in k if "North" in y.ad][0].pts
    gun = [y for y in k if "South" in y.ad][0].pts
    poly = np.vstack([kuz, gun[::-1]])
    im = Image.new("L", (GW, GH), 0)
    ImageDraw.Draw(im).polygon([((x + PAD) * F, (y_ + PAD) * F) for x, y_ in poly], fill=255)
    return np.array(im) > 0


IC1 = halka_ici("Inner")
IC2 = halka_ici("Circular")

IC3_POLY = np.vstack([HALKA_TAM["Orbital Avenue North"], HALKA_TAM["Orbital Avenue South"][::-1]])
_im = Image.new("L", (GW, GH), 0)
ImageDraw.Draw(_im).polygon([((x + PAD) * F, (y_ + PAD) * F) for x, y_ in IC3_POLY], fill=255)
IC3 = np.array(_im) > 0                   # çevre caddesinin (Orbital) içi

# ====================================================================== 3c) oyuk alanlar: parklar, mezarlık, okullar, kampüs
# Büyük adaların içinden bir iskelet caddesine yaslanan alanlar ayrılır (kendi adaları olur): mahalle sokakları buralara girmez.
OYUK = []                                  # dict(tur, ad, maske, c)
OYUK_F = np.zeros((GH, GW), bool)
OYUK_YAKIN = np.zeros((GH, GW), bool)      # oyuk alanların 3 kare çevresi (yenisi buraya konmaz)


def oyuk_ekle(tur, ad, maske):
    global OYUK_F, OYUK_YAKIN
    ys, xs = np.nonzero(maske)
    c = np.array([xs.mean() / F - PAD, ys.mean() / F - PAD])
    OYUK.append(dict(tur=tur, ad=ad, maske=maske, c=c))
    OYUK_F |= maske
    r0, r1, c0, c1 = max(0, ys.min() - 14), ys.max() + 15, max(0, xs.min() - 14), xs.max() + 15
    OYUK_YAKIN[r0:r1, c0:c1] |= ndi.binary_dilation(maske[r0:r1, c0:c1], iterations=12)


def oyuk_yerlestir(tur, ad, kosul, L, D, us=3.2):
    """Bir iskelet caddesine yaslanan alan: cadde boyunca L, caddeden içeri D kare (yuvarlatılmış dikdörtgen; us büyüdükçe köşeli).
    Leke kenarından, kıyıdan, demiryolundan, öbür oyuk alanlardan ve başka iskelet yollarından uzak ilk uygun yer seçilir."""
    aday = []
    for y in SEHIR_ISK:
        if y.tur in ("kopru", "meydan", "park_cevre"):
            continue
        t, nr = teget_normal(y.pts, 8)
        for i in range(10, len(y.pts) - 10, 5):
            for k in (1, -1):
                c = y.pts[i] + nr[i] * k * (D / 2 + 0.45)
                if kosul(c):
                    aday.append((t[i], nr[i] * k, c))
    yari = int((max(L, D) / 2 + 1.5) * F)
    for j in rs.permutation(len(aday))[:500]:
        tg, n, c = aday[j]
        cc, rr = fpx(c)
        r0, r1, c0, c1 = max(0, rr - yari), min(GH, rr + yari + 1), max(0, cc - yari), min(GW, cc + yari + 1)
        u = (WX[r0:r1, c0:c1] - c[0]) * tg[0] + (WY[r0:r1, c0:c1] - c[1]) * tg[1]
        v = (WX[r0:r1, c0:c1] - c[0]) * n[0] + (WY[r0:r1, c0:c1] - c[1]) * n[1]
        m = (np.abs(u) / (L / 2)) ** us + (np.abs(v) / (D / 2)) ** us < 1.0
        if m.sum() < L * D * F * F * 0.6:
            continue
        if (KENT_UZAK[r0:r1, c0:c1][m] < 2.6).any() or (KIYI_UZAK[r0:r1, c0:c1][m] < 3.0).any() or (RAY_UZAK[r0:r1, c0:c1][m] < 1.7).any():
            continue
        if OYUK_YAKIN[r0:r1, c0:c1][m].any() or PARK_F[r0:r1, c0:c1][m].any():
            continue
        ic = m & (v > -D / 2 + 1.3)                 # yaslandığı caddenin kenar şeridi dışında başka iskelet yolu olmasın
        if (ISK_UZAK[r0:r1, c0:c1][ic] < 1.1).any():
            continue
        tam = np.zeros((GH, GW), bool)
        tam[r0:r1, c0:c1] = m
        oyuk_ekle(tur, ad, tam)
        return True
    return False


def oyuklari_kur():
    def bil(c):
        d = c - MERKEZ
        return (math.degrees(math.atan2(d[1], d[0])) % 360, math.hypot(*(c - NM)), ornek(YAKA, c), ornek(IC1, c), ornek(IC2, c), ornek(IC3, c))

    # büyük park (çevre caddesinin içi; caddenin kendisi iskelette)
    if PARK_F.any():
        oyuk_ekle("park", "City Park", ndi.binary_erosion(PARK_F, iterations=2))
    K = lambda f: (lambda c: f(*bil(c)))
    # (tür, ad, koşul(açı°, nehir merkezine uzaklık, yaka, iç halkada, dış halkada, çevre caddesinde), boy, derinlik, köşelilik)
    istek = [
        ("kampus", "Swan University", K(lambda a, r, yk, i1, i2, i3: yk > 0 and (a < 75 or a > 345) and not i1 and 21 < r < 46), 10.5, 7.5, 3.6),
        ("park", "Riverside Common", K(lambda a, r, yk, i1, i2, i3: yk < 0 and i2 and not i1), 12.0, 7.0, 2.6),
        ("park", "Northgate Fields", K(lambda a, r, yk, i1, i2, i3: yk > 0 and 18 < a < 84 and not i2 and i3), 11.0, 7.0, 2.6),
        ("park", "Westbourne Common", K(lambda a, r, yk, i1, i2, i3: yk > 0 and 138 < a < 186 and not i2), 12.5, 8.0, 2.4),
        ("park", "Greenwich Park", K(lambda a, r, yk, i1, i2, i3: yk < 0 and not i2 and a > 285), 13.0, 8.0, 2.4),
        ("park", "Kentish Fields", K(lambda a, r, yk, i1, i2, i3: yk < 0 and not i2 and 215 < a < 268), 10.5, 6.5, 2.6),
        ("park", "Abbey Gardens", K(lambda a, r, yk, i1, i2, i3: yk > 0 and i2 and not i1 and 20 < a < 100), 7.0, 5.0, 3.0),
        ("mezarlik", "Highgate Cemetery", K(lambda a, r, yk, i1, i2, i3: yk > 0 and 84 < a < 142 and not i2), 10.0, 6.5, 4.2),
        ("mezarlik", "Southfields Cemetery", K(lambda a, r, yk, i1, i2, i3: yk < 0 and not i2 and 215 < a < 330), 8.0, 5.5, 4.2),
        ("okul", "Green Hill School", K(lambda a, r, yk, i1, i2, i3: yk > 0 and 62 < a < 118 and not i2), 5.5, 4.5, 4.0),
        ("okul", "Abbey School", K(lambda a, r, yk, i1, i2, i3: yk > 0 and 120 < a < 176 and not i1 and r > 24), 5.5, 4.5, 4.0),
        ("okul", "Harbour School", K(lambda a, r, yk, i1, i2, i3: yk < 0 and not i1 and 190 < a < 255), 5.5, 4.5, 4.0),
        ("okul", "Greenwich Academy", K(lambda a, r, yk, i1, i2, i3: yk < 0 and not i2 and a > 275), 6.0, 4.5, 4.0),
        ("okul", "Mile End School", K(lambda a, r, yk, i1, i2, i3: yk > 0 and (a < 60) and not i1 and i3), 5.5, 4.5, 4.0),
    ]
    eksik = []
    for tur, ad, kosul, L, D, us in istek:
        if not oyuk_yerlestir(tur, ad, kosul, L, D, us):
            eksik.append(ad)
    return eksik


OYUK_EKSIK = oyuklari_kur()

# ====================================================================== 4) adalar (iskeletin böldüğü hücreler)
ENGEL = (ISK_NO > 0) | (KIYI_UZAK < 0.6) | ~KENT_F | (RAY_UZAK < 0.75)
ADA, ADA_N = ndi.label(~ENGEL & ~OYUK_F)
for _o in OYUK:
    _m = _o["maske"] & ~ENGEL
    if _m.sum() < 4 * F * F:
        _o["ada"] = 0
        continue
    ADA_N += 1
    ADA[_m] = ADA_N
    _o["ada"] = ADA_N
# engel: iskelet dışında (kıyı, leke kenarı, demiryolu) sokakların yaklaşmaması gereken yerler
SINIR_UZAK = ndi.distance_transform_edt(~((KIYI_UZAK < 0.6) | ~KENT_F | (RAY_UZAK < 0.5))) / F
ada_alan = ndi.sum(np.ones_like(ADA), ADA, range(1, ADA_N + 1)) / (F * F)
ada_mer = ndi.center_of_mass(np.ones_like(ADA), ADA, range(1, ADA_N + 1))
ada_mer = [np.array([c[1] / F - PAD, c[0] / F - PAD]) for c in ada_mer]


def ada_turleri():
    """Her adaya tür: cekirdek, teras (kuzey yaka), teras_g (güney yaka), ev, banliyo, sanayi, kampus, okul, park, mezarlik, bos."""
    tur = {}
    oyuk_ada = {o["ada"]: o for o in OYUK if o["ada"]}
    buyuk = None
    for k in range(1, ADA_N + 1):
        if k in oyuk_ada:
            tur[k] = oyuk_ada[k]["tur"]
            if oyuk_ada[k]["ad"] == "City Park":
                buyuk = k
            continue
        a, c = ada_alan[k - 1], ada_mer[k - 1]
        if a < 5:
            tur[k] = "bos"
            continue
        yaka = ornek(YAKA, c)
        ky = ornek(KIYI_UZAK, c)
        dogu = c[0] - MX
        if ornek(IC1, c):
            t = "cekirdek"
        elif ornek(IC2, c):
            t = "teras" if yaka > 0 else "teras_g"      # iki yakanın karakteri farklı: kuzey bahçeli meydanlar, güney sık işçi sokakları
        elif ornek(IC3, c):
            t = "ev"
        else:
            t = "banliyo"
        if math.hypot(*(c - MERKEZ)) < 1.2:
            t = "meydan"
        # nehir boyu liman / sanayi. Kuzey yaka: yalnız doğuda (akış aşağı) Docklands. Güney yaka: çekirdeğin dışında boydan boya
        # rıhtım ve ambar şeridi (Bankside), doğuda büyük sanayi alanı.
        if t != "cekirdek":
            if yaka > 0 and dogu > R2 - 4 and ky < 4.5:
                t = "sanayi"
            if yaka > 0 and dogu > R2 + 4 and ky < 13:
                t = "sanayi"
            if yaka < 0 and ky < 4.4 and abs(dogu) < R3 + 5:
                t = "sanayi"
            if yaka < 0 and dogu > R2 - 3 and ky < 10:
                t = "sanayi"
        tur[k] = t
    return tur, buyuk


ADA_TUR, BUYUK_PARK = ada_turleri()


# ====================================================================== 5) mahalle sokakları (adaların içi)
YEREL = []            # Yol: sokaklar, ara sokaklar, çıkmazlar
BAHCELER = []         # meydan bahçeleri (çokgen Nx2)
MEYDANCIK = []        # merkezdeki küçük taş meydanlar (çokgen Nx2)
CIKMAZ_UC = []        # meşru kör uçlar (çıkmaz sokak başları)
TUM_INCE = Image.fromarray((ISK_INCE > 0).astype(np.uint8) * 255)      # bütün yollar (ince çizgi)
TUM_UZAK = ISK_UZAK.copy()

# ada türüne göre: sıra aralıkları (kare; sırayla döner), kesen sokak aralığı (min, max), dalga genliği, ara sokak / geçit /
# meydan bahçesi olasılığı, "boş" sayılan yol uzaklığı (bundan uzak cep kalırsa yeni sıra açılır)
ADA_AYAR = {
    "cekirdek": dict(dar=0.45, sira=[4, 3, 3], kesen=(4, 6), dalga=0.0, ara=0.85, gecit=0.0, bahce=0.7, bos=2.5),
    "teras": dict(dar=0.2, sira=[3, 4, 3, 3], kesen=(7, 10), dalga=0.2, ara=0.8, gecit=0.5, bahce=0.85, bos=2.6),
    "teras_g": dict(dar=0.35, sira=[3, 3, 4], kesen=(6, 9), dalga=0.1, ara=0.5, gecit=0.7, bahce=0.25, bos=2.6),
    "ev": dict(dar=0.0, sira=[3], kesen=(9, 13), dalga=0.7, ara=0.0, gecit=0.0, bahce=0.0, bos=2.9),
    "banliyo": dict(dar=0.0, sira=[6], kesen=(11, 15), dalga=1.0, ara=0.0, gecit=0.0, bahce=0.0, bos=4.3),
    "sanayi": dict(dar=0.0, sira=[5, 4], kesen=(7, 10), dalga=0.0, ara=0.0, gecit=0.0, bahce=0.0, bos=3.6),
    "kampus": dict(dar=0.0, sira=[5], kesen=(7, 9), dalga=0.4, ara=0.0, gecit=0.0, bahce=0.0, bos=3.6),
}


def tum_guncelle():
    global TUM_UZAK
    TUM_UZAK = ndi.distance_transform_edt(np.array(TUM_INCE) == 0) / F


def yerel_ekle(pts, sinif, tur, cikmaz=False):
    pts = np.asarray(pts, float)
    if len(pts) < 2 or uzunluk(pts) < 0.9:
        return None
    y = Yol(pts, sinif, tur)
    y.cikmaz = cikmaz
    YEREL.append(y)
    ImageDraw.Draw(TUM_INCE).line([((x + PAD) * F, (y_ + PAD) * F) for x, y_ in pts], fill=255, width=1)
    return y


def yuru(p, yon, enfazla, maske, enaz=0.5):
    """p'den yon'da ilerler; bir yola değerse oraya kadarki noktaları döndürür, adadan çıkarsa / yol bulamazsa None."""
    pts = [np.array(p, float)]
    n = int(enfazla / 0.25)
    for k in range(1, n + 1):
        q = p + yon * (0.25 * k)
        c, r = fpx(q)
        pts.append(q)
        if k * 0.25 >= enaz and TUM_UZAK[r, c] < 0.3:
            return np.array(pts)
        if not maske[r, c] or SINIR_UZAK[r, c] < 0.8:
            return None
    return None


def kafes(k, tur, g, tut0, yan, maske, genis):
    """
    g kılavuz çizgisinin (0,25 kare aralıklı noktalar) `yan` tarafına (+1 sol, −1 sağ) paralel sıralar ve şaşırtmalı kesen
    sokaklar döşer. tut0: kılavuzun yol olan noktaları (iskelet yolunda hepsi; yeni açılan sırada geçerli kesim).
    """
    ay = ADA_AYAR[tur]
    n = len(g)
    tg, nr = teget_normal(g, 8)
    nr = nr * yan
    kd = cKDTree(g)
    s = np.arange(n) * 0.25
    dalga = ay["dalga"] * np.sin(s / rs.uniform(4.5, 7.5) + rs.uniform(0, 6.28)) if ay["dalga"] > 0 else np.zeros(n)

    # --- sıralar
    sira_pts, sira_tut, sira_d = [g], [np.array(tut0, bool)], [0.0]
    d = 0.0
    m = 0
    ofs = rs.randint(0, len(ay["sira"]))
    bos_sira = 0
    while m < 18:
        d += ay["sira"][(m + ofs) % len(ay["sira"])]
        p = g + nr * (d + dalga)[:, None]
        cc, rr = fpx(p)
        ic = maske[rr, cc]
        sik = np.hypot(*(p[np.minimum(np.arange(n) + 1, n - 1)] - p[np.maximum(np.arange(n) - 1, 0)]).T) / 0.5     # içbükeyde sıkışma
        gec = ic & (kd.query(p)[0] >= d + dalga - 0.45) & (sik > 0.45) & (ISK_UZAK[rr, cc] >= 1.9) & (SINIR_UZAK[rr, cc] >= 1.5) & \
            (TUM_UZAK[rr, cc] >= 2.2)
        tut = np.zeros(n, bool)
        for (i0, i1) in kosular(gec, enaz=10, bosluk=2):
            tut[i0:i1 + 1] = True
        sira_pts.append(p); sira_tut.append(tut); sira_d.append(d)
        m += 1
        bos_sira = 0 if tut.any() else bos_sira + 1
        if bos_sira >= 3 or d > 45:
            break
    M = len(sira_pts) - 1
    if not any(t.any() for t in sira_tut[1:]):
        M = 0
    eklenen = 0

    # --- meydan bahçesi (square): üç sıranın da sürdüğü bir kesimde ortadaki sıra kesilir, çevresi yol kalır
    bahce = None
    if ay["bahce"] > 0 and rs.rand() < ay["bahce"] and M >= 3:
        aday = []
        for m0 in range(0, M - 1):
            if sira_d[m0 + 2] - sira_d[m0] > 7.5:
                continue
            ucu = sira_tut[m0] & sira_tut[m0 + 1] & sira_tut[m0 + 2]
            for (i0, i1) in kosular(ucu, enaz=26, bosluk=0):
                aday.append((m0, i0, i1))
        if aday:
            m0, i0, i1 = aday[rs.randint(len(aday))]
            Ls = int(rs.randint(3, 6) / 0.25)
            ia = rs.randint(i0 + 3, max(i0 + 4, i1 - Ls - 2))
            ib = min(i1 - 3, ia + Ls)
            if ib - ia >= 11:
                bahce = (m0, ia, ib)
                sira_tut[m0 + 1][ia + 1:ib] = False
    # iki sıra arasındaki bir blok da bahçe olabilir (uzun, dar "gardens"); uzun mahallede iki tane
    blok_bahceler = []
    if ay["bahce"] > 0 and M >= 1:
        for _ in range(2 if sira_tut[1].sum() > 64 else 1):
            if rs.rand() > ay["bahce"] * 0.8:
                continue
            aday = []
            for m0 in range(0, M):
                if bahce and m0 in (bahce[0], bahce[0] + 1):
                    continue
                iki = sira_tut[m0] & sira_tut[m0 + 1]
                for (i0, i1) in kosular(iki, enaz=22, bosluk=0):
                    aday.append((m0, i0, i1))
            if not aday:
                break
            m0, i0, i1 = aday[rs.randint(len(aday))]
            Ls = int(rs.randint(3, 6) / 0.25)
            ia = rs.randint(i0 + 2, max(i0 + 3, i1 - Ls - 1))
            ib = min(i1 - 2, ia + Ls)
            if ib - ia >= 11 and not any(m0 == q[0] and ia - 14 < q[2] and q[1] < ib + 14 for q in blok_bahceler):
                blok_bahceler.append((m0, ia, ib))

    # --- sıraları ekle (uçlar bir yola uzatılır)
    acik_uclar = []           # (m, i): bir yola varamayan sıra uçları
    for m in range(1, M + 1):
        for (i0, i1) in kosular(sira_tut[m], enaz=8, bosluk=0):
            p = sira_pts[m][i0:i1 + 1]
            bas_ek = son_ek = None
            if not (bahce and m == bahce[0] + 1 and i0 == bahce[2]):
                yon = p[0] - p[min(3, len(p) - 1)]
                yon /= max(1e-9, math.hypot(*yon))
                bas_ek = yuru(p[0], yon, 5.0, genis)
                if bas_ek is None:
                    acik_uclar.append((m, i0))
            if not (bahce and m == bahce[0] + 1 and i1 == bahce[1]):
                yon = p[-1] - p[max(-4, -len(p))]
                yon /= max(1e-9, math.hypot(*yon))
                son_ek = yuru(p[-1], yon, 5.0, genis)
                if son_ek is None:
                    acik_uclar.append((m, i1))
            if bas_ek is not None:
                p = np.vstack([bas_ek[::-1], p[1:]])
            if son_ek is not None:
                p = np.vstack([p[:-1], son_ek])
            if yerel_ekle(p, SOKAK, "sira"):
                eklenen += 1
    tum_guncelle()

    def kesen(m, i, sinif=SOKAK, tur_="kesen"):
        """m. sıradan (i. kılavuz noktasında) bir üst sıraya ya da ötedeki ilk yola kesen sokak."""
        if not sira_tut[m][i]:
            return False
        p0 = sira_pts[m][i]
        if m + 1 <= M and sira_tut[m + 1][i]:
            p1 = sira_pts[m + 1][i]
            orta = (p0 + p1) / 2
            if not ornek(maske, orta) or ornek(TUM_UZAK, orta) < 1.0:
                return False
            return yerel_ekle(yeniden([p0, p1], 0.25), sinif, tur_) is not None
        yol = yuru(p0, nr[i], (ay["sira"][0] + 1) * 2.0, genis, enaz=1.5)
        if yol is None or len(yol) < 7:
            return False
        orta = yol[len(yol) // 2]
        if ornek(TUM_UZAK, orta) < min(0.9, len(yol) * 0.125 - 0.4):
            return False
        return yerel_ekle(yol, sinif, tur_) is not None

    # --- bahçenin iki ucundaki kesen sokaklar ve çokgeni
    yasak = {}                  # sıra → [(i0, i1)] kesen sokak konmayacak aralıklar
    if bahce:
        m0, ia, ib = bahce
        for i in (ia, ib):
            yerel_ekle(yeniden([sira_pts[m0][i], sira_pts[m0 + 1][i], sira_pts[m0 + 2][i]], 0.25), SOKAK, "meydan")
        for m in (m0, m0 + 1):
            yasak.setdefault(m, []).append((ia - 10, ib + 10))
        pay = 0.62
        ii = np.arange(ia, ib + 1)
        ii = ii[(s[ii] - s[ia] >= pay) & (s[ib] - s[ii] >= pay)]
        if len(ii) >= 3:
            alt = sira_pts[m0][ii] + nr[ii] * pay
            ust = sira_pts[m0 + 2][ii] - nr[ii] * pay
            # merkezde taş döşeli meydan, teras mahallelerinde ortası bahçeli meydan (square)
            (MEYDANCIK if tur == "cekirdek" and rs.rand() < 0.6 else BAHCELER).append(np.vstack([alt, ust[::-1]]))
        eklenen += 2
        tum_guncelle()
    for (m0, ia, ib) in blok_bahceler:
        for i in (ia, ib):
            yerel_ekle(yeniden([sira_pts[m0][i], sira_pts[m0 + 1][i]], 0.25), SOKAK, "meydan")
        yasak.setdefault(m0, []).append((ia - 8, ib + 8))
        pay = 0.6
        ii = np.arange(ia, ib + 1)
        ii = ii[(s[ii] - s[ia] >= pay) & (s[ib] - s[ii] >= pay)]
        if len(ii) >= 3:
            (MEYDANCIK if tur == "cekirdek" and rs.rand() < 0.6 else BAHCELER).append(
                np.vstack([sira_pts[m0][ii] + nr[ii] * pay, (sira_pts[m0 + 1][ii] - nr[ii] * pay)[::-1]]))
    if blok_bahceler:
        tum_guncelle()

    # --- kesen sokaklar: her sıradan yukarı, şaşırtmalı
    kmin, kmax = ay["kesen"]
    for m in range(0, M + 1):
        for (i0, i1) in kosular(sira_tut[m], enaz=8, bosluk=0):
            i = i0 + int(rs.uniform(0.3, 0.8) * kmin / 0.25)
            onceki = None
            while i <= i1 - 3:
                if any(a_ <= i <= b_ for a_, b_ in yasak.get(m, [])):
                    i += 4
                    onceki = None
                    continue
                if kesen(m, i, ARA if rs.rand() < ay["dar"] else SOKAK):
                    eklenen += 1
                    aralik = sira_d[m + 1] - sira_d[m] if m + 1 <= M else 0
                    # ara sokak (mews): geniş sırada, iki kesen sokağın arasında, sıraya paralel dar geçit
                    if onceki is not None and m + 1 <= M and aralik >= 3.9 and rs.rand() < ay["ara"] and \
                            sira_tut[m][onceki:i + 1].all() and sira_tut[m + 1][onceki:i + 1].all():
                        ii = np.arange(onceki, i + 1)
                        yerel_ekle((sira_pts[m][ii] + sira_pts[m + 1][ii]) / 2, ARA, "ara")
                    # geçit: uzun teras bloğunu ortadan bölen dar ara sokak
                    elif onceki is not None and (i - onceki) * 0.25 >= 7.5 and rs.rand() < ay["gecit"]:
                        kesen(m, (onceki + i) // 2, ARA, "gecit")
                    onceki = i
                i += int(rs.randint(kmin, kmax + 1) / 0.25)
    # --- yola varamayan sıra uçları: bir alt sıraya (ya da kılavuza) bağla; dış mahallelerde çıkmaz olarak kalabilir
    for (m, i) in acik_uclar:
        if tur in ("ev", "banliyo") and rs.rand() < 0.3:
            CIKMAZ_UC.append(sira_pts[m][i])
            continue
        if sira_tut[m - 1][i]:
            orta = (sira_pts[m][i] + sira_pts[m - 1][i]) / 2
            if ornek(maske, orta) and ornek(TUM_UZAK, orta) >= 0.9:
                yerel_ekle(yeniden([sira_pts[m - 1][i], sira_pts[m][i]], 0.25), SOKAK, "kesen")
    # --- banliyö: sıralardan iki yana kısa çıkmazlar (close)
    if tur == "banliyo":
        tum_guncelle()
        for m in range(0, M + 1):
            for (i0, i1) in kosular(sira_tut[m], enaz=8, bosluk=0):
                i = i0 + int(rs.uniform(1.5, 3.5) / 0.25)
                taraf = 1
                while i <= i1 - 6:
                    for dene in (taraf, -taraf):
                        p0 = sira_pts[m][i]
                        uc = p0 + nr[i] * dene * 3.0
                        yol_ = yeniden([p0, uc], 0.25)
                        cc, rr = fpx(yol_[4:])
                        if maske[rr, cc].all() and (SINIR_UZAK[rr, cc] >= 1.2).all() and TUM_UZAK[rr[-1], cc[-1]] >= 2.3 and \
                                (TUM_UZAK[rr, cc] >= np.minimum(1.9, np.arange(4, 4 + len(cc)) * 0.25 - 0.4)).all():
                            yerel_ekle(yol_, SOKAK, "cikmaz", cikmaz=True)
                            CIKMAZ_UC.append(uc)
                            tum_guncelle()
                            taraf = -dene
                            break
                    i += int(rs.uniform(3.6, 5.2) / 0.25)
    tum_guncelle()
    return eklenen


def ada_doldur(k, tur):
    ay = ADA_AYAR[tur]
    maske = ADA == k
    genis = ndi.binary_dilation(maske, iterations=3)
    kenar = genis & ~maske
    say = np.bincount(ISK_NO[kenar], minlength=len(SEHIR_ISK) + 1)[1:].astype(float)
    for no, y in enumerate(SEHIR_ISK):
        if y.tur == "kopru":
            say[no] = 0
        elif y.tur == "meydan":
            say[no] *= 0.6
        elif y.tur in ("isin", "isin2"):
            say[no] *= rs.uniform(0.75, 1.35)          # bazen ışınsala, bazen halkaya paralel mahalle
    adaylar = [int(i) for i in np.argsort(-say) if say[i] >= 12][:3]

    def bos_cepler(esik):
        return maske & (TUM_UZAK > esik) & (SINIR_UZAK > 1.5) & (ISK_UZAK > 1.9) & ~CEP_ATLA

    # 1) iskelet yollarını kılavuz alarak
    for tekrar, no in enumerate(adaylar):
        if tekrar > 0 and bos_cepler(ay["bos"] + 0.6).sum() / (F * F) < 8:
            break
        y = SEHIR_ISK[no]
        c, r = fpx(y.pts)
        ks = kosular(genis[r, c], enaz=12, bosluk=6)
        if not ks:
            continue
        a, b = max(ks, key=lambda t: t[1] - t[0])
        a, b = max(0, a - 6), min(len(y.pts) - 1, b + 6)
        g = y.pts[a:b + 1]
        if len(g) < 14:
            continue
        _, nr = teget_normal(g, 8)
        sol = int(ornek(maske, g + nr * 1.5).sum())
        sag = int(ornek(maske, g - nr * 1.5).sum())
        if max(sol, sag) < 6:
            continue
        kafes(k, tur, g, np.ones(len(g), bool), 1 if sol >= sag else -1, maske, genis)

    # 2) kalan cepler: en derin noktadan, en yakın yola paralel yeni bir sokak açılır; iki yanına sıralar
    for _ in range(max(14, int(ada_alan[k - 1] / 30))):
        b = bos_cepler(ay["bos"])
        if b.sum() / (F * F) < 2.5:
            break
        e, n = ndi.label(b)
        boy = ndi.sum(b, e, range(1, n + 1))
        j = int(np.argmax(boy))
        if boy[j] / (F * F) < 2.5:
            break
        cep = e == j + 1
        derin = np.where(cep, TUM_UZAK, 0)
        r0, c0 = np.unravel_index(int(np.argmax(derin)), derin.shape)
        p0 = np.array([c0 / F - PAD, r0 / F - PAD])
        # en yakın yolun yönü: uzaklık alanının eğimine dik
        yum = ndi.gaussian_filter(TUM_UZAK[max(0, r0 - 24):r0 + 25, max(0, c0 - 24):c0 + 25], 3)
        gy, gx = np.gradient(yum)
        cy, cx = min(24, r0), min(24, c0)
        gv = np.array([gx[cy, cx], gy[cy, cx]])
        if math.hypot(*gv) < 1e-4:
            # tam ortada (iki yola eşit uzak) eğim sıfırlanır: yakın bir noktadan al
            gv = np.array([gx[max(0, cy - 4), cx], gy[max(0, cy - 4), cx]])
        if math.hypot(*gv) < 1e-4:
            gv = np.array([1.0, 0.3])
        gv /= math.hypot(*gv)
        yon = np.array([-gv[1], gv[0]])
        g = np.array([p0 + yon * t for t in np.arange(-26, 26.01, 0.25)])
        cc, rr = fpx(g)
        gec = maske[rr, cc] & (TUM_UZAK[rr, cc] >= 2.2) & (SINIR_UZAK[rr, cc] >= 1.5) & (ISK_UZAK[rr, cc] >= 1.9)
        orta = len(g) // 2
        kosu = [t for t in kosular(gec, enaz=4, bosluk=1) if t[0] <= orta <= t[1]]
        if not kosu:
            # bu cebe sokak sığmıyor: bir daha denenmesin diye işaretle
            atla(r0, c0)
            continue
        i0, i1 = kosu[0]
        p = g[i0:i1 + 1]
        bas_ek = yuru(p[0], -yon, 5.5, genis)
        son_ek = yuru(p[-1], yon, 5.5, genis)
        tam = p
        if bas_ek is not None:
            tam = np.vstack([bas_ek[::-1], tam[1:]])
        if son_ek is not None:
            tam = np.vstack([tam[:-1], son_ek])
        if bas_ek is None and son_ek is None:
            # hiçbir yola bağlanamıyor: en yakın yola dik bir bağlantıyla
            bag = yuru(p0, -gv, 6.0, genis)
            if bag is None:
                atla(r0, c0)
                continue
            yerel_ekle(bag, SOKAK, "kesen")
        if yerel_ekle(tam, SOKAK, "sira") is None:
            atla(r0, c0)
            continue
        if bas_ek is None and tur in ("ev", "banliyo"):
            CIKMAZ_UC.append(p[0])
        if son_ek is None and tur in ("ev", "banliyo"):
            CIKMAZ_UC.append(p[-1])
        tum_guncelle()
        tut0 = np.zeros(len(g), bool)
        tut0[i0:i1 + 1] = True
        kafes(k, tur, g, tut0, 1, maske, genis)
        kafes(k, tur, g, tut0, -1, maske, genis)


CEP_ATLA = np.zeros((GH, GW), bool)       # sokak sığmayan cepler: bir daha denenmez


def atla(r0, c0, yari=7):
    CEP_ATLA[max(0, r0 - yari):r0 + yari + 1, max(0, c0 - yari):c0 + yari + 1] = True


def adalari_doldur():
    for k in sorted(ADA_TUR, key=lambda k: -ada_alan[k - 1]):
        if ADA_TUR[k] in ADA_AYAR:
            ada_doldur(k, ADA_TUR[k])
    tum_guncelle()


adalari_doldur()

# ====================================================================== 6) kare ızgarası
def kare_yolu(pts):
    """Çizginin geçtiği kareler, 4 bağlı (kare kare yürünebilir)."""
    q = yeniden(pts, 0.1)
    xi, yi = np.rint(q[:, 0]).astype(int), np.rint(q[:, 1]).astype(int)
    out = [(int(xi[0]), int(yi[0]))]
    for k in range(1, len(q)):
        x, y = int(xi[k]), int(yi[k])
        px_, py_ = out[-1]
        if (x, y) == (px_, py_):
            continue
        if x != px_ and y != py_:
            # çapraz adım: aradaki iki kareden çizgiye yakın olanı
            m = (q[k] + q[k - 1]) / 2
            a, b = (x, py_), (px_, y)
            out.append(a if (a[0] - m[0]) ** 2 + (a[1] - m[1]) ** 2 <= (b[0] - m[0]) ** 2 + (b[1] - m[1]) ** 2 else b)
        out.append((x, y))
    return out


TX, TY = np.meshgrid(np.arange(W), np.arange(H))
TP = np.stack([TX, TY], -1).astype(float)                 # kare merkezleri
kent = ornek(KENT_F, TP)
kiyi = ornek(KIYI_UZAK, TP)
su = kiyi < 0.12
YOLLAR = SEHIR_ISK + YEREL
yolK = np.zeros((H, W), bool)
sinifK = np.full((H, W), 9, int)
for y in YOLLAR:
    for (x, yy_) in kare_yolu(y.pts):
        if 0 <= x < W and 0 <= yy_ < H:
            if su[yy_, x] and y.tur != "kopru":
                continue
            if not kent[yy_, x] and not su[yy_, x]:
                continue
            yolK[yy_, x] = True
            sinifK[yy_, x] = min(sinifK[yy_, x], y.sinif)

K4 = np.array([[0, 1, 0], [1, 0, 1], [0, 1, 0]])


def komsu_say(a):
    return ndi.convolve(a.astype(int), K4, mode="constant")


def izgara_temizle():
    """Ana ağa bağlı olmayan parçalar ve (çıkmaz olmayan) kör uçlar atılır."""
    global yolK
    e, n = ndi.label(yolK)
    kopuk = 0
    if n > 1:
        boy = ndi.sum(yolK, e, range(1, n + 1))
        ana = int(np.argmax(boy)) + 1
        kopuk = int(yolK.sum() - boy[ana - 1])
        yolK &= e == ana
    # meşru kör uçlar: çıkmaz başları ve lekeden çıkan iskelet yollarının ucu
    mesru = np.zeros((H, W), bool)
    for u in CIKMAZ_UC:
        x, yy_ = int(round(u[0])), int(round(u[1]))
        mesru[max(0, yy_ - 1):yy_ + 2, max(0, x - 1):x + 2] = True
    for y in SEHIR_ISK:
        if getattr(y, "cikis", False):
            x, yy_ = int(round(y.pts[-1][0])), int(round(y.pts[-1][1]))
            mesru[max(0, yy_ - 2):yy_ + 3, max(0, x - 2):x + 3] = True
    budanan = 0
    for _ in range(40):
        uc = yolK & (komsu_say(yolK) <= 1) & ~mesru
        if not uc.any():
            break
        budanan += int(uc.sum())
        yolK &= ~uc
    return kopuk, budanan, mesru


KOPUK, BUDANAN, MESRU_UC = izgara_temizle()
sinifK[~yolK] = 9


def cizgileri_kirp():
    """Yol çizgilerini kare ızgarasına uydurur: yol karesi olmayan yerden geçen kesimler atılır."""
    out = []
    for y in YOLLAR:
        q = y.pts
        xi = np.clip(np.rint(q[:, 0]).astype(int), 0, W - 1)
        yi = np.clip(np.rint(q[:, 1]).astype(int), 0, H - 1)
        ic = (q[:, 0] > -0.5) & (q[:, 0] < W - 0.5) & (q[:, 1] > -0.5) & (q[:, 1] < H - 0.5)
        var = yolK[yi, xi] & ic
        for (a, b) in kosular(var, enaz=4, bosluk=3):
            p = q[a:b + 1]
            if uzunluk(p) < 0.8:
                continue
            z = Yol(p, y.sinif, y.tur, y.ad)
            z.cikmaz = y.cikmaz
            out.append(z)
    return out


YOLLAR = cizgileri_kirp()

# ====================================================================== 7) bölgeler, yoğunluk, semtler, önemli yerler
_, _yi = ndi.distance_transform_edt(ADA == 0, return_indices=True)
ADA_YAKIN = ADA[_yi[0], _yi[1]]
ada_k = ornek(ADA_YAKIN, TP)
kent_ic = ornek(KENT_UZAK, TP)
yaka_k = ornek(YAKA, TP)
rn_k = np.hypot(TX - NM[0], TY - NM[1])
rm_k = np.hypot(TX - MERKEZ[0], TY - MERKEZ[1])
HARF = {"cekirdek": "O", "teras": "O", "teras_g": "O", "ev": "E", "banliyo": "B", "sanayi": "W", "kampus": "C", "okul": "C", "park": "P",
        "mezarlik": "G", "meydan": "Z"}
zone = np.full((H, W), ".", dtype="<U1")
for yy_ in range(H):
    for x in range(W):
        if not kent[yy_, x]:
            continue
        t = ADA_TUR.get(int(ada_k[yy_, x]), "bos")
        if t == "bos":
            t = "teras" if ornek(IC2, TP[yy_, x]) else "ev"
        zone[yy_, x] = HARF[t]
# dışa doğru seyrelme: leke kenarına yakın evler banliyö, içeride kalan banliyö adası bahçeli ev
zone[(zone == "E") & (kent_ic < 5.5)] = "B"
zone[(zone == "B") & (kent_ic > 9.0)] = "E"
zone[(zone == "O") & (kent_ic < 2.6)] = "E"
# Docklands: kuzey yakanın doğu ucunda kıyı boyunca liman / ambar (ada sınırlarına bakmadan)
zone[(yaka_k > 0) & (TX - MX > R2 + 3) & (kiyi < 8.5) & np.isin(zone, ["E", "B", "O"])] = "W"
# mahalle merkezleri (eski köyler): sıkışık eski doku (teraslar), çevresinde bahçeli evler
mah_k = np.full((H, W), 99.0)
for _m in MAHALLE:
    if not kentte(_m["c"]):
        continue
    _d = np.hypot(TX - _m["c"][0], TY - _m["c"][1])
    _rr = 6.0 * (1 + 0.2 * _m["g"](np.arctan2(TY - _m["c"][1], TX - _m["c"][0]) + 1.3))
    zone[(_d < _rr) & np.isin(zone, ["E", "B"])] = "O"
    zone[(_d >= _rr) & (_d < _rr + 3.5) & (zone == "B")] = "E"
    mah_k = np.minimum(mah_k, _d)

# çarşı caddeleri (high street): ışınsal ana yolların iç kesimi, iç halka, meydan çevresi, köprü başları
_cars = Image.new("I", (GW, GH), 0)
for y in SEHIR_ISK:
    if y.tur == "isin":
        ic = ornek(IC2, y.pts) | (np.hypot(y.pts[:, 0] - NM[0], y.pts[:, 1] - NM[1]) < R2 + 2.5)
        if ic.sum() > 4:
            ciz(y.pts[ic], 1, 255, _cars)
    elif y.tur == "meydan" or (y.tur == "halka" and "Inner" in y.ad):
        ciz(y.pts, 1, 255, _cars)
    elif y.tur == "isin2":
        ic = np.hypot(y.pts[:, 0] - NM[0], y.pts[:, 1] - NM[1]) < RM + 1.5
        if ic.sum() > 4:
            ciz(y.pts[ic], 1, 255, _cars)
# mahalle merkezlerinin çarşısı: ışınsalın köyden geçen kesimi + kesen cadde (kilise sokağı)
for _m in MAHALLE:
    if not kentte(_m["c"]):
        continue
    _kesen = _m["ad"].split()[0] + " Church Street"
    for y in SEHIR_ISK:
        if y.ad not in (_m["yol_ad"], _kesen):
            continue
        ic = np.hypot(y.pts[:, 0] - _m["c"][0], y.pts[:, 1] - _m["c"][1]) < (5.6 if y.ad == _m["yol_ad"] else 4.4)
        for (a_, b_) in kosular(ic, enaz=4, bosluk=0):
            ciz(y.pts[a_:b_ + 1], 1, 255, _cars)
CARSI_UZAK = ndi.distance_transform_edt(np.array(_cars) == 0) / F
cars_k = ornek(CARSI_UZAK, TP)
zone[(cars_k < 1.5) & np.isin(zone, ["O", "E"])] = "M"
# kuleler: merkezin doğusunda küçük bir iş merkezi, güney köprü başında birkaç kule
KULE1 = MERKEZ + np.array([7.0, 2.2])
KULE2 = NM + np.array([-3.5, -6.5])
zone[(np.hypot(TX - KULE1[0], TY - KULE1[1]) < 3.6) & np.isin(zone, ["O", "M"])] = "H"
zone[(np.hypot(TX - KULE2[0], TY - KULE2[1]) < 2.4) & np.isin(zone, ["O", "M"])] = "H"
# Docklands'in ucunda yeni iş merkezi (cam kuleler)
KULE3 = NM + np.array([R2 + 7.5, 7.0])
zone[(np.hypot(TX - KULE3[0], TY - KULE3[1]) < 2.7) & (yaka_k > 0) & np.isin(zone, ["O", "M", "W", "E"])] = "H"
# meydan
zone[MY - 1:MY + 2, MX - 1:MX + 2] = "Z"
# mahalle meydancıkları: kavşağın bir köşesinde üç dört karelik taş meydan (pazar yeri)
for _m in MAHALLE:
    if not kentte(_m["c"]):
        continue
    _n = np.array([-_m["yon"][1], _m["yon"][0]]) * (1 if rs.rand() < 0.5 else -1)
    _q = _m["c"] + (_m["yon"] * (1 if rs.rand() < 0.5 else -1) + _n) * 1.35
    _ad = sorted((float(np.hypot(x - _q[0], yy_ - _q[1])), x, yy_) for yy_ in range(H) for x in range(W)
                 if abs(x - _q[0]) < 2.2 and abs(yy_ - _q[1]) < 2.2 and zone[yy_, x] in "OME" and not yolK[yy_, x] and kent[yy_, x])
    for _, x, yy_ in _ad[:4]:
        if np.hypot(x - _q[0], yy_ - _q[1]) < 1.9:
            zone[yy_, x] = "Z"
    _m["meydan"] = _q
# meydan bahçeleri
bahce_k = np.zeros((H, W), bool)
for b in BAHCELER:
    im = Image.new("L", (W * 4, H * 4), 0)
    ImageDraw.Draw(im).polygon([((x + 0.5) * 4, (y_ + 0.5) * 4) for x, y_ in b], fill=255)
    a = np.array(im).reshape(H, 4, W, 4).mean(axis=(1, 3)) / 255.0
    bahce_k |= a > 0.45
zone[bahce_k & kent] = "P"
for b in MEYDANCIK:
    im = Image.new("L", (W * 4, H * 4), 0)
    ImageDraw.Draw(im).polygon([((x + 0.5) * 4, (y_ + 0.5) * 4) for x, y_ in b], fill=255)
    a = np.array(im).reshape(H, 4, W, 4).mean(axis=(1, 3)) / 255.0
    zone[(a > 0.45) & kent & ~yolK] = "Z"
# kıyı şeridi (liman dışında): nehir kıyısı çimenli set
zone[(kiyi < 0.95) & kent & ~np.isin(zone, ["W"])] = "R"
zone[su] = "R"


def avm_yeri(isk_ad1, isk_ad2, adet=7):
    """İki iskelet yolunun kesiştiği kavşağın dış köşesine AVM / perakende parkı karoları."""
    a = [y for y in SEHIR_ISK if y.ad == isk_ad1]
    b = [y for y in SEHIR_ISK if y.ad == isk_ad2]
    if not a or not b:
        return []
    kd = cKDTree(b[0].pts)
    d, _ = kd.query(a[0].pts)
    i = int(np.argmin(d))
    kav = a[0].pts[i]
    dis = kav - NM
    dis /= max(1e-9, math.hypot(*dis))
    hedef = kav + dis * 2.6 + np.array([-dis[1], dis[0]]) * 2.4
    aday = [(float(np.hypot(x - hedef[0], yy_ - hedef[1])), x, yy_) for yy_ in range(H) for x in range(W)
            if zone[yy_, x] in "OEBM" and not yolK[yy_, x] and np.hypot(x - hedef[0], yy_ - hedef[1]) < 3.0]
    aday.sort()
    sec = [(x, yy_) for _, x, yy_ in aday[:adet]]
    for x, yy_ in sec:
        zone[yy_, x] = "A"
    return sec


AVM1 = avm_yeri("Northgate Road", "North Circular")
AVM2 = avm_yeri("Harbour Road", "South Circular")
AVM3 = avm_yeri("Greenwich Road", "Orbital Avenue South", 6)
AVM4 = avm_yeri("Abbey Road", "Orbital Avenue North", 6)
# demiryolu: hattın geçtiği kareler yapısız şerit ('D'); istasyonlarda peronlar için genişler, terminalin önünde taş meydan
ray_k = ornek(RAY_UZAK, TP)
_dmask = (ray_k < 0.62) & kent & ~yolK & ~su
for _i in ISTASYON:
    _u = (TX - _i["p"][0]) * _i["yon"][0] + (TY - _i["p"][1]) * _i["yon"][1]
    _v = np.abs(-(TX - _i["p"][0]) * _i["yon"][1] + (TY - _i["p"][1]) * _i["yon"][0])
    if _i["terminal"]:
        _dmask |= (_u > -0.4) & (_u < 5.2) & (_v < 1.55) & kent & ~yolK & ~su
        zone[(_u <= -0.4) & (_u > -2.6) & (_v < 2.0) & kent & ~yolK & ~su & np.isin(zone, ["O", "M", "E", "H"])] = "Z"
    else:
        _dmask |= (np.abs(_u) < 2.4) & (_v < 1.3) & kent & ~yolK & ~su
zone[_dmask] = "D"
zone[yolK & (zone == ".")] = "E"       # (olmamalı) kır karesinde yol kalmasın diye

# kare türü
kare = np.full((H, W), ".", dtype="<U1")
kare[zone == "P"] = "P"
kare[su] = "S"
kare[yolK] = "Y"
kare[yolK & su] = "K"

# yoğunluk
YOG = {"O": 7, "M": 8, "H": 9, "E": 4, "B": 2, "C": 4, "W": 6, "A": 6, "Z": 9, "P": 0, "R": 0, "G": 0, "D": 0, ".": 0}
yog = np.vectorize(lambda z: YOG[z])(zone)
yog[(zone == "O") & (rm_k < 13)] = 9
yog[(zone == "O") & (rm_k >= 13) & (rn_k < 23)] = 8
yog[(zone == "O") & (mah_k < 3.6)] = 8
yog[(zone == "E") & (kent_ic < 7)] = 3
yog[(zone == "B") & (kent_ic < 3.2)] = 1


# semtler (1 Downtown · 2 East Side · 3 Industry & Campus · 4 West Harbour · 5 Green Hill)
SEMT_ACI, SEMT_GUNEY = 80.0, 8.0          # kuzey yakada East Side / Green Hill sınır açısı; güney yakada doğu / batı sınırı
def semt_of(c, tur):
    yk = ornek(YAKA, c)
    rn = math.hypot(*(c - NM))
    aci = math.degrees(math.atan2(c[1] - MERKEZ[1], c[0] - MERKEZ[0])) % 360
    if ornek(IC1, c) or tur == "meydan":
        return 1
    if yk > 0:
        if aci < SEMT_ACI or aci > 325:
            return 3 if (c[0] - MX > R2 + 5 or tur in ("sanayi", "kampus")) else 2
        return 5
    return 3 if c[0] > MX + SEMT_GUNEY else 4


ada_semt = {k: semt_of(ada_mer[k - 1], ADA_TUR.get(k, "bos")) for k in range(1, ADA_N + 1)}
bolge = np.full((H, W), 9, int)
for yy_ in range(H):
    for x in range(W):
        if kent[yy_, x] and not yolK[yy_, x] and not su[yy_, x]:
            bolge[yy_, x] = ada_semt.get(int(ada_k[yy_, x]), 1)
# yol, köprü ve su kareleri: komşu arsaların en küçük semti (sınırdaki yol önce açılan semtin)
for _ in range(60):
    bos = (bolge == 9) & (kent | su)
    if not bos.any():
        break
    mn = ndi.minimum_filter(bolge, size=3, mode="constant", cval=9)
    bolge[bos] = mn[bos]
bolge[bolge == 9] = 1
bolge[~kent & ~su] = 1                  # kır: kilitlenmez


def seviye_baglantisi():
    """Her açılış seviyesinde (1–5) açık yollar merkez ofisten tek parça ulaşılır olmalı: kurye yalnız açık semtlerin yollarından
    geçer (CityMap.FindPath). Ulaşılamayan yol karesi ve binası, ulaşıldığı ilk seviyenin semtine kaydırılır."""
    kaydirilan = 0
    bas = None
    en = 1e9
    for yy_ in range(H):
        for x in range(W):
            if yolK[yy_, x] and bolge[yy_, x] == 1:
                d = (x - MX) ** 2 + (yy_ - MY) ** 2
                if d < en:
                    en, bas = d, (x, yy_)
    for L in range(1, 5):
        gor = np.zeros((H, W), bool)
        gor[bas[1], bas[0]] = True
        kuyruk = [bas]
        while kuyruk:
            x, yy_ = kuyruk.pop()
            for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                nx_, ny_ = x + dx, yy_ + dy
                if 0 <= nx_ < W and 0 <= ny_ < H and yolK[ny_, nx_] and not gor[ny_, nx_] and bolge[ny_, nx_] <= L:
                    gor[ny_, nx_] = True
                    kuyruk.append((nx_, ny_))
        kopuk = yolK & (bolge <= L) & ~gor
        kaydirilan += int(kopuk.sum())
        bolge[kopuk] = L + 1
        # binalar: kapısı (ilk yol komşusu: sol, sağ, aşağı, yukarı) ulaşılamıyorsa
        for yy_ in range(H):
            for x in range(W):
                if yolK[yy_, x] or su[yy_, x] or not kent[yy_, x] or bolge[yy_, x] > L:
                    continue
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    nx_, ny_ = x + dx, yy_ + dy
                    if 0 <= nx_ < W and 0 <= ny_ < H and yolK[ny_, nx_]:
                        if not gor[ny_, nx_]:
                            bolge[yy_, x] = L + 1
                            kaydirilan += 1
                        break
    return kaydirilan


SEVIYE_KAYAN = seviye_baglantisi()


def onemli_yerler():
    # belediye (City Park anıtı): büyük parkın ortasındaki park karesi
    bel = [MX, MY]
    if BUYUK_PARK:
        ys, xs = np.nonzero((ada_k == BUYUK_PARK) & (zone == "P") & ~yolK)
        if len(xs):
            c = ada_mer[BUYUK_PARK - 1]
            i = int(np.argmin((xs - c[0]) ** 2 + (ys - c[1]) ** 2))
            bel = [int(xs[i]), int(ys[i])]
    oto = list(AVM2[0]) if AVM2 else [MX - 20, MY - 20]
    return bel, oto


BELEDIYE, OTOGAR = onemli_yerler()

# ====================================================================== 8) kırsal: yollar, köyler, çiftlikler, tarlalar
KIR = []             # Yol: şehir dışındaki yollar (sinif ANA / CADDE / SOKAK = köy yolu)
KIR_EV = []          # (x, y, açı°, tur)  tur 0 yol boyu ev, 1 köy evi, 2 köy kilisesi
CIFTLIK = []         # (x, y)
TARLA = []           # dict(poly, mx, my, gen, boy, aci, tur)
DIS = np.maximum(np.maximum(-0.5 - WX, WX - (W - 0.5)), np.maximum(-0.5 - WY, WY - (H - 0.5)))      # dikdörtgenin dışına uzaklık (içeride eksi)
KIR_MENZIL = 33.0    # tarlalar şehir dikdörtgeninin bu kadar ötesine kadar


def bakis_acisi(bak):
    """City3D açı kuralı: 0° kuzeye (+y), 90° batıya, 180° güneye, 270° doğuya bakar."""
    return (math.degrees(math.atan2(-bak[0], bak[1])) + 360.0) % 360.0


def kir_yollari():
    # 1) ana yolların ve caddelerin şehir dışındaki devamı
    for y in KIR_ANA:
        p = y.pts
        ic = (ornek(DIS, p) < KIR_MENZIL + 12) & (ornek(KIYI_UZAK, p) > 1.6)
        ks = kosular(ic, enaz=20, bosluk=0)
        if not ks or ks[0][0] > 4:
            continue
        a, b = ks[0]
        KIR.append(Yol(p[a:b + 1], ANA if y.sinif == ANA else CADDE, y.tur, y.ad))
    # 2) çevre köy yolu: şehir lekesini ~9–12 kare dışından dolaşan kıvrımlı yol (nehri şehir dikdörtgeninin dışında geçer)
    acilar = np.linspace(-math.pi, math.pi, 361)
    kenar = np.zeros(len(acilar))
    for i, a in enumerate(acilar):
        yon = np.array([math.cos(a), math.sin(a)])
        r = 20.0
        while r < 70 and ornek(KENT_F, BM + yon * r):
            r += 0.5
        r2 = r
        while r2 < 70:                      # lekenin girintisinden sonra yeniden başlıyorsa en dış kenar
            if ornek(KENT_F, BM + yon * r2):
                r = r2
            r2 += 0.5
        kenar[i] = r
    kenar = ndi.maximum_filter1d(kenar, 31, mode="wrap")
    kenar = ndi.gaussian_filter1d(kenar, 9, mode="wrap")
    g = gur_aci(4)
    r = kenar + 10.0 + 2.5 * g(acilar)
    cev = np.stack([BM[0] + r * np.cos(acilar), BM[1] + r * np.sin(acilar)], 1)
    cev = yeniden(cev, 0.25)
    # nehri dikdörtgenin içinde kesiyorsa oradan koparılır (köprü yok); dışında kesiyorsa köprüyü oyun kurar
    uy = (ornek(KIYI_UZAK, cev) > 1.4) | (ornek(DIS, cev) > 0.8)
    for (a, b) in kosular(uy, enaz=40, bosluk=0):
        KIR.append(Yol(cev[a:b + 1], SOKAK, "cevre", "Orbital Lane"))
    # 3) ikincil yollar çevre yoluna vardığında biter; ana yollar sürer
    ck = cKDTree(cev)
    for y in KIR:
        if y.tur == "kir_isin2":
            d, _ = ck.query(y.pts)
            deg = np.nonzero(d < 0.3)[0]
            if len(deg):
                y.pts = y.pts[:deg[0] + 1]
    # 4) kısa köy yolları: çevre yolundan dışarı, çiftliklere giden çıkmaz tarla yolları
    hepsi = np.vstack([y.pts for y in KIR])
    kd = cKDTree(hepsi)
    n = 0
    for dene in range(120):
        i = rs.randint(40, len(cev) - 40)
        p0 = cev[i]
        dis = p0 - BM
        dis /= math.hypot(*dis)
        aci = math.atan2(dis[1], dis[0]) + rs.uniform(-0.5, 0.5)
        y = isinsal(p0, aci, SOKAK, "tarla_yolu", menzil=rs.uniform(9, 17), sapma=0.9, merkez=BM)
        p = y.pts
        if len(p) < 24:
            continue
        d, _ = kd.query(p[10:])
        if d.min() < 4.5 or (ornek(KIYI_UZAK, p) < 2.2).any() or (ornek(DIS, p) > KIR_MENZIL - 2).any():
            continue
        y.ad = ""
        KIR.append(y)
        hepsi = np.vstack([hepsi, p])
        kd = cKDTree(hepsi)
        n += 1
        if n >= 14:
            break


kir_yollari()
_kr = Image.new("I", (GW, GH), 0)
for y in KIR:
    ciz(y.pts, 1, 255, _kr)
# demiryolunun kırdaki kesimi: tarlalar yol gibi iki yanına dizilir, çiftlik ve evler üstüne konmaz (yol değildir)
RAY_KIR = []
if RAY is not None:
    _dis = np.nonzero(ornek(KENT_DIS, RAY) > 0.5)[0]
    if len(_dis) > 20:
        RAY_KIR = [Yol(RAY[int(_dis[0]):], 9, "ray")]
        ciz(RAY_KIR[0].pts, 1, 255, _kr)
KIRYOL_UZAK = ndi.distance_transform_edt(np.array(_kr) == 0) / F
DOLU = Image.new("L", (GW, GH), 0)          # tarla konmayacak yerler: köyler, çiftlikler, yol boyu evler
_dd = ImageDraw.Draw(DOLU)


def dolu_daire(p, r):
    c, rr = (p[0] + PAD) * F, (p[1] + PAD) * F
    _dd.ellipse([c - r * F, rr - r * F, c + r * F, rr + r * F], fill=255)


def kir_yerlesimi():
    # yol boyu evler (şehirden çıkan yolların ilk kesiminde, uzaklaştıkça seyrelir)
    for y in KIR:
        if not y.tur.startswith("kir_"):
            continue
        p = y.pts
        t, nr = teget_normal(p, 4)
        for i in range(6, min(len(p), 64), 4):
            u = i * 0.25
            for k in (-1, 1):
                if rs.rand() > 0.8 * math.exp(-u / 5.5):
                    continue
                q = p[i] + nr[i] * k * 0.74
                if ornek(KIYI_UZAK, q) < 1.2 or ornek(KENT_DIS, q) < 0.9 or ornek(RAY_UZAK, q) < 1.1:
                    continue
                KIR_EV.append((q[0], q[1], bakis_acisi(-nr[i] * k), 0))
                dolu_daire(q, 0.85)
    # köyler: uzağa giden ana yolların üstünde, şehirden 14–24 kare ötede
    koy_mer = []
    for y in sorted([y for y in KIR if y.tur == "kir_isin" and uzunluk(y.pts) > 30], key=lambda y: rs.rand()):
        p = y.pts
        t, nr = teget_normal(p, 4)
        i0 = int(rs.uniform(15, 24) / 0.25)
        if i0 + 40 >= len(p):
            continue
        m = p[i0]
        if ornek(DIS, m) > 27 or ornek(KIYI_UZAK, m) < 5 or any(math.hypot(*(m - k)) < 26 for k in koy_mer):
            continue
        koy_mer.append(m)
        yari = rs.randint(4, 7)
        for j in range(-yari, yari + 1):
            i = i0 + int(j * 0.95 / 0.25)
            for k in (-1, 1):
                if rs.rand() < 0.16 and not (j == 0 and k == 1):
                    continue
                q = p[i] + nr[i] * k * (0.76 + rs.uniform(0, 0.12))
                if ornek(KIYI_UZAK, q) < 1.0 or ornek(RAY_UZAK, q) < 1.1:
                    continue
                KIR_EV.append((q[0], q[1], bakis_acisi(-nr[i] * k), 2 if (j == 0 and k == 1) else 1))
                dolu_daire(q, 1.25)
        if len(koy_mer) >= 6:
            break
    # çiftlikler: köy yollarının kenarında (çiftlik evi + ahır + meyve bahçesi için yer ayrılır)
    adaylar = [y for y in KIR if y.tur in ("cevre", "tarla_yolu", "kir_isin2")]
    for y in [y for y in KIR if y.tur == "tarla_yolu"]:        # tarla yolunun ucunda çiftlik
        p = y.pts[-1] + (y.pts[-1] - y.pts[-6]) / max(1e-9, math.hypot(*(y.pts[-1] - y.pts[-6]))) * 1.3
        if ornek(KIYI_UZAK, p) > 3 and ornek(DIS, p) < KIR_MENZIL - 4:
            CIFTLIK.append((p[0], p[1]))
    for dene in range(400):
        if len(CIFTLIK) >= 22 or not adaylar:
            break
        y = adaylar[rs.randint(len(adaylar))]
        i = rs.randint(8, max(9, len(y.pts) - 8))
        if i >= len(y.pts):
            continue
        t, nr = teget_normal(y.pts, 4)
        q = y.pts[i] + nr[i] * (1.7 if rs.rand() < 0.5 else -1.7) + np.array([0.0, -0.4])
        if ornek(KIYI_UZAK, q) < 3.5 or ornek(KENT_DIS, q) < 4.5 or ornek(DIS, q) > 26 or ornek(KIRYOL_UZAK, q + np.array([0.3, 1.2])) < 1.2:
            continue
        if any(math.hypot(q[0] - c[0], q[1] - c[1]) < 11 for c in CIFTLIK) or np.array(DOLU)[fpx(q)[1], fpx(q)[0]] > 0:
            continue
        CIFTLIK.append((q[0], q[1]))
    for c in CIFTLIK:
        dolu_daire((c[0] + 0.15, c[1] + 1.15), 2.5)


kir_yerlesimi()
DOLU_F = np.array(DOLU) > 0
_orman = gur(3, 0.11)
_orman2 = gur(3, 0.13)


def tarlalari_uret():
    """Düzensiz tarla çokgenleri: yol boyunca aynalı tohumlar (tarlalar yola dizilir) + serpme tohumlar → Voronoi hücreleri;
    her hücre çit payı kadar içeri çekilir, yola / nehre / şehre / köye taşan kısım kesilir."""
    # şehir zemini (OrganikZemin) kentsel / su karelerinin bir kare ötesine kadar uzanır: tarlalar oraya girmez
    zk = ndi.binary_dilation(kent | su | yolK, structure=np.ones((3, 3), bool))

    def zeminde(p):
        p = np.asarray(p, float)
        xi, yi = np.rint(p[..., 0]).astype(int), np.rint(p[..., 1]).astype(int)
        ic = (xi >= 0) & (yi >= 0) & (xi < W) & (yi < H)
        return ic & zk[np.clip(yi, 0, H - 1), np.clip(xi, 0, W - 1)]

    def yasak(p):
        return (ornek(KENT_DIS, p) < 1.1) | (ornek(KIYI_UZAK, p) < 1.5) | ornek(DOLU_F, p) | (ornek(DIS, p) > KIR_MENZIL) | zeminde(p) | \
            (ornek(RAY_UZAK, p) < 0.55)

    tohum = []
    # yol (ve demiryolu) boyunca: iki yanda karşılıklı tohumlar (iki sıra derinlik)
    for y in KIR + RAY_KIR:
        p = y.pts
        if len(p) < 16:
            continue
        t, nr = teget_normal(p, 6)
        i = rs.randint(4, 14)
        while i < len(p) - 4:
            o1 = rs.uniform(1.9, 3.1)
            o2 = o1 + rs.uniform(3.6, 5.6)
            for k in (-1, 1):
                for o in (o1, o2):
                    q = p[i] + nr[i] * k * o + t[i] * rs.uniform(-0.5, 0.5) * (o > o1)
                    tohum.append(q)
            i += int(rs.uniform(3.8, 7.2) / 0.25)
    tohum = [q for q in tohum if ornek(DIS, q) < KIR_MENZIL + 3 and ornek(KIRYOL_UZAK, q) > 1.5]
    # aynalı çiftlerin birbirine çok yakın düşenlerini ayıkla
    sec = []
    for q in tohum:
        if all((q[0] - s_[0]) ** 2 + (q[1] - s_[1]) ** 2 > 2.6 ** 2 for s_ in sec[-400:]):
            sec.append(q)
    tohum = sec
    # serpme tohumlar (en az 4,4–6,4 kare aralıklı)
    kd_pts = list(tohum)
    for dene in range(15000):
        q = np.array([rs.uniform(-KIR_MENZIL - 4, W + KIR_MENZIL + 4), rs.uniform(-KIR_MENZIL - 4, H + KIR_MENZIL + 4)])
        if ornek(KENT_F, q) or ornek(KIRYOL_UZAK, q) < 2.0:
            continue
        ara = rs.uniform(4.4, 6.4)
        arr = np.array(kd_pts[-4500:])
        if len(arr) and ((arr[:, 0] - q[0]) ** 2 + (arr[:, 1] - q[1]) ** 2).min() < ara * ara:
            continue
        kd_pts.append(q)
    tohum = np.array(kd_pts)
    # dış çerçeve (sonsuz hücre kalmasın)
    cer = [(x, y_) for x in np.linspace(-PAD - 20, W + PAD + 20, 24) for y_ in (-PAD - 20, H + PAD + 20)] + \
          [(x, y_) for y_ in np.linspace(-PAD - 20, H + PAD + 20, 24) for x in (-PAD - 20, W + PAD + 20)]
    vor = Voronoi(np.vstack([tohum, np.array(cer)]))
    kyol = cKDTree(np.vstack([y.pts for y in KIR + RAY_KIR]))
    kyol_pts = np.vstack([y.pts for y in KIR + RAY_KIR])
    kyol_t = np.vstack([teget_normal(y.pts, 4)[0] for y in KIR + RAY_KIR])
    for si in range(len(tohum)):
        reg = vor.regions[vor.point_region[si]]
        if not reg or -1 in reg:
            continue
        poly = [tuple(vor.vertices[v]) for v in reg]
        if alan_poly(poly) < 0:
            poly = poly[::-1]
        s0 = tohum[si]
        # orman / çayır boşlukları: geniş lekelerde tarla yok (yoldan uzak hücrelerde)
        if ornek(KIRYOL_UZAK, s0) > 3.6 and _orman(s0[0] + 0.6 * s0[1]) + _orman2(s0[1] - 0.5 * s0[0]) > 0.62:
            continue
        # kenar payları: yola bakan kenar yoldan 0,6; diğerleri 0,17 (çit / sınır izi)
        m = len(poly)
        yeni = list(poly)
        for i in range(m):
            a, b = np.array(poly[i]), np.array(poly[(i + 1) % m])
            e = b - a
            L = math.hypot(*e)
            if L < 1e-6:
                continue
            nrm = np.array([-e[1], e[0]]) / L            # içeri (saat yönünün tersi çokgende sol)
            orta = (a + b) / 2
            pay = 0.62 if ornek(KIRYOL_UZAK, orta) < 0.75 else 0.17
            yeni = yari_duzlem_kes(yeni, a, nrm, pay)
            if len(yeni) < 3:
                break
        if len(yeni) < 3:
            continue
        # yola taşıyorsa: tohumun yanındaki yarı düzlemle kes (en yakın yol noktasında)
        for _ in range(3):
            pp = np.array(yeni)
            kn = np.vstack([pp, (pp + np.roll(pp, -1, 0)) / 2])
            d, j = kyol.query(kn)
            if d.min() > 0.58:
                break
            jj = j[int(np.argmin(d))]
            q, tt = kyol_pts[jj], kyol_t[jj]
            nrm = np.array([-tt[1], tt[0]])
            if (s0 - q) @ nrm < 0:
                nrm = -nrm
            yeni = yari_duzlem_kes(yeni, q, nrm, 0.62)
            if len(yeni) < 3:
                break
        if len(yeni) < 3:
            continue
        # şehre / nehre / köye taşıyorsa tohuma doğru küçült
        pp = np.array(yeni)
        mer = pp.mean(0)
        tamam = False
        for f in (1.0, 0.86, 0.72, 0.58):
            qq = mer + (pp - mer) * f
            kn = np.vstack([qq, (qq + np.roll(qq, -1, 0)) / 2, (qq + mer) / 2, mer[None]])
            if not yasak(kn).any() and ornek(KIRYOL_UZAK, kn).min() > 0.5:
                pp, tamam = qq, True
                break
        if not tamam:
            continue
        # büyük hücre: en uzun kenara paralel 2–3 şeride bölünür (şerit tarlalar)
        parcalar = [pp]
        A0 = alan_poly(pp)
        if A0 > 26 and rs.rand() < 0.6:
            e = np.roll(pp, -1, 0) - pp
            k = int(np.argmax(np.hypot(e[:, 0], e[:, 1])))
            yon = e[k] / math.hypot(*e[k])
            dik = np.array([-yon[1], yon[0]])
            v = (pp - pp[k]) @ dik
            n_ser = 3 if A0 > 48 else 2
            sin = np.linspace(v.min(), v.max(), n_ser + 1)
            parcalar = []
            for j in range(n_ser):
                q = yari_duzlem_kes([tuple(x) for x in pp], pp[k], dik, sin[j] + (0.17 if j > 0 else 0.0))
                if len(q) >= 3:
                    q = yari_duzlem_kes(q, pp[k], -dik, -(sin[j + 1] - (0.17 if j < n_ser - 1 else 0.0)))
                if len(q) >= 3:
                    parcalar.append(np.array(q))
        for pp in parcalar:
            tarla_ekle(pp)


def tarla_ekle(pp):
    # çok kısa kenarları at
    tem = [pp[0]]
    for q in pp[1:]:
        if math.hypot(*(q - tem[-1])) > 0.35:
            tem.append(q)
    if math.hypot(*(tem[0] - tem[-1])) < 0.35 and len(tem) > 3:
        tem.pop()
    pp = np.array(tem)
    # neredeyse doğrusal köşeleri at (yuvarlamada içbükeylik çıkmasın: oyun tarlayı dışbükey sayar)
    degisti = True
    while degisti and len(pp) > 3:
        degisti = False
        for i in range(len(pp)):
            a, b, c = pp[i - 1], pp[i], pp[(i + 1) % len(pp)]
            e1, e2 = b - a, c - b
            if (e1[0] * e2[1] - e1[1] * e2[0]) < 0.03 * math.hypot(*e1) * math.hypot(*e2):
                pp = np.delete(pp, i, axis=0)
                degisti = True
                break
    if len(pp) < 3:
        return
    A = alan_poly(pp)
    if A < 4.0 or A > 95:
        return
    # içine sığan eğik dikdörtgen: en uzun kenara paralel; merkezi ağırlık merkezi çevresinde aranır
    e = np.roll(pp, -1, 0) - pp
    L = np.hypot(e[:, 0], e[:, 1])
    k = int(np.argmax(L))
    aci = math.atan2(e[k, 1], e[k, 0])
    ca, sa = math.cos(aci), math.sin(aci)
    cp = pp[:, 0] * np.roll(pp[:, 1], -1) - np.roll(pp[:, 0], -1) * pp[:, 1]
    gx = float(cp.dot(pp[:, 0] + np.roll(pp[:, 0], -1)) / (6 * A))
    gy = float(cp.dot(pp[:, 1] + np.roll(pp[:, 1], -1)) / (6 * A))
    u0 = (pp[:, 0] - gx) * ca + (pp[:, 1] - gy) * sa
    v0 = -(pp[:, 0] - gx) * sa + (pp[:, 1] - gy) * ca
    en = (0, 0, 0, gx, gy)
    for du in (0.0, -0.18, 0.18):
        for dv in (0.0, -0.18, 0.18, -0.36, 0.36):
            ou, ov = du * (u0.max() - u0.min()), dv * (v0.max() - v0.min())
            cx, cy = gx + ou * ca - ov * sa, gy + ou * sa + ov * ca
            if ic_nokta(pp, (cx, cy)) < 0.3:
                continue
            u, v = u0 - ou, v0 - ov
            for fu in np.linspace(1.0, 0.3, 15):
                for fv in np.linspace(1.0, 0.3, 15):
                    hu, hv = min(-u.min(), u.max()) * fu, min(-v.min(), v.max()) * fv
                    if hu * hv <= en[0]:
                        continue
                    ok = True
                    for su_, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                        if ic_nokta(pp, (cx + su_ * hu * ca - sv * hv * sa, cy + su_ * hu * sa + sv * hv * ca)) < 0.03:
                            ok = False
                            break
                    if ok:
                        en = (hu * hv, hu, hv, cx, cy)
    if en[1] < 0.7 or en[2] < 0.5:
        return
    TARLA.append(dict(poly=pp, mx=en[3], my=en[4], gen=en[1] * 2, boy=en[2] * 2, aci=math.degrees(aci), tur=int(rs.randint(0, 6))))


tarlalari_uret()

# ====================================================================== 9) adlar
ADLAR = ["Abbey", "Albion", "Alma", "Arundel", "Baker", "Bedford", "Belgrave", "Berkeley", "Bloom", "Brick", "Bridge", "Brook", "Camden",
         "Canon", "Carlton", "Chapel", "Charlotte", "Cheyne", "Church", "Clarence", "Cloth", "Cobb", "Cornwall", "Crown", "Dean", "Devon",
         "Dorset", "Duke", "Eaton", "Elgin", "Essex", "Farrier", "Fleet", "Garrick", "George", "Gloucester", "Gower", "Grange", "Hanover",
         "Harley", "Hatton", "Hobart", "Holland", "Jermyn", "Juniper", "Kendal", "King", "Lambeth", "Lancaster", "Leather", "Lime", "Lombard",
         "Marlow", "Mason", "Milk", "Mill", "Montague", "Mount", "Neal", "Norfolk", "Old Forge", "Orchard", "Oxford", "Paternoster", "Pembroke",
         "Penny", "Portland", "Priory", "Queen", "Regent", "Rectory", "Rose", "Russell", "Saddler", "Savile", "Seymour", "Silver", "Sloane",
         "Somerset", "Spring", "Stable", "Station", "Sussex", "Swan", "Tanner", "Temple", "Thistle", "Tudor", "Vicarage", "Victoria",
         "Warwick", "Weaver", "Wharf", "Willow", "Windsor", "York", "Acorn", "Alder", "Anchor", "Barley", "Beech", "Bishop", "Bramble",
         "Cannon", "Cedar", "Chandler", "Cherry", "Clover", "Copper", "Cutler", "Drover", "Elm", "Falcon", "Fern", "Finch", "Fisher",
         "Forge", "Garland", "Glover", "Hawthorn", "Hazel", "Heron", "Holly", "Ivy", "Jubilee", "Kestrel", "Larch", "Laurel", "Linden",
         "Maple", "Mercer", "Miller", "Nelson", "Oak", "Osprey", "Pilgrim", "Poplar", "Quarry", "Raven", "Rowan", "Saffron", "Sorrel",
         "Tallow", "Thrush", "Turner", "Vine", "Walnut", "Wren", "Yew"]


def adlandir():
    sira = list(rs.permutation(len(ADLAR)))
    sayac = {"n": 0}

    def yeni(ekler):
        i = sayac["n"]
        sayac["n"] += 1
        return ADLAR[sira[i % len(ADLAR)]] + " " + ekler[(i // len(ADLAR) + i) % len(ekler)]

    for y in YOLLAR:
        if y.ad:
            continue
        if y.sinif == ARA:
            y.ad = yeni(["Mews", "Passage", "Yard", "Alley"])
        elif y.cikmaz or y.tur == "cikmaz":
            y.ad = yeni(["Close", "Court"])
        elif y.tur == "meydan":
            y.ad = yeni(["Square", "Gardens"])
        elif y.tur == "sira":
            x, yy_ = int(np.clip(round(y.pts[len(y.pts) // 2][0]), 0, W - 1)), int(np.clip(round(y.pts[len(y.pts) // 2][1]), 0, H - 1))
            dis = zone[yy_, x] in "EB" or ornek(KENT_UZAK, y.pts[len(y.pts) // 2]) < 9
            y.ad = yeni(["Crescent", "Avenue", "Drive", "Gardens", "Way"] if dis else ["Street", "Terrace", "Row", "Street", "Place"])
        else:
            y.ad = yeni(["Lane", "Street", "Hill", "Walk"])


adlandir()


# ====================================================================== 10) çıktı
def sinir_izi(maske):
    """İnce rasterdeki tek parça lekenin dış sınırı (piksel köşelerinden geçen kapalı çokgen, dünya koordinatı)."""
    m = np.pad(maske, 1)
    # kenar parçaları: içerisi solda kalacak şekilde yönlü (saat yönünün tersi)
    sonraki = {}
    ys, xs = np.nonzero(m)
    for r, c in zip(ys, xs):
        if not m[r - 1, c]:
            sonraki.setdefault((c, r), []).append((c + 1, r))
        if not m[r + 1, c]:
            sonraki.setdefault((c + 1, r + 1), []).append((c, r + 1))
        if not m[r, c - 1]:
            sonraki.setdefault((c, r + 1), []).append((c, r))
        if not m[r, c + 1]:
            sonraki.setdefault((c + 1, r), []).append((c + 1, r + 1))
    if not sonraki:
        return None
    en = []
    gorulen = set()
    for bas in list(sonraki):
        if bas in gorulen:
            continue
        zincir = [bas]
        cur = bas
        for _ in range(200000):
            gorulen.add(cur)
            nx_ = sonraki.get(cur)
            if not nx_:
                break
            cur = nx_.pop()
            if cur == bas:
                break
            zincir.append(cur)
        if len(zincir) > len(en):
            en = zincir
    p = np.array(en, float) - 1.0            # dolgu payı
    return np.stack([(p[:, 0] - 0.5) / F - PAD, (p[:, 1] - 0.5) / F - PAD], 1)


def park_cokgenleri(tur_adi="park"):
    out = []
    for k, t in ADA_TUR.items():
        if t != tur_adi:
            continue
        m = ndi.binary_erosion(ADA == k, iterations=1)
        e, n = ndi.label(m)
        if n == 0:
            continue
        boy = ndi.sum(m, e, range(1, n + 1))
        iz = sinir_izi(e == int(np.argmax(boy)) + 1)
        if iz is None or len(iz) < 12:
            continue
        iz = rdp_kapali(iz, 0.3)
        # kapalı Chaikin: köşeler yuvarlanır (basamaklı piksel kenarı yumuşar)
        for _ in range(2):
            iz = np.vstack([np.vstack([a * 0.75 + b * 0.25, a * 0.25 + b * 0.75]) for a, b in zip(iz, np.roll(iz, -1, axis=0))])
        out.append(rdp_kapali(iz, 0.06))
    if tur_adi == "park":
        for b in BAHCELER:
            out.append(rdp_kapali(b, 0.05))
    return [p for p in out if len(p) >= 3 and abs(alan_poly(p)) > 1.0]


PARK_POLY = park_cokgenleri()
MEZAR_POLY = park_cokgenleri("mezarlik")


def yerler_listesi():
    """Haritada adı yazan yerler: tur 0 mahalle merkezi, 1 istasyon, 2 mezarlık, 3 park, 4 okul / kampüs. İstasyonda aci: hattın yönü (°)."""
    out = []
    for m in MAHALLE:
        if kentte(m["c"]):
            out.append(dict(ad=m["ad"], x=round(float(m["c"][0]), 2), y=round(float(m["c"][1]), 2), tur=0, aci=0.0))
    for i in ISTASYON:
        out.append(dict(ad=i["ad"], x=round(float(i["p"][0]), 2), y=round(float(i["p"][1]), 2), tur=1,
                        aci=round(math.degrees(math.atan2(i["yon"][1], i["yon"][0])), 1)))
    for o in OYUK:
        if not o["ada"] or o["ad"] == "City Park":
            continue
        t = {"mezarlik": 2, "park": 3, "okul": 4, "kampus": 4}[o["tur"]]
        out.append(dict(ad=o["ad"], x=round(float(o["c"][0]), 2), y=round(float(o["c"][1]), 2), tur=t, aci=0.0))
    return out


YERLER = yerler_listesi()


def su_cokgeni():
    ii = np.nonzero((NEHIR[:, 0] > -2.6) & (NEHIR[:, 0] < W + 1.6))[0]
    ii = np.arange(ii[0], ii[-1] + 1)
    sol = NEHIR[ii] + NN[ii] * (NEHIR_G[ii] / 2)[:, None]
    sag = NEHIR[ii] - NN[ii] * (NEHIR_G[ii] / 2)[:, None]
    return np.vstack([rdp(sol, 0.05), rdp(sag[::-1], 0.05)])


def nehir_kollari():
    """Şehir dışındaki kollar: dikdörtgenin 1,6 kare içinden başlayıp dışarı doğru."""
    kol = []
    bati = np.nonzero(NEHIR[:, 0] < 1.6)[0]
    if len(bati):
        ii = np.arange(bati[-1], -1, -2)
        kol.append((NEHIR[ii], NEHIR_G[ii]))
    dogu = np.nonzero(NEHIR[:, 0] > W - 2.6)[0]
    if len(dogu):
        ii = np.arange(dogu[0], len(NEHIR), 2)
        kol.append((NEHIR[ii], NEHIR_G[ii]))
    return kol


def y2(a):
    return [round(float(v), 2) for v in a]


def cizgi_json(pts, eps=0.06):
    p = rdp(pts, eps)
    return {"x": y2(p[:, 0]), "y": y2(p[:, 1])}


def satirlar(a):
    return ["".join(str(v) for v in a[yy_]) for yy_ in range(H)]


SU_POLY = su_cokgeni()
plan = {
    "surum": 2, "kaynak": "sehir_uret.py", "tohum": TOHUM, "olcek": 1.0, "W": W, "H": H,
    "merkez": [MX, MY], "meydan": [MX, MY], "belediye": [int(BELEDIYE[0]), int(BELEDIYE[1])], "otogar": [int(OTOGAR[0]), int(OTOGAR[1])],
    "yollar": [dict(genis=bool(y.sinif <= CADDE), sinif=int(y.sinif), ad=y.ad, **cizgi_json(y.pts)) for y in YOLLAR],
    "su": [cizgi_json(SU_POLY, 0.0)],
    "park": [cizgi_json(p, 0.0) for p in PARK_POLY],
    "kare": satirlar(kare), "zone": satirlar(zone), "bolge": satirlar(bolge), "yogunluk": satirlar(yog),
    "kirYollari": [dict(genis=bool(y.sinif <= CADDE), sinif=int(y.sinif), ad=y.ad, **cizgi_json(y.pts, 0.08)) for y in KIR],
    "kirNehir": [{"x": y2(p[:, 0]), "y": y2(p[:, 1]), "g": y2(g)} for p, g in nehir_kollari()],
    "tarlalar": [{"x": y2(t["poly"][:, 0]), "y": y2(t["poly"][:, 1]), "mx": round(t["mx"], 2), "my": round(t["my"], 2),
                  "gen": round(t["gen"], 2), "boy": round(t["boy"], 2), "aci": round(t["aci"], 1), "tur": t["tur"]} for t in TARLA],
    "ciftlikler": {"x": y2([c[0] for c in CIFTLIK]), "y": y2([c[1] for c in CIFTLIK])},
    "demiryolu": [cizgi_json(RAY, 0.05)] if RAY is not None else [],
    "mezarlik": [cizgi_json(p, 0.0) for p in MEZAR_POLY],
    "yerler": YERLER,
    "kirEvleri": {"x": y2([e[0] for e in KIR_EV]), "y": y2([e[1] for e in KIR_EV]), "aci": [round(float(e[2]), 1) for e in KIR_EV],
                  "tur": [int(e[3]) for e in KIR_EV]},
}
if not YAZMA:
    os.makedirs(os.path.dirname(CIKTI), exist_ok=True)
    with open(CIKTI, "w", encoding="utf-8") as f:
        json.dump(plan, f, ensure_ascii=False, separators=(",", ":"))
else:
    with open("/tmp/plan_deneme.json", "w", encoding="utf-8") as f:
        json.dump(plan, f, ensure_ascii=False, separators=(",", ":"))

_on = komsu_say(yolK) > 0
print(f"plan: {W}×{H}, tohum {TOHUM} · yol çizgisi {len(YOLLAR)} (ana {sum(y.sinif == 1 for y in YOLLAR)}, cadde {sum(y.sinif == 2 for y in YOLLAR)}, "
      f"sokak {sum(y.sinif == 3 for y in YOLLAR)}, ara {sum(y.sinif == 4 for y in YOLLAR)}) · yol karesi {int(yolK.sum())} · köprü {int((kare == 'K').sum())} karesi")
print(f"  kentsel kare {int(kent.sum())} · yola bakan arsa {int((_on & kent & ~yolK & ~su & ~np.isin(zone, ['P', 'Z', 'R', '.'])).sum())} · "
      f"park {int((zone == 'P').sum())} · meydan bahçesi {len(BAHCELER)} · kopuk yol karesi {KOPUK} · budanan kör uç {BUDANAN}")
print(f"  seviye bağlantısı için semti kaydırılan kare: {SEVIYE_KAYAN} · taş meydan {len(MEYDANCIK)}")
print(f"  kır: yol {len(KIR)}, tarla {len(TARLA)}, çiftlik {len(CIFTLIK)}, ev {len(KIR_EV)} · merkez {MX},{MY} · belediye {BELEDIYE} · otogar {OTOGAR}")
print(f"  mahalle merkezi {sum(1 for y in YERLER if y['tur'] == 0)} · istasyon {len(ISTASYON)} · demiryolu {uzunluk(RAY) if RAY is not None else 0:.0f} kare "
      f"(şerit karesi {int((zone == 'D').sum())}) · oyuk alan {len(OYUK)} (yerleşmeyen: {', '.join(OYUK_EKSIK) or 'yok'}) · mezarlık karesi {int((zone == 'G').sum())} · "
      f"kampüs / okul karesi {int((zone == 'C').sum())}")
for _h in "OMHEBCWAZPRGD":
    print(f"    bölge {_h}: {int(((zone == _h) & ~yolK & ~su).sum())}", end="")
print()


# ====================================================================== 11) önizleme
def onizleme():
    Z, KEN = 12, 30
    GEN, YUK = (W + 2 * KEN) * Z, (H + 2 * KEN) * Z
    S = 2                                   # 2× çizip küçült (kenar yumuşatma)
    im = Image.new("RGB", (GEN * S, YUK * S), (176, 204, 150))
    d = ImageDraw.Draw(im)

    def P(p):
        return ((p[0] + KEN + 0.5) * Z * S, (H + KEN - 0.5 - p[1]) * Z * S)

    def cizgi(pts, renk, kalin):
        d.line([P(p) for p in pts], fill=renk, width=max(1, int(round(kalin * S))), joint="curve")

    # tarlalar
    TR = [(204, 181, 107), (140, 168, 84), (140, 110, 77), (214, 199, 92), (122, 153, 82), (186, 166, 115)]
    for t in TARLA:
        c = TR[t["tur"]]
        d.polygon([P(p) for p in t["poly"]], fill=c, outline=(96, 128, 78))
        # ekin sıraları: içe sığan dikdörtgende, uzun kenara paralel
        a = math.radians(t["aci"])
        ca, sa = math.cos(a), math.sin(a)
        hu, hv = t["gen"] / 2, t["boy"] / 2
        koyu = tuple(int(v * 0.9) for v in c)
        v = -hv + 0.25
        while v < hv:
            d.line([P((t["mx"] - hu * ca - v * sa, t["my"] - hu * sa + v * ca)), P((t["mx"] + hu * ca - v * sa, t["my"] + hu * sa + v * ca))],
                   fill=koyu, width=S)
            v += 0.5
    # nehir (dünyanın tamamında)
    d.polygon([P(p) for p in np.vstack([NEHIR + NN * (NEHIR_G / 2)[:, None], (NEHIR - NN * (NEHIR_G / 2)[:, None])[::-1]])], fill=(118, 172, 222))
    # şehir kareleri
    ZR = {"O": (226, 190, 168), "M": (232, 176, 160), "H": (206, 186, 222), "E": (236, 226, 190), "B": (224, 232, 196), "C": (236, 222, 176),
          "W": (198, 204, 214), "A": (244, 206, 150), "Z": (236, 228, 208), "P": (150, 200, 130), "R": (176, 210, 156),
          "G": (170, 196, 160), "D": (200, 192, 178)}
    BR = {"O": (176, 104, 84), "M": (196, 84, 80), "H": (120, 96, 168), "E": (196, 150, 96), "B": (170, 140, 100), "C": (190, 160, 90),
          "W": (112, 126, 148), "A": (214, 140, 60)}
    for yy_ in range(H):
        for x in range(W):
            z = zone[yy_, x]
            if z == "." or su[yy_, x]:
                continue
            x0, y0 = P((x - 0.5, yy_ + 0.5))
            x1, y1 = P((x + 0.5, yy_ - 0.5))
            d.rectangle([x0, y0, x1, y1], fill=ZR[z])
    for p in PARK_POLY:
        d.polygon([P(q) for q in p], fill=(132, 190, 112))
    for p in MEZAR_POLY:
        d.polygon([P(q) for q in p], fill=(160, 188, 150), outline=(110, 120, 110))
        xs_, ys_ = p[:, 0], p[:, 1]
        for gx in np.arange(math.floor(xs_.min()), xs_.max(), 0.5):
            for gy in np.arange(math.floor(ys_.min()), ys_.max(), 0.7):
                c_, r_ = fpx((gx, gy))
                if OYUK_F[r_, c_] and ornek(ADA, (gx, gy)) in [o["ada"] for o in OYUK if o["tur"] == "mezarlik"]:
                    a_, b_ = P((gx - 0.08, gy + 0.12)), P((gx + 0.08, gy - 0.12))
                    d.rectangle([a_[0], a_[1], b_[0], b_[1]], fill=(226, 226, 220))
    d.polygon([P(p) for p in SU_POLY], fill=(118, 172, 222))
    # binalar: yola bakan arsa koyu, arka arsa (bahçe / avlu) açık
    for yy_ in range(H):
        for x in range(W):
            z = zone[yy_, x]
            if z not in BR or yolK[yy_, x] or su[yy_, x]:
                continue
            on = _on[yy_, x]
            pay = {"E": 0.22, "B": 0.27, "C": 0.2}.get(z, 0.1)
            if not on:
                if (x * 7 + yy_ * 13) % 10 >= yog[yy_, x]:
                    continue
                pay += 0.08
            x0, y0 = P((x - 0.5 + pay, yy_ + 0.5 - pay))
            x1, y1 = P((x + 0.5 - pay, yy_ - 0.5 + pay))
            c = BR[z] if on else tuple(int(v * 0.5 + w * 0.5) for v, w in zip(BR[z], ZR[z]))
            d.rectangle([x0, y0, x1, y1], fill=c)
    # kır yolları ve yerleşimi
    for y in KIR:
        cizgi(y.pts, (120, 110, 90), {1: 4.5, 2: 3.5}.get(y.sinif, 2.5))
        cizgi(y.pts, (250, 246, 232), {1: 3, 2: 2}.get(y.sinif, 1.2))
    for (x, y_, a, t) in KIR_EV:
        x0, y0 = P((x - 0.3, y_ + 0.3))
        x1, y1 = P((x + 0.3, y_ - 0.3))
        d.rectangle([x0, y0, x1, y1], fill=(170, 96, 70) if t < 2 else (70, 70, 80))
    for c in CIFTLIK:
        x0, y0 = P((c[0] - 0.4, c[1] + 0.3))
        x1, y1 = P((c[0] + 0.4, c[1] - 0.3))
        d.rectangle([x0, y0, x1, y1], fill=(178, 60, 48))
        x0, y0 = P((c[0] + 0.5, c[1] + 0.5))
        x1, y1 = P((c[0] + 1.3, c[1] - 0.1))
        d.rectangle([x0, y0, x1, y1], fill=(150, 44, 40))
    # şehir yolları: önce kenar (kaldırım), sonra asfalt; sınıfa göre genişlik (kare × Z)
    GENISLIK = {ANA: 0.80, CADDE: 0.62, SOKAK: 0.46, ARA: 0.30}
    for sinif in (ARA, SOKAK, CADDE, ANA):
        for y in YOLLAR:
            if y.sinif == sinif:
                cizgi(y.pts, (150, 146, 138), GENISLIK[sinif] * Z + 2.4)
    RENK = {ANA: (255, 236, 150), CADDE: (255, 252, 226), SOKAK: (255, 255, 255), ARA: (238, 232, 222)}
    for sinif in (ARA, SOKAK, CADDE, ANA):
        for y in YOLLAR:
            if y.sinif == sinif:
                cizgi(y.pts, RENK[sinif], GENISLIK[sinif] * Z)
    for y in YOLLAR:
        if y.tur == "kopru":
            cizgi(y.pts, (120, 110, 100), GENISLIK[y.sinif] * Z + 3.5)
            cizgi(y.pts, RENK[y.sinif], GENISLIK[y.sinif] * Z)
    # demiryolu: balast şeridi, ray ve traversler; istasyonlar
    if RAY is not None:
        cizgi(RAY, (150, 140, 128), 5.0)
        cizgi(RAY, (60, 60, 66), 1.6)
        t_, n_ = teget_normal(RAY, 4)
        for i in range(0, len(RAY), 3):
            d.line([P(RAY[i] + n_[i] * 0.22), P(RAY[i] - n_[i] * 0.22)], fill=(60, 60, 66), width=S)
        for i_ in ISTASYON:
            nn_ = np.array([-i_["yon"][1], i_["yon"][0]])
            L_ = 4.6 if i_["terminal"] else 2.0
            b0 = i_["p"] if i_["terminal"] else i_["p"] - i_["yon"] * L_
            b1 = i_["p"] + i_["yon"] * L_
            for k_ in (-1, 1):
                d.polygon([P(b0 + nn_ * k_ * 0.45), P(b1 + nn_ * k_ * 0.45), P(b1 + nn_ * k_ * 1.0), P(b0 + nn_ * k_ * 1.0)], fill=(214, 210, 200), outline=(120, 116, 110))
            if i_["terminal"]:
                g0 = i_["p"] - i_["yon"] * 0.5
                d.polygon([P(g0 + nn_ * 1.5), P(g0 - nn_ * 1.5), P(g0 - nn_ * 1.5 + i_["yon"] * 1.0), P(g0 + nn_ * 1.5 + i_["yon"] * 1.0)], fill=(168, 92, 70), outline=(90, 50, 40))
    # semt sınırları ve önemli yerler
    for yy_ in range(H):
        for x in range(W):
            if not (kent[yy_, x] or su[yy_, x]):
                continue
            for dx, dy in ((1, 0), (0, 1)):
                if x + dx < W and yy_ + dy < H and (kent[yy_ + dy, x + dx] or su[yy_ + dy, x + dx]) and bolge[yy_, x] != bolge[yy_ + dy, x + dx]:
                    a = P((x + 0.5, yy_ + 0.5))
                    b = P((x + 0.5, yy_ - 0.5)) if dx else P((x - 0.5, yy_ + 0.5))
                    d.line([a, b], fill=(60, 90, 200), width=S)
    for ad, p, renk in (("M", (MX, MY), (220, 30, 30)), ("B", BELEDIYE, (20, 120, 40)), ("O", OTOGAR, (200, 120, 0))):
        x0, y0 = P((p[0] - 0.9, p[1] + 0.9))
        x1, y1 = P((p[0] + 0.9, p[1] - 0.9))
        d.ellipse([x0, y0, x1, y1], outline=renk, width=3 * S)
    # yer adları
    try:
        from PIL import ImageFont
        font = ImageFont.truetype("DejaVuSans-Bold.ttf", 13 * S)
    except Exception:
        font = None
    YR = {0: (120, 30, 30), 1: (30, 30, 40), 2: (70, 80, 70), 3: (20, 100, 40), 4: (130, 90, 10)}
    etiket = [(y_["ad"], (y_["x"], y_["y"]), YR[y_["tur"]]) for y_ in YERLER] + [("City Park", BELEDIYE, YR[3]), ("Clock Tower Sq.", (MX, MY + 2.2), (160, 20, 20))]
    for ad, p, renk in etiket:
        x_, y_ = P(p)
        kutu = d.textbbox((0, 0), ad, font=font)
        gw_, gh_ = kutu[2] - kutu[0], kutu[3] - kutu[1]
        for ox in (-S, 0, S):
            for oy in (-S, 0, S):
                d.text((x_ - gw_ / 2 + ox, y_ - gh_ / 2 + oy), ad, fill=(255, 255, 255), font=font)
        d.text((x_ - gw_ / 2, y_ - gh_ / 2), ad, fill=renk, font=font)
    # plan dikdörtgeni
    x0, y0 = P((-0.5, H - 0.5))
    x1, y1 = P((W - 0.5, -0.5))
    d.rectangle([x0, y0, x1, y1], outline=(200, 60, 60), width=S)
    im = im.resize((GEN, YUK), Image.LANCZOS)
    im = im.quantize(255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)       # depo şişmesin: paletli PNG
    im.save(ONIZLEME, optimize=True)
    print("önizleme:", ONIZLEME, im.size)


onizleme()
