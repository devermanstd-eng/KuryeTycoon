// Kurye Tycoon — yere düşen yumuşak ışık (gece far huzmeleri)
//  • Aracın önündeki zemine yatırılmış dörtgen: uv.x boyuna (0 farda, 1 ucunda), uv.y enine. Huzme ilerledikçe genişler,
//    kenarları ve ucu yumuşakça söner. Toplamalı (eklenir): karanlık yerde parlar, aydınlık yerde kaybolur.
//  • Gerçek ışık kaynağı değil (yüzlerce kurye ve araba için ucuz): Forward'da ışık sınırına takılmaz. FarHuzmeleri.cs çizer.
//  • Derinlik yazmaz, binaların arkasında kalır (ZTest), zemine yapışmasın diye biraz öne itilir (Offset).
Shader "KuryeTycoon/YerIsigi"
{
    Properties
    {
        _Renk("Renk", Color) = (1, 0.86, 0.62, 1)
        _Guc("Güç", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Renk;
                half _Guc;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fogCoord : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float u = i.uv.x;                                   // boyuna: 0 farda, 1 ucunda
                float yari = lerp(0.16, 0.5, u);                    // huzme ilerledikçe genişler
                float k = abs(i.uv.y - 0.5) / yari;
                float yan = saturate(1.0 - k * k);
                float boy = smoothstep(0.0, 0.1, u) * (1.0 - smoothstep(0.35, 1.0, u));
                float a = yan * yan * boy;
                float3 c = (float3)_Renk.rgb * (_Guc * a);
                c = MixFogColor(c, float3(0, 0, 0), i.fogCoord);    // siste söner (toplamalı: siyaha karışır)
                return half4(c, 0);
            }
            ENDHLSL
        }
    }
}
