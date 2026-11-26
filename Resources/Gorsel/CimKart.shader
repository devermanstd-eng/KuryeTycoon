// Kurye Tycoon · gerçekçi çim kartları (deneme, Ayarlar > Grafik > "Gerçekçi çim"). Çizen: Scripts/World/Cim.cs
//  • Doku: Resources/Gorsel/CimKart.png (4 × 2 hücre: uzun ot, kısa çim, tohumlu ot, kuru ot, eğrelti, üç kır çiçeği)
//  • Öbek: çapraz 2 dikey kart + iki yana yatık 2 kart; köşe rengi a = yükseklik (0 kök, 1 uç)
//  • Saydamlık kesme + kapsama yumuşatma (MSAA varsa kenarlar pürüzsüz); uzakta dokunun mipmap'i incelmesin diye alfa keskinleştirilir
//  • Rüzgâr, mevsim tonu, kar ve ışık KuryeTycoon/Cim ile aynı parametrelerle
Shader "KuryeTycoon/CimKart"
{
    Properties
    {
        _MainTex("Çim atlası", 2D) = "white" {}
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
        _Kesme("Saydamlık eşiği", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Taban, _Uc, _Kuru;
            float _KuruOran, _Kar, _Gorunum;
            float4 _RuzgarYon;
            float _RuzgarGuc, _RuzgarHiz, _RuzgarOlcek, _Kesme;
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

        float3 CimKonum(A i, out float3 kok)
        {
            kok = TransformObjectToWorld(float3(0, 0, 0));
            float3 ws = TransformObjectToWorld(i.positionOS.xyz);
            float t = saturate(i.color.a);
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
            float2 kay = (yon * egil + dik * sal * 0.05 * (0.3 + _RuzgarGuc)) * k * hgt * 0.8;
            ws.xy += kay;
            ws.z += dot(kay, kay) / max(hgt, 1e-4) * 0.5;
            return ws;
        }

        // Alfa: uzakta (mipmap) incelmesin diye ekran türeviyle keskinleştirilir
        half Alfa(float2 uv)
        {
            half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            return a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            AlphaToMask On
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
                float2 uv : TEXCOORD3;
                float2 kok : TEXCOORD4;
                float fog : TEXCOORD5;
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
                o.uv = i.uv;
                o.kok = kok.xy;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i, bool on : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Keskin kenar: eşik çevresinde alfayı piksel genişliğinde aç (AlphaToMask ile pürüzsüz, MSAA yoksa kesme gibi)
                half a = (tex.a - _Kesme) / max(fwidth(tex.a), 1e-4) + 0.5;
                clip(a - 0.01);
                float t = saturate(i.color.a);

                // Mevsim ve yama: dokunun rengini mevsimin uç rengine göre kaydır, kuru yamalarda samana çek
                float yama = Gurultu(i.kok * 0.32) * 0.6 + Gurultu(i.kok * 1.35 + 3.7) * 0.4;
                float kuru = saturate(_KuruOran + (yama - 0.5) * 0.9 + (Hs(i.kok * 5.31) - 0.5) * 0.4);
                float3 renk = tex.rgb;
                float yesillik = saturate((tex.g - max(tex.r, tex.b)) * 6.0);           // çiçek taçlarını etkileme
                float3 mevsim = _Uc.rgb / float3(0.50, 0.70, 0.27);                     // taze yeşile göre oran
                renk = lerp(renk, renk * mevsim, yesillik * 0.3);
                float lum = dot(renk, float3(0.3, 0.59, 0.11));
                renk = lerp(renk, lum * _Kuru.rgb * 1.55, kuru * 0.25 * yesillik);
                renk *= 1.18;   // doku sRGB'de koyu çizildi; oyun ışığında canlı yeşil görünsün
                renk *= 0.9 + 0.2 * Hs(i.kok * 91.7);
                renk = lerp(renk, float3(0.92, 0.94, 0.97), _Kar * smoothstep(0.3, 1.0, t) * 0.85);

                float3 n = SafeNormalize(i.normalWS) * (on ? 1.0 : -1.0);
                float3 N = SafeNormalize(lerp(n, float3(0, 0, -1), 0.75));
                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float atten = L.shadowAttenuation * L.distanceAttenuation;
                float ndl = dot(N, L.direction) * 0.5 + 0.5;
                float3 Vd = normalize(GetWorldSpaceViewDir(i.positionWS));
                float gecis = pow(saturate(dot(Vd, -L.direction)), 3.0) * 0.6 * t;
                float ao = lerp(0.7, 1.0, t);
                half3 c = renk * (SampleSH(N) * 0.9 * ao + L.color * atten * (ndl * 0.95 * ao + gecis));
                c = MixFog(c, i.fog);
                return half4(c, saturate(a));
            }
            ENDHLSL
        }
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
            struct V2 { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V2 vert(A i)
            {
                V2 o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 kok; o.positionCS = TransformWorldToHClip(CimKonum(i, kok)); o.uv = i.uv; return o;
            }
            half frag(V2 i) : SV_Target { clip(Alfa(i.uv) - _Kesme); return 0; }
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
            struct V2 { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            V2 vert(A i)
            {
                V2 o; UNITY_SETUP_INSTANCE_ID(i); UNITY_TRANSFER_INSTANCE_ID(i, o);
                float3 kok; o.positionCS = TransformWorldToHClip(CimKonum(i, kok)); o.uv = i.uv; return o;
            }
            half4 frag(V2 i) : SV_Target { clip(Alfa(i.uv) - _Kesme); return half4(0, 0, -1, 0) * 0.5 + 0.5; }
            ENDHLSL
        }
    }
}
