# Kurye Tycoon · gerçekçi yapılar (Blender, arka planda). Unity: Kurye Tycoon > Blender > Gerçekçi yapı önizlemesi.
# Her yapı: dokulu malzemeler (Modeller/Gercekci/Dokular, Tools/gercekci_doku.py), metre ölçüsü, ön −Y, zemin z = 0.
# Çıktı: <proje>/Onizleme/Gercekci/<ad>_1..4.png (4 açıdan render) ve Modeller/Gercekci/<ad>.fbx.
# Malzeme adları Unity'de aynı adlı dokuyla eşleşir; SirketRengi_* oyuncunun şirket rengine boyanır.
import bpy, bmesh, os, math, sys, random
from mathutils import Vector

KOK = os.environ.get("KURYE_TYCOON_KOK") or r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon"
DOKU = os.path.join(KOK, "Modeller", "Gercekci", "Dokular")
FBX_KLASOR = os.path.join(KOK, "Modeller", "Gercekci")
ONIZLEME = os.path.join(os.path.dirname(os.path.dirname(KOK)), "Onizleme", "Gercekci")
YAPILAR = (os.environ.get("KURYE_GERCEKCI_YAPI") or "merkez_ofis").split(",")

OLCEK = {}
try:
    for satir in open(os.path.join(DOKU, "olcekler.txt"), encoding="utf-8"):
        p = satir.split()
        if len(p) == 2: OLCEK[p[0]] = float(p[1])
except OSError:
    print("UYARI: olcekler.txt yok")

# ------------------------------------------------------------------ malzemeler
# ad: (doku, renk çarpanı, pürüzlülük, metal, ışıma gücü, normal gücü)
MALZ = {
    "Tugla_Kirmizi":   ("Tugla_Kirmizi", (1, 1, 1), 0.85, 0.0, 0.0, 1.0),
    "Tugla_Kahve":     ("Tugla_Kahve", (1, 1, 1), 0.85, 0.0, 0.0, 1.0),
    "Tas_Kirec":       ("Tas_Kirec", (1, 1, 1), 0.75, 0.0, 0.0, 0.8),
    "Tas_Koyu":        ("Tas_Koyu", (1, 1, 1), 0.6, 0.0, 0.0, 0.8),
    "Siva_Beyaz":      ("Siva_Beyaz", (1, 1, 1), 0.9, 0.0, 0.0, 0.5),
    "Beton":           ("Beton", (1, 1, 1), 0.85, 0.0, 0.0, 0.7),
    "Metal_Antrasit":  ("Metal_Antrasit", (1, 1, 1), 0.45, 0.6, 0.0, 0.5),
    "Metal_Kepenk":    ("Metal_Antrasit", (1.25, 1.25, 1.25), 0.5, 0.5, 0.0, 1.0),
    "Cati_Membran":    ("Cati_Membran", (1, 1, 1), 0.9, 0.0, 0.0, 0.6),
    "Kaldirim":        ("Kaldirim", (1, 1, 1), 0.85, 0.0, 0.0, 0.8),
    "Cam":             (None, (0.015, 0.02, 0.026), 0.06, 0.0, 0.0, 0.0),
    "Ic_Mekan":        (None, (0.55, 0.45, 0.33), 0.9, 0.0, 0.6, 0.0),
    "SirketRengi_Tente":  (None, (0.02, 0.30, 0.62), 0.6, 0.0, 0.0, 0.0),
    "SirketRengi_Tabela": (None, (0.02, 0.30, 0.62), 0.35, 0.0, 0.4, 0.0),
    "Asfalt":          ("Beton", (0.17, 0.17, 0.18), 0.9, 0.0, 0.0, 0.5),
    "Bitki":           (None, (0.05, 0.13, 0.03), 0.75, 0.0, 0.0, 0.0),
    "Toprak":          (None, (0.16, 0.11, 0.07), 0.95, 0.0, 0.0, 0.0),
    "Lamba":           (None, (1.0, 0.85, 0.6), 0.3, 0.0, 2.0, 0.0),
    "Tabela_Yazi":     (None, (0.97, 0.97, 0.95), 0.3, 0.0, 3.0, 0.0),
}
DONMUS = {"Metal_Kepenk"}   # doku 90° döndürülür (yatay kepenk çıtaları)
MALZ_SIRA = list(MALZ.keys())

