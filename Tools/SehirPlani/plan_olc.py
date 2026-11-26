"""
Kurye Tycoon — şehir planı ölçümü (denge karşılaştırması için).
plan.json'u oyunun kurallarıyla okur (CityMap: müşteri binası = yola 4 komşu, müşteri bölgesindeki arsa; kapı = ilk yol komşusu;
rota = 4 bağlı yol kareleri üzerinde en kısa yol, yalnız açık semtlerden) ve şunları yazar:
  • kare sayıları (yol, su, park, kır, müşteri binası, dükkân yeri), semt başına dağılım
  • her açılış seviyesinde (1–5) yol ağı tek parça mı, ulaşılamayan bina var mı
  • rastgele 500 bina çifti arasındaki ortalama yol uzunluğu ve dolanma oranı (yol / kuş uçuşu Manhattan)
  • dükkân → müşteri (yarıçap 16, oyundaki olasılıkla) ortalama teslimat yolu
Kullanım:  python3 plan_olc.py [plan.json] [ikinci_plan.json ...]
Gerekenler: yalnız standart kitaplık.
"""
import json, os, random, sys
from collections import deque

BURA = os.path.dirname(os.path.abspath(__file__))
VARSAYILAN = os.path.join(BURA, "..", "..", "Resources", "Sehir", "plan.json")
MUSTERI = set("OHEMCWTB")        # CityMap.CustomerZone (bilinmeyen harf = çarşı)
DUKKAN_TERCIH = set("M")        # plan sürüm 2'de dükkânlar önce çarşı (high street) karelerine (CityMap.PlaceShops)
YON = [(-1, 0), (1, 0), (0, -1), (0, 1)]   # CityMap.Dirs: sol, sağ, aşağı, yukarı


class Plan:
    def __init__(self, yol):
        p = json.load(open(yol, encoding="utf-8"))
        self.p, self.W, self.H = p, p["W"], p["H"]
        self.kare, self.zone, self.bolge = p["kare"], p["zone"], p["bolge"]
        W, H = self.W, self.H
        self.yol = [[self.kare[y][x] in "YK" for x in range(W)] for y in range(H)]
        self.musteri, self.dukkan = [], []
        for x in range(W):
            for y in range(H):
                if self.kare[y][x] in "YKS":
                    continue
                if not any(self.yolmu(x + dx, y + dy) for dx, dy in YON):
                    continue
                z = self.zone[y][x]
                if z in "PZR." :
                    continue
                if z != "A":
                    self.musteri.append((x, y))
                if z not in "WA":             # organik şehirde AVM karesine dükkân konmaz (CityMap.Generate)
                    self.dukkan.append((x, y))

    def yolmu(self, x, y):
        return 0 <= x < self.W and 0 <= y < self.H and self.yol[y][x]

    def b(self, t):
        return int(self.bolge[t[1]][t[0]])

    def kapi(self, t):
        for dx, dy in YON:
            if self.yolmu(t[0] + dx, t[1] + dy):
                return (t[0] + dx, t[1] + dy)
        return t

    def bfs(self, bas, seviye=5):
        """bas karesinden, yalnız açık (semt ≤ seviye) yol karelerinden geçerek uzaklıklar."""
        uz = {bas: 0}
        q = deque([bas])
        while q:
            c = q.popleft()
            for dx, dy in YON:
                n = (c[0] + dx, c[1] + dy)
                if n in uz or not self.yolmu(*n) or self.b(n) > seviye:
                    continue
                uz[n] = uz[c] + 1
                q.append(n)
        return uz


