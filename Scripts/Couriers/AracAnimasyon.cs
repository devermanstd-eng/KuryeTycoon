using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Arabalı kuryenin aracı (Blender: VW Polo 6N2) canlı: tekerler gidişe göre döner, kurye dükkanda ya da müşteride durunca
    /// sürücü kapısı açılıp kapanır, paket arka koltuktan (arka kapı) ya da ağır / market siparişinde bagajdan alınır;
    /// yağmurda, fırtınada ve karda silecekler çalışır; hava kararınca (ya da kötü havada) farlar yanar, önüne ışık saçar,
    /// stoplar kırmızı parlar.
    /// Parçalar modelde ayrı objeler (Kapi_OnSol, Bagaj, Teker_ArkaSag, Silecek_Arka, Far_Sol …); pivotları menteşede.
    /// Eksenler tekerlerin yerinden bulunur: model nasıl içe aktarılırsa aktarılsın doğru yöne döner.
    /// </summary>
    public class AracAnimasyon : MonoBehaviour
    {
        public Courier kurye;

        class Parca
        {
            public Transform t;
            public Quaternion ilk;
            public Vector3 eksen;      // ebeveyninin yerel uzayında dönme ekseni
            public float isaret = 1f;  // açılma yönü
            public float aci;          // şu anki açı (derece)
        }

        Parca[] tekerler;
        Parca kapiOnSol, kapiArkaSol, kapiOnSag, kapiArkaSag, bagaj, silSol, silSag, silArka;
        Transform farSol, farSag;
        Material[] farMalzemeleri = new Material[0], stopMalzemeleri = new Material[0];
        Light farIsigi;
        Vector3 ileriY, yukariY;        // kök modelin yerel uzayında ileri ve yukarı (ışık için)
        float tekerR = 0.05f, tekerAci;
        Vector3 oncekiKonum;
        bool hazir;

        // kapı / bagaj sırası
        float sira = -1f;
        bool parkta, kargoBagajda;
        // silecekler
        float silT, silArkaT;
        float silOnAci, silArkaAci;

        static Transform Bul(Transform kok, string ad)
        {
            // Blender aynı adlı ikinci modelde ".001" ekler: "Kapi_OnSol.001" de kabul
            if (kok.name == ad || kok.name.StartsWith(ad + ".")) return kok;
            foreach (Transform c in kok)
            {
                var r = Bul(c, ad);
                if (r != null) return r;
            }
            return null;
        }

        Parca Hazirla(string ad, Vector3 eksenDunya, System.Func<Vector3, float> yonTesti)
        {
            var t = Bul(transform, ad);
            if (t == null) return null;
            var p = new Parca { t = t, ilk = t.localRotation };
            var ebeveyn = t.parent != null ? t.parent : transform;
            p.eksen = ebeveyn.InverseTransformDirection(eksenDunya).normalized;
            p.isaret = yonTesti != null ? Mathf.Sign(yonTesti(eksenDunya)) : 1f;
            if (p.isaret == 0f) p.isaret = 1f;
            return p;
        }

        void Kur()
        {
            var onSol = Bul(transform, "Teker_OnSol");
            var onSag = Bul(transform, "Teker_OnSag");
            var arkaSol = Bul(transform, "Teker_ArkaSol");
            var arkaSag = Bul(transform, "Teker_ArkaSag");
            if (onSol == null || onSag == null || arkaSol == null || arkaSag == null) { enabled = false; return; }
            Vector3 orta = (onSol.position + onSag.position + arkaSol.position + arkaSag.position) * 0.25f;
            Vector3 ileri = ((onSol.position + onSag.position) - (arkaSol.position + arkaSag.position)).normalized;
            Vector3 sol = (onSol.position - onSag.position).normalized;
            Vector3 yukari = Vector3.Cross(ileri, sol).normalized;
            var bagajT = Bul(transform, "Bagaj");
            Vector3 ust = bagajT != null ? bagajT.position : transform.position + new Vector3(0, 0, -1f);
            if (Vector3.Dot(yukari, ust - orta) < 0f) yukari = -yukari;
            tekerR = Vector3.Distance(onSol.position, arkaSol.position) * (0.29f / 2.44f);
            ileriY = transform.InverseTransformDirection(ileri);
            yukariY = transform.InverseTransformDirection(yukari);

            // tekerler: ileri giderken üstü öne doğru döner
            float tekerIsaret = Mathf.Sign(Vector3.Dot(Quaternion.AngleAxis(10f, sol) * yukari, ileri));
            tekerler = new Parca[4];
            int i = 0;
            foreach (var t in new[] { onSol, onSag, arkaSol, arkaSag })
            {
                var ebeveyn = t.parent != null ? t.parent : transform;
                tekerler[i++] = new Parca { t = t, ilk = t.localRotation, eksen = ebeveyn.InverseTransformDirection(sol).normalized, isaret = tekerIsaret };
            }
            // kapılar: arka kenarı dışa doğru
            kapiOnSol = Hazirla("Kapi_OnSol", yukari, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * -ileri, sol));
            kapiArkaSol = Hazirla("Kapi_ArkaSol", yukari, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * -ileri, sol));
            kapiOnSag = Hazirla("Kapi_OnSag", yukari, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * -ileri, -sol));
            kapiArkaSag = Hazirla("Kapi_ArkaSag", yukari, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * -ileri, -sol));
            // bagaj: alt kenarı geriye ve yukarı
            bagaj = Hazirla("Bagaj", sol, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * -yukari, -ileri));
            // arka silecek bagaj kapağının üstünde: bagaj açılınca onunla kalksın (modelde gövdeye bağlı gelir)
            var silArkaTr = Bul(transform, "Silecek_Arka");
            if (silArkaTr != null && bagajT != null && silArkaTr.parent != bagajT) silArkaTr.SetParent(bagajT, true);
            // silecekler: ön cam ve bagaj camı düzleminde, uçları yukarı doğru
            Vector3 onCam = (ileri * 0.604f + yukari * 0.797f).normalized;
            Vector3 arkaCam = (-ileri * 0.819f + yukari * 0.574f).normalized;
            silSol = Hazirla("Silecek_Sol", onCam, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * sol, yukari));
            silSag = Hazirla("Silecek_Sag", onCam, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * sol, yukari));
            silArka = Hazirla("Silecek_Arka", arkaCam, e => Vector3.Dot(Quaternion.AngleAxis(10f, e) * -sol, yukari));

            // farlar ve stoplar: malzeme kopyaları (her araç kendi ışığını yakar)
            farSol = Bul(transform, "Far_Sol");
            farSag = Bul(transform, "Far_Sag");
            var farlar = new System.Collections.Generic.List<Material>();
            var stoplar = new System.Collections.Generic.List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (m.name.StartsWith("AracFar_") || m.name.StartsWith("AracFar ")) farlar.Add(m);
                    else if (m.name.StartsWith("AracStop_")) stoplar.Add(m);
                }
            }
            farMalzemeleri = farlar.ToArray();
            stopMalzemeleri = stoplar.ToArray();
            if (farSol != null && farSag != null)
            {
                var go = new GameObject("Far ışığı");
                go.transform.SetParent(transform, false);
                farIsigi = go.AddComponent<Light>();
                farIsigi.type = LightType.Spot;
                farIsigi.color = new Color(1f, 0.95f, 0.82f);
                farIsigi.spotAngle = 80f;
                farIsigi.innerSpotAngle = 35f;
                farIsigi.shadows = LightShadows.None;
                farIsigi.renderMode = LightRenderMode.ForcePixel;
                farIsigi.intensity = 0f;
                farIsigi.enabled = false;
            }
            oncekiKonum = transform.position;
            hazir = true;
        }

        static float Darbe(float t, float a0, float a1, float b0, float b1)
        {
            if (t < a0 || t > b1) return 0f;
            if (t < a1) return Mathf.SmoothStep(0f, 1f, (t - a0) / (a1 - a0));
            if (t < b0) return 1f;
            return Mathf.SmoothStep(1f, 0f, (t - b0) / (b1 - b0));
        }

        static void Dondur(Parca p, float aci)
        {
            if (p == null || p.t == null) return;
            p.aci = aci;
            p.t.localRotation = Quaternion.AngleAxis(aci * p.isaret, p.eksen) * p.ilk;
        }

        void LateUpdate()
        {
            if (!hazir) { Kur(); if (!hazir) return; }
            var saat = GameClock.I;
            bool durdu = saat == null || saat.Paused;
            float hiz = saat != null ? saat.SpeedMultiplier : 1f;
            float dt = durdu ? 0f : Time.unscaledDeltaTime;
            float animHiz = Mathf.Clamp(hiz, 1f, 4f);

            // ---- tekerler: alınan yola göre
            Vector3 p = transform.position;
            Vector3 d = p - oncekiKonum;
            oncekiKonum = p;
            if (d.sqrMagnitude > 1e-10f && d.sqrMagnitude < 4f)
            {
                float yon = Mathf.Sign(Vector3.Dot(d, transform.TransformDirection(ileriY)));
                tekerAci = Mathf.Repeat(tekerAci + yon * d.magnitude / Mathf.Max(0.001f, tekerR) * Mathf.Rad2Deg, 360f);
                foreach (var t in tekerler) Dondur(t, tekerAci);
            }

            // ---- kapılar ve bagaj: kurye dükkanda ya da müşteride durunca
            var durum = kurye != null ? kurye.State : CourierState.Bosta;
            bool park = durum == CourierState.Bekliyor || durum == CourierState.KataCikiyor || durum == CourierState.AdresSorunu;
            if (park && !parkta && hiz < 16f)
            {
                sira = 0f;
                var o = kurye != null ? kurye.AktifSiparis : null;
                kargoBagajda = o != null && (o.IsHeavy || (o.shop != null && o.shop.def != null && o.shop.def.isMarket));
            }
            parkta = park;
            if (sira >= 0f)
            {
                sira += dt * animHiz;
                Dondur(kapiOnSol, 68f * Darbe(sira, 0f, 0.35f, 1.4f, 1.75f));
                if (kargoBagajda) Dondur(bagaj, 72f * Darbe(sira, 0.6f, 1.15f, 2.8f, 3.35f));
                else Dondur(kapiArkaSol, 66f * Darbe(sira, 0.5f, 0.85f, 1.9f, 2.25f));
                if (sira > 3.5f)
                {
                    sira = -1f;
                    Dondur(kapiOnSol, 0f); Dondur(kapiArkaSol, 0f); Dondur(bagaj, 0f);
                }
            }

            // ---- silecekler: yağmur, fırtına, kar
            var hava = WeatherSystem.I != null ? WeatherSystem.I.Current : WeatherKind.Gunesli;
            bool yagis = hava == WeatherKind.Yagmurlu || hava == WeatherKind.Firtina || hava == WeatherKind.Karli;
            float periyot = hava == WeatherKind.Firtina ? 0.8f : 1.3f;
            if (yagis || silOnAci > 0.5f)
            {
                silT += dt * Mathf.Min(animHiz, 2f);
                float hedef = 82f * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * silT / periyot));
                if (!yagis && hedef > silOnAci) hedef = silOnAci;            // yağış bitti: süpürüşü bitirip yatar
                silOnAci = yagis ? hedef : Mathf.MoveTowards(silOnAci, 0f, dt * 140f);
                Dondur(silSol, silOnAci);
                Dondur(silSag, silOnAci);
            }
            else silT = 0f;
            if (yagis || silArkaAci > 0.5f)
            {
                silArkaT += dt * Mathf.Min(animHiz, 2f);
                float faz = Mathf.Repeat(silArkaT, 5f);                       // arka silecek aralıklı: 5 sn'de bir süpürür
                float hedef = faz < 1.4f ? 110f * Mathf.Sin(Mathf.PI * faz / 1.4f) : 0f;
                silArkaAci = yagis ? hedef : Mathf.MoveTowards(silArkaAci, 0f, dt * 140f);
                if (bagaj == null || bagaj.aci < 1f) Dondur(silArka, silArkaAci);
            }
            else silArkaT = 0f;

            // ---- farlar: gece ya da kötü havada yanar, gece önüne ışık düşer
            float gece = DayNight.Night01;
            bool kotuHava = hava == WeatherKind.Firtina || hava == WeatherKind.Yagmurlu || hava == WeatherKind.Karli;
            bool acik = gece > 0.3f || kotuHava;
            float parlak = acik ? Mathf.Lerp(0.6f, 2.2f, gece) : 0f;
            foreach (var m in farMalzemeleri) Isik(m, new Color(1f, 0.96f, 0.85f), parlak);
            foreach (var m in stopMalzemeleri) Isik(m, new Color(1f, 0.08f, 0.06f), acik ? Mathf.Lerp(0.35f, 1.3f, gece) : 0f);
            if (farIsigi != null)
            {
                bool yansin = acik && gece > 0.2f;
                farIsigi.enabled = yansin;
                if (yansin && farSol != null && farSag != null)
                {
                    Vector3 ileriW = transform.TransformDirection(ileriY).normalized;
                    Vector3 yukariW = transform.TransformDirection(yukariY).normalized;
                    float boy = tekerR / (0.29f / 3.75f);                          // aracın boyu (dünya)
                    farIsigi.transform.position = (farSol.position + farSag.position) * 0.5f + ileriW * boy * 0.03f;
                    farIsigi.transform.rotation = Quaternion.LookRotation(ileriW - yukariW * 0.32f, yukariW);
                    farIsigi.range = boy * 2.2f;
                    farIsigi.intensity = 2.6f * gece;
                }
            }
        }

        static void Isik(Material m, Color c, float g)
        {
            if (m == null) return;
            if (g <= 0.001f)
            {
                if (m.IsKeywordEnabled("_EMISSION")) { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }
                return;
            }
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", c * g);
        }
    }

    /// <summary>VW Polo 6N2 (1999–2001) fabrika renkleri: boya kodu, ad, yaklaşık renk. Kurye arabaları bunlardan alır.</summary>
    public static class AracRenkleri
    {
        public static readonly (string kod, string ad, string hex)[] Polo6N2 =
        {
            ("LA7W", "Reflex Silber", "#c3c7cb"), ("LB9A", "Candy Weiss", "#f1f1ec"), ("L041", "Schwarz", "#141416"),
            ("LC9Z", "Black Magic Perleffekt", "#1b1b20"), ("LP3G", "Flash Rot", "#c3161d"), ("LC3X", "Murano Rot", "#7f1c27"),
            ("LC3W", "Rosewood Rot", "#5e2a2c"), ("LW5Z", "Jazz Blau Perleffekt", "#1e3c86"), ("LB5N", "Indigo Blau Perleffekt", "#232c58"),
            ("LA5M", "Mercato Blau", "#2d4a78"), ("LA5F", "Summer Blau", "#6e9dcc"), ("LA5C", "Surf Blau", "#3b78b2"),
            ("LC7V", "Blue Anthracite Perleffekt", "#2a3038"), ("LS7N", "Satin Grau", "#7c8086"), ("LC6M", "Brillant Grün Perleffekt", "#1f5a3a"),
            ("LC6X", "Oceanic Grün Perleffekt", "#2e5b5a"), ("LA6C", "Ever Grün", "#1e3b2b"), ("LA6S", "Science Grün", "#8dbb3c"),
        };

        const string ModKey = "KuryeTycoon.AracRenkModu", RenkKey = "KuryeTycoon.AracRengi";
        /// <summary>0 = karışık (her kuryenin aracı kendi fabrika renginde), 1 = şirket rengi, 2 = hepsi seçilen fabrika renginde.</summary>
        public static int Mod { get => PlayerPrefs.GetInt(ModKey, 0); set { PlayerPrefs.SetInt(ModKey, value); PlayerPrefs.Save(); } }
        public static int Secili { get => Mathf.Clamp(PlayerPrefs.GetInt(RenkKey, 0), 0, Polo6N2.Length - 1); set { PlayerPrefs.SetInt(RenkKey, ((value % Polo6N2.Length) + Polo6N2.Length) % Polo6N2.Length); PlayerPrefs.Save(); } }

        public static Color Renk(int i) => ColorUtility.TryParseHtmlString(Polo6N2[i].hex, out var c) ? c : Color.gray;

        /// <summary>Bu kuryenin aracının rengi (kuryenin adına göre sabit: her açılışta aynı).</summary>
        public static Color Icin(string kuryeAdi)
        {
            if (Mod == 1) return CompanyProfile.Primary;
            if (Mod == 2) return Renk(Secili);
            int h = 0;
            foreach (char ch in kuryeAdi ?? "") h = h * 31 + ch;
            return Renk(Mathf.Abs(h) % Polo6N2.Length);
        }
    }
}