def malzeme_kur():
    out = {}
    for ad, (doku, carp, pur, met, isima, ng) in MALZ.items():
        m = bpy.data.materials.new(ad); m.use_nodes = True
        nt = m.node_tree; b = nt.nodes.get("Principled BSDF")
        b.inputs["Roughness"].default_value = pur
        b.inputs["Metallic"].default_value = met
        renk = (*carp, 1.0)
        if doku:
            yol = os.path.join(DOKU, doku + ".png")
            if os.path.exists(yol):
                im = nt.nodes.new("ShaderNodeTexImage"); im.image = bpy.data.images.load(yol, check_existing=True)
                if carp != (1, 1, 1):
                    mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1
                    nt.links.new(im.outputs["Color"], mix.inputs[1]); mix.inputs[2].default_value = renk
                    nt.links.new(mix.outputs[0], b.inputs["Base Color"])
                else:
                    nt.links.new(im.outputs["Color"], b.inputs["Base Color"])
            ny = os.path.join(DOKU, doku + "_N.png")
            if ng > 0 and os.path.exists(ny):
                ni = nt.nodes.new("ShaderNodeTexImage"); ni.image = bpy.data.images.load(ny, check_existing=True)
                ni.image.colorspace_settings.name = "Non-Color"
                nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = ng
                nt.links.new(ni.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], b.inputs["Normal"])
        else:
            b.inputs["Base Color"].default_value = renk
        if isima > 0:
            for anahtar in ("Emission Color", "Emission"):
                if anahtar in b.inputs: b.inputs[anahtar].default_value = renk; break
            if "Emission Strength" in b.inputs: b.inputs["Emission Strength"].default_value = isima
        if ad == "Cam":
            b.inputs["Specular IOR Level" if "Specular IOR Level" in b.inputs else "Specular"].default_value = 0.6
        out[ad] = m
    return out

