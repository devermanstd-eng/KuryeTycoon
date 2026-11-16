using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kampanyalar penceresi (UI Toolkit, Yazıcı ile; eski IMGUI DrawCampaigns yerine).
    /// Üstte bu haftanın kampanya giderleri sayı kutularında; bugünün boostları tabloda; kötü hava, hafta sonu, hafta içi boostları ve
    /// haftalık/aylık hedef ödülü: kapalı olanın kaydırıcıları gizli (pencere kısa kalır). Hedef ödülü kârla karşılaştırılır.
    /// </summary>
    public partial class GameUI
    {
        class UiKampanyalar : UiSekme
        {
            Yazici y;

            protected override void Kur() { y = new Yazici(ui, kok); }

            static float Yuzde5(float v) => Mathf.Round(v * 20f) / 20f;   // %5 adımlar
            static string Yuzde(float v) => $"%{v * 100f:0}";

            public override void Yenile()
            {
                var camp = CampaignManager.I;
                var clock = GameClock.I;
                var eco = EconomyManager.I;
                var wx = WeatherSystem.I;
                if (camp == null || clock == null || eco == null || wx == null) return;
                y.Basla();

                y.Yazi("Boost saatinde kuryeler daha çok çevrimiçi olur ve sipariş reddetmez. Bedeli kârından ödenir.");
                y.Yardim("Kampanyalar şirketin kârından ödenir; müşterinin teslimat ücretine yansımaz. Boost saatinde çevrimdışı kuryeler " +
                         "çevrimiçi olmaya daha istekli olur, kuryeler sipariş reddetmez ve aynı anda +1 paket alır.");

                var aktif = camp.ActiveWindow;
                y.KutularBasla();
                y.Kutu("Şu an", aktif != null ? Yuzde(aktif.percent) : camp.WeatherBoostPercent > 0f ? Yuzde(camp.WeatherBoostPercent) : "boost yok",
                       aktif != null ? aktif.label : camp.WeatherBoostPercent > 0f ? "hava boostu" : null, aktif != null || camp.WeatherBoostPercent > 0f ? UITheme.Good : (Color?)null);
                y.Kutu("Bugünkü gider", Ft(eco.Today.boosts + eco.Today.rewards), "boost + ödül");
                y.Kutu("Bu hafta boost", Ft(camp.boostPaidThisWeek), $"{clock.WeekOfMonth}. hafta");
                y.Kutu("Hedef ödülleri", Ft(camp.rewardsPaidThisWeek + camp.rewardsPaidThisMonth),
                       $"hafta {camp.rewardsClaimedThisWeek} · ay {camp.rewardsClaimedThisMonth} kurye", null,
                       $"Bu hafta haftalık hedef ödülü {Ft(camp.rewardsPaidThisWeek)} ({camp.rewardsClaimedThisWeek} kurye), " +
                       $"bu ay aylık hedef ödülü {Ft(camp.rewardsPaidThisMonth)} ({camp.rewardsClaimedThisMonth} kurye).");
                y.KutularBitir();

                // Bugünün boostları
                y.Bolum($"Bugünün boostları · {clock.DayName}{(clock.IsWeekend ? " (hafta sonu)" : "")}");
                var bugun = camp.TodayWindows();
                if (bugun.Count == 0) y.Kucuk("Bugün saat boostu yok.");
                foreach (var w in bugun)
                {
                    bool su = aktif != null && w.startHour == aktif.startHour && w.percent == aktif.percent;
                    bool bitti = clock.Hour >= w.endHour;
                    y.Tablo(new[] { $"{w.TimeText} · {w.label}", Yuzde(w.percent), su ? "şu an" : bitti ? "bitti" : "" }, false,
                            su ? UITheme.Good : bitti ? U.Soluk : UITheme.Accent,
                            new Color?[] { bitti ? U.Soluk : (Color?)null, null, su ? UITheme.Good : U.Soluk });
                }

                // Kötü hava
                y.Bolum("Kötü hava boostu (otomatik)",
                        "Kötü havada siparişler artar ama bisiklet, scooter ve motor kuryeleri dışarı çıkmaya daha az isteklidir " +
                        "(yağmurluk ve kış montu bu isteksizliği azaltır, arabalılar etkilenmez). Hava boostu onları çevrimiçi çeker. " +
                        "Saat boostuyla toplanmaz, hangisi yüksekse o geçerli olur. Müşteriden alınan kötü hava ücreti de bu masrafı kısmen karşılar.");
                y.Onay("Kötü havada otomatik boost", camp.weatherBoost, v => camp.weatherBoost = v);
                if (camp.weatherBoost)
                {
                    y.Kaydirici("Soğuk", camp.coldBoost, 0f, 0.6f, Yuzde(camp.coldBoost), v => camp.coldBoost = Yuzde5(v));
                    y.Kaydirici("Yağmur", camp.rainBoost, 0f, 0.6f, Yuzde(camp.rainBoost), v => camp.rainBoost = Yuzde5(v));
                    y.Kaydirici("Kar", camp.snowBoost, 0f, 0.6f, Yuzde(camp.snowBoost), v => camp.snowBoost = Yuzde5(v));
                    y.Kaydirici("Fırtına", camp.stormBoost, 0f, 0.6f, Yuzde(camp.stormBoost), v => camp.stormBoost = Yuzde5(v));
                }
                y.KV($"Şu an: {wx.Name}", $"sipariş ×{wx.DemandFactor:0.00} · boost {Yuzde(camp.WeatherBoostPercent)}");
                y.KV("Kurye hava bonusu / müşteri hava ücreti", $"{Ft(wx.WeatherBonus)} / {Ft(wx.CustomerSurcharge)}");

                // Hafta sonu
                y.Bolum("Hafta sonu boostları");
                y.Onay($"Sabah {camp.weekendMorningStart:00}:00–{camp.weekendMorningEnd:00}:00 (sipariş az olduğu için)", camp.weekendMorningBoost, v => camp.weekendMorningBoost = v);
                if (camp.weekendMorningBoost)
                    y.Kaydirici("Sabah", camp.weekendMorningPercent, 0.05f, 0.6f, Yuzde(camp.weekendMorningPercent), v => camp.weekendMorningPercent = Yuzde5(v));
                y.Onay($"Öğle {camp.weekendLunchStart:00}:00–{camp.weekendLunchEnd:00}:00", camp.weekendLunchBoost, v => camp.weekendLunchBoost = v);
                if (camp.weekendLunchBoost)
                    y.Kaydirici("Öğle", camp.weekendLunchPercent, 0.05f, 0.6f, Yuzde(camp.weekendLunchPercent), v => camp.weekendLunchPercent = Yuzde5(v));

                // Hafta içi
                y.Bolum("Hafta içi rastgele boostlar", "Hafta içi boost saatleri her gün yeniden belirlenir; değişiklikler ertesi gün geçerli olur.");
                y.Onay("Gün içine serpiştirilmiş boostlar", camp.weekdayRandomBoosts, v => camp.weekdayRandomBoosts = v);
                if (camp.weekdayRandomBoosts)
                {
                    y.Kaydirici("Günde kaç tane", camp.weekdayBoostCount, 0, 4, camp.weekdayBoostCount.ToString(), v => camp.weekdayBoostCount = Mathf.RoundToInt(v));
                    y.Kaydirici("En yüksek", camp.weekdayMaxPercent, 0.1f, 0.6f, Yuzde(camp.weekdayMaxPercent), v => camp.weekdayMaxPercent = Yuzde5(v));
                }

                // Hedef ödülleri: ödül, o paketlerden kazanılan kârla karşılaştırılır
                int ornek = 0;
                long toplam = 0;
                foreach (var o in OrderManager.I.History) { toplam += o.Profit; ornek++; }
                float ortKar = ornek > 0 ? toplam / (float)ornek : 300f;

                y.Bolum("Haftalık hedef ödülü");
                y.Onay("Haftalık hedef açık (her Pazartesi sıfırlanır)", camp.weeklyChallenge, v => camp.weeklyChallenge = v);
                if (camp.weeklyChallenge)
                {
                    y.Kaydirici("Hedef paket", camp.weeklyTarget, 20, 400, camp.weeklyTarget.ToString(), v => camp.weeklyTarget = Mathf.RoundToInt(v / 10f) * 10);
                    y.Kaydirici("Ödül", camp.weeklyReward, 0, 100000, Ft(camp.weeklyReward), v => camp.weeklyReward = Mathf.RoundToInt(v / 1000f) * 1000);
                    OdulKontrol(ortKar, camp.weeklyTarget, camp.weeklyReward);
                }

                y.Bolum("Aylık hedef ödülü");
                y.Onay("Aylık hedef açık (her ayın başında sıfırlanır)", camp.monthlyChallenge, v => camp.monthlyChallenge = v);
                if (camp.monthlyChallenge)
                {
                    y.Kaydirici("Hedef paket", camp.monthlyTarget, 50, 1000, camp.monthlyTarget.ToString(), v => camp.monthlyTarget = Mathf.RoundToInt(v / 10f) * 10);
                    y.Kaydirici("Ödül", camp.monthlyReward, 0, 200000, Ft(camp.monthlyReward), v => camp.monthlyReward = Mathf.RoundToInt(v / 1000f) * 1000);
                    OdulKontrol(ortKar, camp.monthlyTarget, camp.monthlyReward);
                }
                y.Bitir();
            }

            void OdulKontrol(float ortKar, int hedef, int odul)
            {
                long hedefKari = (long)(ortKar * hedef);
                y.Yazi($"Sipariş başı ~{Ft((long)ortKar)} kâr → {hedef} paket ≈ {Ft(hedefKari)} kâr · ödül {Ft(odul)}",
                       odul > hedefKari ? "kucuk uyari-renk" : "kucuk soluk");
                if (odul > hedefKari) y.Uyari("Bu ödül, o paketlerden kazandığından fazla: hedefe ulaşan her kurye zarar ettirir.");
            }
        }
    }
}
