// Kurye Tycoon — Mürekkep çizgisi ve boyalı renk ayarı (tam ekran geçiş, URP Full Screen Pass Renderer Feature)
//  • Derinlik ve normal farkından kenar bulur: bina silüeti, çatı kenarı, pencere çerçevesi, ağaç kümeleri.
//  • Çizgi elle çizilmiş gibi: kalınlığı ve koyuluğu ekran gürültüsüyle titrer, uzakta incelir.
//  • Renk ayarı (anime/Ghibli): biraz daha doygun, gölgeler mavi-mora, ışıklar sıcağa kayar; hafif kâğıt dokusu ve kenar karartması.
Shader "KuryeTycoon/MurekkepCizgi"
{
    Properties
    {
        _Murekkep("Mürekkep rengi", Color) = (0.17, 0.13, 0.12, 1)
        _Kalinlik("Çizgi kalınlığı (piksel)", Range(0.5, 4)) = 1.35
        _DerinlikEsik("Derinlik eşiği", Range(0.001, 0.2)) = 0.025
        _NormalEsik("Normal eşiği", Range(0.05, 1.5)) = 0.45
        _Titreme("Elle çizim titremesi", Range(0, 1)) = 0.55
        _Doygunluk("Doygunluk", Range(0.5, 1.6)) = 1.12
        _GolgeMavi("Gölgeleri maviye kaydır", Range(0, 1)) = 0.35
        _IsikSicak("Işıkları sıcağa kaydır", Range(0, 1)) = 0.3
        _Kagit("Kâğıt dokusu", Range(0, 0.2)) = 0.035
        _Vinyet("Kenar karartması", Range(0, 1)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "MurekkepCizgi"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _Murekkep;
            float _Kalinlik;
            float _DerinlikEsik;
            float _NormalEsik;
            float _Titreme;
            float _Doygunluk;
            float _GolgeMavi;
            float _IsikSicak;
            float _Kagit;
            float _Vinyet;

            float MC_Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float MC_Gurultu(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(MC_Hash(i), MC_Hash(i + float2(1, 0)), f.x),
                            lerp(MC_Hash(i + float2(0, 1)), MC_Hash(i + float2(1, 1)), f.x), f.y);
            }

            float MC_Goz(float ham)
            {
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        ham = 1.0 - ham;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, ham);
                }
                return LinearEyeDepth(ham, _ZBufferParams);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 renk = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 piksel = uv * _ScreenParams.xy;
                // Elle çizim: kalınlık ekranda yavaşça değişir, çizgiler bazen incelir bazen kalınlaşır
                float titre = MC_Gurultu(piksel * 0.045) * 0.7 + MC_Gurultu(piksel * 0.17) * 0.3;
                float kal = _Kalinlik * lerp(1.0, 0.55 + titre * 0.9, _Titreme);
                float2 d = _ScreenSize.zw * kal;

                float ham0 = SampleSceneDepth(uv);
                float z0 = MC_Goz(ham0);
                bool gokyuzu = ham0 == 0.0 || ham0 == 1.0;

                float2 ofs[4] = { float2(d.x, 0), float2(-d.x, 0), float2(0, d.y), float2(0, -d.y) };
                float3 n0 = SampleSceneNormals(uv);
                float dz = 0.0;
                float dn = 0.0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float z = MC_Goz(SampleSceneDepth(uv + ofs[k]));
                    dz = max(dz, (z - z0) / max(z0, 1e-3));          // yalnız arkadaki komşu: silüet tek tarafta, ince kalır
                    float3 n = SampleSceneNormals(uv + ofs[k]);
                    dn = max(dn, 1.0 - saturate(dot(n, n0)));
                }
                float kenarZ = smoothstep(_DerinlikEsik, _DerinlikEsik * 2.5, dz);
                float kenarN = smoothstep(_NormalEsik, _NormalEsik * 1.6, dn);
                float kenar = saturate(max(kenarZ, kenarN));
                // Uzakta (yakınlaştırılmamış harita) çizgiler hafiflesin
                kenar *= saturate(1.4 - z0 / 60.0);
                // Mürekkep koyuluğu da titrer: bazı yerlerde çizgi kesik kesik
                kenar *= lerp(1.0, smoothstep(0.15, 0.55, MC_Gurultu(piksel * 0.08 + 7.0)), _Titreme * 0.6);

                // ---- boyalı renk ayarı
                half3 c = renk.rgb;
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Doygunluk);
                half golgeAgir = 1.0h - smoothstep(0.05h, 0.45h, l);
                half isikAgir = smoothstep(0.55h, 1.0h, l);
                c = lerp(c, c * half3(0.86, 0.9, 1.12), golgeAgir * _GolgeMavi);
                c = lerp(c, c * half3(1.07, 1.02, 0.92), isikAgir * _IsikSicak);
                // kâğıt / tuval dokusu
                half kagit = MC_Gurultu(piksel * 0.9) * 0.6 + MC_Gurultu(piksel * 0.23) * 0.4;
                c *= 1.0h + (kagit - 0.5h) * _Kagit * 2.0h;
                // kenar karartması
                float2 m = uv - 0.5;
                c *= 1.0h - dot(m, m) * _Vinyet;

                if (!gokyuzu)
                    c = lerp(c, c * 0.35h + _Murekkep.rgb * 0.65h, kenar * _Murekkep.a);
                return half4(c, renk.a);
            }
            ENDHLSL
        }
    }
}
