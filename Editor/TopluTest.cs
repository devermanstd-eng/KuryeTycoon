using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Toplu simülasyonun komut satırı giriş noktası (Tools/toplu_test.sh çağırır):
    ///   Unity -batchmode -nographics -projectPath &lt;proje&gt; -executeMethod KuryeTycoon.EditorTools.TopluTest.Calistir -logFile &lt;log&gt;
    /// -quit VERİLMEZ: Calistir Play moduna girer, koşuları çalışma anındaki TopluTestKosucu yürütür,
    /// bitince TopluTestIzleyici Unity'yi EditorApplication.Exit(0 | 1) ile kapatır.
    /// </summary>
    public static class TopluTest
    {
        internal const string BasladiAnahtari = "KuryeTycoon.TopluTest.Basladi";

        public static void Calistir()
        {
            Debug.Log("[TopluTest] Unity açıldı, derleme kontrol ediliyor.");
            if (EditorUtility.scriptCompilationFailed)
            {
                Debug.LogError("[TopluTest] Scriptlerde derleme hatası var; simülasyon başlatılmadı.");
                EditorApplication.Exit(1);
                return;
            }
            SessionState.SetBool(BasladiAnahtari, true);
            Debug.Log("[TopluTest] Play moduna giriliyor.");
            EditorApplication.isPlaying = true;
        }
    }

    /// <summary>
    /// Play moduna girerken domain yeniden yüklenir; bu sınıf her yüklemede yeniden kurulur ve
    /// koşucu bitince ya da Play beklenmedik şekilde biterse Unity'yi doğru çıkış koduyla kapatır.
    /// Yalnızca komut satırında TopluTest.Calistir varsa çalışır.
    /// </summary>
    [InitializeOnLoad]
    static class TopluTestIzleyici
    {
        /// <summary>Hiçbir şey olmazsa en fazla bu kadar beklenir (dk). -topluZamanAsimi ile değişir.</summary>
        const double VarsayilanZamanAsimiDk = 360;
        static double zamanAsimiSn;
        static bool cikiyor;

        static TopluTestIzleyici()
        {
            bool istendi = false;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != null && a[i].EndsWith(TopluTestKosucu.MetotAdi, System.StringComparison.Ordinal)) istendi = true;
                if (i < a.Length - 1 && string.Equals(a[i], "-topluZamanAsimi", System.StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double dk))
                    zamanAsimiSn = dk * 60.0;
            }
            if (!istendi) return;
            if (zamanAsimiSn <= 0) zamanAsimiSn = VarsayilanZamanAsimiDk * 60.0;
            EditorApplication.update += Guncelle;
        }

        static void Guncelle()
        {
            if (cikiyor || !SessionState.GetBool(TopluTest.BasladiAnahtari, false)) return;

            if (TopluTestKosucu.Bitti) { Cik(TopluTestKosucu.CikisKodu, "koşular bitti"); return; }

            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                Debug.LogError("[TopluTest] Play modu koşular bitmeden kapandı.");
                Cik(1, "Play modu beklenmedik şekilde bitti");
                return;
            }

            if (EditorApplication.timeSinceStartup > zamanAsimiSn)
            {
                Debug.LogError($"[TopluTest] Genel zaman aşımı ({zamanAsimiSn / 60.0:0} dk).");
                Cik(1, "genel zaman aşımı");
            }
        }

        static void Cik(int kod, string neden)
        {
            cikiyor = true;
            SessionState.EraseBool(TopluTest.BasladiAnahtari);
            Debug.Log($"[TopluTest] Unity kapanıyor ({neden}), çıkış kodu {kod}. Rapor: {TopluTestKosucu.RaporKlasoru}");
            EditorApplication.Exit(kod);
        }
    }
}
