# Blender Python konsolunda: exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_hepsi.py", encoding="utf-8").read())
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
# Hava parçaları (bulut, damla, kar, yaprak, şimşek): hafif sürüm gerekmez
_isler = [dict(MODEL="hava", LOD=False), dict(MODEL="blok"), dict(MODEL="katalog"), dict(MODEL="sehir"), dict(MODEL="dukkan")]
for t in (1, 2, 3):
    _isler.append(dict(MODEL="mustakil_ev", TOHUM=t, VARYANT=3))
for t in (1, 2):
    _isler.append(dict(MODEL="apartman", TOHUM=t, VARYANT=3))
_isler += [dict(MODEL="fastfood", TOHUM=1, VARYANT=2, FASTFOOD_MARKA="karisik"),
           dict(MODEL="market", TOHUM=1, VARYANT=3, MARKET_TIP="karisik"),
           dict(MODEL="sira_ev", TOHUM=1, VARYANT=4, STIL="karisik"),
           dict(MODEL="ofis_kulesi", TOHUM=1, VARYANT=3, OFIS_STIL="karisik")]
_t0 = time.time()
for ayar in _isler:
    km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, True, True
    for k, v in ayar.items():
        setattr(km, k, v)
    try:
        km.main()
        print("TAMAM", ayar)
    except Exception as e:
        import traceback; traceback.print_exc()
        print("HATA", ayar, e)
print("BITTI %.0f sn" % (time.time() - _t0))
