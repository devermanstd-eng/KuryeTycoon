# Sadece kır parsellerini üretir (tarla, bağ, meyve bahçesi, çayır, gölet, sera, koru).
# Blender Python konsolunda:
# exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_kir.py", encoding="utf-8").read())
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
_t0 = time.time()
km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, True, True
km.MODEL = "kir"
km.main()
print("KIR BITTI %.0f sn" % (time.time() - _t0))
