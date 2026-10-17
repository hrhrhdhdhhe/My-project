// World Crisis | Слой: Core/Visuals | Ответственный: OpenCode
// CrispRetroLit — базовая освещаемая поверхность проекта (Crisp Retro-PBR).
//
// Это URP/Lit, у которого перехвачен только фрагментный шейдер: тени, карты
// нормалей, туман и вся keyword-обвязка URP 17 остаются нетронутыми, а свет
// считается вручную по закреплённой формуле проекта:
//   * альбедо читается из ОБОИХ слотов текстуры (_BaseMap и _MainTex) —
//     их произведение умножается на _BaseColor;
//   * эмбиент по вертикали нормали: пол 0.04..0.05, верх 0.08/0.075/0.07;
//   * Blinn-Phong блик: pow(NdotH, exp2(4.5 + smoothness*4.5)) * NdotL * atten
//     * smoothness*0.45, гладкость берётся из surfaceData (карта/константа);
//   * эмиссия = карта × _EmissionColor ВСЕГДА, поэтому у материалов без свечения
//     связка обязана быть чёрной (RetroPbrSet.CreateMaterial выставляет сам);
//   * если max(эмиссии) > 0.15 — экран/лампа рисуется как albedo*0.15 + эмиссия,
//     иначе lit = albedo*diffuseSum + specSum, затем lit /= (1 + lit*0.45).
//
// Важно: набор СВОЙСТВ материала и CBUFFER UnityPerMaterial полностью совпадают
// с URP/Lit, поэтому все материалы остаются SRP-Batcher-совместимыми.
// Ретро-глобалы UnityPerRetro объявлены, но фрагментом больше не читаются:
// RetroRenderPipelineController продолжает их выставлять — это осознанный no-op.

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

            // Легаси-слот: альбедо читается из _BaseMap И _MainTex одновременно
            // (Оба слота получают одну и ту же карту через RetroPbrSet.CreateMaterial).
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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
                // Общий множитель альбедо. Выставляется в 1.0: при 1.45 бетон и цветные
                // корпуса телефонов выжигались в светло-бежевую кашу, а шейдер не мог
                // ни вернуть насыщенность, ни отделить тень от подсветки.
                half _RetroAlbedoGain;
            CBUFFER_END

            // Глобальные ретро-параметры выше остаются объявленными ради совместимости
            // с RetroRenderPipelineController, но фрагментом больше не читаются.

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

                // --- Закреплённая формула освещения проекта ---------------------------------

                // Альбедо читается из ОБОИХ слотов и умножается на _BaseColor. Оба слота
                // получают одну и ту же карту (CreateMaterial), поэтому произведение
                // текстур = квадрат альбедо: так закреплено формулой, менять нельзя.
                // Ветка _Color из исходной формулы свернулась: RetroPbrSet.CreateMaterial
                // всегда кладёт _BaseColor.a = 1, а _Color не входит в UnityPerMaterial URP.
                half3 texBase = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half3 texMain = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                half3 baseCol = texBase.rgb * texMain.rgb * _BaseColor.rgb;

                // Эмиссия всегда считается как карта × цвет: у материалов без свечения
                // CreateMaterial обязан выставлять чёрную карту и чёрный _EmissionColor.
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;

                half3 normalWS = normalize(inputData.normalWS);
                half3 viewDirWS = inputData.viewDirectionWS;
                half smoothness = surfaceData.smoothness;

                // Базовый эмбиент по вертикали нормали: тёмный низ, чуть светлее верх.
                half up = normalWS.y * 0.5h + 0.5h;
                half3 diffuseSum = lerp(half3(0.04h, 0.04h, 0.05h), half3(0.08h, 0.075h, 0.07h), up);
                half3 specSum = half3(0.0h, 0.0h, 0.0h);

                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
                        half atten = light.distanceAttenuation * light.shadowAttenuation;
                        half NdotL = saturate(dot(normalWS, light.direction));
                        diffuseSum += light.color * (NdotL * atten);

                        // Blinn-Phong блик: тёплый отсвет лампы на стали.
                        half3 halfDir = normalize(light.direction + viewDirWS);
                        half NdotH = saturate(dot(normalWS, halfDir));
                        half spec = pow(NdotH, exp2(4.5h + smoothness * 4.5h)) * NdotL * atten * (smoothness * 0.45h);
                        specSum += light.color * spec;
                    LIGHT_LOOP_END
                #endif

                // Компрессор: lit /= (1 + lit*0.45) — мягкий потолок сверхъярких пиков.
                half3 lit = baseCol * diffuseSum + specSum;
                lit /= (1.0h + lit * 0.45h);

                // Экраны и лампы: если эмиссия яркая, свет комнат к ним не примешивается.
                half emisStrength = max(emission.r, max(emission.g, emission.b));
                half3 finalRgb;
                if (emisStrength > 0.15h)
                {
                    finalRgb = baseCol * 0.15h + emission;
                }
                else
                {
                    finalRgb = lit + emission;
                }

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
