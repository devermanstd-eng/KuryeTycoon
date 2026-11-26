// Kurye Tycoon — ağaçların mürekkep çizgi kabuğu (rüzgârda salınan)
//  • Ağaç modellerinin siyah çizgi kabuğu URP/Unlit ile çizilir; yapraklar (AnimeToon, _Salinim = 1) rüzgârda sallanınca kabuk
//    yerinde kalıp ağaçtan ayrılmasın diye ağaç kopyalarında bu gölgelendirici kullanılır (AgacSalinimi.cs).
//  • Unlit ile aynı: tek renk (_BaseColor), ışık yok, sis var, gölge düşürmez. Kayma AnimeToon.shader'daki AT_Salinim ile aynı.
Shader "KuryeTycoon/CizgiSalinim"
{
    Properties
    {
        [MainColor] _BaseColor("Renk", Color) = (0.08, 0.08, 0.08, 1)
        _Salinim("Rüzgârda salınım", Range(0, 1)) = 1
        [HideInInspector] _Cull("__cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        half _Salinim;
    CBUFFER_END

    float _KuryeSalinim;
    float4 _KuryeRuzgarYon;

    // AnimeToon.shader > AT_Salinim ile birebir aynı (değiştirirsen ikisini birlikte değiştir)
    float3 CS_Salinim(float3 pw)
    {
        float guc = _Salinim * _KuryeSalinim;
        float3 kok = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
        float h = saturate((kok.z - pw.z) * 2.0);
        float faz = dot(kok.xy, float2(1.7, 2.3));
        float t = _Time.y;
        float dalga = sin(t * 1.3 + faz) * 0.65 + sin(t * 2.9 + faz * 1.9) * 0.35;
        pw.xy += _KuryeRuzgarYon.xy * (guc * h * h * (dalga * 0.6 + 0.4));
        return pw;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Unlit" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float fogCoord : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(CS_Salinim(TransformObjectToWorld(v.positionOS.xyz)));
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, i.fogCoord), 1.0);
            }
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(CS_Salinim(TransformObjectToWorld(v.positionOS.xyz)));
                return o;
            }

            half frag(V i) : SV_Target { return i.positionCS.z; }
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            V vert(A v)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(CS_Salinim(TransformObjectToWorld(v.positionOS.xyz)));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 n = SafeNormalize(i.normalWS);   // sıfır normal NaN vermesin (siyah daireler)
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
