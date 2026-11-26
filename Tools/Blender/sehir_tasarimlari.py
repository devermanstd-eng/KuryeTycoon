# -*- coding: utf-8 -*-
"""
KURYE TYCOON · BİNA ATÖLYESİ: ŞEHİR TASARIMLARI (prosedürel, tohumlu)

Bütün şehri Bina Atölyesi binalarıyla kurmak için her bölge havuzuna çok sayıda, birbirinden farklı tasarım üretir
(eski şehir terasları, çarşı, kule bloklar, müstakil ev, banliyö, kampüs, sanayi, AVM). Her tasarım farklı bir taban
biçimi, kat sayısı, üslup, renk, çatı ve bahçe düzeni alır; aynı imzalı (taban + kat + üslup + renk) iki tasarım çıkmaz.
Sıra evlerde bir arsada 2–3 ayrı ev yan yana durur (her biri kendi üslubu, rengi ve boyuyla).

Çıktı tek dosya: Resources/Atolye/Tasarimlar/sehir_paketi.json  ({"tasarimlar": ["<tasarım JSON'u>", ...]});
AtolyeUretici bu dosyadaki her tasarımı ayrı bina şablonu yapar (adı Bolge_<bölge>_atolye_s_<bölge>_<n>). Şablonlar
kullanıldıkça kurulur: şehirde çıkmayan tasarım zaman almaz.

Çalıştırma (Blender gerekmez):
  python3 Tools/Blender/sehir_tasarimlari.py              dosyayı yazar
  python3 Tools/Blender/sehir_tasarimlari.py --onizleme K  her bölgeden örnekleri K klasörüne resim olarak çizer
"""

import json
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pastel_parcalar as P  # noqa: E402

APT, FACH, TAS, KAIDE, MERD = 0, 1, 2, 3, 4          # P.TAS sonradan taş malzemesinin adı oluyor: sayıyla
GEO, VIC, SIVA, MEWS, PORT, GOT, DEPO = P.GEORGIAN, P.VICTORIAN, P.SIVA_TERAS, P.MEWS, P.PORTLAND_T, P.GOTIK, P.DEPO
FAB, MOD, SERA = P.USLUP_TABAN + 7, P.USLUP_TABAN + 8, P.USLUP_TABAN + 9
CT_OTO, CT_KIRMA, CT_BESIK, CT_MANSART, CT_TERAS, CT_BASAMAK, CT_TESTERE, CT_PARAPET = range(8)

# duvar rengi sıraları (P.DUVAR_RENK): pastel sıva / boya ve tuğla-taş tonları
PASTEL = [0, 1, 2, 3, 4, 5, 6, 8, 9, 14]
TUGLA = [7, 10, 11, 12, 13, 16]
TAS_TON = [6, 14, 15, 3, 10]
KIREMIT = [0, 1, 8]
ARDUVAZ = [5, 6, 7, 9]
RENKLI_CATI = [2, 3, 4]
# üslubun duvar renkleri ve çatı renkleri
USLUP_RENK = {APT: PASTEL, FACH: PASTEL + [6, 14], TAS: TAS_TON, GEO: TUGLA + [10, 11], VIC: TUGLA, SIVA: [6, 14, 3, 0, 1, 2, 9],
              MEWS: PASTEL + TUGLA, PORT: TAS_TON, GOT: TUGLA, DEPO: TUGLA, FAB: TUGLA, MOD: [6, 14, 15, 8, 0, 4], SERA: [6, 14]}
USLUP_CATI = {APT: KIREMIT + RENKLI_CATI + ARDUVAZ, FACH: KIREMIT + [5, 9], TAS: ARDUVAZ + KIREMIT, GEO: ARDUVAZ, VIC: ARDUVAZ + [8, 0],
              SIVA: ARDUVAZ, MEWS: ARDUVAZ + KIREMIT, PORT: ARDUVAZ, GOT: ARDUVAZ + [8], DEPO: ARDUVAZ + [8], FAB: ARDUVAZ,
              MOD: ARDUVAZ, SERA: [7]}
# üsluba uyan çatı tarzları (0: üslubun kendi çatısı)
USLUP_CT = {APT: [CT_OTO, CT_KIRMA, CT_BESIK, CT_MANSART, CT_BASAMAK, CT_PARAPET], FACH: [CT_OTO, CT_BESIK, CT_KIRMA],
            TAS: [CT_OTO, CT_KIRMA, CT_BESIK], GEO: [CT_OTO, CT_PARAPET, CT_MANSART], VIC: [CT_OTO, CT_KIRMA, CT_BESIK, CT_BASAMAK],
            SIVA: [CT_OTO, CT_PARAPET, CT_MANSART], MEWS: [CT_OTO, CT_PARAPET, CT_BESIK], PORT: [CT_OTO, CT_PARAPET, CT_MANSART, CT_TERAS],
            GOT: [CT_OTO, CT_BESIK, CT_BASAMAK], DEPO: [CT_OTO, CT_BESIK, CT_TESTERE], FAB: [CT_OTO, CT_TESTERE, CT_BESIK],
            MOD: [CT_OTO, CT_TERAS], SERA: [CT_OTO]}