def olc(yol, tohum=7):
    P = Plan(yol)
    W, H = P.W, P.H
    rnd = random.Random(tohum)
    say = {k: sum(r.count(k) for r in P.kare) for k in "YKSP."}
    kir = sum(r.count(".") for r in P.zone)
    print(f"== {os.path.relpath(yol)}  {W}×{H} = {W * H} kare  (sürüm {P.p.get('surum')}, tohum {P.p.get('tohum', '-')})")
    print(f"   yol {say['Y'] + say['K']} (köprü {say['K']}), su {say['S']}, park {say['P']}, kır {kir}, "
          f"yol çizgisi {len(P.p['yollar'])}")
    print(f"   müşteri binası {len(P.musteri)}, dükkân yeri {len(P.dukkan)} "
          f"(çarşı {sum(1 for t in P.dukkan if P.zone[t[1]][t[0]] in DUKKAN_TERCIH)})")
    for s in range(1, 6):
        m = [t for t in P.musteri if P.b(t) == s]
        d = [t for t in P.dukkan if P.b(t) == s]
        dt = [t for t in d if P.zone[t[1]][t[0]] in DUKKAN_TERCIH]
        yk = sum(1 for x in range(W) for y in range(H) if P.yol[y][x] and int(P.bolge[y][x]) == s)
        print(f"   semt {s}: müşteri {len(m):5d}  dükkân yeri {len(d):5d} (çarşı {len(dt):4d})  yol karesi {yk:5d}")
    # bağlantı: her seviyede, merkez ofisin kapısından açık binaların kapılarına ulaşılır mı
    mey = tuple(P.p["meydan"])
    ofis = min((t for t in P.dukkan + P.musteri if t != mey), key=lambda t: (t[0] - mey[0]) ** 2 + (t[1] - mey[1]) ** 2)
    okapi = P.kapi(ofis)
    for s in range(1, 6):
        uz = P.bfs(okapi, s)
        acik = [t for t in set(P.musteri + P.dukkan) if P.b(t) <= s]
        yok = [t for t in acik if P.kapi(t) not in uz and not any((t[0] + dx, t[1] + dy) in uz for dx, dy in YON)]
        yolTop = sum(1 for x in range(W) for y in range(H) if P.yol[y][x] and int(P.bolge[y][x]) <= s)
        print(f"   seviye {s}: açık yol {yolTop}, ofisten ulaşılan {len(uz)}, ulaşılamayan bina {len(yok)} / {len(acik)}"
              + (f"  örn. {yok[:4]}" if yok else ""))
    # kör uçlar
    kor = sum(1 for x in range(W) for y in range(H) if P.yol[y][x] and sum(P.yolmu(x + dx, y + dy) for dx, dy in YON) == 1)
    print(f"   kör uç (tek komşulu yol karesi): {kor}")
    # rastgele 500 bina çifti
    top = man = 0.0
    n = 0
    for _ in range(500):
        a, c = rnd.choice(P.musteri), rnd.choice(P.musteri)
        if a == c:
            continue
        uz = P.bfs(P.kapi(a))
        k = P.kapi(c)
        if k not in uz:
            continue
        top += uz[k] + 2
        man += abs(a[0] - c[0]) + abs(a[1] - c[1])
        n += 1
    print(f"   rastgele {n} bina çifti: ortalama yol {top / n:.1f} kare (≈ {top / n * 10:.0f} m), kuş uçuşu (Manhattan) {man / n:.1f}, "
          f"dolanma ×{top / max(man, 1):.2f}")
    # dükkân → müşteri (yarıçap 16; CityMap.RandomCustomerTileNear olasılığı)
    R = 16
    dk = [t for t in P.dukkan if P.zone[t[1]][t[0]] in DUKKAN_TERCIH] or P.dukkan
    top = man = 0.0
    n = 0
    for _ in range(500):
        s = rnd.choice(dk)
        uz = P.bfs(P.kapi(s))
        for _ in range(200):
            t = rnd.choice(P.musteri)
            d = abs(t[0] - s[0]) + abs(t[1] - s[1])
            if t == s or d > R or rnd.random() > 1 - 0.7 * d / R:
                continue
            k = P.kapi(t)
            if k in uz:
                top += uz[k] + 2
                man += d
                n += 1
            break
    print(f"   dükkân → müşteri (yarıçap {R}), {n} teslimat: ortalama yol {top / n:.1f} kare (≈ {top / n * 10:.0f} m), "
          f"Manhattan {man / n:.1f}, dolanma ×{top / max(man, 1):.2f}")
    return P


if __name__ == "__main__":
    for y in (sys.argv[1:] or [VARSAYILAN]):
        olc(y)
