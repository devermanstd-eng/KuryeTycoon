# Anime stil: boyalı ağaçlar ve çalılar (sehir parçaları) + bahçelerinde ağaç olan Londra evleri.
# Blender Python konsolunda:
# exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_anime.py", encoding="utf-8").read())
# Sonra Unity'de: Kurye Tycoon > Blender Binalarını Prefab Yap
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
_t0 = time.time()
for model in ("sehir", "londra_ev"):
    km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, True, True
    km.MODEL = model
    km.main()
    print("TAMAM", model)
print("ANIME AGACLAR BITTI %.0f sn" % (time.time() - _t0))
