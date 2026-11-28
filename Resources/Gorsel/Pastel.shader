// Kurye Tycoon — Pastel (elle çizilmiş, kâğıt üstü pastel boya) gölgelendiricisi: Bina Atölyesi'nin ürettiği modeller için.
//  • Dokusuz düz renk; ışık yumuşak (yarım Lambert). Gölge tarafı kararmaz: aydınlık rengin _GolgeRengi katı (varsayılan ~%80,
//    hafif mor): pembe duvar gölgede gül kurusu olur. Ortam ışığı sıfır olan boş sahnede de böyle.
//  • Kâğıt tanesi, boya lekesi ve kalem taraması MODEL uzayında (metre) hesaplanır: atölyede (1 birim = 1 m) ve oyunda
//    (bina küçültülmüş) aynı görünür. Tarama gölgede belirgin, ışıkta çok hafif; uzaklaşınca silinir. Doku dosyası yok.
//  • Cam modu (_Cam = 1; "Pastel_Cam_" malzemeleri): camın üstünde bir iki beyaz çapraz parlama çizgisi.
//  • Köşe rengi (COLOR) tabanla çarpılır (atölye mesh'lerinde beyaz).
//  • URP/Lit ve AnimeToon ile aynı özellik adları (_BaseColor, _EmissionColor, _Surface, _Cull ...): oyunun gece ışıkları,
//    kilitli semt rengi, kar katmanı ve mevsim kodu değişmeden çalışır. Işıma her zaman eklenir.
//  • Çevre animasyonu (_Animasyon = 1; AtolyeKurucu adında animasyon işareti olan malzemelerde açar): köşe konumu
//    CevreAnimasyon.hlsl ile kaydırılır (rüzgârda salınan ağaç, titreşen yaprak, bayrak, akan su, dönen atlıkarınca).
//    Mod köşe renginin alfasında, sabitler TEXCOORD0.zw'de. Gölge, derinlik ve normal geçişleri de aynı kaymayı uygular
//    (gölge ve mürekkep konturu da oynar). Kâğıt dokusu kaymamış konuma bağlıdır (yüzeyde kaymaz).
//  • Kontur bu gölgelendiricide değil: mesh'in "Pastel_Cizgi" alt-mesh'i (KuryeTycoon/PastelCizgi) ya da oyunda ekran geçişi.
//  Malzemeler adla kurulur: "Pastel_<etiket>_<rrggbb>" (Editor/BinaAtolyesiKurucu.cs).
Shader "KuryeTycoon/Pastel"
{
    Properties
    {
        [MainColor] _BaseColor("Renk", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Doku", 2D) = "white" {}
        [HDR] _EmissionColor("Işıma", Color) = (0, 0, 0, 1)

        [Header(Isik)]
        _GolgeRengi("Gölge tarafı (aydınlığın katı)", Color) = (0.80, 0.76, 0.86, 1)
        _IsikRengi("Aydınlık (çarpan)", Color) = (1.05, 1.03, 0.98, 1)
        _Yumusak("Işık yumuşaklığı", Range(0.05, 1)) = 0.6
        _Ortam("Ortam ışığı payı", Range(0, 2)) = 0.9

        [Header(Kagit)]
        _KagitOlcek("Kâğıt tanesi ölçeği (1 / m)", Float) = 26
        _KagitGuc("Kâğıt tanesi", Range(0, 0.3)) = 0.07
        _LekeOlcek("Boya lekesi ölçeği (1 / m)", Float) = 0.9
        _LekeGuc("Boya lekesi", Range(0, 0.3)) = 0.06

        [Header(Tarama)]
        _TaramaSiklik("Tarama sıklığı (çizgi / m)", Float) = 14
        _TaramaGuc("Gölgede tarama", Range(0, 1)) = 0.16
        _IsiktaTarama("Işıkta tarama", Range(0, 0.3)) = 0.035
        _TaramaKalinlik("Tarama çizgi kalınlığı", Range(0.05, 0.9)) = 0.16

        [Header(Cam)]
        [Toggle] _Cam("Cam (çapraz parlama)", Float) = 0

        [Header(Mevsim)]
        _Dokum("Mevsim kırpması (0 yok, 1 kışın çizilmez, 2 yalnız kışın çizilir)", Float) = 0

        [Header(Animasyon)]
        [Toggle] _Animasyon("Çevre animasyonu (köşe alfası: mod)", Float) = 0

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
        half4 _IsikRengi;
        half _Yumusak;
        half _Ortam;
        float _KagitOlcek;
        half _KagitGuc;
        float _LekeOlcek;
        half _LekeGuc;
        float _TaramaSiklik;
        half _TaramaGuc;
        half _TaramaKalinlik;
        half _IsiktaTarama;
        half _Cam;
        half _Smoothness;
        half _Metallic;
        half _Surface;
        half _Dokum;
        half _Animasyon;
    CBUFFER_END

    // Kış (global; City3D mevsime göre, Bina Atölyesi "Kış önizlemesi" ile yazar): yaprak döken ağaçların yaprakları
    // (_Dokum = 1) çizilmez, dallardaki kar şeritleri (_Dokum = 2) yalnız o zaman çizilir. Bütün geçişlerde çağrılır
    // (gölge ve derinlik de kırpılsın: çıplak ağacın gölgesi ve mürekkep konturu da çıplak olsun).
    float _PastelKis;

    #include "CevreAnimasyon.hlsl"

    // Köşeyi (ve normalini) çevre animasyonuyla kaydırır; malzemede kapalıysa ya da köşenin modu yoksa dokunmaz
    void PastelAnimasyon(inout float3 po, inout float3 no, half4 renk, float4 uv)
    {
        if (_Animasyon > 0.5) CA_Uygula(po, no, renk.a, uv.zw);
    }

    void PastelMevsimKirp()
    {
        if (_Dokum > 1.5) clip(_PastelKis - 0.5);
        else if (_Dokum > 0.5) clip(0.5 - _PastelKis);
    }

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    // ---- değer gürültüsü (doku gerektirmez)
    float PS_Hash(float3 p)
    {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float PS_Gurultu(float3 x)
    {
        float3 i = floor(x);
        float3 f = frac(x);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(lerp(PS_Hash(i + float3(0, 0, 0)), PS_Hash(i + float3(1, 0, 0)), f.x),
                         lerp(PS_Hash(i + float3(0, 1, 0)), PS_Hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(PS_Hash(i + float3(0, 0, 1)), PS_Hash(i + float3(1, 0, 1)), f.x),
                         lerp(PS_Hash(i + float3(0, 1, 1)), PS_Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
    }

    // Haritada "yukarı" dünya −Z yönü (harita XY düzleminde, binalar −Z'ye yükselir)
    #define PS_YUKARI float3(0.0, 0.0, -1.0)
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
                float4 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogCoord : TEXCOORD3;
                half4 color : TEXCOORD4;
                float3 positionOS : TEXCOORD5;
                float3 normalOS : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 po = v.positionOS.xyz;
                float3 no = v.normalOS;
                PastelAnimasyon(po, no, v.color, v.uv);
                VertexPositionInputs p = GetVertexPositionInputs(po);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(no);
                o.uv = TRANSFORM_TEX(v.uv.xy, _BaseMap);
                o.fogCoord = ComputeFogFactor(p.positionCS.z);
                o.color = half4(v.color.rgb, 1.0h);
                o.positionOS = v.positionOS.xyz;      // kâğıt dokusu kaymamış konuma göre (yüzeye yapışık kalır)
                o.normalOS = v.normalOS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                PastelMevsimKirp();
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 taban = tex.rgb * _BaseColor.rgb * i.color.rgb;
                half alfa = tex.a * _BaseColor.a;

                float3 N = SafeNormalize(i.normalWS);            // sıfır normal (bozuk üçgen) NaN vermesin: bloom onu siyah daireye çevirir
                float3 pw = i.positionWS;

                // ---- yüzey üstünde iki eksen, MODEL uzayında (metre; model Y yukarı): duvarda yatay + dikey, düz yüzeyde X + Z
                float3 po = i.positionOS;
                float3 No = SafeNormalize(i.normalOS);
                float3 t1 = abs(No.y) > 0.9 ? float3(1.0, 0.0, 0.0) : normalize(cross(float3(0.0, 1.0, 0.0), No));
                float3 t2 = normalize(cross(No, t1));
                float2 yuzey = float2(dot(po, t1), dot(po, t2));

                // ---- cam: bir iki beyaz çapraz parlama çizgisi
                if (_Cam > 0.5)
                {
                    float f = frac((yuzey.x + yuzey.y * 0.55) / 1.9);
                    half bant = step(0.30, f) * step(f, 0.38) + step(0.44, f) * step(f, 0.47);
                    taban = lerp(taban, half3(1.0, 1.0, 1.0), bant * 0.75h);
                }

                // ---- kâğıt: ince tane + büyük sulu boya lekesi (renge hafif açık / koyu oynama)
                half tane = PS_Gurultu(float3(yuzey * _KagitOlcek, 1.7)) * 0.6h + PS_Gurultu(float3(yuzey * _KagitOlcek * 0.37, 5.3)) * 0.4h;
                half leke = PS_Gurultu(po * _LekeOlcek) * 0.65h + PS_Gurultu(po * _LekeOlcek * 2.7 + 11.0) * 0.35h;
                half taneGorunur = saturate(1.4h - fwidth(yuzey.x * _KagitOlcek) * 0.8h);     // uzakta tane kaybolur (kıpırdamasın)
                taban *= 1.0h + (tane - 0.5h) * 2.0h * _KagitGuc * taneGorunur + (leke - 0.5h) * 2.0h * _LekeGuc;

                // ---- ışık: yumuşak yarım Lambert. Gölge tarafı aydınlığın _GolgeRengi katı (koyulaşmaz, hafif mora kayar)
                half3 ortam = max(SampleSH(N), 0.0h) * _Ortam;
                float2 ekranUV = GetNormalizedScreenSpaceUV(i.positionCS);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(ekranUV);
                    ortam *= ao.indirectAmbientOcclusion;
                #endif
                float4 golgeKoord = TransformWorldToShadowCoord(pw);
                Light gunes = GetMainLight(golgeKoord);
                half nl = dot(N, gunes.direction);
                half isik = smoothstep(-_Yumusak, _Yumusak, nl) * gunes.shadowAttenuation * gunes.distanceAttenuation;
                half3 aydinlik = taban * _IsikRengi.rgb * (gunes.color * 0.8h + ortam * 0.5h);
                half3 golge = aydinlik * _GolgeRengi.rgb;
                half3 renk = lerp(golge, aydinlik, isik);

                // ---- kalem taraması: ince çapraz çizgiler (titrek); gölgede belirgin, ışıkta çok hafif, uzakta silinir
                float c = (yuzey.x + yuzey.y) * _TaramaSiklik + (leke - 0.5) * 2.2;
                half cizgi = 1.0h - smoothstep(_TaramaKalinlik * 0.5h, _TaramaKalinlik * 0.5h + 0.1h, abs(frac(c) - 0.5h));
                half taramaGorunur = saturate(1.5h - fwidth(c) * 1.4h) * (1.0h - _Cam);
                renk *= 1.0h - cizgi * ((1.0h - isik) * _TaramaGuc + _IsiktaTarama) * taramaGorunur;

                // ---- ek ışıklar (sokak lambaları): yumuşak katkı
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = pw;
                    inputData.normalizedScreenSpaceUV = ekranUV;
                    uint ekSay = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(ekSay)
                        Light ek = GetAdditionalLight(lightIndex, pw, half4(1, 1, 1, 1));
                        half e = saturate(dot(N, ek.direction) * 0.5h + 0.5h) * ek.distanceAttenuation * ek.shadowAttenuation;
                        renk += taban * ek.color * e;
                    LIGHT_LOOP_END
                #endif

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

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VG { float4 positionCS : SV_POSITION; };

            VG vertG(A v)
            {
                VG o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 po = v.positionOS.xyz;
                float3 no = v.normalOS;
                PastelAnimasyon(po, no, v.color, v.uv);
                float3 pw = TransformObjectToWorld(po);
                float3 nw = TransformObjectToWorldNormal(no);
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

            half4 fragG(VG i) : SV_Target { PastelMevsimKirp(); return 0; }
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

            struct A { float4 positionOS : POSITION; float4 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VD { float4 positionCS : SV_POSITION; };

            VD vertD(A v)
            {
                VD o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 po = v.positionOS.xyz;
                float3 no = float3(0.0, 1.0, 0.0);
                PastelAnimasyon(po, no, v.color, v.uv);
                o.positionCS = TransformObjectToHClip(po);
                return o;
            }

            half fragD(VD i) : SV_Target { PastelMevsimKirp(); return i.positionCS.z; }
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

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VN { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            VN vertN(A v)
            {
                VN o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 po = v.positionOS.xyz;
                float3 no = v.normalOS;
                PastelAnimasyon(po, no, v.color, v.uv);
                o.positionCS = TransformObjectToHClip(po);
                o.normalWS = TransformObjectToWorldNormal(no);
                return o;
            }

            half4 fragN(VN i) : SV_Target
            {
                PastelMevsimKirp();
                float3 n = SafeNormalize(i.normalWS);
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
