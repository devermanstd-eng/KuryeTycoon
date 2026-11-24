"""
Kurye Tycoon — organik şehir planı üretici.
Bir şehir haritası görüntüsünden (beyaz yollar, mavi su, yeşil park, gri bina) oyunun okuyacağı planı üretir:
  Resources/Sehir/plan.json  → yol çizgileri (dünya koordinatı), nehir/park konturları, kare rasterleri (yol, su, köprü,
                                park, bina yoğunluğu), bölge türleri (zone), semtler (1–5) ve önemli yerler.
Kullanım:  python3 plan_uret.py [kaynak_harita.png] [ölçek_px_kare=10]
Gerekenler: numpy, scipy, scikit-image, pillow.
Koordinat: 1 kare = 1 dünya birimi. x doğuya, y kuzeye artar (görüntünün üstü kuzey).
"""
import json, math, os, sys, warnings
warnings.filterwarnings("ignore")
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi
from skimage.morphology import skeletonize, remove_small_objects
from skimage import measure

BURA = os.path.dirname(os.path.abspath(__file__))
KAYNAK = sys.argv[1] if len(sys.argv) > 1 else os.path.join(BURA, "kaynak_harita.png")
S = float(sys.argv[2]) if len(sys.argv) > 2 else 9.0           # kaç piksel = 1 kare
YOL_ESIK = float(sys.argv[3]) if len(sys.argv) > 3 else 2.0    # yol sayılacak en dar beyaz şeridin yarı genişliği (piksel)
# Önemli yerler (kaynak haritanın pikselleri; x, y): yıldız kavşak merkez, büyük üçgen park belediye, nehrin güneyinde otogar
ELLE = {"merkez": (268, 388), "belediye": (212, 268), "otogar": (120, 610)}
CIKTI = os.path.join(BURA, "..", "..", "Resources", "Sehir", "plan.json")
ONIZLEME = os.path.join(BURA, "onizleme.png")

im = np.array(Image.open(KAYNAK).convert("RGB")).astype(int)
r, g, b = im[..., 0], im[..., 1], im[..., 2]
HP, WP = r.shape
beyaz = (r >= 236) & (g >= 236) & (b >= 236)
mavi = (b > r + 25) & (b > g + 5)
yesil = (g > r + 15) & (g >= b) & ~mavi
gri = (abs(r - g) < 10) & (abs(g - b) < 10) & (r < 215)

# ------------------------------------------------------------------ yol ağı (iskelet → çizgiler)
dt = ndi.distance_transform_edt(beyaz)
yol = ndi.binary_dilation(dt >= YOL_ESIK, iterations=2) & beyaz
yol = remove_small_objects(yol, 150)
sk = skeletonize(yol)
sk &= ~(ndi.uniform_filter(yesil.astype(float), 9) > 0.55)          # park içi patikalar yol değil

KOM = [(-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1)]


def komsu(p):
    y, x = p
    for dy, dx in KOM:
        yy, xx = y + dy, x + dx
        if 0 <= yy < HP and 0 <= xx < WP and sk[yy, xx]:
            yield (yy, xx)


def derece(p):
    return sum(1 for _ in komsu(p))


for _ in range(4):                                                   # kısa çıkıntıları buda
    for u in [p for p in zip(*np.nonzero(sk)) if derece(p) == 1]:
        hat, cur, prev = [u], u, None
        while True:
            ns = [n for n in komsu(cur) if n != prev and n not in hat]
            if len(ns) != 1:
                break
            prev, cur = cur, ns[0]
            hat.append(cur)
            if derece(cur) != 2 or len(hat) > 18:
                break
        if len(hat) <= 14 and derece(cur) >= 3:
            for p in hat[:-1]:
                sk[p] = False
sk = remove_small_objects(sk, 30, connectivity=2)

noktalar = set(zip(*np.nonzero(sk)))
dugum = set(p for p in noktalar if derece(p) != 2)
gorulen, kenarlar = set(), []
for d in dugum:
    for n in komsu(d):
        if (d, n) in gorulen:
            continue
        hat, prev, cur = [d, n], d, n
        while cur not in dugum and len(hat) < 4000:
            ns = [m for m in komsu(cur) if m != prev]
            if not ns:
                break
            ns.sort(key=lambda m: abs(m[0] - cur[0]) + abs(m[1] - cur[1]))
            prev, cur = cur, ns[0]
            hat.append(cur)
        gorulen.add((d, n))
        gorulen.add((cur, hat[-2]))
        kenarlar.append(hat)
