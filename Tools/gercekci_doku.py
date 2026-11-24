# Kurye Tycoon · gerçekçi yapı dokuları (döşenebilir, 1024 px). Her malzeme için <ad>.png (renk) ve <ad>_N.png (normal, OpenGL).
# Kullanım: python3 Tools/gercekci_doku.py Modeller/Gercekci/Dokular   (Pillow + NumPy + SciPy)
# Ölçek: her doku bir kenarı OLCEK metre olan kareyi kaplar (Blender betiği UV'yi metreye göre açar).
import sys, os, math
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, sobel

N = 1024
rng = np.random.default_rng(2026)
OLCEK = {}   # ad → metre (Blender betiği okur)

def gurultu(olcek, oktav=5, tohum=0, n=N):
    """Döşenebilir değer gürültüsü (periyodik ızgara), 0–1."""
    r = np.random.default_rng(tohum)
    top = np.zeros((n, n)); gen = 0; a = 1.0
    for o in range(oktav):
        g = max(1, int(olcek * 2 ** o))
        izg = r.random((g, g))
        # periyodik çift doğrusal büyütme
        x = np.arange(n) * g / n
        i0 = np.floor(x).astype(int) % g; i1 = (i0 + 1) % g; f = x - np.floor(x)
        f = f * f * (3 - 2 * f)
        sat = izg[i0][:, i0] * (1 - f)[None, :] + izg[i0][:, i1] * f[None, :]
        sat2 = izg[i1][:, i0] * (1 - f)[None, :] + izg[i1][:, i1] * f[None, :]
        top += (sat * (1 - f)[:, None] + sat2 * f[:, None]) * a
        gen += a; a *= 0.5
    return top / gen

def normal(h, guc):
    hx = sobel(h, axis=1, mode="wrap"); hy = sobel(h, axis=0, mode="wrap")
    nx, ny, nz = -hx * guc, hy * guc, np.ones_like(h)
    L = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.stack([(nx / L + 1) / 2, (ny / L + 1) / 2, (nz / L + 1) / 2], -1)

def kaydet(klasor, ad, renk, h, guc, metre):
    OLCEK[ad] = metre
    Image.fromarray((np.clip(renk, 0, 1) * 255).astype(np.uint8)).save(os.path.join(klasor, ad + ".png"), optimize=True)
    Image.fromarray((normal(h, guc) * 255).astype(np.uint8)).save(os.path.join(klasor, ad + "_N.png"), optimize=True)
    print("doku", ad, metre, "m")

def tugla(ad, klasor, taban, sapma, harc, metre=2.0, en=0.215, boy=0.065, derz=0.010, tohum=1):
    px = N / metre
    bw, bh, dz = (en + derz) * px, (boy + derz) * px, derz * px
    yy, xx = np.mgrid[0:N, 0:N].astype(float)
    sira = np.floor(yy / bh).astype(int)
    kay = (sira % 2) * 0.5 * bw
    kol = np.floor((xx + kay) / bw).astype(int)
    lx = (xx + kay) % bw; ly = yy % bh
    harcMaske = (lx < dz) | (ly < dz)
    # tuğla başına renk (döşenebilir: sıra ve kolon sayısı periyot)
    nr, nc = int(round(N / bh)), int(round(N / bw)) + 1
    r = np.random.default_rng(tohum)
    # Tuğla başına: parlaklık farkı + hafif sıcak/soğuk kayma (kanallar birlikte; yeşil/mor tuğla çıkmaz)
    par = r.normal(0, 1, (nr + 2, nc + 2, 1)) * sapma * 2.2
    sicak = r.normal(0, 1, (nr + 2, nc + 2, 1)) * sapma * 0.6
    tr = par * np.array(taban)[None, None, :] / max(taban) + sicak * np.array([1.0, 0.35, -0.4])[None, None, :]
    koyu = (r.random((nr + 2, nc + 2)) < 0.08)[..., None] * -0.12
    idx_r = sira % nr; idx_c = kol % nc
    renk = np.array(taban)[None, None, :] + tr[idx_r, idx_c] + koyu[idx_r, idx_c]
    yuzey = gurultu(24, 4, tohum + 7)[..., None]
    renk = renk * (0.86 + 0.28 * yuzey)
    benek = (gurultu(90, 2, tohum + 9) > 0.72)[..., None] * -0.05
    renk = renk + benek
    hr = np.array(harc)[None, None, :] * (0.9 + 0.2 * gurultu(60, 3, tohum + 3)[..., None])
    renk = np.where(harcMaske[..., None], hr, renk)
    # yükseklik: tuğla kenarları yuvarlak, derz içeride
    kenar = np.minimum(np.minimum(lx - dz, bw - lx), np.minimum(ly - dz, bh - ly))
    h = np.clip(kenar / (0.18 * bh), 0, 1) ** 0.6 * 0.8 + gurultu(40, 4, tohum + 11) * 0.2
    h = np.where(harcMaske, 0.0, h)
    kaydet(klasor, ad, renk, gaussian_filter(h, 0.7, mode="wrap"), 2.2, metre)

