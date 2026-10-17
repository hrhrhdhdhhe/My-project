// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// CrispRetroLit — базовая освещаемая поверхность проекта в стиле Crisp Retro-PBR.
//
// Это URP/Lit, у которого перехвачен только фрагментный шейдер: освещение, тени,
// отражения, карты нормалей и туман берутся из URP без изменений (вместе со всеми
// keyword-вариантами URP 17), а поверх них добавляется ретро-обработка:
//   * тайловая карта грязи/царапин, гасящая блик и подъедающая альбедо;
//   * контрастные металлические блики (повышенная гладкость + френелев ободок);
//   * яркая эмиссия в фирменных цветах #FFB000 / #00E676 / #FF1744.
//
// Важно: набор СВОЙСТВ материала и CBUFFER UnityPerMaterial полностью совпадают
// с URP/Lit, поэтому все материалы остаются SRP-Batcher-совместимыми.
// Ретро-параметры вынесены в ГЛОБАЛЬНЫЙ буфер UnityPerRetro: внешний вид — свойство
// сцены, а не материала, и его один раз выставляет RetroRenderPipelineController.

Shader "WorldCrisis/Crisp Retro Lit"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [ToggleOff] _ScreenSpaceReflections("Screen Space Reflections", Float) = 1.0
        [ToggleOff] _ScreenSpaceReflectionsContributeTransparent("Screen Space Reflections Contribute Transparent", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        // Editmode props
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass — единственный переписанный участок шейдера.
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex LitPassVertex
            #pragma fragment RetroLitPassFragment

            // -------------------------------------
            // Material Keywords (1:1 как в URP/Lit — иначе ломается SRP Batcher)
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SCREENSPACEREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // -------------------------------------
            // Universal Pipeline keywords
#if defined(UNITY_PLATFORM_META_QUEST)
            #pragma multi_compile _ META_QUEST_LIGHTUNROLL
#endif
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_REFLECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#if defined(UNITY_PLATFORM_META_QUEST)
            #pragma multi_compile _ META_QUEST_ORTHO_PROJ
            #pragma multi_compile _ META_QUEST_NO_SPOTLIGHTS_LIGHT_LOOP
#endif
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile_fragment _ REFLECTION_PROBE_ROTATION
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            // ------------------------------------------------------------------------
            //  Crisp Retro-PBR: глобальные параметры внешнего вида.
            //  Их выставляет RetroRenderPipelineController через Shader.SetGlobal*.
            // ------------------------------------------------------------------------
            TEXTURE2D(_RetroDirtMap);
            SAMPLER(sampler_RetroDirtMap);

            CBUFFER_START(UnityPerRetro)
                // xy — тайлинг грязи, zw — сетка разбивки эмиссии
                float4 _RetroDirtTiling;
                float4 _RetroRimColor;
                float4 _RetroEmissionColor;
                // x — сила грязи, y — насколько грязь убивает блик
                half4 _RetroDirt;
                // x — контраст блика, y — нижняя граница гладкости,
                // z — усиление эмиссии, w — разбивка эмиссии пятнами
                half4 _RetroSpec;
                // x — сила френелева ободка, y — степень ободка, z — общий множитель эмиссии
                half4 _RetroRim;
                // Общий множитель альбедо: процедурные текстуры намеренно тёмные,
                // в закрытой кабине без GI им нужен запас, иначе всё уходит в чёрный.
                half _RetroAlbedoGain;
            CBUFFER_END

            /// Дешёвый детерминированный хеш по ячейке сетки — ломает плоскую эмиссию пятнами.
            half RetroCellNoise(float2 cell)
            {
                return frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
            }

            /// Ретро-обработка поверхности: грязь, контрастный блик, ободок, эмиссия.
            /// inout обязателен: SurfaceData по умолчанию передаётся по значению, и без inout
            /// все правки внутри молча терялись бы — материал выглядел бы как обычный Lit.
            void RetroApplySurface(inout SurfaceData surfaceData, float2 uv, half3 normalWS, half3 viewDirWS)
            {
                // Закрытая кабина без запечённых кубических карт: высокий Metallic там
                // гасит диффузную составляющую, и объект уходит в чёрный. Отражаться
                // нечему, поэтому металличность держим низкой, а читаемость даёт альбедо.
                half3 lit = surfaceData.albedo * (1.0h - surfaceData.metallic * 0.5h);
                lit = max(lit, surfaceData.albedo * 0.85h);
                surfaceData.albedo = saturate(lit * _RetroAlbedoGain);

                half dirt = 0.0;

                if (_RetroDirt.x > 0.001)
                {
                    // Грязь тайлится по UV материала: мелкие детали и крупные корпуса
                    // автоматически получают разный масштаб царапин при одном global-тайлинге.
                    float2 dirtUV = uv * _RetroDirtTiling.xy;
                    dirt = saturate(SAMPLE_TEXTURE2D(_RetroDirtMap, sampler_RetroDirtMap, dirtUV).r * _RetroDirt.x);

                    // Грязь темнее и полностью снимает блик: именно так выглядит потёртый металл.
                    surfaceData.albedo = lerp(surfaceData.albedo, surfaceData.albedo * 0.35h, dirt);
                    surfaceData.smoothness = lerp(surfaceData.smoothness, min(surfaceData.smoothness, 0.12h), dirt * _RetroDirt.y);
                    surfaceData.metallic = lerp(surfaceData.metallic, surfaceData.metallic * 0.6h, dirt * _RetroDirt.y);
                    surfaceData.occlusion = lerp(surfaceData.occlusion, surfaceData.occlusion * 0.75h, dirt);
                }

                // Контрастный металлический блик: гладкость поднимается, но грязь держит низ.
                half clean = 1.0 - dirt;
                surfaceData.smoothness = saturate(surfaceData.smoothness * _RetroSpec.x + _RetroSpec.y * clean);

                // Эмиссия: усиливаем и разбиваем пятнами, чтобы лампы «дышали».
                if (_RetroSpec.z > 0.001)
                {
                    float2 cell = floor(uv * max(_RetroDirtTiling.zw, 1e-3));
                    half breakup = lerp(1.0h, RetroCellNoise(cell), _RetroSpec.w);
                    surfaceData.emission = surfaceData.emission * (_RetroSpec.z * breakup)
                                         + _RetroEmissionColor.rgb * (_RetroRim.z * 0.25h);
                }

                // Френелев ободок: тонкая светящаяся кромка на силуэтах — «акварельный» контур.
                if (_RetroRim.x > 0.001)
                {
                    half fresnel = pow(1.0h - saturate(dot(normalWS, viewDirWS)), max(_RetroRim.y, 0.01h));
                    surfaceData.emission += _RetroRimColor.rgb * (fresnel * _RetroRim.x * surfaceData.metallic);
                }
            }

            /// Вклад одного источника света с жёстким зажимом.
            /// Без clamp сумма ламп в бункере превышала единицу в десятки раз
            /// и выжигала поверхности в чисто белый и кислотно-розовый.
            half3 AccumulateLight(Light light, half3 normalWS)
            {
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, light.direction));
                return min(light.color * (ndotl * attenuation), half3(1.15, 1.15, 1.15));
            }

            void RetroLitPassFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);

                #ifdef LOD_FADE_CROSSFADE
                    LODFadeCrossFade(input.positionCS);
                #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

                #if defined(_DBUFFER)
                    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
                #endif

                InitializeBakedGIData(input, inputData);

                RetroApplySurface(surfaceData, input.uv, inputData.normalWS, inputData.viewDirectionWS);

                // Освещение считается вручную: UniversalFragmentPBR складывает вклад
                // источников без ограничения, а нам нужен потолок по каждой лампе.
                half3 normalWS = normalize(inputData.normalWS);
                half3 viewDirWS = inputData.viewDirectionWS;
                half ndotv = saturate(dot(normalWS, viewDirWS));

                Light mainLight = GetMainLight(inputData.shadowCoord);
                half3 lightContrib = AccumulateLight(mainLight, normalWS);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                        lightContrib += AccumulateLight(light, normalWS);
                    LIGHT_LOOP_END
                #endif

                // Гарантированный полусферический свет: ни один объект не уходит
                // в абсолютный чёрный даже при выключенных лампах.
                half upFactor = normalWS.y * 0.5h + 0.5h;
                half3 ambientFloor = lerp(half3(0.18h, 0.16h, 0.15h), half3(0.32h, 0.29h, 0.25h), upFactor);

                // Лёгкая подсветка со стороны камеры, чтобы тёмные бока читались.
                half viewFill = ndotv * 0.16h;

                half3 totalLighting = ambientFloor + viewFill + lightContrib;

                // Мягкий тональный компрессор ДО умножения на albedo: гарантирует
                // и потолок (не выжжено в белое), и пол (не ушло в #000000).
                totalLighting /= (1.0h + totalLighting * 0.55h);
                totalLighting = clamp(totalLighting, half3(0.18h, 0.16h, 0.15h), half3(1.15h, 1.12h, 1.08h));

                half3 finalRgb = surfaceData.albedo * totalLighting + surfaceData.emission;

                outColor.rgb = MixFog(finalRgb, inputData.fogCoord);
                outColor.a = OutputAlpha(surfaceData.alpha, IsSurfaceTypeTransparent());

                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  Служебные проходы берём из URP/Lit: тени, глубина, нормали, GI-мета.
        // ------------------------------------------------------------------
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
        UsePass "Universal Render Pipeline/Lit/Meta"
        UsePass "Universal Render Pipeline/Lit/GBuffer"
        UsePass "Universal Render Pipeline/Lit/MotionVectors"
        UsePass "Universal Render Pipeline/Lit/XRMotionVectors"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}