# ------------------------------------------------------------------ geometri kurucu
class G:
    """Tek mesh: dörtgenler + malzeme indeksi + metreye göre kutu izdüşümlü UV."""
    def __init__(self): self.v = []; self.f = []

    def dortgen(self, a, b, c, d, malz):
        i = len(self.v); self.v += [a, b, c, d]; self.f.append(((i, i + 1, i + 2, i + 3), malz))

    def kutu(self, x0, y0, z0, x1, y1, z1, malz, alt=False):
        if x0 > x1: x0, x1 = x1, x0
        if y0 > y1: y0, y1 = y1, y0
        if z0 > z1: z0, z1 = z1, z0
        if x1 - x0 < 1e-4 or y1 - y0 < 1e-4 or z1 - z0 < 1e-4: return
        P = lambda x, y, z: Vector((x, y, z))
        self.dortgen(P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1), malz)          # üst
        if alt: self.dortgen(P(x0, y1, z0), P(x1, y1, z0), P(x1, y0, z0), P(x0, y0, z0), malz)
        self.dortgen(P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1), malz)          # −Y
        self.dortgen(P(x1, y1, z0), P(x0, y1, z0), P(x0, y1, z1), P(x1, y1, z1), malz)          # +Y
        self.dortgen(P(x0, y1, z0), P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), malz)          # −X
        self.dortgen(P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), P(x1, y0, z1), malz)          # +X

    def kure(self, cx, cy, cz, rx, ry, rz, malz, bolum=2, tohum=0):
        """Yamuk küre (bitki öbeği): ikosfer, köşeler hafif rastgele itilir."""
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=bolum, radius=1.0)
        r = random.Random(tohum)
        for v in bm.verts:
            k = 1.0 + (r.random() - 0.5) * 0.35
            v.co = Vector((cx + v.co.x * rx * k, cy + v.co.y * ry * k, cz + v.co.z * rz * k))
        for f in bm.faces:
            i = len(self.v); self.v += [v.co.copy() for v in f.verts]
            self.f.append((tuple(range(i, i + len(f.verts))), malz))
        bm.free()

    def calilik(self, cx, cy, z, en, boy, malz="Bitki", tohum=0):
        r = random.Random(tohum)
        for k in range(7):
            a = r.random() * 6.283; u = r.random() * en * 0.35
            rr = en * (0.28 + r.random() * 0.18)
            self.kure(cx + math.cos(a) * u, cy + math.sin(a) * u, z + boy * (0.35 + r.random() * 0.45), rr, rr, rr * 0.85, malz, 2, tohum * 13 + k)

    def mesh(self, ad, malzemeler):
        me = bpy.data.meshes.new(ad)
        me.from_pydata([tuple(p) for p in self.v], [], [f for f, _ in self.f])
        for m in MALZ_SIRA: me.materials.append(malzemeler[m])
        idx = {m: i for i, m in enumerate(MALZ_SIRA)}
        uv = me.uv_layers.new(name="UVMap")
        for poly, (_, mz) in zip(me.polygons, self.f):
            poly.material_index = idx[mz]
            n = poly.normal; olc = OLCEK.get(MALZ[mz][0] or "", 2.0)
            ax = max(range(3), key=lambda k: abs(n[k]))
            for li in poly.loop_indices:
                p = me.vertices[me.loops[li].vertex_index].co
                if ax == 2: u, w = p.x, p.y
                elif ax == 1: u, w = (p.x if n.y < 0 else -p.x), p.z
                else: u, w = (-p.y if n.x < 0 else p.y), p.z
                if mz in DONMUS: u, w = w, u
                uv.data[li].uv = (u / olc, w / olc)
        me.validate(); me.update()
        ob = bpy.data.objects.new(ad, me); bpy.context.scene.collection.objects.link(ob)
        return ob

class Cephe:
    """Bina yüzü yerel (s yatay, d dışa, z yukarı) → dünya; kutular eksenlere hizalı kalır."""
    def __init__(self, g, yon, W, D):
        self.g, self.yon, self.W, self.D = g, yon, W, D
    def P(self, s, d):
        W, D = self.W, self.D
        if self.yon == "on":    return (-W / 2 + s, -D / 2 - d)
        if self.yon == "arka":  return (W / 2 - s, D / 2 + d)
        if self.yon == "sol":   return (-W / 2 - d, D / 2 - s)
        return (W / 2 + d, -D / 2 + s)        # sağ
    def kutu(self, s0, s1, d0, d1, z0, z1, malz):
        x0, y0 = self.P(s0, d0); x1, y1 = self.P(s1, d1)
        self.g.kutu(x0, y0, z0, x1, y1, z1, malz)

# ------------------------------------------------------------------ merkez ofis
def pencere(c, s0, z0, w, h, kalinlik, cerceve="Metal_Antrasit", taslik=True, kayit=True, ic="Ic_Mekan"):
    """İçe gömük pencere: cam, doğrama (kayıt + orta dikme), taş denizlik ve lento, arkada iç mekân."""
    gir = 0.16
    c.kutu(s0, s0 + w, -gir - 0.02, -gir, z0, z0 + h, "Cam")
    f = 0.06
    c.kutu(s0, s0 + w, -gir + 0.05, -gir - 0.04, z0, z0 + f, cerceve)
    c.kutu(s0, s0 + w, -gir + 0.05, -gir - 0.04, z0 + h - f, z0 + h, cerceve)
    c.kutu(s0, s0 + f, -gir + 0.05, -gir - 0.04, z0, z0 + h, cerceve)
    c.kutu(s0 + w - f, s0 + w, -gir + 0.05, -gir - 0.04, z0, z0 + h, cerceve)
    c.kutu(s0 + w / 2 - 0.03, s0 + w / 2 + 0.03, -gir + 0.04, -gir - 0.03, z0, z0 + h, cerceve)
    if kayit: c.kutu(s0, s0 + w, -gir + 0.04, -gir - 0.03, z0 + h * 0.7, z0 + h * 0.7 + 0.05, cerceve)
    if taslik:
        c.kutu(s0 - 0.06, s0 + w + 0.06, 0.07, -gir, z0 - 0.07, z0, "Tas_Kirec")                      # denizlik
        c.kutu(s0 - 0.12, s0 + w + 0.12, 0.03, -0.05, z0 + h, z0 + h + 0.24, "Tas_Kirec")             # lento
        c.kutu(s0 + w / 2 - 0.1, s0 + w / 2 + 0.1, 0.05, -0.05, z0 + h + 0.02, z0 + h + 0.28, "Tas_Kirec")   # kilit taşı