def tas_blok(ad, klasor, taban, metre=3.0, en=0.9, boy=0.45, tohum=2):
    px = N / metre; bw, bh, dz = en * px, boy * px, 0.012 * px
    yy, xx = np.mgrid[0:N, 0:N].astype(float)
    sira = np.floor(yy / bh).astype(int); kay = (sira % 2) * 0.5 * bw
    lx = (xx + kay) % bw; ly = yy % bh
    m = (lx < dz) | (ly < dz)
    kol = np.floor((xx + kay) / bw).astype(int)
    r = np.random.default_rng(tohum)
    tr = r.normal(0, 0.035, (40, 40, 1))
    renk = np.array(taban)[None, None, :] + tr[sira % 40, kol % 40]
    g = gurultu(10, 6, tohum + 5)[..., None]
    renk = renk * (0.88 + 0.22 * g) - (gurultu(70, 3, tohum + 8) > 0.75)[..., None] * 0.04
    renk = np.where(m[..., None], np.array(taban) * 0.62, renk)
    kenar = np.minimum(np.minimum(lx - dz, bw - lx), np.minimum(ly - dz, bh - ly))
    h = np.clip(kenar / (0.06 * bh), 0, 1) * 0.7 + g[..., 0] * 0.3
    h = np.where(m, 0.0, h)
    kaydet(klasor, ad, renk, gaussian_filter(h, 0.8, mode="wrap"), 1.6, metre)

def siva(ad, klasor, taban, metre=3.0, tohum=3):
    g = gurultu(6, 7, tohum); ince = gurultu(120, 3, tohum + 1)
    renk = np.array(taban)[None, None, :] * (0.9 + 0.12 * g[..., None] + 0.06 * ince[..., None])
    leke = gurultu(3, 4, tohum + 2)[..., None]
    renk = renk * (1 - 0.06 * np.clip(leke - 0.55, 0, 1) * 4)
    kaydet(klasor, ad, renk, ince * 0.7 + g * 0.3, 1.2, metre)

def beton(ad, klasor, taban, metre=4.0, panel=2.0, tohum=4):
    g = gurultu(8, 7, tohum); ince = gurultu(160, 2, tohum + 1)
    renk = np.array(taban)[None, None, :] * (0.88 + 0.16 * g[..., None] + 0.05 * ince[..., None])
    yy, xx = np.mgrid[0:N, 0:N].astype(float); px = N / metre
    derz = ((xx % (panel * px)) < 2.5) | ((yy % (panel * px)) < 2.5)
    delik = ((((xx + panel * px / 4) % (panel * px / 2)) - 0) ** 2 + (((yy + panel * px / 4) % (panel * px / 2))) ** 2) < 16
    renk = np.where((derz | delik)[..., None], renk * 0.72, renk)
    h = 0.6 + 0.2 * g + 0.2 * ince; h = np.where(derz | delik, 0.2, h)
    kaydet(klasor, ad, renk, gaussian_filter(h, 0.6, mode="wrap"), 1.4, metre)

def metal_panel(ad, klasor, taban, metre=2.0, aralik=0.25, tohum=5):
    yy, xx = np.mgrid[0:N, 0:N].astype(float); px = N / metre
    u = (xx % (aralik * px)) / (aralik * px)
    h = np.clip(np.abs(u - 0.5) * 2, 0, 1); h = 1 - np.where(h > 0.9, (h - 0.9) * 10, 0)
    g = gurultu(5, 5, tohum)
    renk = np.array(taban)[None, None, :] * (0.92 + 0.1 * g[..., None]) * (0.92 + 0.08 * h[..., None])
    kaydet(klasor, ad, renk, h, 1.8, metre)