DUKKAN_TUR = ["dukkan", "firin", "kasap", "cicekci", "kitapci", "pub"]
TABELA = ["HABERDASHERY", "ANTIQUES", "TAILOR", "DELI", "GREENGROCER", "HARDWARE", "BARBER", "LAUNDRETTE", "NEWSAGENT",
          "TOY SHOP", "GALLERY", "OPTICIAN", "JEWELLER", "WINE SHOP", "CHEESE SHOP", "TEA ROOM", "BICYCLES", "RECORDS",
          "STATIONERY", "PET SHOP", "HATS", "SHOES", "CAMERAS", "KEYS CUT", "TATTOO", "NAILS", "OFF LICENCE", "BAKERY",
          "SWEET SHOP", "CHEMIST", "BOUTIQUE", "MUSIC", "FABRICS", "PAWNBROKER", "PRINTER", "COFFEE", "NOODLES", "CURRY HOUSE",
          "TAPAS", "GELATO", "CHIPPY", "DINER", "BISTRO", "WINE BAR", "THE CROWN", "THE RED LION", "THE ROSE", "KING'S ARMS",
          "THE SHIP", "BOOKS & CO", "FLORIST", "ESTATE AGENT", "TRAVEL", "POST & PRINT", "SECOND HAND", "VINTAGE", "SPICES"]
PUB_TABELA = ["THE CROWN", "THE RED LION", "THE ROSE", "KING'S ARMS", "THE SHIP", "THE PLOUGH", "THE BELL", "THE SWAN",
              "THE ANCHOR", "THE GEORGE", "THE STAR", "THE FOX"]

AGAC_BAHCE = ["agac_meyve_1", "agac_meyve_2", "agac_meyve_3", "agac_genc_1", "agac_genc_2", "agac_hus_1", "agac_hus_2",
              "agac_ihlamur_1", "agac_kestane_1", "agac_yuvarlak_2", "agac_cam_1", "agac_sogut_1", "agac_kavak_1", "agac_mese_1"]
CALI = ["cali_1", "cali_2", "cali_3", "cali_simsir", "cali_ortanca", "cali_lavanta"]
BAHCE_SUS = ["sebze_bahcesi", "cicek_obegi", "kuyu", "odun_yigini", "bank", "cicek_tarhi", "saksi", "kutuk", "ot_obegi", "kaya"]

H = P.HUCRE
IZ = P.IZGARA


def hucre_m(i):
    """Hücre sırasının merkezi (metre, arsa ortası 0)."""
    return (i - (IZ - 1) * 0.5) * H


class Uretici:
    def __init__(self, bolge, tohum):
        self.bolge = bolge
        self.rnd = random.Random(tohum)
        self.imzalar = set()

    # ---- yardımcılar
    def sec(self, l):
        return l[self.rnd.randrange(len(l))]

    def renk(self, tarz):
        return self.sec(USLUP_RENK.get(tarz, PASTEL))

    def cati(self, tarz):
        return self.sec(USLUP_CATI.get(tarz, ARDUVAZ))

    def ct(self, tarz):
        return self.sec(USLUP_CT.get(tarz, [CT_OTO]))

    def kutu(self, x0, x1, y0, y1, kat, tarz, renk, cati, ct, k0=0):
        b = []
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for k in range(k0, k0 + kat):
                    b.append(dict(x=x, y=y, k=k, tarz=tarz, renk=renk, cati=cati, ct=ct))
        return b

    def kaydet(self, liste, ad, tur, bloklar, susler=(), tabela="", tente=0, zemin="cim", sutunlar=None, modul=None):
        """zemin: boş hücrelere konan arsa zemini ("cim", "bahce", "avlu" ya da None); sutunlar: zeminin konacağı x sütunları
        (modülde yalnız kendi şeridi). modul: (x0, genişlik) verilirse ad s_<bölge>_m<x0><w>_<n> (City3D şeritleri birleştirir)."""
        # aynı hücrede iki blok kalmasın (sonraki üstüne yazar)
        tek = {}
        for b in bloklar:
            if 0 <= b["x"] < IZ and 0 <= b["y"] < IZ and 0 <= b["k"] < 8:
                tek[(b["x"], b["y"], b["k"])] = b
        bloklar = [tek[k] for k in sorted(tek)]
        if not bloklar and not susler:
            return False
        imza = tuple(sorted((b["x"], b["y"], b["k"], b["tarz"], b["renk"]) for b in bloklar)) + (tur, tabela, ad)
        if not bloklar:
            imza += tuple(sorted((s["ad"], s["x"], s["y"]) for s in susler))
        if imza in self.imzalar:
            return False
        self.imzalar.add(imza)
        susler = list(susler)
        if zemin:
            dolu = {(b["x"], b["y"]) for b in bloklar}
            for x in (sutunlar if sutunlar is not None else range(IZ)):
                for y in range(IZ):
                    if (x, y) in dolu:
                        continue
                    z = zemin
                    if zemin == "bahce":
                        z = "arsa_bahce" if self.rnd.random() < 0.25 else "arsa_cim"
                    elif zemin == "karisik":
                        z = "arsa_avlu" if self.rnd.random() < 0.5 else "arsa_cim"
                    else:
                        z = "arsa_" + zemin
                    susler.insert(0, dict(ad=z, x=round(hucre_m(x), 2), y=round(hucre_m(y), 2), aci=0.0, olcek=1.0, renk=0))
        if modul:
            ad = "s_%s_m%d%d_%03d" % (self.bolge, modul[0], modul[1], len(liste) + 1)
        elif ad:
            ad = "s_%s_%s_%03d" % (self.bolge, ad, len(liste) + 1)       # ör. s_park_meydan_001, s_eski_kilise_003
        else:
            ad = "s_%s_%03d" % (self.bolge, len(liste) + 1)
        liste.append(dict(ad=ad, bolge=self.bolge, tohum=self.rnd.randrange(1, 9999), dukkan=False,
                          tur=tur, tabela=tabela, tente=tente, bloklar=bloklar, susler=susler))
        return True

    def sus(self, ad, x, y, olcek=1.0, renk=0):
        return dict(ad=ad, x=round(x, 2), y=round(y, 2), aci=round(self.rnd.uniform(0, 360), 1), olcek=round(olcek, 2), renk=renk)

    def bahce(self, dolu_x, y_on, y_arka, agac=1, cali=2, ek=1):
        """Binanın önüne ve arkasına bahçe süsleri (metre; ön −y). dolu_x: binanın kapladığı x sütunları."""
        s = []
        bos_x = [x for x in range(IZ) if x not in dolu_x]
        on_y = hucre_m(y_on) - H * 0.5            # binanın ön yüzü
        if y_on > 0:
            for i in range(cali):
                s.append(self.sus(self.sec(CALI), self.rnd.uniform(-6.0, 6.0), self.rnd.uniform(-6.4, on_y - 0.8), self.rnd.uniform(0.8, 1.1)))
            if self.rnd.random() < 0.6:
                s.append(self.sus("cit_bitki", self.rnd.uniform(-5.0, 5.0), -6.4, 1.0))
        arka_y = hucre_m(y_arka) + H * 0.5
        for i in range(agac):
            if bos_x and self.rnd.random() < 0.5:
                x = hucre_m(self.sec(bos_x)) + self.rnd.uniform(-0.6, 0.6)
                y = self.rnd.uniform(-2.0, 4.5)
            elif arka_y < 4.6:
                x, y = self.rnd.uniform(-4.5, 4.5), self.rnd.uniform(arka_y + 1.0, 6.0)
            else:
                continue
            s.append(self.sus(self.sec(AGAC_BAHCE), x, y, self.rnd.uniform(0.75, 1.0), self.rnd.randrange(3)))
        for i in range(ek):
            if arka_y < 5.2:
                s.append(self.sus(self.sec(BAHCE_SUS), self.rnd.uniform(-5.0, 5.0), self.rnd.uniform(arka_y + 0.6, 6.2)))
        return s