# Halka şeklindeki (düğümsüz) yollar
kalan = noktalar - set(p for k in kenarlar for p in k)
while kalan:
    p0 = kalan.pop()
    hat, prev, cur = [p0], None, p0
    while True:
        ns = [m for m in komsu(cur) if m != prev and m in kalan]
        if not ns:
            break
        prev, cur = cur, ns[0]
        kalan.discard(cur)
        hat.append(cur)
    if len(hat) > 10:
        kenarlar.append(hat + [p0])


def rdp(c, eps):
    if len(c) < 3:
        return c
    a, bb = np.array(c[0], float), np.array(c[-1], float)
    v = bb - a
    L = np.linalg.norm(v) + 1e-9
    dmax, idx = 0.0, 0
    for i in range(1, len(c) - 1):
        p = np.array(c[i], float)
        d = abs(v[0] * (a[1] - p[1]) - v[1] * (a[0] - p[0])) / L
        if d > dmax:
            dmax, idx = d, i
    if dmax > eps:
        return rdp(c[: idx + 1], eps)[:-1] + rdp(c[idx:], eps)
    return [c[0], c[-1]]


def dunya(p):           # (piksel y, x) → dünya (x, y)
    return (p[1] / S, (HP - 1 - p[0]) / S)


yollar = []
for k in kenarlar:
    if len(k) < 3:
        continue
    gen = float(np.median([dt[p] for p in k]))
    c = rdp(k, 1.5)
    yollar.append({"genis": bool(gen >= 3.5), "n": [[round(dunya(p)[0], 2), round(dunya(p)[1], 2)] for p in c]})

# ------------------------------------------------------------------ kare rasterleri
W, H = int(math.ceil(WP / S)), int(math.ceil(HP / S))


def kare_ort(maske):
    pad = np.zeros((int(H * S) + 1, int(W * S) + 1), float)
    pad[:HP, :WP] = maske
    out = np.zeros((H, W))
    for ty in range(H):
        for tx in range(W):
            py0 = HP - (ty + 1) * S + S / 2.0            # karenin piksel aralığı (kare merkezi = x, y)
            py0 = int(round(HP - 1 - (ty + 0.5) * S)); px0 = int(round((tx - 0.5) * S))
            blk = maske[max(0, py0):max(0, py0 + int(S)), max(0, px0):max(0, px0 + int(S))]
            out[ty, tx] = blk.mean() if blk.size else 0.0
    return out


su = kare_ort(mavi) > 0.3                        # yarısından azı su olan kareye de bina konmasın (nehre taşmasın)
park = kare_ort(yesil) > 0.45
bina = kare_ort(gri)

yolK = np.zeros((H, W), bool)


def isaretle(x, y):
    xi, yi = int(round(x)), int(round(y))
    if 0 <= xi < W and 0 <= yi < H:
        yolK[yi, xi] = True


for yo in yollar:                                        # 4-bağlı (supercover) çizgi: kare kare yürünebilir
    n = yo["n"]
    for (x0, y0), (x1, y1) in zip(n[:-1], n[1:]):
        cx, cy = int(round(x0)), int(round(y0))
        ex, ey = int(round(x1)), int(round(y1))
        isaretle(cx, cy)
        guard = 0
        while (cx, cy) != (ex, ey) and guard < 500:
            guard += 1
            dx, dy = ex - cx, ey - cy
            # segmente en yakın kalan adımı seç (x mi y mi)
            seg = np.array([x1 - x0, y1 - y0]); L2 = float(seg @ seg) + 1e-9

            def uzak(px, py):
                t = max(0.0, min(1.0, ((px - x0) * seg[0] + (py - y0) * seg[1]) / L2))
                return math.hypot(px - (x0 + t * seg[0]), py - (y0 + t * seg[1]))
            adaylar = []
            if dx:
                adaylar.append((uzak(cx + np.sign(dx), cy), cx + int(np.sign(dx)), cy))
            if dy:
                adaylar.append((uzak(cx, cy + np.sign(dy)), cx, cy + int(np.sign(dy))))
            _, cx, cy = min(adaylar)
            isaretle(cx, cy)

# Ana ağa bağlı olmayan küçük yol parçaları atılır (kuryeler oraya gidemez)
etk, sayi = ndi.label(yolK)
if sayi > 1:
    boy = ndi.sum(yolK, etk, range(1, sayi + 1))
    ana = int(np.argmax(boy)) + 1
    yolK &= etk == ana
