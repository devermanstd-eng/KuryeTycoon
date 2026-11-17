# Sadece bitişik blokları üretir (Londra: Georgian, Notting Hill, Victorian, mansion block, modern, köşe pub'ı).
# Blender Python konsolunda:
# exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_blok.py", encoding="utf-8").read())
# Sonra Unity'de: Kurye Tycoon > Blender Binalarını Prefab Yap
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
_t0 = time.time()
km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, True, True
km.MODEL = "blok"
km.main()
print("BLOK BITTI %.0f sn" % (time.time() - _t0))