# ---------------------------------------------------------------- bölgeler

def sira_evler(u, liste, n, katlar, usluplar, dukkan_orani=0.0, derin=(2, 4), y0=0):
    """Eski şehir / çarşı: arsada 1–3 ayrı sıra ev yan yana (her biri kendi üslubu, rengi, boyu, çatısı)."""
    deneme = 0
    while sum(1 for t in liste) < n and deneme < n * 30:
        deneme += 1
        bol = u.sec([[5], [3, 2], [2, 3], [2, 2, 1], [1, 2, 2], [2, 1, 2], [1, 3, 1], [4, 1], [1, 4], [5]])
        bloklar = []
        x = 0
        d = u.sec(list(range(derin[0], derin[1] + 1)))
        ortak_ust = u.rnd.random() < 0.3                  # bazen bütün sıra aynı üslup (tek uzun teras)
        tarz0 = u.sec(usluplar)
        for w in bol:
            tarz = tarz0 if ortak_ust else u.sec(usluplar)
            kat = u.sec(katlar)
            dd = max(1, min(4, d + u.sec([0, 0, 0, -1, 1])))
            renk, cati, ct = u.renk(tarz), u.cati(tarz), u.ct(tarz)
            bloklar += u.kutu(x, x + w - 1, y0, y0 + dd - 1, kat, tarz, renk, cati, ct)
            if u.rnd.random() < 0.35 and kat >= 2:            # arka kanat (alçak)
                bloklar += u.kutu(x, x + w - 1, y0 + dd, min(IZ - 1, y0 + dd), max(1, kat - u.sec([1, 2])), tarz, renk, cati, u.sec([CT_TERAS, CT_OTO]))
            if u.rnd.random() < 0.15 and kat >= 3 and w >= 2:     # çatı katı geri çekilmiş
                bloklar += u.kutu(x, x + w - 1, y0 + 1, y0 + dd - 1, 1, tarz, renk, cati, ct, k0=kat)
            x += w
        if u.rnd.random() < dukkan_orani:
            tur = u.sec(DUKKAN_TUR)
            tabela = u.sec(PUB_TABELA) if tur == "pub" else u.sec(TABELA)
            u.kaydet(liste, "", tur, bloklar, tabela=tabela, tente=u.rnd.randrange(10), zemin="karisik")
        else:
            u.kaydet(liste, "", "konut", bloklar, zemin="karisik")


