using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kayıtlar penceresi (eski İşe Alım): kayıt hunisi, kayıt açık/kapalı, kontrol sıkılığı, kampanyalar,
    /// hesap kuralları ve süreçteki adaylar. Oyuncu kimseyi tek tek işe almaz; kuralları belirler.
    /// </summary>
    public partial class GameUI
    {
        static readonly Color[] AsamaRenk =
        {
            new Color(0.55f, 0.65f, 0.85f), new Color(0.45f, 0.70f, 0.90f), new Color(0.95f, 0.70f, 0.30f),
            new Color(0.60f, 0.80f, 0.45f), new Color(0.40f, 0.85f, 0.55f)
        };

        void DrawKayit()
        {
            var ks = KayitSistemi.I;
            var cm = CourierManager.I;
            var up = UpgradeManager.I;
            if (ks == null) { DrawHiring(); return; }
            float now = GameClock.I.TotalMinutes;

            if (up.CourierCapFull)
                GUILayout.Label($"Kurye hesapları dolu ({cm.Couriers.Count}/{up.CourierCap}): süreci bitiren {ks.HesapBekleyen} kişi hesap bekliyor. {up.CourierCapHint}", warn);
            else
                KV("Açık kurye hesapları", $"{cm.Couriers.Count}/{up.CourierCap}");
            Intro("kayit", "Kuryeler uygulamaya kendileri kaydolur; sen kimseyi tek tek seçmezsin, kuralları belirlersin.",
                  "Kayıt olanlar sırayla formu doldurur, belgelerini yükler, kontrol edilir, eğitimi izler ve ekipmanını alır; " +
                  "sonra hesabı açılır. Her aşamada bir kısmı vazgeçer. Hesabı açılan kurye ilk haftasında yeterince iş bulamazsa bırakabilir. " +
                  "Kuryeler paket ve km başı kazanır, maaş yoktur. Çok kurye = kişi başı az paket = düşük moral: gerekirse kaydı kapat.");

            // ---------- Huni ----------
            Section("Kayıt süreci", "Her aşamada şu an kaç kişi var; parantez içinde bu hafta o aşamada vazgeçenler.");
            int toplam = ks.Adaylar.Count;
            for (int i = 0; i < 5; i++)
            {
                var s = (KayitAsamasi)i;
                int n = ks.AsamadakiSayi(s);
                int b = ks.haftaBirakan[i];
                string ek = s == KayitAsamasi.Ekipman && ks.HesapBekleyen > 0 ? $" · {ks.HesapBekleyen} hesap bekliyor" : "";
                Bar($"{i + 1}. {KayitSistemi.AsamaAdi(s)}", toplam > 0 ? n / (float)Mathf.Max(4, toplam) : 0f, AsamaRenk[i],
                    $"{n}" + (b > 0 ? $" (−{b})" : "") + ek);
            }
            GUILayout.Label($"Bu hafta: {ks.haftaKayit} kayıt · {ks.haftaAktif} hesap açıldı · {ks.haftaElenen} kontrolde elendi · " +
                            $"{ks.haftaIlkHafta} ilk haftada bıraktı · {ks.haftaAski} askı", small);
            if (ks.gecenHaftaKayit + ks.gecenHaftaAktif > 0)
            {
                int gb = 0; foreach (var x in ks.gecenHaftaBirakan) gb += x;
                GUILayout.Label($"Geçen hafta: {ks.gecenHaftaKayit} kayıt · {ks.gecenHaftaAktif} hesap açıldı · {gb} vazgeçti · " +
                                $"{ks.gecenHaftaElenen} elendi · {ks.gecenHaftaIlkHafta} ilk haftada bıraktı", small);
            }

            // ---------- Kayıt açık / kapalı ----------
            Section("Kayıt durumu", "Kapalı araç türünde yeni kayıtlar bekleme listesine yazılır. Açınca bekleyenler saatte bir kişi sürece girer.");
            GUILayout.BeginHorizontal();
            for (int v = 0; v < 4; v++)
            {
                if (v == 2) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                int vi = v;
                string ad = GameData.Vehicle((VehicleKind)v).name;
                string t = ks.acik[v] ? $"{ad}: açık" : $"{ad}: kapalı" + (ks.bekleme[v] > 0 ? $" ({ks.bekleme[v]} bekliyor)" : "");
                if (SelectButton(ks.acik[v], t, smallButton, GUILayout.Width(panelWidth / 2f - 40f))) deferred = () => ks.acik[vi] = !ks.acik[vi];
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Tahmini kayıt hızı: günde ~{24f * 60f / 140f * ks.KayitCarpani:0.#} kişi (itibar, bonus çarpanı ve kampanyalara göre).", small);

            // ---------- Kontrol ----------
            Section("Kontrol sıkılığı", "Belgeler, ehliyet ve sigorta (motorlu araç), çalışma izni kontrol edilir.");
            GUILayout.BeginHorizontal();
            for (int k = 0; k < 3; k++)
            {
                int kk = k;
                if (SelectButton(ks.kontrol == k, KayitSistemi.KontrolAdlari[k], smallButton)) deferred = () => ks.kontrol = kk;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(ks.kontrol == 0 ? "Hızlı: kontrol birkaç saat sürer, herkes geçer (özensiz kuryeler de)."
                          : ks.kontrol == 1 ? "Normal: 0,5–1,5 gün; çok özensiz adaylar elenir."
                          : "Sıkı: 1,5–3 gün; özensiz ya da kaba adaylar elenir, uzun bekleyen bir kısmı vazgeçer.", small);

            // ---------- Kampanyalar ----------
            Section("Kayıt kampanyaları");
            GUILayout.Label($"Yeni kurye bonusu (ilk {KayitSistemi.BonusAdet} teslimata paket başı):", label);
            GUILayout.BeginHorizontal();
            for (int k = 0; k < 3; k++)
            {
                int kk = k;
                string t = k == 0 ? "Yok" : $"+{Ft(KayitSistemi.BonusTutar[k])}";
                if (SelectButton(ks.yeniBonus == k, t, smallButton)) deferred = () => ks.yeniBonus = kk;
            }
            GUILayout.EndHorizontal();
            HelpOnLast("Kayıtları artırır, süreçte ve ilk haftada vazgeçeni azaltır. Bedeli kampanya gideri olarak ödenir.");
            GUILayout.Label($"Tavsiye primi (tavsiye edilen {KayitSistemi.BonusAdet} teslimata ulaşınca, tavsiye edene):", label);
            GUILayout.BeginHorizontal();
            for (int k = 0; k < 3; k++)
            {
                int kk = k;
                string t = k == 0 ? "Yok" : Ft(KayitSistemi.PrimTutar[k]);
                if (SelectButton(ks.tavsiyePrim == k, t, smallButton)) deferred = () => ks.tavsiyePrim = kk;
            }
            GUILayout.EndHorizontal();
            HelpOnLast("Moralı iyi kuryeler arkadaşlarını getirir. Tavsiyeyle gelenler süreçte yarı yarıya daha az vazgeçer.");
            GUILayout.BeginHorizontal();
            GUI.enabled = modalFree && !ks.ReklamAktif;
            if (GUILayout.Button(ks.ReklamAktif ? $"Sosyal medya reklamı sürüyor ({ks.reklamBitis - GameClock.I.Day + 1} gün)"
                                                : $"Sosyal medya reklamı (7 gün, kayıt +%60) −{Ft(ks.ReklamBedeli)}", smallButton))
                deferred = () => ConfirmIfExpensive(ks.ReklamBedeli, "Kurye reklamı", $"7 gün sosyal medya reklamı {Ft(ks.ReklamBedeli)} karşılığında verilsin mi?", ks.ReklamVer);
            GUI.enabled = modalFree && !ks.IlanAktif;
            if (GUILayout.Button(ks.IlanAktif ? $"Üniversite ilanı sürüyor ({ks.ilanBitis - GameClock.I.Day + 1} gün)"
                                              : $"Üniversite ilanı (7 gün, öğrenci ×2) −{Ft(ks.IlanBedeli)}", smallButton))
                deferred = () => ConfirmIfExpensive(ks.IlanBedeli, "Üniversite ilanı", $"7 gün üniversite ilanı {Ft(ks.IlanBedeli)} karşılığında asılsın mı?", ks.IlanVer);
            GUI.enabled = modalFree;
            GUILayout.EndHorizontal();

            // ---------- Hesap kuralları ----------
            Section("Hesap kuralları", "Kurala uymayan kuryenin hesabı otomatik olarak 7 gün askıya alınır; 60 gün içinde 3. askıda hesap kapanır.");
            GUILayout.Label("Müşteri puanı en az (son ~30 teslimat):", label);
            GUILayout.BeginHorizontal();
            for (int k = 0; k < 4; k++)
            {
                int kk = k;
                string t = k == 0 ? "Kural yok" : $"{KayitSistemi.PuanSinir[k]:0.0}";
                if (SelectButton(ks.puanKurali == k, t, smallButton)) deferred = () => ks.puanKurali = kk;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Ayda teslim almayı iptal en fazla:", label);
            GUILayout.BeginHorizontal();
            for (int k = 0; k < 3; k++)
            {
                int kk = k;
                string t = k == 0 ? "Kural yok" : $"{KayitSistemi.IptalSinir[k] - 1}";
                if (SelectButton(ks.iptalKurali == k, t, smallButton)) deferred = () => ks.iptalKurali = kk;
            }
            GUILayout.EndHorizontal();
            int risk = 0;
            foreach (var c in cm.Couriers)
                if (!ks.Askida(c) && ks.MinPuan > 0f && c.puanAdet >= 20 && c.puanOrt < ks.MinPuan + 0.15f) risk++;
            if (risk > 0) GUILayout.Label($"{risk} kuryenin puanı sınıra yakın.", warn);

            // ---------- Süreçteki adaylar ----------
            Section($"Süreçteki adaylar ({ks.Adaylar.Count})");
            if (ks.Adaylar.Count == 0)
                GUILayout.Label(ks.BeklemeListesi > 0 ? $"Süreçte kimse yok; bekleme listesinde {ks.BeklemeListesi} kişi var (kaydı aç)."
                                                      : "Şu an süreçte kimse yok. Yeni kayıtlar zamanla gelir.", small);
            else
            {
                if (ks.Adaylar.Count >= 5) VehicleFilterBar(applicationFilter, KayitAracSayisi, ks.Adaylar.Count, f => applicationFilter = f);
                else applicationFilter = -1;
            }
            // Son aşamadakiler önce
            for (int s = 4; s >= 0; s--)
                foreach (var a in ks.Adaylar)
                    if ((int)a.asama == s && PassesFilter(applicationFilter, a.aday.vehicle)) DrawKayitAdayi(a, now);

            // Destek personeli de buradan işe alınabilir (Destek menüsü açıldıktan sonra)
            if (!TabOpen(Tab.Destek)) return;
            GUILayout.Space(10);
            DrawSupportCandidates();
        }

        int KayitAracSayisi(VehicleKind v)
        {
            int n = 0;
            foreach (var a in KayitSistemi.I.Adaylar) if (a.aday.vehicle == v) n++;
            return n;
        }

        void DrawKayitAdayi(KayitAdayi a, float now)
        {
            var cand = a.aday;
            bool open = CardOpen(cand);
            GUILayout.BeginVertical(box);
            BeginIdCardHead(cand.name, 2, null, cand.vehicle, cand.level);
            Chip(KayitSistemi.AsamaAdi(a.asama), AsamaRenk[(int)a.asama]);
            Chip(KayitSistemi.TurAdi(a.tur), NeutralTone);
            Chip(ShortLabel(Courier.OccupationLabel(cand.occupation)), NeutralTone);
            if (a.hesapBekliyor) Chip("Hesap sınırı dolu", UITheme.Warn);
            else
            {
                float h = Mathf.Max(0f, (a.asamaBitis - now) / 60f);
                Chip(h < 1f ? "az kaldı" : h < 24f ? $"~{h:0} saat" : $"~{h / 24f:0.#} gün", NeutralTone);
            }
            ChipRow(CardColumnWidth);
            EndIdCardHead();
            if (open)
            {
                GUILayout.Space(2);
                GUILayout.Label(KayitSistemi.TurAciklama(a.tur) + (a.tavsiyeEdenAd.Length > 0 ? $" Tavsiye eden: {a.tavsiyeEdenAd}." : ""), small);
                if (!string.IsNullOrEmpty(cand.note)) GUILayout.Label($"\"{cand.note}\"", note);
                SkillGrid(cand.speedSkill, cand.care, cand.politeness, cand.stamina);
                WorkHoursSection(cand.occupation, cand.scheduleSeed);   // Çalışma saatleri (WorkHoursView.cs)
                KV("Seçicilik", cand.pickiness >= 0.75f ? "çok seçici" : cand.pickiness >= 0.45f ? "normal" : "pek seçici değil");
                KV("Sigorta", cand.insured ? "kendi sigortası var" : "sigortasız");
            }
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            DetailButton(cand);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