def merkez_ofis(malzemeler):
    g = G()
    W = D = 8.4
    T = 0.35                                   # duvar kalınlığı
    zemin = 4.2; kat = 3.4; ust_kat = 4
    tepe = zemin + kat * ust_kat               # 17,8
    korkuluk = 1.1

    # İç: pencerelerin arkasında ~1,3 m derin ışıklı oda kuşağı, ortada çekirdek, her katta döşeme
    oda = T + 1.3
    g.kutu(-W / 2 + oda, -D / 2 + oda, 0.0, W / 2 - oda, D / 2 - oda, tepe, "Ic_Mekan")
    for zk in [0.0, zemin] + [zemin + kat * k for k in range(1, ust_kat)]:
        g.kutu(-W / 2 + T, -D / 2 + T, zk, W / 2 - T, D / 2 - T, zk + 0.12, "Beton")
    g.kutu(-W / 2 + T, -D / 2 + T, tepe - 0.3, W / 2 - T, D / 2 - T, tepe, "Beton")

    for yon in ("on", "sag", "arka", "sol"):
        c = Cephe(g, yon, W, D)
        # --- zemin kat: koyu taş, büyük vitrinler
        c.kutu(0, W, 0, -T, 0, 0.5, "Tas_Koyu")                       # süpürgelik
        c.kutu(0, W, 0.05, -T, 0, 0.12, "Tas_Koyu")
        vitrin = [(0.6, 2.4), (5.4, 2.4)] if yon == "on" else [(0.6, 3.0), (4.8, 3.0)]
        if yon == "sag": vitrin = [(0.6, 2.6)]
        kenar = 0.0; z0, z1 = 0.5, 3.5
        bosluklar = sorted(vitrin + ([(3.1, 2.2)] if yon == "on" else []) + ([(4.6, 3.2)] if yon == "sag" else []))
        for s0, w in bosluklar:
            c.kutu(kenar, s0, 0, -T, z0, zemin, "Tas_Koyu")
            kenar = s0 + w
        c.kutu(kenar, W, 0, -T, z0, zemin, "Tas_Koyu")
        for s0, w in bosluklar:
            c.kutu(s0, s0 + w, 0, -T, z1, zemin, "Tas_Koyu")              # vitrin üstü
        for s0, w in vitrin:
            pencere(c, s0, z0, w, z1 - z0, T, taslik=False, kayit=True)
        c.kutu(0, W, 0.18, -0.05, zemin - 0.25, zemin, "Tas_Kirec")        # zemin kat korniş bandı
        c.kutu(0, W, 0.12, -0.05, zemin, zemin + 0.12, "Tas_Kirec")

        if yon == "on":
            # Giriş: cam kapı, basamak, şirket renginde tente, tente gergileri
            s0, w = 3.1, 2.2
            c.kutu(s0, s0 + w, -0.25, -0.3, 0.12, 3.5, "Cam")
            c.kutu(s0 + w / 2 - 0.03, s0 + w / 2 + 0.03, -0.2, -0.3, 0.12, 3.0, "Metal_Antrasit")
            c.kutu(s0, s0 + w, -0.2, -0.3, 2.95, 3.05, "Metal_Antrasit")
            c.kutu(s0, s0 + 0.08, -0.2, -0.3, 0.12, 3.5, "Metal_Antrasit"); c.kutu(s0 + w - 0.08, s0 + w, -0.2, -0.3, 0.12, 3.5, "Metal_Antrasit")
            c.kutu(s0 - 0.3, s0 + w + 0.3, 0.6, -0.05, 0.0, 0.12, "Tas_Kirec")           # basamak
            c.kutu(s0 - 0.6, s0 + w + 0.6, 1.5, 0.0, 3.55, 3.8, "SirketRengi_Tente")
            c.kutu(s0 - 0.6, s0 + w + 0.6, 1.5, 1.42, 3.4, 3.8, "SirketRengi_Tente")    # ön saçak
            for sx in (s0 - 0.45, s0 + w + 0.45):
                for k in range(6):                                                 # eğik gergi (basamaklı)
                    c.kutu(sx - 0.02, sx + 0.02, 0.25 * k + 0.05, 0.25 * k, 3.8 + 0.12 * (6 - k), 3.92 + 0.12 * (6 - k), "Metal_Antrasit")
            c.kutu(s0 - 0.4, s0 + w + 0.4, 0.04, -0.02, 3.85, 4.15, "SirketRengi_Tabela")  # kapı üstü tabela
        if yon == "sag":
            # Kurye garajı: kepenk, kepenk kutusu, darbe direkleri
            s0, w = 4.6, 3.2
            c.kutu(s0, s0 + w, -0.12, -0.16, 0.0, 3.3, "Metal_Kepenk")
            c.kutu(s0 - 0.05, s0 + w + 0.05, 0.12, -0.12, 3.3, 3.6, "Metal_Antrasit")
            for sx in (s0 - 0.25, s0 + w + 0.25):
                c.kutu(sx - 0.07, sx + 0.07, 0.45, 0.31, 0.0, 1.0, "SirketRengi_Tente")
            c.kutu(s0 + 0.3, s0 + w - 0.3, 0.03, -0.01, 3.62, 3.95, "SirketRengi_Tabela")

        # --- üst katlar: tuğla, pencere sırası, köşe pilastrları
        n_pen = 3; pw = 1.25; ara = (W - 2 * 0.55 - n_pen * pw) / (n_pen - 1)
        for k in range(ust_kat):
            kz = zemin + k * kat
            ps, ph = kz + 0.95, 2.0
            kenar = 0.0
            for i in range(n_pen):
                s0 = 0.55 + i * (pw + ara)
                c.kutu(kenar, s0, 0, -T, kz, kz + kat, "Tugla_Kirmizi")
                kenar = s0 + pw
                c.kutu(s0, s0 + pw, 0, -T, kz, ps, "Tugla_Kirmizi")
                c.kutu(s0, s0 + pw, 0, -T, ps + ph, kz + kat, "Tugla_Kirmizi")
                pencere(c, s0, ps, pw, ph, T)
            c.kutu(kenar, W, 0, -T, kz, kz + kat, "Tugla_Kirmizi")
            c.kutu(0, W, 0.05, -0.02, kz + kat - 0.1, kz + kat, "Tugla_Kahve") if k < ust_kat - 1 else None   # kat hattı
        c.kutu(-0.02, 0.5, 0.1, 0, zemin, tepe, "Tugla_Kirmizi")                              # köşe pilastrı
        c.kutu(W - 0.5, W + 0.02, 0.1, 0, zemin, tepe, "Tugla_Kirmizi")
        # Korniş, frizler, korkuluk duvarı ve kaplama taşı
        c.kutu(-0.1, W + 0.1, 0.15, -0.05, tepe - 0.45, tepe - 0.3, "Tas_Kirec")
        c.kutu(-0.2, W + 0.2, 0.32, -0.05, tepe - 0.1, tepe + 0.15, "Tas_Kirec")
        c.kutu(-0.1, W + 0.1, 0.22, -0.05, tepe - 0.3, tepe - 0.1, "Tugla_Kahve")
        for i in range(17):                                                                   # diş sırası
            sx = 0.15 + i * (W - 0.3) / 16
            c.kutu(sx - 0.07, sx + 0.07, 0.28, 0.05, tepe - 0.3, tepe - 0.12, "Tas_Kirec")
        c.kutu(0, W, 0.0, -0.3, tepe + 0.15, tepe + korkuluk, "Tugla_Kirmizi")
        c.kutu(-0.05, W + 0.05, 0.05, -0.35, tepe + korkuluk, tepe + korkuluk + 0.1, "Tas_Kirec")
        # Yağmur borusu (arka sol köşe)
        if yon == "arka":
            c.kutu(0.55, 0.67, 0.18, 0.06, 0.0, tepe + 0.2, "Metal_Antrasit")

    # Çatı: membran, merdiven kulesi, klima üniteleri, havalandırma
    g.kutu(-W / 2 + 0.3, -D / 2 + 0.3, tepe, W / 2 - 0.3, D / 2 - 0.3, tepe + 0.15, "Cati_Membran")
    g.kutu(0.8, 0.6, tepe, 3.6, 3.6, tepe + 2.8, "Siva_Beyaz")
    g.kutu(0.7, 0.5, tepe + 2.8, 3.7, 3.7, tepe + 2.95, "Tas_Kirec")
    g.kutu(1.6, 0.55, tepe + 0.15, 2.6, 0.6, tepe + 2.3, "Metal_Antrasit")      # kapı
    for (x, y, sx, sy) in ((-3.0, 1.2, 1.4, 1.0), (-3.0, -0.4, 1.4, 1.0), (-1.2, 2.4, 1.0, 1.0)):
        g.kutu(x, y, tepe + 0.15, x + sx, y + sy, tepe + 1.1, "Metal_Antrasit")
        g.kutu(x + 0.15, y + 0.15, tepe + 1.1, x + sx - 0.15, y + sy - 0.15, tepe + 1.18, "Beton")
    for (x, y) in ((-0.6, -2.4), (0.3, -2.4)):
        g.kutu(x, y, tepe + 0.15, x + 0.3, y + 0.3, tepe + 0.9, "Metal_Antrasit")

    # Çatı tabelası (ön, korkuluğun üstünde, çelik ayaklı)
    tz = tepe + korkuluk + 0.1
    g.kutu(-2.8, -D / 2 + 0.25, tz + 0.2, 2.8, -D / 2 + 0.45, tz + 1.6, "SirketRengi_Tabela")
    g.kutu(-2.7, -D / 2 + 0.22, tz + 0.55, 2.7, -D / 2 + 0.25, tz + 1.25, "Tabela_Yazi")
    for x in (-2.4, -0.8, 0.8, 2.4):
        g.kutu(x - 0.05, -D / 2 + 0.45, tz, x + 0.05, -D / 2 + 1.2, tz + 1.5, "Metal_Antrasit")
    # Sokak detayları: kapı aplikleri, saksılar, bisiklet parkı + kargo bisikleti, ilk katta Fransız balkonlar
    on = Cephe(g, "on", W, D)
    for sx in (2.75, 5.65):
        on.kutu(sx - 0.08, sx + 0.08, 0.12, 0.0, 2.6, 2.75, "Metal_Antrasit")
        on.kutu(sx - 0.1, sx + 0.1, 0.3, 0.12, 2.45, 2.95, "Metal_Antrasit")
        on.kutu(sx - 0.07, sx + 0.07, 0.28, 0.14, 2.5, 2.9, "Lamba")
    for sx in (2.35, 6.05):
        on.kutu(sx - 0.3, sx + 0.3, 0.9, 0.3, 0.0, 0.55, "Metal_Antrasit")
        on.kutu(sx - 0.27, sx + 0.27, 0.87, 0.33, 0.55, 0.58, "Toprak")
        bx, by = on.P(sx, 0.6)
        g.calilik(bx, by, 0.55, 0.62, 0.7, tohum=int(sx * 10))
    for k in range(4):                                                   # sağ yan: bisiklet parkı (ters U demirler)
        sg = Cephe(g, "sag", W, D); s = 0.9 + k * 0.85
        sg.kutu(s, s + 0.05, 1.2, 1.15, 0.0, 0.85, "Metal_Antrasit"); sg.kutu(s, s + 0.05, 0.55, 0.5, 0.0, 0.85, "Metal_Antrasit")
        sg.kutu(s, s + 0.05, 1.2, 0.5, 0.8, 0.85, "Metal_Antrasit")
    sg = Cephe(g, "sag", W, D)                                           # kargo bisikleti: şirket renginde kasa
    sg.kutu(1.2, 2.4, 2.4, 1.7, 0.35, 1.05, "SirketRengi_Tente")
    sg.kutu(2.4, 3.6, 2.07, 2.03, 0.55, 0.6, "Metal_Antrasit")
    sg.kutu(3.3, 3.34, 2.07, 2.03, 0.55, 1.1, "Metal_Antrasit"); sg.kutu(3.2, 3.5, 2.12, 1.98, 1.05, 1.1, "Metal_Antrasit")
    for tx in (1.5, 3.4):
        sg.kutu(tx - 0.3, tx + 0.3, 2.08, 2.02, 0.05, 0.6, "Metal_Antrasit")
    pw = 1.25; ara = (W - 2 * 0.55 - 3 * pw) / 2
    for i in range(3):                                                   # Fransız balkon korkulukları (1. kat)
        s0 = 0.55 + i * (pw + ara); z = zemin + 0.95
        on.kutu(s0, s0 + pw, 0.12, 0.08, z + 0.9, z + 0.95, "Metal_Antrasit")
        on.kutu(s0, s0 + pw, 0.12, 0.08, z + 0.05, z + 0.1, "Metal_Antrasit")
        for j in range(9):
            sx = s0 + 0.05 + j * (pw - 0.1) / 8
            on.kutu(sx - 0.012, sx + 0.012, 0.115, 0.085, z + 0.05, z + 0.95, "Metal_Antrasit")
    return g.mesh("MerkezOfis", malzemeler)

