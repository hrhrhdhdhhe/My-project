// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// RetroBunkerDither — финальный вывод кадра в стиле Crisp Retro-PBR (Art Bible, раздел 6).
// Сцена рендерится в низкое разрешение (960x540) с FilterMode.Point, этот шейдер
// выводит её на экран и придаёт ретро-характер: упорядоченный дизеринг Байера 8x8,
// постеризация цвета, контурный аутлайн, ACES-тонмаппинг, контраст, приглушённая
// насыщенность, сочный bloom на индикаторах, виньетка и зерно.
//
// Шейдер работает и как материал полноэкранного RawImage (вход POSITION/uv из меша Canvas),
// и как обычный fullscreen-блит — вход вершинного шейдера читает меш, а не SV_VertexID.

Shader "WorldCrisis/Retro Bunker Dither"
{
    Properties
    {
        // Источник: низкоразрешённая RenderTexture сцены. Имя обязано быть _MainTex —
        // именно в это свойство UnityEngine.UI.RawImage отдаёт свою текстуру
        // через Graphic.mainTexture, любое другое имя останется незаполненным.
        _MainTex("Retro Source", 2D) = "black" {}
        [HideInInspector] _MainTex_TexelSize("Retro Source Texel Size", Vector) = (0.001, 0.001, 960, 540)

        // Необязательная карта глубины: если ключ _RETRO_DEPTH включён, аутлайн
        // учитывает ещё и перепад глубины, а не только яркость.
        _RetroDepth("Retro Depth (optional)", 2D) = "black" {}

        [Header(Dither and posterize)]
        _DitherStrength("Dither Strength", Range(0, 1)) = 1.0
        _ColorSteps("Color Steps Per Channel", Range(4, 64)) = 28

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineStrength("Outline Strength", Range(0, 2)) = 1.0
        _OutlineWidth("Outline Width (px)", Range(0.5, 4)) = 1.0
        _DepthOutlineStrength("Depth Outline Strength", Range(0, 4)) = 1.0
        _DepthOutlineThreshold("Depth Outline Threshold", Range(0.001, 0.5)) = 0.02

        [Header(Tonemap and grade)]
        _Contrast("Contrast (+25% = 1.25)", Range(0.5, 2)) = 1.25
        _Saturation("Saturation (-15% = 0.85)", Range(0, 2)) = 0.85
        _Exposure("Exposure", Range(0.1, 4)) = 1.0

        [Header(Bloom)]
        _BloomThreshold("Bloom Threshold", Range(0, 2)) = 0.9
        _BloomIntensity("Bloom Intensity", Range(0, 4)) = 1.8
        _BloomRadius("Bloom Radius (px)", Range(1, 32)) = 9.0
        _BloomTint("Bloom Tint", Color) = (1, 1, 1, 1)

        [Header(CRT)]
        _VignetteIntensity("Vignette Intensity", Range(0, 1)) = 0.45
        _VignetteSoftness("Vignette Softness", Range(0.05, 2)) = 0.9
        _GrainIntensity("Grain Intensity", Range(0, 1)) = 0.35
        _GrainScale("Grain Scale", Range(1, 64)) = 1.0
        _ScanlineIntensity("Scanline Intensity", Range(0, 1)) = 0.12
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
                half4 _OutlineColor;
                half _DitherStrength;
                half _ColorSteps;
                half _OutlineStrength;
                half _OutlineWidth;
                half _DepthOutlineStrength;
                half _DepthOutlineThreshold;
                half _Contrast;
                half _Saturation;
                half _Exposure;
                half _BloomThreshold;
                half _BloomIntensity;
                half _BloomRadius;
                half4 _BloomTint;
                half _VignetteIntensity;
                half _VignetteSoftness;
                half _GrainIntensity;
                half _GrainScale;
                half _ScanlineIntensity;
            CBUFFER_END

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

            half Luminance(half3 color)
            {
                return dot(color, half3(0.2126, 0.7152, 0.0722));
            }

            /// Упорядоченный дизеринг Байера: матрица 2^bits x 2^bits считается
            /// битовым чередованием координат пикселя, без выборки из массива.
            half OrderedDither(uint2 pixel, int bits)
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

                // Внимание: дробный литерал нельзя писать с суффиксом u (0.5u) — такой литерал
                // не существует в HLSL, и компилятор роняет всю функцию. Поэтому 0.5f.
                return (half)(index + 0.5f) / (half)(1u << (2 * bits));
            }

            /// Приближённая ACES-кривая (Narkowicz): удерживает яркие эмиссии
            /// без выжигания в белое, ради чего в Art Bible и выбран ACES.
            half3 AcesFilm(half3 x)
            {
                const half a = 2.51h;
                const half b = 0.03h;
                const half c = 2.43h;
                const half d = 0.59h;
                const half e = 0.14h;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            half3 SampleSource(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
            }

            /// Яркостная составляющая для порога bloom: мягкое «колено».
            half BrightPassWeight(half3 color)
            {
                half brightness = max(color.r, max(color.g, color.b));
                half knee = max(1e-4, _BloomThreshold * 0.5);
                half soft = clamp(brightness - _BloomThreshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                return max(soft, brightness - _BloomThreshold) / max(brightness, 1e-4);
            }

            /// Однопроходный «дешёвый» bloom: яркая выборка + спиральный сбор.
            /// Для индикаторов (лампы, экраны, неоновые кольца) этого достаточно,
            /// а полноценное размытие в два прохода стоило бы лишнего RT.
            half3 SampleBloom(float2 uv, float2 texel)
            {
                const int TAPS = 12;
                float angleStep = 2.39996323; // золотой угол — равномерное покрытие диска
                float radius = _BloomRadius;
                half3 sum = half3(0, 0, 0);
                half weightSum = 0;

                [unroll]
                for (int i = 0; i < TAPS; i++)
                {
                    float fi = (float)i;
                    float r = sqrt((fi + 0.5) / (float)TAPS) * radius;
                    float a = fi * angleStep + _Time.y * 0.15;
                    float2 offset = float2(cos(a), sin(a)) * r * texel;
                    half3 sample = SampleSource(uv + offset);
                    half w = BrightPassWeight(sample);
                    sum += sample * w;
                    weightSum += w;
                }

                return weightSum > 1e-4 ? sum / weightSum : half3(0, 0, 0);
            }

            /// Соберает контур по соседним пикселям: яркостный градиент + градиент глубины.
            half ComputeOutline(float2 uv, float2 texel, float centerLuminance, float centerDepth)
            {
                float2 o = texel * _OutlineWidth;

                half l1 = Luminance(SampleSource(uv + float2(o.x, 0)));
                half l2 = Luminance(SampleSource(uv - float2(o.x, 0)));
                half l3 = Luminance(SampleSource(uv + float2(0, o.y)));
                half l4 = Luminance(SampleSource(uv - float2(0, o.y)));

                // Перепад яркости между соседями — этого достаточно для силуэтов предметов.
                half dx = abs(l1 - l2);
                half dy = abs(l3 - l4);
                half luminanceEdge = (dx + dy) * 2.0;

                // Диагонали ловят ступеньки на наклонных гранях.
                half d1 = abs(Luminance(SampleSource(uv + o)) - Luminance(SampleSource(uv - o)));
                half d2 = abs(Luminance(SampleSource(uv + float2(o.x, -o.y)))
                            - Luminance(SampleSource(uv + float2(-o.x, o.y))));
                luminanceEdge = max(luminanceEdge, (d1 + d2));

                half edge = saturate(luminanceEdge * _OutlineStrength - 0.12);

                #if defined(_RETRO_DEPTH)
                    // Линейная глубина снимает проплешины на однотонных поверхностях.
                    float z1 = LinearEyeDepth(SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv + float2(o.x, 0)).r, _ZBufferParams);
                    float z2 = LinearEyeDepth(SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv - float2(o.x, 0)).r, _ZBufferParams);
                    float z3 = LinearEyeDepth(SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv + float2(0, o.y)).r, _ZBufferParams);
                    float z4 = LinearEyeDepth(SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv - float2(0, o.y)).r, _ZBufferParams);

                    float zMax = max(max(z1, z2), max(z3, z4));
                    float zMin = min(min(z1, z2), min(z3, z4));
                    float relative = (zMax - zMin) / max(centerDepth, 0.01);
                    edge = max(edge, saturate((relative - _DepthOutlineThreshold) * _DepthOutlineStrength));
                #endif

                return edge;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 RetroFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float2 texel = _MainTex_TexelSize.xy;

                half3 scene = SampleSource(uv);

                // --- 1. Bloom по индикаторам (до тонмаппинга, в линейном свете) ---
                #if defined(_RETRO_DEPTH)
                    float centerDepth = LinearEyeDepth(
                        SAMPLE_TEXTURE2D(_RetroDepth, sampler_RetroDepth, uv).r, _ZBufferParams);
                #else
                    float centerDepth = 1.0;
                #endif

                half3 bloom = SampleBloom(uv, texel);
                half3 color = scene + bloom * (_BloomIntensity * _BloomTint.rgb);

                // --- 2. Экспозиция и ACES-тонмаппинг ---
                color = AcesFilm(color * _Exposure);

                // --- 3. Контраст (+25%) и приглушённая насыщенность (-15%) ---
                color = saturate((color - 0.5) * _Contrast + 0.5);
                half luma = Luminance(color);
                color = lerp(half3(luma, luma, luma), color, _Saturation);

                // --- 4. Контурный аутлайн (после тонмаппинга, чтобы линии оставались чёрными) ---
                half outline = ComputeOutline(uv, texel, luma, centerDepth);
                color = lerp(color, _OutlineColor.rgb, saturate(outline));

                // --- 5. Виньетка ЭЛТ ---
                // Aspect берётся как width/height: иначе виньетка схлопывается в угол,
                // потому что uv уже в нормализованных координатах.
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 centered = uv * 2.0 - 1.0;
                centered.x *= aspect;
                float radial = saturate(dot(centered, centered) * 0.5);
                half vignette = 1.0 - _VignetteIntensity * pow(radial, _VignetteSoftness);
                color *= vignette;

                // --- 6. Строчная развёртка катода ---
                half scanline = 1.0 - _ScanlineIntensity * (0.5 + 0.5 * sin(uv.y * _MainTex_TexelSize.w * 3.14159));
                color *= scanline;

                // --- 7. Зерно ---
                float2 grainCoord = floor(input.positionCS.xy * _GrainScale);
                half grain = Hash21(grainCoord + _Time.y * 37.0) - 0.5;
                color += grain * _GrainIntensity * 0.12;

                // --- 8. Дизеринг Байера 8x8 и постеризация до _ColorSteps уровней ---
                // Матрица привязана к пикселю ИСХОДНОЙ 540p-картинки, а не к пикселю экрана:
                // на 1080p один пиксель источника покрывает два экранных, и привязка к экрану
                // сделала бы узор вдвое мельче задуманного.
                uint2 pixel = uint2(uv / max(texel, 1e-8));
                half dither = (OrderedDither(pixel, 3) - 0.5) * _DitherStrength;
                color = saturate(color + dither * (1.0 / max(2.0, _ColorSteps)));
                half steps = max(2.0, (half)_ColorSteps);
                color = floor(color * steps + 0.5) / steps;

                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}