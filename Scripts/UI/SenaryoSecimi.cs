using UnityEngine;

namespace KuryeTycoon
{
    /// <summary>
    /// Kurulum ekranında oyun türü (senaryo, serbest mod, günlük meydan okuma) ve olay anlatıcısı seçimi.
    /// Kurulum ekranı henüz IMGUI (UI Toolkit'e taşınacak); bu yüzden burası da IMGUI.
    /// </summary>
    public partial class GameUI
    {
        int setupSenaryo;
        int setupAnlatici = 1;

        void DrawSenaryoSecici()
        {
            GUILayout.Label("Oyun türü", header);
            int n = Senaryo.Adlar.Length;
            for (int satir = 0; satir < 2; satir++)
            {
                GUILayout.BeginHorizontal();
                for (int i = satir * 3; i < Mathf.Min(n, satir * 3 + 3); i++)
                {
                    int k = i;
                    if (SelectButton(setupSenaryo == i, Senaryo.Adlar[i], smallButton, GUILayout.Height(28))) deferred = () => setupSenaryo = k;
                }
                GUILayout.EndHorizontal();
            }
            string ek = setupSenaryo == (int)Senaryo.Tur.GunlukMeydan ? $" Bugünün şehri: #{Senaryo.BugununTohumu()}." : "";
            GUILayout.Label(Senaryo.Aciklamalar[setupSenaryo] + ek, small);
            GUILayout.Space(10);

            GUILayout.Label("Olay anlatıcısı", header);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Anlatici.Adlar.Length; i++)
            {
                int k = i;
                if (SelectButton(setupAnlatici == i, Anlatici.Adlar[i], smallButton, GUILayout.Height(28))) deferred = () => setupAnlatici = k;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Anlatici.Aciklamalar[setupAnlatici] + " Oyun sırasında Ayarlar'dan değiştirilebilir.", small);
        }

        /// <summary>Ayarlar > Arayüz: olay anlatıcısını oyun içinde değiştirme.</summary>
        void DrawAnlaticiAyari()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Olay anlatıcısı", body, GUILayout.Width(130));
            for (int i = 0; i < Anlatici.Adlar.Length; i++)
            {
                int k = i;
                if (SelectButton((int)Anlatici.Secili == i, Anlatici.Adlar[i], smallButton)) deferred = () => Anlatici.Secili = (Anlatici.Tur)k;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Anlatici.Aciklamalar[(int)Anlatici.Secili], small);
            if (Senaryo.Secili != Senaryo.Tur.Klasik)
                GUILayout.Label($"Oyun türü: {Senaryo.Adlar[(int)Senaryo.Secili]}" + (Senaryo.HedefVar ? $" · {Senaryo.IlerlemeYazisi}" : ""), small);
            GUILayout.Space(4);
        }
    }
}
