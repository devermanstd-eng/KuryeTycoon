# Kurye Tycoon · gerçekçi çim kartı dokusu (atlas) üretici. Çıktı: CimKart.png (2048×1024, 4×2 hücre, RGBA)
# Hücreler: 0 uzun ot · 1 kısa çim · 2 tohum başlı ot · 3 kuru ot · 4 eğrelti · 5–7 kır çiçekleri
import sys, math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SS = 2                      # süper örnekleme
CW, CH = 512, 512           # hücre
W, H = CW * 4, CH * 2
img = Image.new("RGBA", (W * SS, H * SS), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

def karis(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
def jit(c, j, rnd): return tuple(max(0, min(255, int(v + rnd.uniform(-j, j)))) for v in c)

def bezier(p0, p1, p2, t):
    u = 1 - t
    return (u*u*p0[0] + 2*u*t*p1[0] + t*t*p2[0], u*u*p0[1] + 2*u*t*p1[1] + t*t*p2[1])

def yaprak(ox, oy, taban, uc, kontrol, en, renkler, rnd, parca=14, damar=True):
    """Kökten uca daralan eğri yaprak; renk kökten uca kademeli (koyu dip → açık uç)."""
    pts = [bezier(taban, kontrol, uc, i / parca) for i in range(parca + 1)]
    for i in range(parca):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy) or 1
        nx, ny = -dy / L, dx / L
        w0 = en * (1 - i / parca) ** 0.8; w1 = en * (1 - (i + 1) / parca) ** 0.8
        t = (i + 0.5) / parca
        if t < 0.5: c = karis(renkler[0], renkler[1], t / 0.5)
        else: c = karis(renkler[1], renkler[2], (t - 0.5) / 0.5)
        c = jit(c, 6, rnd)
        poly = [(ox + (x0 + nx * w0) * SS, oy + (y0 + ny * w0) * SS), (ox + (x1 + nx * w1) * SS, oy + (y1 + ny * w1) * SS),
                (ox + (x1 - nx * w1) * SS, oy + (y1 - ny * w1) * SS), (ox + (x0 - nx * w0) * SS, oy + (y0 - ny * w0) * SS)]
        d.polygon(poly, fill=c + (255,))
        if damar and w0 > 1.6:
            lc = tuple(min(255, int(v * 1.12 + 8)) for v in c)
            d.line([(ox + x0 * SS, oy + y0 * SS), (ox + x1 * SS, oy + y1 * SS)], fill=lc + (255,), width=max(1, int(w0 * 0.35 * SS)))
    return pts

# Renk paletleri (sRGB): dip, orta, uç
TAZE = [((38, 52, 22), (88, 120, 42), (150, 170, 78)), ((34, 50, 20), (76, 112, 38), (128, 158, 64)), ((44, 58, 24), (104, 128, 48), (176, 178, 96))]
SAMAN = [((60, 58, 30), (150, 138, 82), (206, 190, 130)), ((58, 54, 28), (132, 120, 70), (196, 176, 118)), ((52, 56, 28), (128, 132, 70), (190, 182, 120))]

def ot_obegi(cx, cy, rnd, adet, boyAr, yayAr, sarkAr, enAr, kuruOran, alt=6):
    ox, oy = cx * CW * SS, cy * CH * SS
    yapraklar = []
    for _ in range(adet):
        bx = CW / 2 + rnd.gauss(0, CW * 0.13)
        by = CH - alt - rnd.uniform(0, 8)
        boy = rnd.uniform(*boyAr) * CH
        yon = rnd.uniform(-1, 1)
        yay = yon * rnd.uniform(*yayAr) * CW
        sark = rnd.uniform(*sarkAr)
        ux = bx + yay; uy = by - boy * (1 - sark * 0.5)
        kx = bx + yay * 0.25; ky = by - boy * rnd.uniform(0.85, 1.05)
        ux = max(6, min(CW - 6, ux)); uy = max(6, uy)
        pal = rnd.choice(SAMAN) if rnd.random() < kuruOran else rnd.choice(TAZE)
        yapraklar.append((boy, (bx, by), (ux, uy), (kx, ky), rnd.uniform(*enAr), pal))
    yapraklar.sort(key=lambda y: -y[0] * 0 + rnd.random())  # karışık sıra: önde / arkada
    for (_, b, u, k, en, pal) in yapraklar:
        yaprak(ox, oy, b, u, k, en, pal, rnd)

def tohum_saplari(cx, cy, rnd, adet):
    ox, oy = cx * CW * SS, cy * CH * SS
    renkler = [(196, 156, 140), (214, 196, 150), (170, 130, 140), (204, 178, 128)]
    for _ in range(adet):
        bx = CW / 2 + rnd.gauss(0, CW * 0.1); by = CH - 8
        boy = rnd.uniform(0.7, 0.95) * CH
        egil = rnd.uniform(-0.25, 0.25) * CW
        u = (max(10, min(CW - 10, bx + egil)), by - boy)
        k = (bx + egil * 0.2, by - boy * 0.7)
        pts = yaprak(ox, oy, (bx, by), u, k, 1.6, ((70, 80, 40), (120, 130, 70), (160, 150, 100)), rnd, damar=False)
        rc = rnd.choice(renkler)
        # Salkım: sapın üst %35'inde, sapa göre iki yana sarkan küçük başakçıklar
        for i in range(int(len(pts) * 0.62), len(pts)):
            x, y = pts[i]
            for _ in range(3):
                a = rnd.uniform(-2.3, -0.8) if rnd.random() < 0.5 else rnd.uniform(-2.3, -0.8) + math.pi * 0.95
                L = rnd.uniform(8, 20)
                ex, ey = x + math.cos(a) * L, y - math.sin(a) * L * 0.4 + L * 0.5
                c = jit(rc, 14, rnd)
                d.line([(ox + x * SS, oy + y * SS), (ox + ex * SS, oy + ey * SS)], fill=(120, 110, 80, 255), width=SS)
                r = rnd.uniform(2.2, 3.6) * SS
                d.ellipse([ox + ex * SS - r * 0.6, oy + ey * SS - r, ox + ex * SS + r * 0.6, oy + ey * SS + r], fill=c + (255,))

def egrelti(cx, cy, rnd):
    ox, oy = cx * CW * SS, cy * CH * SS
    for j in range(11):
        bx = CW / 2 + rnd.gauss(0, 10); by = CH - 6
        yon = (j / 10 * 2 - 1) * 0.95 + rnd.uniform(-0.08, 0.08)
        boy = rnd.uniform(0.6, 0.9) * CH * (1 - abs(yon) * 0.25)
        u = (max(30, min(CW - 30, bx + yon * CW * 0.45)), by - boy * (0.85 - abs(yon) * 0.6))
        k = (bx + yon * CW * 0.15, by - boy * 1.05)
        pal = rnd.choice([((30, 50, 18), (70, 112, 34), (120, 160, 56)), ((34, 54, 20), (84, 122, 40), (140, 170, 66))])
        pts = yaprak(ox, oy, (bx, by), u, k, 2.2, pal, rnd, parca=22, damar=False)
        for i in range(2, len(pts) - 1):
            x, y = pts[i]; x2, y2 = pts[i + 1]
            dx, dy = x2 - x, y2 - y; L = math.hypot(dx, dy) or 1
            nx, ny = -dy / L, dx / L
            t = i / len(pts)
            uz = 46 * math.sin(t * math.pi) * (1.05 - t * 0.5)
            c = jit(karis(pal[1], pal[2], t), 8, rnd)
            for sg in (-1, 1):
                tip = (x + nx * uz * sg + dx / L * uz * 0.35, y + ny * uz * sg + dy / L * uz * 0.35 + uz * 0.25)
                d.polygon([(ox + (x - dx / L * 3) * SS, oy + (y - dy / L * 3) * SS), (ox + (x + dx / L * 3) * SS, oy + (y + dy / L * 3) * SS),
                           (ox + tip[0] * SS, oy + tip[1] * SS)], fill=c + (255,))

def cicekler(cx, cy, rnd, palet, adet):
    ox, oy = cx * CW * SS, cy * CH * SS
    ot_obegi(cx, cy, rnd, 110, (0.25, 0.5), (0.05, 0.3), (0.0, 0.35), (2.4, 4.0), 0.25)
    for _ in range(adet):
        bx = CW / 2 + rnd.gauss(0, CW * 0.17); by = CH - 8
        boy = rnd.uniform(0.32, 0.58) * CH
        u = (max(14, min(CW - 14, bx + rnd.uniform(-40, 40))), by - boy)
        k = (bx, by - boy * 0.6)
        yaprak(ox, oy, (bx, by), u, k, 1.5, ((50, 70, 30), (80, 110, 40), (100, 130, 50)), rnd, damar=False)
        tac, gobek = rnd.choice(palet)
        x, y = u; r = rnd.uniform(7, 12)
        for i in range(rnd.choice([5, 6, 8])):
            a = i / 6 * math.pi * 2 + rnd.uniform(-0.2, 0.2)
            px_, py_ = x + math.cos(a) * r * 0.75, y + math.sin(a) * r * 0.45
            rr = r * 0.55
            d.ellipse([ox + (px_ - rr) * SS, oy + (py_ - rr * 0.7) * SS, ox + (px_ + rr) * SS, oy + (py_ + rr * 0.7) * SS], fill=jit(tac, 10, rnd) + (255,))
        g = r * 0.38
        d.ellipse([ox + (x - g) * SS, oy + (y - g) * SS, ox + (x + g) * SS, oy + (y + g) * SS], fill=gobek + (255,))

rnd = random.Random(2026)
ot_obegi(0, 0, rnd, 170, (0.55, 0.95), (0.15, 0.55), (0.25, 0.7), (2.6, 4.6), 0.3)      # 0 uzun ot
ot_obegi(1, 0, rnd, 230, (0.25, 0.55), (0.05, 0.3), (0.0, 0.3), (2.4, 4.0), 0.22)       # 1 kısa çim
ot_obegi(2, 0, rnd, 90, (0.35, 0.65), (0.1, 0.45), (0.2, 0.6), (2.4, 4.0), 0.35); tohum_saplari(2, 0, rnd, 26)   # 2 tohumlu
ot_obegi(3, 0, rnd, 160, (0.45, 0.9), (0.15, 0.55), (0.3, 0.7), (2.4, 4.2), 0.85)       # 3 kuru ot
egrelti(0, 1, rnd)                                                                       # 4 eğrelti
cicekler(1, 1, rnd, [((140, 100, 210), (240, 210, 70)), ((100, 120, 230), (60, 60, 140)), ((245, 242, 232), (245, 200, 50))], 14)
cicekler(2, 1, rnd, [((220, 50, 40), (30, 24, 24)), ((245, 242, 232), (245, 200, 50)), ((150, 100, 200), (240, 210, 70))], 12)
cicekler(3, 1, rnd, [((250, 210, 50), (220, 170, 30)), ((230, 150, 190), (250, 240, 240)), ((245, 242, 232), (245, 200, 50))], 14)

img = img.resize((W, H), Image.LANCZOS)
a = np.array(img).astype(np.float32)
# Saydam piksellere komşu rengini yay (mipmap'te koyu kenar olmasın)
rgb, al = a[..., :3], a[..., 3:4] / 255.0
acc = rgb * al; wsum = al.copy()
from scipy.ndimage import gaussian_filter
for s in (2, 6, 16, 40):
    b = np.stack([gaussian_filter(acc[..., i], s) for i in range(3)], -1)
    wb = gaussian_filter(wsum[..., 0], s)[..., None]
    dolu = np.where(wb > 1e-4, b / np.maximum(wb, 1e-4), 0)
    rgb = np.where(al > 0.5, rgb, np.where(wsum > 0.01, rgb, dolu))
    acc = np.where(al > 0.01, acc, dolu * 1.0); wsum = np.maximum(wsum, (wb > 1e-4).astype(np.float32))
out = np.concatenate([np.clip(rgb, 0, 255), a[..., 3:4]], -1).astype(np.uint8)
Image.fromarray(out, "RGBA").save(sys.argv[1])
# Önizleme: koyu toprak üstünde
bg = Image.new("RGBA", (W, H), (70, 62, 45, 255)); bg.alpha_composite(Image.fromarray(out, "RGBA")); bg.convert("RGB").resize((W // 2, H // 2)).save(sys.argv[2])
print("tamam")