YAPI = {"merkez_ofis": merkez_ofis}

# ------------------------------------------------------------------ önizleme sahnesi
def motor_sec(sc):
    for m in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try: sc.render.engine = m; return m
        except TypeError: pass
    sc.render.engine = "CYCLES"; sc.cycles.samples = 64; return "CYCLES"

def sahne_kur(hedef_yukseklik):
    sc = bpy.context.scene
    motor = motor_sec(sc)
    try:
        sc.eevee.taa_render_samples = 64
        for ozellik in ("use_gtao", "use_raytracing", "use_shadows"):
            if hasattr(sc.eevee, ozellik): setattr(sc.eevee, ozellik, True)
    except Exception: pass
    sc.render.resolution_x, sc.render.resolution_y = 1280, 960
    try: sc.view_settings.view_transform = "AgX"
    except TypeError: sc.view_settings.view_transform = "Filmic"
    for gorunum in ("AgX - Medium High Contrast", "Medium High Contrast", "AgX - Base Contrast"):
        try: sc.view_settings.look = gorunum; break
        except TypeError: pass
    sc.view_settings.exposure = -0.9
    # Gökyüzü
    w = bpy.data.worlds.new("Gok"); sc.world = w; w.use_nodes = True
    nt = w.node_tree; bg = nt.nodes.get("Background")
    try:
        sky = nt.nodes.new("ShaderNodeTexSky")
        for tur in ("NISHITA", "MULTIPLE_SCATTERING", "SINGLE_SCATTERING"):
            try: sky.sky_type = tur; break
            except TypeError: pass
        sky.sun_elevation = math.radians(35); sky.sun_rotation = math.radians(220)
        nt.links.new(sky.outputs["Color"], bg.inputs["Color"]); bg.inputs["Strength"].default_value = 0.35
    except Exception:
        bg.inputs["Color"].default_value = (0.55, 0.65, 0.8, 1)
    # Güneş
    gl = bpy.data.lights.new("Gunes", "SUN"); gl.energy = 3.2; gl.angle = math.radians(1.5)
    gl.color = (1.0, 0.95, 0.88)
    go = bpy.data.objects.new("Gunes", gl); sc.collection.objects.link(go)
    go.rotation_euler = (math.radians(52), 0, math.radians(220))
    # Zemin: kaldırım + çim şeridi
    g = G()
    g.kutu(-8, -8, -0.15, 8, 8, 0.0, "Kaldirim")
    g.kutu(-400, -400, -0.3, 400, 400, -0.15, "Asfalt")
    g.kutu(-8.15, -8.15, -0.15, 8.15, -8.0, 0.0, "Tas_Kirec")          # bordür
    for x in (-6.5, 6.5):                                              # ağaç çukuru + çalı
        g.kutu(x - 0.6, -7.4, -0.15, x + 0.6, -6.2, 0.02, "Toprak")
        g.calilik(x, -6.8, 0.0, 1.1, 0.9, tohum=int(x * 10))
    zemin = g.mesh("OnizlemeZemini", MALZEMELER)
    return motor

