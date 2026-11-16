using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kendi Marketin penceresi (UI Toolkit, Yazıcı ile; eski IMGUI DrawMarket yerine).
    /// Kapalıyken açılış kartı (bedel, günlük gider, ilk stok). Açıkken bugünün sayıları kutularda, depo bilgisi ve işlemler;
    /// her reyon tek kart: doluluk çubuğu, çeşit seçimi, doldur. Otomatik stok ve hedef doluluk en altta.
    /// </summary>
    public partial class GameUI
    {
        class UiMarket : UiSekme
        {
            Yazici y;
            readonly string[] cesitAdlari = new string[3];

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Yenile()
            {
                var ds = DarkStore.I;
                if (ds == null || GameClock.I == null) return;
                y.Basla();
                y.Yazi($"Kendi deponu aç, market siparişlerini komisyonsuz sat. Ürünleri sen alırsın; brüt marj ~%{DarkStore.Margin * 100f:0}.");
                y.Yardim("Stok alış fiyatıyla tutulur. Stokta olmayan ürünler siparişten çıkar; sepetin çoğu yoksa sipariş kaçar. " +
                         "Boş raflı markete müşteri az gelir. Taze ürün her gün ~%12 bozulur. Çeşit arttıkça daha çok sipariş gelir ama raf ve soğutma gideri artar. " +
                         "Depo büyüdükçe kira ve toplayıcı sayısı artar, stok kapasitesi ve talep de artar.");

                if (!ds.IsOpen) { Kapali(ds); y.Bitir(); return; }

                // Bugün
                y.KutularBasla();
                y.Kutu("Bugün sipariş", ds.ordersToday.ToString(), ds.lostToday > 0 ? $"stoktan kaçan {ds.lostToday}" : null, null, null,
                       ds.lostToday > 0 ? UITheme.Warn : (Color?)null);
                y.Kutu("Satış", Ft(ds.salesToday), "bugün");
                y.Kutu("Brüt kâr", Ft(ds.profitToday), $"gider {Ft(ds.DailyCost)}/gün", MoneyColor(ds.profitToday - ds.DailyCost),
                       "Brüt kâr = satış − ürünün alış fiyatı. Kira, toplayıcı ve raf gideri her gece ayrıca kesilir.");
                y.Kutu("Stok", $"%{ds.TotalStock * 100f / Mathf.Max(1L, ds.Capacity):0}", $"{Ft(ds.TotalStock)} / {Ft(ds.Capacity)}", null,
                       "Stok alış fiyatıyla tutulur. Taze ürün her gece ~%12 bozulur: fazla stok tutma.");
                y.KutularBitir();
                y.Kucuk($"{DarkStore.StoreName} · {DarkStore.SizeNames[ds.size]} · {CityMap.DistrictNames[CityMap.RegionOf(ds.shop.tile)]} · " +
                        $"{GameClock.I.Day - ds.openedDay + 1}. gün · {DarkStore.SizePickers[ds.size]} toplayıcı");
                y.Kucuk($"Toplam {ds.ordersTotal} sipariş · satış {Ft(ds.salesTotal)} · brüt kâr {Ft(ds.profitTotal)} · bozulan {Ft(ds.spoiledTotal)}");
                y.SatirBasla();
                var tile = ds.shop.tile;
                y.Dugme("Haritada göster", () => { if (MapCamera.I != null) MapCamera.I.FocusOn(tile, 0.75f); }, true, "kucuk-dg");
                if (ds.size < 3)
                {
                    int yeni = ds.size + 1;
                    long bedel = DarkStore.SizeCost[yeni];
                    y.Dugme($"Büyüt: {DarkStore.SizeNames[yeni]} — {Ft(bedel)}", () => ui.AskConfirm("Depo büyütülsün mü?",
                        $"{DarkStore.SizeNames[yeni]}: stok kapasitesi {Ft(DarkStore.SizeCapacity[yeni])}, {DarkStore.SizePickers[yeni]} toplayıcı, " +
                        $"kira {Ft(DarkStore.SizeRent[yeni])}/gün. Daha çok sipariş gelir.", "Büyüt", () => ds.Upgrade()), true, "kucuk-dg");
                }
                y.SatirBitir();

                // Reyonlar
                y.Bolum("Reyonlar", "Çeşit arttıkça daha çok sipariş gelir ama raf ve soğutma gideri artar.");
                for (int lv = 1; lv <= 3; lv++)
                    cesitAdlari[lv - 1] = DarkStore.AssortNames[lv] + (DarkStore.AssortDaily[lv] > 0 ? $" +{Ft(DarkStore.AssortDaily[lv])}" : "");
                long hepsi = 0;
                for (int a = 0; a < DarkStore.Aisles; a++)
                {
                    int reyon = a;
                    float dolu = ds.Fill01(a);
                    long eksik = System.Math.Max(0, ds.AisleCapacity(a) - ds.stock[a]);
                    hepsi += eksik;
                    y.KartBasla();
                    y.Cubuk(DarkStore.AisleNames[a], dolu, dolu < 0.25f ? UITheme.Bad : dolu < 0.5f ? UITheme.Warn : UITheme.Good,
                            $"{Ft(ds.stock[a])} / {Ft(ds.AisleCapacity(a))}");
                    y.Secim(cesitAdlari, ds.assortment[a] - 1, v => ds.SetAssortment(reyon, v + 1));
                    y.Dugme($"Doldur ({Ft(eksik / 100 * 100)})", () => ds.Restock(reyon, 1f, true), eksik > 1000, "kucuk-dg");
                    y.KartBitir();
                }
                y.Dugme($"Bütün reyonları doldur ({Ft(hepsi / 100 * 100)})", () => ds.RestockAll(1f, true), hepsi > 1000, "vurgu");

                y.Bolum("Otomatik stok");
                y.Onay($"Her sabah reyonları %{ds.restockTarget * 100f:0}'e tamamla (serbest kasadan)", ds.autoRestock, v => ds.autoRestock = v);
                if (ds.autoRestock)
                    y.Kaydirici("Hedef doluluk", ds.restockTarget, 0.4f, 1f, $"%{ds.restockTarget * 100f:0}", v => ds.restockTarget = Mathf.Round(v * 20f) / 20f);
                y.Bitir();
            }

            void Kapali(DarkStore ds)
            {
                long gider = DarkStore.SizeRent[1] + DarkStore.SizePickers[1] * DarkStore.PickerDaily + 5 * DarkStore.AssortDaily[2];
                y.KartBasla();
                y.Baslik($"{DarkStore.StoreName} — {DarkStore.SizeNames[1]}");
                y.KutularBasla();
                y.Kutu("Açılış", Ft(DarkStore.SizeCost[1]), "raf, soğutucu, tadilat");
                y.Kutu("Günlük gider", $"~{Ft(gider)}", $"kira, {DarkStore.SizePickers[1]} toplayıcı, raflar");
                y.Kutu("İlk stok", $"~{Ft(DarkStore.SizeCapacity[1] * 8 / 10)}", "ayrıca gerekir");
                y.KutularBitir();
                y.Kucuk("Depo merkeze yakın, açık bir bölgede kurulur. Kuryeler siparişi depodan alır, hazırlık ~6 dk.");
                string kilit = ds.LockReason;
                if (kilit != "") y.Uyari(kilit);
                y.Dugme($"Marketi aç — {Ft(DarkStore.SizeCost[1])}", () => ui.AskConfirm($"{DarkStore.StoreName} açılsın mı?",
                    $"{Ft(DarkStore.SizeCost[1])} açılış bedeli serbest kasadan ödenir. Her gün kira, toplayıcı ve raf gideri çıkar. " +
                    "Açtıktan sonra raflar boş: stok almayı unutma (otomatik stok varsayılan açık).", "Aç", () => ds.Open()), kilit == "", "vurgu");
                y.KartBitir();
            }
        }
    }
}
