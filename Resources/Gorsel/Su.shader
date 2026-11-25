// Kurye Tycoon · canlı nehir suyu (URP). Dünyada yukarı −Z.
//  • Akıntı yönünde kayan iki ölçekli dalga gürültüsü → yüzey normali (güneşte parıltı, gölgede koyu)
//  • Sığ / derin renk: kıyıda açık turkuaz, ortada koyu (nehir yatağı derinliği, Arazi.NehirYatak → SuKiyisi dokusu)
//  • Kıyıda ince beyaz köpük: yatağın sığ kenarında, akıntıyla kıpırdayan iki ölçekli gürültüyle kırılır
//  • Gökyüzü yansıması: Gorsel3D'nin saate göre gök ve ufuk rengi (_AnimeGok, _AnimeUfuk); eğik bakışta daha çok
//  • Güneş yansıması, ufak parıltı benekleri. Gece: ana ışık sönünce koyulaşır, lacivert gök yansır
//  • Kıyı dokusu yoksa (_KuryeKiyiVar = 0) köpük yok, renk eskisi gibi dalgaya göre
Shader "KuryeTycoon/Su"
{
    Properties
    {
        _Derin("Derin renk", Color) = (0.14, 0.40, 0.55, 1)
        _Sig("Sığ renk", Color) = (0.33, 0.63, 0.76, 1)
        _Parilti("Parıltı", Color) = (1, 0.97, 0.90, 1)
        _Akinti("Akıntı (x, y)", Vector) = (0.035, 0.02, 0, 0)
        _Olcek("Dalga ölçeği", Float) = 2.4
        _Egim("Dalga eğimi", Float) = 0.9
        _Kopuk("Kıyı köpüğü", Color) = (0.95, 0.97, 0.96, 1)
        _KopukGenislik("Köpük genişliği (yatak derinliği)", Range(0.05, 1)) = 0.4
        _Yansima("Gökyüzü yansıması", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Derin, _Sig, _Parilti, _Kopuk;
                float4 _Akinti;
                float _Olcek, _Egim, _KopukGenislik, _Yansima;
            CBUFFER_END

            // Kodun verdiği ortak değerler (malzemeye ait değil)
            TEXTURE2D(_KuryeKiyi);
            SAMPLER(sampler_KuryeKiyi);
            float4 _KuryeKiyiOlcek;     // uv = dünya xy * xy + zw (SuKiyisi.cs)
            float _KuryeKiyiVar;        // doku hazır mı (0: köpük yok)
            half4 _AnimeGok;            // Gorsel3D: saate göre gök
            half4 _AnimeUfuk;           // Gorsel3D: saate göre ufuk

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fogCoord : TEXCOORD1; };

            V vert(A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Hs(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Gurultu(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hs(i), Hs(i + float2(1, 0)), u.x), lerp(Hs(i + float2(0, 1)), Hs(i + float2(1, 1)), u.x), u.y);
            }
            float Dalga(float2 p, float t)
            {
                float2 a = p * _Olcek + _Akinti.xy * t * 12.0;
                float2 b = p * _Olcek * 2.7 - _Akinti.yx * t * 18.0 + 5.3;
                return Gurultu(a) * 0.62 + Gurultu(b) * 0.38;
            }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                float2 p = i.positionWS.xy;
                const float e = 0.03;
                float h = Dalga(p, t);
                float hx = Dalga(p + float2(e, 0), t);
                float hy = Dalga(p + float2(0, e), t);
                // Yukarı −Z: düz yüzeyin normali (0, 0, −1)
                float3 N = normalize(float3(-(hx - h) / e * _Egim * 0.05, -(hy - h) / e * _Egim * 0.05, -1.0));

                // Nehir yatağının derinliği: 0 kıyıda, 1 ortada (doku yoksa her yer derin)
                float2 kuv = p * _KuryeKiyiOlcek.xy + _KuryeKiyiOlcek.zw;
                float icerde = step(0.5, _KuryeKiyiVar) * step(0.0, kuv.x) * step(kuv.x, 1.0) * step(0.0, kuv.y) * step(kuv.y, 1.0);
                float yatakDoku = SAMPLE_TEXTURE2D_LOD(_KuryeKiyi, sampler_KuryeKiyi, saturate(kuv), 0).r;
                float yatak = lerp(1.0, yatakDoku, icerde);

                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 Vd = normalize(GetWorldSpaceViewDir(i.positionWS));
                float ndl = saturate(dot(N, L.direction));
                float atten = L.shadowAttenuation * L.distanceAttenuation;

                // Sığ kıyı açık, orta koyu; dalga tepeleri biraz açılır
                // (kıyı dokusu yoksa eskisi gibi: derin renk, dalga tepesinde sığ renk)
                float dalgaTepe = saturate(h * 1.3 - 0.15);
                half3 renk = lerp(_Sig.rgb, _Derin.rgb, lerp(1.0, smoothstep(0.0, 0.85, yatak), icerde));
                renk = lerp(renk, _Sig.rgb, dalgaTepe * lerp(1.0, 0.45, icerde));
                half3 ortam = SampleSH(N) * renk * 0.9;
                half3 isik = renk * L.color * (0.35 + 0.65 * ndl) * atten;
                float3 H = normalize(L.direction + Vd);
                float spec = pow(saturate(dot(N, H)), 90.0) * 1.6 * atten;
                // Küçük parıltı benekleri (güneş varken)
                float benek = step(0.93, Gurultu(p * 9.0 + float2(t * 0.6, -t * 0.4))) * step(0.5, Gurultu(p * 3.1 - t * 0.3));
                half3 c = ortam + isik;

                // Gökyüzü yansıması: eğik bakışta ufuk, dik bakışta gök rengi (gece lacivert, akşam turuncu)
                half3 gok = _AnimeGok.rgb, ufuk = _AnimeUfuk.rgb;
                float gokVar = step(0.001, gok.r + gok.g + gok.b);
                gok = lerp(half3(0.62, 0.74, 0.86), gok, gokVar);
                ufuk = lerp(half3(0.80, 0.78, 0.72), ufuk, gokVar);
                float ndv = saturate(dot(N, Vd));
                float fresnel = (1.0 - ndv) * (1.0 - ndv);
                c = lerp(c, lerp(gok, ufuk, fresnel) * 1.05, _Yansima * (0.22 + 0.6 * fresnel));

                // Kıyı köpüğü: sığ kenarda, gürültüyle kırık; en kenarda ince beyaz çizgi
                float kiyi = 1.0 - smoothstep(0.0, _KopukGenislik, yatak);
                float n1 = Gurultu(p * 16.0 + float2(t * 0.35, -t * 0.22));
                float n2 = Gurultu(p * 41.0 - float2(t * 0.6, t * 0.4));
                float kopuk = smoothstep(0.45, 0.6, kiyi * 1.1 + (n1 - 0.5) * 0.55 + (n2 - 0.5) * 0.25) * 0.7;
                float cizgi = smoothstep(0.86, 0.97, kiyi + (n2 - 0.5) * 0.1);
                kopuk = saturate(max(kopuk, cizgi)) * icerde;
                half3 kopukIsik = SampleSH(float3(0, 0, -1)) * 0.85 + L.color * (0.35 + 0.65 * saturate(-L.direction.z)) * atten;
                c = lerp(c, _Kopuk.rgb * kopukIsik, kopuk);

                c += _Parilti.rgb * L.color * (spec + benek * 0.35 * atten * ndl) * (1.0 - kopuk);
                c = MixFog(c, i.fogCoord);
                return half4(c, 1);
            }
            ENDHLSL
        }
        // Gölge düşürmez; derinlik geçişi (mürekkep çizgisi ve sis derinliği okur)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            float4 vert(A i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; };
            float4 vert(A i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half4 frag() : SV_Target { return half4(0, 0, -1, 0) * 0.5 + 0.5; }
            ENDHLSL
        }
    }
}