def moduller(u, liste, her, katlar, usluplar, dukkan_orani):
    """Sıra ev şeritleri: (x0, genişlik) yuvası başına `her` tasarım. City3D bir arsayı 2–3 şeritten kurar (her arsada başka
    birleşim: binlerce farklı cephe, mesh'ler paylaşılır). Şerit kaldırıma yaslanır (y = 0); arkası kendi avlusu / bahçesi."""
    for w in (1, 2, 3):
        for x0 in range(0, IZ - w + 1):
            sayac, deneme = 0, 0
            while sayac < her and deneme < her * 40:
                deneme += 1
                tarz = u.sec(usluplar)
                kat = u.sec(katlar)
                if tarz == MEWS:
                    kat = min(kat, 2)
                d = u.sec([2, 2, 3, 3, 4])
                renk, cati, ct = u.renk(tarz), u.cati(tarz), u.ct(tarz)
                b = u.kutu(x0, x0 + w - 1, 0, d - 1, kat, tarz, renk, cati, ct)
                if u.rnd.random() < 0.3 and kat >= 2 and d < IZ:
                    b += u.kutu(x0, x0 + w - 1, d, d, max(1, kat - u.sec([1, 2])), tarz, renk, cati, u.sec([CT_TERAS, CT_OTO]))
                if u.rnd.random() < 0.12 and kat >= 3 and d >= 2:
                    b += u.kutu(x0, x0 + w - 1, 1, d - 1, 1, tarz, renk, cati, ct, k0=kat)
                s = []
                if u.rnd.random() < 0.3:
                    s.append(u.sus(u.sec(CALI + ["saksi", "bisiklet_park", "cop_kutusu", "odun_yigini", "bank"]), hucre_m(x0) + u.rnd.uniform(-0.8, 0.8 + (w - 1) * H),
                                   u.rnd.uniform(hucre_m(min(IZ - 1, d + 1)) - 0.8, 6.2)))
                zemin = u.sec(["bahce", "karisik", "avlu", "bahce"])
                if u.rnd.random() < dukkan_orani:
                    tur = u.sec(DUKKAN_TUR)
                    tabela = u.sec(PUB_TABELA) if tur == "pub" else u.sec(TABELA)
                    ok = u.kaydet(liste, "", tur, b, susler=s, tabela=tabela, tente=u.rnd.randrange(10), zemin=zemin, sutunlar=range(x0, x0 + w), modul=(x0, w))
                else:
                    ok = u.kaydet(liste, "", "konut", b, susler=s, zemin=zemin, sutunlar=range(x0, x0 + w), modul=(x0, w))
                sayac += 1 if ok else 0


ESKI_US = [GEO, GEO, VIC, SIVA, PORT, APT, GOT, MEWS, TAS]
KARMA_US = [GEO, VIC, VIC, SIVA, APT, APT, MEWS, PORT, GOT, MOD, FACH]


def eski(u, n):
    l = []
    sira_evler(u, l, n, [3, 4, 4, 5, 5, 6], ESKI_US, dukkan_orani=0.25)
    kiliseler(u, l, 3)
    moduller(u, l, 20, [3, 3, 4, 4, 5, 5, 6], ESKI_US, 0.25)
    return l


def karma(u, n):
    l = []
    sira_evler(u, l, n, [3, 3, 4, 4, 5], KARMA_US, dukkan_orani=0.6)
    kiliseler(u, l, 2)
    moduller(u, l, 20, [2, 3, 3, 4, 4, 5], KARMA_US, 0.6)
    return l


def kule(u, n):
    l = []
    deneme = 0
    while len(l) < n and deneme < n * 30:
        deneme += 1
        tarz = u.sec([MOD, MOD, PORT, GOT, VIC, GEO, APT, SIVA])
        renk, cati = u.renk(tarz), u.cati(tarz)
        kat = u.sec([5, 6, 6, 7, 8])
        x0, x1 = u.sec([(0, 4), (0, 3), (1, 4), (1, 3), (0, 2), (2, 4)])
        y0, y1 = 1, u.sec([2, 3, 3, 4])
        b = u.kutu(x0, x1, y0, y1, kat, tarz, renk, cati, u.ct(tarz))
        bicim = u.rnd.random()
        if bicim < 0.25:          # podyum: geniş alçak taban, üstte ince kule
            b = u.kutu(0, 4, 1, y1, 2, tarz, renk, cati, CT_TERAS) + u.kutu(x0 + (1 if x1 - x0 >= 2 else 0), x1, y0 + 1 if y1 - y0 >= 2 else y0, y1, kat - 2, tarz, renk, cati, u.ct(tarz), k0=2)
        elif bicim < 0.45:        # kademeli: üst katlar geri çekilir
            b = u.kutu(x0, x1, y0, y1, kat - 2, tarz, renk, cati, CT_TERAS) + u.kutu(x0, x1, y0 + 1, y1, 2, tarz, renk, cati, u.ct(tarz), k0=kat - 2)
        elif bicim < 0.6:         # L biçimli
            b += u.kutu(x0, x0 + 1, min(4, y1 + 1), 4, kat - u.sec([0, 1, 2]), tarz, renk, cati, u.ct(tarz))
        elif bicim < 0.7:         # ikiz kule
            t2 = u.sec([MOD, PORT, GOT])
            b = u.kutu(0, 1, 1, 3, kat, tarz, renk, cati, u.ct(tarz)) + u.kutu(3, 4, 1, 3, kat - u.sec([1, 2]), t2, u.renk(t2), u.cati(t2), u.ct(t2)) + \
                u.kutu(2, 2, 1, 3, 2, tarz, renk, cati, CT_TERAS)
        if u.rnd.random() < 0.4:
            tur = u.sec(DUKKAN_TUR[:5])
            u.kaydet(l, "", tur, b, tabela=u.sec(TABELA), tente=u.rnd.randrange(10), zemin="avlu")
        else:
            u.kaydet(l, "", "konut", b, zemin="karisik")
    return l


