using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kayıtlar penceresi (UI Toolkit, Yazıcı ile; eski IMGUI DrawKayit yerine). Oyuncu kimseyi tek tek işe almaz, kuralları belirler:
    ///  • Üstte hesap sınırı ve bu haftanın kayıt sayıları kutularda; kayıt hunisi aşama çubuklarıyla (parantezde bu hafta vazgeçenler).
    ///  • Kayıt durumu (araç türüne göre açık/kapalı), kontrol sıkılığı, kayıt kampanyaları (yeni kurye bonusu, tavsiye primi,
    ///    sosyal medya reklamı, üniversite ilanı), hesap kuralları (puan ve iptal sınırı: kurala uymayanın hesabı askıya alınır).
    ///  • Süreçteki adaylar: son aşamadakiler önce, araç filtresiyle; kartı açınca tür, not, beceriler, çalışma saatleri.
    ///  • Destek menüsü açıldıysa destek başvuruları da burada (Destek penceresiyle ortak).
    /// </summary>
    public partial class GameUI
    {
        static readonly Color[] AsamaRenk =
        {
            new Color(0.55f, 0.65f, 0.85f), new Color(0.45f, 0.70f, 0.90f), new Color(0.95f, 0.70f, 0.30f),
            new Color(0.60f, 0.80f, 0.45f), new Color(0.40f, 0.85f, 0.55f)
        };

        class UiKayitlar : UiSekme
        {
            Yazici y;
            readonly HashSet<CourierCandidate> acik = new HashSet<CourierCandidate>();
            readonly List<string> aracAdlari = new List<string>();
            readonly List<string> filtreAdlari = new List<string>();
            readonly List<string> cipAd = new List<string>();
            readonly List<Color> cipRenk = new List<Color>();
            static readonly string[] BonusAdlari = new string[3];
            static readonly string[] PrimAdlari = new string[3];
            static readonly string[] PuanAdlari = new string[4];
            static readonly string[] IptalAdlari = new string[3];

            protected override void Kur() { y = new Yazici(ui, kok); }

            public override void Yenile()
            {
                var ks = KayitSistemi.I;
                var cm = CourierManager.I;
                var up = UpgradeManager.I;
                var clock = GameClock.I;
                if (cm == null || up == null || clock == null) return;
                y.Basla();
                if (ks == null)
                {
                    y.Uyari("Kayıt sistemi bulunamadı.");
                    y.Bitir();
                    return;
                }
                float now = clock.TotalMinutes;

                y.Yazi("Kuryeler uygulamaya kendileri kaydolur; sen kimseyi tek tek seçmezsin, kuralları belirlersin.");
                y.Yardim("Kayıt olanlar sırayla formu doldurur, belgelerini yükler, kontrol edilir ve ekipmanını alır; " +
                         "sonra hesabı açılır. Her aşamada bir kısmı vazgeçer. Hesabı açılan kurye ilk haftasında yeterince iş bulamazsa bırakabilir. " +
                         "Kuryeler paket ve km başı kazanır, maaş yoktur. Çok kurye = kişi başı az paket = düşük moral: gerekirse kaydı kapat.");

                y.KutularBasla();
                y.Kutu("Kurye hesapları", $"{cm.Couriers.Count}/{up.CourierCap}", up.CourierCapFull ? $"{ks.HesapBekleyen} kişi bekliyor" : "açık yer var",
                       up.CourierCapFull ? UITheme.Warn : (Color?)null, up.CourierCapFull ? up.CourierCapHint : null);
                y.Kutu("Süreçte", ks.Adaylar.Count.ToString(), ks.BeklemeListesi > 0 ? $"bekleme listesi {ks.BeklemeListesi}" : "aday");
                y.Kutu("Bu hafta kayıt", ks.haftaKayit.ToString(), $"{ks.haftaAktif} hesap açıldı");
                y.Kutu("Kayıt hızı", $"~{24f * 60f / 210f * ks.KayitCarpani:0.#}/gün", "itibar, bonus, kampanya", null,
                       "Tahmini günlük kayıt: itibar, bonus çarpanı ve kampanyalara göre.");
                y.KutularBitir();
                if (up.CourierCapFull)
                    y.Uyari($"Kurye hesapları dolu: süreci bitiren {ks.HesapBekleyen} kişi hesap bekliyor. {up.CourierCapHint}");

                Huni(ks);
                KayitDurumu(ks);
                Kontrol(ks);
                Kampanyalar(ks);
                Kurallar(ks, cm);
                Adaylar(ks, now);

                // Destek personeli de buradan işe alınabilir (Destek menüsü açıldıktan sonra)
                if (ui.TabOpen(Tab.Destek))
                {
                    y.Bosluk(6);
                    ui.UiDestekAdaylari(y);
                }
                y.Bitir();
            }

            void Huni(KayitSistemi ks)
            {
                y.Bolum("Kayıt süreci", "Her aşamada şu an kaç kişi var; parantez içinde bu hafta o aşamada vazgeçenler.");
                int toplam = ks.Adaylar.Count, sira = 0;
                for (int i = 0; i < 5; i++)
                {
                    var s = (KayitAsamasi)i;
                    if (s == KayitAsamasi.Egitim) continue;   // eğitim aşaması yok
                    sira++;
                    int n = ks.AsamadakiSayi(s);
                    int b = ks.haftaBirakan[i];
                    string ek = s == KayitAsamasi.Ekipman && ks.HesapBekleyen > 0 ? $" · {ks.HesapBekleyen} hesap bekliyor" : "";
                    y.Cubuk($"{sira}. {KayitSistemi.AsamaAdi(s)}", toplam > 0 ? n / (float)Mathf.Max(4, toplam) : 0f, AsamaRenk[i],
                            $"{n}" + (b > 0 ? $" (−{b})" : "") + ek, 112f);
                }
                y.Kucuk($"Bu hafta: {ks.haftaKayit} kayıt · {ks.haftaAktif} hesap açıldı · {ks.haftaElenen} kontrolde elendi · " +
                        $"{ks.haftaIlkHafta} ilk haftada bıraktı · {ks.haftaAski} askı");
                if (ks.gecenHaftaKayit + ks.gecenHaftaAktif > 0)
                {
                    int gb = 0;
                    foreach (var x in ks.gecenHaftaBirakan) gb += x;
                    y.Kucuk($"Geçen hafta: {ks.gecenHaftaKayit} kayıt · {ks.gecenHaftaAktif} hesap açıldı · {gb} vazgeçti · " +
                            $"{ks.gecenHaftaElenen} elendi · {ks.gecenHaftaIlkHafta} ilk haftada bıraktı");
                }
            }

            void KayitDurumu(KayitSistemi ks)
            {
                y.Bolum("Kayıt durumu (araç türüne göre)", "Kapalı araç türünde yeni kayıtlar bekleme listesine yazılır. Açınca bekleyenler saatte bir kişi sürece girer.");
                aracAdlari.Clear();
                for (int v = 0; v < 4; v++)
                {
                    string ad = GameData.Vehicle((VehicleKind)v).name;
                    aracAdlari.Add(ks.acik[v] ? $"{ad}: açık" : $"{ad}: kapalı" + (ks.bekleme[v] > 0 ? $" ({ks.bekleme[v]} bekliyor)" : ""));
                }
                y.CokluSecim(aracAdlari, v => ks.acik[v], v => ks.acik[v] = !ks.acik[v]);
                if (ks.Dolu)
                    y.Uyari($"Kurye hesapları dolu ve süreçte {ks.Adaylar.Count} kişi var: yeni kayıtlar kendiliğinden bekleme listesine yazılıyor " +
                            $"({ks.BeklemeListesi} kişi). Gelişim'den kurye hesabı sınırını artırınca sürece girerler.");
            }

            void Kontrol(KayitSistemi ks)
            {
                y.Bolum("Kontrol sıkılığı", "Belgeler, ehliyet ve sigorta (motorlu araç), çalışma izni kontrol edilir.");
                y.Secim(KayitSistemi.KontrolAdlari, ks.kontrol, v => ks.kontrol = v);
                y.Kucuk(ks.kontrol == 0 ? "Hızlı: kontrol birkaç saat sürer, herkes geçer (özensiz kuryeler de)."
                      : ks.kontrol == 1 ? "Normal: 0,5–1,5 gün; çok özensiz adaylar elenir."
                      : "Sıkı: 1,5–3 gün; özensiz ya da kaba adaylar elenir, uzun bekleyen bir kısmı vazgeçer.");
            }

            void Kampanyalar(KayitSistemi ks)
            {
                y.Bolum("Kayıt kampanyaları");
                for (int k = 0; k < 3; k++)
                {
                    BonusAdlari[k] = k == 0 ? "Yok" : $"+{Ft(KayitSistemi.BonusTutar[k])}";
                    PrimAdlari[k] = k == 0 ? "Yok" : Ft(KayitSistemi.PrimTutar[k]);
                }
                y.Yazi($"Yeni kurye bonusu (ilk {KayitSistemi.BonusAdet} teslimata paket başı)", "kucuk");
                y.Yardim("Kayıtları artırır, süreçte ve ilk haftada vazgeçeni azaltır. Bedeli kampanya gideri olarak ödenir.");
                y.Secim(BonusAdlari, ks.yeniBonus, v => ks.yeniBonus = v);
                y.Yazi($"Tavsiye primi (tavsiye edilen {KayitSistemi.BonusAdet} teslimata ulaşınca, tavsiye edene)", "kucuk");
                y.Yardim("Morali iyi kuryeler arkadaşlarını getirir. Tavsiyeyle gelenler süreçte yarı yarıya daha az vazgeçer.");
                y.Secim(PrimAdlari, ks.tavsiyePrim, v => ks.tavsiyePrim = v);
                int gun = GameClock.I.Day;
                y.SatirBasla();
                y.Dugme(ks.ReklamAktif ? $"Sosyal medya reklamı sürüyor ({ks.reklamBitis - gun + 1} gün)"
                                       : $"Sosyal medya reklamı (7 gün, kayıt +%60) −{Ft(ks.ReklamBedeli)}",
                        () => ui.ConfirmIfExpensive(ks.ReklamBedeli, "Kurye reklamı",
                            $"7 gün sosyal medya reklamı {Ft(ks.ReklamBedeli)} karşılığında verilsin mi?", ks.ReklamVer),
                        !ks.ReklamAktif, "kucuk-dg");
                y.Dugme(ks.IlanAktif ? $"Üniversite ilanı sürüyor ({ks.ilanBitis - gun + 1} gün)"
                                     : $"Üniversite ilanı (7 gün, öğrenci ×2) −{Ft(ks.IlanBedeli)}",
                        () => ui.ConfirmIfExpensive(ks.IlanBedeli, "Üniversite ilanı",
                            $"7 gün üniversite ilanı {Ft(ks.IlanBedeli)} karşılığında asılsın mı?", ks.IlanVer),
                        !ks.IlanAktif, "kucuk-dg");
                y.SatirBitir();
            }

            void Kurallar(KayitSistemi ks, CourierManager cm)
            {
                y.Bolum("Hesap kuralları", $"Kurala uymayan kuryenin hesabı otomatik olarak {KayitSistemi.AskiGun} gün askıya alınır; 60 gün içinde 3. askıda hesap kapanır.");
                for (int k = 0; k < 4; k++) PuanAdlari[k] = k == 0 ? "Kural yok" : $"{KayitSistemi.PuanSinir[k]:0.0}";
                for (int k = 0; k < 3; k++) IptalAdlari[k] = k == 0 ? "Kural yok" : $"{KayitSistemi.IptalSinir[k] - 1}";
                y.Kucuk("Müşteri puanı en az (son ~30 teslimat):");
                y.Secim(PuanAdlari, ks.puanKurali, v => ks.puanKurali = v);
                y.Kucuk("Ayda teslim almayı iptal en fazla:");
                y.Secim(IptalAdlari, ks.iptalKurali, v => ks.iptalKurali = v);
                int risk = 0, askida = 0;
                foreach (var c in cm.Couriers)
                {
                    if (ks.Askida(c)) { askida++; continue; }
                    if (ks.MinPuan > 0f && c.puanAdet >= 20 && c.puanOrt < ks.MinPuan + 0.15f) risk++;
                }
                if (risk > 0) y.Uyari($"{risk} kuryenin puanı sınıra yakın.");
                if (askida > 0) y.Kucuk($"Şu an hesabı askıda: {askida} kurye (Kuryeler penceresinde kartında yazar).");
            }

            void Adaylar(KayitSistemi ks, float now)
            {
                y.Bolum($"Süreçteki adaylar ({ks.Adaylar.Count})");
                if (ks.Adaylar.Count == 0)
                {
                    y.Kucuk(ks.BeklemeListesi > 0 ? $"Süreçte kimse yok; bekleme listesinde {ks.BeklemeListesi} kişi var (kaydı aç)."
                                                  : "Şu an süreçte kimse yok. Yeni kayıtlar zamanla gelir.");
                    return;
                }
                if (ks.Adaylar.Count >= 5)
                {
                    filtreAdlari.Clear();
                    filtreAdlari.Add($"Hepsi ({ks.Adaylar.Count})");
                    foreach (var v in FilterKinds)
                    {
                        int n = 0;
                        foreach (var a in ks.Adaylar) if (a.aday.vehicle == v) n++;
                        filtreAdlari.Add($"{GameData.Vehicle(v).name} ({n})");
                    }
                    int sec = ui.applicationFilter < 0 ? 0 : System.Array.IndexOf(FilterKinds, (VehicleKind)ui.applicationFilter) + 1;
                    y.Secim(filtreAdlari, sec, v => ui.applicationFilter = v == 0 ? -1 : (int)FilterKinds[v - 1], true);
                }
                else ui.applicationFilter = -1;

                acik.RemoveWhere(c => !ks.Adaylar.Exists(a => a.aday == c));
                // Son aşamadakiler önce
                for (int s = 4; s >= 0; s--)
                    foreach (var a in ks.Adaylar)
                        if ((int)a.asama == s && PassesFilter(ui.applicationFilter, a.aday.vehicle)) Aday(a, now);
            }

            void Aday(KayitAdayi a, float now)
            {
                var cand = a.aday;
                bool ac = acik.Contains(cand);
                y.KartBasla();
                y.AcilirBaslik(cand.name, $"{GameData.Vehicle(cand.vehicle).name}{(cand.level > 0 ? $" · ★{cand.level}" : "")}", null, ac,
                               () => { if (!acik.Remove(cand)) acik.Add(cand); });
                cipAd.Clear(); cipRenk.Clear();
                cipAd.Add(KayitSistemi.AsamaAdi(a.asama)); cipRenk.Add(AsamaRenk[(int)a.asama]);
                cipAd.Add(KayitSistemi.TurAdi(a.tur)); cipRenk.Add(NeutralTone);
                cipAd.Add(ShortLabel(Courier.OccupationLabel(cand.occupation))); cipRenk.Add(NeutralTone);
                if (a.hesapBekliyor) { cipAd.Add("Hesap sınırı dolu"); cipRenk.Add(UITheme.Warn); }
                else
                {
                    float h = Mathf.Max(0f, (a.asamaBitis - now) / 60f);
                    cipAd.Add(h < 1f ? "az kaldı" : h < 24f ? $"~{h:0} saat" : $"~{h / 24f:0.#} gün");
                    cipRenk.Add(NeutralTone);
                }
                y.Cipler(cipAd, cipRenk);
                if (ac)
                {
                    y.Kucuk(KayitSistemi.TurAciklama(a.tur) + (a.tavsiyeEdenAd.Length > 0 ? $" Tavsiye eden: {a.tavsiyeEdenAd}." : ""));
                    if (!string.IsNullOrEmpty(cand.note)) y.Not($"\"{cand.note}\"");
                    y.Tablo(new[] { "Hız", YildizYazi(SpeedStars(cand.speedSkill)), "Dikkat", YildizYazi(CareStars(cand.care)) });
                    y.Tablo(new[] { "Nezaket", YildizYazi(CareStars(cand.politeness)), "Dayanıklılık", YildizYazi(StaminaStars(cand.stamina)) });
                    if (GameClock.I != null)
                        y.Kucuk("Çalışma saatleri: " + string.Join("  ·  ", ui.WorkInfoFor(cand.occupation, cand.scheduleSeed, GameClock.I).lines));
                    y.KV("Seçicilik", cand.pickiness >= 0.75f ? "çok seçici" : cand.pickiness >= 0.45f ? "normal" : "pek seçici değil");
                    y.KV("Sigorta", cand.insured ? "kendi sigortası var" : "sigortasız");
                }
                y.KartBitir();
            }
        }
    }
}
