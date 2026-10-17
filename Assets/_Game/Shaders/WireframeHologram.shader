// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// WireframeHologram — проволочные голограммы проекта: купола ПВО (AAShield_Dome),
// голографические архипелаги на столе саммита, карта мира и ядерный «гриб».
//
// Проволока строится не по UV (их масштаб зависит от примитива), а по объектным
// полярным координатам: долготы дают меридианы, высота — параллели, а их сумма —
// ромбовидную сетку. Все линии сглаживаются через fwidth, поэтому на 960x540
// сетка остаётся ровной и не «дрожит».
//
// Смешивание аддитивное, туман не применяется: вместо него — затухание по глубине,
// чтобы дальние голограммы гасли сами.

Shader "WorldCrisis/Wireframe Hologram"
{
    Properties
    {
        [HDR] _GlowColor("Glow Color", Color) = (0, 1, 0.7, 1)
        _GlowColorCore("Core Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Range(0, 8)) = 1.6

        [Header(Wireframe grid)]
        _RadialLines("Meridian Lines", Range(1, 64)) = 24
        _HeightLines("Parallel Lines", Range(1, 64)) = 12
        _DiagonalStrength("Diagonal Mesh Strength", Range(0, 2)) = 0.6
        _LineWidth("Line Width", Range(0.02, 0.6)) = 0.12
        _GridIntensity("Grid Intensity", Range(0, 4)) = 1.0

        [Header(Scanlines)]
        _ScanDensity("Scanline Density", Range(0.5, 32)) = 8
        _ScanSpeed("Scanline Speed", Range(-4, 4)) = 0.6
        _ScanStrength("Scanline Strength", Range(0, 3)) = 1.2
        _ScanWidth("Scanline Width", Range(0.01, 0.8)) = 0.12

        [Header(Look)]
        _FresnelStrength("Edge Glow Strength", Range(0, 4)) = 1.2
        _FresnelPower("Edge Glow Power", Range(0.5, 12)) = 2.5
        _FadeStart("Fade Start", Float) = 6.0
        _FadeEnd("Fade End", Float) = 22.0
        _GlobalFade("Global Fade", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            // Без LightMode — рисуется в прозрачном проходе URP.
        }

        Pass
        {
            Name "WireframeHologram"

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex RetroHologramVert
            #pragma fragment RetroHologramFrag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_RetroDirtMap);
            SAMPLER(sampler_RetroDirtMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float4 _GlowColorCore;
                half _Intensity;
                half _RadialLines;
                half _HeightLines;
                half _DiagonalStrength;
                half _LineWidth;
                half _GridIntensity;
                half _ScanDensity;
                half _ScanSpeed;
                half _ScanStrength;
                half _ScanWidth;
                half _FresnelStrength;
                half _FresnelPower;
                float _FadeStart;
                float _FadeEnd;
                half _GlobalFade;
            CBUFFER_END

            // Общие параметры внешнего вида сцены — те же глобалы, что у CrispRetroLit,
            // поэтому внешний вид всех голограмм задаётся из одного места.
            CBUFFER_START(UnityPerRetro)
                float4 _RetroDirtTiling;
                half4 _RetroDirt;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings RetroHologramVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            /// Сглаженная линия сетки: расстояние до ближайшего узла, нормализованное
            /// производной шириной пикселя — линия одинаково тонкая и на близких, и на далёких гранях.
            float GridLine(float coordinate, float lines, float width)
            {
                float f = coordinate * lines;
                float distance = abs(frac(f) - 0.5);

                // Нижняя граница производной обязательна: на грани, почти параллельной линии
                // сетки, fwidth стремится к нулю, линия схлопывается в ноль и просто исчезает.
                float pixel = max(fwidth(f), 0.02);
                return 1.0 - smoothstep(width * pixel, width * pixel * 2.5, distance);
            }

            half4 RetroHologramFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionOS = input.positionOS;

                // Полярные координаты объекта: долгота и высота.
                float angle = atan2(positionOS.z, positionOS.x) * 0.15915494; // /2π → -0.5..0.5
                float height = positionOS.y;

                // Сетка: меридианы, параллели и ромбовидная диагональная сетка поверх них.
                float meridian = GridLine(angle, _RadialLines, _LineWidth);
                float parallel = GridLine(height, _HeightLines, _LineWidth);
                float diagonal = max(
                    GridLine(angle + height, _RadialLines, _LineWidth),
                    GridLine(angle - height, _RadialLines, _LineWidth));

                float grid = saturate(meridian + parallel + diagonal * _DiagonalStrength) * _GridIntensity;

                // Бегущая полоса развёртки: движется вверх по высоте.
                float scanCoord = height * _ScanDensity - _Time.y * _ScanSpeed;
                float scanDistance = abs(frac(scanCoord) - 0.5);
                float scanPixel = max(fwidth(scanCoord), 0.02);
                float scan = (1.0 - smoothstep(_ScanWidth * scanPixel * 0.5, _ScanWidth * scanPixel * 2.0, scanDistance))
                             * _ScanStrength;

                // Френелев ободок: силуэт голограммы горит ярче сетки — «объём» без геометрии.
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), max(_FresnelPower, 0.01h));

                // Затухание по глубине вместо тумана: аддитивный блендинг с туманом выбивается.
                float eyeDepth = distance(input.positionWS, GetCameraPositionWS());
                float depthFade = saturate((_FadeEnd - eyeDepth) / max(_FadeEnd - _FadeStart, 1e-3));

                // Лёгкая зернистость пятен: голограмма не выглядит «стерильной заливкой».
                float2 noiseUV = input.uv * _RetroDirtTiling.xy + float2(0.0, _Time.y * 0.05);
                half noise = SAMPLE_TEXTURE2D(_RetroDirtMap, sampler_RetroDirtMap, noiseUV).g;
                half flicker = lerp(1.0h, 0.6h + noise * 0.8h, saturate(_RetroDirt.x));

                half3 color = _GlowColor.rgb * (grid + scan);
                color += _GlowColorCore.rgb * (grid * grid * 0.8h);
                color += _GlowColor.rgb * (fresnel * _FresnelStrength);
                color *= _Intensity * depthFade * _GlobalFade * flicker;

                half alpha = saturate((grid * 0.7h + scan * 0.5h + fresnel) * depthFade) * _GlobalFade;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}