def mustakil(u, n, kucuk=False):
    """Müstakil ev, ikiz ev, bungalov, köşk: önde ve arkada bahçe."""
    l = []
    deneme = 0
    usluplar = [VIC, VIC, APT, APT, FACH, GEO, SIVA, TAS, GOT, MEWS] if not kucuk else [VIC, APT, APT, FACH, FACH, TAS, MEWS, GEO]
    while len(l) < n and deneme < n * 30:
        deneme += 1
        tarz = u.sec(usluplar)
        renk, cati, ct = u.renk(tarz), u.cati(tarz), u.ct(tarz)
        if ct == CT_PARAPET and u.rnd.random() < 0.7:
            ct = u.sec([CT_KIRMA, CT_BESIK])
        kat = u.sec([2, 2, 2, 3, 3]) if not kucuk else u.sec([1, 2, 2, 2])
        y0 = u.sec([1, 1, 1, 2]) if not kucuk else u.sec([0, 1, 1])
        bicim = u.rnd.random()
        if bicim < 0.22 and not kucuk:          # ikiz ev: iki yarım, farklı renk (biri bazen farklı üslup)
            r2 = u.renk(tarz) if u.rnd.random() < 0.7 else renk
            t2 = tarz if u.rnd.random() < 0.75 else u.sec(usluplar)
            ww = u.sec([(0, 1, 2, 4), (0, 2, 3, 4), (0, 1, 2, 3)])
            b = u.kutu(ww[0], ww[1], y0, y0 + 2, kat, tarz, renk, cati, ct) + u.kutu(ww[2], ww[3], y0, y0 + 2, kat, t2, r2, cati, ct)
            dolu = list(range(ww[0], ww[3] + 1))
        elif bicim < 0.45:                      # L biçimli ev
            x0 = u.sec([0, 1])
            b = u.kutu(x0, x0 + 3, y0, y0 + 1, kat, tarz, renk, cati, ct) + u.kutu(x0, x0 + 1, y0 + 2, min(4, y0 + 3), max(1, kat - u.sec([0, 1])), tarz, renk, cati, ct)
            dolu = [x0, x0 + 1, x0 + 2, x0 + 3]
        elif bicim < 0.6:                       # bungalov: geniş tek kat
            x0 = u.sec([0, 1])
            b = u.kutu(x0, x0 + u.sec([2, 3]), y0, y0 + 1, 1, tarz, renk, cati, u.sec([CT_KIRMA, CT_BESIK, CT_OTO]))
            dolu = list(range(x0, x0 + 4))
        elif bicim < 0.72 and not kucuk:        # köşk: kuleli
            x0 = u.sec([1, 2])
            b = u.kutu(x0, x0 + 1, y0, y0 + 1, kat, tarz, renk, cati, ct) + u.kutu(x0 - 1, x0 - 1, y0, y0, kat + 1, tarz, renk, cati, u.sec([CT_KIRMA, CT_OTO]))
            dolu = [x0 - 1, x0, x0 + 1]
        else:                                   # kare ev, yanında tek katlı ek (garaj / kış bahçesi)
            w = u.sec([2, 3]) if kucuk else u.sec([3, 3, 4])
            x0 = u.sec(list(range(0, IZ - w + 1)))
            d = u.sec([2, 2, 3])
            b = u.kutu(x0, min(4, x0 + w - 1), y0, min(4, y0 + d - 1), kat, tarz, renk, cati, ct)
            dolu = list(range(x0, x0 + w))
            if u.rnd.random() < 0.5:
                xe = x0 - 1 if x0 > 0 else x0 + w
                if 0 <= xe < IZ:
                    t_ek = u.sec([tarz, tarz, SERA, MEWS])
                    b += u.kutu(xe, xe, y0, y0, 1, t_ek, u.renk(t_ek), cati, u.sec([CT_TERAS, CT_BESIK, CT_OTO]))
                    dolu.append(xe)
        y_arka = max(bb["y"] for bb in b)
        s = u.bahce(dolu, y0, y_arka, agac=u.sec([0, 1, 1, 2]) if not kucuk else u.sec([0, 1, 1]), cali=u.sec([1, 2, 3]), ek=u.sec([0, 1, 1]))
        u.kaydet(l, "", "konut", b, susler=s, zemin="bahce")
    return l


