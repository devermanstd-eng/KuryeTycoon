// Kurye Tycoon — Pastel kalem çizgisi: Bina Atölyesi mesh'lerinin "Pastel_Cizgi" alt-mesh'i için.
//  • Her kenar dört köşeli bir şerittir: köşe konumu kenarın bir ucu, TEXCOORD1 öbür ucu, uv.x yan (−1 / +1), uv.y uç (+1 baş,
//    −1 son). Köşe gölgelendiricisi şeridi EKRANDA sabit piksel kalınlığına açar (geometry shader yok): yakında da uzakta da
//    aynı incelikte; çok kısa görünen kenarlar silinir (uzakta bina çizgi yumağına dönmez).
//  • El çizimi hissi: kalınlık uçtan uca değişir, ton ve koyuluk çizgi boyunca gürültüyle oynar, yer yer incelip kesilir.
//  • uv.x = 0 olan üçgenler açılmaz: ağaç taçlarının "ters kabuğu" (biraz büyük, yüzleri ters kopya) aynı malzemeyle dış hat olur.
//  • Renk köşe renginden (yüzey renginin koyusu). Işıksız; gölge düşürmez; derinlik / normal dokusuna yazmaz (ekran
//    geçişindeki mürekkep çizgisi bu şeritleri görmez).
//  • _PastelCizgiGizle (global) 1 ise hiçbir şey çizmez: oyunda ekran geçişi (MurekkepCizgi) konturu çizerken çift çizgi
//    olmasın diye (City3D, Resources/Gorsel/AtolyeAyari).
Shader "KuryeTycoon/PastelCizgi"
{
    Properties
    {
        [MainColor] _BaseColor("Renk çarpanı", Color) = (1, 1, 1, 1)
        _Kalinlik("Kalınlık (piksel)", Range(0.5, 4)) = 1.4
        _Titreme("El titremesi", Range(0, 1)) = 0.55
        _Silinme("Bu boydan kısa görünen çizgi silinir (piksel)", Range(0, 12)) = 3
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
            CBUFFER_END

            float _PastelCizgiGizle;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
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
                pw += bakis * (uzak * 0.004 + 0.0002);
                return TransformWorldToHClip(pw);
            }

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float4 cp = PC_Kirpma(v.positionOS.xyz);
                half alfa = 1.0h;
                if (abs(v.uv.x) > 0.5)
                {
                    float4 cq = PC_Kirpma(v.diger);
                    float2 sp = cp.xy / max(cp.w, 1e-5) * _ScreenParams.xy * 0.5;
                    float2 sq = cq.xy / max(cq.w, 1e-5) * _ScreenParams.xy * 0.5;
                    float2 d = sq - sp;
                    float boy = length(d);
                    if (boy > 1e-4 && cp.w > 0.0 && cq.w > 0.0)
                    {
                        d /= boy;
                        float2 n = float2(-d.y, d.x) * v.uv.y;       // iki uçta aynı yöne bakan dik
                        float kal = _Kalinlik * lerp(1.0, 0.55 + 0.9 * PC_Hash(v.positionOS.xyz * 3.7 + 1.3), _Titreme);
                        float2 kay = n * v.uv.x * kal * 0.5 - d * kal * 0.3;     // yana aç, uçtan biraz taşır
                        cp.xy += kay * 2.0 / _ScreenParams.xy * cp.w;
                    }
                    alfa = saturate((boy - _Silinme * 0.5) / max(_Silinme, 0.01));
                }
                if (_PastelCizgiGizle > 0.5) cp = float4(2.0, 2.0, 2.0, 1.0);    // kırpma hacminin dışı: çizilmez
                o.positionCS = cp;
                half3 c = v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    c = c * (c * (c * 0.305306011h + 0.682171111h) + 0.012522878h);   // sRGB → doğrusal (yaklaşık)
                #endif
                o.color = half4(c * _BaseColor.rgb, alfa * _BaseColor.a);
                o.positionOS = v.positionOS.xyz;
                o.kabuk = abs(v.uv.x) > 0.5 ? 0.0 : 1.0;
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
