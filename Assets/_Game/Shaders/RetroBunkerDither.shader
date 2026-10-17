// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// RetroBunkerDither — финальный вывод кадра в стиле Crisp Retro-PBR (Art Bible, раздел 6).
// Сцена рендерится в низкое разрешение (960x540) с FilterMode.Point, этот шейдер
// выводит её на экран и придаёт ретро-характер: упорядоченный дизеринг Байера 8x8,
// постеризация, мягкий контур, виньетка и зерно.
//
// Ключевое требование: НИКАКИХ добавок яркости. Дизеринг и зерно умножаются на
// shadowMask, который строго равен нулю на тёмных пикселях. Без этого матрица Байера
// превращала чёрные тени в вертикальные серые полосы — «штрихкод» по всему экрану.
//
// Шейдер работает как материал полноэкранного RawImage: вершинный шейдер читает
// POSITION и uv из меша CanvasRenderer, а не SV_VertexID.

Shader "WorldCrisis/Retro Bunker Dither"
{
    Properties
    {
        // Источник: низкоразрешённая RenderTexture сцены. Имя обязательно _MainTex —
        // именно в это свойство UnityEngine.UI.RawImage отдаёт свою текстуру
        // через Graphic.mainTexture, любое другое имя останется незаполненным.
        _MainTex("Retro Source", 2D) = "black" {}
        [HideInInspector] _MainTex_TexelSize("Retro Source Texel Size", Vector) = (0.001, 0.001, 960, 540)

        // Необязательная карта глубины: по ключу _RETRO_DEPTH усиливается контур.
        _RetroDepth("Retro Depth (optional)", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            // Без LightMode: шейдер берётся и обычным рендером, и Canvas-рендерером (RawImage).
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            Name "RetroBunkerDither"

            HLSLPROGRAM
            #pragma vertex RetroVert
            #pragma fragment RetroFrag
            #pragma target 3.0
            #pragma multi_compile_local _ _RETRO_DEPTH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_RetroDepth);
            SAMPLER(sampler_RetroDepth);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _RetroDepth_TexelSize;
            CBUFFER_END

            // Алгоритм вывода. Значения зафиксированы: они подобраны по скриншотам
            // и меняются только вместе с Art Bible.
            static const float3 InkColor = float3(0.03, 0.03, 0.04);
            static const float EdgeLumaGain = 2.5;
            static const float EdgeDepthGain = 15.0;
            static const float EdgeThreshold = 0.15;
            static const float EdgeStrength = 0.65;
            static const float BayerAmplitude = 0.035;
            static const float ColorSteps = 32.0;
            static const float GrainAmplitude = 0.018;
            static const float MidtoneGain = 1.08;
            static const float VignetteFalloff = 0.28;
            static const float VignetteFloor = 0.55;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RetroVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // --- Вспомогательные функции -------------------------------------------

            float3 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
            }

            float LumaAt(float2 uv)
            {
                return dot(SampleSource(uv), float3(0.2126, 0.7152, 0.0722));
            }

            /// Упорядоченный дизеринг Байера: матрица 2^bits x 2^bits считается
            /// битовым чередованием координат пикселя, без выборки из массива.
            float GetBayer8x8(uint2 pixel, int bits)
            {
                uint index = 0u;
                uint x = pixel.x;
                uint y = pixel.y;

                for (int i = 0; i < bits; i++)
                {
                    uint xBit = (x >> i) & 1u;
                    uint yBit = (y >> i) & 1u;
                    index = (index << 2) | ((xBit ^ yBit) << 1) | (1u - yBit);
                }

                // Внимание: дробный литерал нельзя писать с суффиксом u (0.5u) — такого
                // литерала в HLSL не существует, и компилятор роняет всю функцию.
                return (float)(index + 0.5f) / (float)(1u << (2 * bits));
            }

            /// Разница яркости с четырьмя соседями: сумма разностей по обеим осям.
            float2 ComputeEdge(float2 uv, float2 texel)
            {
                float lumaDiff =
                      abs(LumaAt(uv + float2(texel.x, 0.0)) - LumaAt(uv - float2(texel.x, 0.0)))
                    + abs(LumaAt(uv + float2(0.0, texel.y)) - LumaAt(uv - float2(0.0, texel.y)));

                float depthDiff = 0.0;

                #if defined(_RETRO_DEPTH)
                    float center = LinearEyeDepth(
                        SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv).r, _ZBufferParams);

                    float z1 = LinearEyeDepth(
                        SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv + float2(texel.x, 0.0)).r, _ZBufferParams);
                    float z2 = LinearEyeDepth(
                        SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv - float2(texel.x, 0.0)).r, _ZBufferParams);
                    float z3 = LinearEyeDepth(
                        SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv + float2(0.0, texel.y)).r, _ZBufferParams);
                    float z4 = LinearEyeDepth(
                        SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv - float2(0.0, texel.y)).r, _ZBufferParams);

                    float zMax = max(max(z1, z2), max(z3, z4));
                    float zMin = min(min(z1, z2), min(z3, z4));
                    depthDiff = (zMax - zMin) / max(center, 0.01);
                #endif

                return float2(lumaDiff, depthDiff);
            }

            /// Interleaved Gradient Noise: зерно без вертикальной и горизонтальной
            /// решётки, в отличие от обычного хеша по пикселю.
            float IgnNoise(float2 pixelCoord)
            {
                return frac(52.9829189 * frac(dot(pixelCoord, float2(0.06711056, 0.00583715)))) - 0.5;
            }

            float4 RetroFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float2 texel = _MainTex_TexelSize.xy;

                // 1. Исходный цвет.
                float3 col = saturate(SampleSource(uv));

                // 2. Яркость пикселя.
                float luma = dot(col, float3(0.2126, 0.7152, 0.0722));

                // 3. Маска теней. На тёмных пикселях дизеринг и зерно строго нулевые —
                //    именно это убирает вертикальные серые полосы на чёрном фоне.
                float shadowMask = smoothstep(0.04, 0.22, luma);

                // 4. Мягкая коррекция средних тонов. Линейное умножение, а НЕ pow(col, 0.85):
                //    pow поднял бы 0.01 в заметный серый и вернул «шум на чёрном».
                col = saturate(col * MidtoneGain);

                // 5. Контур: только мягкое затемнение границ, никаких добавок белого.
                float2 edgeDiff = ComputeEdge(uv, texel);
                float edge = saturate((edgeDiff.x * EdgeLumaGain + edgeDiff.y * EdgeDepthGain) - EdgeThreshold);
                col = lerp(col, InkColor, edge * EdgeStrength);

                // 6. Виньетка — ДО подъёма теней, иначе она снова обнулила бы углы.
                float2 d = (uv - 0.5) * 2.0;
                float vig = saturate(1.0 - dot(d, d) * VignetteFalloff);
                col *= lerp(VignetteFloor, 1.0, vig);

                // 7. Мягкий подъём глубоких теней перед квантованием. Тёмные детали
                //    интерьера (тени под столом, ниша двери, дальняя стена) давали
                //    0.06 * 0.15 ≈ 0.009, и floor(col * 32 + 0.5) округлял их в чистый
                //    ноль — комната выглядела вырезанной по контуру. Нижняя планка
                //    поднимает тени над нулём, но остаётся глубокой.
                col = max(col, float3(0.025, 0.024, 0.028));

                // 8. Дизеринг Байера 8x8 и квантование, строго с умножением на shadowMask.
                //    Матрица привязана к пикселю исходной 540p-картинки, а не экрана.
                uint2 pixelCoord = uint2(uv / max(texel, 1e-8));
                float bayer = (GetBayer8x8(pixelCoord, 3) - 0.5) * BayerAmplitude * shadowMask;
                col = floor(saturate(col + bayer) * ColorSteps + 0.5) / ColorSteps;

                // 9. Зерно Interleaved Gradient Noise, амплитуда 0.018, тоже под маской.
                float ign = IgnNoise(pixelCoord);
                col = saturate(col + ign * GrainAmplitude * shadowMask);

                return float4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}