def kampus(u, n):
    l = []
    deneme = 0
    while len(l) < n and deneme < n * 30:
        deneme += 1
        tarz = u.sec([GOT, GOT, PORT, VIC, MOD, GEO, DEPO])
        renk, cati, ct = u.renk(tarz), u.cati(tarz), u.ct(tarz)
        kat = u.sec([2, 3, 3, 4])
        bicim = u.rnd.random()
        if bicim < 0.35:         # U biçimli avlulu
            b = u.kutu(0, 4, 1, 1, kat, tarz, renk, cati, ct) + u.kutu(0, 0, 2, 4, kat, tarz, renk, cati, ct) + u.kutu(4, 4, 2, 4, kat, tarz, renk, cati, ct)
            s = [u.sus(u.sec(["agac_cinar_1", "agac_ihlamur_2", "agac_mese_2"]), u.rnd.uniform(-1.5, 1.5), u.rnd.uniform(1.0, 3.5), 0.8)]
        elif bicim < 0.6:        # ortada kule, yanlarda kanat
            b = u.kutu(0, 4, 1, 2, kat, tarz, renk, cati, ct) + u.kutu(2, 2, 1, 1, kat + u.sec([1, 2]), tarz, renk, cati, u.sec([CT_KIRMA, CT_OTO]))
            s = u.bahce([0, 1, 2, 3, 4], 1, 2, agac=1, cali=1, ek=1)
        else:                    # T biçimli
            b = u.kutu(0, 4, 1, 1, kat, tarz, renk, cati, ct) + u.kutu(1, 3, 2, u.sec([3, 4]), kat - u.sec([0, 1]), tarz, renk, cati, ct)
            s = []
        u.kaydet(l, "", "konut", b, susler=s, zemin="cim")
    return l


def sanayi(u, n):
    l = []
    deneme = 0
    while len(l) < n and deneme < n * 30:
        deneme += 1
        tarz = u.sec([FAB, FAB, FAB, DEPO, DEPO, MOD, SERA])
        renk, cati, ct = u.renk(tarz), u.cati(tarz), u.ct(tarz)
        kat = u.sec([1, 1, 2, 2, 3]) if tarz != SERA else 1
        x0, x1 = u.sec([(0, 4), (0, 3), (1, 4), (0, 2), (1, 3)])
        y1 = u.sec([2, 3, 4])
        b = u.kutu(x0, x1, 1, y1, kat, tarz, renk, cati, ct)
        if u.rnd.random() < 0.4:        # ofis kanadı (yüksek, dar) ya da ikinci hangar
            t2 = u.sec([MOD, VIC, DEPO, GEO])
            xo = x0 if u.rnd.random() < 0.5 else x1
            b += u.kutu(xo, xo, 1, 1, kat + 1, t2, u.renk(t2), u.cati(t2), u.ct(t2))
        s = []
        if u.rnd.random() < 0.6:
            for i in range(u.sec([1, 2, 3])):
                s.append(u.sus(u.sec(["kasalar", "fici", "odun_yigini", "araba_yeri", "kasalar"]), u.rnd.uniform(-6.0, 6.0), u.rnd.uniform(-6.3, -4.6)))
        u.kaydet(l, "", "konut", b, susler=s, zemin="avlu")
    return l


def avm(u, n):
    l = []
    deneme = 0
    while len(l) < n and deneme < n * 30:
        deneme += 1
        tarz = u.sec([MOD, MOD, MOD, PORT, DEPO])
        renk, cati = u.renk(tarz), u.cati(tarz)
        kat = u.sec([1, 2, 2, 3])
        b = u.kutu(0, 4, 1, u.sec([3, 4]), kat, tarz, renk, cati, u.ct(tarz))
        if u.rnd.random() < 0.4:
            b += u.kutu(u.sec([0, 3]), u.sec([1, 4]), 1, 2, 1, tarz, renk, cati, CT_TERAS, k0=kat)
        s = [u.sus("araba_yeri", u.rnd.uniform(-5, 5), -5.6)] if u.rnd.random() < 0.6 else []
        u.kaydet(l, "", u.sec(["konut", "dukkan"]), b, susler=s, zemin="avlu", tabela=u.sec(["OUTLET", "MEGASTORE", "GALLERIA", "ARCADE", "HOME & GARDEN",
                                                                               "FASHION HOUSE", "TECH STORE", "SPORTS"]), tente=u.rnd.randrange(10))
    return l


AGAC_PARK = ["agac_cinar_1", "agac_cinar_2", "agac_cinar_3", "agac_mese_1", "agac_mese_2", "agac_ihlamur_1", "agac_ihlamur_2",
             "agac_kestane_1", "agac_kestane_2", "agac_yuvarlak_1", "agac_yuvarlak_3", "agac_hus_1", "agac_sogut_1", "agac_sogut_2",
             "agac_kavak_1", "agac_kavak_2", "agac_cam_2"]


