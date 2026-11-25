using System.Collections.Generic;
using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Yakınlaştırınca yerden yükselen 3D şehir.
    ///  • Şehir paketi kuruluysa (ör. SimplePoly City → Kurye Tycoon > Şehir Paketini Kur) paketin binaları ve ağaçları kullanılır.
    ///  • Kurulu değilse basit kutu binalar çizilir.
    /// Harita XY düzleminde; "yukarı" −Z (kameraya doğru). Binalar yola bakacak şekilde döndürülür.
    /// Paket modelleri repoya konmaz (Asset Store lisansı): Assets/Resources/SehirPaketi altına kopyalanır.
    /// </summary>
    public class City3D : MonoBehaviour
    {
        public static City3D I { get; private set; }

        public const string PackFolder = "SehirPaketi";
        const string FrontKey = "KuryeTycoon.BinaCephesi";
        const string UsePackKey = "KuryeTycoon.SehirPaketiKullan";

        /// <summary>Paketteki binaların ön cephesi farklı yöne bakıyorsa düzeltme (0/90/180/270).</summary>
        public static int FrontOffset
        {
            get { return PlayerPrefs.GetInt(FrontKey, 0); }
            set { PlayerPrefs.SetInt(FrontKey, ((value % 360) + 360) % 360); PlayerPrefs.Save(); }
        }

        public static bool UsePack
        {
            get { return PlayerPrefs.GetInt(UsePackKey, 1) == 1; }
            set { PlayerPrefs.SetInt(UsePackKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        const string UseRoadsKey = "KuryeTycoon.PaketYollari";
        public static bool UseRoads
        {
            get { return PlayerPrefs.GetInt(UseRoadsKey, 1) == 1; }
            set { PlayerPrefs.SetInt(UseRoadsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Yol parçalarının paketteki yönü bilinmediği için tür başına döndürme düzeltmesi (0: düz, 1: köşe, 2: T).</summary>
        public static int RoadOffset(int kind) => PlayerPrefs.GetInt("KuryeTycoon.YolYonu" + kind, 0);
        public static void SetRoadOffset(int kind, int deg) { PlayerPrefs.SetInt("KuryeTycoon.YolYonu" + kind, ((deg % 360) + 360) % 360); PlayerPrefs.Save(); }

        static GameObject[] packBuildings, packTrees, packRoads;
        // Blender scriptiyle üretilen kendi binalarımız (Tools/Blender/kurye_modeller.py → Kurye Tycoon > Blender Binalarını Prefab Yap)
        static GameObject[] ownHouses, ownHousesTum, ownTown, ownAll;
        static Dictionary<string, List<GameObject>> ownByKey;
        static readonly string[] OwnPrefixes = { "MustakilEv_", "SiraEv_", "OfisKulesi_", "Market_", "FastFood_", "Apartman_", "Dukkan_",
                                                            "Konut_", "Buro_", "Sanayi_", "Kamu_", "Blok_", "Simge_", "Bolge_" };
        // Her bölgeye özel binalar (Blender: Tools/Blender/bolge_modelleri.py → Bolge_<bölge>_<tür>_<n>):
        // eski şehir, çarşı, kule, evler, banliyö, kampüs, sanayi, AVM. Anahtar: bölge adı ("eski", "karma" ...).
        static Dictionary<string, GameObject[]> bolgeHavuz;
        // Sıra ev şeritleri (Tools/Blender/sehir_tasarimlari.py: s_<bölge>_m<x0><w>_<n>): bölge → (x0, genişlik) → şeritler.
        // Bir arsa 2–4 şeridin birleşimiyle kurulur: mesh'ler paylaşılır, her arsada başka cephe çıkar.
        static Dictionary<string, Dictionary<Vector2Int, GameObject[]>> modulHavuz;
        static readonly int[][] ModulBolum =
        {
            new[] { 2, 3 }, new[] { 3, 2 }, new[] { 1, 2, 2 }, new[] { 2, 1, 2 }, new[] { 2, 2, 1 }, new[] { 1, 3, 1 },
            new[] { 3, 1, 1 }, new[] { 1, 1, 3 }, new[] { 1, 2, 1, 1 }, new[] { 2, 1, 1, 1 }, new[] { 1, 1, 2, 1 }, new[] { 3, 2 }, new[] { 2, 3 },
        };
        int kiliseSayisi;                                    // bu kurulumda yerleşen kilise (şehirde en çok iki)
        readonly Dictionary<string, GameObject> bolgeSon = new Dictionary<string, GameObject>();
        /// <summary>Dükkan adı → kendi binasının ad parçası (FastFood_burger_queen_1_1 gibi).</summary>
        static readonly Dictionary<string, string> ShopModel = new Dictionary<string, string>
        {
            { "Burger Queen", "FastFood_burger_queen" }, { "McDanold's", "FastFood_mcdanolds" },
            { "Sparx Express", "Market_szpar_express" }, { "Market 24", "Market_market_24" }, { "InterSparx", "Market_interszpar" },
            // Altı dükkanlı binalar (Blender: MODEL = "dukkan")
            { "Kebab King", "Dukkan_doner_kral" }, { "Starbocks", "Dukkan_starbocks" }, { "Pizza Hat", "Dukkan_pizza_hat" },
            { "KFG", "Dukkan_kfg" }, { "Sushi Samurai", "Dukkan_sushi_samurai" }, { "Fish & Ships", "Dukkan_fish_ships" },
            { "Station Snack Bar", "Dukkan_otogar_bufe" }, { "Villa Gourmet", "Dukkan_villa_gourmet" }, { "Non-Stop Gyros", "Dukkan_nonstop_gyros" },
            { "BioSparx", "Dukkan_bioszpar" }, { "24/7 Corner Store", "Dukkan_abc_nonstop" }, { "Corner Shop", BakkalOnek },
            { "Day & Night Market", "Dukkan_ejjel_nappali" }, { "Mini Mart", "Dukkan_mini_abc" }, { "Harbour 24", "Dukkan_kikoto_nonstop" },
            { "Village Store", "Dukkan_falu_abc" },
            // Eczaneler: yalnız Bina Atölyesi'nde binası var (Dukkan_eczane_atolye_*); yoksa eskisi gibi paket binası
            { "Central Pharmacy", "Dukkan_eczane" }, { "High Street Pharmacy", "Dukkan_eczane" }, { "Harbour Night Pharmacy", "Dukkan_eczane" },
        };
        /// <summary>
        /// Mahalle bakkalı (Blender: Tools/Blender/bakkal.py → Dukkan_bakkal_1): içi modelli, çatısı kalkan (CatiKaldir) yeni
        /// tür bina. Bölge modelleri gibi 14 m arsaya göre çizildi (arsa levhası yok, sabit ölçek).
        /// </summary>
        const string BakkalOnek = "Dukkan_bakkal";
        /// <summary>Yeni modelin prefabı henüz kurulmadıysa (Blender çalıştırılmadı) kullanılacak eski bina.</summary>
        static readonly Dictionary<string, string> ShopModelYedek = new Dictionary<string, string>
        {
            { "Corner Shop", "Dukkan_sarok_kisbolt" },
        };
        /// <summary>14 m arsaya göre çizilmiş, arsa levhasız modeller: sabit ölçek, kök karenin ortasına (bkz. AddPrefab).</summary>
        static bool ArsaOlculu(string ad) => ad.StartsWith("Bolge_") || ad.StartsWith(BakkalOnek) || AtolyeAyari.AtolyeMi(ad);   // atölye binaları da 14 m arsaya göre
        public static int OwnBuildingCount { get { LoadPack(); return ownAll.Length; } }
        // Kendi park parçalarımız (Resources/SehirPaketi/Parklar): park kareleri, göl, anıt, meydan çeşmesi
        static GameObject[] parkTiles, courtTiles, courtGardens, countryExtras, kirAlanlari, kirCimenler;
        /// <summary>Kendi binalarımızın dikey çarpanı: taban kareye sığdığı için binalar basık görünüyordu.</summary>
        const float OwnHeightBoost = 1.18f;
        static GameObject[] ownOld, ownHigh, ownMixed;
        static GameObject[] ownIndustry, ownFarm, ownKamu;   // fabrika/depo/garaj · ahır/sera/yel değirmeni · kamu binaları
        List<GameObject> kamuKalan;                         // bu kurulumda henüz yerleşmemiş kamu binaları (her biri bir kez)
        static GameObject parkPond, parkMonument, plazaFountain;
        static GameObject roadStraight, roadCorner, roadT, roadCross, groundBlock, groundGrass;
        static GameObject roadStraightTrees;   // ağaçlı düz yol (Blender: Yol_lane_agacli)
        // Geniş caddeler (Blender: Yol_cadde_*, Yol_kavsak_ca/cc, Yol_t_XY, Yol_kose_XY) — yoksa normal yol parçaları
        static GameObject avenueStraight, avenueStraightTrees;
        static Dictionary<string, GameObject> avenueJunction;
        // Bitişik blok binaları (Blender: MODEL = "blok"): kat sayısı → modeller
        static Dictionary<int, List<GameObject>> blokDuz, blokKose;
        // Referans (CityRT) binaları: tek tek şehir binaları ve dükkânlar; simge yapılar bir bloğun tamamını kaplar
        static GameObject[] ownRef, ownSimge;
        Dictionary<Vector2Int, GameObject> simgeBlok;   // blok merkezi → o bloğa konan simge yapı
        public static int PackBuildingCount { get { LoadPack(); return packBuildings.Length; } }
        public static int PackTreeCount { get { LoadPack(); return packTrees.Length; } }
        public static bool PackRoadsOk { get { LoadPack(); return roadStraight != null && roadCross != null; } }

        /// <summary>
        /// Alan yeniden yüklenmeden (Enter Play Mode ayarı) yeni Play'e geçilince önbellek boşaltılır: atölye binaları
        /// oyun çalışırken kurulan şablonlardır ve Play bitince yok olur; eski listede kalırlarsa MissingReferenceException.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OyunBasiOnbellekSifirla() { packBuildings = null; }

        static void LoadPack()
        {
            if (packBuildings != null) return;
            AtolyeAyari.Yenile();
            // Bina Atölyesi modellerinin kendi kalem çizgileri: ekran geçişi konturu çizerken gizlenir (çift çizgi olmasın)
            Shader.SetGlobalFloat("_PastelCizgiGizle", AtolyeAyari.OyundaKalemCizgisi ? 0f : 1f);
            var loaded = Resources.LoadAll<GameObject>(PackFolder + "/Binalar");
            packTrees = Resources.LoadAll<GameObject>(PackFolder + "/Agaclar");
            // Bina Atölyesi modelleri dosya değil: oyun çalışırken tasarımlardan kurulan şablonlar (AtolyeUretici). Klasörde
            // eski kaydedilmiş atölye prefabı kaldıysa (yerel, depoda yok) kullanılmaz: aynı ad iki kez havuza girmesin.
            {
                var binaListe = new List<GameObject>(loaded);
                binaListe.RemoveAll(eskiAtolye => eskiAtolye == null || AtolyeAyari.AtolyeMi(eskiAtolye.name));
                if (AtolyeAyari.Binalar) binaListe.AddRange(AtolyeUretici.Binalar());
                loaded = binaListe.ToArray();
                var agacListe = new List<GameObject>(packTrees);
                agacListe.RemoveAll(eskiAtolye => eskiAtolye == null || AtolyeAyari.AtolyeMi(eskiAtolye.name));
                if (AtolyeAyari.Cevre) agacListe.AddRange(AtolyeUretici.Agaclar());
                packTrees = agacListe.ToArray();
            }
            packRoads = Resources.LoadAll<GameObject>(PackFolder + "/Yollar");

            // Kendi binalarımız paketten ayrı tutulur: genişlik süzgecine girmez, yerine göre seçilir
            var all = new List<GameObject>();
            var own = new List<GameObject>();
            var houses = new List<GameObject>();
            var town = new List<GameObject>();
            ownByKey = new Dictionary<string, List<GameObject>>();
            var industry = new List<GameObject>(); var farm = new List<GameObject>(); var kamu = new List<GameObject>();
            var refs = new List<GameObject>(); var simge = new List<GameObject>();
            blokDuz = new Dictionary<int, List<GameObject>>();
            blokKose = new Dictionary<int, List<GameObject>>();
            var bolgeTmp = new Dictionary<string, List<GameObject>>();
            var modulTmp = new Dictionary<string, Dictionary<Vector2Int, List<GameObject>>>();
            foreach (var p in loaded)
            {
                if (!IsOwn(p.name)) { all.Add(p); continue; }
                if (AtolyeAyari.AtolyeMi(p.name) && !AtolyeAyari.Binalar) continue;   // Bina Atölyesi modelleri: ayarla aç / kapa
                own.Add(p);
                if (p.name.StartsWith("Bolge_"))
                {
                    var parca = p.name.Split('_');
                    if (ModulYuvasi(p.name, out var yuva) && parca.Length >= 3)
                    {
                        if (!modulTmp.TryGetValue(parca[1], out var md)) modulTmp[parca[1]] = md = new Dictionary<Vector2Int, List<GameObject>>();
                        if (!md.TryGetValue(yuva, out var ml)) md[yuva] = ml = new List<GameObject>();
                        ml.Add(p);
                        continue;
                    }
                    if (parca.Length >= 3)
                    {
                        if (!bolgeTmp.TryGetValue(parca[1], out var bl)) bolgeTmp[parca[1]] = bl = new List<GameObject>();
                        bl.Add(p);
                    }
                    continue;
                }
                if (p.name.StartsWith("Simge_")) { simge.Add(p); continue; }
                if (p.name.StartsWith("Konut_ref") || p.name.StartsWith("Dukkan_cadde") || p.name.StartsWith("Buro_klinker")) refs.Add(p);
                if (p.name.StartsWith("Blok_"))
                {
                    // Blok_duz_5k_2 → tür "duz", 5 kat
                    var parca = p.name.Split('_');
                    if (parca.Length >= 3 && int.TryParse(parca[2].TrimEnd('k'), out int kat))
                    {
                        var d = parca[1] == "kose" ? blokKose : blokDuz;
                        if (!d.TryGetValue(kat, out var bl)) d[kat] = bl = new List<GameObject>();
                        bl.Add(p);
                    }
                    continue;
                }
                if (p.name.StartsWith("MustakilEv_") || p.name.StartsWith("Konut_villa") || p.name.StartsWith("Konut_konak") ||
                    p.name.StartsWith("Konut_sale")) houses.Add(p);
                else if (p.name.StartsWith("Apartman_") || p.name.StartsWith("SiraEv_") || p.name.StartsWith("OfisKulesi_") ||
                         p.name.StartsWith("Dukkan_genel") || p.name.StartsWith("Dukkan_cadde") || p.name.StartsWith("Konut_") || p.name.StartsWith("Buro_")) town.Add(p);
                else if (p.name.StartsWith("Sanayi_fabrika") || p.name.StartsWith("Sanayi_depo") || p.name.StartsWith("Sanayi_garaj")) industry.Add(p);
                else if (p.name.StartsWith("Sanayi_")) farm.Add(p);
                else if (p.name.StartsWith("Kamu_")) kamu.Add(p);
                foreach (var kv in ShopModel)
                    if (p.name.StartsWith(kv.Value))
                    {
                        if (!ownByKey.TryGetValue(kv.Key, out var l)) ownByKey[kv.Key] = l = new List<GameObject>();
                        l.Add(p);
                    }
            }
            // Bina Atölyesi: bir dükkânın atölye binası varsa o dükkân için yalnız atölye binaları aday
            if (AtolyeAyari.Binalar)
                foreach (var atolyeListe in ownByKey.Values)
                    if (atolyeListe.Exists(atolyeAday => AtolyeAyari.AtolyeMi(atolyeAday.name))) atolyeListe.RemoveAll(atolyeAday => !AtolyeAyari.AtolyeMi(atolyeAday.name));
            // Yeni modeli olmayan dükkân eski binasını kullanır
            foreach (var kv in ShopModelYedek)
            {
                if (ownByKey.TryGetValue(kv.Key, out var mevcut) && mevcut.Count > 0) continue;
                var yedek = new List<GameObject>();
                foreach (var p in own) if (p.name.StartsWith(kv.Value)) yedek.Add(p);
                if (yedek.Count > 0) ownByKey[kv.Key] = yedek;
            }
            if (AtolyeAyari.YalnizAtolye)
            {
                // Şehir yalnız atölye binalarıyla: atölye modeli olan havuzlardan eskiler çıkar; paket binaları ve Londra blokları
                // kullanılmaz (boş havuzlar bölge havuzuna düşer). Çiftlik gibi atölye karşılığı olmayan havuz eskisi gibi kalır.
                foreach (var liste in new[] { houses, town, industry, kamu, refs, simge })
                    if (liste.Exists(p => AtolyeAyari.AtolyeMi(p.name))) liste.RemoveAll(p => !AtolyeAyari.AtolyeMi(p.name));
                    else liste.Clear();
                foreach (var kv in bolgeTmp)
                    if (kv.Value.Exists(p => AtolyeAyari.AtolyeMi(p.name))) kv.Value.RemoveAll(p => !AtolyeAyari.AtolyeMi(p.name));
                blokDuz.Clear();
                blokKose.Clear();
                all.Clear();
            }
            ownAll = own.ToArray();
            bolgeHavuz = new Dictionary<string, GameObject[]>();
            foreach (var kv in bolgeTmp) bolgeHavuz[kv.Key] = kv.Value.ToArray();
            modulHavuz = new Dictionary<string, Dictionary<Vector2Int, GameObject[]>>();
            foreach (var kv in modulTmp)
            {
                var d = new Dictionary<Vector2Int, GameObject[]>();
                foreach (var y in kv.Value) d[y.Key] = y.Value.ToArray();
                modulHavuz[kv.Key] = d;
            }
            // Londra evleri varsa banliyöde sadece onlar (eski Avrupa evleri kullanılmaz)
            var londraEv = houses.FindAll(h => h.name.Contains("_londra_"));
            ownHouses = londraEv.Count > 0 ? londraEv.ToArray() : houses.ToArray();
            ownHousesTum = houses.ToArray();     // organik şehir: bütün ev çeşitleri (Londra, Avrupa, villa, konak, salaş)
            ownTown = town.ToArray();
            ownIndustry = industry.ToArray();
            ownFarm = farm.ToArray();
            ownKamu = kamu.ToArray();
            ownRef = refs.ToArray();
            ownSimge = simge.ToArray();
            // Bölgeye göre bina havuzları: eski şehirde sıra evler, kule bloklarda ofis kuleleri, çarşıda karışık
            var old = new List<GameObject>(); var high = new List<GameObject>(); var mixed = new List<GameObject>();
            foreach (var p in town)
            {
                bool apt = p.name.StartsWith("Apartman_"), sira = p.name.StartsWith("SiraEv_"), ofis = p.name.StartsWith("OfisKulesi_");
                bool dukkan = p.name.StartsWith("Dukkan_genel") || p.name.StartsWith("Dukkan_cadde");
                bool eskiKonut = p.name.StartsWith("Konut_sira_evler") || p.name.StartsWith("Konut_tugla_apt") || p.name.StartsWith("Konut_avlulu");
                bool yeniBlok = p.name.StartsWith("Konut_balkonlu") || p.name.StartsWith("Buro_");
                if (sira || apt || dukkan || eskiKonut || p.name.StartsWith("Buro_saat")) old.Add(p);
                if (ofis || apt || yeniBlok) high.Add(p);
                mixed.Add(p);
            }
            ownOld = old.Count > 0 ? old.ToArray() : ownTown;
            ownHigh = high.Count > 0 ? high.ToArray() : ownTown;
            ownMixed = mixed.ToArray();
            if (AtolyeAyari.YalnizAtolye)
            {
                // boşalan eski havuzların yerine bölge havuzlarındaki atölye binaları (yedek yollar boş listeden seçmesin)
                GameObject[] Birlesik(params string[] anahtarlar)
                {
                    var l = new List<GameObject>();
                    foreach (var a in anahtarlar) if (bolgeHavuz.TryGetValue(a, out var h)) l.AddRange(h);
                    return l.ToArray();
                }
                if (ownTown.Length == 0) ownTown = Birlesik("eski", "karma", "kule");
                if (ownOld.Length == 0) ownOld = Birlesik("eski");
                if (ownHigh.Length == 0) ownHigh = Birlesik("kule");
                if (ownMixed.Length == 0) ownMixed = Birlesik("karma");
                if (ownOld.Length == 0) ownOld = ownTown;
                if (ownHigh.Length == 0) ownHigh = ownTown;
                if (ownMixed.Length == 0) ownMixed = ownTown;
                if (ownHouses.Length == 0) ownHouses = Birlesik("ev", "banliyo");
                if (ownHousesTum.Length == 0) ownHousesTum = ownHouses;
                if (ownIndustry.Length == 0) ownIndustry = Birlesik("sanayi");
                if (ownRef.Length == 0) ownRef = null;
            }

            // Kendi yol parçalarımız (Yol_...) paketinkilerden önce bulunsun; kendi ağaçlarımız varsa sadece onlar
            System.Array.Sort(packRoads, (p, q) => (q.name.StartsWith("Yol_") ? 1 : 0) - (p.name.StartsWith("Yol_") ? 1 : 0));
            var ownTrees = new List<GameObject>();
            foreach (var p in packTrees)
                if (p.name.StartsWith("Agac_") || p.name.StartsWith("Cali_")) ownTrees.Add(p);
            // Bina Atölyesi ağaçları (Agac_atolye_*, Cali_atolye_*): ayar kapalıysa kullanılmaz, açıksa yalnız onlar
            if (!AtolyeAyari.Cevre) ownTrees.RemoveAll(atolyeAgac => AtolyeAyari.AtolyeMi(atolyeAgac.name));
            else if (ownTrees.Exists(atolyeAgac => AtolyeAyari.AtolyeMi(atolyeAgac.name))) ownTrees.RemoveAll(atolyeAgac => !AtolyeAyari.AtolyeMi(atolyeAgac.name));
            if (ownTrees.Count > 0) packTrees = ownTrees.ToArray();

            var parks = Resources.LoadAll<GameObject>(PackFolder + "/Parklar");
            var tiles = new List<GameObject>();
            var courts = new List<GameObject>();
            var extras = new List<GameObject>();
            var alanlar = new List<GameObject>();
            var cimenler = new List<GameObject>();
            parkPond = parkMonument = plazaFountain = null;
            foreach (var p in parks)
            {
                if (p.name.StartsWith("Park_cim")) tiles.Add(p);
                else if (p.name.StartsWith("Avlu_")) courts.Add(p);
                else if (p.name.StartsWith("Kir_alan_")) { if (!p.name.EndsWith("_Hafif")) alanlar.Add(p); }   // kır parselleri (Blender: MODEL = "kir")
                else if (p.name.StartsWith("Kir_cimen_")) { if (!p.name.EndsWith("_Hafif")) cimenler.Add(p); } // çayır parçaları
                else if (p.name.StartsWith("Kir_")) extras.Add(p);
                else if (p.name == "Park_gol") parkPond = p;
                else if (p.name == "Park_anit") parkMonument = p;
                else if (p.name == "Meydan_cesme") plazaFountain = p;
            }
            parkTiles = tiles.ToArray();
            courtTiles = courts.ToArray();
            courtGardens = courts.FindAll(c => !c.name.Contains("otopark") && !c.name.Contains("karma")).ToArray();   // arabasız avlular
            countryExtras = extras.ToArray();
            kirAlanlari = alanlar.ToArray();
            kirCimenler = cimenler.ToArray();

            // Binalar klasörüne düşmüş direk, lamba, rüzgar gülü (çok ince) ve hazır şehir blokları (çok geniş) elenir
            var widths = new List<float>();
            foreach (var p in all) widths.Add(Width(p));
            widths.Sort();
            float median = widths.Count > 0 ? widths[widths.Count / 2] : 1f;
            var keep = new List<GameObject>();
            foreach (var p in all)
            {
                float w = Width(p);
                if (w >= median * 0.45f && w <= median * 2.6f) keep.Add(p);
            }
            packBuildings = keep.ToArray();

            roadStraight = FindRoad("lane_01", "lane_02", "road lane", "road tile");
            roadStraightTrees = null;
            foreach (var p in packRoads) if (p != null && p.name == "Yol_lane_agacli") roadStraightTrees = p;
            avenueStraight = avenueStraightTrees = null;
            avenueJunction = new Dictionary<string, GameObject>();
            foreach (var p in packRoads)
            {
                if (p == null) continue;
                if (p.name == "Yol_cadde_01") avenueStraight = p;
                else if (p.name == "Yol_cadde_agacli") avenueStraightTrees = p;
                else if (p.name.StartsWith("Yol_kavsak_") || p.name.StartsWith("Yol_t_") && p.name.Length == 8 || p.name.StartsWith("Yol_kose_"))
                    avenueJunction[p.name] = p;
            }
            roadCorner = FindRoad("corner_01", "corner");
            roadT = FindRoad("t_intersection_01", "t_intersection", "t intersection");
            roadCross = FindRoadExcept("intersection", "t_", "t intersection");
            groundBlock = FindRoad("sidewalk", "concrete");
            groundGrass = FindRoad("grass");
        }

        static GameObject FindRoad(params string[] keys)
        {
            foreach (var k in keys)
                foreach (var p in packRoads)
                    if (p.name.ToLowerInvariant().Contains(k)) return p;
            return null;
        }

        static GameObject FindRoadExcept(string key, params string[] not)
        {
            foreach (var p in packRoads)
            {
                string n = p.name.ToLowerInvariant();
                if (!n.Contains(key)) continue;
                bool bad = false;
                foreach (var x in not) if (n.Contains(x)) bad = true;
                if (!bad) return p;
            }
            return null;
        }

        class Piece
        {
            public Transform t;       // tutucu: yerde durur, z ölçeği yükselme oranı
            public MeshFilter mf;     // kutu binalarda; paket modellerinde null
            public float height;      // tam boy (kare biriminde)
            public bool prefab;
            public float heightMul = 1f;  // paket modellerinde yükselme çarpanı (kendi binalarımız biraz daha uzun: referanstaki gibi dolu silüet)
            public bool flat;         // yol / zemin: yükselmez, sadece görünür olur
            public Color baseColor;
            public Vector2Int tile;
            public Shop shop;
            public bool hq;
            public int varSeed = -1;  // kendi binalarımızda renk çeşitliliği tohumu (−1: yok)
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Vector2Int, float> heightByTile = new Dictionary<Vector2Int, float>();
        Transform root;
        /// <summary>3D şehrin kökü (GeceIsiklari pencere ve lambaları buradan bulur).</summary>
        public Transform Root => root;
        /// <summary>Renkler (kilit, bina tonu) her yeniden boyandığında artar: üstüne yazılan gece ışıkları yeniden uygulansın.</summary>
        public static int TintVersion { get; private set; }
        /// <summary>Şehir kurulunca çağrılır (gece ışıkları pencere ve lambaları kaydeder).</summary>
        public static event System.Action Built;

        float nightAmt;
        /// <summary>Gece: kod kutusu binalar (AVM, otogar, merkez ofis, depo kutuları) ışıksız malzemeyle çizildiği için
        /// geceleri bembeyaz parlıyordu; renkleri geceye göre koyulaşır (Gorsel3D çağırır).</summary>
        public void SetNight(float n)
        {
            if (Mathf.Abs(n - nightAmt) < 0.03f && !(n == 0f && nightAmt > 0f)) return;
            nightAmt = n;
            var map = CityMap.I;
            if (map == null) return;
            foreach (var p in pieces)
            {
                if (p.prefab || p.mf == null) continue;
                Paint(p.mf.mesh, BoxColor(p, map));
            }
        }

        Color BoxColor(Piece p, CityMap map)
        {
            var c = p.hq ? CompanyProfile.Primary : p.baseColor;
            if (p.shop != null && !p.shop.contracted) c = Color.Lerp(c, new Color(0.45f, 0.47f, 0.52f), 0.55f);
            if (!map.IsUnlocked(p.tile)) c = CityMap.LockedTint(c);
            if (nightAmt > 0f)
            {
                var m = CityMap.NightMul;
                c = Color.Lerp(c, new Color(c.r * m.r, c.g * m.g, c.b * m.b, c.a), nightAmt * 0.85f);
            }
            return c;
        }
        Material mat;
        float lastGrow = -1f;
        int lastUnlock = -1;
        int lastContracts = -1;

        void Awake() { I = this; ClearTintCache(); }

        /// <summary>Karedeki binanın şu anki boyu (yükselme oranı dahil). Etiketler ve müşteri noktaları çatıya oturur.</summary>
        /// <summary>Binaların tam boyları (kare birimi, yükselme oranı hariç). Gece pencere ışıkları kullanır.</summary>
        public Dictionary<Vector2Int, float> BuildingHeights => heightByTile;

        public float HeightAt(Vector2Int tile)
        {
            float g = MapCamera.I != null ? MapCamera.I.Tilt01 : 0f;
            return heightByTile.TryGetValue(tile, out float h) ? h * g : 0f;
        }

        /// <summary>Ayar değişince şehri yeniden kurar.</summary>
        public void Rebuild()
        {
            packBuildings = null;
            packTrees = null;
            packRoads = null;
            if (root != null) Destroy(root.gameObject);
            pieces.Clear();
            bekleyenler.Clear();
            heightByTile.Clear();
            ClearTintCache();
            lastGrow = -1f;
            Build();
            if (Traffic.I != null) Traffic.I.Build();   // trafik de paketin araçlarını kullanır
        }

        public void Build()
        {
            var map = CityMap.I;
            if (map == null) return;
            LoadPack();
            bool pack = UsePack && packBuildings.Length > 0;
            bool own = UsePack && ownAll.Length > 0;
            bool ownPark = UsePack && parkTiles.Length > 0;
            bool packTreesOk = UsePack && packTrees.Length > 0;
            kamuKalan = ownKamu != null ? new List<GameObject>(ownKamu) : new List<GameObject>();
            kiliseSayisi = 0;
            atlikarincaSayisi = hobiSayisi = 0;
            bolgeSon.Clear();
            binaBolge = null;
            if (CityMap.Organik) Arazi.Hazirla();   // nehir yatağı ve tepeler: modeller araziye oturur
            SuKiyisi.Hazirla();                     // su gölgelendiricisine nehir yatağı derinliği (kıyı köpüğü, sığ renk)
            bool roads3D = UsePack && UseRoads && roadStraight != null && roadCross != null;

            root = new GameObject("3D Şehir").transform;
            root.SetParent(transform, false);

            // Kutu binalar için sprite malzemesi: projenin render pipeline'ına uygun, vertex rengini gösterir
            var probe = SpriteFactory.Create("malzeme", root, false, Color.white, Vector3.zero, 1f, 0);
            mat = probe.sharedMaterial;
            Destroy(probe.gameObject);

            var shopTiles = new Dictionary<Vector2Int, Shop>();
            foreach (var s in map.Shops) shopTiles[s.tile] = s;

            var rng = new System.Random(12345);
            SimgeBloklariniSec(map, shopTiles);
            for (int x = 0; x < CityMap.Width; x++)
            {
                for (int y = 0; y < CityMap.Height; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (map.IsRoad(x, y))
                    {
                        if (CityMap.Organik) continue;    // organik şehirde yollar tek parça çizgi mesh'i (OrganikSehir3D)
                        // Köprüler teknelerin üstünde çizilsin (sortingOrder 3)
                        bool bridge = CityMap.BridgeAt(x, y);
                        if (roads3D) AddRoad(map, tile, bridge ? 3 : 1);
                        if (bridge)
                        {
                            var rail = new Color(0.80f, 0.78f, 0.74f);
                            AddBoxR(tile, new Vector2(0.06f, 1.0f), 0.07f, rail, null, false, new Vector2(-0.46f, 0f), false);
                            AddBoxR(tile, new Vector2(0.06f, 1.0f), 0.07f, rail, null, false, new Vector2(0.46f, 0f), false);
                        }
                        continue;
                    }
                    // Nehir ve tren yolu 2D haritadan görünür (su, raylar); üstlerine bir şey konmaz
                    if (CityMap.WaterAt(x, y) || CityMap.RailAt(x, y)) continue;

                    var zone = CityMap.ZoneOf(tile);
                    // Organik şehirde bina bölgesi komşulara göre düzlenir: planın tek tük karelerinde apartmanın yanına
                    // müstakil ev düşmesin, mahalleler bütün olsun
                    if (CityMap.Organik) zone = BinaBolgesi(tile, zone);
                    // Kır kareleri (şehrin girintili kenarı): dokulu kır zemini görünür, ağaçları kır düzeni koyar (orman, çit)
                    if (zone == Zone.Country) continue;
                    bool frontage = IsAdjacentToRoad(map, x, y);
                    float face = FaceAngle(map, tile);
                    int floors = Addresses.Floors(tile);

                    // Paket yolları açıksa zemin: kaldırım ya da çim (bölgeye göre).
                    // Organik şehirde kare zemin plakası yok: yollar eğri, kare plakalar yolun kenarında merdiven gibi basamak
                    // ve mürekkep çizgisi yapıyordu. Zemin düz 2D renk, asfalt ve kaldırım çizgi mesh'i.
                    if (roads3D && !CityMap.Organik)
                    {
                        bool grass = zone == Zone.Park || zone == Zone.Houses || zone == Zone.River ||
                                     (!frontage && (zone == Zone.HighRise || zone == Zone.Mixed || zone == Zone.Campus));
                        var gnd = grass && groundGrass != null ? groundGrass : groundBlock;
                        // Kendi park parçalarımız ve meydan çeşmesi kendi zeminiyle gelir (üst üste binmesin)
                        bool ownGround = (zone == Zone.Park && ownPark) || (tile == CityMap.PlazaCenter && UsePack && plazaFountain != null);
                        if (gnd != null && !ownGround) AddFlat(gnd, tile, 0f);
                    }

                    // Nehir kıyısı (kıvrımlı nehrin suyu olmayan tarafı): sadece çimenli set. Ağaç konmaz: yukarıdan nehrin
                    // içinde ağaç varmış gibi görünüyordu.
                    if (zone == Zone.River) continue;

                    if (simgeBlok != null && simgeBlok.Count > 0 && !frontageRoadOnly(map, x, y))
                    {
                        var bm = CityMap.BlockCenterOf(x, y);
                        if (simgeBlok.TryGetValue(bm, out var sp))
                        {
                            if (tile == bm)
                            {
                                // Simge yapı bloğun dokuz karesini kaplar (opera, gar, pazar …), önü güneydeki yola bakar
                                AddPrefab(sp, tile, 2.95f, 180f, null, default, 0f, true, true);
                                float hh = heightByTile.TryGetValue(tile, out var h0) ? h0 : 1f;
                                for (int dx = -1; dx <= 1; dx++)
                                    for (int dy = -1; dy <= 1; dy++) heightByTile[new Vector2Int(x + dx, y + dy)] = hh;
                            }
                            continue;
                        }
                    }

                    if (CityMap.Organik && tile == CityMap.PlazaCenter && tile != map.HQTile)
                    {
                        // Organik şehirde meydan tek kare: çeşme
                        if (UsePack && plazaFountain != null) AddFlat(plazaFountain, tile, 0f, 2);
                        else
                        {
                            AddBoxR(tile, new Vector2(0.5f, 0.5f), 0.06f, new Color(0.70f, 0.68f, 0.64f), null, false, default, false);
                            AddBoxR(tile, new Vector2(0.36f, 0.36f), 0.07f, new Color(0.45f, 0.72f, 0.90f), null, false, default, false);
                        }
                        continue;
                    }

                    if (tile == map.HQTile)
                    {
                        // Merkez ofis her zaman şirket renginde kutu: haritada hemen tanınsın
                        AddBox(tile, 0.84f, 1.9f, CompanyProfile.Primary, null, true);
                        continue;
                    }

                    if (shopTiles.TryGetValue(tile, out var shop))
                    {
                        // AVM'deki dükkanlar AVM binasının içinde: ayrı bina yok
                        if (zone == Zone.Mall) continue;
                        // Dükkan: paket binası biraz küçük (etrafında dükkan renginde zemin görünür) ya da renkli kutu
                        var ownShop = own ? OwnForShop(shop.def.name, rng) : null;
                        if (ownShop != null) AddPrefab(ownShop, tile, OwnFootprint(ownShop, 0.92f), face, shop, default, 0f, true, true);
                        else if (pack) AddPrefab(Pick(packBuildings, rng, 2, true), tile, 0.66f, face, shop);
                        else AddBox(tile, 0.8f, 0.75f, shop.def.color, shop, false);
                        continue;
                    }

                    switch (zone)
                    {
                        case Zone.Plaza:
                            if (tile == CityMap.PlazaCenter && UsePack && plazaFountain != null)
                                AddFlat(plazaFountain, tile, 0f, 2);
                            else if (tile == CityMap.PlazaCenter)
                            {
                                // Meydan çeşmesi ve heykel
                                AddBoxR(tile, new Vector2(0.5f, 0.5f), 0.06f, new Color(0.70f, 0.68f, 0.64f), null, false, new Vector2(0f, 0.2f), false);
                                AddBoxR(tile, new Vector2(0.36f, 0.36f), 0.07f, new Color(0.45f, 0.72f, 0.90f), null, false, new Vector2(0f, 0.2f), false);
                                AddBoxR(tile, new Vector2(0.08f, 0.08f), 0.32f, new Color(0.62f, 0.60f, 0.56f), null, false, new Vector2(0f, 0.2f), false);
                            }
                            break;

                        case Zone.Park:
                            if (ownPark)
                            {
                                if (CityMap.IsBlockCenter(x, y))
                                {
                                    var orta = tile == CityMap.BelediyeCenter ? parkMonument : parkPond;
                                    AddFlat(orta != null ? orta : parkTiles[rng.Next(parkTiles.Length)], tile, 90f * rng.Next(4), 2);
                                }
                                else
                                {
                                    // Park karesi (oyun parkı, çiçek bahçesi, gezinti yolu) ve köşelerinde ağaçlar. Atlıkarınca ve
                                    // oyun alanı şehirde birkaç kez (her parkta tekrar etmesin); çoğu kare düz çim / çiçek / gezinti
                                    AddFlat(ParkKaresi(rng), tile, 90f * rng.Next(4), 2);
                                    int nt = 1 + rng.Next(3);
                                    for (int i = 0; i < nt; i++)
                                    {
                                        var kose = new Vector2((i % 2 == 0) ? -0.36f : 0.36f, (i < 2) ? -0.36f : 0.36f);
                                        AddTrees(tile, rng, 1, packTreesOk, kose, 0.04f);
                                    }
                                }
                            }
                            else if (CityMap.IsBlockCenter(x, y))
                            {
                                if (tile == CityMap.BelediyeCenter)   // City Park: anıt kule
                                {
                                    AddBoxR(tile, new Vector2(0.34f, 0.34f), 0.08f, new Color(0.78f, 0.76f, 0.70f), null, false, default, false);
                                    AddBoxR(tile, new Vector2(0.16f, 0.16f), 0.95f, new Color(0.86f, 0.84f, 0.78f), null, false, default, false);
                                }
                                else AddBoxR(tile, new Vector2(0.62f, 0.46f), 0.01f, new Color(0.30f, 0.58f, 0.80f), null, false, default, false); // gölet
                            }
                            else AddTrees(tile, rng, 3 + rng.Next(2), packTreesOk);
                            break;

                        case Zone.Mall:
                            // Organik şehirde 3×3 blok yok: AVM bölgesinin her karesine hipermarket / mağaza sırası
                            if (CityMap.Organik)
                            {
                                if (!BolgeBinasi(zone, tile, frontage ? face : 90f * rng.Next(4), rng) && own && ownIndustry.Length > 0)
                                    AddPrefab(ownIndustry[rng.Next(ownIndustry.Length)], tile, 0.97f, face, null, default, 0f, true, true);
                                break;
                            }
                            if (CityMap.IsBlockCenter(x, y))
                            {
                                float top;
                                if (tile == CityMap.OtogarCenter)
                                {
                                    // Otogar: alçak, geniş peron binası ve önünde otobüsler
                                    AddBoxR(tile, new Vector2(2.7f, 1.2f), 0.35f, CityMap.OtogarColor, null, false, new Vector2(0f, 0.7f), false);
                                    for (int i = 0; i < 4; i++)
                                        AddBoxR(tile, new Vector2(0.28f, 0.85f), 0.2f, new Color(0.95f, 0.78f, 0.20f), null, false, new Vector2(-0.9f + i * 0.6f, -0.5f), false);
                                    top = 0.35f;
                                }
                                else
                                {
                                    // Bloğun tamamını kaplayan büyük AVM binası + cam çatılı orta bölüm
                                    AddBoxR(tile, new Vector2(2.7f, 2.7f), 0.7f, CityMap.MallColor, null, false, default, false);
                                    AddBoxR(tile, new Vector2(1.5f, 0.8f), 0.82f, new Color(0.62f, 0.80f, 0.90f), null, false, default, false);
                                    top = 0.82f;
                                }
                                for (int dx = -1; dx <= 1; dx++)
                                    for (int dy = -1; dy <= 1; dy++) heightByTile[new Vector2Int(x + dx, y + dy)] = top;
                            }
                            break;

                        case Zone.Warehouse:
                            // Fabrikalar, depolar, garajlar (kendi modellerimiz varsa); yoksa geniş, alçak kutular
                            if (own && (CityMap.Organik || rng.NextDouble() < 0.9) && BolgeBinasi(zone, tile, frontage ? face : 90f * rng.Next(4), rng)) break;
                            if (own && ownIndustry.Length > 0)
                            {
                                var sb = ownIndustry[rng.Next(ownIndustry.Length)];
                                AddPrefab(sb, tile, OwnFootprint(sb, 0.97f), frontage ? face : 90f * rng.Next(4), null, default, 0f, true, true);
                                break;
                            }
                            AddBox(tile, frontage ? 0.9f : 0.82f, 0.28f + (float)rng.NextDouble() * 0.12f, CityMap.RoofColor(zone, x, y), null, false);
                            break;

                        case Zone.Houses:
                            if (frontage)
                            {
                                // Müstakil ev: yola yakın küçük bina, arkasında bahçe ağacı
                                var front = FrontDir(map, tile) * 0.14f;
                                // Bölgeye özel evler (kocka ház, aile evi, bungalov, modern, ikiz) çoğunlukta; Londra/Avrupa evleri karışır
                                if (own && (CityMap.Organik || rng.NextDouble() < 0.9) && BolgeBinasi(zone, tile, face, rng)) break;
                                if (own && ownHouses.Length > 0)
                                {
                                    // Kendi evlerimizin bahçesi ve ağaçları modelin içinde
                                    // Organik şehirde ev mahalleleri çok geniş: tek tip Londra evi yerine bütün çeşitler
                                    var evler = CityMap.Organik && ownHousesTum != null && ownHousesTum.Length > 0 ? ownHousesTum : ownHouses;
                                    var h = evler[rng.Next(evler.Length)];
                                    AddPrefab(h, tile, OwnFootprint(h, 0.97f), face, null, default, 0f, true, true);
                                    break;
                                }
                                if (pack) AddPrefab(Pick(packBuildings, rng, floors, false), tile, 0.55f, face, null, front);
                                else AddBox(tile, 0.55f, 0.18f + floors * 0.12f, CityMap.RoofColor(zone, x, y), null, false, front);
                                AddTrees(tile, rng, 1, packTreesOk, -front * 2.2f, 0.12f);
                            }
                            else if (CityMap.Organik && own && rng.NextDouble() < (AtolyeAyari.YalnizAtolye ? 0.92 : 0.7) && BolgeBinasi(zone, tile, face, rng)) { }
                            else if (CityMap.Organik && BolgeHavuzu("ev") == null && own && ownHousesTum != null && ownHousesTum.Length > 0 && rng.NextDouble() < 0.15)
                            {
                                // Organik şehirde bloklar düzensiz: yola değmeyen arsada da arka sıra ev (boş gri arsa kalmasın)
                                var h = ownHousesTum[rng.Next(ownHousesTum.Length)];
                                AddPrefab(h, tile, OwnFootprint(h, 0.97f), face, null, default, 0f, true, true);
                            }
                            else AddTrees(tile, rng, 2 + rng.Next(2), packTreesOk);
                            break;

                        default: // eski şehir, kule bloklar, çarşı, kampüs
                            if (!frontage && CityMap.Organik && own && rng.NextDouble() < (zone == Zone.Campus ? 0.5 : AtolyeAyari.YalnizAtolye ? 0.97 : 0.85) &&
                                BolgeBinasi(zone, tile, face, rng)) { }
                            else if (!frontage && CityMap.Organik && own && BolgeHavuzu(BolgeAnahtar(zone)) == null && rng.NextDouble() < 0.35)
                            {
                                // Organik şehir: yola değmeyen arsada arka bina (kare avlu plakası merdiven gibi görünüyordu)
                                var havuz = zone == Zone.HighRise ? ownHigh : zone == Zone.OldTown ? ownOld : ownMixed;
                                if (havuz == null || havuz.Length == 0) havuz = ownTown;
                                var b = Pick(havuz, rng, Mathf.Max(1, floors - 1), false);
                                AddPrefab(b, tile, OwnFootprint(b, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (!frontage && CityMap.Organik) AddTrees(tile, rng, 1 + rng.Next(3), packTreesOk);
                            else if (!frontage)
                            {
                                if (UsePack && courtTiles.Length > 0)
                                {
                                    // İç avlu: bahçe, oyun alanı, havuz; bir köşesinde ağaç.
                                    // Otopark yok: Londra blokları avluyu dört yandan kapatıyor, arabaların girecek yolu yok.
                                    var secenek = courtGardens != null && courtGardens.Length > 0 ? courtGardens : courtTiles;
                                    var avlu = secenek[rng.Next(secenek.Length)];
                                    if (avlu.name.Contains("havuz") && rng.NextDouble() < 0.6) avlu = secenek[rng.Next(secenek.Length)];
                                    AddFlat(avlu, tile, 90f * rng.Next(4), 2);
                                    AddTrees(tile, rng, 1, packTreesOk, new Vector2(rng.Next(2) == 0 ? -0.36f : 0.36f, rng.Next(2) == 0 ? -0.36f : 0.36f), 0.04f);
                                }
                                // İç avlu: eski şehirde az ağaç, diğerlerinde yeşil alan
                                else if (zone == Zone.OldTown) AddTrees(tile, rng, 1, packTreesOk);
                                else AddTrees(tile, rng, 1 + rng.Next(3), packTreesOk);
                            }
                            else if (own && kamuKalan.Count > 0 && (zone == Zone.Mixed || zone == Zone.OldTown) && rng.NextDouble() < 0.12)
                            {
                                // Kamu binaları (polis, itfaiye, hastane, okul, kütüphane, belediye, postane): her biri şehirde bir kez
                                int ki = rng.Next(kamuKalan.Count);
                                var kb = kamuKalan[ki];
                                kamuKalan.RemoveAt(ki);
                                AddPrefab(kb, tile, OwnFootprint(kb, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (own && (CityMap.Organik || (!BlokVar && rng.NextDouble() < 0.9)) && BolgeBinasi(zone, tile, face, rng)) { }
                            else if (!BlokVar && own && ownRef != null && ownRef.Length > 0 && (zone == Zone.OldTown || zone == Zone.Mixed || zone == Zone.HighRise) &&
                                     rng.NextDouble() < 0.28)
                            {
                                // Referans binaları (Haussmann, Altbau, pasajlı bina, pub, fırın, benzinlik …)
                                var rb = ownRef[rng.Next(ownRef.Length)];
                                AddPrefab(rb, tile, OwnFootprint(rb, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (CityMap.Organik && own && (zone == Zone.Mixed || zone == Zone.OldTown || zone == Zone.HighRise || zone == Zone.Campus) &&
                                     rng.NextDouble() < (zone == Zone.Mixed ? 0.5 : 0.35))
                            {
                                // Organik şehir: blokların arasına başka binalar da karışsın (aynı modeller tekrar etmesin)
                                bool referans = ownRef != null && ownRef.Length > 0 && rng.NextDouble() < 0.35;
                                var havuz = referans ? ownRef : zone == Zone.HighRise ? ownHigh : zone == Zone.OldTown ? ownOld : ownMixed;
                                if (havuz == null || havuz.Length == 0) havuz = ownTown;
                                var b = referans ? havuz[rng.Next(havuz.Length)] : Pick(havuz, rng, floors, false);
                                AddPrefab(b, tile, OwnFootprint(b, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (BlokVar && (zone == Zone.OldTown || zone == Zone.Mixed || zone == Zone.HighRise || zone == Zone.Campus))
                            {
                                // Kapalı blok (Londra): bitişik binalar kaldırıma yaslanır, köşede L biçimli köşe binası.
                                // Eski Avrupa binaları (Konut_ref, Büro, ofis kulesi) Londra blokları varken kullanılmaz.
                                AddBlok(map, tile, floors, rng);
                            }
                            else if (own && ownTown.Length > 0)
                            {
                                var havuz = zone == Zone.HighRise ? ownHigh : zone == Zone.OldTown ? ownOld : ownMixed;
                                var b = Pick(havuz, rng, floors, false);
                                AddPrefab(b, tile, OwnFootprint(b, 0.97f), face, null, default, 0f, true, true);
                            }
                            else if (pack)
                                AddPrefab(Pick(packBuildings, rng, floors, false), tile, zone == Zone.HighRise ? 0.8f : 0.86f, face, null);
                            else
                                AddBox(tile, zone == Zone.HighRise ? 0.74f : 0.84f, 0.25f + floors * 0.16f, CityMap.RoofColor(zone, x, y), null, false);
                            break;
                    }
                }
            }
            if (CityMap.Organik) OrganikYollarKur(map);
            BuildCountryside(map, pack, packTreesOk, rng);
            Refresh(true);
            foreach (var p in pieces) p.t.gameObject.SetActive(false);
            AtolyeUretici.SureyiYaz();   // oyun çalışırken kurulan atölye modellerinin süresi
            Built?.Invoke();
        }

        Material organikOrnek;
        readonly List<Vector2> koyNoktalari = new List<Vector2>();
        readonly List<Vector2> banliyoEvleri = new List<Vector2>();   // çayır çimi bu noktalara girmez

        /// <summary>Organik şehir: yol ağı ve nehir tek parça mesh (merkez ofisin karesine bağlı: hiç kararmaz).</summary>
        void OrganikYollarKur(CityMap map)
        {
            Material ornek = null;
            foreach (var p in pieces)
            {
                if (!p.prefab) continue;
                var r = p.t.GetComponentInChildren<Renderer>();
                if (r != null && r.sharedMaterial != null) { ornek = r.sharedMaterial; break; }
            }
            // Yol ağı ve nehir uzaktan (2D harita) da görünür kalır: 3D binalarla birlikte gizlenmez, kilitli bölgede kararmaz
            // Bölgeye göre desenli 3D zemin (nehir yatağı çukur), canlı nehir (tekneler, ördekler, sazlık)
            organikOrnek = ornek;
            OrganikZemin.Kur(root, ornek);
            map.Zemin3D();
            OrganikSehir3D.Kur(root, ornek);
            NehirHayati.Kur(root, 99);
            // Lambalar, banklar, duraklar, levhalar, elektrik direkleri ... (Blender: Donati_*)
            if (UsePack) SokakDonatilari.Kur(root, 777);
        }

        /// <summary>
        /// Paketten bina seç: merkezde yüksek binalar daha olası, sanayide alçak/geniş; dükkanlarda alçak binalar.
        /// (Prefabların boyu bilinmediği için yükseklik/genişlik oranına göre ağırlık verilir.)
        /// </summary>
        const float OwnLotDepth = 0.08f;   // Blender'daki arsa levhası kalınlığı (metre; FBX 1 m = 1 birim)
        const float OwnAsphalt = 0.10f;    // Blender'da yol parçalarının asfalt yüzeyi (metre)
        // Kendi yol parçalarımızda kaldırım üstünün dünya yüksekliği: (0,22 − 0,10) m × (1,01 / 10 m) yukarıda (−Z yukarı)
        const float OwnSidewalkTop = -(0.22f - 0.10f) * 0.101f - 0.0025f;

        /// <summary>Kendi düz parçalarımız mı (yol, zemin, park, avlu, meydan)?</summary>
        static bool IsOwnFlat(string name) =>
            name.StartsWith("Yol_") || name.StartsWith("Park_") || name.StartsWith("Avlu_") || name.StartsWith("Meydan_");

        static bool IsOwn(string name)
        {
            foreach (var p in OwnPrefixes) if (name.StartsWith(p)) return true;
            return false;
        }

        /// <summary>Dükkanın kendi binası (Burger Queen → Burger Queen binası). Yoksa null: paket binası ya da kutu kullanılır.</summary>
        static GameObject OwnForShop(string shopName, System.Random rng)
        {
            if (ownByKey == null || !ownByKey.TryGetValue(shopName, out var l) || l.Count == 0) return null;
            return l[rng.Next(l.Count)];
        }

        Dictionary<Vector2Int, Zone> binaBolge;

        static bool BinaBolgesiMi(Zone z) =>
            z == Zone.OldTown || z == Zone.Mixed || z == Zone.HighRise || z == Zone.Houses || z == Zone.Campus || z == Zone.Warehouse || z == Zone.Mall;

        /// <summary>
        /// Bina seçimi için düzlenmiş bölge: 5×5 komşulukta en çok görülen bina bölgesi (kendi karesi iki kat sayılır).
        /// Park, meydan, nehir ve kır olduğu gibi kalır. Böylece mahalleler bütün olur: apartman bloklarının arasına tek
        /// müstakil ev, ev mahallesinin ortasına tek kule düşmez.
        /// </summary>
        Zone BinaBolgesi(Vector2Int t, Zone z)
        {
            if (!BinaBolgesiMi(z)) return z;
            if (binaBolge == null) binaBolge = new Dictionary<Vector2Int, Zone>();
            if (binaBolge.TryGetValue(t, out var hazir)) return hazir;
            var say = new int[16];
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    var zz = CityMap.ZoneOf(t.x + dx, t.y + dy);
                    if (!BinaBolgesiMi(zz)) continue;
                    say[(int)zz] += (dx == 0 && dy == 0) ? 2 : 1;
                }
            int en = (int)z;
            for (int i = 0; i < say.Length; i++) if (say[i] > say[en]) en = i;
            var sonuc = (Zone)en;
            binaBolge[t] = sonuc;
            return sonuc;
        }

        static string BolgeAnahtar(Zone z)
        {
            switch (z)
            {
                case Zone.OldTown: return "eski";
                case Zone.Mixed: return "karma";
                case Zone.HighRise: return "kule";
                case Zone.Houses: return "ev";
                case Zone.Campus: return "kampus";
                case Zone.Warehouse: return "sanayi";
                case Zone.Mall: return "avm";
                default: return null;
            }
        }

        static GameObject[] BolgeHavuzu(string anahtar) =>
            anahtar != null && bolgeHavuz != null && bolgeHavuz.TryGetValue(anahtar, out var a) && a.Length > 0 ? a : null;

        /// <summary>
        /// Bölgeye özel bina seç (yoksa null). Aynı model art arda gelmez; kilise şehirde en çok iki kez ve seyrek çıkar.
        /// </summary>
        GameObject BolgeSec(string anahtar, System.Random rng)
        {
            var h = BolgeHavuzu(anahtar);
            if (h == null) return null;
            GameObject b = null;
            for (int deneme = 0; deneme < 6; deneme++)
            {
                b = h[rng.Next(h.Length)];
                bool kilise = b.name.Contains("_kilise_");
                if (kilise && (kiliseSayisi >= 2 || rng.NextDouble() < 0.85)) continue;
                if (bolgeSon.TryGetValue(anahtar, out var son) && son == b && h.Length > 1) continue;
                break;
            }
            if (b.name.Contains("_kilise_"))
            {
                if (kiliseSayisi >= 2) return null;
                kiliseSayisi++;
            }
            bolgeSon[anahtar] = b;
            return b;
        }

        /// <summary>Bölgeye özel binayı kareye koy; havuz boşsa false.</summary>
        bool BolgeBinasi(Zone zone, Vector2Int tile, float face, System.Random rng, string anahtar = null)
        {
            anahtar = anahtar ?? BolgeAnahtar(zone);
            if (rng.NextDouble() < 0.85 && ModulBinasi(anahtar, tile, face, rng)) return true;
            var b = BolgeSec(anahtar, rng);
            if (b == null) return false;
            AddPrefab(b, tile, 0.97f, face, null, default, 0f, true, true);
            return true;
        }

        // Yollar 10 m'lik kareye göre (OrganikSehir3D), atölye arsası 14 m: 14 m'yi bir kareye sığdırmak binaları gerçeğin ~%70'ine
        // küçültüyordu (yollar binalardan büyük görünüyordu). Yalnız atölye şehrinde arsa 12,5 m'ye sığar (komşu arsanın bahçe
        // şeridine biraz taşar; sıra evler bitişik görünür) ve binalar biraz daha uzun.
        const float AtolyeBoyCarpani = 1.32f;
        static bool AtolyeSehirOlcegi(string ad) => AtolyeAyari.YalnizAtolye && ad.StartsWith("Bolge_") && AtolyeAyari.AtolyeMi(ad);

        /// <summary>Ad sıra ev şeridi mi ("..._atolye_s_&lt;bölge&gt;_m&lt;x0&gt;&lt;w&gt;_&lt;n&gt;"); öyleyse yuvası (x0, genişlik).</summary>
        static bool ModulYuvasi(string ad, out Vector2Int yuva)
        {
            yuva = default;
            int i = ad.IndexOf("_atolye_s_", System.StringComparison.Ordinal);
            if (i < 0) return false;
            int m = ad.IndexOf("_m", i + 10, System.StringComparison.Ordinal);
            if (m < 0 || m + 4 > ad.Length || !char.IsDigit(ad[m + 2]) || !char.IsDigit(ad[m + 3])) return false;
            yuva = new Vector2Int(ad[m + 2] - '0', ad[m + 3] - '0');
            return true;
        }

        /// <summary>
        /// Arsayı 2–4 sıra ev şeridinden kurar (her şerit ayrı bina, kendi üslubu / rengi / boyu): aynı modelden kurulan iki
        /// arsa yok denecek kadar az. Bölgenin şeridi yoksa ya da bir yuva boşsa false (bütün arsa tek binaya kalır).
        /// </summary>
        bool ModulBinasi(string anahtar, Vector2Int tile, float face, System.Random rng)
        {
            if (anahtar == null || modulHavuz == null || !modulHavuz.TryGetValue(anahtar, out var yuvalar) || yuvalar.Count == 0) return false;
            var bolum = ModulBolum[rng.Next(ModulBolum.Length)];
            int x = 0;
            var secilen = new List<GameObject>(4);
            foreach (int w in bolum)
            {
                if (!yuvalar.TryGetValue(new Vector2Int(x, w), out var liste) || liste.Length == 0) return false;
                secilen.Add(liste[rng.Next(liste.Length)]);
                x += w;
            }
            foreach (var b in secilen) AddPrefab(b, tile, 0.97f, face, null, default, 0f, true, true);
            return true;
        }

        /// <summary>Kendi binalarımız gerçek boylarıyla oranlı: hipermarket evden geniş, ama komşu kareye taşmaz.</summary>
        static float OwnFootprint(GameObject prefab, float baseFoot)
        {
            const float RefWidth = 14f;   // metre: tipik müstakil ev arsası
            float w = Width(prefab);
            return Mathf.Min(0.99f, baseFoot * Mathf.Clamp(w / RefWidth, 0.95f, 1.3f));
        }

        /// <summary>Karenin bloğu bir simge yapıya ayrıldıysa (yollar hariç) true. Yol karesi değilse çağrılır.</summary>
        static bool frontageRoadOnly(CityMap map, int x, int y) => map.IsRoad(x, y);

        /// <summary>
        /// Simge yapılar için bloklar seç: eski şehir, çarşı ve kule bloklarından içinde yol, su, dükkân, merkez ofis ya da
        /// meydan çeşmesi olmayan 3×3 bloklar. Her simge bir kez, bloklar birbirine bitişik değil.
        /// </summary>
        void SimgeBloklariniSec(CityMap map, Dictionary<Vector2Int, Shop> shopTiles)
        {
            simgeBlok = new Dictionary<Vector2Int, GameObject>();
            if (CityMap.Organik) return;     // organik şehirde 3×3 blok yok
            if (!UsePack || ownSimge == null || ownSimge.Length == 0) return;
            var aday = new List<Vector2Int>();
            for (int bx = 0; bx * CityMap.Block + 3 < CityMap.Width; bx++)
                for (int by = 0; by * CityMap.Block + 3 < CityMap.Height; by++)
                {
                    var c = new Vector2Int(bx * CityMap.Block + 2, by * CityMap.Block + 2);
                    var z = CityMap.ZoneOf(c);
                    if (z != Zone.OldTown && z != Zone.Mixed && z != Zone.HighRise && z != Zone.Campus) continue;
                    bool ok = true;
                    for (int dx = -1; dx <= 1 && ok; dx++)
                        for (int dy = -1; dy <= 1 && ok; dy++)
                        {
                            var t = new Vector2Int(c.x + dx, c.y + dy);
                            if (map.IsRoad(t.x, t.y) || CityMap.WaterAt(t.x, t.y) || CityMap.RailAt(t.x, t.y) || shopTiles.ContainsKey(t) ||
                                t == map.HQTile || t == CityMap.PlazaCenter || CityMap.ZoneOf(t) != z) ok = false;
                        }
                    if (ok) aday.Add(c);
                }
            var rng = new System.Random(4242);
            var simgeler = new List<GameObject>(ownSimge);
            while (simgeler.Count > 0 && aday.Count > 0)
            {
                var c = aday[rng.Next(aday.Count)];
                aday.Remove(c);
                bool komsu = false;
                foreach (var k in simgeBlok.Keys)
                    if (Mathf.Abs(k.x - c.x) <= CityMap.Block && Mathf.Abs(k.y - c.y) <= CityMap.Block) komsu = true;
                if (komsu) continue;
                int i = rng.Next(simgeler.Count);
                simgeBlok[c] = simgeler[i];
                simgeler.RemoveAt(i);
            }
        }

        static bool BlokVar => UsePack && blokDuz != null && blokDuz.Count > 0;

        /// <summary>İstenen kata en yakın blok modeli (aynı kattan rastgele varyant).</summary>
        static GameObject BlokSec(Dictionary<int, List<GameObject>> d, int floors, System.Random rng)
        {
            if (d == null || d.Count == 0) return null;
            int best = -1, bestFark = int.MaxValue;
            foreach (var k in d.Keys)
            {
                int f = Mathf.Abs(k - floors);
                if (f < bestFark || (f == bestFark && rng.Next(2) == 0)) { bestFark = f; best = k; }
            }
            var l = d[best];
            return l[rng.Next(l.Count)];
        }

        /// <summary>
        /// Blok binası: tek yola bakan kare ara bina, iki dik yola bakan kare köşe binası (L).
        /// Kanonik köşe modeli kuzey ve doğu yollarına bakar (yol köşesiyle aynı dönüş kuralı).
        /// </summary>
        void AddBlok(CityMap map, Vector2Int t, int floors, System.Random rng)
        {
            bool n = t.y < CityMap.Height - 1 && map.IsRoad(t.x, t.y + 1);
            bool s = t.y > 0 && map.IsRoad(t.x, t.y - 1);
            bool e = t.x < CityMap.Width - 1 && map.IsRoad(t.x + 1, t.y);
            bool w = t.x > 0 && map.IsRoad(t.x - 1, t.y);
            bool kose = !CityMap.Organik && (n || s) && (e || w) && blokKose != null && blokKose.Count > 0;
            GameObject p;
            float aci;
            if (kose)
            {
                // İki dik yol: güneydeki ve doğudaki yol öncelikli (üç yola bakan karede)
                bool kn = !s && n, ke = e;
                aci = (kn && ke) ? 0f : kn ? 90f : !ke ? 180f : 270f;
                p = BlokSec(blokKose, floors, rng);
            }
            else
            {
                aci = FaceAngle(map, t);
                p = BlokSec(blokDuz, floors, rng);
            }
            AddPrefab(p, t, 1.003f, aci, null, default, 0f, true, true);
        }

        GameObject Pick(GameObject[] list, System.Random rng, int floors, bool shop)
        {
            GameObject best = list[rng.Next(list.Length)];
            float bestScore = float.MinValue;
            for (int i = 0; i < 3; i++)
            {
                var cand = list[rng.Next(list.Length)];
                float tall = Tallness(cand);
                // Kat sayısına uygun boy/en oranı: 1–2 kat basık, 10 kat kule
                float want = Mathf.Max(0.4f, floors * 0.32f);
                float score = -Mathf.Abs(Mathf.Log(Mathf.Max(0.1f, tall) / want)) + (float)rng.NextDouble() * 0.6f;
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            return best;
        }

        static float Tallness(GameObject prefab)
        {
            var sz = PrefabSize(prefab);
            return sz.y / Mathf.Max(0.01f, Mathf.Max(sz.x, sz.z));
        }

        static float Width(GameObject prefab)
        {
            var sz = PrefabSize(prefab);
            return Mathf.Max(sz.x, sz.z);
        }

        static readonly Dictionary<GameObject, Vector3> sizeCache = new Dictionary<GameObject, Vector3>();
        /// <summary>Prefabın kendi ölçeğindeki boyutu (Y yukarı), sahneye koymadan.</summary>
        static Vector3 PrefabSize(GameObject prefab)
        {
            if (sizeCache.TryGetValue(prefab, out var cached)) return cached;
            AtolyeUretici.Hazirla(prefab);   // atölye şablonu: mesh'leri ilk kullanımda kurulur
            var b = new Bounds();
            bool any = false;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var s = mf.transform.lossyScale;
                var size = Vector3.Scale(mb.size, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
                var c = mf.transform.TransformPoint(mb.center);
                var bb = new Bounds(c, size);
                if (!any) { b = bb; any = true; } else b.Encapsulate(bb);
            }
            var result = any ? b.size : Vector3.one;
            sizeCache[prefab] = result;
            return result;
        }

        static bool IsAdjacentToRoad(CityMap map, int x, int y) =>
            (x > 0 && map.IsRoad(x - 1, y)) || (x < CityMap.Width - 1 && map.IsRoad(x + 1, y)) ||
            (y > 0 && map.IsRoad(x, y - 1)) || (y < CityMap.Height - 1 && map.IsRoad(x, y + 1));

        /// <summary>Binanın önü yola baksın (güneydeki yol öncelikli: kamera güneyden baktığı için cephe görünür).</summary>
        static float FaceAngle(CityMap map, Vector2Int t)
        {
            if (CityMap.Organik) return SehirPlani.YolaBakanAci(t.x, t.y);
            if (t.y > 0 && map.IsRoad(t.x, t.y - 1)) return 180f;
            if (t.x > 0 && map.IsRoad(t.x - 1, t.y)) return 90f;
            if (t.x < CityMap.Width - 1 && map.IsRoad(t.x + 1, t.y)) return 270f;
            return 0f;
        }

        /// <summary>
        /// Şehir dışı: ormanlar ve çiftlikler 3D (tarla, çayır, nehir ve kır yolları 2D haritadan görünür).
        /// Parçalar merkez ofisin karesine bağlanır (o kare hep açık): kilitli bölge gibi kararmazlar.
        /// </summary>
        /// <summary>Kır yolları şehir yollarıyla aynı 3D parçalarla döşenir (şehirden ne kadar uzağa).</summary>
        const int KirYoluUzunluk = 30;

        void BuildCountryside(CityMap map, bool pack, bool packTreesOk, System.Random rng)
        {
            KirZeminiKur(map);
            KirParselleriKur(map);
            KirCimenKur(map);
            var anchor = map.HQTile;
            if (UsePack && UseRoads && roadStraight != null)
            {
                foreach (var r in CityMap.CountryRoads)
                {
                    bool yatay = Mathf.Approximately(r.a.y, r.b.y);
                    int sabit = Mathf.RoundToInt(yatay ? r.a.y : r.a.x);
                    float bas = yatay ? Mathf.Min(r.a.x, r.b.x) : Mathf.Min(r.a.y, r.b.y);
                    float son = yatay ? Mathf.Max(r.a.x, r.b.x) : Mathf.Max(r.a.y, r.b.y);
                    for (int k = Mathf.CeilToInt(bas); k <= Mathf.FloorToInt(son); k++)
                    {
                        var t = yatay ? new Vector2Int(k, sabit) : new Vector2Int(sabit, k);
                        int W = CityMap.Width, H = CityMap.Height;
                        if (t.x >= 0 && t.y >= 0 && t.x < W && t.y < H) continue;           // şehrin içi: zaten yol karesi
                        int u = Mathf.Max(Mathf.Max(-t.x, t.x - W + 1), Mathf.Max(-t.y, t.y - H + 1));
                        if (u > KirYoluUzunluk) continue;
                        // Parçalar merkez ofisin karesine bağlanır: kilitli bölge gibi kararmasın
                        AddFlat(roadStraight, anchor, yatay ? 90f : 0f, 1, new Vector2(t.x - anchor.x, t.y - anchor.y));
                    }
                }
            }
            if (CityMap.Organik)
            {
                BanliyoKur(map, rng, packTreesOk);
                KoyleriKur(map, rng, packTreesOk);
                KirOrman.Kur(root, map, organikOrnek, koyNoktalari, 2026);
                Cim.Kur(root, map, banliyoEvleri);
            }
            int sira = 0;
            bool extras = UsePack && countryExtras != null && countryExtras.Length > 0;
            foreach (var t in map.CountryTrees)
            {
                var off = new Vector2(t.x - anchor.x, t.y - anchor.y);
                // Her beş ağaçtan birinin yanında çiçek öbeği ya da kaya (çayır canlı görünsün)
                if (extras && (sira++ % 9) == 0)
                    AddPrefab(countryExtras[rng.Next(countryExtras.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null,
                              off + new Vector2(0.45f, -0.3f), 0.06f, false);
                if (packTreesOk)
                    AddPrefab(packTrees[rng.Next(packTrees.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null, off, t.z + 0.15f, false);
                else
                {
                    var c = Color.Lerp(new Color(0.14f, 0.38f, 0.20f), new Color(0.26f, 0.50f, 0.22f), (float)rng.NextDouble());
                    AddBox(anchor, 0.3f, t.z, c, null, false, off, false);
                }
            }
            foreach (var f in map.Farms)
            {
                var off = new Vector2(f.x - anchor.x, f.y - anchor.y);
                if (UsePack && ownFarm != null && ownFarm.Length > 0)
                {
                    // Kendi çiftlik yapılarımız: ahır, sera ya da yel değirmeni; yanında ikinci bir yapı
                    var a = ownFarm[rng.Next(ownFarm.Length)];
                    var b = ownFarm[rng.Next(ownFarm.Length)];
                    AddPrefab(a, anchor, OwnFootprint(a, 0.9f), 180f, null, off, 0f, false, true);
                    if (b != a) AddPrefab(b, anchor, OwnFootprint(b, 0.9f), 90f * rng.Next(4), null, off + new Vector2(1f, 0.2f), 0f, false, true);
                    continue;
                }
                if (pack) AddPrefab(Pick(packBuildings, rng, 1, false), anchor, 0.55f, 180f, null, off, 0f, false);
                else AddBoxR(anchor, new Vector2(0.6f, 0.45f), 0.3f, new Color(0.85f, 0.80f, 0.70f), null, false, off, false);
                // Kırmızı ahır
                AddBoxR(anchor, new Vector2(0.7f, 0.5f), 0.35f, new Color(0.62f, 0.22f, 0.18f), null, false, off + new Vector2(0.9f, 0.2f), false);
            }
        }

        /// <summary>
        /// Organik şehrin çevresi: şehir harita kenarında birden bitmesin. Kır yollarının iki yanında evler ve aradaki kuşakta
        /// seyrek evler, ağaçlar; şehirden uzaklaştıkça seyrelir, sonra tarlalar başlar. Süs: sipariş gelmez, hiç kararmaz.
        /// </summary>
        void BanliyoKur(CityMap map, System.Random rng, bool packTreesOk)
        {
            var evler = ownHousesTum != null && ownHousesTum.Length > 0 ? ownHousesTum : ownHouses;
            if (!UsePack || evler == null || evler.Length == 0) return;
            var anchor = map.HQTile;
            int w = CityMap.Width, h = CityMap.Height;
            float B = CityMap.Banliyo;
            var dolu = new HashSet<Vector2Int>();
            banliyoEvleri.Clear();

            void Ev(Vector2 p, float aci)
            {
                var t = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                if (CityMap.KirNehirYakin(p, 0.7f)) return;
                if (!dolu.Add(t)) return;
                banliyoEvleri.Add(p);
                // Banliyö: uzun köy evleri, çiftlikler, bağ evleri çoğunlukta; arada aile evleri ve eski evler
                double z0 = rng.NextDouble();
                var bh = z0 < 0.65 ? BolgeHavuzu("banliyo") : z0 < 0.92 ? BolgeHavuzu("ev") : null;
                var e = bh != null ? bh[rng.Next(bh.Length)] : evler[rng.Next(evler.Length)];
                AddPrefab(e, anchor, OwnFootprint(e, 0.92f), aci, null, p - anchor, 0f, false, true);
                if (packTreesOk && rng.NextDouble() < 0.6)
                {
                    float r = aci * Mathf.Deg2Rad;
                    var arka = new Vector2(Mathf.Sin(r), -Mathf.Cos(r)) * 0.45f;    // evin arkası (önü yola bakar)
                    AddPrefab(packTrees[rng.Next(packTrees.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null,
                              p + arka + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.4f - anchor,
                              0.35f + (float)rng.NextDouble() * 0.25f, false);
                }
            }

            float Uzak(Vector2 p) => Mathf.Max(Mathf.Max(-p.x, p.x - (w - 1)), Mathf.Max(-p.y, p.y - (h - 1)));

            // 1) Kır yolları boyunca: iki yanda evler (şehirden uzaklaştıkça seyrek)
            foreach (var r in CityMap.CountryRoads)
            {
                var yon = (r.b - r.a).normalized;
                var yan = new Vector2(-yon.y, yon.x);
                float L = Vector2.Distance(r.a, r.b);
                for (float d = 0.5f; d < L; d += 1f)
                {
                    var p = r.a + yon * d;
                    float u = Uzak(p);
                    if (u < 0.6f || u > B + 5f) continue;
                    float olasilik = 0.85f * Mathf.Exp(-u / 6f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        if (rng.NextDouble() > olasilik) continue;
                        var q = p + yan * (0.72f * k);
                        // Yola bakan açı: City3D 0° kuzey, 90° batı, 180° güney, 270° doğu
                        var bak = -yan * k;
                        float aci = (Mathf.Atan2(-bak.x, bak.y) * Mathf.Rad2Deg + 360f) % 360f;
                        Ev(q, aci);
                    }
                }
            }

            // 2) Kuşakta seyrek evler ve ağaç öbekleri (yollardan uzak)
            for (int x = -(int)B; x < w + (int)B; x++)
                for (int y = -(int)B; y < h + (int)B; y++)
                {
                    if (x >= 0 && y >= 0 && x < w && y < h) continue;
                    var p = new Vector2(x + (float)rng.NextDouble() * 0.4f - 0.2f, y + (float)rng.NextDouble() * 0.4f - 0.2f);
                    float u = Uzak(p);
                    if (u < 0.8f || CityMap.KirYoluYakin(p, 0.7f)) continue;
                    double z = rng.NextDouble();
                    if (z < 0.42f * (1f - u / B)) Ev(p, 90f * rng.Next(4));
                    else if (packTreesOk && z < 0.5f)
                        AddPrefab(packTrees[rng.Next(packTrees.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null, p - anchor,
                                  0.35f + (float)rng.NextDouble() * 0.3f, false);
                }
        }

        /// <summary>
        /// Köyler: şehirden uzağa giden caddelerin (kır yolları) üstünde, çevre yolunun ötesinde birer küçük köy.
        /// Yolun iki yanına dizili köy evleri, ortada bir kilise ya da büyük yapı, evlerin arkasında ağaçlar.
        /// Kırın boş görünmemesi için; süs: sipariş gelmez, kararmaz.
        /// </summary>
        void KoyleriKur(CityMap map, System.Random rng, bool packTreesOk)
        {
            koyNoktalari.Clear();
            var evler = BolgeHavuzu("banliyo");
            if (evler == null || evler.Length == 0) evler = ownHousesTum != null && ownHousesTum.Length > 0 ? ownHousesTum : ownHouses;
            if (!UsePack || evler == null || evler.Length == 0) return;
            // Köy kilisesi: bölge modelleri arasındaki kiliseler (şehirdeki iki kilise sınırı köyleri saymaz)
            var kiliseler = new List<GameObject>();
            if (bolgeHavuz != null)
                foreach (var kv in bolgeHavuz) foreach (var b in kv.Value) if (b != null && b.name.Contains("_kilise_") && !kiliseler.Contains(b)) kiliseler.Add(b);
            var buyuk = kiliseler.ToArray();
            var anchor = map.HQTile;
            int w = CityMap.Width, h = CityMap.Height;
            float Uzak(Vector2 p) => Mathf.Max(Mathf.Max(-p.x, p.x - (w - 1)), Mathf.Max(-p.y, p.y - (h - 1)));
            var dolu = new HashSet<Vector2Int>();
            int koy = 0, ev = 0;
            foreach (var r in CityMap.CountryRoads)
            {
                float L = Vector2.Distance(r.a, r.b);
                if (L < 30f) continue;                                   // sadece uzağa giden caddeler (çevre yolu değil)
                var yon = (r.b - r.a).normalized;
                var yan = new Vector2(-yon.y, yon.x);
                // Yolun şehirden uzak ucuna doğru: şehir kenarından 17–30 kare ötede köy merkezi
                bool disaBas = Uzak(r.a) > Uzak(r.b);
                var dis = disaBas ? r.a : r.b;
                var ic = disaBas ? r.b : r.a;
                var d = (dis - ic).normalized;
                float hedef = 17f + (float)rng.NextDouble() * 13f;
                var merkez = ic + d * hedef;
                if (CityMap.KirNehirYakin(merkez, 4f)) merkez += d * 6f;
                if (Uzak(merkez) > 40f) continue;
                bool yakinKoy = false;
                foreach (var k in koyNoktalari) if ((k - merkez).sqrMagnitude < 14f * 14f) { yakinKoy = true; break; }
                if (yakinKoy) continue;
                koy++;
                int yari = 4 + rng.Next(4);
                for (int i = -yari; i <= yari; i++)
                {
                    var p = merkez + d * (i * 0.95f);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        if (rng.NextDouble() < 0.18) continue;
                        float uzaklik = Mathf.Abs(i) / (float)yari;
                        var q = p + new Vector2(-d.y, d.x) * (k * (0.75f + (float)rng.NextDouble() * 0.12f));
                        var t = new Vector2Int(Mathf.RoundToInt(q.x * 2f), Mathf.RoundToInt(q.y * 2f));
                        if (CityMap.KirNehirYakin(q, 0.6f) || !dolu.Add(t)) continue;
                        var bak = -new Vector2(-d.y, d.x) * k;
                        float aci = (Mathf.Atan2(-bak.x, bak.y) * Mathf.Rad2Deg + 360f) % 360f;
                        bool kilise = i == 0 && k == 1 && buyuk != null && buyuk.Length > 0;
                        var prefab = kilise ? buyuk[rng.Next(buyuk.Length)] : evler[rng.Next(evler.Length)];
                        AddPrefab(prefab, anchor, OwnFootprint(prefab, kilise ? 1.0f : 0.85f - uzaklik * 0.1f), aci, null, q - anchor, 0f, false, true);
                        koyNoktalari.Add(q);
                        ev++;
                        // Arka bahçede bir iki ağaç
                        if (packTreesOk && rng.NextDouble() < 0.7)
                        {
                            var arka = q + new Vector2(-d.y, d.x) * (k * (0.75f + (float)rng.NextDouble() * 0.35f)) + d * (((float)rng.NextDouble() - 0.5f) * 0.6f);
                            if (!CityMap.KirNehirYakin(arka, 0.4f))
                                AddPrefab(packTrees[rng.Next(packTrees.Length)], anchor, 0f, (float)rng.NextDouble() * 360f, null, arka - anchor,
                                          0.35f + (float)rng.NextDouble() * 0.25f, false);
                        }
                    }
                }
                koyNoktalari.Add(merkez);
            }
            if (koy > 0) Debug.Log($"Kurye Tycoon: kırda {koy} köy, {ev} köy evi.");
        }

        // ---------- Dokulu kır zemini ----------
        // Şehir dışı eskiden ışık almayan düz 2D renk kutularıydı (çayır, tarla): parlak ve düz görünüyordu.
        // Artık tek, ışık ve gölge alan bir zemin: hafif dalgalı çimen, sıra sıra ekilmiş tarlalar, tarla kenarlarında koyu çit izi.
        // Zeminin mevsime göre beş dokusu var (taze, yaz, kurak, sonbahar, kış); gerektiğinde üretilir ve saklanır.
        // Şehre yakın tarlaların üstüne Blender'da modellenmiş kır parselleri oturur (KirParselleriKur).
        // Nehir, kır yolları ve kar örtüsü 2D olarak zeminin üstünde kalır.

        const float KirPay = 44f;
        static readonly Texture2D[] kirDokular = new Texture2D[5];
        static Material kirMalzeme;
        int kirDokuDurum = -1;

        void KirZeminiKur(CityMap map)
        {
            float x0 = -KirPay - 0.5f, y0 = -KirPay - 0.5f;
            float w = CityMap.Width + 2f * KirPay, h = CityMap.Height + 2f * KirPay;
            kirDokuDurum = KirDurum.EnYakin();
            if (kirDokular[kirDokuDurum] == null) kirDokular[kirDokuDurum] = KirDokusu(map, x0, y0, w, h, kirDokuDurum);
            if (kirMalzeme == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh == null) return;   // 3D ışık yok: 2D kır zemini kalır
                kirMalzeme = new Material(sh) { name = "KirZemini" };
                if (kirMalzeme.HasProperty("_BaseColor")) kirMalzeme.SetColor("_BaseColor", Color.white);
                if (kirMalzeme.HasProperty("_Smoothness")) kirMalzeme.SetFloat("_Smoothness", 0.04f);
                if (kirMalzeme.HasProperty("_Metallic")) kirMalzeme.SetFloat("_Metallic", 0f);
                if (kirMalzeme.HasProperty("_Cull")) kirMalzeme.SetFloat("_Cull", 0f);   // iki yüz de çizilsin
            }
            KirDokuAta(kirDokular[kirDokuDurum]);

            var go = new GameObject("Kır zemini (3D)");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 0f, 0.012f);   // 2D nehir, yollar ve kar örtüsünün hemen altında (−Z yukarı)
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Mesh { name = "KirZemini" };
            if (CityMap.Organik)
            {
                // Tepeli kır: kare başına bir köşe; şehirden uzaklaştıkça yükselen tepeler (Arazi.Tepe), yollar vadide
                int nx = Mathf.CeilToInt(w), ny = Mathf.CeilToInt(h);
                var v = new Vector3[(nx + 1) * (ny + 1)];
                var uv = new Vector2[v.Length];
                for (int j = 0; j <= ny; j++)
                    for (int i = 0; i <= nx; i++)
                    {
                        float x = x0 + w * i / nx, y = y0 + h * j / ny;
                        v[j * (nx + 1) + i] = new Vector3(x, y, -Arazi.Tepe(x, y));
                        uv[j * (nx + 1) + i] = new Vector2((float)i / nx, (float)j / ny);
                    }
                var tr = new int[nx * ny * 6];
                int k = 0;
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                        tr[k++] = a; tr[k++] = c; tr[k++] = b;
                        tr[k++] = b; tr[k++] = c; tr[k++] = d;
                    }
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.vertices = v;
                m.uv = uv;
                m.triangles = tr;
                m.RecalculateNormals();
                var nr = m.normals;
                if (nr.Length > 0 && nr[0].z > 0f)
                {
                    for (int q = 0; q < tr.Length; q += 3) { int tmp = tr[q + 1]; tr[q + 1] = tr[q + 2]; tr[q + 2] = tmp; }
                    m.triangles = tr;
                    m.RecalculateNormals();
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;   // tepeler gölge düşürsün
            }
            else
            {
                m.vertices = new[] { new Vector3(x0, y0, 0f), new Vector3(x0 + w, y0, 0f), new Vector3(x0 + w, y0 + h, 0f), new Vector3(x0, y0 + h, 0f) };
                m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };   // (0,0,−1): yukarı
                m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            }
            m.RecalculateBounds();
            mf.sharedMesh = m;
            mr.sharedMaterial = kirMalzeme;
            if (!CityMap.Organik) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.sortingOrder = -20;
            map.Kir3DZemin();   // 2D çayır ve tarlalar gizlenir (sadece kar yağınca beyaz örtü olarak görünür)
        }

        static void KirDokuAta(Texture2D t)
        {
            if (kirMalzeme == null || t == null) return;
            kirMalzeme.mainTexture = t;
            if (kirMalzeme.HasProperty("_BaseMap")) kirMalzeme.SetTexture("_BaseMap", t);
        }

        /// <summary>Mevsime göre çimen renkleri (taze, yaz, kurak, sonbahar, kış) ve tarlaların o mevsimdeki tonu.</summary>
        static readonly Color[] KirCimA = { new Color(0.40f, 0.57f, 0.26f), new Color(0.40f, 0.53f, 0.27f), new Color(0.58f, 0.56f, 0.32f), new Color(0.45f, 0.48f, 0.27f), new Color(0.37f, 0.43f, 0.29f) };
        static readonly Color[] KirCimB = { new Color(0.50f, 0.66f, 0.31f), new Color(0.51f, 0.62f, 0.33f), new Color(0.68f, 0.64f, 0.38f), new Color(0.55f, 0.55f, 0.31f), new Color(0.45f, 0.50f, 0.35f) };
        static readonly Color[] KirTarlaTon = { new Color(0.45f, 0.62f, 0.30f), Color.clear, new Color(0.80f, 0.70f, 0.42f), new Color(0.55f, 0.43f, 0.30f), new Color(0.46f, 0.42f, 0.33f) };
        static readonly float[] KirTarlaKarisim = { 0.25f, 0f, 0.35f, 0.35f, 0.45f };

        static Texture2D KirDokusu(CityMap map, float x0, float y0, float w, float h, int durum)
        {
            const int TW = 1536;
            int TH = Mathf.Clamp(Mathf.RoundToInt(TW * h / w), 256, 2048);
            var px = new Color32[TW * TH];
            float sx = w / TW, sy = h / TH;

            // Çimen: iki yumuşak ton arasında geniş lekeler, üstüne ince doku
            var cA = KirCimA[durum];
            var cB = KirCimB[durum];
            for (int j = 0; j < TH; j++)
            {
                float wy = y0 + (j + 0.5f) * sy;
                for (int i = 0; i < TW; i++)
                {
                    float wx = x0 + (i + 0.5f) * sx;
                    float n1 = Mathf.PerlinNoise(wx * 0.07f + 31.7f, wy * 0.07f + 11.3f);
                    float n2 = Mathf.PerlinNoise(wx * 0.55f + 5.1f, wy * 0.55f + 77.9f);
                    var c = Color.Lerp(cA, cB, n1) * (0.93f + 0.12f * n2);
                    c.a = 1f;
                    px[j * TW + i] = c;
                }
            }

            // Tarlalar: ekin sıraları (yatay ya da dikey), kenarda koyu çit izi, hafif renk dalgası; mevsim tonuna doğru kayar
            int fi = 0;
            foreach (var f in map.Fields)
            {
                fi++;
                var r = f.r;
                var fc = Color.Lerp(f.c, KirTarlaTon[durum], KirTarlaKarisim[durum]);
                bool yatay = ((fi * 7919) % 2) == 0;
                float aralik = 0.38f + 0.08f * (fi % 3);
                int i0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - x0) / sx)), i1 = Mathf.Min(TW - 1, Mathf.CeilToInt((r.xMax - x0) / sx));
                int j0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - y0) / sy)), j1 = Mathf.Min(TH - 1, Mathf.CeilToInt((r.yMax - y0) / sy));
                for (int j = j0; j <= j1; j++)
                {
                    float wy = y0 + (j + 0.5f) * sy;
                    for (int i = i0; i <= i1; i++)
                    {
                        float wx = x0 + (i + 0.5f) * sx;
                        float kenar = Mathf.Min(Mathf.Min(wx - r.xMin, r.xMax - wx), Mathf.Min(wy - r.yMin, r.yMax - wy));
                        if (kenar < 0f) continue;
                        int k = j * TW + i;
                        Color c;
                        if (kenar < 0.09f) c = (Color)px[k] * 0.8f;   // tarla kenarı: koyu çimen/çit izi
                        else
                        {
                            float u = yatay ? wy - r.yMin : wx - r.xMin;
                            float serit = Mathf.Repeat(u / aralik, 1f) < 0.5f ? 0.9f : 1.04f;
                            float n = Mathf.PerlinNoise(wx * 0.35f + fi * 3.3f, wy * 0.35f + fi * 1.7f);
                            c = fc * (serit * (0.94f + 0.1f * n));
                        }
                        c.a = 1f;
                        px[k] = c;
                    }
                }
            }

            var t = new Texture2D(TW, TH, TextureFormat.RGBA32, true)
            {
                name = "KirZemini_" + durum, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        // ---------- Kır parselleri (Blender: Kir_alan_...) ----------
        // Şehre yakın tarlalara Blender'da modellenmiş parseller oturur: ekin sıraları, sürülmüş toprak, rulo balyalı anız,
        // üzüm bağı, meyve bahçesi, inekli çayır, gölet, seralar, koru. Tarlanın boyutuna esnetilir. Uzak tarlalar dokuda kalır.
        // Malzemeleri ("Kir..." ile başlayanlar) parsele özel kopyalardır: KirGuncelle mevsime, ıslaklığa ve kara göre boyar.

        readonly List<GameObject> kirParseller = new List<GameObject>();
        static readonly Dictionary<Material, Material> kirKopya = new Dictionary<Material, Material>();
        static readonly List<(Material m, string etiket, Color asil)> kirBoyali = new List<(Material, string, Color)>();
        float kirSonGuncelleme = -999f;
        bool kirGorunur = true;

        static readonly (string on, float agirlik)[] KirTurAgirlik =
        {
            ("Kir_alan_ekin", 0.30f), ("Kir_alan_surulmus", 0.14f), ("Kir_alan_saman", 0.12f), ("Kir_alan_cayir", 0.15f),
            ("Kir_alan_meyve", 0.09f), ("Kir_alan_bag", 0.06f), ("Kir_alan_sera", 0.04f), ("Kir_alan_gol", 0.05f), ("Kir_alan_koru", 0.05f)
        };

        void KirParselleriKur(CityMap map)
        {
            kirParseller.Clear();
            if (kirAlanlari == null || kirAlanlari.Length == 0) return;
            float menzil = GrafikKalitesi.Seviye == GrafikKalitesi.Yuksek ? 26f : GrafikKalitesi.Seviye == GrafikKalitesi.Orta ? 18f : 11f;
            var parent = new GameObject("Kır parselleri (3D)").transform;
            parent.SetParent(root, false);
            int fi = 0;
            foreach (var f in map.Fields)
            {
                fi++;
                var r = f.r;
                // Şehrin dikdörtgenine uzaklık
                float dx = Mathf.Max(0f, Mathf.Max(-r.xMax, r.xMin - (CityMap.Width - 1)));
                float dy = Mathf.Max(0f, Mathf.Max(-r.yMax, r.yMin - (CityMap.Height - 1)));
                if (Mathf.Sqrt(dx * dx + dy * dy) > menzil) continue;
                var prefab = KirSec(fi);
                if (prefab == null) continue;
                var inst = Instantiate(prefab, parent, false);
                inst.name = prefab.name;
                bool cevir = ((fi * 2654435761u) >> 7) % 2 == 1;
                inst.transform.localRotation = Quaternion.Euler(0f, 0f, cevir ? 180f : 0f) * Quaternion.Euler(-90f, 0f, 0f);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localScale = Vector3.one;
                foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
                var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Destroy(inst); continue; }
                var b = CombinedBounds(rs);
                float ox = r.width / Mathf.Max(0.01f, b.size.x), oy = r.height / Mathf.Max(0.01f, b.size.y);
                // FBX −90° X ile döndürülür: yerel X dünya X'i, yerel Z dünya Y'si, yerel Y yükseklik
                inst.transform.localScale = new Vector3(ox, (ox + oy) * 0.5f, oy);
                b = CombinedBounds(rs);
                // Eksen karışırsa (FBX dönüşü) ölçeği dünya boyutuna göre düzelt
                if (Mathf.Abs(b.size.x - r.width) > 0.05f || Mathf.Abs(b.size.y - r.height) > 0.05f)
                {
                    var s = inst.transform.localScale;
                    inst.transform.localScale = new Vector3(s.x * r.width / Mathf.Max(0.01f, b.size.x), s.y, s.z * r.height / Mathf.Max(0.01f, b.size.y));
                    b = CombinedBounds(rs);
                }
                // Taban kır zemininin hemen üstünde (−Z yukarı: zeminin z'si +0,012)
                inst.transform.position += new Vector3(r.center.x - b.center.x, r.center.y - b.center.y, 0.006f - b.max.z);
                foreach (var rr in rs)
                {
                    rr.shadowCastingMode = GrafikKalitesi.ZeminGolgesi ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    rr.receiveShadows = true;
                    rr.sortingOrder = 1;
                    var mats = rr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = KirMalzemesi(mats[i]);
                    rr.sharedMaterials = mats;
                }
                kirParseller.Add(inst);
            }
            kirSonGuncelleme = -999f;
            KirGuncelle();
        }

        /// <summary>
        /// Çayır: şehrin çevresindeki boş çimenliğe (tarla, orman, nehir, yol ve şehir dışı) Blender'da modellenmiş çayır parçaları
        /// serpilir (ot kümeleri, çalılar, kır çiçekleri, kaya, sazlık). Yoğunluk geniş lekelerle değişir; parçalar birleştirilir
        /// (statik toplu çizim). Ot rengi mevsime göre değişir, kar yağınca beyazlar.
        /// </summary>
        void KirCimenKur(CityMap map)
        {
            if (kirCimenler == null || kirCimenler.Length == 0) return;
            float menzil = GrafikKalitesi.Seviye == GrafikKalitesi.Yuksek ? 24f : GrafikKalitesi.Seviye == GrafikKalitesi.Orta ? 16f : 9f;
            var parent = new GameObject("Çayır (3D)");
            parent.transform.SetParent(root, false);
            var rnd = new System.Random(5150);
            int n = 0;
            for (float x = -menzil; x < CityMap.Width + menzil; x += 2f)
                for (float y = -menzil; y < CityMap.Height + menzil; y += 2f)
                {
                    var p = new Vector2(x + (float)rnd.NextDouble() * 1.4f - 0.7f, y + (float)rnd.NextDouble() * 1.4f - 0.7f);
                    // Şehrin içi (kır kareleri hariç), tarlalar, nehir ve kır yolları
                    int tx = Mathf.RoundToInt(p.x), ty = Mathf.RoundToInt(p.y);
                    bool icerde = tx >= -1 && ty >= -1 && tx <= CityMap.Width && ty <= CityMap.Height;
                    if (icerde && !KirKaresi(tx, ty)) continue;
                    if (map.KirdaDolu(p, 1.0f)) continue;
                    // Yoğunluk lekeli: bazı yerler gür, bazıları seyrek
                    float yog = Mathf.PerlinNoise(p.x * 0.09f + 3.7f, p.y * 0.09f + 8.1f);
                    if (rnd.NextDouble() > 0.15 + yog * 0.75) continue;
                    var prefab = kirCimenler[rnd.Next(kirCimenler.Length)];
                    if (prefab.name.Contains("sazlik") && rnd.NextDouble() < 0.7) prefab = kirCimenler[rnd.Next(kirCimenler.Length)];   // sazlık nadir
                    var inst = Instantiate(prefab, parent.transform, false);
                    inst.transform.localRotation = Quaternion.Euler(0f, 0f, (float)rnd.NextDouble() * 360f) * Quaternion.Euler(-90f, 0f, 0f);
                    inst.transform.localScale = Vector3.one;
                    foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
                    var rs = inst.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) { Destroy(inst); continue; }
                    var b = CombinedBounds(rs);
                    float s = (1.6f + (float)rnd.NextDouble() * 0.6f) / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.y));
                    inst.transform.localScale = Vector3.one * s;
                    b = CombinedBounds(rs);
                    inst.transform.position += new Vector3(p.x - b.center.x, p.y - b.center.y, 0.008f - b.max.z);
                    foreach (var rr in rs)
                    {
                        rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // alçak: gölgesi görünmez, maliyeti yüksek
                        rr.receiveShadows = true;
                        rr.sortingOrder = 1;
                        var mats = rr.sharedMaterials;
                        for (int i = 0; i < mats.Length; i++) mats[i] = KirMalzemesi(mats[i]);
                        rr.sharedMaterials = mats;
                    }
                    n++;
                }
            if (n > 0) StaticBatchingUtility.Combine(parent);
            kirParseller.Add(parent);   // düz harita görünümünde parsellerle birlikte gizlenir
        }

        static bool KirKaresi(int x, int y)
        {
            if (x < 0 || y < 0 || x >= CityMap.Width || y >= CityMap.Height) return true;
            return CityMap.ZoneOf(x, y) == Zone.Country && !CityMap.RoadAt(x, y);
        }

        /// <summary>Tarlaya parsel türü: tohumlu, ağırlıklı (her oyunda aynı tarlada aynı tür).</summary>
        static GameObject KirSec(int fi)
        {
            float toplam = 0f;
            foreach (var t in KirTurAgirlik) toplam += t.agirlik;
            var rnd = new System.Random(fi * 7331 + 17);
            float x = (float)rnd.NextDouble() * toplam;
            string on = KirTurAgirlik[0].on;
            foreach (var t in KirTurAgirlik) { if (x < t.agirlik) { on = t.on; break; } x -= t.agirlik; }
            var aday = new List<GameObject>();
            foreach (var p in kirAlanlari) if (p.name.StartsWith(on)) aday.Add(p);
            if (aday.Count == 0) return kirAlanlari[rnd.Next(kirAlanlari.Length)];
            return aday[rnd.Next(aday.Count)];
        }

        /// <summary>"Kir..." malzemesinin parsellere özel kopyası (asıl malzeme dosyası değişmesin, git'te değişmiş görünmesin).</summary>
        static Material KirMalzemesi(Material m)
        {
            if (m == null) return null;
            string etiket = m.name.Split('_')[0];
            if (!etiket.StartsWith("Kir")) return m;
            if (kirKopya.TryGetValue(m, out var k) && k != null) return k;
            k = new Material(m) { name = m.name + " (mevsim)" };
            Color asil = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            kirKopya[m] = k;
            kirBoyali.Add((k, etiket, asil));
            return k;
        }

        /// <summary>Mevsim, ıslaklık ve kar: parsel malzemeleri ve kır zemini birkaç saniyede bir güncellenir.</summary>
        void KirGuncelle()
        {
            // Düz harita görünümünde 3D parseller gizlenir (binalar gibi)
            bool goster = MapCamera.I == null || MapCamera.I.Tilt01 > 0.01f;
            if (goster != kirGorunur)
            {
                kirGorunur = goster;
                foreach (var p in kirParseller) if (p != null) p.SetActive(goster);
            }
            if (Time.unscaledTime - kirSonGuncelleme < 1.5f) return;
            kirSonGuncelleme = Time.unscaledTime;
            var map = CityMap.I;
            if (map == null || GameClock.I == null) return;

            var agirlik = KirDurum.Agirliklar();
            // Hava kar örtüsünü en fazla ~0,55'e çıkarır: tarlalar da şehir gibi 0–1 aralığında beyazlasın
            float kar = Mathf.Clamp01(map.KarOrtusu / 0.5f), islak = map.Islaklik;

            // Zemin dokusu: en ağır mevsim; ıslakken koyulaşır
            int en = KirDurum.EnYakin();
            if (en != kirDokuDurum && kirMalzeme != null)
            {
                kirDokuDurum = en;
                if (kirDokular[en] == null)
                    kirDokular[en] = KirDokusu(map, -KirPay - 0.5f, -KirPay - 0.5f, CityMap.Width + 2f * KirPay, CityMap.Height + 2f * KirPay, en);
                KirDokuAta(kirDokular[en]);
            }
            if (kirMalzeme != null && kirMalzeme.HasProperty("_BaseColor"))
                kirMalzeme.SetColor("_BaseColor", Color.white * Mathf.Lerp(1f, 0.8f, islak));

            foreach (var (m, etiket, asil) in kirBoyali)
            {
                if (m == null) continue;
                Color c = KirPalet.Renk(etiket, asil, agirlik);
                float zeminMi = KirPalet.Zemin(etiket);
                c *= Mathf.Lerp(1f, zeminMi > 0.5f ? 0.76f : 0.88f, islak);           // ıslak toprak ve çimen koyulaşır
                // Kar önce düz tarla ve çayırı örter (sürülmüş toprakta bile sıralar karın altında kaybolur), ağaç ve çit daha az tutar
                float tut = KirPalet.KarTutar(etiket);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(kar * (0.7f + tut * 1.1f) - (1f - tut) * 0.3f)) * Mathf.Max(tut, 0.15f);
                c = Color.Lerp(c, new Color(0.92f, 0.94f, 0.97f), k);
                c.a = 1f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }

        // ---------- Şehir: mevsim renkleri ve kar birikmesi ----------
        // Çatı, çim, kaldırım, ağaç gibi malzemeler çalışma anında kopyalanır (asıl .mat dosyaları değişmez).
        // Kar yağdıkça kar örtüsü oranında çatılar, kubbeler, çimler ve kaldırımlar beyazlar; yollar trafikten dolayı az tutar.
        // Ağaçlar ve çimen mevsime göre renk değiştirir: ilkbaharda taze yeşil, yazın koyu, kurakta sararır, sonbaharda
        // sarı-turuncu-kızıl, kışın soluk kahve.
        static readonly Dictionary<(Material, int), Material> mevsimKopya = new Dictionary<(Material, int), Material>();
        static readonly HashSet<Material> mevsimKopyalari = new HashSet<Material>();
        static readonly List<(Material m, string etiket, Color asil, int tohum)> mevsimBoyali = new List<(Material, string, Color, int)>();
        float sehirSonGuncelleme = -99f;

        static Material SehirMalzemesi(Material m, int cesit)
        {
            if (m == null || mevsimKopyalari.Contains(m)) return m;
            var adParca = m.name.Split('_');
            // "Pastel_Yaprak_8fbf7a" (Bina Atölyesi): etiket ikinci parça; yaprak mevsimle değişir, çatı ve taş kar tutar
            string etiket = adParca[0] == "Pastel" && adParca.Length > 2 ? adParca[1] : adParca[0];
            if (etiket.StartsWith("Kir")) return m;
            bool bitki = SehirMevsim.Bitki(etiket);
            if (!bitki && SehirMevsim.KarTutar(etiket) <= 0f) return m;
            int anahtar = bitki ? cesit : 0;
            if (mevsimKopya.TryGetValue((m, anahtar), out var k) && k != null) return k;
            k = new Material(m) { name = m.name };   // ad aynı kalır: gece ışıkları ve vurgu ada bakar
            Color asil = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            mevsimKopya[(m, anahtar)] = k;
            mevsimKopyalari.Add(k);
            mevsimBoyali.Add((k, etiket, asil, anahtar));
            return k;
        }

        /// <summary>Şehirdeki bütün parçalara mevsim kopyalarını takar (kurulumda ve her yeniden boyamada).</summary>
        void SehirMevsimTak()
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                bool degisti = false;
                var p = r.transform.position;
                int cesit = Mathf.Abs(Mathf.RoundToInt(p.x * 3.1f) * 7 + Mathf.RoundToInt(p.y * 2.3f) * 13) % 3;
                for (int i = 0; i < mats.Length; i++)
                {
                    var y = SehirMalzemesi(mats[i], cesit);
                    if (y != mats[i]) { mats[i] = y; degisti = true; }
                }
                if (degisti) r.sharedMaterials = mats;
            }
            sehirSonGuncelleme = -99f;
        }

        void SehirMevsimGuncelle()
        {
            if (Time.unscaledTime - sehirSonGuncelleme < 2f) return;
            sehirSonGuncelleme = Time.unscaledTime;
            var map = CityMap.I;
            if (map == null || GameClock.I == null || mevsimBoyali.Count == 0) return;
            var agirlik = KirDurum.Agirliklar();
            // Bina Atölyesi ağaçları: kışın yaprak döken taçlar kırpılır, dallarda kar görünür (Pastel.shader: _Dokum)
            Shader.SetGlobalFloat("_PastelKis", agirlik[KirDurum.Kis] > 0.6f ? 1f : 0f);
            // Hava kar örtüsünü en fazla 0,55'e çıkarır: burada 0–1'e açılır (tam kar = tam beyaz çatı)
            float kar = Mathf.Clamp01(map.KarOrtusu / 0.5f), islak = map.Islaklik;
            KarKatmaniGuncelle(kar);
            var karRenk = new Color(0.93f, 0.95f, 0.98f);
            foreach (var (m, etiket, asil, tohum) in mevsimBoyali)
            {
                if (m == null) continue;
                Color c = SehirMevsim.Mevsim(etiket, asil, agirlik, tohum);
                if (SehirMevsim.Islanir(etiket)) c *= Mathf.Lerp(1f, 0.78f, islak);   // ıslak asfalt ve taş koyulaşır
                // Kar önce en çok tutan yüzeylerde görünür: az karda çatı ve çim beyazlar, yol ancak yoğun karda
                float tut = SehirMevsim.KarTutar(etiket);
                // tutan yüzey erken beyazlar (çatı yarım karda bile çoğunlukla beyaz), yol ancak yoğun karda
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(kar * (0.6f + tut * 1.2f) - (1f - tut) * 0.35f)) * tut;
                c = Color.Lerp(c, karRenk, k);
                c.a = asil.a;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
        }

        // ---------- Kar katmanı: yukarı bakan her yüzeyin üstünde biriken kar ----------
        // Kar yağınca her parçanın (bina, ağaç, bank, araba, çit…) yukarı bakan yüzeylerinden ince bir "kar kabuğu" mesh'i
        // üretilir ve biraz yukarı kaydırılır. Duvarlar renkli kalır, çatı, saçak, pencere pervazı, ağaç tepesi beyazlar.
        // Katman yarı saydam başlar, kar arttıkça koyulaşır; az karda yalnızca düz yüzeyler, yoğun karda eğimli çatılar da örtülür.
        // Mesh'ler ilk karda bir kez (birkaç karede parça parça) üretilir; aynı modeller aynı mesh'i paylaşır.
        static readonly Dictionary<(Mesh, int, int, int, int), (Mesh duz, Mesh egik)> karMeshOnbellek = new Dictionary<(Mesh, int, int, int, int), (Mesh, Mesh)>();
        readonly List<GameObject> karDuz = new List<GameObject>(), karEgik = new List<GameObject>();
        Renderer[] karKuyruk;
        int karSira;
        Transform karKoku;
        bool karKuruldu;
        Material karMalzeme;
        float karSonAlfa = -1f;
        bool karDuzGorunur, karEgikGorunur;

        static readonly Vector3 DunyaYukari = Vector3.back;   // −Z yukarı

        /// <summary>Kar tutmayan malzemeler: siyah çizgi, cam, ışık, su, yazı.</summary>
        static bool KarTutmaz(Material m)
        {
            if (m == null) return true;
            string n = m.name;
            return n.StartsWith("Cizgi") || n.StartsWith("Kesit") || n.StartsWith("Pastel_Cam") || n.StartsWith("Pastel_Vitrin") || n.StartsWith("Pastel_Asfalt") || n.StartsWith("Pastel_Isik") || n.StartsWith("Pastel_Cizgi") || n.StartsWith("Pastel_Yazi") || n.StartsWith("Cam") || n.StartsWith("Vitrin") || n.StartsWith("Vitray") || n.StartsWith("Isik") ||
                   n.StartsWith("Simsek") || n.StartsWith("Bulut") || n.StartsWith("Yazi") || n.StartsWith("Su_") || n.StartsWith("PSu") ||
                   n.StartsWith("KirSu") || n.StartsWith("OfisCam") || n.StartsWith("Kar_") || n.StartsWith("Reklam") || n.StartsWith("Pano") ||
                   // Yollar: trafik karı eritir, kuryeler yolu görebilsin (yol yalnızca hafif beyazlar)
                   n.StartsWith("YAsfalt") || n.StartsWith("TAsfalt") || n.StartsWith("Yol_") || n.StartsWith("YolKoyu") ||
                   n.StartsWith("Serit") || n.StartsWith("YSerit") || n.StartsWith("TSerit") || n.StartsWith("YSari") || n.StartsWith("TSari") ||
                   n.StartsWith("YBeyaz") || n.StartsWith("TBeyaz") || n.StartsWith("Rogar") || n.StartsWith("Tramvay");
        }

        void KarKatmaniGuncelle(float kar)
        {
            if (root == null) return;
            if (karKoku != null && karKoku.parent != root) { karKoku = null; karKuruldu = false; }   // şehir yeniden kuruldu
            if (karKoku == null) { karDuz.Clear(); karEgik.Clear(); karKuyruk = null; karKuruldu = false; karDuzGorunur = karEgikGorunur = false; karSonAlfa = -1f; }

            bool gerek = kar > 0.04f;
            if (!gerek && !karKuruldu && karKuyruk == null) return;

            // İlk kar: kuyruğu doldur ve birkaç karede üret (oyun takılmasın)
            if (gerek && !karKuruldu && karKuyruk == null && (MapCamera.I == null || MapCamera.I.Tilt01 > 0.95f))
            {
                karKoku = new GameObject("KarKatmani").transform;
                karKoku.SetParent(root, false);
                karKuyruk = root.GetComponentsInChildren<Renderer>(true);
                karSira = 0;
            }
            if (karKuyruk != null)
            {
                int son = Mathf.Min(karKuyruk.Length, karSira + 600);
                for (; karSira < son; karSira++) KarKabuguEkle(karKuyruk[karSira]);
                if (karSira >= karKuyruk.Length) { karKuyruk = null; karKuruldu = true; karSonAlfa = -1f; karDuzGorunur = karEgikGorunur = false; }
                else sehirSonGuncelleme = Time.unscaledTime - 1.95f;   // sonraki karede devam
            }
            if (!karKuruldu || karMalzeme == null) return;

            // Az kar: düz yüzeyler, ince ve yarı saydam. Yoğun kar: eğimli çatılar da, tamamen beyaz.
            bool duz = kar > 0.04f, egik = kar > 0.4f;
            if (duz != karDuzGorunur) { karDuzGorunur = duz; foreach (var g in karDuz) if (g != null) g.SetActive(duz); }
            if (egik != karEgikGorunur) { karEgikGorunur = egik; foreach (var g in karEgik) if (g != null) g.SetActive(egik); }
            float alfa = Mathf.Clamp01(Mathf.InverseLerp(0.04f, 0.75f, kar));
            alfa = Mathf.Lerp(0.35f, 1f, alfa);
            if (Mathf.Abs(alfa - karSonAlfa) > 0.01f)
            {
                karSonAlfa = alfa;
                var c = new Color(0.94f, 0.96f, 0.99f, alfa);
                if (karMalzeme.HasProperty("_BaseColor")) karMalzeme.SetColor("_BaseColor", c);
                if (karMalzeme.HasProperty("_Color")) karMalzeme.SetColor("_Color", c);
            }
        }

        /// <summary>Kar katmanı malzemesi: şehirdeki bir malzemenin (URP Lit) yarı saydam beyaz kopyası.</summary>
        Material KarMalzemesiYap(Material ornek)
        {
            // Hazır saydam malzeme (Resources/Gorsel): oyun derlemesinde URP'nin saydam Lit çeşidi atılmasın diye diskte durur
            var hazir = Resources.Load<Material>("Gorsel/Kar_katmani");
            if (hazir != null) return new Material(hazir) { name = "Kar_katmani" };
            var m = new Material(ornek) { name = "Kar_katmani" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", Color.black); m.DisableKeyword("_EMISSION"); }
            m.SetColor("_BaseColor", new Color(0.94f, 0.96f, 0.99f, 0.35f));
            return m;
        }

        void KarKabuguEkle(Renderer r)
        {
            if (r == null || r is ParticleSystemRenderer || r is SkinnedMeshRenderer || r.isPartOfStaticBatch) return;
            if (karKoku != null && r.transform.IsChildOf(karKoku)) return;
            if (CatiKaldir.KarYok(r.transform)) return;   // iç mekân (çatı kalkınca görünen) ve canlı figürler: içeriye kar yağmaz
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = mf.sharedMesh;
            if (!mesh.isReadable) return;
            var mats = r.sharedMaterials;
            int maske = 0;
            for (int i = 0; i < mats.Length && i < mesh.subMeshCount && i < 30; i++)
            {
                if (KarTutmaz(mats[i])) continue;
                maske |= 1 << i;
                if (karMalzeme == null && mats[i].HasProperty("_BaseColor")) karMalzeme = KarMalzemesiYap(mats[i]);
            }
            if (maske == 0 || karMalzeme == null) return;

            // Dünya yukarısının modelin kendi uzayındaki yönü (ölçek dahil) ve 1 cm'lik kaydırmanın yerel boyu
            var M = r.transform.localToWorldMatrix;
            var Mi = M.inverse;
            Vector3 d = Mi.MultiplyVector(DunyaYukari);
            if (d.sqrMagnitude < 1e-12f) return;
            float olcek = M.MultiplyVector(d.normalized).magnitude;
            if (olcek < 1e-6f) return;
            float kalinlik = 0.006f / olcek;            // ~6 cm (1 birim = 10 m)
            d.Normalize();
            var anahtar = (mesh, Mathf.RoundToInt(d.x * 32f), Mathf.RoundToInt(d.y * 32f), Mathf.RoundToInt(d.z * 32f) * 1000 + Mathf.RoundToInt(Mathf.Log(kalinlik) * 8f), maske);
            if (!karMeshOnbellek.TryGetValue(anahtar, out var ikili))
            {
                ikili = (KarMesh(mesh, maske, d, kalinlik, Mi, M, 0.82f, 2f), KarMesh(mesh, maske, d, kalinlik, Mi, M, 0.42f, 0.82f));
                karMeshOnbellek[anahtar] = ikili;
            }
            if (ikili.duz != null) karDuz.Add(KarParcasi(r, ikili.duz, "duz"));
            if (ikili.egik != null) karEgik.Add(KarParcasi(r, ikili.egik, "egik"));
        }

        GameObject KarParcasi(Renderer r, Mesh m, string ad)
        {
            var go = new GameObject("Kar_" + ad);
            go.transform.SetParent(r.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = karMalzeme;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            go.SetActive(false);
            return go;
        }

        /// <summary>Yukarı bakan üçgenlerden (eğim [min, max) aralığında) kaydırılmış kar kabuğu.</summary>
        static Mesh KarMesh(Mesh kaynak, int maske, Vector3 yukari, float kalinlik, Matrix4x4 Mi, Matrix4x4 M, float min, float max)
        {
            var v = kaynak.vertices;
            var yeniV = new List<Vector3>();
            var yeniN = new List<Vector3>();
            var tri = new List<int>();
            var MiT = Mi.transpose;
            for (int s = 0; s < kaynak.subMeshCount && s < 30; s++)
            {
                if ((maske & (1 << s)) == 0) continue;
                if (kaynak.GetTopology(s) != MeshTopology.Triangles) continue;
                var t = kaynak.GetTriangles(s);
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-14f) continue;
                    // Dünya uzayındaki eğim (ölçek dahil): normal ters-devrik matrisle çevrilir
                    Vector3 dn = MiT.MultiplyVector(n).normalized;
                    float yukariMi = Vector3.Dot(dn, DunyaYukari);
                    if (yukariMi < min || yukariMi >= max) continue;
                    Vector3 kay = yukari * kalinlik;
                    int k = yeniV.Count;
                    yeniV.Add(a + kay); yeniV.Add(b + kay); yeniV.Add(c + kay);
                    var ln = n.normalized;
                    yeniN.Add(ln); yeniN.Add(ln); yeniN.Add(ln);
                    tri.Add(k); tri.Add(k + 1); tri.Add(k + 2);
                }
            }
            if (tri.Count == 0) return null;
            var m = new Mesh { name = kaynak.name + "_kar" };
            if (yeniV.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(yeniV);
            m.SetNormals(yeniN);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Binadan yola doğru birim vektör.</summary>
        static Vector2 FrontDir(CityMap map, Vector2Int t)
        {
            if (t.y > 0 && map.IsRoad(t.x, t.y - 1)) return Vector2.down;
            if (t.y < CityMap.Height - 1 && map.IsRoad(t.x, t.y + 1)) return Vector2.up;
            if (t.x > 0 && map.IsRoad(t.x - 1, t.y)) return Vector2.left;
            if (t.x < CityMap.Width - 1 && map.IsRoad(t.x + 1, t.y)) return Vector2.right;
            return Vector2.zero;
        }

        /// <summary>Kareye ağaç koyar (paketin ağaçları ya da yeşil kutular). center verilirse ağaçlar onun etrafında toplanır.</summary>
        void AddTrees(Vector2Int tile, System.Random rng, int n, bool packTreesOk, Vector2 center = default, float spread = 0.3f)
        {
            for (int i = 0; i < n; i++)
            {
                var off = center + new Vector2((float)rng.NextDouble() * spread * 2f - spread, (float)rng.NextDouble() * spread * 2f - spread);
                float h = 0.25f + (float)rng.NextDouble() * 0.25f;
                if (packTreesOk)
                {
                    var a = packTrees[rng.Next(packTrees.Length)];
                    // çalı ağaç boyuna büyütülmesin (dev çalılar); ağaç türüne göre boy: kavak, çınar uzun, genç ağaç kısa
                    if (a.name.StartsWith("Cali_")) h = 0.05f + (float)rng.NextDouble() * 0.07f;
                    else if (a.name.Contains("kavak") || a.name.Contains("cinar")) h += 0.25f;
                    else if (a.name.Contains("genc") || a.name.Contains("meyve")) h *= 0.7f;
                    AddPrefab(a, tile, 0f, (float)rng.NextDouble() * 360f, null, off, h + (a.name.StartsWith("Cali_") ? 0f : 0.15f));
                }
                else
                {
                    var c = Color.Lerp(new Color(0.16f, 0.42f, 0.22f), new Color(0.28f, 0.55f, 0.25f), (float)rng.NextDouble());
                    AddBox(tile, 0.22f, h, c, null, false, off, false);
                }
            }
        }

        Transform Holder(string name, Vector2Int tile, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(tile.x + offset.x, tile.y + offset.y, 0f);
            return go.transform;
        }

        // ---------- Paket modeli ----------

        /// <summary>Prefabı kareye sığdırır (footprint) ya da verilen boya ölçekler, tabanı yere oturtur, yüzünü döndürür.</summary>
        void AddPrefab(GameObject prefab, Vector2Int tile, float footprint, float faceAngle, Shop shop,
                       Vector2 offset = default, float targetHeight = 0f, bool registerHeight = true, bool ownModel = false)
        {
            if (prefab == null) return;
            // Şehir yalnız atölye binalarıyla kuruluyorsa (yüzlerce şablon): kurulmamış şablonun yerine önce kutu, model arka
            // planda (kameraya yakın olan önce) kurulunca yerine geçer. Dükkân binaları hemen kurulur (canlı dükkân bağlanır).
            if (shop == null && AtolyeAyari.YalnizAtolye && AtolyeUretici.Kurulmadi(prefab))
            {
                YerTutucu(prefab, tile, footprint, faceAngle, offset, targetHeight, registerHeight, ownModel);
                return;
            }
            AtolyeUretici.Hazirla(prefab);   // atölye şablonu: mesh'leri ilk kullanımda kurulur
            // Organik şehirde binalar yola dönük, açıları 90'ın katı değil: döndürülmüş kare komşuya taşmasın diye küçülür
            if (CityMap.Organik && footprint > 0f)
            {
                float r = faceAngle * Mathf.Deg2Rad;
                float kucul = Mathf.Abs(Mathf.Cos(r)) + Mathf.Abs(Mathf.Sin(r));
                // yalnız atölye şehrinde daha az küçülür (binalar arsayı doldursun; köşeler komşuya biraz değebilir)
                footprint /= AtolyeAyari.YalnizAtolye && ArsaOlculu(prefab.name) ? Mathf.Sqrt(kucul) : kucul;
            }
            var holder = Holder(prefab.name, tile, offset);
            var inst = Instantiate(prefab, holder, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            // Kendi binalarımızın önü hep +Z (Blender'da −Y): paket için olan cephe düzeltmesi uygulanmaz
            float duzeltme = ownModel ? 0f : FrontOffset;
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, faceAngle + duzeltme) * Quaternion.Euler(-90f, 0f, 0f);

            foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
            foreach (var rb in inst.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
            foreach (var l in inst.GetComponentsInChildren<Light>()) Destroy(l);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder.gameObject); return; }
            foreach (var r in renderers)
            {
                // Binalar gölge düşürür ve alır (güneş: Gorsel3D). 2D Renderer'da bu ayarların etkisi yok.
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                r.sortingOrder = 5;
            }

            var b = CombinedBounds(renderers);
            float horiz = Mathf.Max(b.size.x, b.size.y);
            // Bölge modelleri (Bolge_*) hep 14 m arsaya göre çizildi, arsa levhası yok: sabit ölçek (bütün binalar aynı
            // metre ölçüsünde, bina boyları birbirine göre tutarlı) ve arsanın ortası (model kökü) karenin ortasına
            bool bolge = ArsaOlculu(prefab.name);
            // Blok binaları tam 10 m kare: ölçek sabit (1 m = 0,1 kare), çizgi kabuğu yüzünden küçülüp aralarında boşluk kalmasın
            float s = targetHeight > 0f ? targetHeight / Mathf.Max(0.001f, b.size.z)
                    : prefab.name.StartsWith("Blok_") ? footprint * 0.1f : bolge ? footprint / (AtolyeSehirOlcegi(prefab.name) ? 12.5f : 14f) : footprint / Mathf.Max(0.001f, horiz);
            inst.transform.localScale = Vector3.one * s;
            b = CombinedBounds(renderers);
            if (bolge) inst.transform.position += new Vector3(0f, 0f, -b.max.z);
            else inst.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.center.y, -b.max.z);
            float height = b.size.z;
            // Kendi binalarımızın arsa levhasının üstü, yol kaldırımıyla aynı seviyede olsun (bina ne gömük ne havada)
            if (ownModel && !bolge) inst.transform.position += new Vector3(0f, 0f, OwnLotDepth * s + OwnSidewalkTop);
            // Araziye otur: tepelerde yukarıda, nehir kıyısında aşağıda (şehrin içi düz: 0)
            if (CityMap.Organik) inst.transform.position += new Vector3(0f, 0f, Arazi.Z(holder.position.x, holder.position.y));
            // Aynı model her karede biraz farklı renkte (duvar, kapı, panjur, çatı): şehir tekrar eden kopyalar gibi görünmesin
            int seed = ownModel ? ((tile.x + 101) * 73856093 ^ (tile.y + 37) * 19349663 ^ (int)(offset.x * 1000f)) & 0x7fffffff : -1;

            float mul = ownModel && targetHeight <= 0f ? (AtolyeSehirOlcegi(prefab.name) ? AtolyeBoyCarpani : OwnHeightBoost) : 1f;
            pieces.Add(new Piece { t = holder, height = height * mul, prefab = true, tile = tile, shop = shop, varSeed = seed, heightMul = mul });
            // bir arsada birden çok şerit olabilir: en yüksek olanı
            if (targetHeight <= 0f && registerHeight) heightByTile[tile] = heightByTile.TryGetValue(tile, out float eskiH) ? Mathf.Max(eskiH, height * mul) : height * mul;
            if (AgacSalinimi.AgacMi(prefab)) AgacSalinimi.Uygula(renderers);   // rüzgârda salınım (renklendirmeden önce: kopyalar asıl sayılsın)
            Tint(renderers, !CityMap.I.IsUnlocked(tile), seed);
            // İçi modelli binalar ("_Cati" çocuğu olanlar, ör. mahalle bakkalı): kamera yaklaşınca çatı kalkar
            // Bakkalın canlı hâli (BakkalCanli): müşteriler, kasiyer, sipariş poşeti, kepenk, tedarik kamyoneti
            // Yalnız sipariş alınan yerler (dükkân bağlı bina): konut, kamu ve sanayi binalarının çatısı kalkmaz, içi gösterilmez
            if (ownModel && shop != null)
            {
                var catiKaldir = CatiKaldir.Tak(inst);
                if (catiKaldir != null)
                {
                    // Bina Atölyesi binaları (kökünde AtolyePlani): genel canlı bina; mahalle bakkalı: kendi canlı sistemi
                    if (AtolyeAyari.AtolyeMi(prefab.name)) DukkanCanli.Tak(inst, shop, tile, catiKaldir);
                    else BakkalCanli.Tak(inst, shop, catiKaldir);
                }
            }
        }

        // ---------- Renk: ortak malzeme kopyaları (performans) ----------
        // Eskiden her binanın her malzemesine ayrı "property block" veriliyordu. Property block'lu parçalar SRP Batcher'a
        // giremez: binlerce parça tek tek çiziliyordu (gölge geçişlerinde bir kez daha). Artık aynı renge boyanan bütün
        // parçalar aynı malzeme kopyasını paylaşır; renk çeşitliliği 16 tohumla sınırlı, kopya sayısı az kalır.
        const int VaryCount = 16;
        static readonly Dictionary<(Material, int), Material> tintCache = new Dictionary<(Material, int), Material>();
        static readonly Dictionary<Renderer, Material[]> origMats = new Dictionary<Renderer, Material[]>();

        /// <summary>Parçanın özgün (gündüz, boyanmamış) malzemeleri. Gece ışıkları ve boyama aynı listeden başlar.</summary>
        static Material[] Originals(Renderer r)
        {
            if (!origMats.TryGetValue(r, out var o))
            {
                // "Oyunda pastel zemin": yol, kaldırım, çim malzemeleri pastel karşılıklarıyla (AtolyeAyari.PastelZeminCevir)
                o = AtolyeAyari.PastelZeminCevir(r.sharedMaterials);
                origMats[r] = o;
            }
            return o;
        }

        /// <summary>Malzemenin belirli renkteki ortak kopyası (anahtar: özgün malzeme + tohum/karartma).</summary>
        static Material Tinted(Material m, int key, Color c)
        {
            if (tintCache.TryGetValue((m, key), out var t) && t != null) return t;
            t = new Material(m) { name = m.name };   // ad aynı kalır: gece ışıkları ve vurgu ada bakar
            if (t.HasProperty("_BaseColor")) t.SetColor("_BaseColor", c);
            if (t.HasProperty("_Color")) t.SetColor("_Color", c);
            tintCache[(m, key)] = t;
            return t;
        }

        static void ClearTintCache()
        {
            foreach (var t in tintCache.Values) if (t != null) Destroy(t);
            tintCache.Clear();
            origMats.Clear();
            foreach (var m in mevsimKopyalari) if (m != null) Destroy(m);
            mevsimKopya.Clear();
            mevsimKopyalari.Clear();
            mevsimBoyali.Clear();
        }

        static void TintVaried(Renderer[] rs, bool dark, int seed)
        {
            int vs = seed % VaryCount;
            foreach (var r in rs)
            {
                r.SetPropertyBlock(null);
                var orig = Originals(r);
                var mats = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    var m = orig[i];
                    mats[i] = m;
                    if (m == null || m.name.StartsWith("Cizgi")) continue;   // siyah çizgi hep siyah
                    Color c0 = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                    Color c = Vary(c0, m.name, vs);
                    bool varied = c != c0;
                    if (!varied && !dark) continue;                           // rengi değişmeyen: özgün malzeme
                    if (dark) c = CityMap.LockedTint(c);
                    mats[i] = Tinted(m, (varied ? vs + 1 : 0) + (dark ? 100 : 0), c);
                }
                r.sharedMaterials = mats;
            }
        }

        static float Rand01(int seed, int k) => ((seed * 73856093 ^ k * 19349663 ^ 0x5bd1e995) & 0xffff) / 65535f;

        /// <summary>Malzeme adına göre renk oynatma: duvar/kapı/panjur belirgin, çatı/baca/taban hafif, diğerleri aynı.</summary>
        static Color Vary(Color c, string name, int seed)
        {
            float hue, sat, val;
            int k = name.GetHashCode() & 0xff;
            if (name.StartsWith("Duvar") || name.StartsWith("Kapi") || name.StartsWith("Panjur")) { hue = 0.10f; sat = 0.30f; val = 0.12f; }
            else if (name.StartsWith("Cati") || name.StartsWith("Baca") || name.StartsWith("Taban") || name.StartsWith("Suslu")) { hue = 0.03f; sat = 0.15f; val = 0.08f; }
            else return c;
            Color.RGBToHSV(c, out float h, out float sv, out float v);
            h = Mathf.Repeat(h + (Rand01(seed, k + 1) - 0.5f) * hue, 1f);
            sv = Mathf.Clamp01(sv * (1f + (Rand01(seed, k + 2) - 0.5f) * 2f * sat));
            v = Mathf.Clamp01(v * (1f + (Rand01(seed, k + 3) - 0.5f) * 2f * val));
            var o = Color.HSVToRGB(h, sv, v);
            o.a = c.a;
            return o;
        }

        static Bounds CombinedBounds(Renderer[] rs)
        {
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>
        /// Kilitli bölgedeki paket binalarını karart (malzemeyi bozmadan, property block ile).
        /// seed ≥ 0 ise (kendi binalarımız) her malzemeye ayrı renk verilir: duvar, kapı, panjur, çatı tonu karede kareye değişir.
        /// </summary>
        static void Tint(Renderer[] rs, bool dark, int seed = -1)
        {
            if (seed >= 0) { TintVaried(rs, dark, seed); return; }
            foreach (var r in rs)
            {
                r.SetPropertyBlock(null);
                var orig = Originals(r);
                if (!dark) { r.sharedMaterials = orig; continue; } // malzemenin kendi rengi
                // Kilitli bölge: her malzeme kendi renginin soluk hâli (düz gri değil)
                var mats = new Material[orig.Length];
                for (int i = 0; i < orig.Length; i++)
                {
                    var m = orig[i];
                    mats[i] = m;
                    if (m == null || m.name.StartsWith("Cizgi") || m.name.StartsWith("IsikHavuz")) continue;
                    var c = CityMap.LockedTint(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color);
                    mats[i] = Tinted(m, 100, c);
                }
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Grafik kalitesi: Düşük'te yol ve zemin parçaları gölge düşürmez (gölge geçişindeki çizim sayısı azalır).</summary>
        public void ApplyQuality()
        {
            var mode = GrafikKalitesi.ZeminGolgesi ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var p in pieces)
            {
                if (!p.flat || p.t == null) continue;
                foreach (var r in p.t.GetComponentsInChildren<Renderer>(true))
                    if (r.shadowCastingMode != mode) r.shadowCastingMode = mode;
            }
        }

        // ---------- Yollar ve zemin ----------

        /// <summary>Ana cadde çizgileri: her ikinci blok yolu (dikeyde x = 8, 16 …; yatayda y = 4, 12 …), harita kenarı hariç.</summary>
        public static bool AvenueColumn(int x) => x % (CityMap.Block * 2) == 0 && x > 0 && x < CityMap.Width - 1;
        public static bool AvenueRow(int y) => y % (CityMap.Block * 2) == CityMap.Block && y > 0 && y < CityMap.Height - 1;
        bool AvenuesOn => avenueStraight != null && avenueJunction != null && avenueJunction.Count >= 8;

        /// <summary>Yön (0 kuzey, 1 batı, 2 güney, 3 doğu) kolunda cadde mi? Kuzey-güney kolu sütuna, doğu-batı satıra bağlı.</summary>
        static bool ArmAvenue(Vector2Int t, int dir) => (dir % 2 == 0) ? AvenueColumn(t.x) : AvenueRow(t.y);

        GameObject Junction(string ad) => avenueJunction != null && avenueJunction.TryGetValue(ad, out var p) ? p : null;

        /// <summary>Komşu yollara göre parça seçer: düz, köşe, T, dörtlü kavşak (ana caddelerde geniş sürümleri).</summary>
        void AddRoad(CityMap map, Vector2Int t, int order = 1)
        {
            // Harita kenarındaki yol karesi: şehir dışına devam eden kır yolu da komşu sayılır (kavşak doğru seçilsin)
            bool n = t.y < CityMap.Height - 1 ? map.IsRoad(t.x, t.y + 1) : CityMap.CountryRoadAt(t.x, t.y + 1);
            bool s = t.y > 0 ? map.IsRoad(t.x, t.y - 1) : CityMap.CountryRoadAt(t.x, t.y - 1);
            bool e = t.x < CityMap.Width - 1 ? map.IsRoad(t.x + 1, t.y) : CityMap.CountryRoadAt(t.x + 1, t.y);
            bool w = t.x > 0 ? map.IsRoad(t.x - 1, t.y) : CityMap.CountryRoadAt(t.x - 1, t.y);
            int count = (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (w ? 1 : 0);
            bool av = AvenuesOn;
            // Açılar Z ekseninde, saat yönünün tersine: 0 = kuzey, 90 = batı, 180 = güney, 270 = doğu.
            // k adım (90°) döndürünce kanonik yön d dünyada (d + k) % 4 olur.
            if (count >= 4)
            {
                bool cv = ArmAvenue(t, 0), ch = ArmAvenue(t, 1);
                if (av && cv && ch && Junction("Yol_kavsak_cc") != null) { AddFlat(Junction("Yol_kavsak_cc"), t, 0f, order); return; }
                if (av && (cv || ch) && Junction("Yol_kavsak_ca") != null) { AddFlat(Junction("Yol_kavsak_ca"), t, cv ? 0f : 90f, order); return; }
                AddFlat(roadCross, t, 0f, order);
                return;
            }
            if (count == 3 && roadT != null)
            {
                // Kanonik T: batı-kuzey-doğu (güney yok)
                int k = !s ? 0 : !e ? 1 : !n ? 2 : 3;
                if (av)
                {
                    bool bar = ArmAvenue(t, (1 + k) % 4), govde = ArmAvenue(t, k % 4);
                    var jp = (bar || govde) ? Junction("Yol_t_" + (bar ? "c" : "s") + (govde ? "c" : "s")) : null;
                    if (jp != null) { AddFlat(jp, t, k * 90f, order); return; }
                }
                AddFlat(roadT, t, k * 90f + RoadOffset(2), order);
                return;
            }
            if (count == 2 && !(n && s) && !(e && w) && roadCorner != null)
            {
                // Kanonik köşe: kuzey-doğu
                int k = (n && e) ? 0 : (n && w) ? 1 : (s && w) ? 2 : 3;
                if (av)
                {
                    bool ku = ArmAvenue(t, k % 4), do_ = ArmAvenue(t, (3 + k) % 4);
                    var jp = (ku || do_) ? Junction("Yol_kose_" + (ku ? "c" : "s") + (do_ ? "c" : "s")) : null;
                    if (jp != null) { AddFlat(jp, t, k * 90f, order); return; }
                }
                AddFlat(roadCorner, t, k * 90f + RoadOffset(1), order);
                return;
            }
            if (count >= 3) { AddFlat(roadCross, t, 0f, order); return; }
            // Düz: kanonik kuzey-güney
            float st = (e || w) && !(n || s) ? 90f : 0f;
            bool cadde = st == 0f ? AvenueColumn(t.x) : AvenueRow(t.y);
            if (av && cadde)
            {
                // Ana cadde: bulvar boyunca ağaçlı (her karede aynı), ama gökdelen ve eski şehir merkezinde, köprüde ve
                // nehir kenarında ağaçsız
                var c = avenueStraightTrees != null && AgacliOlur(map, t, st, true) ? avenueStraightTrees : avenueStraight;
                AddFlat(c, t, st, order);
                return;
            }
            var straight = roadStraight;
            if (roadStraightTrees != null && CityMap.InCity(t.x, t.y))
            {
                // Ağaçlı sokak: rastgele tek tük kare değil, sokağın tamamı. Yalnızca konut, park ve kampüs arasındaki sokaklar
                // (sık şehir merkezinde binalar kaldırıma yaslı, ağaç yok); köprüde ve nehir kenarında hiç ağaç yok.
                int hat = st == 0f ? t.x : t.y;
                if (AgacliOlur(map, t, st, false) && ((hat * 7 + 3) % 3) != 0) straight = roadStraightTrees;
            }
            AddFlat(straight, t, st + RoadOffset(0), order);
        }

        /// <summary>Düz yol karesine kaldırım ağacı uygun mu: iki yanındaki bölgelere, köprüye ve suya bakar.</summary>
        static bool AgacliOlur(CityMap map, Vector2Int t, float st, bool cadde)
        {
            if (CityMap.BridgeAt(t.x, t.y)) return false;
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                {
                    int x = t.x + dx, y = t.y + dy;
                    if (x < 0 || y < 0 || x >= CityMap.Width || y >= CityMap.Height) continue;
                    if (CityMap.WaterAt(x, y) || CityMap.BridgeAt(x, y) || CityMap.ZoneOf(x, y) == Zone.River) return false;
                }
            // Yolun iki yanı (kuzey-güney yolda doğu ve batı, doğu-batı yolda kuzey ve güney)
            var a = st == 0f ? new Vector2Int(t.x - 1, t.y) : new Vector2Int(t.x, t.y - 1);
            var b = st == 0f ? new Vector2Int(t.x + 1, t.y) : new Vector2Int(t.x, t.y + 1);
            return YanUygun(map, a, cadde) && YanUygun(map, b, cadde);
        }

        static bool YanUygun(CityMap map, Vector2Int p, bool cadde)
        {
            if (!map.InBounds(p)) return false;
            if (map.IsRoad(p.x, p.y)) return true;
            switch (CityMap.ZoneOf(p))
            {
                case Zone.Houses: case Zone.Park: case Zone.Campus: return true;
                case Zone.Mixed: return cadde;            // karma bölgede yalnızca geniş caddeler ağaçlı
                default: return false;                    // gökdelen, eski şehir, çarşı, meydan, AVM, sanayi, kır
            }
        }

        int atlikarincaSayisi, hobiSayisi;

        /// <summary>Park karesi: atlıkarınca en çok 2, hobi / oyun alanı en çok 6 kez; gerisi düzenli çim, çiçek, gezinti yolu.</summary>
        GameObject ParkKaresi(System.Random rng)
        {
            for (int deneme = 0; deneme < 8; deneme++)
            {
                var p = parkTiles[rng.Next(parkTiles.Length)];
                if (p.name.Contains("atlikarinca")) { if (atlikarincaSayisi >= 2 || rng.NextDouble() < 0.7) continue; atlikarincaSayisi++; }
                else if (p.name.Contains("hobi")) { if (hobiSayisi >= 6 || rng.NextDouble() < 0.4) continue; hobiSayisi++; }
                return p;
            }
            foreach (var p in parkTiles) if (p.name.Contains("duzenli") || p.name.Contains("gezinti")) return p;
            return parkTiles[0];
        }

        /// <summary>Düz parça: kareyi tam kaplar, yüzeyi zeminin hemen üstünde, yükselmez.</summary>
        void AddFlat(GameObject prefab, Vector2Int tile, float angle, int order = 1, Vector2 offset = default)
        {
            if (prefab == null) return;
            var holder = Holder(prefab.name, tile, offset);
            var inst = Instantiate(prefab, holder, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            inst.transform.localRotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(-90f, 0f, 0f);
            foreach (var col in inst.GetComponentsInChildren<Collider>()) Destroy(col);
            var renderers = inst.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(holder.gameObject); return; }
            foreach (var r in renderers)
            {
                // Yol ve park parçaları binaların gölgesini alır; üstlerindeki lamba, ağaç, bank da gölge düşürür
                r.shadowCastingMode = GrafikKalitesi.ZeminGolgesi ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.sortingOrder = order; // zemin karelerinin üstünde, binaların altında
            }
            var b = CombinedBounds(renderers);
            float boyut = Mathf.Max(b.size.x, b.size.y);
            // Kendi yol ve park parçalarımız 10 m karedir; kenardan taşan trafik ışığı, lamba ya da ışık halkası
            // parçanın sınırını büyütüp onu küçültmesin (yoksa yollar arasında boşluk kalıyor)
            if (IsOwnFlat(prefab.name) && boyut > 9.5f && boyut < 12f) boyut = 10f;
            float s = 1.01f / Mathf.Max(0.001f, boyut);
            inst.transform.localScale = Vector3.one * s;
            b = CombinedBounds(renderers);
            // Paket parçası: en üst yüzey zeminin hemen üstünde. Kendi parçalarımızda (lamba, trafik ışığı, ağaç var)
            // en üst nokta lambanın tepesi olur; onu hizalamak yolu zeminin altına gömüyordu. Onlarda asfalt yüzeyi (0,10 m) hizalanır.
            float z = IsOwnFlat(prefab.name) ? -b.max.z + OwnAsphalt * s - 0.002f : -0.002f - b.min.z;
            inst.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.center.y, z);
            pieces.Add(new Piece { t = holder, prefab = true, flat = true, tile = tile });
            Tint(renderers, !CityMap.I.IsUnlocked(tile));
        }

        // ---------- Kutu bina (paket yoksa) ----------

        void AddBox(Vector2Int tile, float size, float height, Color c, Shop shop, bool hq, Vector2 offset = default, bool registerHeight = true)
            => AddBoxR(tile, new Vector2(size, size), height, c, shop, hq, offset, registerHeight);

        /// <summary>Dikdörtgen tabanlı kutu (köprü korkuluğu, AVM, çeşme için).</summary>
        void AddBoxR(Vector2Int tile, Vector2 size, float height, Color c, Shop shop, bool hq, Vector2 offset = default, bool registerHeight = true)
        {
            var holder = Holder(shop != null ? shop.def.name + " (3D)" : hq ? "Merkez Ofis (3D)" : "Bina", tile, offset);
            holder.localScale = new Vector3(size.x, size.y, 0.001f);
            var mf = holder.gameObject.AddComponent<MeshFilter>();
            var mr = holder.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = 5; // zemin ve dükkan karelerinin üstünde, kurye ve müşterilerin altında
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = BoxMesh();
            pieces.Add(new Piece { t = holder, mf = mf, height = height, baseColor = c, tile = tile, shop = shop, hq = hq });
            if (registerHeight) heightByTile[tile] = height;
        }

        /// <summary>Birim kutu: taban z=0, çatı z=−1. Görünen yüzler (yanlar, güney cephe, çatı); renk vertex renginde.</summary>
        public static Mesh BoxMesh()
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var tris = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }
            const float h = 0.5f;
            // Sıra önemli (sprite malzemesi derinlik yazmaz): önce arka, yanlar, sonra ön cephe, en son çatı
            Quad(new Vector3(-h, h, 0), new Vector3(h, h, 0), new Vector3(h, h, -1), new Vector3(-h, h, -1));     // kuzey (arka)
            Quad(new Vector3(-h, -h, 0), new Vector3(-h, h, 0), new Vector3(-h, h, -1), new Vector3(-h, -h, -1));
            Quad(new Vector3(h, -h, 0), new Vector3(h, -h, -1), new Vector3(h, h, -1), new Vector3(h, h, 0));
            Quad(new Vector3(-h, -h, 0), new Vector3(-h, -h, -1), new Vector3(h, -h, -1), new Vector3(h, -h, 0));
            Quad(new Vector3(-h, -h, -1), new Vector3(-h, h, -1), new Vector3(h, h, -1), new Vector3(h, -h, -1));
            m.SetVertices(v);
            m.SetTriangles(tris, 0);
            m.SetColors(new Color[v.Count]);
            m.RecalculateBounds();
            return m;
        }

        public static void Paint(Mesh m, Color baseColor)
        {
            var side = Color.Lerp(baseColor, Color.black, 0.45f);
            var front = Color.Lerp(baseColor, Color.black, 0.28f);
            var roof = Color.Lerp(baseColor, Color.white, 0.12f);
            var cols = new Color[20];
            for (int i = 0; i < 4; i++) { cols[i] = side; cols[4 + i] = side; cols[8 + i] = side; cols[12 + i] = front; cols[16 + i] = roof; }
            cols[13] = cols[14] = Color.Lerp(front, roof, 0.3f);
            m.colors = cols;
        }

        int ContractCount()
        {
            int n = 0;
            foreach (var s in CityMap.I.Shops) if (s.contracted) n++;
            return n;
        }

        void Refresh(bool colors)
        {
            var map = CityMap.I;
            if (!colors) return;
            foreach (var p in pieces)
            {
                if (p.prefab)
                {
                    Tint(p.t.GetComponentsInChildren<Renderer>(true), !map.IsUnlocked(p.tile), p.varSeed);
                    continue;
                }
                Paint(p.mf.mesh, BoxColor(p, map));
            }
            lastUnlock = map.UnlockLevel;
            lastContracts = ContractCount();
            SehirMevsimTak();
            TintVersion++;
        }

        // ---------- Atölye binaları arka planda ----------

        class Bekleyen
        {
            public Vector2Int tile;
            public float footprint, face, targetHeight;
            public Vector2 offset;
            public bool registerHeight, own;
            public Piece yer;
        }
        readonly Dictionary<GameObject, List<Bekleyen>> bekleyenler = new Dictionary<GameObject, List<Bekleyen>>();
        int arkaPlanKurulan;
        float arkaPlanBaslangic;

        /// <summary>Şehirde henüz kurulmamış atölye modeli kaldı mı (yükleme ekranı / hata raporu için).</summary>
        public int BekleyenModel => bekleyenler.Count;

        /// <summary>Kurulmamış şablonun yerine geçici kutu (kare boyu kaydedilmez: gerçek bina kurulunca yazılır).</summary>
        void YerTutucu(GameObject prefab, Vector2Int tile, float footprint, float face, Vector2 offset, float targetHeight, bool registerHeight, bool own)
        {
            // kutunun boyu tasarımın kat sayısından (3 m kat + çatı); arsa 14 m, karede 0,97
            int kat = AtolyeUretici.KatSayisi(prefab);
            float h = Mathf.Max(0.06f, (kat * 3f + 1.2f) * (0.97f / 14f)) * OwnHeightBoost;
            AddBoxR(tile, new Vector2(footprint * 0.62f, footprint * 0.62f), h, new Color(0.88f, 0.86f, 0.82f), null, false, offset, false);
            var p = pieces[pieces.Count - 1];
            if (!bekleyenler.TryGetValue(prefab, out var l)) bekleyenler[prefab] = l = new List<Bekleyen>();
            if (bekleyenler.Count == 1 && l.Count == 0) { arkaPlanKurulan = 0; arkaPlanBaslangic = Time.realtimeSinceStartup; }
            l.Add(new Bekleyen { tile = tile, footprint = footprint, face = face, targetHeight = targetHeight, offset = offset, registerHeight = registerHeight, own = own, yer = p });
        }

        /// <summary>Her karede ~10 ms: kameranın baktığı yere en yakın bekleyen şablonu kurar, kutuları gerçek binayla değiştirir.</summary>
        void ArkaPlanAdim()
        {
            if (bekleyenler.Count == 0) return;
            var cam = Camera.main;
            Vector2 odak = default;
            bool odakVar = cam != null && MapCamera.I != null && MapCamera.I.GroundPoint(cam, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out odak);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                GameObject enIyi = null;
                float enYakin = float.MaxValue;
                foreach (var kv in bekleyenler)
                {
                    float d = 0f;
                    if (odakVar)
                    {
                        d = float.MaxValue;
                        foreach (var e in kv.Value) d = Mathf.Min(d, (new Vector2(e.tile.x, e.tile.y) - odak).sqrMagnitude);
                    }
                    if (d < enYakin) { enYakin = d; enIyi = kv.Key; }
                    if (!odakVar) break;
                }
                if (enIyi == null) break;
                var liste = bekleyenler[enIyi];
                bekleyenler.Remove(enIyi);
                AtolyeUretici.Hazirla(enIyi);
                foreach (var e in liste)
                {
                    pieces.Remove(e.yer);
                    if (e.yer.t != null) Destroy(e.yer.t.gameObject);
                    AddPrefab(enIyi, e.tile, e.footprint, e.face, null, e.offset, e.targetHeight, e.registerHeight, e.own);
                }
                arkaPlanKurulan++;
            } while (bekleyenler.Count > 0 && sw.ElapsedMilliseconds < 10);
            lastGrow = -1f;      // yeni parçalar yükselme oranını ve görünürlüğünü alsın
            if (bekleyenler.Count == 0)
            {
                Debug.Log($"Kurye Tycoon · atölye: {arkaPlanKurulan} şehir modeli arka planda kuruldu ({Time.realtimeSinceStartup - arkaPlanBaslangic:0.0} sn)");
                AtolyeUretici.SureyiYaz();
                Refresh(true);           // mevsim malzemeleri ve kilit solgunluğu yeni binalara
                Built?.Invoke();         // gece ışıkları yeni pencereleri toplasın
            }
        }

        void LateUpdate()
        {
            if (CityMap.I == null || MapCamera.I == null || pieces.Count == 0) return;
            ArkaPlanAdim();
            KirGuncelle();
            SehirMevsimGuncelle();
            if (CityMap.I.UnlockLevel != lastUnlock || ContractCount() != lastContracts) Refresh(true);

            float g = MapCamera.I.Tilt01;
            if (Mathf.Abs(g - lastGrow) < 0.001f) return;
            lastGrow = g;
            bool show = g > 0.01f;
            foreach (var p in pieces)
            {
                if (p.t.gameObject.activeSelf != show) p.t.gameObject.SetActive(show);
                if (!show) continue;
                var s = p.t.localScale;
                // Kutu: birim kutu boyu kadar uzatılır. Paket modeli: zaten doğru boyda, sadece yükselme oranı.
                if (p.flat) continue;
                p.t.localScale = new Vector3(s.x, s.y, Mathf.Max(0.001f, (p.prefab ? p.heightMul : p.height) * g));
            }
        }
    }
}
