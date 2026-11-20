// Kurye Tycoon · canlı nehir suyu (URP). Dünyada yukarı −Z.
//  • Akıntı yönünde kayan iki ölçekli dalga gürültüsü → yüzey normali (güneşte parıltı, gölgede koyu)
//  • Derin / sığ renk dalgalanması, güneş yansıması, ufak parıltı benekleri
//  • Gece: ana ışık sönünce koyulaşır; ortam ışığı (SH) ile göğün rengini alır
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Derin, _Sig, _Parilti;
                float4 _Akinti;
                float _Olcek, _Egim;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
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

                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 Vd = normalize(GetWorldSpaceViewDir(i.positionWS));
                float ndl = saturate(dot(N, L.direction));
                float atten = L.shadowAttenuation * L.distanceAttenuation;

                half3 renk = lerp(_Derin.rgb, _Sig.rgb, saturate(h * 1.3 - 0.15));
                half3 ortam = SampleSH(N) * renk * 0.9;
                half3 isik = renk * L.color * (0.35 + 0.65 * ndl) * atten;
                float3 H = normalize(L.direction + Vd);
                float spec = pow(saturate(dot(N, H)), 90.0) * 1.6 * atten;
                // Küçük parıltı benekleri (güneş varken)
                float benek = step(0.93, Gurultu(p * 9.0 + float2(t * 0.6, -t * 0.4))) * step(0.5, Gurultu(p * 3.1 - t * 0.3));
                half3 c = ortam + isik + _Parilti.rgb * L.color * (spec + benek * 0.35 * atten * ndl);
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