def park(u, n):
    """Parklar (yalnız süs): çim, yürüyüş yolları, ağaçlar, banklar, lamba, çiçek tarhı; bazılarında çeşme, heykel, gölet.
    Ayrıca meydan (çeşmeli, taş döşeli), anıt ve göl arsaları (City3D: meydan çeşmesi, City Park anıtı, park göleti)."""
    l = []
    deneme = 0
    while len(l) < n and deneme < n * 30:
        deneme += 1
        s = []
        yol = u.sec(["arti", "capraz", "kenar", "halka", "yok", "arti", "kenar"])
        avlu = set()
        if yol == "arti":
            avlu = {(2, y) for y in range(IZ)} | {(x, 2) for x in range(IZ)}
        elif yol == "kenar":
            avlu = {(x, 0) for x in range(IZ)} | {(0, y) for y in range(IZ)}
        elif yol == "halka":
            avlu = {(x, y) for x in range(1, 4) for y in range(1, 4)} - {(2, 2)}
        elif yol == "capraz":
            avlu = {(i, i) for i in range(IZ)} | {(i, IZ - 1 - i) for i in range(IZ)}
        gol = None
        if u.rnd.random() < 0.2:
            gol = u.sec([(c, d) for c in range(IZ) for d in range(IZ) if (c, d) not in avlu])
        for x in range(IZ):
            for y in range(IZ):
                z = "arsa_gol" if (x, y) == gol else "arsa_avlu" if (x, y) in avlu else ("arsa_bahce" if u.rnd.random() < 0.12 else "arsa_cim")
                s.append(dict(ad=z, x=round(hucre_m(x), 2), y=round(hucre_m(y), 2), aci=0.0, olcek=1.0, renk=0))
        bos = [(x, y) for x in range(IZ) for y in range(IZ) if (x, y) not in avlu and (x, y) != gol]
        u.rnd.shuffle(bos)
        for (x, y) in bos[:u.sec([2, 3, 3, 4, 5])]:
            s.append(u.sus(u.sec(AGAC_PARK), hucre_m(x) + u.rnd.uniform(-0.5, 0.5), hucre_m(y) + u.rnd.uniform(-0.5, 0.5), u.rnd.uniform(0.75, 1.05), u.rnd.randrange(3)))
        for (x, y) in bos[5:5 + u.sec([1, 2, 3])]:
            s.append(u.sus(u.sec(CALI + ["cicek_tarhi", "cicek_obegi", "ot_obegi"]), hucre_m(x), hucre_m(y)))
        yollar = sorted(avlu)
        u.rnd.shuffle(yollar)
        for (x, y) in yollar[:u.sec([2, 3, 4])]:
            s.append(u.sus(u.sec(["bank", "bank", "sokak_lambasi", "cop_kutusu", "bisiklet_park"]), hucre_m(x) + 1.0, hucre_m(y) + u.rnd.uniform(-0.6, 0.6)))
        if u.rnd.random() < 0.25 and avlu:
            x, y = u.sec(sorted(avlu))
            s.append(u.sus(u.sec(["cesme", "heykel", "cicek_havuzu"]), hucre_m(x), hucre_m(y)))
        u.kaydet(l, "", "konut", [], susler=s, zemin=None)
    # meydan (çeşmeli), anıt, göl: City3D bunları adlarından tanır
    for i in range(3):
        s = [dict(ad="arsa_avlu", x=round(hucre_m(x), 2), y=round(hucre_m(y), 2), aci=0.0, olcek=1.0, renk=0) for x in range(IZ) for y in range(IZ)]
        s += [u.sus("cesme", 0, 0, 1.4), u.sus("bank", -3.0, 0, 1.0), u.sus("bank", 3.0, 0, 1.0), u.sus("sokak_lambasi", -5.6, -5.6), u.sus("sokak_lambasi", 5.6, 5.6),
              u.sus(u.sec(AGAC_PARK), -5.0, 5.0, 0.9), u.sus(u.sec(AGAC_PARK), 5.0, -5.0, 0.9), u.sus("cicek_tarhi", 0, -4.2), u.sus("cicek_tarhi", 0, 4.2)]
        u.kaydet(l, "meydan", "konut", [], susler=s, zemin=None)
    for i in range(2):
        s = [dict(ad="arsa_avlu" if (x in (1, 2, 3) and y in (1, 2, 3)) else "arsa_cim", x=round(hucre_m(x), 2), y=round(hucre_m(y), 2), aci=0.0, olcek=1.0, renk=0)
             for x in range(IZ) for y in range(IZ)]
        s += [u.sus("heykel", 0, 0, 1.8), u.sus("cicek_tarhi", -2.8, 0), u.sus("cicek_tarhi", 2.8, 0)] + \
             [u.sus(u.sec(AGAC_PARK), hucre_m(x), hucre_m(y), 0.9) for x, y in ((0, 0), (4, 0), (0, 4), (4, 4))]
        u.kaydet(l, "anit", "konut", [], susler=s, zemin=None)
    for i in range(2):
        s = [dict(ad="arsa_gol" if (1 <= x <= 3 and 1 <= y <= 3) else "arsa_cim", x=round(hucre_m(x), 2), y=round(hucre_m(y), 2), aci=0.0, olcek=1.0, renk=0)
             for x in range(IZ) for y in range(IZ)]
        s += [u.sus("agac_sogut_1", -5.4, 5.2, 0.9), u.sus("agac_sogut_2", 5.2, -5.4, 0.9), u.sus("bank", 0, -6.0, 1.0), u.sus("kayik", 0.5, 0.5, 0.8)]
        u.kaydet(l, "gol", "konut", [], susler=s, zemin=None)
    return l


