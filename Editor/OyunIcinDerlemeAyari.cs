using UnityEditor;
using UnityEngine;

namespace KuryeTycoon.EditorTools
{
    /// <summary>
    /// Oyun açıkken (Play) script değişirse (ör. git pull) Unity varsayılan olarak hemen derleyip devam eder.
    /// Bu sırada tüm statik referanslar (EventManager.I, CityMap.I ...) boşalır ve oyun her karede
    /// NullReferenceException verir. Bu ayar derlemeyi Play bitene kadar erteler.
    /// (Edit > Preferences > General > Script Changes While Playing = Recompile After Finished Playing)
    /// </summary>
    [InitializeOnLoad]
    static class OyunIcinDerlemeAyari
    {
        const string Key = "ScriptCompilationDuringPlay"; // 0: derle ve devam et, 1: Play bitince derle, 2: durdur ve derle

        static OyunIcinDerlemeAyari()
        {
            if (EditorPrefs.GetInt(Key, 0) == 0)
            {
                EditorPrefs.SetInt(Key, 1);
                Debug.Log("Kurye Tycoon: 'Script Changes While Playing' ayarı 'Recompile After Finished Playing' yapıldı. " +
                          "Oyun açıkken git pull yaparsan değişiklikler Play'i durdurunca derlenir.");
            }
        }
    }
}
