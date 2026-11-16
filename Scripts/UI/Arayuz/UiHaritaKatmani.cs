using UnityEngine;
using UnityEngine.UIElements;

namespace KuryeTycoon
{
    /// <summary>
    /// Haritanın hemen üstündeki, bütün arayüzün altındaki UI Toolkit katmanı: hava (bulut, yağmur, kar, güneş parlaması),
    /// gece/gündüz tonu ve ışıkları (WeatherFx, DayNight) ile harita katmanı lekeleri (HeatMap). Kayıtlar Ciz2D'den gelir.
    ///  • Kök elemanın ilk çocuğu (uiKok'tan önce): üst çubuk, pencere, kurye telefonu, bina kartı, durum kartları, bildirimler
    ///    ve olay kartı hep üstünde kalır. Eskiden bunlar OnGUI'de çiziliyordu ve IMGUI UI Toolkit'in üstüne çıktığı için güneş
    ///    parlaması telefonun üstüne biniyordu (16.11).
    ///  • uiKok'tan ayrı: foto modunda ve eski arayüzde (F8) de görünür; ortada bir kutu açıkken de çizilir.
    ///  • Hava kaydı ekran pikseli, katman kaydı GUI birimi; panel birimine ölçekle çevrilir ve kaydın alanına kırpılır.
    /// Harita etiketleri IMGUI'de kaldı; UI Toolkit kutularının altına giren etiket çizilmez (UiHaritaUstuDolu).
    /// </summary>
    public partial class GameUI
    {
        VisualElement katHarita;
        HaritaKatmani havaKatmani, isiKatmani;

        /// <summary>Bir Ciz2D kaydını çizen eleman: kaydın alanında durur, dışına taşanı kırpar.</summary>
        class HaritaKatmani : VisualElement
        {
            readonly Ciz2D kayit;
            float olcek = 1f;
            Rect sonAlan;

            public HaritaKatmani(Ciz2D k)
            {
                kayit = k;
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.overflow = Overflow.Hidden;
                generateVisualContent += m => kayit.Ciz(m, olcek);
            }

            /// <summary>Her karede: alanı (kayıt birimi × ölçek) ve yeniden çizim.</summary>
            public void Yenile(float o)
            {
                olcek = o;
                var a = kayit.Alan;
                var r = new Rect(a.x * o, a.y * o, a.width * o, a.height * o);
                if (r != sonAlan)
                {
                    sonAlan = r;
                    style.left = r.x;
                    style.top = r.y;
                    style.width = r.width;
                    style.height = r.height;
                }
                MarkDirtyRepaint();
            }
        }

        /// <summary>UiKur'dan, kök elemanın ilk çocuğu olarak (uiKok'tan önce).</summary>
        void UiHaritaKatmaniKur(VisualElement root)
        {
            katHarita = U.Gecirgen(null, "katman");
            root.Insert(0, katHarita);
            havaKatmani = new HaritaKatmani(Ciz2D.Hava);
            katHarita.Add(havaKatmani);
            isiKatmani = new HaritaKatmani(Ciz2D.Isi);   // katman lekeleri havanın üstünde (eskisi gibi)
            katHarita.Add(isiKatmani);
        }

        /// <summary>LateUpdate'ten (harita görüntü alanı ayarlandıktan sonra): katmanı kaydet, iki kaydı yerleştir ve çizdir.</summary>
        void UiHaritaKatmaniYenile()
        {
            bool basladi = GameBootstrap.I != null && GameBootstrap.I.Started;
            bool hazir = uiHazir && katHarita != null && uiPanel != null && basladi;
            Ciz2D.UitkCiziyor = hazir;
            float s = uiPanel != null ? uiPanel.scale : uiScale;
            if (basladi)
            {
                float sw = Screen.width / Mathf.Max(0.01f, s), sh = Screen.height / Mathf.Max(0.01f, s);
                IsiKaydet(new Rect(MapLeftGui, TopBar, Mathf.Max(0f, sw - MapLeftGui), Mathf.Max(0f, sh - TopBar)));
            }
            if (katHarita == null) return;
            U.Goster(katHarita, hazir);
            if (!hazir) return;
            havaKatmani.Yenile(1f / Mathf.Max(0.01f, s));   // ekran pikseli → panel birimi
            isiKatmani.Yenile(1f);                          // GUI birimi = panel birimi
        }
    }
}