def ciftlik(u, n):
    """Kırsal: çiftlik evi, ahır / samanlık, sera; çim avlu, saman balyası, kuyu, sebze bahçesi, koyun, meyve ağacı."""
    l = []
    deneme = 0
    while len(l) < n and deneme < n * 30:
        deneme += 1
        b = []
        ev_t = u.sec([FACH, TAS, VIC, APT, FACH])
        x0 = u.sec([0, 1, 2])
        b += u.kutu(x0, x0 + u.sec([1, 2]), 1, 2, u.sec([1, 2, 2]), ev_t, u.renk(ev_t), u.cati(ev_t), u.sec([CT_BESIK, CT_KIRMA, CT_OTO]))
        dolu = {(bb["x"], bb["y"]) for bb in b}
        ek = u.sec(["ahir", "ahir", "sera", "yok", "ahir"])
        if ek != "yok":
            t2 = DEPO if ek == "ahir" else SERA
            xa = 3 if x0 <= 1 else 0
            ya = u.sec([2, 3])
            b += u.kutu(xa, min(4, xa + 1), ya, min(4, ya + 1), 1 if ek == "sera" else u.sec([1, 2]), t2,
                        u.sec([12, 13, 7]) if ek == "ahir" else 6, u.sec([5, 9, 8]), CT_BESIK if ek == "ahir" else CT_OTO)
        s = []
        dolu = {(bb["x"], bb["y"]) for bb in b}
        bos = [(x, y) for x in range(IZ) for y in range(IZ) if (x, y) not in dolu]
        u.rnd.shuffle(bos)
        for (x, y) in bos[:u.sec([3, 4, 5])]:
            s.append(u.sus(u.sec(["saman_balyasi", "kuyu", "sebze_bahcesi", "koyun", "koyun", "odun_yigini", "agac_meyve_1", "agac_meyve_2", "cit_ahsap", "kutuk"]),
                           hucre_m(x) + u.rnd.uniform(-0.5, 0.5), hucre_m(y) + u.rnd.uniform(-0.5, 0.5)))
        u.kaydet(l, "", "konut", b, susler=s, zemin=u.sec(["cim", "cim", "bahce"]))
    return l


def kiliseler(u, l, n):
    """Gotik kilise: nef (beşik çatı) ve sivri kuleli çan kulesi. Adında _kilise_: City3D şehirde en çok ikisini koyar."""
    for i in range(n):
        renk = u.sec([10, 11, 12, 15, 6])
        nef = u.kutu(1, 3, 0, 3, u.sec([2, 3]), GOT, renk, u.sec(ARDUVAZ), CT_BESIK)
        kx = u.sec([0, 4])
        kule = u.kutu(kx, kx, 0, 0, u.sec([4, 5, 6]), GOT, renk, u.sec(ARDUVAZ), CT_OTO)
        s = [u.sus(u.sec(AGAC_PARK), hucre_m(4 - kx), hucre_m(4), 0.9), u.sus("bank", hucre_m(kx), hucre_m(2), 1.0)]
        u.kaydet(l, "kilise", "konut", nef + kule, susler=s, zemin="cim")


# bölge → (üretici, tam tasarım sayısı). Eski şehir ve çarşıda ayrıca 12 şerit yuvası × 20 şerit; şehirde çıkanlar oyun
# çalışırken (arka planda, kameraya yakın olanlar önce) kurulur.
BOLGELER = [("eski", eski, 30), ("karma", karma, 30), ("kule", kule, 60), ("ev", lambda u, n: mustakil(u, n), 120),
            ("banliyo", lambda u, n: mustakil(u, n, kucuk=True), 100), ("kampus", kampus, 30), ("sanayi", sanayi, 60), ("avm", avm, 16),
            ("park", park, 30), ("ciftlik", ciftlik, 30)]


def tasarimlar():
    hepsi = []
    for i, (bolge, fn, n) in enumerate(BOLGELER):
        u = Uretici(bolge, 7001 + i * 131)
        hepsi += fn(u, n)
    return hepsi


def yaz(yol):
    l = tasarimlar()
    veri = {"tasarimlar": [json.dumps(t, ensure_ascii=False, separators=(",", ":")) for t in l]}
    with open(yol, "w", encoding="utf-8") as f:
        json.dump(veri, f, ensure_ascii=False, separators=(",", ":"))
    from collections import Counter
    print("YAZILDI", yol, len(l), "tasarım", dict(Counter(t["bolge"] for t in l)), "%.0f KB" % (os.path.getsize(yol) / 1024.0))


def onizleme(klasor, her=12):
    from PIL import Image, ImageDraw
    os.makedirs(klasor, exist_ok=True)
    l = tasarimlar()
    boy, sut = 360, 6
    for bolge, _, _ in BOLGELER:
        secim = [t for t in l if t["bolge"] == bolge][:her]
        k = Image.new("RGB", (boy * sut, (boy + 20) * ((len(secim) + sut - 1) // sut)), P.ARKA)
        d = ImageDraw.Draw(k)
        for i, t in enumerate(secim):
            im = P._ciz(P.bina(t), boy, 28, 16)
            x, y = (i % sut) * boy, (i // sut) * (boy + 20)
            k.paste(im, (x, y))
            d.text((x + 6, y + boy + 3), "%s (%s)" % (t["ad"], t["tur"]), fill=(60, 50, 60))
        k.save(os.path.join(klasor, "sehir_%s.jpg" % bolge), quality=86)
        print("CIZILDI", bolge, len(secim))


if __name__ == "__main__":
    if "--onizleme" in sys.argv:
        onizleme(sys.argv[sys.argv.index("--onizleme") + 1])
    else:
        yaz(os.path.join(P.depo_koku(), "Resources", "Atolye", "Tasarimlar", "sehir_paketi.json"))
