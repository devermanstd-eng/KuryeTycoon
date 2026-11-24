using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurye kariyeri ve efsane kuryeler.
    ///
    /// Rütbe: kuryenin tecrübe seviyesi (Courier.level, teslimatla artar) bir rütbeye karşılık gelir: Çaylak → Kurye → Deneyimli →
    /// Usta → Kıdemli → Uzman → Şehrin efsanesi. Seviye hızı zaten artırıyor (Courier.Speed, seviye başı +%4); rütbeler buna ek küçük
    /// ayrıcalıklar verir: Usta kolay bırakmaz, Kıdemli daha iyi puan alır, Uzman geç yorulur. Seviye atlayınca haritada kuryenin
    /// üstünde altın "★ 5" yazısı uçar; rütbe değişince bildirim gelir.
    ///
    /// Efsane kuryeler: başvuruların çok küçük bir kısmı (şirketin 5. gününden sonra ~%1,2) "efsane"dir: lakabı olan, tek bir güçlü
    /// yeteneği olan deneyimli kurye. Aynı efsane şirkette ya da aday listesinde ikinci kez görünmez. İşe aldığın efsaneler bu
    /// bilgisayardaki koleksiyona yazılır (oyunlar arası, Ekip > Efsaneler). Otomatik testte koleksiyon yazılmaz.
    /// </summary>
    public static class KuryeKariyer
    {
        // ------------------------------------------------------------------ rütbeler

        public static readonly string[] Rutbeler = { "Çaylak", "Kurye", "Deneyimli", "Usta", "Kıdemli", "Uzman", "Şehrin efsanesi" };
        static readonly int[] RutbeSeviyesi = { 1, 2, 4, 6, 8, 10, 13 };

        public static readonly string[] RutbeAyricaliklari =
        {
            "—",
            "—",
            "—",
            "Kolay kolay bırakmaz (bırakma ihtimali −%25).",
            "Müşteriler daha yüksek puan verir (+0,1 yıldız), Usta ayrıcalığı da var.",
            "%10 daha geç yorulur, önceki ayrıcalıklar da var.",
            "Şehri avucunun içi gibi bilir: %3 hızlı, önceki ayrıcalıklar da var.",
        };

        public static int Rutbe(int seviye)
        {
            int r = 0;
            for (int i = 0; i < RutbeSeviyesi.Length; i++) if (seviye >= RutbeSeviyesi[i]) r = i;
            return r;
        }

        public static string RutbeAdi(int seviye) => Rutbeler[Rutbe(seviye)];

        /// <summary>Bir sonraki rütbe için gereken seviye (son rütbedeyse −1).</summary>
        public static int SonrakiRutbeSeviyesi(int seviye)
        {
            int r = Rutbe(seviye);
            return r + 1 < RutbeSeviyesi.Length ? RutbeSeviyesi[r + 1] : -1;
        }

        /// <summary>Courier.AddXp seviye atlatınca çağırır: haritada altın yazı, rütbe değiştiyse bildirim.</summary>
        public static void SeviyeAtladi(Courier c, int eski)
        {
            if (c == null) return;
            MapFx.Ozel(Vector2Int.RoundToInt(c.Position), $"★ {c.level}", MapFx.Altin);
            int r0 = Rutbe(eski), r1 = Rutbe(c.level);
            if (r1 > r0 && EventManager.I != null)
            {
                EventManager.I.Notify($"{c.courierName} artık {Rutbeler[r1]} (tecrübe {c.level}). {RutbeAyricaliklari[r1]}");
                GameSounds.Play(Sfx.Iyi);
            }
        }

        // ------------------------------------------------------------------ efsaneler

        public static readonly string[] EfsaneLakaplari = { "Night Owl", "Clockwork", "Rainmaker", "Local Hero", "Iron Legs", "Old Hand" };

        public static readonly string[] EfsaneAciklamalari =
        {
            "Akşam 20'den sabah 6'ya kadar %20 hızlı. Gece siparişleri onu bekler.",
            "Her zaman %12 hızlı ve dakik: müşteriler +0,2 yıldız verir.",
            "Yağmurda, karda ve soğukta %18 hızlı: kötü hava onu yavaşlatmaz, tersine hızlandırır.",
            "Herkes onu tanır: müşteriler +0,5 yıldız verir, morali hep yüksek.",
            "Neredeyse yorulmaz: yorgunluk yarıya iner.",
            "Şirketin ilk günlerinden beri buradaymış gibi: neredeyse hiç bırakmaz, her teslimat iki kat tecrübe verir.",
        };

        public const int EfsaneSayisi = 6;
        const float EfsaneIhtimali = 0.012f;
        const string KoleksiyonAnahtari = "KuryeTycoon.EfsaneKoleksiyonu";

        public static bool EfsaneMi(Courier c) => c != null && c.efsane >= 0 && c.efsane < EfsaneSayisi;
        static bool Var(Courier c, int e) => c != null && c.efsane == e;

        /// <summary>Yeni başvuru için efsane zarı: −1 sıradan kurye. Aynı efsane şirkette ya da adaylarda varsa gelmez.</summary>
        public static int EfsaneZari()
        {
            var clk = GameClock.I;
            if (clk == null || clk.CompanyDay < 5 || Random.value >= EfsaneIhtimali) return -1;
            var bos = new System.Collections.Generic.List<int>();
            for (int e = 0; e < EfsaneSayisi; e++) if (!SirkettenBiri(e)) bos.Add(e);
            return bos.Count == 0 ? -1 : bos[Random.Range(0, bos.Count)];
        }

        static bool SirkettenBiri(int e)
        {
            var cm = CourierManager.I;
            if (cm == null) return false;
            foreach (var c in cm.Couriers) if (c.efsane == e) return true;
            foreach (var a in cm.Applications) if (a.efsane == e) return true;
            if (KayitSistemi.I != null && KayitSistemi.I.AdayVarMi(e)) return true;
            return false;
        }

        /// <summary>Adayı efsaneye çevirir: lakap, deneyim ve biraz daha iyi beceri. Bildirim gönderir.</summary>
        public static void EfsaneYap(CourierCandidate a, int e)
        {
            if (a == null || e < 0 || e >= EfsaneSayisi) return;
            a.efsane = e;
            a.name = $"{a.name} \"{EfsaneLakaplari[e]}\"";
            a.level = Mathf.Max(a.level, Random.value < 0.5f ? 4 : 5);
            a.speedSkill = Mathf.Max(a.speedSkill, 1.1f);
            a.care = Mathf.Max(a.care, 0.85f);
            a.politeness = Mathf.Max(a.politeness, 0.85f);
            a.pickiness = Mathf.Min(a.pickiness, 0.4f);
            a.note = $"Efsane kurye: {EfsaneAciklamalari[e]}";
            if (EventManager.I != null && GameBootstrap.I != null && GameBootstrap.I.Started)
            {
                EventManager.I.Notify($"Efsane bir kurye başvurdu: {a.name}. {EfsaneAciklamalari[e]} Ekip > Kayıtlar'da, kaçırma!");
                GameSounds.Play(Sfx.Iyi);
            }
        }

        /// <summary>Efsane işe alındı: koleksiyona yazılır (bu bilgisayarda, oyunlar arası).</summary>
        public static void IseAlindi(Courier c)
        {
            if (!EfsaneMi(c) || TestModu.Aktif) return;
            int mask = PlayerPrefs.GetInt(KoleksiyonAnahtari, 0);
            if ((mask & (1 << c.efsane)) != 0) return;
            PlayerPrefs.SetInt(KoleksiyonAnahtari, mask | (1 << c.efsane));
            PlayerPrefs.Save();
            if (EventManager.I != null)
                EventManager.I.Push(new GameEvent($"Efsane koleksiyonu: {EfsaneLakaplari[c.efsane]}",
                    $"{c.courierName} ekibine katıldı. {EfsaneAciklamalari[c.efsane]}\n\nKoleksiyonda {KoleksiyonSayisi}/{EfsaneSayisi} efsane var " +
                    "(Ekip > Efsaneler). Koleksiyon oyunlar arası kalır."));
        }

        public static bool Koleksiyonda(int e) => (PlayerPrefs.GetInt(KoleksiyonAnahtari, 0) & (1 << e)) != 0;

        public static int KoleksiyonSayisi
        {
            get { int n = 0; for (int e = 0; e < EfsaneSayisi; e++) if (Koleksiyonda(e)) n++; return n; }
        }

        /// <summary>Teslimat başına tecrübe (Eski Toprak iki kat).</summary>
        public static float TecrubeCarpani(Courier c) => Var(c, 5) ? 2f : 1f;

        // ------------------------------------------------------------------ etkiler (KuryeOzellik bunları kendi çarpanlarına katar)

        public static float HizCarpani(Courier c)
        {
            if (c == null) return 1f;
            float m = Rutbe(c.level) >= 6 ? 1.03f : 1f;
            if (c.efsane < 0) return m;
            int h = GameClock.I != null ? GameClock.I.Hour : 12;
            if (Var(c, 0) && (h >= 20 || h < 6)) m *= 1.20f;
            if (Var(c, 1)) m *= 1.12f;
            var w = WeatherSystem.I;
            if (Var(c, 2) && w != null && (w.IsRainy || w.IsCold)) m *= 1.18f;
            return m;
        }

        public static float PuanEki(Courier c)
        {
            if (c == null) return 0f;
            float p = Rutbe(c.level) >= 4 ? 0.1f : 0f;
            if (Var(c, 1)) p += 0.2f;
            if (Var(c, 3)) p += 0.5f;
            return p;
        }

        public static float YorgunlukCarpani(Courier c)
        {
            if (c == null) return 1f;
            float m = Rutbe(c.level) >= 5 ? 0.9f : 1f;
            if (Var(c, 4)) m *= 0.5f;
            return m;
        }

        public static float BirakmaCarpani(Courier c)
        {
            if (c == null) return 1f;
            float m = Rutbe(c.level) >= 3 ? 0.75f : 1f;
            if (Var(c, 5)) m *= 0.1f;
            return m;
        }

        public static float MoralEki(Courier c) => Var(c, 3) ? 0.05f : 0f;
    }
}
