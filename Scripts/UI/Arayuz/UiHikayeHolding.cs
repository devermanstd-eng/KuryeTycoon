using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yeni sekmeler (UI Toolkit, Yazici ile):
    ///  • Hedefler > Hikâye: bu şehrin bölümü, hedefleri ve bütün bölümlerin durumu (Hikaye).
    ///  • Şirket > Holding: iştirakler, temettü, yönetime yatırım; şirketi holdinge katma (Holding).
    ///  • İşletme > Bölgeler: bölge müdürleri, adaylar ve atama (BolgeMudurleri).
    ///  • Ekip > Efsaneler: rütbeler, efsane kuryeler ve koleksiyon (KuryeKariyer).
    /// Hepsi yarım saniyede bir yenilenir.
    /// </summary>
    public partial class GameUI
    {
        abstract class UiYaziciSekme : UiSekme
        {
            protected Yazici y;
            float son = -10f;
            protected override void Kur() { y = new Yazici(ui, kok); }
            public override void Acildi() { son = -10f; }
            public override void Yenile()
            {
                if (GameClock.I == null || Time.unscaledTime - son < 0.5f) return;
                son = Time.unscaledTime;
                y.Basla();
                Yaz();
                y.Bitir();
            }
            protected abstract void Yaz();
        }

        // ================================================================== Hikâye

        class UiHikaye : UiYaziciSekme
        {
            protected override void Yaz()
            {
                var b = Hikaye.Aktif;
                y.Yazi("Her şehir bir bölüm: üç hedefi tamamla, Kovács Viktor'u ve Turbo Grup'u geride bırak.");
                y.Yardim($"Bölüm bitince +{Hikaye.BolumOdulu} marka puanı (her bölüm bir kez). Sonra şirketi satıp ya da holdinge katıp sonraki şehre geçersin. " +
                         "Bölümü bitirmeden satarsan o bölüm yarım kalır. Budapeşte son bölüm.");
                if (!Hikaye.Etkin)
                {
                    y.Uyari(TestModu.Aktif ? "Otomatik testte hikâye kapalı." : "Hikâye sadece Klasik oyunda. Bu senaryoda bölüm hedefleri sayılmaz.");
                }
                var h = Hikaye.I;
                int tamam = Hikaye.TamamHedef(b);
                y.KutularBasla();
                y.Kutu("Bölüm", $"{Hikaye.BolumNo + 1} / 7", Marka.SehirAdi, UITheme.Accent);
                y.Kutu("Hedefler", $"{tamam} / {b.hedefler.Length}", h != null && h.bitti ? "bölüm bitti" : "bu şehirde", tamam == b.hedefler.Length ? U.Iyi : (Color?)null);
                y.Kutu("Biten bölümler", $"{Hikaye.TamamSayisi} / 7", Hikaye.Veri.final ? "final görüldü" : "bütün oyunlar");
                y.KutularBitir();

                y.KartBasla();
                y.Baslik(b.baslik);
                y.Kucuk(Hikaye.Yaz(b.giris));
                foreach (var hd in b.hedefler)
                {
                    bool ok = hd.tamam();
                    y.KV((ok ? "✓ " : "") + hd.ad, hd.durum(), ok ? U.Iyi : (Color?)null);
                }
                if (h != null && h.bitti) y.Not(Hikaye.Yaz(b.bitis));
                y.KartBitir();

                y.Bolum("Bütün bölümler");
                for (int i = 0; i < Hikaye.Bolumler.Length; i++)
                {
                    var bb = Hikaye.Bolumler[i];
                    bool bitti = Hikaye.Veri.tamam[i];
                    string durum = bitti ? "tamamlandı" : i == Hikaye.BolumNo ? "şimdi" : i < Hikaye.BolumNo ? "yarım kaldı" : "kilitli";
                    y.Noktali($"{bb.baslik} — {durum}", bitti ? U.Iyi : i == Hikaye.BolumNo ? (Color)UITheme.Accent : U.Soluk);
                }
            }
        }

        // ================================================================== Holding

        class UiHolding : UiYaziciSekme
        {
            protected override void Yaz()
            {
                var v = Holding.Veri;
                y.Yazi("Şirketini satmak yerine holdinge katabilirsin: kapanmaz, sonraki bütün oyunlarında her sabah temettü gönderir.");
                y.Yardim($"Holdinge katınca marka puanının yarısını alırsın ve yine sonraki şehirde baştan başlarsın. Temettü = son 7 günün ortalama " +
                         $"kârının %{Holding.TemettuOrani * 100f:0}'i × payın (en az {Ft(Holding.EnAzTemettu)}/gün); sermaye olarak girer, işletme kârına sayılmaz. " +
                         "Yönetime yatırımla her iştirakin temettüsü artar. Her iştirak sipariş +%2 (en fazla +%12). Otomatik testte kapalı.");
                y.KutularBasla();
                y.Kutu("Holding", Holding.Unvan, $"{Holding.IstirakSayisi} iştirak", UITheme.Accent);
                y.Kutu("Günlük temettü", Ft(Holding.GunlukToplam), $"toplam ödenen {Ft(v.toplamTemettu)}", Holding.GunlukToplam > 0 ? U.Iyi : (Color?)null);
                y.Kutu("Sinerji", $"sipariş +%{(Holding.SiparisCarpani - 1f) * 100f:0}", $"iştiraklerin değeri {Ft(Holding.ToplamDeger)}");
                y.KutularBitir();

                y.Bolum("Bu şirket");
                y.KartBasla();
                y.Baslik($"{CompanyProfile.Name} ({Marka.SehirAdi})");
                string engel = Marka.SatisEngeli;
                y.KV("Holdinge katılırsa", $"günlük {Ft(Holding.OlasiTemettu)} temettü · {Holding.OlasiPuan} marka puanı");
                y.KV("Satılırsa", $"{Marka.PuanFor(Marka.PayinDegeri)} marka puanı");
                if (engel.Length > 0) y.Uyari($"Şimdilik olmaz: {engel}.");
                else
                {
                    string sonraki = Marka.Sehirler[Mathf.Min(Marka.Veri.satilan + 1, Marka.Sehirler.Length - 1)];
                    y.Dugme($"Holdinge kat ve {sonraki} şehrine geç", () => ui.AskConfirm("Holdinge kat",
                        $"{CompanyProfile.Name} holdingine katılacak: her sabah {Ft(Holding.OlasiTemettu)} temettü gönderecek, {Holding.OlasiPuan} marka puanı alacaksın " +
                        $"(satsaydın {Marka.PuanFor(Marka.PayinDegeri)}). Bu oyun biter ve kaydı silinir; {sonraki} şehrinde yeni bir şirketle başlarsın.",
                        "Evet, holdinge kat", () =>
                        {
                            int p = Holding.HoldingeKat();
                            if (p > 0) ui.deferred = () => GameBootstrap.Restart();
                        }), true, "kucuk-dg");
                }
                y.KartBitir();

                y.Bolum("İştirakler");
                if (v.istirakler.Count == 0) y.Kucuk("Henüz iştirak yok. İlk şirketini holdinge kattığında burada görünür.");
                foreach (var i in v.istirakler)
                {
                    var ii = i;
                    y.KartBasla();
                    y.Baslik($"{i.ad} · {i.sehir}");
                    y.KV("Günlük temettü", $"{Ft(Holding.Temettu(i))} (yönetim seviyesi {i.seviye}/{Holding.MaksSeviye})", U.Iyi);
                    y.KV("Katıldığındaki değer", Ft(i.deger));
                    y.KV("Ödediği temettü", $"{Ft(i.odenen)} · yatırdığın {Ft(i.yatirilan)}");
                    if (i.seviye < Holding.MaksSeviye)
                    {
                        long bedel = Holding.YatirimBedeli(i);
                        y.Dugme($"Yönetime yatır ({Ft(bedel)}) → günlük {Ft(i.temettu + i.temettu * (i.seviye + 1) / 2)}", () => Holding.Yatir(ii),
                                EconomyManager.I != null && EconomyManager.I.FreeCash >= bedel, "kucuk-dg");
                    }
                    y.KartBitir();
                }
            }
        }

        // ================================================================== Bölge müdürleri

        class UiBolgeler : UiYaziciSekme
        {
            protected override void Yaz()
            {
                var bm = BolgeMudurleri.I;
                var map = CityMap.I;
                if (bm == null || map == null) return;
                y.Yazi("Her bölgeye bir müdür ata: bölgedeki dükkanları senin yerine yönetsin.");
                y.Yardim("Müdür bölgesindeki dükkanların siparişini artırır, memnuniyetlerini her gün yükseltir, sözleşme yenilemede rakip teklif " +
                         "gelince kart açmadan karar verir (mevcut şartlarla tutar ya da teklife eşitler) ve istersen yeni dükkanlarla anlaşır " +
                         "(kasada bedelin 3 katı varsa). Görevde kaldıkça deneyim kazanır (5 seviye). Maaşı her gün ödenir; atarken bir aylık maaş işe alım bedeli.");
                if (!BolgeMudurleri.Acik) { y.Uyari("İkinci bölge açılınca müdür atayabilirsin (Gelişim > yeni bölge)."); return; }

                y.KutularBasla();
                y.Kutu("Müdürler", $"{bm.Mudurler.Count} / {map.UnlockLevel}", "açık bölge başına bir", UITheme.Accent);
                y.Kutu("Günlük maaş", Ft(bm.GunlukToplamMaas), $"toplam ödenen {Ft(bm.odenen)}");
                int yen = 0, imz = 0;
                foreach (var m in bm.Mudurler) { yen += m.yenilenen; imz += m.imzalanan; }
                y.Kutu("İşleri", $"{imz} yeni · {yen} yenileme", "sözleşme");
                y.KutularBitir();

                for (int b = 1; b <= map.UnlockLevel; b++)
                {
                    var m = bm.MuduruOf(b);
                    y.KartBasla();
                    y.Baslik($"{CityMap.DistrictNames[b]} · {bm.DukkanSayisi(b, true)} sözleşmeli, {bm.DukkanSayisi(b, false)} sözleşmesiz dükkan");
                    if (m != null)
                    {
                        var mm = m;
                        int lv = BolgeMudurleri.Seviye(m);
                        int kalan = BolgeMudurleri.SonrakiSeviyeyeGun(m);
                        y.KV("Müdür", $"{m.ad} · {BolgeMudurleri.UzmanlikAdlari[m.uzmanlik]} · yetenek %{m.yetenek * 100f:0}");
                        y.KV("Deneyim", $"{lv}. seviye · {m.gun} gün" + (kalan > 0 ? $" (sonraki seviyeye {kalan} gün)" : " (en üst)"));
                        y.KV("Etkisi", $"sipariş +%{BolgeMudurleri.TalepEki(m) * 100f:0.#} · memnuniyet +{BolgeMudurleri.MemnuniyetArtisi(m):0.#}/gün", U.Iyi);
                        y.KV("Maaş", $"{Ft(BolgeMudurleri.AylikMaas(m))}/ay ({Ft(BolgeMudurleri.GunlukMaas(m))}/gün)");
                        y.KV("İşleri", $"{m.imzalanan} yeni sözleşme · {m.yenilenen} yenileme");
                        y.Onay("Yeni dükkanlarla kendisi anlaşsın", m.otoSozlesme, val => mm.otoSozlesme = val);
                        y.Dugme("Görevden al", () => ui.AskConfirm("Görevden al", $"{mm.ad} bölge müdürlüğünden ayrılacak. İşe alım bedeli geri gelmez.",
                            "Evet, görevden al", () => bm.GorevdenAl(mm)), true, "kucuk-dg");
                    }
                    else
                    {
                        y.Kucuk("Müdür yok: sözleşme yenilemeleri sana kart olarak gelir.");
                        int bb = b;
                        foreach (var a in bm.Adaylar)
                        {
                            var aa = a;
                            long bedel = BolgeMudurleri.IseAlimBedeli(a);
                            y.Dugme($"{a.ad} ({BolgeMudurleri.UzmanlikAdlari[a.uzmanlik]}, yetenek %{a.yetenek * 100f:0}" + (a.gun > 0 ? ", tecrübeli" : "") +
                                    $") ata — {Ft(bedel)}, sonra {Ft(BolgeMudurleri.AylikMaas(a))}/ay",
                                    () => bm.Ata(aa, bb), EconomyManager.I.FreeCash >= bedel, "kucuk-dg");
                        }
                    }
                    y.KartBitir();
                }

                y.Bolum("Adaylar");
                for (int u = 0; u < BolgeMudurleri.UzmanlikAdlari.Length; u++)
                    y.KV(BolgeMudurleri.UzmanlikAdlari[u], BolgeMudurleri.UzmanlikAciklamalari[u]);
                if (bm.Adaylar.Count == 0) y.Kucuk("Aday yok. Ara: 3 aday gelir (ücretsiz).");
                y.Dugme(bm.AdayYenilenebilir ? "Aday ara" : "Aday ara (3 günde bir)", () => bm.AdaylariYenile(), bm.AdayYenilenebilir, "kucuk-dg");
            }
        }

        // ================================================================== Efsaneler ve rütbeler

        class UiEfsaneler : UiYaziciSekme
        {
            protected override void Yaz()
            {
                var cm = CourierManager.I;
                if (cm == null) return;
                y.Yazi("Kuryeler teslimat yaptıkça seviye atlar ve rütbe kazanır. Çok nadiren lakaplı bir efsane kurye başvurur.");
                y.Yardim("Seviye hızı artırır (seviye başı +%4); rütbeler küçük ayrıcalıklar ekler. Efsaneler başvuruların yaklaşık %1'i (şirketin 5. gününden sonra), " +
                         "kayıt sürecinde neredeyse hiç vazgeçmez. İşe aldığın efsaneler bu bilgisayardaki koleksiyona yazılır.");

                var sayac = new int[KuryeKariyer.Rutbeler.Length];
                int efsane = 0;
                foreach (var c in cm.Couriers) { sayac[KuryeKariyer.Rutbe(c.level)]++; if (KuryeKariyer.EfsaneMi(c)) efsane++; }
                y.KutularBasla();
                y.Kutu("Efsaneler ekipte", efsane.ToString(), $"koleksiyon {KuryeKariyer.KoleksiyonSayisi}/{KuryeKariyer.EfsaneSayisi}", MapFx.Altin);
                y.Kutu("Usta ve üstü", (sayac[3] + sayac[4] + sayac[5] + sayac[6]).ToString(), $"{cm.Couriers.Count} kuryeden");
                y.Kutu("Seri", Kutlama.I != null ? $"×{Kutlama.I.seri}" : "—", Kutlama.I != null ? $"rekor {Kutlama.I.enIyiSeri}" : "");
                y.KutularBitir();

                y.Bolum("Rütbeler");
                for (int r = 0; r < KuryeKariyer.Rutbeler.Length; r++)
                {
                    int minLv = 0;
                    for (int lv = 1; lv < 40; lv++) if (KuryeKariyer.Rutbe(lv) == r) { minLv = lv; break; }
                    string ayr = KuryeKariyer.RutbeAyricaliklari[r];
                    y.KV($"{KuryeKariyer.Rutbeler[r]} (seviye {minLv}+) · {sayac[r]} kurye", ayr == "—" ? "seviye hız bonusu" : ayr);
                }

                y.Bolum("Ekipteki en tecrübeliler");
                var liste = new System.Collections.Generic.List<Courier>(cm.Couriers);
                liste.Sort((a, b) => b.level != a.level ? b.level.CompareTo(a.level) : b.deliveries.CompareTo(a.deliveries));
                for (int i = 0; i < liste.Count && i < 8; i++)
                {
                    var c = liste[i];
                    int sonraki = KuryeKariyer.SonrakiRutbeSeviyesi(c.level);
                    y.Noktali($"{c.courierName} · {KuryeKariyer.RutbeAdi(c.level)} (seviye {c.level}) · {c.deliveries} teslimat" +
                              (sonraki > 0 ? $" · {KuryeKariyer.Rutbeler[KuryeKariyer.Rutbe(c.level) + 1]} için seviye {sonraki}" : ""),
                              KuryeKariyer.EfsaneMi(c) ? MapFx.Altin : U.Soluk);
                }
                if (liste.Count == 0) y.Kucuk("Henüz kurye yok.");

                y.Bolum("Efsane koleksiyonu");
                for (int e = 0; e < KuryeKariyer.EfsaneSayisi; e++)
                {
                    bool sahip = KuryeKariyer.Koleksiyonda(e);
                    y.KartBasla();
                    y.Baslik((sahip ? "★ " : "? ") + KuryeKariyer.EfsaneLakaplari[e]);
                    y.Kucuk(sahip ? KuryeKariyer.EfsaneAciklamalari[e] : "Henüz tanışmadın. Başvurular arasında bir gün çıkabilir.");
                    y.KartBitir();
                }
            }
        }
    }
}
