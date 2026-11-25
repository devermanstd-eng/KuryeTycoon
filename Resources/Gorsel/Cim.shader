// Kurye Tycoon · çayır çimi (URP, GPU instancing). Dünyada yukarı −Z. Çizen: Scripts/World/Cim.cs
//  • Öbek mesh'i: yaprak başına köşe rengi a = yükseklik (0 kök, 1 uç), uv.x = yaprak tohumu, uv.y = 1 ise çiçek/tohum başı (köşe rengi kullanılır)
//  • Rüzgâr: rüzgâr yönünde kayan gürültü dalgaları (esinti tarlada dalga gibi ilerler) + her öbeğin kendi salınımı; kök sabit, uç en çok eğilir
//  • Renk: kökte koyu, uca doğru açılır; dünya ölçekli gürültüyle yeşil / kuru yamalar; öbek ve yaprak başına küçük ton farkı
//  • Işık: yumuşak (yarım Lambert), güneşe karşı bakınca yapraktan ışık geçer, kökte gölge (AO); gölge alır, düşürmez
//  • Mevsim ve kar: Cim.cs her kare ayarlar (_Uc, _Kuru, _KuruOran, _Kar); karda boy kısalır ve uçlar beyazlar
Shader "KuryeTycoon/Cim"
{
    Properties
    {
        _Taban("Kök rengi", Color) = (0.16, 0.26, 0.10, 1)
        _Uc("Uç rengi (yeşil)", Color) = (0.50, 0.68, 0.27, 1)
        _Kuru("Kuru yama rengi", Color) = (0.74, 0.68, 0.38, 1)
        _KuruOran("Kuruluk", Range(0, 1)) = 0.2
        _Kar("Kar", Range(0, 1)) = 0
        _Gorunum("Görünüm (boy çarpanı)", Range(0, 1)) = 1
        _RuzgarYon("Rüzgâr yönü (x, y)", Vector) = (0.8, 0.6, 0, 0)
        _RuzgarGuc("Rüzgâr gücü", Float) = 0.45
        _RuzgarHiz("Esinti hızı", Float) = 0.35
        _RuzgarOlcek("Esinti ölçeği", Float) = 0.9
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Taban, _Uc, _Kuru;
            float _KuruOran, _Kar, _Gorunum;
            float4 _RuzgarYon;
            float _RuzgarGuc, _RuzgarHiz, _RuzgarOlcek;
        CBUFFER_END

        struct A
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hs(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
        float Gurultu(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hs(i), Hs(i + float2(1, 0)), u.x), lerp(Hs(i + float2(0, 1)), Hs(i + float2(1, 1)), u.x), u.y);
        }

        // Köşenin rüzgârla eğilmiş dünya konumu. kok: öbeğin kökü (dünya).
        float3 CimKonum(A i, out float3 kok)
        {
            kok = TransformObjectToWorld(float3(0, 0, 0));
            float3 ws = TransformObjectToWorld(i.positionOS.xyz);
            float t = saturate(i.color.a);

            // Karda ve görünürken boy (yukarı −Z: kökten uzaklık negatif z)
            float boy = _Gorunum * (1.0 - 0.55 * _Kar);
            ws = kok + (ws - kok) * float3(lerp(1.0, boy, 0.35), lerp(1.0, boy, 0.35), boy);
            float hgt = max(kok.z - ws.z, 0.0);

            float2 yon = normalize(_RuzgarYon.xy + float2(1e-4, 0));
            float2 dik = float2(-yon.y, yon.x);
            float faz = Hs(kok.xy * 37.17);
            float2 q = kok.xy * _RuzgarOlcek - yon * _Time.y * _RuzgarHiz;
            float esinti = Gurultu(q) * 0.7 + Gurultu(q * 2.3 + 7.1) * 0.3;
            esinti = smoothstep(0.32, 0.82, esinti);
            float sal = sin(_Time.y * (2.1 + faz * 1.7) + faz * 6.2832 + dot(kok.xy, yon) * 4.0);
            float egil = _RuzgarGuc * (0.2 + 1.15 * esinti) + sal * 0.1 * (0.35 + _RuzgarGuc);
            egil = clamp(egil, -0.95, 0.95);
            float k = t * t;
            float2 kay = (yon * egil + dik * sal * 0.05 * (0.3 + _RuzgarGuc)) * k * hgt * 1.1;
            ws.xy += kay;
            ws.z += dot(kay, kay) / max(hgt, 1e-4) * 0.5;    // uç eğilince alçalır (+Z aşağı)
            return ws;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2;
                float4 veri : TEXCOORD3;    // xy: kök, z: yaprak tohumu, w: çiçek bayrağı
                float fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 kok;
                float3 ws = CimKonum(i, kok);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.color = i.color;
                o.veri = float4(kok.xy, i.uv.x, i.uv.y);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i, bool on : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float t = saturate(i.color.a);
                float2 kok = i.veri.xy;

                // Yeşil / kuru yamalar (dünya ölçekli), öbek ve yaprak başına ton farkı
                float yama = Gurultu(kok * 0.32) * 0.6 + Gurultu(kok * 1.35 + 3.7) * 0.4;
                float kuru = saturate(_KuruOran + (yama - 0.5) * 0.95 + (Hs(kok * 5.31) - 0.5) * 0.6);   // öbekten öbeğe: biri taze yeşil, yanındaki saman
                float tohum = Hs(kok * 91.7 + i.veri.z * 13.1);
                float3 uc = lerp(_Uc.rgb, _Kuru.rgb, kuru);
                uc *= 0.84 + 0.3 * tohum;
                uc = lerp(uc, uc * float3(1.08, 1.02, 0.78), step(0.82, Hs(kok * 17.3)) * 0.6);   // sarımsı öbekler
                float3 kokRenk = _Taban.rgb * lerp(1.0, 1.35, kuru);
                float3 renk = lerp(kokRenk, uc, smoothstep(0.0, 0.85, t));
                renk = lerp(renk, renk * 1.12 + float3(0.05, 0.045, 0.01), smoothstep(0.7, 1.0, t) * 0.55);   // uçlar güneşte açılır
                if (i.veri.w > 0.5) renk = i.color.rgb;    // çiçek ve tohum başları kendi renginde
                renk = lerp(renk, float3(0.92, 0.94, 0.97), _Kar * smoothstep(0.25, 1.0, t) * 0.85);

                // Işık: yumuşak normal (yaprak yüzü ile gök arası), arka yüz çevrilir
                float3 n = normalize(i.normalWS) * (on ? 1.0 : -1.0);
                float3 N = normalize(lerp(n, float3(0, 0, -1), 0.6));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float atten = L.shadowAttenuation * L.distanceAttenuation;
                float ndl = dot(N, L.direction) * 0.5 + 0.5;
                float3 Vd = normalize(GetWorldSpaceViewDir(i.positionWS));
                float gecis = pow(saturate(dot(Vd, -L.direction)), 3.0) * 0.55 * t;      // güneşe karşı ışık geçirir
                float ao = lerp(0.72, 1.0, t);
                half3 c = renk * (SampleSH(N) * 0.85 * ao + L.color * atten * (ndl * 0.95 * ao + gecis));
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        // Derinlik: yere yakın düz normal (mürekkep çizgisi çimin kenarlarını çizmesin)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            float4 vert(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 kok;
                return TransformWorldToHClip(CimKonum(i, kok));
            }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            float4 vert(A i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 kok;
                return TransformWorldToHClip(CimKonum(i, kok));
            }
            half4 frag() : SV_Target { return half4(0, 0, -1, 0) * 0.5 + 0.5; }
            ENDHLSL
        }
    }
}
