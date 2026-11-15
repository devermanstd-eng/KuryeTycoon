# Sadece araçları üretir (VW Polo 6N2: gümüş sade sürüm ve tavan kutulu kurye sürümü).
# Blender Python konsolunda:
# exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_arac.py", encoding="utf-8").read())
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
_t0 = time.time()
km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, False, True   # araçta hafif sürüm yok (parçalı)
km.MODEL = "arac"
km.main()
print("ARAC BITTI %.0f sn" % (time.time() - _t0))