# Ana ağa bağlı olmayan çizgiler de atılır (çizilip yürünemeyen yol kalmasın)
def ana_agda(yo):
    n = yo["n"]
    on = 0
    for p in n:
        xi, yi = int(round(p[0])), int(round(p[1]))
        if 0 <= xi < W and 0 <= yi < H and yolK[yi, xi]:
            on += 1
    return on >= max(1, len(n) // 2)


yollar = [yo for yo in yollar if ana_agda(yo)]
kopru = yolK & su
su &= ~yolK

# ------------------------------------------------------------------ bölge türleri ve semtler
yogun = ndi.uniform_filter(bina, 5)
yesilAlan = ndi.uniform_filter(park.astype(float), 7)
merkezPx = None
# Merkez: en çok yolun buluştuğu yoğun nokta (yıldız kavşak)
derece_k = ndi.convolve(yolK.astype(int), np.ones((3, 3), int), mode="constant") * yolK
skor = ndi.uniform_filter(yogun, 9) * 2 + ndi.uniform_filter(derece_k.astype(float), 7)
skor[: H // 4] = 0; skor[-H // 6:] = 0
my, mx = np.unravel_index(int(np.argmax(skor)), skor.shape)
merkez = (int(mx), int(my))


def px_kare(p):
    return (int(round(p[0] / S)), int(round((HP - 1 - p[1]) / S)))


if "merkez" in ELLE:
    merkez = px_kare(ELLE["merkez"])

zone = np.full((H, W), "M")
for y in range(H):
    for x in range(W):
        if su[y, x]:
            zone[y, x] = "R"
            continue
        d = math.hypot(x - merkez[0], y - merkez[1])
        if park[y, x] and bina[y, x] < 0.15:
            zone[y, x] = "P"
        elif yesilAlan[y, x] > 0.42 and bina[y, x] > 0.08:
            zone[y, x] = "C"
        elif d < 7 and yogun[y, x] > 0.28:
            zone[y, x] = "H"
        elif yogun[y, x] > 0.36 or d < 12:
            zone[y, x] = "O"
        elif yogun[y, x] > 0.22:
            zone[y, x] = "M"
        else:
            zone[y, x] = "E"
# Nehre bakan karelerden birkaçı liman/sanayi
kenarSu = ndi.binary_dilation(su, iterations=2) & ~su & ~yolK
for y, x in zip(*np.nonzero(kenarSu)):
    if zone[y, x] in "ME" and (x + y * 3) % 5 == 0:
        zone[y, x] = "W"

# Semtler: merkez çevresi 1; nehrin güneybatısı 4 (West Harbour); kuzeybatı 5 (Green Hill); kuzeydoğu 3; doğu/güneydoğu 2
nehirY = {}
for x in range(W):
    ys = np.nonzero(su[:, x])[0]
    nehirY[x] = float(ys.mean()) if len(ys) else None
bolge = np.ones((H, W), int)
for y in range(H):
    for x in range(W):
        dx, dy = x - merkez[0], y - merkez[1]
        if math.hypot(dx * 0.9, dy) < 13:
            bolge[y, x] = 1
            continue
        ny = nehirY.get(x)
        guneyNehir = ny is not None and y < ny
        if guneyNehir or (dy < -16 and dx < 4):
            bolge[y, x] = 4
        elif dy > 8 and dx < 2:
            bolge[y, x] = 5
        elif dy > 2:
            bolge[y, x] = 3
        else:
            bolge[y, x] = 2


# ------------------------------------------------------------------ önemli yerler
def en_yakin(maske, hedef):
    ys, xs = np.nonzero(maske)
    if len(xs) == 0:
        return None
    i = int(np.argmin((xs - hedef[0]) ** 2 + (ys - hedef[1]) ** 2))
    return (int(xs[i]), int(ys[i]))


bos = ~yolK & ~su
meydan = en_yakin(bos & ~park, merkez)
# Belediye parkı: merkeze en yakın büyük park
etkP, sayP = ndi.label(park & ~yolK)
belediye = None
if sayP:
    boyP = ndi.sum(park, etkP, range(1, sayP + 1))
    adaylar = [i + 1 for i in range(sayP) if boyP[i] >= 6]
    if adaylar:
        en = min(adaylar, key=lambda i: math.hypot(*(np.array(ndi.center_of_mass(park, etkP, i))[::-1] - np.array(merkez))))
        cy, cx = ndi.center_of_mass(park, etkP, en)
        belediye = en_yakin(etkP == en, (cx, cy))
otogar = en_yakin(bos & ~park, px_kare(ELLE["otogar"])) if "otogar" in ELLE else en_yakin(bos & (bolge == 4) & ~park, (merkez[0] - 10, merkez[1] - 18))
if "belediye" in ELLE:
    belediye = en_yakin(park & ~yolK, px_kare(ELLE["belediye"])) or belediye


def kontur(maske, kenar=0.5, enaz=8):
    out = []
    for c in measure.find_contours(np.pad(maske.astype(float), 1), kenar):
        if len(c) < enaz:
            continue
        c = [tuple(p) for p in c]
        m = len(c) // 2                                     # kapalı halka: ikiye bölüp sadeleştir (uçlar aynı nokta)
        c = rdp(c[: m + 1], 0.8)[:-1] + rdp(c[m:], 0.8)
        out.append([[round(dunya((p[0] - 1, p[1] - 1))[0], 2), round(dunya((p[0] - 1, p[1] - 1))[1], 2)] for p in c])
    return out


suKontur = kontur(ndi.binary_closing(mavi, iterations=2))
parkKontur = kontur(ndi.binary_opening(yesil, iterations=1) & ~beyaz, enaz=60)


def satirlar(f):
    return ["".join(f(x, y) for x in range(W)) for y in range(H)]


plan = {
    "surum": 1, "kaynak": os.path.basename(KAYNAK), "olcek": S, "W": W, "H": H,
    "merkez": list(merkez), "meydan": list(meydan) if meydan else list(merkez),
    "belediye": list(belediye) if belediye else list(merkez), "otogar": list(otogar) if otogar else [2, 2],
    "yollar": [{"genis": yo["genis"], "x": [p[0] for p in yo["n"]], "y": [p[1] for p in yo["n"]]} for yo in yollar],
    "su": [{"x": [p[0] for p in c], "y": [p[1] for p in c]} for c in suKontur],
    "park": [{"x": [p[0] for p in c], "y": [p[1] for p in c]} for c in parkKontur],
    # kare: Y yol, K köprü, S su, P park, . yapı arsası
    "kare": satirlar(lambda x, y: "K" if kopru[y, x] else "Y" if yolK[y, x] else "S" if su[y, x] else "P" if park[y, x] else "."),
    "zone": satirlar(lambda x, y: zone[y, x]),
    "bolge": satirlar(lambda x, y: str(bolge[y, x])),
    "yogunluk": satirlar(lambda x, y: str(min(9, int(bina[y, x] * 10)))),
}
os.makedirs(os.path.dirname(CIKTI), exist_ok=True)
with open(CIKTI, "w", encoding="utf-8") as f:
    json.dump(plan, f, ensure_ascii=False, separators=(",", ":"))
print("plan:", CIKTI, W, "x", H, "yol çizgisi", len(yollar), "yol karesi", int(yolK.sum()), "köprü", int(kopru.sum()),
      "merkez", merkez, "belediye", belediye, "otogar", otogar)

# ------------------------------------------------------------------ önizleme (kare + çizgi)
Z = 12
renk = {"O": (196, 120, 96), "H": (210, 150, 165), "E": (150, 185, 120), "M": (170, 170, 185), "C": (225, 205, 150),
        "P": (120, 180, 100), "W": (130, 165, 160), "R": (110, 170, 220)}
img = Image.new("RGB", (W * Z, H * Z), (230, 228, 220))
dr = ImageDraw.Draw(img)
for y in range(H):
    for x in range(W):
        Y = (H - 1 - y) * Z
        c = renk.get(zone[y, x], (200, 200, 200))
        if yolK[y, x]:
            c = (150, 140, 120) if kopru[y, x] else (60, 62, 70)
        dr.rectangle([x * Z, Y, x * Z + Z - 1, Y + Z - 1], fill=c)
for yo in yollar:
    dr.line([(p[0] * Z + Z / 2, (H - 1 - p[1]) * Z + Z / 2) for p in yo["n"]], fill=(250, 250, 250), width=3 if yo["genis"] else 1)
for c in suKontur:
    dr.line([(p[0] * Z + Z / 2, (H - 1 - p[1]) * Z + Z / 2) for p in c], fill=(0, 60, 160), width=2)
for ad, p in (("M", merkez), ("B", belediye), ("O", otogar)):
    if p:
        dr.ellipse([p[0] * Z - 4, (H - 1 - p[1]) * Z - 4, p[0] * Z + Z + 4, (H - 1 - p[1]) * Z + Z + 4], outline=(255, 0, 0), width=3)
img.save(ONIZLEME)
