# Yazılı tabelası olan binalar: marka dükkânları (Dukkan_*), marketler (Market_*) ve katalog (konut, büro, sanayi, kamu).
# Tabela metni değişince (ör. 26.11 isim değişikliği) bunları yeniden üretmek yeter; Unity'de "Kurye Tycoon > Blender > Tabelalı binaları üret".
# Blender Python konsolunda:
# exec(open(r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\calistir_tabela.py", encoding="utf-8").read())
import importlib.util, time
_yol = r"C:\UnityProjectsSSD\KuryeTycoon\Assets\KuryeTycoon\Tools\Blender\kurye_modeller.py"
_spec = importlib.util.spec_from_file_location("kurye_modeller", _yol)
km = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(km)
_t0 = time.time()
for ayar in (dict(MODEL="dukkan"), dict(MODEL="market", VARYANT=3, MARKET_TIP="karisik"), dict(MODEL="katalog")):
    km.TOHUM, km.DETAY, km.CIZGI, km.LOD, km.DISA_AKTAR = 1, True, True, True, True
    for k, v in ayar.items():
        setattr(km, k, v)
    try:
        km.main()
        print("TAMAM", ayar)
    except Exception as e:
        import traceback; traceback.print_exc()
        print("HATA", ayar, e)
print("TABELA BITTI %.0f sn" % (time.time() - _t0))