def cati_kiremit(ad, klasor, taban, metre=2.0, tohum=6):
    yy, xx = np.mgrid[0:N, 0:N].astype(float); px = N / metre
    rh, cw = 0.32 * px, 0.21 * px
    sira = np.floor(yy / rh).astype(int); kay = (sira % 2) * cw / 2
    lx = ((xx + kay) % cw) / cw; ly = (yy % rh) / rh
    dalga = np.sin(lx * math.pi)
    h = dalga * 0.6 + ly * 0.4
    r = np.random.default_rng(tohum)
    tr = r.normal(0, 0.06, (60, 60, 1)) * np.array(taban)[None, None, :] / max(taban) + r.normal(0, 0.02, (60, 60, 1)) * np.array([1.0, 0.4, -0.3])[None, None, :]
    kol = np.floor((xx + kay) / cw).astype(int)
    renk = np.array(taban)[None, None, :] + tr[sira % 60, kol % 60]
    renk = renk * (0.7 + 0.35 * h[..., None]) * (0.9 + 0.15 * gurultu(12, 5, tohum + 1)[..., None])
    kaydet(klasor, ad, renk, h, 3.0, metre)

def membran(ad, klasor, taban, metre=6.0, tohum=7):
    g = gurultu(5, 7, tohum); yy, xx = np.mgrid[0:N, 0:N].astype(float); px = N / metre
    dikis = ((xx % (1.0 * px)) < 3)
    renk = np.array(taban)[None, None, :] * (0.86 + 0.2 * g[..., None])
    renk = np.where(dikis[..., None], renk * 0.85, renk)
    kaydet(klasor, ad, renk, np.where(dikis, 0.8, 0.5) + 0.1 * g, 1.0, metre)

def kaldirim(ad, klasor, taban, metre=2.0, tas=0.3, tohum=8):
    yy, xx = np.mgrid[0:N, 0:N].astype(float); px = N / metre; s = tas * px
    sira = np.floor(yy / s).astype(int); kay = (sira % 2) * s / 2
    lx = (xx + kay) % s; ly = yy % s; m = (lx < 3) | (ly < 3)
    r = np.random.default_rng(tohum); tr = r.normal(0, 0.03, (40, 40, 1))
    kol = np.floor((xx + kay) / s).astype(int)
    renk = np.array(taban)[None, None, :] + tr[sira % 40, kol % 40]
    renk = renk * (0.9 + 0.15 * gurultu(20, 4, tohum)[..., None])
    renk = np.where(m[..., None], renk * 0.6, renk)
    h = np.where(m, 0.0, 0.8)
    kaydet(klasor, ad, renk, gaussian_filter(h, 0.8, mode="wrap"), 1.5, metre)

def ahsap(ad, klasor, taban, metre=1.0, tohum=9):
    yy, xx = np.mgrid[0:N, 0:N].astype(float)
    g = gurultu(3, 5, tohum)
    damar = np.sin((xx / N * 60 + g * 8) * math.pi * 2) * 0.5 + 0.5
    renk = np.array(taban)[None, None, :] * (0.8 + 0.25 * damar[..., None] * (0.7 + 0.3 * gurultu(40, 3, tohum + 1)[..., None]))
    kaydet(klasor, ad, renk, damar * 0.5, 0.8, metre)

def main(klasor):
    os.makedirs(klasor, exist_ok=True)
    tugla("Tugla_Kirmizi", klasor, (0.56, 0.24, 0.16), 0.045, (0.70, 0.67, 0.62), tohum=11)
    tugla("Tugla_Kahve", klasor, (0.36, 0.22, 0.16), 0.035, (0.55, 0.52, 0.48), tohum=12)
    tugla("Tugla_Sari", klasor, (0.72, 0.58, 0.38), 0.035, (0.78, 0.75, 0.70), tohum=13)
    tas_blok("Tas_Kirec", klasor, (0.74, 0.70, 0.62))
    tas_blok("Tas_Koyu", klasor, (0.36, 0.35, 0.34), tohum=21)
    siva("Siva_Krem", klasor, (0.86, 0.80, 0.68))
    siva("Siva_Beyaz", klasor, (0.90, 0.89, 0.86), tohum=31)
    beton("Beton", klasor, (0.62, 0.61, 0.58))
    metal_panel("Metal_Antrasit", klasor, (0.20, 0.21, 0.22))
    metal_panel("Metal_Bakir", klasor, (0.55, 0.34, 0.22), tohum=51)
    cati_kiremit("Kiremit_Kirmizi", klasor, (0.55, 0.25, 0.16))
    cati_kiremit("Arduvaz", klasor, (0.24, 0.25, 0.28), tohum=61)
    membran("Cati_Membran", klasor, (0.34, 0.34, 0.35))
    kaldirim("Kaldirim", klasor, (0.58, 0.56, 0.53))
    ahsap("Ahsap_Koyu", klasor, (0.30, 0.19, 0.11))
    with open(os.path.join(klasor, "olcekler.txt"), "w") as f:
        for k, v in OLCEK.items(): f.write(f"{k} {v}\n")

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Dokular")
