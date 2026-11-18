using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Blender'ı arka planda (pencere açmadan) çalıştırıp Tools/Blender/calistir_*.py betiklerini yürütür.
    /// Bitince "Blender Binalarını Prefab Yap" kendiliğinden çalışır. Blender'ın yeri bir kez seçilir ve hatırlanır.
    /// </summary>
    public static class BlenderCalistir
    {
        const string YolAnahtar = "KuryeTycoon.BlenderYolu";
        static Process surec;
        static string betikAdi;
        static bool prefabYapilsin;
        static readonly System.Text.StringBuilder cikti = new System.Text.StringBuilder();

        [MenuItem("Kurye Tycoon/Blender/Anime ağaçlarını üret")]
        static void Agaclar() => Calistir("calistir_anime.py");

        [MenuItem("Kurye Tycoon/Blender/Londra evleri ve dükkânları üret")]
        static void Londra() => Calistir("calistir_londra.py");

        [MenuItem("Kurye Tycoon/Blender/Londra blokları üret")]
        static void Bloklar() => Calistir("calistir_blok.py");

        /// <summary>Stilize pilot paketi (kurye, bisiklet, paket dolabı, ağaçlar, sokak lambası) → Modeller/Stilize. Prefab adımı yok.</summary>
        [MenuItem("Kurye Tycoon/Blender/Stilize pilot paketini üret")]
        static void Stilize() => Calistir("stilize_paket.py", false);

        [MenuItem("Kurye Tycoon/Blender/Blender yerini değiştir")]
        static void YerSec()
        {
            string y = Sec(Mac ? "Blender.app'i seçin" : "blender.exe dosyasını seçin");
            if (y != null) EditorPrefs.SetString(YolAnahtar, y);
        }

        static bool Mac => Application.platform == RuntimePlatform.OSXEditor;

        /// <summary>Dosya penceresinden Blender'ı seçtirir. Mac'te Blender bir .app paketi: "exe" süzgeciyle seçilemiyordu,
        /// "app" ile seçilir ve içindeki çalıştırılabilir dosyaya (Contents/MacOS/Blender) çevrilir.</summary>
        static string Sec(string baslik)
        {
            string y = EditorUtility.OpenFilePanel(baslik, Mac ? "/Applications" : "", Mac ? "app" : "exe");
            if (string.IsNullOrEmpty(y)) return null;
            if (y.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) y = Path.Combine(y, "Contents/MacOS/Blender");
            if (File.Exists(y)) return y;
            Debug.LogError("Kurye Tycoon: seçilen yerde Blender yok: " + y);
            return null;
        }

        static string BlenderBul()
        {
            string kayitli = EditorPrefs.GetString(YolAnahtar, "");
            if (File.Exists(kayitli)) return kayitli;
            var adaylar = new[]
            {
                @"D:\steam\steamapps\common\Blender\blender.exe",
                @"D:\Steam\steamapps\common\Blender\blender.exe",
                @"D:\SteamLibrary\steamapps\common\Blender\blender.exe",
                @"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe",
                @"C:\Program Files\Steam\steamapps\common\Blender\blender.exe",
                "/Applications/Blender.app/Contents/MacOS/Blender",
            }.ToList();
            string ev = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(ev))
            {
                adaylar.Add(Path.Combine(ev, "Applications/Blender.app/Contents/MacOS/Blender"));
                adaylar.Add(Path.Combine(ev, "Library/Application Support/Steam/steamapps/common/Blender/Blender.app/Contents/MacOS/Blender"));
            }
            const string vakif = @"C:\Program Files\Blender Foundation";
            if (Directory.Exists(vakif))
                adaylar.InsertRange(0, Directory.GetDirectories(vakif).OrderByDescending(d => d).Select(d => Path.Combine(d, "blender.exe")));
            var bulunan = adaylar.FirstOrDefault(File.Exists);
            if (bulunan != null) { EditorPrefs.SetString(YolAnahtar, bulunan); return bulunan; }
            string secilen = Sec(Mac ? "Blender bulunamadı: Blender.app'i seçin" : "blender.exe bulunamadı: lütfen seçin");
            if (secilen != null) { EditorPrefs.SetString(YolAnahtar, secilen); return secilen; }
            EditorUtility.DisplayDialog("Blender",
                "Blender bulunamadı. blender.org'dan indirip kur (Mac'te Apple Silicon sürümü, Uygulamalar klasörüne), " +
                "bir kez normal aç, sonra menüyü tekrar çalıştır.", "Tamam");
            return null;
        }

        static void Calistir(string betik, bool prefabYap = true)
        {
            if (surec != null && !surec.HasExited)
            {
                EditorUtility.DisplayDialog("Blender", "Blender zaten çalışıyor (" + betikAdi + "). Bitmesini bekleyin.", "Tamam");
                return;
            }
            string blender = BlenderBul();
            if (blender == null) return;
            string yol = Path.GetFullPath(Path.Combine(Application.dataPath, "KuryeTycoon/Tools/Blender", betik));
            if (!File.Exists(yol)) { Debug.LogError("Kurye Tycoon: betik yok: " + yol); return; }

            // Betik konsol için yazıldı (exec ile); arka planda da aynı şekilde çalıştır
            string sarici = Path.Combine(Path.GetTempPath(), "kurye_blender_" + Path.GetFileNameWithoutExtension(betik) + ".py");
            File.WriteAllText(sarici,
                "import sys, traceback\n" +
                "try:\n" +
                "    exec(open(r\"" + yol + "\", encoding=\"utf-8\").read())\n" +
                "except Exception:\n" +
                "    traceback.print_exc()\n" +
                "    sys.exit(1)\n");

            cikti.Clear();
            betikAdi = betik;
            prefabYapilsin = prefabYap;
            var bilgi = new ProcessStartInfo(blender, "-b --factory-startup -P \"" + sarici + "\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            // Betik çıktı klasörünü buradan bulur (sarıcıyla çalıştığı için kendi yerini bilemez)
            bilgi.EnvironmentVariables["KURYE_TYCOON_KOK"] = Path.GetFullPath(Path.Combine(Application.dataPath, "KuryeTycoon"));
            surec = new Process { StartInfo = bilgi, EnableRaisingEvents = true };
            surec.OutputDataReceived += (s, e) => { if (e.Data != null) lock (cikti) cikti.AppendLine(e.Data); };
            surec.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (cikti) cikti.AppendLine(e.Data); };
            surec.Start();
            surec.BeginOutputReadLine();
            surec.BeginErrorReadLine();
            Debug.Log("Kurye Tycoon: Blender arka planda çalışıyor: " + betik + " (" + blender + ")");
            EditorApplication.update += Izle;
        }

        static void Izle()
        {
            if (surec == null) { EditorApplication.update -= Izle; return; }
            if (!surec.HasExited)
            {
                EditorUtility.DisplayProgressBar("Blender", betikAdi + " çalışıyor... (bu birkaç dakika sürebilir)", 0.5f);
                return;
            }
            EditorApplication.update -= Izle;
            EditorUtility.ClearProgressBar();
            int kod = surec.ExitCode;
            surec.WaitForExit();
            string metin;
            lock (cikti) metin = cikti.ToString();
            surec = null;
            try
            {
                string logKlasor = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
                Directory.CreateDirectory(logKlasor);
                File.WriteAllText(Path.Combine(logKlasor, "blender_son.txt"), metin);
            }
            catch (Exception) { }
            string son = string.Join("\n", metin.Split('\n').Reverse().Take(25).Reverse());
            if (kod != 0 || metin.Contains("Traceback (most recent call last)"))
            {
                Debug.LogError("Kurye Tycoon: Blender hata verdi (" + betikAdi + "):\n" + metin);
                EditorUtility.DisplayDialog("Blender", "Blender hata verdi. Ayrıntı Console'da.\n\n" + son, "Tamam");
                return;
            }
            Debug.Log("Kurye Tycoon: Blender bitti (" + betikAdi + "):\n" + son);
            AssetDatabase.Refresh();
            if (prefabYapilsin) EditorApplication.ExecuteMenuItem("Kurye Tycoon/Blender Binalarını Prefab Yap");
        }
    }
}
