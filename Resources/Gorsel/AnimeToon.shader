// Kurye Tycoon — Anime (Ghibli tarzı boyalı) toon shader
//  • Işık 3 düz tonda: gölge (morumsu-mavi), ara ton (sıcak), aydınlık. Yumuşak geçiş yok, sınırlar fırça gibi pürüzlü.
//  • Fırça dokusu: dünya uzayında üç yönlü (triplanar) gürültü; ton sınırlarını oynatır ve renge hafif ton farkı katar
//    (elle boyanmış duvar, sıva ve çatı görünümü). Ölçek: 1 birim = bir kare (10 m).
//  • Gölgeler (güneş + binaların gölgesi) ve ekran ortam gölgesi (SSAO) desteklenir; kenarda ince ışık (rim).
//  • URP/Lit ile aynı özellik adları: _BaseColor, _BaseMap, _EmissionColor, _Surface, _Cull ... böylece oyunun gece
//    ışıkları, ıslak/kar tonlaması, kilitli semt rengi ve kar katmanı kodu değişmeden çalışır.
//  • Işıma (_EmissionColor) anahtar kelimesiz her zaman eklenir: gece pencereleri derlemede de yanar.
Shader "KuryeTycoon/AnimeToon"
{
    Properties
    {
        [MainColor] _BaseColor("Renk", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Doku", 2D) = "white" {}
        [HDR] _EmissionColor("Işıma", Color) = (0, 0, 0, 1)

        [Header(Anime tonlari)]
        _GolgeRengi("Gölge rengi (çarpan)", Color) = (0.56, 0.55, 0.78, 1)
        _AraRengi("Ara ton (çarpan)", Color) = (0.93, 0.84, 0.8, 1)
        _IsikRengi("Aydınlık (çarpan)", Color) = (1.06, 1.03, 0.97, 1)
        _Esik1("Gölge sınırı", Range(0, 1)) = 0.2
        _Esik2("Aydınlık sınırı", Range(0, 1)) = 0.55
        _Yumusak("Sınır yumuşaklığı", Range(0.001, 0.3)) = 0.035
        _Ortam("Ortam ışığı payı", Range(0, 2)) = 0.85

        [Header(Firca)]
        _FircaOlcek("Fırça ölçeği", Float) = 6
        _FircaGuc("Fırça sınır oynaması", Range(0, 0.6)) = 0.05
        _TonFarki("Renk ton farkı", Range(0, 0.4)) = 0.025

        [Header(Boyali dokular)]
        _Sarmasik("Duvar: sarmaşık", Range(0, 1)) = 0
        _SarmasikBoy("Sarmaşık yoğunluğu", Float) = 0.5
        _Cim("Zemin: fırçalı çim", Range(0, 1)) = 0
        _CatiFirca("Çatı: fırça dokusu", Range(0, 1)) = 0

        [Header(Kenar isigi)]
        _Rim("Kenar ışığı", Range(0, 1)) = 0.22

        // URP/Lit uyumu (oyun kodu bu adları kullanıyor)
        [HideInInspector] _Smoothness("Smoothness", Range(0, 1)) = 0.1
        [HideInInspector] _Metallic("Metallic", Range(0, 1)) = 0
        [HideInInspector] _Surface("__surface", Float) = 0
        [HideInInspector] _Blend("__blend", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [HideInInspector] _Cull("__cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _EmissionColor;
        half4 _GolgeRengi;
        half4 _AraRengi;
        half4 _IsikRengi;
        half _Esik1;
        half _Esik2;
        half _Yumusak;
        half _Ortam;
        float _FircaOlcek;
        half _FircaGuc;
        half _TonFarki;
        half _Rim;
        half _Sarmasik;
        float _SarmasikBoy;
        half _Cim;
        half _CatiFirca;
        half _Smoothness;
        half _Metallic;
        half _Surface;
    CBUFFER_END

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    // ---- değer gürültüsü (doku gerektirmez)
    float AT_Hash(float3 p)
    {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float AT_Gurultu(float3 x)
    {
        float3 i = floor(x);
        float3 f = frac(x);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(lerp(AT_Hash(i + float3(0, 0, 0)), AT_Hash(i + float3(1, 0, 0)), f.x),
                         lerp(AT_Hash(i + float3(0, 1, 0)), AT_Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(AT_Hash(i + float3(0, 0, 1)), AT_Hash(i + float3(1, 0, 1)), f.x),
                         lerp(AT_Hash(i + float3(0, 1, 1)), AT_Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
    }

    // Fırça darbesi: bir yönde uzatılmış iki katmanlı gürültü, yüz yönüne göre üç düzlemde karıştırılır
    float AT_Firca(float3 p, float3 n, float olcek)
    {
        float3 w = pow(abs(n), 4.0);
        w /= (w.x + w.y + w.z + 1e-4);
        float3 q = p * olcek;
        float a = AT_Gurultu(float3(q.y * 0.45, q.z, 3.1)) * 0.65 + AT_Gurultu(float3(q.y * 1.3, q.z * 2.6, 7.7)) * 0.35;
        float b = AT_Gurultu(float3(q.x, q.z * 0.45, 11.3)) * 0.65 + AT_Gurultu(float3(q.x * 2.6, q.z * 1.3, 5.9)) * 0.35;
        float c = AT_Gurultu(float3(q.x * 0.45, q.y, 17.9)) * 0.65 + AT_Gurultu(float3(q.x * 1.3, q.y * 2.6, 2.3)) * 0.35;
        return a * w.x + b * w.y + c * w.z;
    }

    // Haritada "yukarı" dünya −Z yönü (harita XY düzleminde, binalar −Z'ye yükselir); zemin z = 0
    #define AT_YUKARI float3(0.0, 0.0, -1.0)

    float AT_Hash2(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    // Boyalı dokular: taban rengini ışıktan önce değiştirir (sarmaşık, çim, çatı fırçası)
    half3 AT_Dokular(half3 taban, float3 pw, float3 N, inout half isikPay)
    {
        float yukari = dot(N, AT_YUKARI);
        float dikey = saturate(1.0 - abs(yukari) * 1.6);                 // duvarlarda 1
        float h = dot(pw, AT_YUKARI);                                    // yerden yükseklik

        if (_Sarmasik > 0.001 && dikey > 0.5)
        {
            float3 t = normalize(cross(AT_YUKARI, N) + 1e-5);
            float u = dot(pw, t);

            // --- sarmaşık: duvarda yer yer, kenarı tırtıklı yaprak lekeleri; aşağıda daha çok, yukarı doğru seyrek
            if (_Sarmasik > 0.001 && dikey > 0.5)
            {
                float leke = AT_Gurultu(float3(u * 3.0, h * 2.2, 5.3)) * 0.7 + AT_Gurultu(float3(u * 9.0, h * 6.0, 9.1)) * 0.3;
                float yaprak = AT_Gurultu(float3(u * 26.0, h * 26.0, 2.2)) * 0.55 + AT_Gurultu(float3(u * 11.0, h * 11.0, 6.6)) * 0.45;
                float esik = 0.72 - _SarmasikBoy * 0.2 + saturate(h) * 0.12;
                float sarmasik = smoothstep(esik, esik + 0.025, leke + (yaprak - 0.5) * 0.14);
                half3 koyu = half3(0.16, 0.31, 0.18), acik = half3(0.45, 0.64, 0.3);
                half3 yaprakRenk = lerp(koyu, acik, smoothstep(0.42, 0.66, yaprak + (leke - esik) * 1.5));
                taban = lerp(taban, yaprakRenk, sarmasik * _Sarmasik * dikey);
            }
        }

        // --- fırçalı çim: yatay zeminde eğik kısa fırça darbeleri, açık lekeler sarıya kayar
        if (_Cim > 0.001 && yukari > 0.5)
        {
            float2 p = pw.xy * 18.0;
            float2 r = float2(p.x * 0.866 - p.y * 0.5, p.x * 0.5 + p.y * 0.866);
            float s1 = AT_Gurultu(float3(r.x * 0.28, r.y * 1.5, 1.3)) * 0.6 + AT_Gurultu(float3(r.x * 0.7, r.y * 3.4, 4.4)) * 0.4;
            float buyuk = AT_Gurultu(float3(pw.xy * 1.3, 8.8)) * 0.6 + AT_Gurultu(float3(pw.xy * 4.0, 2.9)) * 0.4;
            float leke = smoothstep(0.5, 0.6, s1) - (1.0 - smoothstep(0.32, 0.42, s1));
            float gorunur = saturate(1.6 - fwidth(r.y) * 0.9);
            half3 c = taban * (0.86h + buyuk * 0.28h);
            c *= 1.0h + leke * 0.24h * gorunur;
            c = lerp(c, c * half3(1.1, 1.07, 0.8), saturate(leke) * 0.6 * gorunur + saturate(buyuk - 0.6) * 0.5);
            taban = lerp(taban, c, _Cim);
        }

        // --- çatı: eğim boyunca uzun fırça izleri, ton farkı
        if (_CatiFirca > 0.001 && yukari > 0.15)
        {
            float3 egim = normalize(AT_YUKARI - N * yukari + 1e-5);          // çatının aşağı-yukarı yönü
            float3 yan = normalize(cross(N, egim) + 1e-5);
            float a = dot(pw, yan) * 26.0, b = dot(pw, egim) * 26.0;
            float iz = AT_Gurultu(float3(a * 1.2, b * 0.18, 3.3)) * 0.6 + AT_Gurultu(float3(a * 3.0, b * 0.4, 7.1)) * 0.4;
            float gorunur = saturate(1.6 - fwidth(a) * 0.5);
            taban *= 1.0h + (iz - 0.5h) * 0.5h * _CatiFirca * gorunur;
        }
        return taban;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Bir ışığın 0..1 aydınlatmasını 3 düz tona çevirir (fırça ile pürüzlü sınırlar)
            half3 AT_Tonla(half3 taban, half isik, half firca, half3 isikRengi, half3 ortam)
            {
                half l = isik + (firca - 0.5h) * _FircaGuc;
                half b1 = smoothstep(_Esik1 - _Yumusak, _Esik1 + _Yumusak, l);
                half b2 = smoothstep(_Esik2 - _Yumusak, _Esik2 + _Yumusak, l);
                half3 golge = taban * _GolgeRengi.rgb * ortam;
                half3 aydinlik = taban * _IsikRengi.rgb * (isikRengi + ortam * 0.35h);
                half3 ara = lerp(golge, aydinlik, 0.6h) * _AraRengi.rgb;
                return lerp(lerp(golge, ara, b1), aydinlik, b2);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 taban = tex.rgb * _BaseColor.rgb;
                half alfa = tex.a * _BaseColor.a;

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half firca = AT_Firca(i.positionWS, N, _FircaOlcek);
                half firca2 = AT_Firca(i.positionWS + 13.7, N, _FircaOlcek * 0.37);
                taban *= 1.0h + (firca2 - 0.5h) * 2.0h * _TonFarki;
                half isikPay = 1.0h;
                taban = AT_Dokular(taban, i.positionWS, N, isikPay);

                // Ortam ışığı (gökyüzü/zemin) + ekran ortam gölgesi
                half3 ortam = max(SampleSH(N), 0.0h) * _Ortam;
                float2 ekranUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(ekranUV);
                    ortam *= ao.indirectAmbientOcclusion;
                #endif

                // Güneş
                float4 golgeKoord = TransformWorldToShadowCoord(i.positionWS);
                Light gunes = GetMainLight(golgeKoord);
                half gIsik = saturate(dot(N, gunes.direction)) * gunes.shadowAttenuation * gunes.distanceAttenuation;
                half3 renk = AT_Tonla(taban, gIsik, firca, gunes.color, ortam);

                // Ek ışıklar (sokak lambaları, araba farları): yumuşak toon katkı
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = ekranUV;
                    uint ekSay = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(ekSay)
                        Light ek = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        half e = saturate(dot(N, ek.direction)) * ek.distanceAttenuation * ek.shadowAttenuation;
                        e = smoothstep(0.05h, 0.25h, e + (firca - 0.5h) * 0.1h);
                        renk += taban * ek.color * e;
                    LIGHT_LOOP_END
                #endif

                // Kenar ışığı: aydınlık tarafta ince parlak kenar
                half rim = pow(1.0h - saturate(dot(N, V)), 4.0h) * _Rim * saturate(gIsik * 2.0h);
                renk += taban * gunes.color * rim;

                renk += _EmissionColor.rgb;
                renk = MixFog(renk, i.fogCoord);
                return half4(renk, alfa);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertG
            #pragma fragment fragG
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VG { float4 positionCS : SV_POSITION; };

            VG vertG(A v)
            {
                VG o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pw = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 ld = normalize(_LightPosition - pw);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(pw, nw, ld));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }

            half4 fragG(VG i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertD
            #pragma fragment fragD
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VD { float4 positionCS : SV_POSITION; };

            VD vertD(A v)
            {
                VD o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half fragD(VD i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertN
            #pragma fragment fragN
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VN { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            VN vertN(A v)
            {
                VN o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 fragN(VN i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    float2 r = saturate(oct * 0.5 + 0.5);
                    return half4(PackFloat2To888(r), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
