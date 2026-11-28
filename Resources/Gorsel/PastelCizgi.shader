// Kurye Tycoon — Pastel kalem çizgisi: Bina Atölyesi mesh'lerinin "Pastel_Cizgi" alt-mesh'i için.
//  • Her kenar dört köşeli bir şerittir: köşe konumu kenarın bir ucu, TEXCOORD1 öbür ucu, uv.x yan (−1 / +1), uv.y uç (+1 baş,
//    −1 son). Köşe gölgelendiricisi şeridi EKRANDA sabit piksel kalınlığına açar (geometry shader yok): yakında da uzakta da
//    aynı incelikte; çok kısa görünen kenarlar silinir (uzakta bina çizgi yumağına dönmez).
//  • El çizimi hissi: kalınlık uçtan uca değişir, ton ve koyuluk çizgi boyunca gürültüyle oynar, yer yer incelip kesilir.
//  • Kademeli kalınlık: |uv.x| şeridin yarı genişliğini ölçekler (büyük kenar 1,0; ayrıntı kenarı 0,45). v6: çizgiler
//    inceldi (varsayılan 1,15 piksel), ayrıntı çizgileri biraz saydam; çizgi kameraya daha az çekilir (arkada kalan
//    girintili kenarlar öne sızıp görüntüyü ağırlaştırmasın).
//  • uv.x = 0 olan üçgenler açılmaz: ağaç taçlarının "ters kabuğu" (biraz büyük, yüzleri ters kopya) aynı malzemeyle dış hat olur.
//  • Çevre animasyonu: köşe renginin alfası animasyon modunu taşır (CevreAnimasyon.hlsl; 255: yok). Şeridin iki ucu da
//    (POSITION ve TEXCOORD1) yüzeyle AYNI işlevle kaydırılır: salınan ağacın, dalgalanan bayrağın, dönen atlıkarıncanın
//    çizgisi yüzeyden ayrılmaz. Ters kabuk üçgenleri (uv.x = 0) de aynı kaymayı alır. Titreme gürültüsü kaymamış konuma
//    bağlıdır (çizgi oynarken kalınlığı kıpırdamaz).
//  • Renk köşe renginden (yüzey renginin koyusu). Işıksız; gölge düşürmez; derinlik / normal dokusuna yazmaz (ekran
//    geçişindeki mürekkep çizgisi bu şeritleri görmez).
//  • Uzaklık solması (u59): kameradan _CizgiYakin karede tam, _CizgiUzak karede ana hatlar _CizgiUzakAlfa'ya kadar solar ve
//    incelir; ayrıntı çizgileri daha erken tamamen söner. Uzaktan şehir çizgi ağına dönmez.
//  • _PastelCizgiGizle (global) 1 ise hiçbir şey çizmez: oyunda ekran geçişi (MurekkepCizgi) konturu çizerken çift çizgi
//    olmasın diye (City3D, Resources/Gorsel/AtolyeAyari).
//  • Oyunda yumuşak çizgi (u60): _Kalinlik, _CizgiGuc, _AyrintiGuc, _Acik, _CizgiYakin, _CizgiUzak oyunda AtolyeAyari'dan gelir
//    (AtolyeKurucu.Malzeme); atölye önizlemesi varsayılanlarla, güçlü çizgiyle kalır.
Shader "KuryeTycoon/PastelCizgi"
{
    Properties
    {
        [MainColor] _BaseColor("Renk çarpanı", Color) = (1, 1, 1, 1)
        _Kalinlik("Kalınlık (piksel)", Range(0.5, 4)) = 1.15
        _Titreme("El titremesi", Range(0, 1)) = 0.55
        _Silinme("Bu boydan kısa görünen çizgi silinir (piksel)", Range(0, 12)) = 3
        _CizgiYakin("Bu uzaklığa kadar çizgi tam (dünya birimi = kare)", Range(1, 60)) = 11
        _CizgiUzak("Bu uzaklıkta çizgi en soluk (kare)", Range(2, 120)) = 30
        _CizgiUzakAlfa("En uzakta ana çizginin saydamlığı", Range(0, 1)) = 0.12
        _CizgiGuc("Ana çizgi koyuluğu (yakında)", Range(0, 1)) = 1
        _AyrintiGuc("Ayrıntı çizgisi koyuluğu (pencere, kiremit; yakında)", Range(0, 1)) = 0.8
        _Acik("Çizgi rengini açma (0: yüzeyin koyusu, 1: çok açık)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+50" }

        Pass
        {
            Name "Cizgi"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Kalinlik;
                float _Titreme;
                float _Silinme;
                float _CizgiYakin;
                float _CizgiUzak;
                float _CizgiUzakAlfa;
                float _CizgiGuc;
                float _AyrintiGuc;
                float _Acik;
            CBUFFER_END

            float _PastelCizgiGizle;
            float _PastelKis;        // kış: yaprak döken taçların ters kabuğu (uv.x = 0, uv.y = 2) çizilmez

            #include "CevreAnimasyon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;       // xy: yan / uç; zw: animasyon sabitleri (mesh'te animasyon varsa)
                float3 diger : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                float kabuk : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float PC_Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float PC_Gurultu(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(PC_Hash(i + float3(0, 0, 0)), PC_Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(PC_Hash(i + float3(0, 1, 0)), PC_Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(PC_Hash(i + float3(0, 0, 1)), PC_Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(PC_Hash(i + float3(0, 1, 1)), PC_Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Noktayı kameraya doğru azıcık çeker (çizgi kendi yüzeyinin önünde kalsın), kırpma uzayına çevirir
            float4 PC_Kirpma(float3 po)
            {
                float3 pw = TransformObjectToWorld(po);
                float3 bakis = GetWorldSpaceNormalizeViewDir(pw);
                float uzak = abs(TransformWorldToView(pw).z);
                pw += bakis * (uzak * 0.002 + 0.001);
                return TransformWorldToHClip(pw);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // çevre animasyonu: şeridin iki ucu da yüzeyle aynı kaymayı alır
                float3 pa = v.positionOS.xyz;
                float3 pb = v.diger;
                float3 nBos = float3(0.0, 1.0, 0.0);
                CA_Uygula(pa, nBos, v.color.a, v.uv.zw);
                CA_Uygula(pb, nBos, v.color.a, v.uv.zw);
                float4 cp = PC_Kirpma(pa);
                // Uzaklaştıkça çizgi solar ve incelir: uzaktan şehir çizgi ağına dönmesin (yakında el çizimi, uzakta yumuşak)
                float uzaklik = abs(TransformWorldToView(TransformObjectToWorld(pa)).z);
                float sol = smoothstep(_CizgiYakin, _CizgiUzak, uzaklik);
                bool ayrinti = abs(v.uv.x) > 0.05 && abs(v.uv.x) < 0.75;
                half alfa = 1.0h;
                if (abs(v.uv.x) > 0.05)
                {
                    float4 cq = PC_Kirpma(pb);
                    float2 sp = cp.xy / max(cp.w, 1e-5) * _ScreenParams.xy * 0.5;
                    float2 sq = cq.xy / max(cq.w, 1e-5) * _ScreenParams.xy * 0.5;
                    float2 d = sq - sp;
                    float boy = length(d);
                    if (boy > 1e-4 && cp.w > 0.0 && cq.w > 0.0)
                    {
                        d /= boy;
                        float2 n = float2(-d.y, d.x) * v.uv.y;       // iki uçta aynı yöne bakan dik
                        float kal = _Kalinlik * lerp(1.0, 0.55 + 0.9 * PC_Hash(v.positionOS.xyz * 3.7 + 1.3), _Titreme) * lerp(1.0, 0.65, sol);
                        float2 kay = n * v.uv.x * kal * 0.5 - d * kal * 0.3;     // yana aç, uçtan biraz taşır
                        cp.xy += kay * 2.0 / _ScreenParams.xy * cp.w;
                    }
                    alfa = saturate((boy - _Silinme * 0.5) / max(_Silinme, 0.01));
                    alfa *= abs(v.uv.x) < 0.75 ? (half)_AyrintiGuc : (half)_CizgiGuc;     // ayrıntı çizgisi daha saydam
                }
                // uzaklık solması: ayrıntı çizgileri (pencere, kapı, kiremit) daha erken ve tamamen, ana hatlar _CizgiUzakAlfa'ya kadar
                alfa *= ayrinti ? (half)(1.0 - smoothstep(_CizgiYakin * 0.7, _CizgiUzak * 0.75, uzaklik))
                                : (half)lerp(1.0, _CizgiUzakAlfa, sol);
                if (_PastelCizgiGizle > 0.5) cp = float4(2.0, 2.0, 2.0, 1.0);    // kırpma hacminin dışı: çizilmez
                if (_PastelKis > 0.5 && abs(v.uv.x) <= 0.05 && v.uv.y > 1.5) cp = float4(2.0, 2.0, 2.0, 1.0);
                o.positionCS = cp;
                half3 c = v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    c = c * (c * (c * 0.305306011h + 0.682171111h) + 0.012522878h);   // sRGB → doğrusal (yaklaşık)
                #endif
                c = lerp(c, saturate(c * 1.6h + 0.06h), (half)_Acik);        // çizgi rengi açılır: kalem bastırmaz, hafif dokunur
                o.color = half4(c * _BaseColor.rgb, alfa * _BaseColor.a);
                o.positionOS = v.positionOS.xyz;
                o.kabuk = abs(v.uv.x) > 0.05 ? 0.0 : 1.0;
                o.fogCoord = ComputeFogFactor(cp.z);
                return o;
            }

            half4 frag(Varyings i, bool onYuz : SV_IsFrontFace) : SV_Target
            {
                // ters kabuk: yalnız içe bakan (kameradan uzak) yüzleri çizilir; şeritler iki yüzlü
                if (i.kabuk > 0.5 && !onYuz) discard;
                // çizgi boyunca ton ve koyuluk oynaması (model uzayı, metre): kalem bazı yerlerde bastırır, bazı yerlerde kaldırır
                half g = PC_Gurultu(i.positionOS * 3.1) * 0.65h + PC_Gurultu(i.positionOS * 9.0 + 4.0) * 0.35h;
                half3 renk = i.color.rgb * (0.85h + g * 0.4h * _Titreme + (1.0h - _Titreme) * 0.15h);
                half alfa = i.color.a * lerp(1.0h, smoothstep(0.12h, 0.42h, g), _Titreme * 0.75h);
                renk = MixFog(renk, i.fogCoord);
                return half4(renk, alfa);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