def kamera(ad, hedef, uzaklik, yukseklik, aci, lens=35):
    cd = bpy.data.cameras.new(ad); cd.lens = lens
    co = bpy.data.objects.new(ad, cd); bpy.context.scene.collection.objects.link(co)
    a = math.radians(aci)
    co.location = Vector((hedef.x + math.sin(a) * uzaklik, hedef.y - math.cos(a) * uzaklik, yukseklik))
    yon = hedef - co.location
    co.rotation_euler = yon.to_track_quat("-Z", "Y").to_euler()
    return co

def render(ad, ob):
    os.makedirs(ONIZLEME, exist_ok=True)
    bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    h = max(p.z for p in bb)
    hedef = Vector((0, 0, h * 0.5))
    acilar = [("1_on_capraz", hedef, 36, 9, -32, 35), ("2_arka_capraz", hedef, 36, 14, 145, 35),
              ("3_sokak", Vector((0, 0, h * 0.35)), 13, 1.7, -18, 24), ("4_oyun_acisi", Vector((0, 0, h * 0.3)), 30, 30, -38, 35)]
    sc = bpy.context.scene
    for n, (etiket, hd, uz, yk, aci, lens) in enumerate(acilar):
        sc.camera = kamera("Kamera_" + etiket, hd, uz, yk, aci, lens)
        sc.render.filepath = os.path.join(ONIZLEME, f"{ad}_{etiket}.png")
        bpy.ops.render.render(write_still=True)
        print("ONIZLEME", sc.render.filepath)

def disa_aktar(ad, ob):
    os.makedirs(FBX_KLASOR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    yol = os.path.join(FBX_KLASOR, ad + ".fbx")
    bpy.ops.export_scene.fbx(filepath=yol, use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             add_leaf_bones=False, path_mode='STRIP')
    print("FBX", yol)

# ------------------------------------------------------------------ ana
bpy.ops.wm.read_factory_settings(use_empty=True)
MALZEMELER = malzeme_kur()
URETILEN = {}
print("Blender", bpy.app.version_string)
for ad in YAPILAR:
    ob = YAPI[ad.strip()](MALZEMELER)
    URETILEN[ad.strip()] = ob
    disa_aktar("Gercekci_" + ad.strip(), ob)
motor = sahne_kur(20)
print("Motor:", motor)
for ad, ob in URETILEN.items():
    render(ad, ob)
print("GERCEKCI BITTI")
