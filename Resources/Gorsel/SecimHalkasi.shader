// Kurye Tycoon — seçim halkası (haritada tıklanan dükkân, bina ya da kurye)
//  • Zemine yatırılmış dörtgen üstünde elle çizilmiş gibi hafif dalgalı halka: sıcak kehribar çizgi, ince koyu mürekkep kenar,
//    içi çok hafif aydınlık. Halka nabız gibi hafifçe genişleyip daralır, dışa doğru sönen ikinci bir dalga yayılır.
//  • Işıktan etkilenmez (gece de okunur), derinlik yazmaz, binaların arkasında kalır. SecimHalkasi.cs çizer.
Shader "KuryeTycoon/SecimHalkasi"
{
    Properties
    {
        _Renk("Halka", Color) = (1, 0.78, 0.36, 1)
        _Murekkep("Mürekkep kenar", Color) = (0.10, 0.07, 0.06, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-40" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Renk;
                half4 _Murekkep;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V vert(A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float2 q = i.uv * 2.0 - 1.0;
                float r = length(q);
                float aci = atan2(q.y, q.x);
                float t = _Time.y;
                float aa = max(fwidth(r) * 1.25, 0.004);

                // Nabız: halka 0,78 → 0,84 arasında nefes alır; elle çizilmiş gibi açıya göre hafif dalgalı
                float R = 0.78 + 0.06 * (0.5 + 0.5 * sin(t * 3.2)) + 0.012 * sin(aci * 5.0 + t * 0.8) + 0.006 * sin(aci * 11.0 - t);
                float yari = 0.05;
                float d = abs(r - R);
                float halka = 1.0 - smoothstep(yari - aa, yari + aa, d);
                float kenar = 1.0 - smoothstep(yari + 0.025 - aa, yari + 0.025 + aa, d);    // mürekkep: halkadan biraz geniş

                // Dışa yayılan dalga (her 1,6 saniyede bir), uzaklaştıkça söner
                float f = frac(t * 0.62);
                float Rd = lerp(R, 1.0, f);
                float dalga = (1.0 - smoothstep(0.0, 0.025 + aa, abs(r - Rd))) * (1.0 - f) * 0.55;

                // İçi çok hafif aydınlık
                float ic = (1.0 - smoothstep(R - 0.25, R - yari, r)) * 0.10;

                half3 c = lerp(_Murekkep.rgb, _Renk.rgb, saturate(halka + dalga + ic * 3.0));
                half a = saturate(kenar * 0.95 + dalga + ic);
                a *= 1.0 - smoothstep(0.97, 1.0, r);                         // dörtgenin kenarına taşmasın
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
