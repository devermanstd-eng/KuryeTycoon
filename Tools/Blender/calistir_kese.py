# Sadece kraft kâğıt keseleri üretir (dükkan çalışanlarının ürünleri koyduğu, kuryenin teslim aldığı paketler):
# ufak (iki kez kıvrık), orta, uzun, geniş (market), kare ve ağzı açık kese.
# Blender Python konsolunda:
# exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_kese.py", encoding="utf-8").read())
# Sonra Unity'de: Kurye Tycoon > Blender Binalarını Prefab Yap  → prefablar Resources/Keseler'e gider.
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
_t0 = time.time()
km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, False, True   # küçük model: hafif sürüme gerek yok
km.MODEL = "kese"
km.main()
print("KESE BITTI %.0f sn" % (time.time() - _t0))
